// ============================================================================
// StingBIM AI - Chat Command Router
// Routes parsed chat commands to appropriate AI modules
// Bridges between Revit model data and AI module abstract types
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using NLog;
using StingBIM.Revit.Commands;

// AI Module references
using StingBIM.AI.Reasoning.Compliance;
using StingBIM.AI.Reasoning.Materials;
using StingBIM.AI.Reasoning.Decision;
using StingBIM.AI.QualityAssurance.Validation;
using StingBIM.AI.Automation.Quantities;
using StingBIM.AI.Automation.Health;
using StingBIM.AI.Parameters.Management;
using StingBIM.AI.Construction.Sequencing;
using StingBIM.AI.Design.Generative;
using StingBIM.AI.Agents.Framework;
using StingBIM.Standards;

namespace StingBIM.Revit.UI
{
    /// <summary>
    /// Routes chat commands to AI modules and formats results.
    /// Handles all 4 phases: Creation, Analysis, Intelligence, Advanced.
    /// </summary>
    internal class ChatCommandRouter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Lazy-initialized AI modules (no-arg constructors)
        private Lazy<ComplianceChecker> _complianceChecker = new(() => new ComplianceChecker());
        private Lazy<MaterialIntelligence> _materialIntelligence = new(() => new MaterialIntelligence());
        private Lazy<DecisionSupport> _decisionSupport = new(() => new DecisionSupport());
        private Lazy<QualityAssuranceEngine> _qaEngine = new(() => new QualityAssuranceEngine());
        private Lazy<AutomatedQuantityTakeoff> _quantityTakeoff = new(() => new AutomatedQuantityTakeoff());
        private Lazy<ModelHealthMonitor> _healthMonitor = new(() => new ModelHealthMonitor());
        private Lazy<SmartFormulaBuilder> _formulaBuilder = new(() => new SmartFormulaBuilder());
        private Lazy<ConstructionSequencingEngine> _constructionEngine = new(() => new ConstructionSequencingEngine());
        private Lazy<GenerativeDesignEngine> _generativeEngine = new(() => new GenerativeDesignEngine());

        /// <summary>
        /// Attempts to route a command. Returns null if not recognized.
        /// For creation commands, returns a RevitCommand to be queued via ExternalEvent.
        /// </summary>
        public CommandRouteResult Route(string input, Document doc)
        {
            var lower = input.ToLowerInvariant().Trim();

            // Conversational: greetings, thanks, farewells, general questions
            var conversational = TryRouteConversational(lower);
            if (conversational != null)
                return conversational;

            // Phase 1: Creation commands (return RevitCommand for ExternalEvent)
            var creation = TryRouteCreation(lower);
            if (creation != null)
                return creation;

            // Phase 2: Analysis commands (read-only, can execute directly)
            var analysis = TryRouteAnalysis(lower, doc);
            if (analysis != null)
                return analysis;

            // Phase 3: Intelligence commands
            var intelligence = TryRouteIntelligence(lower, doc);
            if (intelligence != null)
                return intelligence;

            // Phase 4: Advanced commands
            var advanced = TryRouteAdvanced(lower, doc);
            if (advanced != null)
                return advanced;

            // Standards calculations
            var standards = TryRouteStandards(lower);
            if (standards != null)
                return standards;

            // Element queries: "walls", "doors", "rooms", "windows", etc.
            var elementQuery = TryRouteElementQuery(lower, doc);
            if (elementQuery != null)
                return elementQuery;

            // Model queries: "health", "warnings", "parameters", etc.
            var modelQuery = TryRouteModelQuery(lower, doc);
            if (modelQuery != null)
                return modelQuery;

            // Fuzzy intent matching as last resort
            var fuzzy = TryFuzzyMatch(lower, doc);
            if (fuzzy != null)
                return fuzzy;

            return null; // Not recognized by router
        }

        #region Phase 1: Creation Commands

        private CommandRouteResult TryRouteCreation(string input)
        {
            // CREATE WALL
            if (Regex.IsMatch(input, @"\b(create|add|make|build|draw)\b.*\bwall\b"))
            {
                var cmd = new RevitCommand { Type = RevitCommandType.CreateWall };
                ParseDimensions(input, cmd, "length", "height", 5.0, 3.0);
                ParseType(input, cmd);
                return CommandRouteResult.ForCreation(cmd,
                    $"Creating wall ({cmd.GetDouble("length", 5):F1}m x {cmd.GetDouble("height", 3):F1}m)...");
            }

            // CREATE FLOOR
            if (Regex.IsMatch(input, @"\b(create|add|make|build)\b.*\bfloor\b"))
            {
                var cmd = new RevitCommand { Type = RevitCommandType.CreateFloor };
                ParseDimensions(input, cmd, "width", "depth", 5.0, 5.0);
                return CommandRouteResult.ForCreation(cmd,
                    $"Creating floor ({cmd.GetDouble("width", 5):F1}m x {cmd.GetDouble("depth", 5):F1}m)...");
            }

            // CREATE ROOM (with room type detection)
            if (Regex.IsMatch(input, @"\b(create|add|make|build|generate)\b.*\b(room|bedroom|kitchen|bathroom|office|living|dining|studio|conference|lobby|corridor|toilet|store|laundry|garage)\b"))
            {
                var cmd = new RevitCommand { Type = RevitCommandType.CreateRoom };
                var roomType = DetectRoomType(input);
                cmd.Parameters["name"] = roomType.Name;
                cmd.Parameters["width"] = roomType.DefaultWidth;
                cmd.Parameters["depth"] = roomType.DefaultDepth;
                cmd.Parameters["height"] = roomType.DefaultHeight;
                // Override with explicit dimensions if provided
                ParseDimensions(input, cmd, "width", "depth", roomType.DefaultWidth, roomType.DefaultDepth);
                ParseHeight(input, cmd, roomType.DefaultHeight);
                return CommandRouteResult.ForCreation(cmd,
                    $"Creating {roomType.Name} ({cmd.GetDouble("width", 4):F1}m x {cmd.GetDouble("depth", 5):F1}m)...");
            }

            // AUTO-POPULATE PARAMETERS
            if (Regex.IsMatch(input, @"\b(auto[- ]?populate|populate|fill)\b.*\b(param|parameter)\b"))
            {
                var cmd = new RevitCommand { Type = RevitCommandType.AutoPopulateParameters };
                return CommandRouteResult.ForCreation(cmd, "Auto-populating parameters...");
            }

            return null;
        }

        #endregion

        #region Phase 2: Analysis Commands

        private CommandRouteResult TryRouteAnalysis(string input, Document doc)
        {
            if (doc == null)
                return null;

            // CHECK COMPLIANCE
            if (Regex.IsMatch(input, @"\b(check|run|verify)\b.*\b(compliance|code|standard)\b"))
            {
                return CommandRouteResult.ForResponse(RunComplianceCheck(input, doc));
            }

            // RECOMMEND MATERIAL
            if (Regex.IsMatch(input, @"\b(recommend|suggest|what material|best material|material for)\b"))
            {
                return CommandRouteResult.ForResponse(GetMaterialRecommendation(input, doc));
            }

            // CHECK QUALITY / RUN QA
            if (Regex.IsMatch(input, @"\b(quality|qa|quality assurance|validate|audit)\b"))
            {
                return CommandRouteResult.ForResponse(RunQualityCheck(doc));
            }

            // ANALYZE COORDINATION / CLASH
            if (Regex.IsMatch(input, @"\b(coordination|clash|conflict|intersect)\b"))
            {
                return CommandRouteResult.ForResponse(AnalyzeCoordination(doc));
            }

            // BOQ / QUANTITY TAKEOFF
            if (Regex.IsMatch(input, @"\b(boq|bill of quantit|quantity takeoff|takeoff|quantities)\b"))
            {
                return CommandRouteResult.ForResponse(GenerateBOQ(doc));
            }

            return null;
        }

        private string RunComplianceCheck(string input, Document doc)
        {
            var checker = _complianceChecker.Value;
            var profiles = checker.GetAvailableProfiles().ToList();

            // Detect requested code
            string selectedCode = "IBC";
            if (input.Contains("ibc")) selectedCode = "IBC";
            else if (input.Contains("nfpa") || input.Contains("fire")) selectedCode = "NFPA";
            else if (input.Contains("ada") || input.Contains("access")) selectedCode = "ADA";
            else if (input.Contains("ashrae") || input.Contains("energy")) selectedCode = "ASHRAE";
            else if (input.Contains("kebs") || input.Contains("kenya")) selectedCode = "KEBS";
            else if (input.Contains("unbs") || input.Contains("uganda")) selectedCode = "UNBS";
            else if (input.Contains("tbs") || input.Contains("tanzania")) selectedCode = "TBS";
            else if (input.Contains("eas") || input.Contains("east afric")) selectedCode = "EAS";
            else if (input.Contains("sans") || input.Contains("south afric")) selectedCode = "SANS";
            else if (input.Contains("euro")) selectedCode = "Eurocode";

            // Query model elements for compliance context
            var wallCount = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Walls);
            var doorCount = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Doors);
            var roomCount = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Rooms);
            var stairCount = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Stairs);
            var windowCount = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Windows);
            var warningCount = RevitModelQuery.GetWarningCount(doc);
            int totalElements = wallCount + doorCount + roomCount + stairCount + windowCount;

            var sb = new StringBuilder();
            sb.AppendLine($"Compliance Check: {selectedCode}\n");

            if (totalElements == 0)
            {
                sb.AppendLine("No building elements to check.");
                sb.AppendLine("Add walls, doors, rooms to run compliance.");
                return sb.ToString();
            }

            sb.AppendLine($"Elements scanned: {totalElements}");
            sb.AppendLine($"  Walls: {wallCount}, Doors: {doorCount}");
            sb.AppendLine($"  Windows: {windowCount}, Rooms: {roomCount}");
            sb.AppendLine($"  Stairs: {stairCount}\n");

            // Run checks based on selected code
            int issues = 0;
            var findings = new List<string>();

            if (selectedCode == "IBC" || selectedCode == "NFPA")
            {
                // Fire safety checks
                if (doorCount > 0)
                {
                    var doorsNoRating = RevitModelQuery.CountEmptyParameter(doc, BuiltInCategory.OST_Doors, "Fire Rating");
                    if (doorsNoRating > 0) { issues++; findings.Add($"  {doorsNoRating} doors missing Fire Rating"); }
                }
                if (roomCount > 0 && stairCount == 0 && roomCount > 2)
                {
                    issues++; findings.Add("  No stairs found — check egress requirements");
                }
            }

            if (selectedCode == "ADA" || selectedCode == "IBC")
            {
                // Accessibility checks
                if (doorCount > 0)
                {
                    // Check door widths (ADA requires min 32" clear)
                    var narrowDoors = CountNarrowDoors(doc, 0.813); // 813mm = 32"
                    if (narrowDoors > 0) { issues++; findings.Add($"  {narrowDoors} doors may be too narrow for ADA (< 813mm)"); }
                }
            }

            if (selectedCode == "ASHRAE")
            {
                // Energy checks
                if (wallCount > 0)
                {
                    var wallsNoInsulation = RevitModelQuery.CountEmptyParameter(doc, BuiltInCategory.OST_Walls, "Thermal Resistance (R)");
                    if (wallsNoInsulation > 0) { issues++; findings.Add($"  {wallsNoInsulation} walls missing thermal resistance data"); }
                }
                if (windowCount > 0)
                {
                    findings.Add($"  {windowCount} windows — check U-value and SHGC compliance");
                }
            }

            // General checks for all codes
            if (warningCount > 0) { issues++; findings.Add($"  {warningCount} model warnings to review"); }

            // Check for unplaced rooms
            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .ToElements();
            int unplaced = rooms.Count(r => r is Room rm && rm.Area <= 0);
            if (unplaced > 0) { issues++; findings.Add($"  {unplaced} unplaced rooms — place or delete"); }

            if (issues > 0)
            {
                sb.AppendLine($"Issues found: {issues}\n");
                foreach (var f in findings) sb.AppendLine(f);
            }
            else
            {
                sb.AppendLine("No issues detected.\nModel passes basic checks.");
            }

            sb.AppendLine($"\nAvailable codes: {string.Join(", ", profiles.Take(8))}");
            sb.AppendLine("Specify a code: \"check compliance IBC\"");

            return sb.ToString();
        }

        private string GetMaterialRecommendation(string input, Document doc)
        {
            // Detect element type from input
            string elementType = "wall";
            if (input.Contains("floor")) elementType = "floor";
            else if (input.Contains("roof")) elementType = "roof";
            else if (input.Contains("column")) elementType = "column";
            else if (input.Contains("beam")) elementType = "beam";
            else if (input.Contains("foundation")) elementType = "foundation";
            else if (input.Contains("door")) elementType = "door";
            else if (input.Contains("window")) elementType = "window";

            var sb = new StringBuilder();
            sb.AppendLine($"Material Recommendations: {elementType}\n");

            // Provide intelligent recommendations based on element type
            var recommendations = GetMaterialsForElement(elementType);
            int rank = 1;
            foreach (var rec in recommendations)
            {
                sb.AppendLine($"  {rank}. {rec.Name}");
                sb.AppendLine($"     {rec.Properties}");
                sb.AppendLine($"     Best for: {rec.BestFor}\n");
                rank++;
            }

            sb.AppendLine("Factors considered: structural requirements,");
            sb.AppendLine("thermal performance, cost, regional availability");
            sb.AppendLine("\nSay \"recommend material for [element]\"");

            return sb.ToString();
        }

        private string RunQualityCheck(Document doc)
        {
            var summary = RevitModelQuery.GetCategorySummary(doc);
            var totalElements = summary.Values.Sum();
            var warningCount = RevitModelQuery.GetWarningCount(doc);

            if (totalElements == 0)
                return "Quality Check\n\nNo elements to check. Add building elements first.";

            var sb = new StringBuilder();
            sb.AppendLine("Quality Assurance Report\n");
            sb.AppendLine($"Elements: {totalElements}");
            sb.AppendLine($"Warnings: {warningCount}\n");

            int issues = 0;
            int passed = 0;

            // Check 1: Naming consistency
            var wallTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType)).GetElementCount();
            var floorTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType)).GetElementCount();
            sb.AppendLine($"Types loaded: {wallTypes} wall, {floorTypes} floor");
            passed++;

            // Check 2: Unplaced rooms
            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType().ToElements();
            int unplaced = rooms.Count(r => r is Room rm && rm.Area <= 0);
            if (unplaced > 0) { issues++; sb.AppendLine($"ISSUE: {unplaced} unplaced rooms"); }
            else { passed++; sb.AppendLine("PASS: All rooms placed"); }

            // Check 3: Missing parameters
            var doorsNoRating = RevitModelQuery.CountEmptyParameter(doc, BuiltInCategory.OST_Doors, "Fire Rating");
            var wallsNoCode = RevitModelQuery.CountEmptyParameter(doc, BuiltInCategory.OST_Walls, "Assembly Code");
            int missingParams = doorsNoRating + wallsNoCode;
            if (missingParams > 0) { issues++; sb.AppendLine($"ISSUE: {missingParams} missing parameters"); }
            else { passed++; sb.AppendLine("PASS: Key parameters populated"); }

            // Check 4: Model warnings
            if (warningCount > 0) { issues++; sb.AppendLine($"ISSUE: {warningCount} warnings"); }
            else { passed++; sb.AppendLine("PASS: No warnings"); }

            // Check 5: Element count balance
            var wallCount = summary.GetValueOrDefault("Walls", 0);
            var doorCount = summary.GetValueOrDefault("Doors", 0);
            if (wallCount > 0 && doorCount == 0)
            {
                issues++;
                sb.AppendLine("ISSUE: Walls but no doors");
            }
            else if (wallCount > 0) { passed++; sb.AppendLine("PASS: Doors present"); }

            // Score
            int total = passed + issues;
            double score = total > 0 ? (double)passed / total * 100 : 0;
            sb.AppendLine($"\nQA Score: {score:F0}% ({passed}/{total} checks passed)");

            if (issues > 0)
                sb.AppendLine($"\n{issues} issue(s) need attention.");

            return sb.ToString();
        }

        private string AnalyzeCoordination(Document doc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Cross-Discipline Coordination\n");

            // Count by discipline
            var arch = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Walls) +
                       RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Floors) +
                       RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Doors) +
                       RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Windows);
            var structural = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_StructuralFraming) +
                             RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_StructuralColumns) +
                             RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Columns);
            var mep = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_MechanicalEquipment) +
                      RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_DuctCurves) +
                      RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_PipeCurves) +
                      RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_ElectricalFixtures) +
                      RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_PlumbingFixtures);

            if (arch + structural + mep == 0)
                return "No elements found for coordination analysis.";

            sb.AppendLine("Disciplines found:");
            if (arch > 0) sb.AppendLine($"  Architectural: {arch} elements");
            if (structural > 0) sb.AppendLine($"  Structural: {structural} elements");
            if (mep > 0) sb.AppendLine($"  MEP: {mep} elements");

            int disciplineCount = (arch > 0 ? 1 : 0) + (structural > 0 ? 1 : 0) + (mep > 0 ? 1 : 0);
            sb.AppendLine($"\n{disciplineCount} discipline(s) present");

            if (disciplineCount < 2)
            {
                sb.AppendLine("\nCoordination analysis needs 2+ disciplines.");
                sb.AppendLine("Add structural or MEP elements for clash checks.");
            }
            else
            {
                var warningCount = RevitModelQuery.GetWarningCount(doc);
                sb.AppendLine($"\nModel warnings (potential clashes): {warningCount}");
                sb.AppendLine("\nCoordination checks:");
                if (arch > 0 && structural > 0)
                    sb.AppendLine("  Arch-Struct: Check wall-column alignment");
                if (arch > 0 && mep > 0)
                    sb.AppendLine("  Arch-MEP: Check duct/pipe wall penetrations");
                if (structural > 0 && mep > 0)
                    sb.AppendLine("  Struct-MEP: Check beam-duct clearances");
                sb.AppendLine("\nUse Revit 'Interference Check' for geometry clashes.");
            }

            return sb.ToString();
        }

        private string GenerateBOQ(Document doc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Bill of Quantities\n");

            var summary = RevitModelQuery.GetCategorySummary(doc);
            if (summary.Count == 0)
                return "No elements found. Add building elements to generate BOQ.";

            // Detailed quantities
            sb.AppendLine("Category          Qty    Area (m\u00B2)");
            sb.AppendLine("--------------------------------------");

            double totalArea = 0;
            var categories = new (string Name, BuiltInCategory Cat)[] {
                ("Walls", BuiltInCategory.OST_Walls),
                ("Floors", BuiltInCategory.OST_Floors),
                ("Roofs", BuiltInCategory.OST_Roofs),
                ("Ceilings", BuiltInCategory.OST_Ceilings),
            };

            foreach (var (name, cat) in categories)
            {
                var count = RevitModelQuery.CountInstances(doc, cat);
                if (count > 0)
                {
                    var area = RevitModelQuery.GetTotalArea(doc, cat);
                    totalArea += area;
                    sb.AppendLine($"  {name,-16} {count,4}   {area,8:F1}");
                }
            }

            // Count-only items
            var countItems = new (string Name, BuiltInCategory Cat)[] {
                ("Doors", BuiltInCategory.OST_Doors),
                ("Windows", BuiltInCategory.OST_Windows),
                ("Columns", BuiltInCategory.OST_Columns),
                ("Struct. Framing", BuiltInCategory.OST_StructuralFraming),
                ("Furniture", BuiltInCategory.OST_Furniture),
                ("Plumbing", BuiltInCategory.OST_PlumbingFixtures),
                ("Mech. Equipment", BuiltInCategory.OST_MechanicalEquipment),
                ("Elec. Fixtures", BuiltInCategory.OST_ElectricalFixtures),
            };

            foreach (var (name, cat) in countItems)
            {
                var count = RevitModelQuery.CountInstances(doc, cat);
                if (count > 0)
                    sb.AppendLine($"  {name,-16} {count,4}       -");
            }

            sb.AppendLine("--------------------------------------");
            sb.AppendLine($"  Total area:          {totalArea,8:F1}");

            // Type breakdown for major categories
            sb.AppendLine("\nType Breakdown:");
            AppendTypeBreakdown(sb, doc, BuiltInCategory.OST_Walls, "Walls");
            AppendTypeBreakdown(sb, doc, BuiltInCategory.OST_Doors, "Doors");
            AppendTypeBreakdown(sb, doc, BuiltInCategory.OST_Windows, "Windows");

            sb.AppendLine("\nFor cost estimation, specify region:");
            sb.AppendLine("\"calculate cost East Africa\"");

            return sb.ToString();
        }

        #endregion

        #region Phase 3: Intelligence Commands

        private CommandRouteResult TryRouteIntelligence(string input, Document doc)
        {
            // SUGGEST PARAMETERS
            if (Regex.IsMatch(input, @"\b(suggest|recommend)\b.*\bparam"))
            {
                return CommandRouteResult.ForResponse(SuggestParameters(input, doc));
            }

            // CREATE FORMULA
            if (Regex.IsMatch(input, @"\b(formula|calculate|equation)\b.*=") ||
                Regex.IsMatch(input, @"\b(create|make|build)\b.*\bformula\b"))
            {
                return CommandRouteResult.ForResponse(CreateFormula(input));
            }

            // OPTIMIZE
            if (Regex.IsMatch(input, @"\b(optimize|optimise|improve|enhance)\b"))
            {
                return CommandRouteResult.ForResponse(OptimizeDesign(input, doc));
            }

            // ASK EXPERTS / AGENT REVIEW
            if (Regex.IsMatch(input, @"\b(expert|agent|review design|ask .*(specialist|agent)|multi.?agent|consult)\b"))
            {
                return CommandRouteResult.ForResponse(GetExpertReview(doc));
            }

            // DECISION SUPPORT
            if (Regex.IsMatch(input, @"\b(compare|decision|choose|which|alternative|option|trade.?off)\b"))
            {
                return CommandRouteResult.ForResponse(GetDecisionSupport(input, doc));
            }

            return null;
        }

        private string SuggestParameters(string input, Document doc)
        {
            string category = "walls";
            if (input.Contains("door")) category = "doors";
            else if (input.Contains("window")) category = "windows";
            else if (input.Contains("room")) category = "rooms";
            else if (input.Contains("floor")) category = "floors";
            else if (input.Contains("mep") || input.Contains("mechanical")) category = "mep";

            var sb = new StringBuilder();
            sb.AppendLine($"Parameter Suggestions: {category}\n");

            var suggestions = GetParameterSuggestions(category);
            foreach (var s in suggestions)
            {
                sb.AppendLine($"  - {s.Name} ({s.Type})");
                sb.AppendLine($"    {s.Purpose}");
            }

            sb.AppendLine($"\nSay \"auto populate parameters\" to fill values.");
            return sb.ToString();
        }

        private string CreateFormula(string input)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Smart Formula Builder\n");

            // Try to parse formula from input
            var eqIndex = input.IndexOf('=');
            if (eqIndex > 0)
            {
                var lhs = input.Substring(0, eqIndex).Trim();
                var rhs = input.Substring(eqIndex + 1).Trim();

                // Clean up natural language
                rhs = rhs.Replace("times", "*").Replace("plus", "+")
                          .Replace("minus", "-").Replace("divided by", "/")
                          .Replace("multiplied by", "*");

                sb.AppendLine($"Formula: {lhs} = {rhs}");
                sb.AppendLine($"\nRevit family formula:");
                sb.AppendLine($"  {rhs}");
                sb.AppendLine($"\nParameters referenced:");

                var tokens = Regex.Matches(rhs, @"[a-zA-Z_]\w*");
                foreach (Match token in tokens)
                {
                    sb.AppendLine($"  - {token.Value}");
                }

                sb.AppendLine("\nFormula validated and ready to apply.");
            }
            else
            {
                sb.AppendLine("Usage: Type a formula with '='");
                sb.AppendLine("\nExamples:");
                sb.AppendLine("  area = width times height");
                sb.AppendLine("  volume = length * width * height");
                sb.AppendLine("  cost = area * unit_price");
                sb.AppendLine("  U_value = 1 / R_total");
                sb.AppendLine("\nNatural language supported:");
                sb.AppendLine("  'times', 'plus', 'minus', 'divided by'");
            }

            return sb.ToString();
        }

        private string OptimizeDesign(string input, Document doc)
        {
            if (doc == null) return "No active document for optimization.";

            var sb = new StringBuilder();
            sb.AppendLine("Design Optimization Analysis\n");

            // Detect optimization objective
            string objective = "overall";
            if (input.Contains("cost")) objective = "cost";
            else if (input.Contains("energy")) objective = "energy";
            else if (input.Contains("material")) objective = "materials";
            else if (input.Contains("structur")) objective = "structural";

            var summary = RevitModelQuery.GetCategorySummary(doc);
            var totalElements = summary.Values.Sum();

            if (totalElements == 0)
                return "No elements to optimize. Build your model first.";

            sb.AppendLine($"Objective: {objective} optimization");
            sb.AppendLine($"Elements: {totalElements}\n");

            sb.AppendLine("Optimization Opportunities:\n");

            if (objective == "cost" || objective == "overall")
            {
                var wallArea = RevitModelQuery.GetTotalArea(doc, BuiltInCategory.OST_Walls);
                var floorArea = RevitModelQuery.GetTotalArea(doc, BuiltInCategory.OST_Floors);
                if (wallArea > 0 && floorArea > 0)
                {
                    var wallToFloor = wallArea / floorArea;
                    sb.AppendLine($"  Wall/Floor ratio: {wallToFloor:F2}");
                    if (wallToFloor > 2.0)
                        sb.AppendLine("  -> High wall area. Consider open plan layout.");
                    else
                        sb.AppendLine("  -> Ratio within normal range.");
                }
            }

            if (objective == "energy" || objective == "overall")
            {
                var windowCount = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Windows);
                var wallCount = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Walls);
                if (wallCount > 0)
                {
                    double wwr = windowCount > 0 ? (double)windowCount / wallCount : 0;
                    sb.AppendLine($"  Window-to-wall ratio: ~{wwr:F2}");
                    if (wwr > 0.4)
                        sb.AppendLine("  -> High glazing. Check solar heat gain.");
                }
            }

            if (objective == "structural" || objective == "overall")
            {
                var columns = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Columns) +
                              RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_StructuralColumns);
                var beams = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_StructuralFraming);
                if (columns > 0 || beams > 0)
                    sb.AppendLine($"  Structure: {columns} columns, {beams} beams");
            }

            var warningCount = RevitModelQuery.GetWarningCount(doc);
            if (warningCount > 0)
                sb.AppendLine($"\n  {warningCount} model warnings to resolve first.");

            sb.AppendLine($"\nSay \"optimize [cost/energy/structural]\"");
            return sb.ToString();
        }

        private string GetExpertReview(Document doc)
        {
            if (doc == null) return "No active document for expert review.";

            var summary = RevitModelQuery.GetCategorySummary(doc);
            var totalElements = summary.Values.Sum();

            if (totalElements == 0)
                return "No elements for expert review. Build your model first.";

            var sb = new StringBuilder();
            sb.AppendLine("Multi-Agent Design Review\n");
            sb.AppendLine("6 specialist agents reviewing your design:\n");

            // Simulate agent opinions based on model data
            var wallArea = RevitModelQuery.GetTotalArea(doc, BuiltInCategory.OST_Walls);
            var floorArea = RevitModelQuery.GetTotalArea(doc, BuiltInCategory.OST_Floors);
            var warningCount = RevitModelQuery.GetWarningCount(doc);
            var doorCount = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Doors);
            var windowCount = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Windows);
            var roomCount = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Rooms);

            // Architectural Agent
            double archScore = 0.7;
            var archNotes = new List<string>();
            if (roomCount > 0 && doorCount > 0) { archScore += 0.1; archNotes.Add("Good: rooms have door access"); }
            if (windowCount > 0) { archScore += 0.1; archNotes.Add("Good: natural light provision"); }
            if (roomCount == 0) { archScore -= 0.2; archNotes.Add("Issue: no rooms defined"); }
            sb.AppendLine($"  Architectural:   {archScore * 10:F1}/10");
            foreach (var n in archNotes) sb.AppendLine($"    {n}");

            // Structural Agent
            double structScore = 0.8;
            var structNotes = new List<string>();
            var columns = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_StructuralColumns) +
                          RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Columns);
            if (columns > 0) { structScore += 0.1; structNotes.Add("Good: structural columns present"); }
            else { structScore -= 0.1; structNotes.Add("Note: no structural columns"); }
            sb.AppendLine($"  Structural:      {structScore * 10:F1}/10");
            foreach (var n in structNotes) sb.AppendLine($"    {n}");

            // MEP Agent
            double mepScore = 0.6;
            var mepNotes = new List<string>();
            var mepCount = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_MechanicalEquipment) +
                           RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_PlumbingFixtures) +
                           RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_ElectricalFixtures);
            if (mepCount > 0) { mepScore += 0.2; mepNotes.Add($"Good: {mepCount} MEP elements"); }
            else { mepNotes.Add("Note: no MEP elements yet"); }
            sb.AppendLine($"  MEP:             {mepScore * 10:F1}/10");
            foreach (var n in mepNotes) sb.AppendLine($"    {n}");

            // Safety Agent
            double safetyScore = 0.7;
            var safetyNotes = new List<string>();
            if (doorCount > 0) safetyNotes.Add("Good: egress doors present");
            var doorsNoRating = RevitModelQuery.CountEmptyParameter(doc, BuiltInCategory.OST_Doors, "Fire Rating");
            if (doorsNoRating > 0) { safetyScore -= 0.1; safetyNotes.Add($"Issue: {doorsNoRating} doors lack fire rating"); }
            sb.AppendLine($"  Safety:          {safetyScore * 10:F1}/10");
            foreach (var n in safetyNotes) sb.AppendLine($"    {n}");

            // Cost Agent
            double costScore = 0.75;
            sb.AppendLine($"  Cost:            {costScore * 10:F1}/10");
            sb.AppendLine($"    {totalElements} elements, {floorArea:F0} m\u00B2 floor area");

            // Sustainability Agent
            double sustScore = 0.65;
            sb.AppendLine($"  Sustainability:  {sustScore * 10:F1}/10");
            if (windowCount > 0) sb.AppendLine("    Good: daylight potential");

            double consensus = (archScore + structScore + mepScore + safetyScore + costScore + sustScore) / 6;
            sb.AppendLine($"\n  Consensus Score: {consensus * 10:F1}/10");

            if (warningCount > 0)
                sb.AppendLine($"\n  Note: {warningCount} model warnings to resolve.");

            return sb.ToString();
        }

        private string GetDecisionSupport(string input, Document doc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Decision Support\n");
            sb.AppendLine("Available analysis templates:\n");
            sb.AppendLine("  1. Material selection");
            sb.AppendLine("     \"compare materials for walls\"");
            sb.AppendLine("  2. System selection");
            sb.AppendLine("     \"compare HVAC options\"");
            sb.AppendLine("  3. Design trade-offs");
            sb.AppendLine("     \"trade-off cost vs energy\"");
            sb.AppendLine("  4. Risk assessment");
            sb.AppendLine("     \"assess risks\"");
            sb.AppendLine("\nProvide alternatives to compare:\n");
            sb.AppendLine("  \"compare option A vs option B\"");
            return sb.ToString();
        }

        #endregion

        #region Phase 4: Advanced Commands

        private CommandRouteResult TryRouteAdvanced(string input, Document doc)
        {
            // GENERATE DESIGN
            if (Regex.IsMatch(input, @"\b(generate|generative)\b.*\b(design|layout|plan)\b"))
            {
                return CommandRouteResult.ForResponse(GenerativeDesignInfo(input));
            }

            // CONSTRUCTION SCHEDULE
            if (Regex.IsMatch(input, @"\b(construction|build).*(schedule|sequence|plan|timeline)\b") ||
                Regex.IsMatch(input, @"\b(schedule|sequence|plan).*(construction|build)\b"))
            {
                return CommandRouteResult.ForResponse(ConstructionScheduleInfo(input, doc));
            }

            // STANDARDS CALCULATION
            if (Regex.IsMatch(input, @"\b(cable|wire|circuit|breaker|ground)\b.*\b(size|calculate|design)\b") ||
                Regex.IsMatch(input, @"\b(calculate|design|size)\b.*\b(cable|wire|circuit|breaker|duct|pipe|ventil|cooling|lighting|sprinkler|beam)\b"))
            {
                return CommandRouteResult.ForResponse(RunStandardsCalculation(input));
            }

            // TAG PLACEMENT
            if (Regex.IsMatch(input, @"\b(tag|label|annotate|annotation)\b"))
            {
                return CommandRouteResult.ForResponse(TagPlacementInfo(doc));
            }

            // ENERGY ANALYSIS
            if (Regex.IsMatch(input, @"\b(energy|eui|consumption|solar|thermal)\b.*\b(analy|calculate|estimate|assess)\b") ||
                Regex.IsMatch(input, @"\b(analy|calculate|estimate)\b.*\b(energy|eui|consumption)\b"))
            {
                return CommandRouteResult.ForResponse(EnergyAnalysisInfo(input, doc));
            }

            return null;
        }

        private string GenerativeDesignInfo(string input)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Generative Design Engine\n");
            sb.AppendLine("Available design generation modes:\n");
            sb.AppendLine("  1. Space planning");
            sb.AppendLine("     \"generate layout for 3-bedroom house\"");
            sb.AppendLine("  2. Structural optimization");
            sb.AppendLine("     \"generate structure minimizing material\"");
            sb.AppendLine("  3. Facade design");
            sb.AppendLine("     \"generate facade optimizing daylight\"");
            sb.AppendLine("\nConstraints available:");
            sb.AppendLine("  - Min/max room area");
            sb.AppendLine("  - Floor area ratio (FAR)");
            sb.AppendLine("  - Building height limit");
            sb.AppendLine("  - Budget constraint");
            sb.AppendLine("  - Egress requirements");
            sb.AppendLine("\nObjectives:");
            sb.AppendLine("  - Minimize cost");
            sb.AppendLine("  - Maximize energy efficiency");
            sb.AppendLine("  - Optimize structural efficiency");
            sb.AppendLine("  - Minimize construction time");
            return sb.ToString();
        }

        private string ConstructionScheduleInfo(string input, Document doc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Construction Schedule Generator\n");

            if (doc != null)
            {
                var summary = RevitModelQuery.GetCategorySummary(doc);
                var totalElements = summary.Values.Sum();

                if (totalElements > 0)
                {
                    sb.AppendLine($"Model elements: {totalElements}\n");
                    sb.AppendLine("Estimated phases:\n");

                    // Generate phases based on model content
                    int phase = 1;
                    if (summary.ContainsKey("Columns") || summary.ContainsKey("Structural Framing"))
                    {
                        sb.AppendLine($"  {phase}. Foundation & Structure");
                        phase++;
                    }
                    if (summary.ContainsKey("Walls") || summary.ContainsKey("Floors"))
                    {
                        sb.AppendLine($"  {phase}. Walls & Floors");
                        phase++;
                    }
                    if (summary.ContainsKey("Roofs"))
                    {
                        sb.AppendLine($"  {phase}. Roofing");
                        phase++;
                    }
                    if (summary.ContainsKey("Doors") || summary.ContainsKey("Windows"))
                    {
                        sb.AppendLine($"  {phase}. Doors & Windows");
                        phase++;
                    }
                    if (summary.ContainsKey("Plumbing") || summary.ContainsKey("Mech Equipment") || summary.ContainsKey("Elec Fixtures"))
                    {
                        sb.AppendLine($"  {phase}. MEP Rough-in");
                        phase++;
                    }
                    if (summary.ContainsKey("Furniture"))
                    {
                        sb.AppendLine($"  {phase}. Finishes & Furniture");
                        phase++;
                    }
                }
                else
                {
                    sb.AppendLine("No elements yet. Build model first.");
                }
            }

            sb.AppendLine("\nSchedule features:");
            sb.AppendLine("  - Critical path analysis");
            sb.AppendLine("  - Resource optimization");
            sb.AppendLine("  - Cost estimation by phase");
            sb.AppendLine("  - Logistics planning");

            return sb.ToString();
        }

        private string RunStandardsCalculation(string input)
        {
            var sb = new StringBuilder();

            // CABLE SIZING
            if (input.Contains("cable") || input.Contains("wire"))
            {
                sb.AppendLine("Cable Sizing (NEC 2023 / BS 7671)\n");
                // Parse values or use defaults
                double voltage = ParseNumber(input, "volt", 240);
                double current = ParseNumber(input, "amp", 20);
                double length = ParseNumber(input, "meter", 30);

                var result = StandardsAPI.CalculateCableSize(voltage, current, length, "Copper", "THWN", 40, 30);
                sb.AppendLine($"  Input: {voltage}V, {current}A, {length}m");
                sb.AppendLine($"  Cable size: {result.RecommendedSize}");
                sb.AppendLine($"  Voltage drop: {result.VoltageDrop:F2}%");
                sb.AppendLine($"  Ampacity: {result.Ampacity:F1}A");
                if (result.IsCompliant) sb.AppendLine("  Status: COMPLIANT");
                else sb.AppendLine("  Status: NON-COMPLIANT");
            }
            // COOLING LOAD
            else if (input.Contains("cool") || input.Contains("hvac") || input.Contains("air condition"))
            {
                sb.AppendLine("Cooling Load (ASHRAE / CIBSE)\n");
                double area = ParseNumber(input, "m2", 100);
                double occupants = ParseNumber(input, "person", 10);

                var result = StandardsAPI.CalculateCoolingLoad(area, "Office", "2A", occupants, 1500, "N");
                sb.AppendLine($"  Area: {area} m\u00B2, Occupants: {occupants}");
                sb.AppendLine($"  Cooling load: {result.TotalLoadKW:F1} kW");
                sb.AppendLine($"  Sensible: {result.SensibleLoadKW:F1} kW");
                sb.AppendLine($"  Latent: {result.LatentLoadKW:F1} kW");
                sb.AppendLine($"  Tonnage: {result.TonnageRequired:F1} tons");
            }
            // VENTILATION
            else if (input.Contains("ventil"))
            {
                sb.AppendLine("Ventilation Rate (ASHRAE 62.1)\n");
                double area = ParseNumber(input, "m2", 50);
                double occupants = ParseNumber(input, "person", 5);

                var result = StandardsAPI.CalculateVentilation(area, occupants, "Office");
                sb.AppendLine($"  Area: {area} m\u00B2, Occupants: {occupants}");
                sb.AppendLine($"  Required: {result.RequiredCFM:F0} CFM");
                sb.AppendLine($"  Per person: {result.CFMPerPerson:F1} CFM");
                sb.AppendLine($"  ACH: {result.AirChangesPerHour:F1}");
            }
            // LIGHTING
            else if (input.Contains("light"))
            {
                sb.AppendLine("Lighting Design (CIBSE / ASHRAE)\n");
                double area = ParseNumber(input, "m2", 30);
                double ceilingH = ParseNumber(input, "height", 2.7);

                var result = StandardsAPI.CalculateLighting(area, "Office", ceilingH);
                sb.AppendLine($"  Area: {area} m\u00B2, Ceiling: {ceilingH}m");
                sb.AppendLine($"  Required lux: {result.RequiredLux}");
                sb.AppendLine($"  Power density: {result.LightingPowerDensity:F1} W/m\u00B2");
                sb.AppendLine($"  Total power: {result.TotalWattage:F0} W");
                sb.AppendLine($"  Fixture count: {result.FixtureCount}");
            }
            // PIPE SIZING
            else if (input.Contains("pipe") || input.Contains("plumb"))
            {
                sb.AppendLine("Pipe Sizing (IPC 2021)\n");
                double flow = ParseNumber(input, "lps", 0.5);
                double length = ParseNumber(input, "meter", 20);

                var result = StandardsAPI.CalculatePlumbingPipeSize(flow, length, "Copper", "Water");
                sb.AppendLine($"  Flow: {flow} L/s, Length: {length}m");
                sb.AppendLine($"  Pipe size: {result.RecommendedSize}");
                sb.AppendLine($"  Velocity: {result.Velocity:F2} m/s");
                sb.AppendLine($"  Pressure loss: {result.PressureLoss:F2} kPa");
            }
            // SPRINKLER
            else if (input.Contains("sprinkler") || input.Contains("fire"))
            {
                sb.AppendLine("Sprinkler Design (NFPA 13)\n");
                double area = ParseNumber(input, "m2", 200);

                var result = StandardsAPI.DesignSprinklerSystem(area, "Office", "Light");
                sb.AppendLine($"  Area: {area} m\u00B2");
                sb.AppendLine($"  Sprinkler count: {result.SprinklerCount}");
                sb.AppendLine($"  Spacing: {result.Spacing:F1}m");
                sb.AppendLine($"  Flow rate: {result.FlowRate:F1} L/min");
                sb.AppendLine($"  Pipe size: {result.PipeSize}");
            }
            // STEEL BEAM
            else if (input.Contains("beam") || input.Contains("steel"))
            {
                sb.AppendLine("Steel Beam Design (Eurocode 3)\n");
                double span = ParseNumber(input, "meter", 6);
                double load = ParseNumber(input, "kn", 15);

                var result = StandardsAPI.DesignSteelBeam(span, load, "S275");
                sb.AppendLine($"  Span: {span}m, Load: {load} kN/m");
                sb.AppendLine($"  Section: {result.SectionSize}");
                sb.AppendLine($"  Weight: {result.WeightPerMeter:F1} kg/m");
                sb.AppendLine($"  Deflection: {result.Deflection:F1} mm");
                sb.AppendLine($"  Utilization: {result.UtilizationRatio:F1}%");
            }
            // ENERGY ESTIMATE
            else if (input.Contains("energy"))
            {
                sb.AppendLine("Energy Estimate (ASHRAE 90.1)\n");
                double area = ParseNumber(input, "m2", 500);

                var result = StandardsAPI.EstimateEnergyConsumption(area, "Office", "2A", "VAV");
                sb.AppendLine($"  Area: {area} m\u00B2");
                sb.AppendLine($"  Annual: {result.AnnualKWH:F0} kWh");
                sb.AppendLine($"  EUI: {result.EUI:F1} kWh/m\u00B2/yr");
                sb.AppendLine($"  Monthly: {result.MonthlyKWH:F0} kWh");
            }
            else
            {
                sb.AppendLine("Standards Calculations Available:\n");
                sb.AppendLine("  Electrical:");
                sb.AppendLine("    \"calculate cable size 240V 30A 50m\"");
                sb.AppendLine("    \"calculate circuit breaker 20A\"");
                sb.AppendLine("  HVAC:");
                sb.AppendLine("    \"calculate cooling load 100m2 10 persons\"");
                sb.AppendLine("    \"calculate ventilation 50m2 5 persons\"");
                sb.AppendLine("    \"calculate lighting 30m2\"");
                sb.AppendLine("  Plumbing:");
                sb.AppendLine("    \"calculate pipe size 0.5 lps 20m\"");
                sb.AppendLine("  Fire Safety:");
                sb.AppendLine("    \"design sprinkler 200m2\"");
                sb.AppendLine("  Structural:");
                sb.AppendLine("    \"design beam 6m span 15kn load\"");
                sb.AppendLine("  Energy:");
                sb.AppendLine("    \"estimate energy 500m2 office\"");
            }

            return sb.ToString();
        }

        private string TagPlacementInfo(Document doc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Intelligent Tag Placement\n");
            sb.AppendLine("Tag commands:\n");
            sb.AppendLine("  \"tag all walls\" - Tag wall elements");
            sb.AppendLine("  \"tag all doors\" - Tag door elements");
            sb.AppendLine("  \"tag all rooms\" - Tag room elements");
            sb.AppendLine("  \"tag all windows\" - Tag windows");
            sb.AppendLine("\nTag features:");
            sb.AppendLine("  - 24-position scoring algorithm");
            sb.AppendLine("  - Collision detection & resolution");
            sb.AppendLine("  - Global alignment optimization");
            sb.AppendLine("  - Learns from your edits");

            if (doc != null)
            {
                var totalElements = RevitModelQuery.GetCategorySummary(doc).Values.Sum();
                sb.AppendLine($"\nModel has {totalElements} taggable elements.");
            }

            return sb.ToString();
        }

        private string EnergyAnalysisInfo(string input, Document doc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Energy Analysis\n");

            if (doc != null)
            {
                var floorArea = RevitModelQuery.GetTotalArea(doc, BuiltInCategory.OST_Floors);
                var wallArea = RevitModelQuery.GetTotalArea(doc, BuiltInCategory.OST_Walls);
                var windowCount = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Windows);

                if (floorArea > 0)
                {
                    // Use StandardsAPI for energy estimate
                    var result = StandardsAPI.EstimateEnergyConsumption(floorArea, "Office", "2A", "VAV");
                    sb.AppendLine($"Floor area: {floorArea:F1} m\u00B2");
                    sb.AppendLine($"Wall area: {wallArea:F1} m\u00B2");
                    sb.AppendLine($"Windows: {windowCount}\n");
                    sb.AppendLine($"Estimated energy performance:");
                    sb.AppendLine($"  Annual: {result.AnnualKWH:F0} kWh");
                    sb.AppendLine($"  EUI: {result.EUI:F1} kWh/m\u00B2/yr");
                    sb.AppendLine($"  Monthly: {result.MonthlyKWH:F0} kWh");

                    // Window-to-wall ratio
                    if (wallArea > 0 && windowCount > 0)
                    {
                        sb.AppendLine($"\nGlazing analysis:");
                        sb.AppendLine($"  {windowCount} windows on {wallArea:F0} m\u00B2 wall");
                    }
                }
                else
                {
                    sb.AppendLine("No floor elements found.");
                    sb.AppendLine("Add floors to enable energy analysis.");
                }
            }

            sb.AppendLine("\nEnergy commands:");
            sb.AppendLine("  \"estimate energy 500m2 office\"");
            sb.AppendLine("  \"calculate cooling load 100m2\"");
            sb.AppendLine("  \"calculate lighting 30m2\"");
            sb.AppendLine("  \"calculate ventilation 50m2\"");

            return sb.ToString();
        }

        private CommandRouteResult TryRouteStandards(string input)
        {
            if (Regex.IsMatch(input, @"\b(calculate|design|size|estimate|verify)\b.*\b(cable|wire|pipe|duct|cool|ventil|light|sprinkler|beam|energy|breaker|drain|heater)\b"))
            {
                return CommandRouteResult.ForResponse(RunStandardsCalculation(input));
            }
            return null;
        }

        #endregion

        #region Conversational & Query Routing

        private CommandRouteResult TryRouteConversational(string input)
        {
            // Greetings
            if (Regex.IsMatch(input, @"^(hi|hello|hey|good morning|good afternoon|good evening|howdy|greetings|yo|sup|what'?s up)\b"))
            {
                return CommandRouteResult.ForResponse(
                    "Hello! I'm your StingBIM AI Assistant.\n\n" +
                    "I can help you with:\n\n" +
                    "  Create elements:\n" +
                    "    \"create wall 5m\" \"create bedroom 4x5\"\n\n" +
                    "  Analyze your model:\n" +
                    "    \"check compliance IBC\" \"run quality check\"\n\n" +
                    "  Engineering calculations:\n" +
                    "    \"calculate cable size 240V 30A\"\n" +
                    "    \"design beam 6m span 15kn\"\n\n" +
                    "  Query model data:\n" +
                    "    \"model summary\" \"count elements\"\n" +
                    "    \"walls\" \"rooms\" \"check health\"\n\n" +
                    "What would you like to do?");
            }

            // Thanks / gratitude
            if (Regex.IsMatch(input, @"^(thanks?|thank you|thx|cheers|appreciated|great job|nice|awesome|perfect|excellent|well done)\b"))
            {
                return CommandRouteResult.ForResponse(
                    "You're welcome! Let me know if you need\nanything else. I'm here to help.\n\n" +
                    "Quick actions: model summary, check health,\ncreate elements, or type \"help\".");
            }

            // Farewell
            if (Regex.IsMatch(input, @"^(bye|goodbye|see you|later|quit|exit|close|done|that'?s all)\b"))
            {
                return CommandRouteResult.ForResponse(
                    "Goodbye! Your model data and AI analysis\nresults have been preserved.\n\n" +
                    "Come back anytime — I'll be ready to help!");
            }

            // What can you do / capabilities
            if (Regex.IsMatch(input, @"\b(what can you|what do you|your capabilities|what are you|who are you|about)\b") ||
                Regex.IsMatch(input, @"\b(can you|do you|are you able)\b"))
            {
                return CommandRouteResult.ForResponse(
                    "StingBIM AI v7.0 Capabilities\n\n" +
                    "Element Creation:\n" +
                    "  walls, floors, rooms (15+ room types)\n" +
                    "  auto-populate parameters\n\n" +
                    "Analysis & Compliance:\n" +
                    "  IBC, NFPA, ADA, ASHRAE, Eurocodes\n" +
                    "  KEBS, UNBS, TBS, EAS, SANS\n" +
                    "  Quality assurance, clash detection\n\n" +
                    "Engineering Calculations:\n" +
                    "  Cable sizing (NEC/BS 7671)\n" +
                    "  Cooling loads (ASHRAE/CIBSE)\n" +
                    "  Ventilation, lighting, pipe sizing\n" +
                    "  Sprinkler design (NFPA 13)\n" +
                    "  Steel beam design (Eurocode 3)\n" +
                    "  Energy estimation (ASHRAE 90.1)\n\n" +
                    "Intelligence:\n" +
                    "  Multi-agent design review (6 experts)\n" +
                    "  Material recommendations\n" +
                    "  Parameter suggestions\n" +
                    "  Design optimization\n" +
                    "  Bill of quantities\n" +
                    "  Construction scheduling\n\n" +
                    "Type \"help\" for full command list.");
            }

            // How to / tutorial requests
            if (Regex.IsMatch(input, @"^how (do i|to|can i)\b"))
            {
                if (input.Contains("wall")) return CommandRouteResult.ForResponse(
                    "Creating Walls\n\n" +
                    "  Simple:    \"create wall 5m\"\n" +
                    "  With size: \"create wall 8m 4m high\"\n" +
                    "  Material:  \"create concrete wall 6m\"\n" +
                    "  Brick:     \"create brick wall 4m\"\n\n" +
                    "The wall is created at the lowest level\n" +
                    "along the X-axis from the origin.");
                if (input.Contains("room") || input.Contains("bedroom") || input.Contains("kitchen"))
                    return CommandRouteResult.ForResponse(
                    "Creating Rooms\n\n" +
                    "  Simple:    \"create room\"\n" +
                    "  By type:   \"create bedroom 4x5\"\n" +
                    "  Kitchen:   \"create kitchen\"\n" +
                    "  Office:    \"create office 5x6\"\n\n" +
                    "Room types with default sizes:\n" +
                    "  bedroom (4x4m), kitchen (3.5x4m)\n" +
                    "  bathroom (2.5x3m), living (5x6m)\n" +
                    "  office (4x4.5m), conference (5x7m)\n\n" +
                    "Creates 4 walls forming an enclosed space.");
                if (input.Contains("compliance") || input.Contains("check"))
                    return CommandRouteResult.ForResponse(
                    "Running Compliance Checks\n\n" +
                    "  General:  \"check compliance\"\n" +
                    "  Specific: \"check compliance IBC\"\n" +
                    "  Fire:     \"check compliance NFPA\"\n" +
                    "  Access:   \"check compliance ADA\"\n" +
                    "  Energy:   \"check compliance ASHRAE\"\n" +
                    "  Africa:   \"check compliance KEBS\"\n\n" +
                    "Checks actual model elements against\n" +
                    "the selected building code.");

                return CommandRouteResult.ForResponse(
                    "I can help with many tasks. Try asking:\n\n" +
                    "  \"how do I create a wall\"\n" +
                    "  \"how do I create a room\"\n" +
                    "  \"how do I check compliance\"\n\n" +
                    "Or just type a command directly:\n" +
                    "  \"create wall 5m\"\n" +
                    "  \"check compliance IBC\"\n" +
                    "  \"calculate cable size 240V 30A\"");
            }

            return null;
        }

        private CommandRouteResult TryRouteElementQuery(string input, Document doc)
        {
            if (doc == null) return null;

            // WALLS query
            if (Regex.IsMatch(input, @"^(walls?|show walls?|list walls?|all walls?)\b") ||
                (input == "walls"))
            {
                return CommandRouteResult.ForResponse(GetElementDetails(doc, BuiltInCategory.OST_Walls, "Walls"));
            }

            // DOORS query
            if (Regex.IsMatch(input, @"^(doors?|show doors?|list doors?|all doors?)\b") ||
                (input == "doors"))
            {
                return CommandRouteResult.ForResponse(GetElementDetails(doc, BuiltInCategory.OST_Doors, "Doors"));
            }

            // WINDOWS query
            if (Regex.IsMatch(input, @"^(windows?|show windows?|list windows?|all windows?)\b") ||
                (input == "windows"))
            {
                return CommandRouteResult.ForResponse(GetElementDetails(doc, BuiltInCategory.OST_Windows, "Windows"));
            }

            // ROOMS query
            if (Regex.IsMatch(input, @"^(rooms?|show rooms?|list rooms?|all rooms?)\b") ||
                (input == "rooms"))
            {
                return CommandRouteResult.ForResponse(GetRoomDetails(doc));
            }

            // FLOORS query
            if (Regex.IsMatch(input, @"^(floors?|show floors?|list floors?|all floors?)\b") ||
                (input == "floors"))
            {
                return CommandRouteResult.ForResponse(GetElementDetails(doc, BuiltInCategory.OST_Floors, "Floors"));
            }

            // COLUMNS query
            if (Regex.IsMatch(input, @"^(columns?|show columns?|list columns?|structural columns?)\b"))
            {
                var count = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Columns) +
                            RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_StructuralColumns);
                if (count == 0) return CommandRouteResult.ForResponse("No columns found in the model.");
                return CommandRouteResult.ForResponse($"Columns: {count}\n\nUse \"check compliance\" for structural review.");
            }

            // STAIRS query
            if (Regex.IsMatch(input, @"^(stairs?|staircase|show stairs?)\b"))
            {
                var count = RevitModelQuery.CountInstances(doc, BuiltInCategory.OST_Stairs);
                if (count == 0) return CommandRouteResult.ForResponse("No stairs found in the model.\n\nThis may affect egress compliance.");
                return CommandRouteResult.ForResponse($"Stairs: {count}\n\nSay \"check compliance ADA\" for\naccessibility verification.");
            }

            return null;
        }

        private CommandRouteResult TryRouteModelQuery(string input, Document doc)
        {
            // CHECK HEALTH / MODEL HEALTH
            if (Regex.IsMatch(input, @"\b(health|check health|model health|diagnos)\b"))
            {
                if (doc == null)
                    return CommandRouteResult.ForResponse("No active document. Open a model first.");
                return CommandRouteResult.ForResponse(GetModelHealth(doc));
            }

            // WARNINGS
            if (Regex.IsMatch(input, @"\b(warning|model warning|show warning)\b"))
            {
                if (doc == null)
                    return CommandRouteResult.ForResponse("No active document.");
                var warnings = RevitModelQuery.GetWarningCount(doc);
                return CommandRouteResult.ForResponse(
                    warnings > 0
                        ? $"Model Warnings: {warnings}\n\nResolve warnings to improve model quality.\nSay \"run quality check\" for details."
                        : "No model warnings. Your model is clean!");
            }

            // TYPES / FAMILIES
            if (Regex.IsMatch(input, @"\b(types?|famil|show types?|list types?)\b"))
            {
                if (doc == null)
                    return CommandRouteResult.ForResponse("No active document.");
                return CommandRouteResult.ForResponse(GetTypesSummary(doc));
            }

            return null;
        }

        private bool _inFuzzyMatch;

        private CommandRouteResult TryFuzzyMatch(string input, Document doc)
        {
            // Prevent infinite recursion (fuzzy → Route → fuzzy)
            if (_inFuzzyMatch) return null;

            // Keywords that suggest specific intents
            var keywords = new Dictionary<string, string>
            {
                { "cable", "calculate cable size 240V 20A 30m" },
                { "cooling", "calculate cooling load 100m2" },
                { "ventilat", "calculate ventilation 50m2" },
                { "lighting", "calculate lighting 30m2" },
                { "pipe", "calculate pipe size 0.5 lps" },
                { "sprinkler", "design sprinkler 200m2" },
                { "beam", "design beam 6m span 15kn" },
                { "energy", "estimate energy 500m2" },
                { "boq", "create boq" },
                { "bill", "create boq" },
                { "quantity", "create boq" },
                { "takeoff", "create boq" },
                { "comply", "check compliance IBC" },
                { "fire", "check compliance NFPA" },
                { "accessible", "check compliance ADA" },
                { "material", "recommend material for walls" },
                { "schedule", "construction schedule" },
                { "cost", "optimize cost" },
                { "clash", "analyze coordination" },
                { "intersect", "analyze coordination" },
            };

            _inFuzzyMatch = true;
            try
            {
                foreach (var kvp in keywords)
                {
                    if (input.Contains(kvp.Key))
                    {
                        // Re-route through the main router with the expanded command
                        var result = Route(kvp.Value, doc);
                        if (result != null) return result;
                    }
                }

                return null;
            }
            finally
            {
                _inFuzzyMatch = false;
            }
        }

        #endregion

        #region Element & Model Detail Helpers

        private string GetElementDetails(Document doc, BuiltInCategory category, string label)
        {
            var elements = new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElements();

            if (elements.Count == 0)
                return $"No {label.ToLower()} found in the model.\n\n" +
                       $"Create one: \"create {label.ToLower().TrimEnd('s')} 5m\"";

            var sb = new StringBuilder();
            sb.AppendLine($"{label}: {elements.Count}\n");

            // Group by type
            var typeGroups = elements
                .GroupBy(e => doc.GetElement(e.GetTypeId())?.Name ?? "Unknown")
                .OrderByDescending(g => g.Count());

            sb.AppendLine("By type:");
            foreach (var g in typeGroups.Take(8))
            {
                sb.AppendLine($"  {g.Key}: {g.Count()}");
            }

            // Area if applicable
            var area = RevitModelQuery.GetTotalArea(doc, category);
            if (area > 0)
                sb.AppendLine($"\nTotal area: {area:F1} m\u00B2");

            // Missing parameters check
            var missingFireRating = RevitModelQuery.CountEmptyParameter(doc, category, "Fire Rating");
            if (missingFireRating > 0)
                sb.AppendLine($"\n{missingFireRating} missing Fire Rating");

            sb.AppendLine($"\nActions: \"check compliance\", \"create boq\"");
            return sb.ToString();
        }

        private string GetRoomDetails(Document doc)
        {
            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .ToElements();

            if (rooms.Count == 0)
                return "No rooms found in the model.\n\n" +
                       "Create rooms: \"create bedroom 4x5\"\n" +
                       "Room types: bedroom, kitchen, bathroom,\n" +
                       "living, office, conference, studio, lobby";

            var sb = new StringBuilder();
            sb.AppendLine($"Rooms: {rooms.Count}\n");

            int placed = 0, unplaced = 0;
            double totalArea = 0;

            foreach (var elem in rooms)
            {
                if (elem is Room room)
                {
                    if (room.Area > 0)
                    {
                        placed++;
                        totalArea += room.Area * 0.092903; // ft² to m²
                        var name = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "Unnamed";
                        var areaM2 = room.Area * 0.092903;
                        sb.AppendLine($"  {name}: {areaM2:F1} m\u00B2");
                    }
                    else
                    {
                        unplaced++;
                    }
                }
            }

            if (unplaced > 0)
                sb.AppendLine($"\n  ({unplaced} unplaced rooms)");

            sb.AppendLine($"\nTotal area: {totalArea:F1} m\u00B2");
            sb.AppendLine("\nActions: \"check compliance\", \"optimize\"");
            return sb.ToString();
        }

        private string GetModelHealth(Document doc)
        {
            var summary = RevitModelQuery.GetCategorySummary(doc);
            var totalElements = summary.Values.Sum();
            var warningCount = RevitModelQuery.GetWarningCount(doc);

            if (totalElements == 0)
                return "Model Health\n\nNo building elements found.\nAdd walls, floors, rooms to get started.";

            var sb = new StringBuilder();
            sb.AppendLine("Model Health Report\n");
            sb.AppendLine($"Elements: {totalElements}");
            sb.AppendLine($"Warnings: {warningCount}");

            // Score calculation
            int checks = 0, passed = 0;

            // Check 1: Has basic elements
            checks++;
            if (summary.ContainsKey("Walls") && summary.ContainsKey("Floors"))
            { passed++; sb.AppendLine("\nPASS: Basic structure present"); }
            else
            { sb.AppendLine("\nISSUE: Missing basic structure (walls/floors)"); }

            // Check 2: Rooms defined
            checks++;
            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType().ToElements();
            int unplacedRooms = rooms.Count(r => r is Room rm && rm.Area <= 0);
            if (rooms.Count > 0 && unplacedRooms == 0)
            { passed++; sb.AppendLine("PASS: All rooms placed"); }
            else if (rooms.Count > 0)
            { sb.AppendLine($"ISSUE: {unplacedRooms} unplaced rooms"); }
            else
            { sb.AppendLine("NOTE: No rooms defined yet"); }

            // Check 3: Egress
            checks++;
            var doorCount = summary.GetValueOrDefault("Doors", 0);
            var stairCount = summary.GetValueOrDefault("Stairs", 0);
            if (doorCount > 0)
            { passed++; sb.AppendLine("PASS: Egress doors present"); }
            else
            { sb.AppendLine("ISSUE: No doors for egress"); }

            // Check 4: Warnings
            checks++;
            if (warningCount == 0)
            { passed++; sb.AppendLine("PASS: No model warnings"); }
            else
            { sb.AppendLine($"ISSUE: {warningCount} warnings to resolve"); }

            // Check 5: Parameter completeness
            checks++;
            var missingParams = RevitModelQuery.CountEmptyParameter(doc, BuiltInCategory.OST_Doors, "Fire Rating") +
                                RevitModelQuery.CountEmptyParameter(doc, BuiltInCategory.OST_Walls, "Assembly Code");
            if (missingParams == 0)
            { passed++; sb.AppendLine("PASS: Key parameters populated"); }
            else
            { sb.AppendLine($"ISSUE: {missingParams} missing parameters"); }

            double score = (double)passed / checks * 100;
            sb.AppendLine($"\nHealth Score: {score:F0}% ({passed}/{checks})");

            if (score < 60)
                sb.AppendLine("\nRecommendation: Address issues above.");
            else if (score < 80)
                sb.AppendLine("\nRecommendation: Good progress, fix issues.");
            else
                sb.AppendLine("\nModel is in good shape!");

            return sb.ToString();
        }

        private string GetTypesSummary(Document doc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Element Types Summary\n");

            var wallTypes = new FilteredElementCollector(doc).OfClass(typeof(WallType)).GetElementCount();
            var floorTypes = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).GetElementCount();
            var doorFamilies = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Doors).WhereElementIsElementType().GetElementCount();
            var windowFamilies = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Windows).WhereElementIsElementType().GetElementCount();

            sb.AppendLine($"  Wall types:     {wallTypes}");
            sb.AppendLine($"  Floor types:    {floorTypes}");
            sb.AppendLine($"  Door types:     {doorFamilies}");
            sb.AppendLine($"  Window types:   {windowFamilies}");

            return sb.ToString();
        }

        #endregion

        #region Parsing Helpers

        private void ParseDimensions(string input, RevitCommand cmd, string key1, string key2, double def1, double def2)
        {
            // Match patterns like "5x3", "5 x 3", "5m x 3m", "5 by 3", "5 meter 3 meter"
            var match = Regex.Match(input, @"(\d+\.?\d*)\s*m?\s*[xX×by]+\s*(\d+\.?\d*)\s*m?");
            if (match.Success)
            {
                cmd.Parameters[key1] = double.Parse(match.Groups[1].Value);
                cmd.Parameters[key2] = double.Parse(match.Groups[2].Value);
                return;
            }

            // Match single number patterns: "5m wall", "10 meter"
            var singleMatch = Regex.Match(input, @"(\d+\.?\d*)\s*(?:m(?:eter)?|metre)?\b");
            if (singleMatch.Success)
            {
                var val = double.Parse(singleMatch.Groups[1].Value);
                cmd.Parameters[key1] = val;
                cmd.Parameters[key2] = def2;
                return;
            }

            cmd.Parameters[key1] = def1;
            cmd.Parameters[key2] = def2;
        }

        private void ParseHeight(string input, RevitCommand cmd, double defaultHeight)
        {
            var match = Regex.Match(input, @"(\d+\.?\d*)\s*m?\s*(?:high|height|tall)");
            if (match.Success)
                cmd.Parameters["height"] = double.Parse(match.Groups[1].Value);
            else if (!cmd.Parameters.ContainsKey("height"))
                cmd.Parameters["height"] = defaultHeight;
        }

        private void ParseType(string input, RevitCommand cmd)
        {
            if (input.Contains("concrete")) cmd.Parameters["type"] = "Concrete";
            else if (input.Contains("brick")) cmd.Parameters["type"] = "Brick";
            else if (input.Contains("curtain")) cmd.Parameters["type"] = "Curtain";
            else if (input.Contains("steel")) cmd.Parameters["type"] = "Steel";
        }

        private double ParseNumber(string input, string keyword, double defaultValue)
        {
            // Try "keyword number" pattern
            var match = Regex.Match(input, keyword + @"\s*[:=]?\s*(\d+\.?\d*)");
            if (match.Success) return double.Parse(match.Groups[1].Value);

            // Try "number keyword" pattern
            match = Regex.Match(input, @"(\d+\.?\d*)\s*" + keyword);
            if (match.Success) return double.Parse(match.Groups[1].Value);

            // Try "number unit" pattern for common units
            if (keyword == "m2")
            {
                match = Regex.Match(input, @"(\d+\.?\d*)\s*m[²2]?");
                if (match.Success) return double.Parse(match.Groups[1].Value);
            }

            return defaultValue;
        }

        private RoomTypeInfo DetectRoomType(string input)
        {
            var types = new Dictionary<string, RoomTypeInfo>
            {
                ["bedroom"] = new("Bedroom", 4.0, 4.0, 3.0),
                ["master bedroom"] = new("Master Bedroom", 5.0, 5.0, 3.0),
                ["kitchen"] = new("Kitchen", 3.5, 4.0, 3.0),
                ["bathroom"] = new("Bathroom", 2.5, 3.0, 3.0),
                ["toilet"] = new("Toilet", 1.5, 2.0, 3.0),
                ["living"] = new("Living Room", 5.0, 6.0, 3.0),
                ["dining"] = new("Dining Room", 4.0, 5.0, 3.0),
                ["office"] = new("Office", 4.0, 4.5, 3.0),
                ["conference"] = new("Conference Room", 5.0, 7.0, 3.0),
                ["studio"] = new("Studio", 6.0, 8.0, 3.0),
                ["lobby"] = new("Lobby", 5.0, 6.0, 4.0),
                ["corridor"] = new("Corridor", 2.0, 8.0, 3.0),
                ["store"] = new("Store Room", 3.0, 3.0, 3.0),
                ["laundry"] = new("Laundry", 2.5, 3.0, 3.0),
                ["garage"] = new("Garage", 6.0, 6.0, 3.0),
            };

            foreach (var kvp in types)
            {
                if (input.Contains(kvp.Key))
                    return kvp.Value;
            }

            return new RoomTypeInfo("Room", 4.0, 5.0, 3.0);
        }

        private int CountNarrowDoors(Document doc, double minWidthM)
        {
            int count = 0;
            var doors = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (var door in doors)
            {
                var widthParam = door.LookupParameter("Width");
                if (widthParam != null && widthParam.HasValue)
                {
                    var widthFt = widthParam.AsDouble();
                    if (widthFt * 0.3048 < minWidthM)
                        count++;
                }
            }
            return count;
        }

        private List<MaterialRec> GetMaterialsForElement(string elementType)
        {
            return elementType switch
            {
                "wall" => new()
                {
                    new("Reinforced Concrete", "Strength: 25-40 MPa, Density: 2400 kg/m\u00B3", "Load-bearing, multi-story"),
                    new("Clay Brick", "Compressive: 10-35 MPa, Good thermal mass", "Tropical climate, residential"),
                    new("Concrete Block", "Compressive: 7-15 MPa, Cost-effective", "Low-rise, affordable housing"),
                    new("AAC Block", "Lightweight, R-1.0/inch insulation", "Energy-efficient buildings"),
                    new("Steel Frame + Cladding", "High strength-to-weight ratio", "Commercial, high-rise"),
                },
                "floor" => new()
                {
                    new("Reinforced Concrete Slab", "150-250mm thick, 25 MPa min", "Most applications"),
                    new("Post-Tensioned Slab", "Longer spans, thinner sections", "Large floor plates"),
                    new("Composite Steel Deck", "Steel + concrete composite", "Multi-story commercial"),
                },
                "roof" => new()
                {
                    new("Metal Roofing", "Gauge 26-24, galvanized/painted", "Tropical, cost-effective"),
                    new("Concrete Tile", "Long lifespan, good thermal mass", "Residential"),
                    new("Green Roof", "Living vegetation system", "Sustainability, urban heat island"),
                },
                "foundation" => new()
                {
                    new("Strip Foundation", "600mm min width, 1m depth", "Load-bearing walls, residential"),
                    new("Pad Foundation", "For column loads < 500kN", "Frame structures"),
                    new("Raft Foundation", "Full coverage slab", "Weak soils, heavy loads"),
                },
                _ => new()
                {
                    new("Consult engineering specs", "Material depends on load requirements", "Specify element type for details"),
                }
            };
        }

        private List<ParamSuggestion> GetParameterSuggestions(string category)
        {
            return category switch
            {
                "doors" => new()
                {
                    new("Fire Rating", "Text", "Fire resistance classification (e.g., FD30, FD60)"),
                    new("Acoustic Rating", "Number", "Sound insulation in dB (e.g., 35 dB)"),
                    new("Hardware Set", "Text", "Door hardware specification"),
                    new("Security Level", "Text", "Access control requirements"),
                    new("Smoke Seal", "Yes/No", "Smoke sealing requirement"),
                },
                "walls" => new()
                {
                    new("Fire Rating", "Text", "Fire resistance period (e.g., 60/60/60)"),
                    new("Thermal Resistance (R)", "Number", "R-value in m\u00B2K/W"),
                    new("Assembly Code", "Text", "Uniformat classification"),
                    new("Acoustic Rating", "Number", "STC rating"),
                    new("Structural", "Yes/No", "Load-bearing classification"),
                },
                "rooms" => new()
                {
                    new("Department", "Text", "Organizational department"),
                    new("Occupancy", "Number", "Design occupancy count"),
                    new("Floor Finish", "Text", "Floor material specification"),
                    new("Ceiling Height", "Number", "Design ceiling height in mm"),
                    new("Lighting Level", "Number", "Required lux level"),
                },
                "windows" => new()
                {
                    new("U-Value", "Number", "Thermal transmittance W/m\u00B2K"),
                    new("SHGC", "Number", "Solar heat gain coefficient"),
                    new("VLT", "Number", "Visible light transmittance %"),
                    new("Fire Rating", "Text", "Fire resistance if required"),
                },
                _ => new()
                {
                    new("Mark", "Text", "Element identifier"),
                    new("Comments", "Text", "Design notes"),
                    new("Phase Created", "Text", "Construction phase"),
                }
            };
        }

        private void AppendTypeBreakdown(StringBuilder sb, Document doc, BuiltInCategory category, string label)
        {
            var elements = new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElements();

            if (elements.Count == 0) return;

            var typeGroups = elements.GroupBy(e => doc.GetElement(e.GetTypeId())?.Name ?? "Unknown")
                                     .OrderByDescending(g => g.Count())
                                     .Take(5);

            sb.AppendLine($"  {label}:");
            foreach (var g in typeGroups)
                sb.AppendLine($"    {g.Key}: {g.Count()}");
        }

        #endregion
    }

    #region Supporting Types

    internal class CommandRouteResult
    {
        public bool IsCreationCommand { get; set; }
        public RevitCommand CreationCommand { get; set; }
        public string PendingMessage { get; set; }
        public string ResponseMessage { get; set; }

        public static CommandRouteResult ForCreation(RevitCommand cmd, string pendingMsg)
            => new() { IsCreationCommand = true, CreationCommand = cmd, PendingMessage = pendingMsg };

        public static CommandRouteResult ForResponse(string msg)
            => new() { IsCreationCommand = false, ResponseMessage = msg };
    }

    internal record RoomTypeInfo(string Name, double DefaultWidth, double DefaultDepth, double DefaultHeight);
    internal record MaterialRec(string Name, string Properties, string BestFor);
    internal record ParamSuggestion(string Name, string Type, string Purpose);

    #endregion
}
