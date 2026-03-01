// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagContextEngine.cs - Context-aware tag formatting engine
// Automatically adjusts tag content, size, detail level, and presentation
// based on view context (scale, purpose, audience, density).
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using StingBIM.AI.Tagging.Data;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Intelligence
{
    #region Enums and Inner Types

    /// <summary>The level of detail a tag should display based on view context.</summary>
    public enum DetailLevel { Full, Standard, Abbreviated, Minimal }

    /// <summary>The purpose of the drawing or view, determining which parameters are shown.</summary>
    public enum DrawingPurpose { Design, Construction, AsBuilt, Coordination, Presentation, Regulatory }

    /// <summary>Defines how tag content and appearance adapt to a specific view scale range.</summary>
    public class ScaleProfile
    {
        public double ScaleMin { get; set; }
        public double ScaleMax { get; set; }
        public DetailLevel DetailLevel { get; set; }
        public int MaxTextLength { get; set; }
        public bool IncludeLeaders { get; set; }
        public double TextSizeMultiplier { get; set; }
        public int MaxContentLines { get; set; }
        public bool ShowSecondaryInfo { get; set; }
        public string ProfileName { get; set; }
    }

    /// <summary>Defines the parameter priorities and filtering for a specific audience type.</summary>
    public class AudienceProfile
    {
        public string Name { get; set; }
        public List<string> PriorityCategories { get; set; } = new List<string>();
        public List<string> PriorityParameters { get; set; } = new List<string>();
        public List<string> HiddenCategories { get; set; } = new List<string>();
        public DetailLevel? DetailPreference { get; set; }
        public List<string> ExcludedParameters { get; set; } = new List<string>();
        public bool PreferTechnicalNotation { get; set; }
    }

    /// <summary>Thresholds for classifying local element density (elements per unit area).</summary>
    public class DensityThresholds
    {
        public double LowDensity { get; set; } = 0.5;
        public double MediumDensity { get; set; } = 2.0;
        public double HighDensity { get; set; } = 5.0;
    }

    /// <summary>Maps a Revit view template to a tagging context configuration.</summary>
    public class ViewTemplateMapping
    {
        public string ViewTemplateName { get; set; }
        public DrawingPurpose Purpose { get; set; }
        public AudienceProfile Audience { get; set; }
        public ScaleProfile ScaleProfileOverride { get; set; }
        public Dictionary<string, string> ContentExpressionOverrides { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public LeaderType? LeaderTypeOverride { get; set; }
        public double? TextSizeMultiplierOverride { get; set; }
    }

    /// <summary>A single factor applied during context resolution, for audit trail.</summary>
    public class ContextFactor
    {
        public string FactorName { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
    }

    /// <summary>The fully resolved tag configuration after merging all context signals.</summary>
    public class ContextResolution
    {
        public string ResolvedContentExpression { get; set; }
        public double ResolvedTextSize { get; set; } = 1.0;
        public DetailLevel ResolvedDetailLevel { get; set; } = DetailLevel.Standard;
        public LeaderType ResolvedLeaderType { get; set; } = LeaderType.Auto;
        public int ResolvedMaxTextLength { get; set; } = 80;
        public int ResolvedMaxContentLines { get; set; } = 3;
        public bool IncludeSecondaryInfo { get; set; } = true;
        public bool ShouldAbbreviate { get; set; }
        public bool EnableStacking { get; set; }
        public double OffsetMultiplier { get; set; } = 1.0;
        public List<ContextFactor> ContextFactors { get; set; } = new List<ContextFactor>();
        public bool Success { get; set; }
        public List<string> Messages { get; set; } = new List<string>();
    }

    #endregion

    /// <summary>
    /// Context-aware tag formatting engine. Adjusts tag content, size, detail level, and
    /// presentation based on view context signals including scale, drawing purpose, target
    /// audience, local element density, and view template configuration.
    ///
    /// Resolution pipeline order:
    /// 1. Default template configuration
    /// 2. Scale-adaptive modifications
    /// 3. Purpose-based content filtering
    /// 4. Audience parameter priorities
    /// 5. Density-responsive adjustments
    /// 6. View template overrides
    /// </summary>
    public class TagContextEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly TagRepository _repository;
        private readonly object _profilesLock = new object();
        private readonly object _mappingsLock = new object();

        private readonly List<ScaleProfile> _scaleProfiles;
        private readonly List<ScaleProfile> _customScaleProfiles;
        private readonly Dictionary<string, AudienceProfile> _audienceProfiles;
        private readonly List<ViewTemplateMapping> _viewTemplateMappings;
        private DensityThresholds _densityThresholds;

        // Purpose-specific parameter sets: purpose -> category -> parameter list
        private readonly Dictionary<DrawingPurpose, Dictionary<string, List<string>>> _purposeParameters;
        // Purpose-specific content expressions: purpose -> category -> expression
        private readonly Dictionary<DrawingPurpose, Dictionary<string, string>> _purposeExpressions;

        #region Constructor and Initialization

        public TagContextEngine(TagRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _scaleProfiles = new List<ScaleProfile>();
            _customScaleProfiles = new List<ScaleProfile>();
            _audienceProfiles = new Dictionary<string, AudienceProfile>(StringComparer.OrdinalIgnoreCase);
            _viewTemplateMappings = new List<ViewTemplateMapping>();
            _densityThresholds = new DensityThresholds();
            _purposeParameters = new Dictionary<DrawingPurpose, Dictionary<string, List<string>>>();
            _purposeExpressions = new Dictionary<DrawingPurpose, Dictionary<string, string>>();

            InitializeScaleProfiles();
            InitializeAudienceProfiles();
            InitializePurposeData();
            InitializeDefaultViewTemplateMappings();

            Logger.Info("TagContextEngine initialized: {0} scale profiles, {1} audiences, {2} template mappings",
                _scaleProfiles.Count, _audienceProfiles.Count, _viewTemplateMappings.Count);
        }

        private void InitializeScaleProfiles()
        {
            _scaleProfiles.Add(new ScaleProfile { ProfileName = "Detail",   ScaleMin = 1,   ScaleMax = 75,             DetailLevel = DetailLevel.Full,        MaxTextLength = 120, IncludeLeaders = true,  TextSizeMultiplier = 1.0, MaxContentLines = 5, ShowSecondaryInfo = true });
            _scaleProfiles.Add(new ScaleProfile { ProfileName = "Standard", ScaleMin = 76,  ScaleMax = 150,            DetailLevel = DetailLevel.Standard,     MaxTextLength = 60,  IncludeLeaders = true,  TextSizeMultiplier = 1.0, MaxContentLines = 3, ShowSecondaryInfo = true });
            _scaleProfiles.Add(new ScaleProfile { ProfileName = "Overview", ScaleMin = 151, ScaleMax = 300,            DetailLevel = DetailLevel.Abbreviated,  MaxTextLength = 30,  IncludeLeaders = false, TextSizeMultiplier = 1.2, MaxContentLines = 2, ShowSecondaryInfo = false });
            _scaleProfiles.Add(new ScaleProfile { ProfileName = "Site",     ScaleMin = 301, ScaleMax = double.MaxValue, DetailLevel = DetailLevel.Minimal,     MaxTextLength = 15,  IncludeLeaders = false, TextSizeMultiplier = 1.5, MaxContentLines = 1, ShowSecondaryInfo = false });
        }

        private void InitializeAudienceProfiles()
        {
            AddAudience("Architects",
                priority: new[] { "Rooms", "Doors", "Windows", "Walls", "Floors", "Ceilings", "Stairs" },
                parameters: new[] { "Room_Name", "Room_Number", "Area", "Material_Finish", "Width", "Height", "Level", "Mark", "Wall_Finish", "Floor_Finish" },
                hidden: new[] { "Conduits", "Cable Trays", "Pipe Accessories" },
                excluded: new[] { "Circuit_Number", "Panel_Name", "System_Abbreviation", "Structural_Usage", "Rebar_Cover" },
                technical: false);

            AddAudience("Structural Engineers",
                priority: new[] { "Structural Columns", "Structural Framing", "Structural Foundations", "Floors", "Walls" },
                parameters: new[] { "Mark", "Size", "Structural_Usage", "Load_Bearing", "Design_Load", "Rebar_Cover", "Concrete_Grade", "Steel_Grade", "Section_Shape" },
                hidden: new[] { "Furniture", "Plumbing Fixtures", "Specialty Equipment", "Electrical Fixtures" },
                excluded: new[] { "Room_Name", "Room_Number", "Material_Finish", "Ceiling_Height", "Floor_Finish" },
                technical: true);

            AddAudience("MEP Engineers",
                priority: new[] { "Mechanical Equipment", "Ducts", "Air Terminals", "Pipes", "Plumbing Fixtures", "Electrical Equipment", "Lighting Fixtures", "Cable Trays" },
                parameters: new[] { "System_Type", "System_Abbreviation", "Size", "Flow", "Circuit_Number", "Panel_Name", "Voltage", "Capacity", "Mounting_Height", "CFM" },
                hidden: new[] { "Furniture", "Curtain Panels", "Railings", "Stairs" },
                excluded: new[] { "Room_Name", "Wall_Finish", "Floor_Finish", "Ceiling_Height", "Material_Finish" },
                technical: true);

            AddAudience("Contractors",
                priority: new[] { "Doors", "Windows", "Walls", "Floors", "Mechanical Equipment", "Electrical Equipment", "Plumbing Fixtures" },
                parameters: new[] { "Mark", "Type_Name", "Material", "Mounting_Height", "Width", "Height", "Level", "Size", "Installation_Method", "Connection_Type" },
                hidden: Array.Empty<string>(),
                excluded: new[] { "Design_Load", "CFM", "Pressure_Drop", "Structural_Usage", "Rebar_Cover" },
                technical: false);

            AddAudience("Facility Managers",
                priority: new[] { "Rooms", "Mechanical Equipment", "Electrical Equipment", "Plumbing Fixtures", "Lighting Fixtures", "Doors" },
                parameters: new[] { "Asset_ID", "Serial_Number", "Manufacturer", "Model", "Warranty_Expiry", "Maintenance_Schedule", "Room_Name", "Room_Number", "Level" },
                hidden: new[] { "Structural Columns", "Structural Framing", "Structural Foundations", "Rebar" },
                excluded: new[] { "Design_Load", "Rebar_Cover", "Concrete_Grade", "Steel_Grade", "Structural_Usage" },
                technical: false);

            AddAudience("Code Reviewers",
                priority: new[] { "Doors", "Walls", "Rooms", "Stairs", "Fire Alarm Devices", "Sprinklers" },
                parameters: new[] { "Fire_Rating", "Egress_Distance", "Occupancy_Type", "Occupant_Load", "Compartment_ID", "Accessibility_Compliant", "Exit_Width", "Mark" },
                hidden: new[] { "Furniture", "Curtain Panels", "Specialty Equipment" },
                excluded: new[] { "Material_Finish", "Floor_Finish", "Ceiling_Height", "Wall_Finish", "Asset_ID", "Serial_Number" },
                technical: true);
        }

        private void AddAudience(string name, string[] priority, string[] parameters,
            string[] hidden, string[] excluded, bool technical)
        {
            _audienceProfiles[name] = new AudienceProfile
            {
                Name = name,
                PriorityCategories = new List<string>(priority),
                PriorityParameters = new List<string>(parameters),
                HiddenCategories = new List<string>(hidden),
                ExcludedParameters = new List<string>(excluded),
                PreferTechnicalNotation = technical
            };
        }

        private void InitializePurposeData()
        {
            var d = StringComparer.OrdinalIgnoreCase;

            // Purpose parameters: which parameters matter per category per purpose
            _purposeParameters[DrawingPurpose.Design] = new Dictionary<string, List<string>>(d) {
                { "Doors", L("Mark", "Type_Name", "Width", "Height", "Fire_Rating", "Frame_Material") },
                { "Windows", L("Mark", "Type_Name", "Width", "Height", "U_Value", "SHGC", "Glass_Type") },
                { "Walls", L("Type_Name", "Width", "Fire_Rating", "Acoustic_Rating", "Structure_Type") },
                { "Rooms", L("Room_Number", "Room_Name", "Area", "Design_Load", "Occupancy_Type", "Ceiling_Height") },
                { "Mechanical Equipment", L("Mark", "Type_Name", "Capacity", "CFM", "Power", "Weight") },
                { "Structural Columns", L("Mark", "Section_Shape", "Size", "Steel_Grade", "Design_Load") }
            };
            _purposeParameters[DrawingPurpose.Construction] = new Dictionary<string, List<string>>(d) {
                { "Doors", L("Mark", "Type_Name", "Mounting_Height", "Hardware_Set", "Installation_Method") },
                { "Windows", L("Mark", "Type_Name", "Sill_Height", "Head_Height", "Frame_Material") },
                { "Walls", L("Type_Name", "Width", "Material", "Connection_Type", "Level") },
                { "Rooms", L("Room_Number", "Room_Name", "Floor_Finish", "Wall_Finish", "Ceiling_Finish") },
                { "Mechanical Equipment", L("Mark", "Mounting_Height", "Connection_Type", "Weight", "Clearance") },
                { "Electrical Equipment", L("Mark", "Panel_Name", "Voltage", "Circuit_Number", "Mounting_Height") }
            };
            _purposeParameters[DrawingPurpose.AsBuilt] = new Dictionary<string, List<string>>(d) {
                { "Doors", L("Mark", "Asset_ID", "Manufacturer", "Model", "Serial_Number") },
                { "Rooms", L("Room_Number", "Room_Name", "Area", "Department", "Occupancy_Type") },
                { "Mechanical Equipment", L("Mark", "Asset_ID", "Serial_Number", "Manufacturer", "Maintenance_Schedule") },
                { "Electrical Equipment", L("Mark", "Asset_ID", "Serial_Number", "Panel_Name") }
            };
            _purposeParameters[DrawingPurpose.Coordination] = new Dictionary<string, List<string>>(d) {
                { "Ducts", L("System_Type", "Size", "System_Abbreviation", "Clearance", "Insulation") },
                { "Pipes", L("System_Type", "Size", "System_Abbreviation", "Clearance", "Insulation") },
                { "Mechanical Equipment", L("Mark", "System_Type", "Size", "Clearance", "Service_Access") }
            };
            _purposeParameters[DrawingPurpose.Presentation] = new Dictionary<string, List<string>>(d) {
                { "Rooms", L("Room_Name", "Area") }, { "Doors", L("Mark") }, { "Windows", L("Mark") }
            };
            _purposeParameters[DrawingPurpose.Regulatory] = new Dictionary<string, List<string>>(d) {
                { "Doors", L("Mark", "Fire_Rating", "Accessibility_Compliant", "Exit_Width") },
                { "Walls", L("Type_Name", "Fire_Rating", "Acoustic_Rating", "Smoke_Zone") },
                { "Rooms", L("Room_Number", "Room_Name", "Occupancy_Type", "Occupant_Load", "Egress_Distance") },
                { "Stairs", L("Mark", "Width", "Riser_Height", "Tread_Depth", "Handrail_Height") }
            };

            // Purpose expressions: tag content templates per purpose/category
            _purposeExpressions[DrawingPurpose.Design] = new Dictionary<string, string>(d) {
                { "Doors", "{Mark}\n{Type_Name}\n{Width}x{Height}\n{IF Fire_Rating != null THEN \"FR: \" + Fire_Rating ELSE \"\"}" },
                { "Windows", "{Mark}\n{Type_Name}\n{Width}x{Height}\nU={U_Value:F2}" },
                { "Walls", "{Type_Name}\nw={Width}\n{IF Fire_Rating != null THEN \"FR: \" + Fire_Rating ELSE \"\"}" },
                { "Rooms", "{Room_Number}\n{Room_Name}\n{Area:F1} m2\n{IF Occupancy_Type != null THEN Occupancy_Type ELSE \"\"}" },
                { "Mechanical Equipment", "{Mark}\n{Type_Name}\n{Capacity}\n{IF CFM != null THEN CFM + \" CFM\" ELSE \"\"}" },
                { "Structural Columns", "{Mark}\n{Section_Shape} {Size}\n{Steel_Grade}" }
            };
            _purposeExpressions[DrawingPurpose.Construction] = new Dictionary<string, string>(d) {
                { "Doors", "{Mark}\n{Type_Name}\nMH: {Mounting_Height}\n{Hardware_Set}" },
                { "Windows", "{Mark}\nSH: {Sill_Height}\nHH: {Head_Height}" },
                { "Rooms", "{Room_Number} - {Room_Name}\nFLR: {Floor_Finish}\nWALL: {Wall_Finish}" },
                { "Mechanical Equipment", "{Mark}\nMH: {Mounting_Height}\n{Connection_Type}\nWT: {Weight}" },
                { "Electrical Equipment", "{Mark}\n{Panel_Name}\n{Voltage}V\nMH: {Mounting_Height}" }
            };
            _purposeExpressions[DrawingPurpose.AsBuilt] = new Dictionary<string, string>(d) {
                { "Doors", "{Mark}\nID: {Asset_ID}\n{Manufacturer} {Model}" },
                { "Rooms", "{Room_Number} - {Room_Name}\n{Area:F1} m2" },
                { "Mechanical Equipment", "{Mark}\nID: {Asset_ID}\nSN: {Serial_Number}\n{Manufacturer}" },
                { "Electrical Equipment", "{Mark}\nID: {Asset_ID}\n{Panel_Name}\nSN: {Serial_Number}" }
            };
            _purposeExpressions[DrawingPurpose.Coordination] = new Dictionary<string, string>(d) {
                { "Ducts", "{System_Abbreviation}\n{Size}\nCLR: {Clearance}" },
                { "Pipes", "{System_Abbreviation}\n{Size}\nCLR: {Clearance}" },
                { "Mechanical Equipment", "{Mark}\n{System_Type}\nCLR: {Clearance}" }
            };
            _purposeExpressions[DrawingPurpose.Presentation] = new Dictionary<string, string>(d) {
                { "Rooms", "{Room_Name}\n{Area:F0} m2" }, { "Doors", "{Mark}" }, { "Windows", "{Mark}" }
            };
            _purposeExpressions[DrawingPurpose.Regulatory] = new Dictionary<string, string>(d) {
                { "Doors", "{Mark}\nFR: {Fire_Rating}\n{IF Accessibility_Compliant != null THEN \"ACC\" ELSE \"\"}" },
                { "Walls", "{Type_Name}\nFR: {Fire_Rating}\n{IF Acoustic_Rating != null THEN \"STC: \" + Acoustic_Rating ELSE \"\"}" },
                { "Rooms", "{Room_Number}\n{Occupancy_Type}\nOCC: {Occupant_Load}\nEGR: {Egress_Distance}" },
                { "Stairs", "{Mark}\nW: {Width}\nR: {Riser_Height} T: {Tread_Depth}" }
            };
        }

        private static List<string> L(params string[] items) => new List<string>(items);

        private void InitializeDefaultViewTemplateMappings()
        {
            void Map(string pattern, DrawingPurpose purpose, string audienceName)
            {
                _audienceProfiles.TryGetValue(audienceName, out var audience);
                _viewTemplateMappings.Add(new ViewTemplateMapping
                {
                    ViewTemplateName = pattern,
                    Purpose = purpose,
                    Audience = audience
                });
            }

            Map("*Construction*", DrawingPurpose.Construction, "Contractors");
            Map("*Install*",      DrawingPurpose.Construction, "Contractors");
            Map("*Coordination*", DrawingPurpose.Coordination, "MEP Engineers");
            Map("*Clash*",        DrawingPurpose.Coordination, "MEP Engineers");
            Map("*As-Built*",     DrawingPurpose.AsBuilt,      "Facility Managers");
            Map("*AsBuilt*",      DrawingPurpose.AsBuilt,      "Facility Managers");
            Map("*Record*",       DrawingPurpose.AsBuilt,      "Facility Managers");
            Map("*Presentation*", DrawingPurpose.Presentation, "Architects");
            Map("*Render*",       DrawingPurpose.Presentation, "Architects");
            Map("*Code*",         DrawingPurpose.Regulatory,   "Code Reviewers");
            Map("*Compliance*",   DrawingPurpose.Regulatory,   "Code Reviewers");
            Map("*Fire*",         DrawingPurpose.Regulatory,   "Code Reviewers");
            Map("*Egress*",       DrawingPurpose.Regulatory,   "Code Reviewers");
            Map("*Structural*",   DrawingPurpose.Design,       "Structural Engineers");
            Map("*Mechanical*",   DrawingPurpose.Design,       "MEP Engineers");
            Map("*Electrical*",   DrawingPurpose.Design,       "MEP Engineers");
            Map("*Plumbing*",     DrawingPurpose.Design,       "MEP Engineers");
        }

        #endregion

        #region Context Resolution Pipeline

        /// <summary>
        /// Resolves the complete tag context for a specific view, category, and context signals.
        /// Merges scale, purpose, audience, density, and view template signals into a single
        /// <see cref="ContextResolution"/>.
        /// </summary>
        public ContextResolution ResolveContext(int viewId, ViewTagContext context, string categoryName)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var resolution = new ContextResolution();

            try
            {
                // Step 1: Start with default template configuration
                var template = _repository.GetBestTemplate(categoryName, context.ViewType);
                if (template != null)
                {
                    resolution.ResolvedContentExpression = template.ContentExpression;
                    resolution.ResolvedLeaderType = template.LeaderType;
                    resolution.ContextFactors.Add(new ContextFactor
                    {
                        FactorName = "DefaultTemplate",
                        Description = $"Base template: {template.Name}",
                        Source = $"Template:{template.Name}"
                    });
                }
                else
                {
                    resolution.ResolvedContentExpression = "{Mark}";
                    resolution.ContextFactors.Add(new ContextFactor
                    {
                        FactorName = "DefaultTemplate",
                        Description = "No template found, using fallback {Mark} expression",
                        Source = "Fallback"
                    });
                }

                // Step 2: Apply scale-adaptive modifications
                ApplyScaleAdaptation(resolution, context.Scale);

                // Step 3: Apply purpose-based content filtering
                DrawingPurpose purpose = DetectPurposeFromViewTemplate(context.ViewName);
                ApplyPurposeFilter(resolution, purpose, categoryName);

                // Step 4: Apply audience parameter priorities
                AudienceProfile audience = DetectAudienceFromViewTemplate(context.ViewName);
                if (audience != null)
                {
                    ApplyAudienceFilter(resolution, audience, categoryName);
                }

                // Step 5: Apply density-responsive adjustments
                double localDensity = CalculateLocalDensity(context);
                ApplyDensityAdjustments(resolution, localDensity);

                // Step 6: Apply view template overrides
                ApplyViewTemplateOverrides(resolution, context.ViewName, categoryName);

                resolution.Success = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to resolve context for view {0}, category {1}", viewId, categoryName);
                resolution.Success = false;
                resolution.Messages.Add($"Context resolution error: {ex.Message}");
            }

            return resolution;
        }

        private void ApplyScaleAdaptation(ContextResolution resolution, double viewScale)
        {
            var profile = GetScaleProfile(viewScale);
            if (profile == null) return;
            resolution.ResolvedDetailLevel = profile.DetailLevel;
            resolution.ResolvedTextSize = profile.TextSizeMultiplier;
            resolution.ResolvedMaxTextLength = profile.MaxTextLength;
            resolution.ResolvedMaxContentLines = profile.MaxContentLines;
            resolution.IncludeSecondaryInfo = profile.ShowSecondaryInfo;
            if (!profile.IncludeLeaders) resolution.ResolvedLeaderType = LeaderType.None;
            resolution.ResolvedContentExpression = AdaptContentForScale(
                resolution.ResolvedContentExpression, viewScale);
            resolution.ContextFactors.Add(new ContextFactor {
                FactorName = "ScaleAdaptation",
                Description = $"Scale 1:{viewScale} -> {profile.ProfileName} ({profile.DetailLevel})",
                Source = $"ScaleProfile:{profile.ProfileName}"
            });
        }

        private void ApplyPurposeFilter(ContextResolution resolution, DrawingPurpose purpose, string categoryName)
        {
            string purposeExpression = GetPurposeExpression(purpose, categoryName);
            if (!string.IsNullOrEmpty(purposeExpression))
                resolution.ResolvedContentExpression = purposeExpression;
            else
                resolution.ResolvedContentExpression = FilterForPurpose(
                    resolution.ResolvedContentExpression, purpose);
            resolution.ContextFactors.Add(new ContextFactor {
                FactorName = "PurposeFilter",
                Description = $"Drawing purpose: {purpose}",
                Source = $"Purpose:{purpose}"
            });
        }

        private void ApplyAudienceFilter(ContextResolution resolution, AudienceProfile audience, string categoryName)
        {
            resolution.ResolvedContentExpression = FilterForAudience(
                resolution.ResolvedContentExpression, audience);
            if (audience.DetailPreference.HasValue)
                resolution.ResolvedDetailLevel = audience.DetailPreference.Value;
            if (audience.HiddenCategories.Any(c =>
                string.Equals(c, categoryName, StringComparison.OrdinalIgnoreCase)))
            {
                resolution.ResolvedDetailLevel = DetailLevel.Minimal;
                resolution.ResolvedContentExpression = "{Mark}";
                resolution.Messages.Add($"Category '{categoryName}' de-emphasized for {audience.Name}");
            }
            resolution.ContextFactors.Add(new ContextFactor {
                FactorName = "AudienceFilter",
                Description = $"Audience: {audience.Name}",
                Source = $"Audience:{audience.Name}"
            });
        }

        private void ApplyDensityAdjustments(ContextResolution resolution, double localDensity)
        {
            string densityClass;
            if (localDensity <= _densityThresholds.LowDensity)
            {
                densityClass = "Low";
                resolution.OffsetMultiplier = 1.5;
                resolution.EnableStacking = false;
                resolution.ShouldAbbreviate = false;
            }
            else if (localDensity <= _densityThresholds.MediumDensity)
            {
                densityClass = "Medium";
                resolution.OffsetMultiplier = 1.0;
                resolution.EnableStacking = false;
                resolution.ShouldAbbreviate = false;
            }
            else if (localDensity <= _densityThresholds.HighDensity)
            {
                densityClass = "High";
                resolution.OffsetMultiplier = 0.7;
                resolution.EnableStacking = true;
                resolution.ShouldAbbreviate = true;
                resolution.ResolvedMaxContentLines = Math.Max(1, resolution.ResolvedMaxContentLines - 1);
                if (resolution.ResolvedMaxTextLength > 20)
                    resolution.ResolvedMaxTextLength = (int)(resolution.ResolvedMaxTextLength * 0.6);
            }
            else
            {
                densityClass = "VeryHigh";
                resolution.OffsetMultiplier = 0.5;
                resolution.EnableStacking = true;
                resolution.ShouldAbbreviate = true;
                resolution.ResolvedMaxContentLines = 1;
                resolution.IncludeSecondaryInfo = false;
                resolution.ResolvedMaxTextLength = Math.Min(resolution.ResolvedMaxTextLength, 15);
                if (resolution.ResolvedLeaderType != LeaderType.None)
                    resolution.ResolvedLeaderType = LeaderType.Straight;
            }
            resolution.ContextFactors.Add(new ContextFactor {
                FactorName = "DensityAdjustment",
                Description = $"Local density: {localDensity:F2} elements/unit2 ({densityClass})",
                Source = $"Density:{densityClass}"
            });
        }

        private void ApplyViewTemplateOverrides(ContextResolution resolution, string viewName, string categoryName)
        {
            ViewTemplateMapping mapping = FindViewTemplateMapping(viewName);
            if (mapping == null) return;
            if (mapping.ContentExpressionOverrides.TryGetValue(categoryName, out string expressionOverride))
                resolution.ResolvedContentExpression = expressionOverride;
            if (mapping.LeaderTypeOverride.HasValue)
                resolution.ResolvedLeaderType = mapping.LeaderTypeOverride.Value;
            if (mapping.TextSizeMultiplierOverride.HasValue)
                resolution.ResolvedTextSize = mapping.TextSizeMultiplierOverride.Value;
            if (mapping.ScaleProfileOverride != null)
            {
                resolution.ResolvedDetailLevel = mapping.ScaleProfileOverride.DetailLevel;
                resolution.ResolvedMaxTextLength = mapping.ScaleProfileOverride.MaxTextLength;
                resolution.ResolvedMaxContentLines = mapping.ScaleProfileOverride.MaxContentLines;
                resolution.IncludeSecondaryInfo = mapping.ScaleProfileOverride.ShowSecondaryInfo;
            }
            resolution.ContextFactors.Add(new ContextFactor {
                FactorName = "ViewTemplateOverride",
                Description = $"View template match: {mapping.ViewTemplateName}",
                Source = $"ViewTemplate:{mapping.ViewTemplateName}"
            });
        }

        private double CalculateLocalDensity(ViewTagContext context)
        {
            if (context.CropRegion == null) return 0.0;
            double viewArea = context.CropRegion.Area;
            if (viewArea < 1e-10) return 0.0;
            return (context.ExistingAnnotationBounds?.Count ?? 0) / viewArea;
        }

        #endregion

        #region Scale-Adaptive Formatting

        /// <summary>
        /// Adapts a content expression for the given view scale. Reduces content lines,
        /// removes secondary tokens, and abbreviates text for smaller-scale views.
        /// </summary>
        public string AdaptContentForScale(string expression, double viewScale)
        {
            if (string.IsNullOrEmpty(expression))
                return expression;

            var profile = GetScaleProfile(viewScale);
            if (profile == null)
                return expression;

            string adapted = expression;

            switch (profile.DetailLevel)
            {
                case DetailLevel.Full:
                    break;

                case DetailLevel.Standard:
                    adapted = StripConditionalBlocks(adapted);
                    adapted = LimitContentLines(adapted, profile.MaxContentLines);
                    break;

                case DetailLevel.Abbreviated:
                    adapted = StripConditionalBlocks(adapted);
                    adapted = LimitContentLines(adapted, profile.MaxContentLines);
                    adapted = StripFormatSpecifiers(adapted);
                    break;

                case DetailLevel.Minimal:
                    adapted = ExtractPrimaryToken(adapted);
                    break;
            }

            return adapted;
        }

        /// <summary>
        /// Gets the scale profile matching the given view scale.
        /// Custom profiles are checked before predefined profiles.
        /// </summary>
        public ScaleProfile GetScaleProfile(double viewScale)
        {
            lock (_profilesLock)
            {
                foreach (var profile in _customScaleProfiles)
                {
                    if (viewScale >= profile.ScaleMin && viewScale <= profile.ScaleMax)
                        return profile;
                }
                foreach (var profile in _scaleProfiles)
                {
                    if (viewScale >= profile.ScaleMin && viewScale <= profile.ScaleMax)
                        return profile;
                }
            }
            return null;
        }

        /// <summary>
        /// Adds a custom scale profile. Custom profiles take precedence over predefined ones.
        /// </summary>
        public void AddCustomScaleProfile(ScaleProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            lock (_profilesLock)
            {
                _customScaleProfiles.Add(profile);
                Logger.Info("Added custom scale profile: {0} ({1}-{2})",
                    profile.ProfileName, profile.ScaleMin, profile.ScaleMax);
            }
        }

        /// <summary>
        /// Removes all custom scale profiles.
        /// </summary>
        public void ClearCustomScaleProfiles()
        {
            lock (_profilesLock) { _customScaleProfiles.Clear(); }
        }

        #endregion

        #region Purpose-Based Content

        /// <summary>
        /// Filters a content expression based on drawing purpose. Removes parameters
        /// not relevant to the specified purpose.
        /// </summary>
        public string FilterForPurpose(string expression, DrawingPurpose purpose)
        {
            if (string.IsNullOrEmpty(expression))
                return expression;

            var paramRefs = ExtractParameterReferences(expression);
            if (paramRefs.Count == 0)
                return expression;

            // Build the allowed parameter set for this purpose across all categories
            var allowedParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_purposeParameters.TryGetValue(purpose, out var purposeCategories))
            {
                foreach (var categoryParams in purposeCategories.Values)
                    foreach (var param in categoryParams)
                        allowedParams.Add(param);
            }

            if (allowedParams.Count == 0)
                return expression;

            // Remove lines containing only excluded parameters
            var lines = expression.Split('\n');
            var keptLines = new List<string>();

            foreach (string line in lines)
            {
                var lineParams = ExtractParameterReferences(line);
                if (lineParams.Count == 0)
                {
                    keptLines.Add(line);
                    continue;
                }
                if (lineParams.Any(p => allowedParams.Contains(p)))
                    keptLines.Add(line);
            }

            return keptLines.Count > 0 ? string.Join("\n", keptLines) : expression;
        }

        /// <summary>
        /// Gets the purpose-specific content expression for a category.
        /// </summary>
        public string GetPurposeExpression(DrawingPurpose purpose, string categoryName)
        {
            if (_purposeExpressions.TryGetValue(purpose, out var cats))
                if (cats.TryGetValue(categoryName, out string expr))
                    return expr;
            return null;
        }

        /// <summary>
        /// Detects the drawing purpose from a view template name using keyword matching.
        /// Defaults to Design if no keywords match.
        /// </summary>
        public DrawingPurpose DetectPurposeFromViewTemplate(string viewTemplateName)
        {
            if (string.IsNullOrEmpty(viewTemplateName))
                return DrawingPurpose.Design;

            string n = viewTemplateName.ToUpperInvariant();

            if (n.Contains("CONSTRUCTION") || n.Contains("INSTALL") ||
                n.Contains("SHOP DRAWING") || n.Contains("FABRICATION"))
                return DrawingPurpose.Construction;

            if (n.Contains("AS-BUILT") || n.Contains("ASBUILT") ||
                n.Contains("RECORD") || n.Contains("HANDOVER") ||
                n.Contains("FACILITY") || n.Contains("O&M"))
                return DrawingPurpose.AsBuilt;

            if (n.Contains("COORDINATION") || n.Contains("CLASH") ||
                n.Contains("COMBINED") || n.Contains("COMPOSITE"))
                return DrawingPurpose.Coordination;

            if (n.Contains("PRESENTATION") || n.Contains("RENDER") ||
                n.Contains("CLIENT") || n.Contains("MARKETING") ||
                n.Contains("CONCEPT") || n.Contains("SCHEMATIC"))
                return DrawingPurpose.Presentation;

            if (n.Contains("CODE") || n.Contains("COMPLIANCE") ||
                n.Contains("FIRE") || n.Contains("EGRESS") ||
                n.Contains("ACCESSIBILITY") || n.Contains("REGULATORY"))
                return DrawingPurpose.Regulatory;

            return DrawingPurpose.Design;
        }

        #endregion

        #region Audience-Aware Filtering

        /// <summary>
        /// Filters a content expression based on the audience profile. Removes parameters
        /// that are excluded for the audience.
        /// </summary>
        public string FilterForAudience(string expression, AudienceProfile audience)
        {
            if (string.IsNullOrEmpty(expression) || audience == null)
                return expression;

            string filtered = expression;
            foreach (string excludedParam in audience.ExcludedParameters)
                filtered = RemoveParameterFromExpression(filtered, excludedParam);

            filtered = CleanUpExpression(filtered);
            return string.IsNullOrWhiteSpace(filtered) ? expression : filtered;
        }

        /// <summary>
        /// Detects the audience profile from a view template name using keyword matching.
        /// </summary>
        public AudienceProfile DetectAudienceFromViewTemplate(string viewTemplateName)
        {
            if (string.IsNullOrEmpty(viewTemplateName))
                return null;

            ViewTemplateMapping mapping = FindViewTemplateMapping(viewTemplateName);
            if (mapping?.Audience != null)
                return mapping.Audience;

            string n = viewTemplateName.ToUpperInvariant();

            if (n.Contains("STRUCTURAL") || n.Contains("FRAMING") || n.Contains("FOUNDATION"))
                return _audienceProfiles.TryGetValue("Structural Engineers", out var se) ? se : null;

            if (n.Contains("MECHANICAL") || n.Contains("ELECTRICAL") ||
                n.Contains("PLUMBING") || n.Contains("MEP") || n.Contains("HVAC"))
                return _audienceProfiles.TryGetValue("MEP Engineers", out var mep) ? mep : null;

            if (n.Contains("FACILITY") || n.Contains("MAINTENANCE") ||
                n.Contains("AS-BUILT") || n.Contains("ASBUILT") || n.Contains("O&M"))
                return _audienceProfiles.TryGetValue("Facility Managers", out var fm) ? fm : null;

            if (n.Contains("CODE") || n.Contains("COMPLIANCE") ||
                n.Contains("FIRE") || n.Contains("EGRESS"))
                return _audienceProfiles.TryGetValue("Code Reviewers", out var cr) ? cr : null;

            if (n.Contains("CONSTRUCTION") || n.Contains("INSTALL") || n.Contains("SHOP"))
                return _audienceProfiles.TryGetValue("Contractors", out var co) ? co : null;

            if (n.Contains("ARCHITECTURAL") || n.Contains("FLOOR PLAN") || n.Contains("INTERIOR"))
                return _audienceProfiles.TryGetValue("Architects", out var ar) ? ar : null;

            return null;
        }

        /// <summary>Gets a named audience profile.</summary>
        public AudienceProfile GetAudienceProfile(string audienceName)
        {
            return _audienceProfiles.TryGetValue(audienceName, out var profile) ? profile : null;
        }

        /// <summary>Adds or replaces an audience profile.</summary>
        public void RegisterAudienceProfile(AudienceProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrEmpty(profile.Name)) throw new ArgumentException("Profile must have a name.");
            _audienceProfiles[profile.Name] = profile;
            Logger.Info("Registered audience profile: {0}", profile.Name);
        }

        #endregion

        #region Density-Responsive Content

        /// <summary>
        /// Adjusts a tag template definition based on local element density.
        /// Returns a modified copy; the original is not mutated.
        /// </summary>
        public TagTemplateDefinition AdjustForDensity(TagTemplateDefinition template, double localDensity)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            var adjusted = new TagTemplateDefinition
            {
                Name = template.Name,
                Description = template.Description,
                CategoryName = template.CategoryName,
                ViewTypes = new List<TagViewType>(template.ViewTypes),
                TagFamilyName = template.TagFamilyName,
                TagTypeName = template.TagTypeName,
                PreferredPositions = new List<TagPosition>(template.PreferredPositions),
                LeaderType = template.LeaderType,
                MinLeaderLength = template.MinLeaderLength,
                MaxLeaderLength = template.MaxLeaderLength,
                LeaderDistanceThreshold = template.LeaderDistanceThreshold,
                Orientation = template.Orientation,
                FollowElementRotation = template.FollowElementRotation,
                ContentExpression = template.ContentExpression,
                OffsetX = template.OffsetX,
                OffsetY = template.OffsetY,
                AllowStacking = template.AllowStacking,
                AllowHostOverlap = template.AllowHostOverlap,
                Alignment = template.Alignment,
                FallbackChain = new List<CollisionAction>(template.FallbackChain),
                InheritsFrom = template.InheritsFrom
            };

            if (localDensity <= _densityThresholds.LowDensity)
            {
                // Low density: expand offsets for better readability
                adjusted.OffsetX *= 1.5;
                adjusted.OffsetY *= 1.5;
                adjusted.MaxLeaderLength *= 1.3;
            }
            else if (localDensity <= _densityThresholds.MediumDensity)
            {
                // Medium density: keep defaults
            }
            else if (localDensity <= _densityThresholds.HighDensity)
            {
                // High density: compact layout
                adjusted.OffsetX *= 0.7;
                adjusted.OffsetY *= 0.7;
                adjusted.MaxLeaderLength *= 0.6;
                adjusted.AllowStacking = true;
                adjusted.ContentExpression = StripConditionalBlocks(adjusted.ContentExpression);
                adjusted.ContentExpression = LimitContentLines(adjusted.ContentExpression, 2);
                if (!adjusted.FallbackChain.Contains(CollisionAction.Abbreviate))
                    adjusted.FallbackChain.Insert(0, CollisionAction.Abbreviate);
            }
            else
            {
                // Very high density: minimal layout
                adjusted.OffsetX *= 0.5;
                adjusted.OffsetY *= 0.5;
                adjusted.MaxLeaderLength *= 0.4;
                adjusted.MinLeaderLength = 0;
                adjusted.AllowStacking = true;
                adjusted.AllowHostOverlap = true;
                adjusted.ContentExpression = ExtractPrimaryToken(adjusted.ContentExpression);
                adjusted.FallbackChain = new List<CollisionAction>
                {
                    CollisionAction.Abbreviate,
                    CollisionAction.Stack,
                    CollisionAction.Nudge,
                    CollisionAction.FlagManual
                };
            }

            return adjusted;
        }

        /// <summary>Sets custom density thresholds.</summary>
        public void SetDensityThresholds(DensityThresholds thresholds)
        {
            _densityThresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
            Logger.Info("Density thresholds updated: low={0}, medium={1}, high={2}",
                thresholds.LowDensity, thresholds.MediumDensity, thresholds.HighDensity);
        }

        /// <summary>Gets the current density thresholds.</summary>
        public DensityThresholds GetDensityThresholds() => _densityThresholds;

        #endregion

        #region View Template Integration

        /// <summary>Registers a view template mapping.</summary>
        public void RegisterViewTemplateMapping(ViewTemplateMapping mapping)
        {
            if (mapping == null) throw new ArgumentNullException(nameof(mapping));
            lock (_mappingsLock)
            {
                _viewTemplateMappings.Add(mapping);
                Logger.Info("Registered view template mapping: {0} -> {1}",
                    mapping.ViewTemplateName, mapping.Purpose);
            }
        }

        /// <summary>Removes all view template mappings matching the given pattern.</summary>
        public int RemoveViewTemplateMapping(string viewTemplateName)
        {
            lock (_mappingsLock)
            {
                int removed = _viewTemplateMappings.RemoveAll(m =>
                    string.Equals(m.ViewTemplateName, viewTemplateName, StringComparison.OrdinalIgnoreCase));
                if (removed > 0)
                    Logger.Info("Removed {0} view template mapping(s) for: {1}", removed, viewTemplateName);
                return removed;
            }
        }

        /// <summary>Gets all registered view template mappings.</summary>
        public List<ViewTemplateMapping> GetViewTemplateMappings()
        {
            lock (_mappingsLock) { return _viewTemplateMappings.ToList(); }
        }

        /// <summary>
        /// Finds the best matching view template mapping for a view name.
        /// Supports wildcard (*) matching in template name patterns.
        /// </summary>
        public ViewTemplateMapping FindViewTemplateMapping(string viewName)
        {
            if (string.IsNullOrEmpty(viewName))
                return null;

            lock (_mappingsLock)
            {
                var exact = _viewTemplateMappings.FirstOrDefault(m =>
                    string.Equals(m.ViewTemplateName, viewName, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;

                foreach (var mapping in _viewTemplateMappings)
                {
                    if (WildcardMatch(viewName, mapping.ViewTemplateName))
                        return mapping;
                }
            }
            return null;
        }

        #endregion

        #region Private Methods - Expression Manipulation

        /// <summary>
        /// Strips conditional blocks ({IF ... THEN ... ELSE ...}) from an expression.
        /// </summary>
        private string StripConditionalBlocks(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return expression;

            string stripped = Regex.Replace(
                expression,
                @"\{IF\s+.+?\s+THEN\s+.+?\s+ELSE\s+.+?\}",
                string.Empty,
                RegexOptions.IgnoreCase);

            return CleanUpExpression(stripped);
        }

        /// <summary>
        /// Limits the expression to a maximum number of newline-separated lines.
        /// </summary>
        private string LimitContentLines(string expression, int maxLines)
        {
            if (string.IsNullOrEmpty(expression) || maxLines <= 0)
                return expression;

            var lines = expression.Split('\n');
            if (lines.Length <= maxLines)
                return expression;

            return string.Join("\n", lines.Take(maxLines));
        }

        /// <summary>
        /// Removes format specifiers from parameter references ({Area:F2} becomes {Area}).
        /// </summary>
        private string StripFormatSpecifiers(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return expression;
            return Regex.Replace(expression, @"\{([A-Za-z_][A-Za-z0-9_ ]*):[A-Za-z]\d+\}", "{$1}");
        }

        /// <summary>
        /// Extracts the first brace-delimited parameter reference from an expression.
        /// Falls back to the first line if no tokens found.
        /// </summary>
        private string ExtractPrimaryToken(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return expression;

            var match = Regex.Match(expression, @"\{([A-Za-z_][A-Za-z0-9_ ]*?)(?::[A-Za-z]\d+)?\}");
            if (match.Success)
                return match.Value;

            int newlineIndex = expression.IndexOf('\n');
            return newlineIndex >= 0 ? expression.Substring(0, newlineIndex).Trim() : expression.Trim();
        }

        /// <summary>
        /// Extracts all parameter names referenced in an expression, excluding special tokens.
        /// </summary>
        private List<string> ExtractParameterReferences(string expression)
        {
            var paramNames = new List<string>();
            if (string.IsNullOrEmpty(expression))
                return paramNames;

            var matches = Regex.Matches(expression, @"\{([A-Za-z_][A-Za-z0-9_ ]*?)(?::[A-Za-z]\d+)?\}");
            foreach (Match match in matches)
            {
                string paramName = match.Groups[1].Value;
                if (!string.Equals(paramName, "CLUSTER_COUNT", StringComparison.OrdinalIgnoreCase) &&
                    !paramName.StartsWith("UNIQUE_ID", StringComparison.OrdinalIgnoreCase) &&
                    !paramName.StartsWith("IF", StringComparison.OrdinalIgnoreCase))
                {
                    if (!paramNames.Contains(paramName, StringComparer.OrdinalIgnoreCase))
                        paramNames.Add(paramName);
                }
            }
            return paramNames;
        }

        /// <summary>
        /// Removes a specific parameter reference from a content expression.
        /// If the parameter is the only content on a line, the entire line is removed.
        /// </summary>
        private string RemoveParameterFromExpression(string expression, string parameterName)
        {
            if (string.IsNullOrEmpty(expression) || string.IsNullOrEmpty(parameterName))
                return expression;

            string escapedParam = Regex.Escape(parameterName);
            string cleaned = Regex.Replace(
                expression,
                @"\{" + escapedParam + @"(?::[A-Za-z]\d+)?\}",
                string.Empty,
                RegexOptions.IgnoreCase);

            var lines = cleaned.Split('\n');
            var keptLines = new List<string>();

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && !IsOnlySeparators(trimmed))
                    keptLines.Add(line);
            }

            return string.Join("\n", keptLines);
        }

        /// <summary>
        /// Returns true if the string contains only separator characters with no
        /// alphanumeric content or brace tokens.
        /// </summary>
        private bool IsOnlySeparators(string text)
        {
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c) || c == '{' || c == '}')
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Cleans up an expression by removing empty lines and separator-only lines.
        /// </summary>
        private string CleanUpExpression(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return expression;

            var lines = expression.Split('\n')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Where(l => !IsOnlySeparators(l))
                .ToList();

            return string.Join("\n", lines);
        }

        #endregion

        #region Private Methods - Wildcard Matching

        /// <summary>
        /// Case-insensitive wildcard match. Supports * as a multi-character wildcard.
        /// </summary>
        private bool WildcardMatch(string input, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return string.IsNullOrEmpty(input);

            string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
        }

        #endregion
    }
}
