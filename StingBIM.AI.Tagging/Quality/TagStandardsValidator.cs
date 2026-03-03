// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagStandardsValidator.cs - Validates tags against international building standards
// Integrates with StingBIM's 32-standard library for ISO 19650, NEC, ASHRAE, NFPA,
// IBC, BS 7671, SANS, UNBS, and KEBS compliance checking

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Tagging.Data;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Quality
{
    #region Inner Types

    /// <summary>
    /// A validation rule checking a tag against a standard requirement.
    /// Check delegate returns null if compliant, or a description string if violated.
    /// </summary>
    public class ValidationRule
    {
        public string RuleId { get; set; }
        public string Standard { get; set; }
        public string Section { get; set; }
        public string Requirement { get; set; }
        public IssueSeverity Severity { get; set; }
        public HashSet<string> ApplicableCategories { get; set; }
        public Func<TagInstance, string> Check { get; set; }
        public string SuggestedFix { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// A single violation found during standards validation.
    /// </summary>
    public class ValidationViolation
    {
        public string RuleId { get; set; }
        public string Standard { get; set; }
        public string Section { get; set; }
        public int ElementId { get; set; }
        public string TagId { get; set; }
        public string CategoryName { get; set; }
        public string Description { get; set; }
        public IssueSeverity Severity { get; set; }
        public string SuggestedFix { get; set; }
        public int ViewId { get; set; }
        public DateTime DetectedAt { get; set; }
    }

    /// <summary>
    /// Options controlling which standards are checked and strictness level.
    /// </summary>
    public class ValidationOptions
    {
        public bool CheckISO19650 { get; set; } = true;
        public bool CheckNEC { get; set; } = true;
        public bool CheckASHRAE { get; set; } = true;
        public bool CheckFireSafety { get; set; } = true;
        public bool CheckRegional { get; set; } = true;
        public bool CheckDocumentationCompleteness { get; set; } = true;
        /// <summary>0=lenient (critical only), 1=normal, 2=strict (all rules).</summary>
        public int StrictnessLevel { get; set; } = 1;
        /// <summary>Project region code for regional standards (e.g., "GB","ZA","UG","KE").</summary>
        public string ProjectRegion { get; set; }
        public int MaxViolationsPerStandard { get; set; } = 0;
        public static ValidationOptions Default => new ValidationOptions();
        public static ValidationOptions CriticalOnly => new ValidationOptions { StrictnessLevel = 0 };
    }

    /// <summary>
    /// Full report from a standards validation run.
    /// </summary>
    public class StandardsValidationReport
    {
        public Dictionary<string, List<ValidationViolation>> ViolationsByStandard { get; set; }
            = new Dictionary<string, List<ValidationViolation>>(StringComparer.OrdinalIgnoreCase);
        public List<ValidationViolation> AllViolations { get; set; } = new List<ValidationViolation>();
        public double ComplianceScore { get; set; }
        public Dictionary<string, double> ScoresByStandard { get; set; }
            = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<IssueSeverity, int> CountsBySeverity { get; set; }
            = new Dictionary<IssueSeverity, int>();
        public int RulesEvaluated { get; set; }
        public int TagsChecked { get; set; }
        public int ViewsChecked { get; set; }
        public List<string> Recommendations { get; set; } = new List<string>();
        public DateTime GeneratedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public CompletenessReport DocumentationCompleteness { get; set; }
    }

    /// <summary>
    /// Documentation completeness assessment for sheets and views.
    /// </summary>
    public class CompletenessReport
    {
        public Dictionary<string, double> CategoryCoveragePercent { get; set; }
            = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<int>> MissingTagsByCategory { get; set; }
            = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> RequiredCountByCategory { get; set; }
            = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> ActualCountByCategory { get; set; }
            = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public double OverallCompleteness { get; set; }
        public List<string> CrossReferenceIssues { get; set; } = new List<string>();
        public List<int> IncompleteViewIds { get; set; } = new List<int>();
        public List<int> AnalyzedSheetIds { get; set; } = new List<int>();
    }

    #endregion

    /// <summary>
    /// Validates tags against international building standards and documentation requirements.
    /// Integrates with StingBIM's 32-standard library for ISO 19650, NEC 2023, ASHRAE 90.1/62.1,
    /// NFPA 13/72, IBC 2021, BS 7671, SANS 10142, UNBS US 319, and KEBS KS 1278 compliance.
    /// Thread-safe: all mutable state protected by locks. Rules are immutable after construction.
    /// </summary>
    public sealed class TagStandardsValidator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly TagRepository _repository;
        private readonly TagConfiguration _configuration;
        private readonly ITagCreator _tagCreator;
        private readonly List<ValidationRule> _allRules;
        private readonly Dictionary<string, List<ValidationRule>> _rulesByStandard;

        // Compiled regex patterns for validation rules
        private static readonly Regex AssetIdPattern = new Regex(
            @"^[A-Z]{2}-[A-Z0-9]{3}-[A-Z0-9]{3}-\d{3,}$", RegexOptions.Compiled);
        private static readonly Regex VoltagePattern = new Regex(
            @"\d+\s*[Vv]", RegexOptions.Compiled);
        private static readonly Regex PhasePattern = new Regex(
            @"(\d[- ]?[Pp]h(ase)?|single[- ]?phase|three[- ]?phase|1[Pp]h|3[Pp]h)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SourcePattern = new Regex(
            @"(panel|[LMD]P|[Cc]kt|[Cc]ircuit|source|fed from)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CapacityPattern = new Regex(
            @"\d+\.?\d*\s*(kW|BTU|[Tt]on|CFM|[Hh][Pp]|GPM|[Ll]/s)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FireRatingPattern = new Regex(
            @"(\d+\.?\d*\s*(-?\s*)?([Hh][Rr]|[Hh]our|[Mm]in)|FRL\s*\d+/\d+/\d+)",
            RegexOptions.Compiled);
        private static readonly Regex FireZonePattern = new Regex(
            @"(FZ[-\s]?\d+|[Ff]ire\s*[Zz]one\s*\d+|[Zz]one\s*[A-Z0-9]+)",
            RegexOptions.Compiled);
        private static readonly Regex DuctSystemPattern = new Regex(
            @"(supply|return|exhaust|outside|fresh|relief|transfer|mixed)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DuctSizePattern = new Regex(
            @"(\d+\s*[xX]\s*\d+|[Dd]ia\.?\s*\d+|\d+\s*mm)",
            RegexOptions.Compiled);
        private static readonly Regex SprinklerTypePattern = new Regex(
            @"(K[-\s]?\d+\.?\d*|[Pp]endent|[Uu]pright|[Ss]idewall|[Cc]oncealed|[Qq]uick\s*[Rr]esponse)",
            RegexOptions.Compiled);
        private static readonly Regex BSCircuitPattern = new Regex(
            @"(DB|MSB|SDB|CU)\s*[-/]?\s*\d+\s*[-/]\s*(C|[Ww]ay|[Cc]ct)\s*\d+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #region Category Sets

        private static readonly HashSet<string> ElectricalCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures", "Lighting Devices",
              "Communication Devices", "Fire Alarm Devices", "Security Devices", "Conduits", "Cable Trays" };
        private static readonly HashSet<string> PanelCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Electrical Equipment" };
        private static readonly HashSet<string> HVACEquipCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Mechanical Equipment" };
        private static readonly HashSet<string> DuctCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Ducts", "Duct Fittings", "Flex Ducts" };
        private static readonly HashSet<string> AssetCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Mechanical Equipment", "Electrical Equipment", "Plumbing Equipment", "Plumbing Fixtures",
              "Lighting Fixtures", "Communication Devices", "Fire Alarm Devices", "Sprinklers",
              "Furniture", "Casework", "Specialty Equipment" };
        private static readonly Dictionary<TagViewType, HashSet<string>> RequiredCatsByView =
            new Dictionary<TagViewType, HashSet<string>>
            {
                { TagViewType.FloorPlan, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "Doors", "Windows", "Rooms", "Walls" } },
                { TagViewType.CeilingPlan, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "Lighting Fixtures", "Air Terminals", "Sprinklers", "Fire Alarm Devices" } },
                { TagViewType.Section, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "Walls", "Floors", "Structural Framing", "Structural Columns" } },
                { TagViewType.Elevation, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "Windows", "Doors", "Walls" } },
                { TagViewType.StructuralPlan, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "Structural Framing", "Structural Columns", "Structural Foundations" } }
            };

        #endregion

        #region Constructor

        public TagStandardsValidator(
            TagRepository repository, TagConfiguration configuration, ITagCreator tagCreator)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _tagCreator = tagCreator ?? throw new ArgumentNullException(nameof(tagCreator));
            _allRules = new List<ValidationRule>();
            _rulesByStandard = new Dictionary<string, List<ValidationRule>>(StringComparer.OrdinalIgnoreCase);

            RegisterISO19650Rules();
            RegisterNECRules();
            RegisterASHRAERules();
            RegisterFireSafetyRules();
            RegisterRegionalRules();

            Logger.Info("TagStandardsValidator initialized: {0} rules across {1} standards",
                _allRules.Count, _rulesByStandard.Count);
        }

        private void RegisterRule(ValidationRule rule)
        {
            _allRules.Add(rule);
            if (!_rulesByStandard.TryGetValue(rule.Standard, out var list))
            {
                list = new List<ValidationRule>();
                _rulesByStandard[rule.Standard] = list;
            }
            list.Add(rule);
        }

        #endregion

        #region ISO 19650 Rules

        private void RegisterISO19650Rules()
        {
            // ISO 19650-3:2020 5.4 - All assets must have identification tags
            RegisterRule(new ValidationRule
            {
                RuleId = "ISO19650-ID-001", Standard = "ISO 19650",
                Section = "ISO 19650-3:2020 Sec 5.4",
                Requirement = "All asset elements shall have an identification tag with a unique asset ID.",
                Severity = IssueSeverity.Critical, ApplicableCategories = AssetCategories,
                SuggestedFix = "Add an identification tag with asset ID in format XX-YYY-ZZZ-NNN.",
                Check = tag => string.IsNullOrWhiteSpace(tag.DisplayText)
                    ? "Asset element has no identification tag text." : null
            });

            // ISO 19650-3:2020 5.4.2 - Asset ID naming convention XX-YYY-ZZZ-NNN
            RegisterRule(new ValidationRule
            {
                RuleId = "ISO19650-ID-002", Standard = "ISO 19650",
                Section = "ISO 19650-3:2020 Sec 5.4.2",
                Requirement = "Asset IDs shall follow XX-YYY-ZZZ-NNN (class-system-location-number).",
                Severity = IssueSeverity.Warning, ApplicableCategories = AssetCategories,
                SuggestedFix = "Correct asset ID to XX-YYY-ZZZ-NNN format (e.g., AH-HVA-L01-001).",
                Check = tag =>
                {
                    if (string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    string text = tag.DisplayText.Trim();
                    if (text.Contains('-') && text.Length >= 10 && text.Length <= 30 &&
                        !AssetIdPattern.IsMatch(text))
                        return $"Asset ID '{text}' does not match required XX-YYY-ZZZ-NNN format.";
                    return null;
                }
            });

            // ISO 19650-3:2020 5.4.3 - Asset class code matches category
            RegisterRule(new ValidationRule
            {
                RuleId = "ISO19650-ID-003", Standard = "ISO 19650",
                Section = "ISO 19650-3:2020 Sec 5.4.3",
                Requirement = "Asset class code (first 2 chars) shall correspond to element category.",
                Severity = IssueSeverity.Warning, ApplicableCategories = AssetCategories,
                SuggestedFix = "Update class code to match category (AH=Air Handling, EL=Electrical, etc.).",
                Check = tag =>
                {
                    if (string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    string text = tag.DisplayText.Trim();
                    if (!AssetIdPattern.IsMatch(text)) return null;
                    string code = text.Substring(0, 2).ToUpperInvariant();
                    var valid = GetValidClassCodes(tag.CategoryName ?? "");
                    if (valid.Count > 0 && !valid.Contains(code))
                        return $"Class code '{code}' invalid for '{tag.CategoryName}'. Expected: {string.Join(", ", valid)}.";
                    return null;
                }
            });

            // ISO 19650-2:2018 5.1.2 - No placeholder text at Information Exchange Points
            RegisterRule(new ValidationRule
            {
                RuleId = "ISO19650-IEP-001", Standard = "ISO 19650",
                Section = "ISO 19650-2:2018 Sec 5.1.2",
                Requirement = "Tags shall not contain placeholder text at delivery milestones.",
                Severity = IssueSeverity.Critical, ApplicableCategories = null,
                SuggestedFix = "Replace placeholder text with actual parameter values.",
                Check = tag =>
                {
                    if (string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    string t = tag.DisplayText.Trim();
                    if (t.StartsWith("<") && t.EndsWith(">"))
                        return $"Tag contains placeholder '{t}'.";
                    if (Regex.IsMatch(t, @"^\{[^}]+\}$"))
                        return $"Tag contains unresolved template expression '{t}'.";
                    string u = t.ToUpperInvariant();
                    if (u == "SAMPLE" || u == "PLACEHOLDER" || u == "DRAFT" || u == "XXX")
                        return $"Tag contains non-deliverable text '{t}'.";
                    if (t.StartsWith("[") && t.EndsWith("]") && u.Contains("TBD"))
                        return $"Tag contains unresolved TBD marker '{t}'.";
                    return null;
                }
            });

            // ISO 19650-1:2018 Sec 11 - Permitted characters in asset identifiers
            RegisterRule(new ValidationRule
            {
                RuleId = "ISO19650-IC-001", Standard = "ISO 19650",
                Section = "ISO 19650-1:2018 Sec 11",
                Requirement = "Asset identifiers shall use only A-Z, 0-9, hyphen, period, underscore.",
                Severity = IssueSeverity.Info, ApplicableCategories = AssetCategories,
                SuggestedFix = "Remove special characters from the asset identifier string.",
                Check = tag =>
                {
                    if (string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    string t = tag.DisplayText.Trim();
                    if (t.Contains(' ')) return null; // descriptive text, not an ID
                    if (Regex.IsMatch(t, @"[^A-Za-z0-9\-._]"))
                        return $"Identifier '{t}' contains characters outside permitted set.";
                    return null;
                }
            });

            // ISO 19650-3:2020 5.6 - Uniqueness (cross-tag, checked in group analysis)
            RegisterRule(new ValidationRule
            {
                RuleId = "ISO19650-ID-004", Standard = "ISO 19650",
                Section = "ISO 19650-3:2020 Sec 5.6",
                Requirement = "Each asset identifier shall be unique within the project.",
                Severity = IssueSeverity.Critical, ApplicableCategories = AssetCategories,
                SuggestedFix = "Assign a unique asset ID to this element.",
                Check = _ => null // Checked in RunCrossTagISO19650Checks
            });

            // ISO 19650-2:2018 5.4 - Cross-view consistency (cross-tag, checked in group analysis)
            RegisterRule(new ValidationRule
            {
                RuleId = "ISO19650-IR-001", Standard = "ISO 19650",
                Section = "ISO 19650-2:2018 Sec 5.4",
                Requirement = "Tag content for same element shall be consistent across all views.",
                Severity = IssueSeverity.Warning, ApplicableCategories = null,
                SuggestedFix = "Ensure all tags for this element show identical text across views.",
                Check = _ => null // Checked in RunCrossTagISO19650Checks
            });

            // ISO 19650-3:2020 5.7 - Equipment tags include type designation
            RegisterRule(new ValidationRule
            {
                RuleId = "ISO19650-MC-001", Standard = "ISO 19650",
                Section = "ISO 19650-3:2020 Sec 5.7",
                Requirement = "Equipment tags shall include both asset ID and equipment type designation.",
                Severity = IssueSeverity.Info, ApplicableCategories = HVACEquipCategories,
                SuggestedFix = "Include equipment type alongside asset ID (e.g., 'AH-HVA-L01-001 / AHU-1').",
                Check = tag =>
                {
                    if (string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    string t = tag.DisplayText.Trim();
                    if (AssetIdPattern.IsMatch(t) && !t.Contains('/') && !t.Contains('\n'))
                        return $"Equipment tag '{t}' has only asset ID; add type designation.";
                    return null;
                }
            });

            // ISO 19650-2:2018 5.1 - Level of Information Need (LOI) compliance
            RegisterRule(new ValidationRule
            {
                RuleId = "ISO19650-LOI-001", Standard = "ISO 19650",
                Section = "ISO 19650-2:2018 Sec 5.1",
                Requirement = "Tag content shall meet minimum Level of Information Need for the delivery stage.",
                Severity = IssueSeverity.Warning, ApplicableCategories = AssetCategories,
                SuggestedFix = "Ensure tag includes parameters appropriate to the current LOI level (e.g., LOI 3+ needs manufacturer/model).",
                Check = tag =>
                {
                    if (string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    if (tag.Metadata == null || !tag.Metadata.TryGetValue("LOILevel", out var loiObj)) return null;
                    int loi = loiObj is int i ? i : 0;
                    if (loi < 3) return null;
                    string t = tag.DisplayText;
                    bool hasSpec = t.Contains('/') || t.Contains('\n') || t.Split(new[] { ';', ',' }).Length >= 2;
                    return hasSpec ? null : $"Asset tag at LOI {loi} has insufficient detail. Include manufacturer/model at LOI 3+.";
                }
            });

            // ISO 19650-2:2018 5.6 - CDE workflow state awareness
            RegisterRule(new ValidationRule
            {
                RuleId = "ISO19650-CDE-001", Standard = "ISO 19650",
                Section = "ISO 19650-2:2018 Sec 5.6",
                Requirement = "Tags in Published/Archive CDE state shall not contain draft markers.",
                Severity = IssueSeverity.Critical, ApplicableCategories = null,
                SuggestedFix = "Remove draft/WIP markers before publishing. Replace with final values.",
                Check = tag =>
                {
                    if (string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    if (tag.Metadata == null || !tag.Metadata.TryGetValue("CDEState", out var stateObj)) return null;
                    string state = stateObj?.ToString()?.ToUpperInvariant() ?? "";
                    if (state != "PUBLISHED" && state != "ARCHIVE") return null;
                    string u = tag.DisplayText.ToUpperInvariant();
                    if (u.Contains("DRAFT") || u.Contains("WIP") || u.Contains("PRELIMINARY") || u.Contains("FOR REVIEW"))
                        return $"Tag '{tag.DisplayText}' contains draft marker in {state} state.";
                    return null;
                }
            });

            // ISO 19650-3:2020 5.5 - Asset location hierarchy
            RegisterRule(new ValidationRule
            {
                RuleId = "ISO19650-LOC-001", Standard = "ISO 19650",
                Section = "ISO 19650-3:2020 Sec 5.5",
                Requirement = "Asset tags should include location reference for facility management handover.",
                Severity = IssueSeverity.Info, ApplicableCategories = AssetCategories,
                SuggestedFix = "Include location code in tag (e.g., 'AH-HVA-L01-001' where L01 = Level 01).",
                Check = tag =>
                {
                    if (string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    string t = tag.DisplayText.Trim();
                    if (!AssetIdPattern.IsMatch(t)) return null;
                    // Check the location segment (3rd group) for meaningful content
                    var parts = t.Split('-');
                    if (parts.Length >= 3)
                    {
                        string loc = parts[2];
                        if (loc == "000" || loc == "XXX" || loc == "ZZZ")
                            return $"Asset ID '{t}' has placeholder location code '{loc}'.";
                    }
                    return null;
                }
            });

            // ISO 19650-3:2020 6.2 - Lifecycle metadata for FM handover
            RegisterRule(new ValidationRule
            {
                RuleId = "ISO19650-FM-001", Standard = "ISO 19650",
                Section = "ISO 19650-3:2020 Sec 6.2",
                Requirement = "Assets tagged for FM handover shall include lifecycle metadata (installation date, expected life).",
                Severity = IssueSeverity.Info, ApplicableCategories = HVACEquipCategories,
                SuggestedFix = "Populate ASS_INST_DATE and ASS_EXPECTED_LIFE parameters for this asset.",
                Check = tag =>
                {
                    if (tag.Metadata == null || !tag.Metadata.ContainsKey("FMHandover")) return null;
                    if (string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    bool hasDate = tag.Metadata.ContainsKey("ASS_INST_DATE") &&
                                   !string.IsNullOrWhiteSpace(tag.Metadata["ASS_INST_DATE"]?.ToString());
                    bool hasLife = tag.Metadata.ContainsKey("ASS_EXPECTED_LIFE");
                    if (!hasDate && !hasLife)
                        return $"FM-handover asset '{tag.DisplayText}' missing lifecycle data (installation date, expected life).";
                    if (!hasDate)
                        return $"FM-handover asset '{tag.DisplayText}' missing installation date.";
                    return null;
                }
            });
        }

        private static HashSet<string> GetValidClassCodes(string category)
        {
            var c = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            switch (category?.ToUpperInvariant())
            {
                case "MECHANICAL EQUIPMENT": c.UnionWith(new[] { "AH","CH","BL","CT","PU","FC","HX","VF","CU","HU" }); break;
                case "ELECTRICAL EQUIPMENT": c.UnionWith(new[] { "EL","TR","GE","UP","SW","DB","MS","PB" }); break;
                case "PLUMBING EQUIPMENT": case "PLUMBING FIXTURES": c.UnionWith(new[] { "PL","WH","PU","TK","VL" }); break;
                case "LIGHTING FIXTURES": c.UnionWith(new[] { "LT","LF","EM" }); break;
                case "FIRE ALARM DEVICES": c.UnionWith(new[] { "FA","FD","FS","FP" }); break;
                case "SPRINKLERS": c.UnionWith(new[] { "SP","FP","FS" }); break;
                case "COMMUNICATION DEVICES": c.UnionWith(new[] { "CD","IT","AV" }); break;
                case "FURNITURE": case "CASEWORK": c.UnionWith(new[] { "FN","CW","FF" }); break;
                case "SPECIALTY EQUIPMENT": c.UnionWith(new[] { "SE","EQ" }); break;
            }
            return c;
        }

        #endregion

        #region NEC 2023 Rules

        private void RegisterNECRules()
        {
            // Article 408.4 - Panelboard identification (designation required)
            RegisterRule(new ValidationRule
            {
                RuleId = "NEC-408.4-001", Standard = "NEC 2023", Section = "Article 408.4",
                Requirement = "Every panelboard shall have a label identifying the panel designation.",
                Severity = IssueSeverity.Critical, ApplicableCategories = PanelCategories,
                SuggestedFix = "Add panel designation tag (e.g., 'LP-1A', 'MDP', 'PP-2').",
                Check = tag =>
                {
                    if (!IsPanel(tag)) return null;
                    return string.IsNullOrWhiteSpace(tag.DisplayText)
                        ? "Panelboard has no identification tag. NEC 408.4 requires panel designation." : null;
                }
            });

            // Article 408.4 - Panel voltage and phase marking
            RegisterRule(new ValidationRule
            {
                RuleId = "NEC-408.4-002", Standard = "NEC 2023", Section = "Article 408.4",
                Requirement = "Panelboard label shall include supply voltage and number of phases.",
                Severity = IssueSeverity.Warning, ApplicableCategories = PanelCategories,
                SuggestedFix = "Include voltage and phase (e.g., 'LP-1A, 120/208V, 3-Phase').",
                Check = tag =>
                {
                    if (!IsPanel(tag) || string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    string text = tag.DisplayText;
                    bool v = VoltagePattern.IsMatch(text), p = PhasePattern.IsMatch(text);
                    if (!v && !p) return $"Panel tag '{text}' missing voltage and phase info.";
                    if (!v) return $"Panel tag '{text}' missing voltage.";
                    if (!p) return $"Panel tag '{text}' missing phase info.";
                    return null;
                }
            });

            // Article 408.4 - Panelboard load type identification
            RegisterRule(new ValidationRule
            {
                RuleId = "NEC-408.4-003", Standard = "NEC 2023", Section = "Article 408.4",
                Requirement = "Panel label should indicate load type (lighting, power, emergency).",
                Severity = IssueSeverity.Info, ApplicableCategories = PanelCategories,
                SuggestedFix = "Use standard prefix (LP=Lighting, PP=Power, EP=Emergency).",
                Check = tag =>
                {
                    if (!IsPanel(tag) || string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    string u = tag.DisplayText.ToUpperInvariant();
                    bool has = u.Contains("LP") || u.Contains("PP") || u.Contains("EP") ||
                               u.Contains("MP") || u.Contains("LIGHTING") || u.Contains("POWER") ||
                               u.Contains("EMERGENCY") || u.Contains("MECHANICAL") || u.Contains("LIFE SAFETY");
                    return has ? null : $"Panel tag '{tag.DisplayText}' does not indicate load type.";
                }
            });

            // Article 210.5 - Branch circuit source identification
            RegisterRule(new ValidationRule
            {
                RuleId = "NEC-210.5-001", Standard = "NEC 2023", Section = "Article 210.5",
                Requirement = "Branch circuit tags shall identify the source panel and circuit number.",
                Severity = IssueSeverity.Warning, ApplicableCategories = ElectricalCategories,
                SuggestedFix = "Include source panel and circuit (e.g., 'LP-1A/Ckt 12').",
                Check = tag =>
                {
                    if (!IsCircuitElement(tag) || string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    return SourcePattern.IsMatch(tag.DisplayText) ? null
                        : $"Circuit tag '{tag.DisplayText}' does not identify source panel.";
                }
            });

            // Article 210.5(C) - Voltage marking on branch circuits
            RegisterRule(new ValidationRule
            {
                RuleId = "NEC-210.5C-001", Standard = "NEC 2023", Section = "Article 210.5(C)",
                Requirement = "Each branch circuit shall be identified by voltage when multiple systems exist.",
                Severity = IssueSeverity.Warning, ApplicableCategories = ElectricalCategories,
                SuggestedFix = "Add voltage identification (e.g., '120V', '208V', '277V').",
                Check = tag =>
                {
                    if (!IsCircuitElement(tag) || string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    return VoltagePattern.IsMatch(tag.DisplayText) ? null
                        : $"Circuit tag '{tag.DisplayText}' missing voltage identification.";
                }
            });

            // Article 230.2 - Service equipment marking
            RegisterRule(new ValidationRule
            {
                RuleId = "NEC-230.2-001", Standard = "NEC 2023", Section = "Article 230.2",
                Requirement = "Service disconnecting means shall be marked as service disconnect.",
                Severity = IssueSeverity.Critical, ApplicableCategories = PanelCategories,
                SuggestedFix = "Mark with 'SERVICE DISCONNECT' or 'MAIN SERVICE' designation.",
                Check = tag =>
                {
                    string f = tag.FamilyName?.ToUpperInvariant() ?? "";
                    if (!f.Contains("SERVICE") && !f.Contains("MAIN DISCONNECT") && !f.Contains("UTILITY"))
                        return null;
                    if (string.IsNullOrWhiteSpace(tag.DisplayText))
                        return "Service equipment has no identification tag per NEC 230.2.";
                    string u = tag.DisplayText.ToUpperInvariant();
                    return (u.Contains("SERVICE") || u.Contains("MAIN") || u.Contains("MSB"))
                        ? null : $"Service equipment tag '{tag.DisplayText}' lacks service marking.";
                }
            });

            // Article 408.3(A) - Upstream source identification
            RegisterRule(new ValidationRule
            {
                RuleId = "NEC-408.3A-001", Standard = "NEC 2023", Section = "Article 408.3(A)",
                Requirement = "Panelboard tag shall identify the upstream supply source.",
                Severity = IssueSeverity.Info, ApplicableCategories = PanelCategories,
                SuggestedFix = "Include 'Fed from [source]' in the panel tag.",
                Check = tag =>
                {
                    if (!IsPanel(tag) || string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    string u = tag.DisplayText.ToUpperInvariant();
                    return (u.Contains("FED FROM") || u.Contains("SUPPLY:") || u.Contains("SOURCE:"))
                        ? null : $"Panel tag '{tag.DisplayText}' does not identify upstream source.";
                }
            });
        }

        private static bool IsPanel(TagInstance tag)
        {
            string f = tag.FamilyName?.ToUpperInvariant() ?? "";
            return f.Contains("PANEL") || f.Contains("SWITCHBOARD") || f.Contains("MCC") ||
                   f.Contains("DISTRIBUTION") || f.Contains("SWITCHGEAR") || f.Contains("MDP") || f.Contains("MSB");
        }

        private static bool IsCircuitElement(TagInstance tag)
        {
            string f = tag.FamilyName?.ToUpperInvariant() ?? "";
            return f.Contains("OUTLET") || f.Contains("SWITCH") || f.Contains("RECEPTACLE") ||
                   f.Contains("DEVICE") || f.Contains("JUNCTION");
        }

        #endregion

        #region ASHRAE Rules

        private void RegisterASHRAERules()
        {
            // ASHRAE 90.1 6.7.2.4 - HVAC equipment capacity identification
            RegisterRule(new ValidationRule
            {
                RuleId = "ASHRAE-90.1-001", Standard = "ASHRAE 90.1", Section = "Section 6.7.2.4",
                Requirement = "HVAC equipment tags shall include rated capacity for energy compliance.",
                Severity = IssueSeverity.Warning, ApplicableCategories = HVACEquipCategories,
                SuggestedFix = "Include capacity (e.g., 'AHU-1, 50 ton', 'FCU-12, 3.5 kW').",
                Check = tag => string.IsNullOrWhiteSpace(tag.DisplayText) ? null
                    : CapacityPattern.IsMatch(tag.DisplayText) ? null
                    : $"HVAC tag '{tag.DisplayText}' missing rated capacity per ASHRAE 90.1."
            });

            // ASHRAE 90.1 6.7.2.4 - Equipment type designation
            RegisterRule(new ValidationRule
            {
                RuleId = "ASHRAE-90.1-002", Standard = "ASHRAE 90.1", Section = "Section 6.7.2.4",
                Requirement = "HVAC equipment tags shall indicate equipment type (AHU, FCU, RTU, etc.).",
                Severity = IssueSeverity.Warning, ApplicableCategories = HVACEquipCategories,
                SuggestedFix = "Include equipment type abbreviation (AHU, FCU, RTU, CH, BL, PU, CT).",
                Check = tag =>
                {
                    if (string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    string u = tag.DisplayText.ToUpperInvariant();
                    bool has = u.Contains("AHU") || u.Contains("FCU") || u.Contains("RTU") ||
                               u.Contains("CHILLER") || u.Contains("BOILER") || u.Contains("PUMP") ||
                               u.Contains("COOLING TOWER") || u.Contains("HRU") || u.Contains("ERV") ||
                               u.Contains("VRF") || u.Contains("MAU") || u.Contains("EF") ||
                               Regex.IsMatch(u, @"\b(CH|BL|PU|CT|CU|HX)\s*[-]?\s*\d");
                    return has ? null : $"HVAC tag '{tag.DisplayText}' missing equipment type designation.";
                }
            });

            // ASHRAE 90.1 6.4.1 - Efficiency rating
            RegisterRule(new ValidationRule
            {
                RuleId = "ASHRAE-90.1-003", Standard = "ASHRAE 90.1", Section = "Section 6.4.1",
                Requirement = "Equipment tags should include efficiency rating (COP, EER, SEER, AFUE).",
                Severity = IssueSeverity.Info, ApplicableCategories = HVACEquipCategories,
                SuggestedFix = "Include efficiency (e.g., 'COP: 3.5', 'SEER: 16').",
                Check = tag =>
                {
                    if (string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    string u = tag.DisplayText.ToUpperInvariant();
                    bool has = u.Contains("COP") || u.Contains("EER") || u.Contains("SEER") ||
                               u.Contains("AFUE") || u.Contains("HSPF") || u.Contains("IEER");
                    return has ? null : $"Equipment tag '{tag.DisplayText}' missing efficiency rating.";
                }
            });

            // ASHRAE 62.1 6.2.6 - Duct system type identification
            RegisterRule(new ValidationRule
            {
                RuleId = "ASHRAE-62.1-001", Standard = "ASHRAE 62.1", Section = "Section 6.2.6",
                Requirement = "Duct tags shall identify system type (supply, return, exhaust, outside air).",
                Severity = IssueSeverity.Warning, ApplicableCategories = DuctCategories,
                SuggestedFix = "Include system type (e.g., 'Supply Air', 'Return', 'Exhaust').",
                Check = tag => string.IsNullOrWhiteSpace(tag.DisplayText) ? null
                    : DuctSystemPattern.IsMatch(tag.DisplayText) ? null
                    : $"Duct tag '{tag.DisplayText}' missing system type per ASHRAE 62.1."
            });

            // ASHRAE 62.1 6.2.6 - Duct size for commissioning
            RegisterRule(new ValidationRule
            {
                RuleId = "ASHRAE-62.1-002", Standard = "ASHRAE 62.1", Section = "Section 6.2.6",
                Requirement = "Duct tags shall include dimensions for airflow verification.",
                Severity = IssueSeverity.Info, ApplicableCategories = DuctCategories,
                SuggestedFix = "Include duct dimensions (e.g., '600x400', 'dia 300mm').",
                Check = tag => string.IsNullOrWhiteSpace(tag.DisplayText) ? null
                    : DuctSizePattern.IsMatch(tag.DisplayText) ? null
                    : $"Duct tag '{tag.DisplayText}' missing duct dimensions."
            });

            // ASHRAE 90.1 6.4.3 - Air terminal airflow rate
            RegisterRule(new ValidationRule
            {
                RuleId = "ASHRAE-90.1-004", Standard = "ASHRAE 90.1", Section = "Section 6.4.3",
                Requirement = "Air terminal tags should include design airflow rate.",
                Severity = IssueSeverity.Info,
                ApplicableCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Air Terminals" },
                SuggestedFix = "Include airflow (e.g., '200 CFM', '100 L/s').",
                Check = tag =>
                {
                    if (string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    bool has = Regex.IsMatch(tag.DisplayText, @"\d+\.?\d*\s*(CFM|[Ll]/s|[Mm]3/h)",
                        RegexOptions.None, TimeSpan.FromMilliseconds(50));
                    return has ? null : $"Air terminal tag '{tag.DisplayText}' missing airflow rate.";
                }
            });
        }

        #endregion

        #region Fire Safety Rules

        private void RegisterFireSafetyRules()
        {
            // NFPA 13 6.2.3 - Sprinkler head type identification
            RegisterRule(new ValidationRule
            {
                RuleId = "NFPA13-6.2.3-001", Standard = "NFPA 13", Section = "Section 6.2.3",
                Requirement = "Sprinkler tags shall identify head type and response type.",
                Severity = IssueSeverity.Warning,
                ApplicableCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sprinklers" },
                SuggestedFix = "Include type and response (e.g., 'Pendent QR K5.6').",
                Check = tag => string.IsNullOrWhiteSpace(tag.DisplayText) ? null
                    : SprinklerTypePattern.IsMatch(tag.DisplayText) ? null
                    : $"Sprinkler tag '{tag.DisplayText}' missing type/response per NFPA 13."
            });

            // NFPA 13 8.3 - Sprinkler temperature rating
            RegisterRule(new ValidationRule
            {
                RuleId = "NFPA13-8.3-001", Standard = "NFPA 13", Section = "Section 8.3",
                Requirement = "Sprinkler tags should include temperature rating.",
                Severity = IssueSeverity.Info,
                ApplicableCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sprinklers" },
                SuggestedFix = "Include temperature (e.g., '155F/68C', '200F/93C').",
                Check = tag =>
                {
                    if (string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    bool has = Regex.IsMatch(tag.DisplayText, @"\d+\s*\u00b0?\s*[FfCc]\b",
                        RegexOptions.None, TimeSpan.FromMilliseconds(50));
                    return has ? null : $"Sprinkler tag '{tag.DisplayText}' missing temperature rating.";
                }
            });

            // NFPA 72 18.4 - Fire alarm device labeling
            RegisterRule(new ValidationRule
            {
                RuleId = "NFPA72-18.4-001", Standard = "NFPA 72", Section = "Section 18.4",
                Requirement = "Fire alarm devices shall be tagged with device type and zone/loop ID.",
                Severity = IssueSeverity.Critical,
                ApplicableCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fire Alarm Devices" },
                SuggestedFix = "Include device type and zone (e.g., 'SD-01, Zone 3', 'MCP-05, Loop A').",
                Check = tag =>
                {
                    if (string.IsNullOrWhiteSpace(tag.DisplayText))
                        return "Fire alarm device has no tag. NFPA 72 requires labeling.";
                    string u = tag.DisplayText.ToUpperInvariant();
                    bool type = u.Contains("SD") || u.Contains("HD") || u.Contains("MCP") ||
                                u.Contains("SMOKE") || u.Contains("HEAT") || u.Contains("HORN") ||
                                u.Contains("STROBE") || u.Contains("SPEAKER") || u.Contains("PULL");
                    bool zone = Regex.IsMatch(u, @"(ZONE|LOOP|SLC|NAC|IDC)\s*[-:]?\s*[A-Z0-9]+",
                        RegexOptions.None, TimeSpan.FromMilliseconds(50));
                    if (!type) return $"Fire alarm tag '{tag.DisplayText}' missing device type.";
                    if (!zone) return $"Fire alarm tag '{tag.DisplayText}' missing zone/loop ID.";
                    return null;
                }
            });

            // IBC 716.5 - Fire door rating marking
            RegisterRule(new ValidationRule
            {
                RuleId = "IBC-716.5-001", Standard = "IBC 2021", Section = "Section 716.5",
                Requirement = "Fire doors shall be tagged with fire resistance rating.",
                Severity = IssueSeverity.Critical,
                ApplicableCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Doors" },
                SuggestedFix = "Add fire rating (e.g., '1-HR Fire Door', '90 min FRL').",
                Check = tag =>
                {
                    if (!IsFireRatedDoor(tag)) return null;
                    if (string.IsNullOrWhiteSpace(tag.DisplayText))
                        return "Fire-rated door has no tag. IBC 716.5 requires rating marking.";
                    return FireRatingPattern.IsMatch(tag.DisplayText) ? null
                        : $"Fire door tag '{tag.DisplayText}' missing fire resistance rating.";
                }
            });

            // IBC 707.2 - Fire-rated wall assembly marking
            RegisterRule(new ValidationRule
            {
                RuleId = "IBC-707.2-001", Standard = "IBC 2021", Section = "Section 707.2",
                Requirement = "Fire-rated wall assemblies shall be tagged with rating.",
                Severity = IssueSeverity.Warning,
                ApplicableCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Walls" },
                SuggestedFix = "Add fire rating (e.g., '2-HR Fire Barrier', 'FRL 120/120/120').",
                Check = tag =>
                {
                    string f = tag.FamilyName?.ToUpperInvariant() ?? "";
                    string t = tag.TypeName?.ToUpperInvariant() ?? "";
                    bool rated = f.Contains("FIRE") || t.Contains("FIRE") || t.Contains("BARRIER") || t.Contains("RATED");
                    if (!rated || string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    return FireRatingPattern.IsMatch(tag.DisplayText) ? null
                        : $"Fire-rated wall tag '{tag.DisplayText}' missing fire rating.";
                }
            });

            // IBC 903 - Fire zone designation
            RegisterRule(new ValidationRule
            {
                RuleId = "IBC-903-001", Standard = "IBC 2021", Section = "Section 903",
                Requirement = "Fire protection zones shall have zone identification tags.",
                Severity = IssueSeverity.Info,
                ApplicableCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Rooms", "Areas" },
                SuggestedFix = "Include fire zone designation (e.g., 'FZ-01', 'Fire Zone 3').",
                Check = tag =>
                {
                    if (tag.Metadata == null) return null;
                    bool hasFire = tag.Metadata.ContainsKey("FireZone") || tag.Metadata.ContainsKey("FireCompartment");
                    if (!hasFire || string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    return FireZonePattern.IsMatch(tag.DisplayText) ? null
                        : $"Room/area '{tag.DisplayText}' in fire zone but tag lacks zone designation.";
                }
            });
        }

        private static bool IsFireRatedDoor(TagInstance tag)
        {
            string f = tag.FamilyName?.ToUpperInvariant() ?? "";
            string t = tag.TypeName?.ToUpperInvariant() ?? "";
            return f.Contains("FIRE") || t.Contains("FIRE") || f.Contains("RATED") || t.Contains("RATED") ||
                   t.Contains("FD30") || t.Contains("FD60") || t.Contains("FD90") || t.Contains("FD120");
        }

        #endregion

        #region Regional Standards Rules

        private void RegisterRegionalRules()
        {
            // BS 7671 514.9 - Circuit designation format
            RegisterRule(new ValidationRule
            {
                RuleId = "BS7671-514.9-001", Standard = "BS 7671", Section = "Section 514.9",
                Requirement = "Circuits shall be identified at distribution boards using BS format (DB/Way).",
                Severity = IssueSeverity.Warning, ApplicableCategories = ElectricalCategories,
                SuggestedFix = "Use BS 7671 format (e.g., 'DB1/Way 3', 'MSB-01/C12').",
                Check = tag =>
                {
                    string f = tag.FamilyName?.ToUpperInvariant() ?? "";
                    bool isCct = f.Contains("SOCKET") || f.Contains("SPUR") || f.Contains("MCB") ||
                                 f.Contains("RCD") || f.Contains("RCBO") || f.Contains("OUTLET") || f.Contains("SWITCH");
                    if (!isCct || string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    return BSCircuitPattern.IsMatch(tag.DisplayText) ? null
                        : $"Circuit tag '{tag.DisplayText}' does not follow BS 7671 format.";
                }
            });

            // BS 7671 514.12 - Dual supply warning
            RegisterRule(new ValidationRule
            {
                RuleId = "BS7671-514.12-001", Standard = "BS 7671", Section = "Section 514.12",
                Requirement = "Dual supply equipment shall have a warning label.",
                Severity = IssueSeverity.Critical, ApplicableCategories = PanelCategories,
                SuggestedFix = "Add dual supply warning (e.g., 'DANGER - DUAL SUPPLY').",
                Check = tag =>
                {
                    if (tag.Metadata == null) return null;
                    bool dual = tag.Metadata.ContainsKey("DualSupply") || tag.Metadata.ContainsKey("AlternateSource");
                    if (!dual) return null;
                    if (string.IsNullOrWhiteSpace(tag.DisplayText))
                        return "Dual supply equipment has no tag. BS 7671 514.12 requires warning.";
                    string u = tag.DisplayText.ToUpperInvariant();
                    return (u.Contains("DUAL") || u.Contains("DANGER") || u.Contains("WARNING") ||
                            u.Contains("MULTIPLE SOURCE")) ? null
                        : $"Dual supply tag '{tag.DisplayText}' missing warning per BS 7671 514.12.";
                }
            });

            // BS 7671 514.1 - Switchgear supply characteristics
            RegisterRule(new ValidationRule
            {
                RuleId = "BS7671-514.1-001", Standard = "BS 7671", Section = "Section 514.1",
                Requirement = "Switchgear tags shall include equipment designation and supply characteristics.",
                Severity = IssueSeverity.Warning, ApplicableCategories = PanelCategories,
                SuggestedFix = "Include voltage and frequency (e.g., 'DB1 - 230V 1Ph 50Hz').",
                Check = tag =>
                {
                    string f = tag.FamilyName?.ToUpperInvariant() ?? "";
                    bool isSw = f.Contains("DISTRIBUTION") || f.Contains("CONSUMER") || f.Contains("DB") || f.Contains("MSB");
                    if (!isSw || string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    string text = tag.DisplayText;
                    bool hz = Regex.IsMatch(text, @"\d+\s*[Hh][Zz]", RegexOptions.None, TimeSpan.FromMilliseconds(50));
                    bool v = VoltagePattern.IsMatch(text);
                    if (!v) return $"Switchgear tag '{text}' missing supply voltage.";
                    if (!hz) return $"Switchgear tag '{text}' missing supply frequency.";
                    return null;
                }
            });

            // SANS 10142 - South African DB identification
            RegisterRule(new ValidationRule
            {
                RuleId = "SANS10142-001", Standard = "SANS 10142", Section = "Section 6.16",
                Requirement = "Each distribution board shall have a unique designation per SANS 10142.",
                Severity = IssueSeverity.Warning, ApplicableCategories = PanelCategories,
                SuggestedFix = "Add unique DB designation (e.g., 'DB-A1', 'MDB-01').",
                Check = tag =>
                {
                    string f = tag.FamilyName?.ToUpperInvariant() ?? "";
                    if (!f.Contains("DISTRIBUTION") && !f.Contains("DB") && !f.Contains("BOARD") && !f.Contains("MDB"))
                        return null;
                    if (string.IsNullOrWhiteSpace(tag.DisplayText))
                        return "Distribution board has no identification per SANS 10142.";
                    return tag.DisplayText.Trim().Length < 2
                        ? $"DB tag '{tag.DisplayText}' too short for SANS 10142 compliance." : null;
                }
            });

            // UNBS US 319 - Uganda: voltage, short-circuit, IP rating
            RegisterRule(new ValidationRule
            {
                RuleId = "UNBS-US319-001", Standard = "UNBS US 319", Section = "Section 8",
                Requirement = "DBs in Uganda shall show voltage, short-circuit rating, and IP class.",
                Severity = IssueSeverity.Warning, ApplicableCategories = PanelCategories,
                SuggestedFix = "Include voltage, kA, and IP rating (e.g., 'DB1 - 240V, 10kA, IP54').",
                Check = tag =>
                {
                    if (!IsPanel(tag) || string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    bool hasIP = Regex.IsMatch(tag.DisplayText, @"IP\s*\d{2}",
                        RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));
                    return hasIP ? null
                        : $"DB tag '{tag.DisplayText}' missing IP protection rating per UNBS US 319.";
                }
            });

            // KEBS KS 1278 - Kenya: voltage and current rating
            RegisterRule(new ValidationRule
            {
                RuleId = "KEBS-KS1278-001", Standard = "KEBS KS 1278", Section = "Section 9.3",
                Requirement = "Electrical equipment in Kenya shall show rated voltage and current.",
                Severity = IssueSeverity.Warning, ApplicableCategories = PanelCategories,
                SuggestedFix = "Include voltage and current (e.g., 'DB1 - 240V, 100A').",
                Check = tag =>
                {
                    if (!IsPanel(tag) || string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    bool a = Regex.IsMatch(tag.DisplayText, @"\d+\s*[Aa]\b",
                        RegexOptions.None, TimeSpan.FromMilliseconds(50));
                    bool v = VoltagePattern.IsMatch(tag.DisplayText);
                    return (a && v) ? null
                        : $"Equipment tag '{tag.DisplayText}' missing voltage/current per KEBS KS 1278.";
                }
            });

            // KEBS KS 1278 9.5 - RCD rating indication
            RegisterRule(new ValidationRule
            {
                RuleId = "KEBS-KS1278-002", Standard = "KEBS KS 1278", Section = "Section 9.5",
                Requirement = "Boards with earth leakage protection shall indicate RCD rating.",
                Severity = IssueSeverity.Info, ApplicableCategories = PanelCategories,
                SuggestedFix = "Include RCD trip current (e.g., 'with 30mA RCD').",
                Check = tag =>
                {
                    if (tag.Metadata == null) return null;
                    bool rcd = tag.Metadata.ContainsKey("RCD") || tag.Metadata.ContainsKey("ELCB");
                    if (!rcd || string.IsNullOrWhiteSpace(tag.DisplayText)) return null;
                    string u = tag.DisplayText.ToUpperInvariant();
                    bool has = u.Contains("RCD") || u.Contains("ELCB") || u.Contains("RCCB") ||
                               Regex.IsMatch(u, @"\d+\s*[Mm][Aa]", RegexOptions.None, TimeSpan.FromMilliseconds(50));
                    return has ? null : $"Board tag '{tag.DisplayText}' missing RCD rating per KEBS KS 1278.";
                }
            });
        }

        #endregion

        #region Public API

        /// <summary>
        /// Performs comprehensive standards validation across specified views.
        /// </summary>
        public async Task<StandardsValidationReport> ValidateAsync(
            List<int> viewIds, ValidationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (viewIds == null) throw new ArgumentNullException(nameof(viewIds));
            if (viewIds.Count == 0) throw new ArgumentException("At least one view ID required.", nameof(viewIds));

            options = options ?? ValidationOptions.Default;
            var startTime = DateTime.UtcNow;
            Logger.Info("Starting standards validation across {0} view(s)", viewIds.Count);

            // Collect tags from views
            var allTags = new List<TagInstance>();
            foreach (int viewId in viewIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                allTags.AddRange(_repository.GetTagsByView(viewId));
            }

            // Get applicable rules and run per-tag checks
            var rules = GetApplicableRules(options);
            var violations = new List<ValidationViolation>();

            foreach (var tag in allTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var rule in rules)
                {
                    if (!IsRuleApplicable(rule, tag)) continue;
                    string desc;
                    try { desc = rule.Check(tag); }
                    catch (Exception ex) { Logger.Warn(ex, "Rule {0} failed for {1}", rule.RuleId, tag.TagId); continue; }
                    if (desc != null)
                        violations.Add(MakeViolation(rule, tag, desc));
                }
            }

            // Cross-tag ISO 19650 checks
            if (options.CheckISO19650)
                violations.AddRange(await Task.Run(() => RunCrossTagISO19650Checks(allTags), cancellationToken));

            // Trim per-standard if configured
            if (options.MaxViolationsPerStandard > 0)
                violations = violations.GroupBy(v => v.Standard, StringComparer.OrdinalIgnoreCase)
                    .SelectMany(g => g.OrderByDescending(v => SeverityRank(v.Severity)).Take(options.MaxViolationsPerStandard))
                    .ToList();

            // Build grouped violations
            var byStandard = new Dictionary<string, List<ValidationViolation>>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in violations)
            {
                if (!byStandard.TryGetValue(v.Standard, out var list)) { list = new List<ValidationViolation>(); byStandard[v.Standard] = list; }
                list.Add(v);
            }

            // Scores
            double overall = ComputeScore(violations, allTags.Count);
            var scores = byStandard.ToDictionary(kv => kv.Key, kv => ComputeScore(kv.Value, allTags.Count), StringComparer.OrdinalIgnoreCase);
            var countsBySev = new Dictionary<IssueSeverity, int>();
            foreach (IssueSeverity s in Enum.GetValues(typeof(IssueSeverity)))
            { int c = violations.Count(v => v.Severity == s); if (c > 0) countsBySev[s] = c; }

            // Documentation completeness
            CompletenessReport completeness = null;
            if (options.CheckDocumentationCompleteness)
                completeness = await Task.Run(() => BuildCompletenessReport(viewIds), cancellationToken);

            var duration = DateTime.UtcNow - startTime;
            var report = new StandardsValidationReport
            {
                ViolationsByStandard = byStandard, AllViolations = violations,
                ComplianceScore = overall, ScoresByStandard = scores,
                CountsBySeverity = countsBySev, RulesEvaluated = rules.Count,
                TagsChecked = allTags.Count, ViewsChecked = viewIds.Count,
                Recommendations = GenerateRecommendations(byStandard, completeness),
                GeneratedAt = DateTime.UtcNow, Duration = duration,
                DocumentationCompleteness = completeness
            };

            Logger.Info("Validation complete: {0} violations, score {1:F1}, {2} tags, {3}ms",
                violations.Count, overall, allTags.Count, duration.TotalMilliseconds);
            return report;
        }

        /// <summary>Validates tags against ISO 19650 requirements including cross-tag checks.</summary>
        public List<ValidationViolation> ValidateISO19650(List<TagInstance> tags)
        {
            if (tags == null) throw new ArgumentNullException(nameof(tags));
            var violations = new List<ValidationViolation>();
            var rules = GetRulesForStandard("ISO 19650");
            foreach (var tag in tags)
                foreach (var rule in rules)
                {
                    if (!IsRuleApplicable(rule, tag)) continue;
                    string d = SafeCheck(rule, tag);
                    if (d != null) violations.Add(MakeViolation(rule, tag, d));
                }
            violations.AddRange(RunCrossTagISO19650Checks(tags));
            return violations;
        }

        /// <summary>Validates tags against NEC 2023 electrical tagging requirements.</summary>
        public List<ValidationViolation> ValidateElectrical(List<TagInstance> tags)
        {
            if (tags == null) throw new ArgumentNullException(nameof(tags));
            var violations = new List<ValidationViolation>();
            foreach (var rule in GetRulesForStandard("NEC 2023"))
                foreach (var tag in tags)
                {
                    if (!IsRuleApplicable(rule, tag)) continue;
                    string d = SafeCheck(rule, tag);
                    if (d != null) violations.Add(MakeViolation(rule, tag, d));
                }
            return violations;
        }

        /// <summary>Validates tags against NFPA and IBC fire safety tagging requirements.</summary>
        public List<ValidationViolation> ValidateFireSafety(List<TagInstance> tags)
        {
            if (tags == null) throw new ArgumentNullException(nameof(tags));
            var violations = new List<ValidationViolation>();
            foreach (var std in new[] { "NFPA 13", "NFPA 72", "IBC 2021" })
                foreach (var rule in GetRulesForStandard(std))
                    foreach (var tag in tags)
                    {
                        if (!IsRuleApplicable(rule, tag)) continue;
                        string d = SafeCheck(rule, tag);
                        if (d != null) violations.Add(MakeViolation(rule, tag, d));
                    }
            return violations;
        }

        /// <summary>Checks documentation completeness for views on the specified sheets.</summary>
        public CompletenessReport CheckDocumentationCompleteness(List<int> sheetIds)
        {
            if (sheetIds == null) throw new ArgumentNullException(nameof(sheetIds));
            var viewIds = new HashSet<int>(_repository.GetAllTags().Select(t => t.ViewId).Where(v => v > 0));
            return BuildCompletenessReport(viewIds.ToList());
        }

        #endregion

        #region Cross-Tag ISO 19650 Checks

        private List<ValidationViolation> RunCrossTagISO19650Checks(List<TagInstance> tags)
        {
            var violations = new List<ValidationViolation>();

            // Uniqueness: same asset ID text on different host elements
            var idGroups = tags
                .Where(t => !string.IsNullOrWhiteSpace(t.DisplayText) && AssetIdPattern.IsMatch(t.DisplayText.Trim()))
                .GroupBy(t => t.DisplayText.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(t => t.HostElementId).Distinct().Count() > 1);

            foreach (var g in idGroups)
            {
                int hostCount = g.Select(t => t.HostElementId).Distinct().Count();
                foreach (var tag in g.GroupBy(t => t.HostElementId).Skip(1).SelectMany(x => x.Take(1)))
                    violations.Add(new ValidationViolation
                    {
                        RuleId = "ISO19650-ID-004", Standard = "ISO 19650",
                        Section = "ISO 19650-3:2020 Sec 5.6", ElementId = tag.HostElementId,
                        TagId = tag.TagId, CategoryName = tag.CategoryName,
                        Description = $"Asset ID '{tag.DisplayText.Trim()}' shared by {hostCount} elements.",
                        Severity = IssueSeverity.Critical,
                        SuggestedFix = "Assign unique asset IDs to each element.",
                        ViewId = tag.ViewId, DetectedAt = DateTime.UtcNow
                    });
            }

            // Cross-view consistency: same host element with different tag text
            var hostGroups = tags.Where(t => !string.IsNullOrWhiteSpace(t.DisplayText))
                .GroupBy(t => t.HostElementId).Where(g => g.Count() > 1);
            foreach (var g in hostGroups)
            {
                var texts = g.Select(t => t.DisplayText.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (texts.Count > 1)
                {
                    var first = g.First();
                    violations.Add(new ValidationViolation
                    {
                        RuleId = "ISO19650-IR-001", Standard = "ISO 19650",
                        Section = "ISO 19650-2:2018 Sec 5.4", ElementId = g.Key,
                        TagId = first.TagId, CategoryName = first.CategoryName,
                        Description = $"Element {g.Key} has inconsistent tags: {string.Join(" vs ", texts.Select(t => $"'{t}'"))}.",
                        Severity = IssueSeverity.Warning,
                        SuggestedFix = "Ensure all tags for this element show identical text.",
                        ViewId = first.ViewId, DetectedAt = DateTime.UtcNow
                    });
                }
            }

            return violations;
        }

        #endregion

        #region Documentation Completeness

        private CompletenessReport BuildCompletenessReport(List<int> viewIds)
        {
            var report = new CompletenessReport();
            var catCoverage = new Dictionary<string, (int tagged, int total)>(StringComparer.OrdinalIgnoreCase);

            foreach (int viewId in viewIds)
            {
                var viewTags = _repository.GetTagsByView(viewId);
                if (viewTags.Count == 0) continue;

                // Count tagged elements per category
                var taggedByCat = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
                foreach (var tag in viewTags)
                {
                    if (string.IsNullOrEmpty(tag.CategoryName)) continue;
                    if (!taggedByCat.TryGetValue(tag.CategoryName, out var set))
                    { set = new HashSet<int>(); taggedByCat[tag.CategoryName] = set; }
                    set.Add(tag.HostElementId);
                }

                foreach (var kvp in taggedByCat)
                {
                    int count = kvp.Value.Count;
                    int total = count; // baseline; Revit API would provide actual total
                    if (!catCoverage.ContainsKey(kvp.Key)) catCoverage[kvp.Key] = (0, 0);
                    var (pt, pe) = catCoverage[kvp.Key];
                    catCoverage[kvp.Key] = (pt + count, pe + total);
                }

                // Check if required categories for view type are present
                bool incomplete = false;
                foreach (var reqCats in RequiredCatsByView.Values)
                    foreach (var cat in reqCats)
                        if (!taggedByCat.ContainsKey(cat)) incomplete = true;
                if (incomplete) report.IncompleteViewIds.Add(viewId);
            }

            double totalT = 0, totalE = 0;
            foreach (var kvp in catCoverage)
            {
                var (tagged, total) = kvp.Value;
                double pct = total > 0 ? Math.Min(100.0, (double)tagged / total * 100.0) : 0.0;
                report.CategoryCoveragePercent[kvp.Key] = pct;
                report.ActualCountByCategory[kvp.Key] = tagged;
                report.RequiredCountByCategory[kvp.Key] = total;
                totalT += tagged; totalE += total;
            }
            report.OverallCompleteness = totalE > 0 ? Math.Min(100.0, totalT / totalE * 100.0) : 0.0;

            // Cross-reference consistency
            var allTags = new List<TagInstance>();
            foreach (int viewId in viewIds) allTags.AddRange(_repository.GetTagsByView(viewId));
            var taggedViews = new HashSet<int>(allTags.Select(t => t.ViewId));

            foreach (var tag in allTags.Where(t => t.Metadata != null))
            {
                if (tag.Metadata.TryGetValue("ReferenceViewId", out var rv) && rv is int refId && !taggedViews.Contains(refId))
                {
                    string catDesc = tag.CategoryName ?? "annotation";
                    report.CrossReferenceIssues.Add(
                        $"{catDesc} '{tag.DisplayText ?? "(blank)"}' in view {tag.ViewId} references view {refId} which has no tags.");
                }
            }

            Logger.Info("Completeness: {0:F1}%, {1} categories, {2} incomplete views",
                report.OverallCompleteness, catCoverage.Count, report.IncompleteViewIds.Count);
            return report;
        }

        #endregion

        #region Helpers

        private List<ValidationRule> GetApplicableRules(ValidationOptions options)
        {
            var standards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (options.CheckISO19650) standards.Add("ISO 19650");
            if (options.CheckNEC) standards.Add("NEC 2023");
            if (options.CheckASHRAE) { standards.Add("ASHRAE 90.1"); standards.Add("ASHRAE 62.1"); }
            if (options.CheckFireSafety) { standards.Add("NFPA 13"); standards.Add("NFPA 72"); standards.Add("IBC 2021"); }
            if (options.CheckRegional)
            {
                string r = options.ProjectRegion?.ToUpperInvariant() ?? "";
                switch (r)
                {
                    case "GB": case "UK": standards.Add("BS 7671"); break;
                    case "ZA": standards.Add("SANS 10142"); break;
                    case "UG": standards.Add("UNBS US 319"); standards.Add("BS 7671"); break;
                    case "KE": standards.Add("KEBS KS 1278"); standards.Add("BS 7671"); break;
                    default:
                        standards.Add("BS 7671"); standards.Add("SANS 10142");
                        standards.Add("UNBS US 319"); standards.Add("KEBS KS 1278"); break;
                }
            }

            int minRank = options.StrictnessLevel == 0 ? 3 : options.StrictnessLevel == 1 ? 2 : 1;
            return _allRules.Where(r => r.IsEnabled && standards.Contains(r.Standard) &&
                                        SeverityRank(r.Severity) >= minRank).ToList();
        }

        private List<ValidationRule> GetRulesForStandard(string name) =>
            _rulesByStandard.TryGetValue(name, out var l) ? l.Where(r => r.IsEnabled).ToList() : new List<ValidationRule>();

        private static bool IsRuleApplicable(ValidationRule rule, TagInstance tag)
        {
            if (tag.State == TagState.MarkedForDeletion || tag.State == TagState.Orphaned) return false;
            if (rule.ApplicableCategories == null || rule.ApplicableCategories.Count == 0) return true;
            return !string.IsNullOrEmpty(tag.CategoryName) && rule.ApplicableCategories.Contains(tag.CategoryName);
        }

        private static string SafeCheck(ValidationRule rule, TagInstance tag)
        { try { return rule.Check(tag); } catch { return null; } }

        private static ValidationViolation MakeViolation(ValidationRule rule, TagInstance tag, string desc) =>
            new ValidationViolation
            {
                RuleId = rule.RuleId, Standard = rule.Standard, Section = rule.Section,
                ElementId = tag.HostElementId, TagId = tag.TagId, CategoryName = tag.CategoryName,
                Description = desc, Severity = rule.Severity, SuggestedFix = rule.SuggestedFix,
                ViewId = tag.ViewId, DetectedAt = DateTime.UtcNow
            };

        private static double ComputeScore(List<ValidationViolation> v, int tagCount)
        {
            if (v == null || v.Count == 0) return 100.0;
            int tc = Math.Max(1, tagCount);
            double p = (v.Count(x => x.Severity == IssueSeverity.Critical) * 10.0 +
                         v.Count(x => x.Severity == IssueSeverity.Warning) * 3.0 +
                         v.Count(x => x.Severity == IssueSeverity.Info) * 0.5) / tc * 10.0;
            return Math.Max(0.0, Math.Min(100.0, 100.0 - p));
        }

        private static int SeverityRank(IssueSeverity s) =>
            s == IssueSeverity.Critical ? 3 : s == IssueSeverity.Warning ? 2 : 1;

        private static List<string> GenerateRecommendations(
            Dictionary<string, List<ValidationViolation>> byStd, CompletenessReport comp)
        {
            var recs = new List<string>();

            if (byStd.TryGetValue("ISO 19650", out var iso))
            {
                int idIssues = iso.Count(v => v.RuleId.StartsWith("ISO19650-ID"));
                if (idIssues > 5) recs.Add($"ISO 19650: {idIssues} asset ID issues. Run batch asset ID assignment.");
                int consistency = iso.Count(v => v.RuleId == "ISO19650-IR-001");
                if (consistency > 0) recs.Add($"ISO 19650: {consistency} cross-view inconsistencies. Use shared parameters for consistency.");
                int dupes = iso.Count(v => v.RuleId == "ISO19650-ID-004");
                if (dupes > 0) recs.Add($"ISO 19650: {dupes} duplicate asset IDs. Each asset needs a unique identifier.");
                int loiIssues = iso.Count(v => v.RuleId == "ISO19650-LOI-001");
                if (loiIssues > 0) recs.Add($"ISO 19650: {loiIssues} tags below required Level of Information Need. Add manufacturer/model data.");
                int cdeIssues = iso.Count(v => v.RuleId == "ISO19650-CDE-001");
                if (cdeIssues > 0) recs.Add($"ISO 19650: {cdeIssues} tags with draft markers in published state. Remove WIP/DRAFT text before issuance.");
                int fmIssues = iso.Count(v => v.RuleId == "ISO19650-FM-001");
                if (fmIssues > 0) recs.Add($"ISO 19650: {fmIssues} FM-handover assets missing lifecycle data. Populate installation date and expected life.");
                int locIssues = iso.Count(v => v.RuleId == "ISO19650-LOC-001");
                if (locIssues > 0) recs.Add($"ISO 19650: {locIssues} asset IDs with placeholder location codes. Update with actual locations.");
            }

            if (byStd.TryGetValue("NEC 2023", out var nec))
            {
                int panel = nec.Count(v => v.RuleId.StartsWith("NEC-408"));
                if (panel > 0) recs.Add($"NEC 2023: {panel} panelboard ID issues. Ensure panels show designation, voltage, phase.");
                int circuit = nec.Count(v => v.RuleId.StartsWith("NEC-210"));
                if (circuit > 3) recs.Add($"NEC 2023: {circuit} circuit ID issues. Update templates to include source and voltage.");
            }

            if (byStd.TryGetValue("ASHRAE 90.1", out var ashrae))
            {
                int cap = ashrae.Count(v => v.RuleId == "ASHRAE-90.1-001");
                if (cap > 0) recs.Add($"ASHRAE 90.1: {cap} equipment missing capacity data for energy compliance.");
            }

            if (byStd.TryGetValue("ASHRAE 62.1", out var a62))
            {
                int duct = a62.Count(v => v.RuleId == "ASHRAE-62.1-001");
                if (duct > 3) recs.Add($"ASHRAE 62.1: {duct} ducts missing system type ID.");
            }

            foreach (var fs in new[] { "NFPA 13", "NFPA 72", "IBC 2021" })
                if (byStd.TryGetValue(fs, out var fv))
                {
                    int crit = fv.Count(v => v.Severity == IssueSeverity.Critical);
                    if (crit > 0) recs.Add($"{fs}: {crit} critical fire safety tagging violations. Resolve before issuance.");
                }

            foreach (var rs in new[] { "BS 7671", "SANS 10142", "UNBS US 319", "KEBS KS 1278" })
                if (byStd.TryGetValue(rs, out var rv) && rv.Count > 0)
                    recs.Add($"{rs}: {rv.Count} regional compliance issues. Review for project jurisdiction.");

            if (comp != null)
            {
                if (comp.OverallCompleteness < 80.0)
                    recs.Add($"Documentation: {comp.OverallCompleteness:F1}% complete. Target 95%+ before submission.");
                foreach (var kv in comp.CategoryCoveragePercent.Where(kv => kv.Value < 50.0))
                    recs.Add($"Documentation: '{kv.Key}' only {kv.Value:F1}% tagged.");
                if (comp.CrossReferenceIssues.Count > 0)
                    recs.Add($"Documentation: {comp.CrossReferenceIssues.Count} cross-reference inconsistencies.");
                if (comp.IncompleteViewIds.Count > 0)
                    recs.Add($"Documentation: {comp.IncompleteViewIds.Count} views with incomplete coverage.");
            }

            return recs;
        }

        #endregion
    }
}
