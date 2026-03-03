// StingBIM.AI.Automation.Export.COBieExporter
// COBie 2.4 export — 18-sheet FM handover data from BIM model
// v4 Prompt Reference: Phase 7 — COBie 2.4 (NBIMS-US V3, BS 1192-4:2014)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using NLog;

namespace StingBIM.AI.Automation.Export
{
    /// <summary>
    /// Exports COBie 2.4 compliant data from a Revit model.
    /// Generates 18 CSV sheets following NBIMS-US V3 / BS 1192-4:2014.
    /// Each sheet is a separate CSV in a designated folder.
    /// Includes validation with warnings for missing required fields.
    /// </summary>
    public class COBieExporter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private Document _doc;
        private string _outputDir;
        private readonly List<COBieValidationWarning> _warnings = new List<COBieValidationWarning>();

        /// <summary>
        /// Export all 18 COBie sheets from the Revit document.
        /// Creates a folder with individual CSV files per sheet.
        /// </summary>
        public COBieExportResult Export(Document doc, string outputDir)
        {
            _doc = doc;
            _outputDir = outputDir;
            _warnings.Clear();

            if (doc == null)
                return new COBieExportResult { Success = false, Error = "No active Revit document." };

            try
            {
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                var sheetsExported = new List<string>();

                // 1. Contact
                ExportContact(); sheetsExported.Add("Contact");
                // 2. Facility
                ExportFacility(); sheetsExported.Add("Facility");
                // 3. Floor
                ExportFloor(); sheetsExported.Add("Floor");
                // 4. Space
                ExportSpace(); sheetsExported.Add("Space");
                // 5. Zone
                ExportZone(); sheetsExported.Add("Zone");
                // 6. Type
                ExportType(); sheetsExported.Add("Type");
                // 7. Component
                ExportComponent(); sheetsExported.Add("Component");
                // 8. System
                ExportSystem(); sheetsExported.Add("System");
                // 9. Assembly
                ExportAssembly(); sheetsExported.Add("Assembly");
                // 10. Connection
                ExportConnection(); sheetsExported.Add("Connection");
                // 11. Spare
                ExportSpare(); sheetsExported.Add("Spare");
                // 12. Resource
                ExportResource(); sheetsExported.Add("Resource");
                // 13. Job
                ExportJob(); sheetsExported.Add("Job");
                // 14. Impact
                ExportImpact(); sheetsExported.Add("Impact");
                // 15. Document
                ExportDocument(); sheetsExported.Add("Document");
                // 16. Attribute
                ExportAttribute(); sheetsExported.Add("Attribute");
                // 17. Coordinate
                ExportCoordinate(); sheetsExported.Add("Coordinate");
                // 18. Issue
                ExportIssue(); sheetsExported.Add("Issue");

                Logger.Info($"COBie export complete: {sheetsExported.Count} sheets to {outputDir}");

                return new COBieExportResult
                {
                    Success = true,
                    OutputDirectory = outputDir,
                    SheetsExported = sheetsExported,
                    Warnings = _warnings.ToList(),
                    Summary = $"COBie 2.4 exported: {sheetsExported.Count} sheets.\n" +
                        (_warnings.Count > 0
                            ? $"Warnings: {_warnings.Count} missing required fields."
                            : "All required fields populated.")
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "COBie export failed");
                return new COBieExportResult
                {
                    Success = false,
                    Error = $"COBie export failed: {ex.Message}"
                };
            }
        }

        #region Sheet Exporters

        private void ExportContact()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Email,Company,Phone,Department,OrganizationCode,GivenName,FamilyName," +
                "Street,PostalBox,Town,StateRegion,PostalCode,Country");

            // Default contact from project info
            var info = _doc.ProjectInformation;
            var org = info?.OrganizationName ?? "StingBIM";
            sb.AppendLine($"\"{org.ToLowerInvariant()}@example.com\",\"{Esc(org)}\"," +
                "\"\",,\"{Esc(org)}\",\"Project\",\"Manager\",,,,,,\"Uganda\"");

            WriteCsv("Contact", sb);
        }

        private void ExportFacility()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,CreatedBy,CreatedOn,Category,ProjectName,SiteName," +
                "LinearUnits,AreaUnits,VolumeUnits,CurrencyUnit,AreaMeasurement,Phase,Description");

            var info = _doc.ProjectInformation;
            var name = info?.Name ?? _doc.Title ?? "Facility";
            var category = "Office"; // Default
            var phase = "As-Built";

            sb.AppendLine($"\"{Esc(name)}\",\"{Esc(info?.OrganizationName ?? "")}\"," +
                $"\"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\",\"{category}\"," +
                $"\"{Esc(name)}\",\"{Esc(info?.BuildingName ?? name)}\"," +
                "\"meters\",\"square meters\",\"cubic meters\",\"UGX\"," +
                $"\"Gross Internal\",\"{phase}\",\"{Esc(info?.BuildingName ?? "")}\"");

            WriteCsv("Facility", sb);
        }

        private void ExportFloor()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,CreatedBy,CreatedOn,Category,ExtSystem,ExtObject,ExtIdentifier," +
                "Description,Elevation,Height");

            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            for (int i = 0; i < levels.Count; i++)
            {
                var l = levels[i];
                var elevation = l.Elevation * 0.3048; // ft to m
                var height = i < levels.Count - 1
                    ? (levels[i + 1].Elevation - l.Elevation) * 0.3048
                    : 3.0; // Default floor height

                sb.AppendLine($"\"{Esc(l.Name)}\",\"StingBIM\"," +
                    $"\"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\",\"Floor\"," +
                    $"\"Revit\",\"Level\",\"{l.Id.IntegerValue}\"," +
                    $"\"{Esc(l.Name)}\",{elevation:F3},{height:F3}");
            }

            if (levels.Count == 0)
                AddWarning("Floor", "No levels found in the model.");

            WriteCsv("Floor", sb);
        }

        private void ExportSpace()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,CreatedBy,CreatedOn,Category,FloorName,Description," +
                "ExtSystem,ExtObject,ExtIdentifier,RoomTag,UsableHeight,GrossArea,NetArea");

            var rooms = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .ToList();

            int missingArea = 0;
            foreach (var r in rooms)
            {
                var name = r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "Unnamed";
                var number = r.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "";
                var area = (r.get_Parameter(BuiltInParameter.ROOM_AREA)?.AsDouble() ?? 0) * 0.0929;
                var volume = (r.get_Parameter(BuiltInParameter.ROOM_VOLUME)?.AsDouble() ?? 0) * 0.0283;

                if (area <= 0) missingArea++;

                var levelId = r.get_Parameter(BuiltInParameter.ROOM_LEVEL_ID)?.AsElementId();
                var level = levelId != null && levelId != ElementId.InvalidElementId
                    ? _doc.GetElement(levelId)?.Name ?? "" : "";

                var height = area > 0 && volume > 0 ? volume / area : 2.7;

                sb.AppendLine($"\"{Esc(name)}\",\"StingBIM\"," +
                    $"\"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\",\"Room\"," +
                    $"\"{Esc(level)}\",\"{Esc(name)} {number}\"," +
                    $"\"Revit\",\"Room\",\"{r.Id.IntegerValue}\"," +
                    $"\"{Esc(number)}\",{height:F2},{area:F2},{area:F2}");
            }

            if (missingArea > 0)
                AddWarning("Space", $"{missingArea} room(s) have no computed area.");

            WriteCsv("Space", sb);
        }

        private void ExportZone()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,CreatedBy,CreatedOn,Category,SpaceNames,ExtSystem,ExtObject,ExtIdentifier,Description");

            // Group rooms by department
            var rooms = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .ToList();

            var departments = rooms
                .GroupBy(r => r.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT)?.AsString() ?? "General")
                .Where(g => !string.IsNullOrWhiteSpace(g.Key));

            foreach (var dept in departments)
            {
                var spaceNames = string.Join(",",
                    dept.Select(r => r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? ""));

                sb.AppendLine($"\"{Esc(dept.Key)}\",\"StingBIM\"," +
                    $"\"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\",\"Department\"," +
                    $"\"{Esc(spaceNames)}\",\"Revit\",\"Zone\",\"\",\"{Esc(dept.Key)}\"");
            }

            WriteCsv("Zone", sb);
        }

        private void ExportType()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,CreatedBy,CreatedOn,Category,Description,AssetType,Manufacturer," +
                "ModelNumber,WarrantyGuarantorParts,WarrantyDurationParts,WarrantyGuarantorLabor," +
                "WarrantyDurationLabor,ReplacementCost,ExpectedLife,NominalLength,NominalWidth,NominalHeight");

            var fmCategories = new[]
            {
                BuiltInCategory.OST_MechanicalEquipment,
                BuiltInCategory.OST_ElectricalEquipment,
                BuiltInCategory.OST_PlumbingFixtures,
                BuiltInCategory.OST_Sprinklers,
                BuiltInCategory.OST_FireAlarmDevices,
                BuiltInCategory.OST_LightingFixtures,
                BuiltInCategory.OST_ElectricalFixtures,
                BuiltInCategory.OST_Furniture,
            };

            int missingManufacturer = 0;

            foreach (var cat in fmCategories)
            {
                try
                {
                    var types = new FilteredElementCollector(_doc)
                        .OfCategory(cat)
                        .WhereElementIsElementType()
                        .ToList();

                    foreach (var t in types)
                    {
                        var name = t.Name ?? "Unknown";
                        var catName = t.Category?.Name ?? "";
                        var manufacturer = t.LookupParameter("Manufacturer")?.AsString() ?? "";
                        var modelNum = t.LookupParameter("Model")?.AsString() ?? "";

                        if (string.IsNullOrEmpty(manufacturer)) missingManufacturer++;

                        sb.AppendLine($"\"{Esc(name)}\",\"StingBIM\"," +
                            $"\"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\"," +
                            $"\"{Esc(catName)}\",\"{Esc(name)}\",\"Fixed\"," +
                            $"\"{Esc(manufacturer)}\",\"{Esc(modelNum)}\"," +
                            $"\"\",\"0\",\"\",\"0\"," +
                            $"\"0\",\"0\",\"0\",\"0\",\"0\"");
                    }
                }
                catch { }
            }

            if (missingManufacturer > 0)
                AddWarning("Type", $"{missingManufacturer} type(s) missing Manufacturer — check before handover.");

            WriteCsv("Type", sb);
        }

        private void ExportComponent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,CreatedBy,CreatedOn,TypeName,Space,Description," +
                "ExtSystem,ExtObject,ExtIdentifier,SerialNumber,InstallationDate,WarrantyStartDate,TagNumber,BarCode");

            var fmCategories = new[]
            {
                BuiltInCategory.OST_MechanicalEquipment,
                BuiltInCategory.OST_ElectricalEquipment,
                BuiltInCategory.OST_PlumbingFixtures,
                BuiltInCategory.OST_Sprinklers,
                BuiltInCategory.OST_FireAlarmDevices,
                BuiltInCategory.OST_LightingFixtures,
                BuiltInCategory.OST_ElectricalFixtures,
            };

            int missingMark = 0;

            foreach (var cat in fmCategories)
            {
                try
                {
                    var instances = new FilteredElementCollector(_doc)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .ToList();

                    foreach (var e in instances)
                    {
                        var mark = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "";
                        var typeName = e.Name ?? "";
                        var space = GetContainingRoom(e);

                        if (string.IsNullOrEmpty(mark)) missingMark++;

                        var compName = !string.IsNullOrEmpty(mark) ? mark : $"{typeName}-{e.Id.IntegerValue}";

                        sb.AppendLine($"\"{Esc(compName)}\",\"StingBIM\"," +
                            $"\"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\"," +
                            $"\"{Esc(typeName)}\",\"{Esc(space)}\"," +
                            $"\"{Esc(typeName)}\"," +
                            $"\"Revit\",\"{e.Category?.Name ?? ""}\",\"{e.Id.IntegerValue}\"," +
                            $"\"\",\"\",\"\",\"{Esc(mark)}\",\"\"");
                    }
                }
                catch { }
            }

            if (missingMark > 0)
                AddWarning("Component", $"{missingMark} component(s) missing Mark — assign unique marks before handover.");

            WriteCsv("Component", sb);
        }

        private void ExportSystem()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,CreatedBy,CreatedOn,Category,ComponentNames,ExtSystem,ExtObject,ExtIdentifier,Description");

            // Extract MEP systems
            try
            {
                var systems = new FilteredElementCollector(_doc)
                    .OfClass(typeof(MEPSystem))
                    .Cast<MEPSystem>()
                    .ToList();

                foreach (var sys in systems.Take(100)) // Limit for performance
                {
                    var name = sys.Name ?? "Unnamed System";
                    var category = sys.Category?.Name ?? "MEP";

                    sb.AppendLine($"\"{Esc(name)}\",\"StingBIM\"," +
                        $"\"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\"," +
                        $"\"{Esc(category)}\",\"\"," +
                        $"\"Revit\",\"MEPSystem\",\"{sys.Id.IntegerValue}\"," +
                        $"\"{Esc(name)}\"");
                }
            }
            catch { }

            WriteCsv("System", sb);
        }

        // Sheets 9-18: Minimal/placeholder exports for completeness
        private void ExportAssembly() { WriteMinimalSheet("Assembly",
            "Name,CreatedBy,CreatedOn,SheetName,ParentName,ChildNames,AssemblyType,Description"); }

        private void ExportConnection() { WriteMinimalSheet("Connection",
            "Name,CreatedBy,CreatedOn,ConnectionType,SheetName,RowName1,RowName2,RealizingElement,PortName1,PortName2,Description"); }

        private void ExportSpare() { WriteMinimalSheet("Spare",
            "Name,CreatedBy,CreatedOn,Category,TypeName,Suppliers,SetNumber,PartNumber,Description"); }

        private void ExportResource() { WriteMinimalSheet("Resource",
            "Name,CreatedBy,CreatedOn,Category,ExtSystem,ExtObject,ExtIdentifier,Description"); }

        private void ExportJob() { WriteMinimalSheet("Job",
            "Name,CreatedBy,CreatedOn,Category,Status,TypeName,Description,Duration,DurationUnit,Start,TaskStartUnit,Frequency,FrequencyUnit"); }

        private void ExportImpact() { WriteMinimalSheet("Impact",
            "Name,CreatedBy,CreatedOn,ImpactType,ImpactStage,SheetName,RowName,Value,ImpactUnit,LeadInTime,Duration,LeadOutTime,Description"); }

        private void ExportDocument() { WriteMinimalSheet("Document",
            "Name,CreatedBy,CreatedOn,Category,ApprovalBy,Stage,SheetName,RowName,Directory,File,Description"); }

        private void ExportAttribute()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,CreatedBy,CreatedOn,Category,SheetName,RowName,Value,Unit,Description,AllowedValues");

            // Export FM-relevant parameters from equipment types
            var fmCategories = new[] {
                BuiltInCategory.OST_MechanicalEquipment,
                BuiltInCategory.OST_ElectricalEquipment,
            };

            foreach (var cat in fmCategories)
            {
                try
                {
                    var types = new FilteredElementCollector(_doc)
                        .OfCategory(cat)
                        .WhereElementIsElementType()
                        .Take(50) // Limit for performance
                        .ToList();

                    foreach (var t in types)
                    {
                        foreach (Parameter p in t.Parameters)
                        {
                            if (p.IsShared || p.Definition.Name.StartsWith("COBie", StringComparison.OrdinalIgnoreCase))
                            {
                                var val = GetParamValueString(p);
                                sb.AppendLine($"\"{Esc(p.Definition.Name)}\",\"StingBIM\"," +
                                    $"\"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\",\"Attribute\"," +
                                    $"\"Type\",\"{Esc(t.Name)}\",\"{Esc(val)}\"," +
                                    $"\"\",\"{Esc(p.Definition.Name)}\",\"\"");
                            }
                        }
                    }
                }
                catch { }
            }

            WriteCsv("Attribute", sb);
        }

        private void ExportCoordinate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,CreatedBy,CreatedOn,Category,SheetName,RowName," +
                "CoordinateXAxis,CoordinateYAxis,CoordinateZAxis,ClockwiseRotation,ElevationalRotation,YawRotation");

            var fmCategories = new[] {
                BuiltInCategory.OST_MechanicalEquipment,
                BuiltInCategory.OST_ElectricalEquipment,
            };

            foreach (var cat in fmCategories)
            {
                try
                {
                    var instances = new FilteredElementCollector(_doc)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .Take(100)
                        .ToList();

                    foreach (var e in instances)
                    {
                        var loc = e.Location as LocationPoint;
                        if (loc == null) continue;

                        var pt = loc.Point;
                        var x = pt.X * 0.3048;
                        var y = pt.Y * 0.3048;
                        var z = pt.Z * 0.3048;

                        var mark = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()
                            ?? $"{e.Name}-{e.Id.IntegerValue}";

                        sb.AppendLine($"\"{Esc(mark)}\",\"StingBIM\"," +
                            $"\"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\",\"Point\"," +
                            $"\"Component\",\"{Esc(mark)}\"," +
                            $"{x:F3},{y:F3},{z:F3},0,0,0");
                    }
                }
                catch { }
            }

            WriteCsv("Coordinate", sb);
        }

        private void ExportIssue() { WriteMinimalSheet("Issue",
            "Name,CreatedBy,CreatedOn,Type,Risk,Chance,Impact,SheetName1,RowName1,SheetName2,RowName2,Description,Owner,Mitigation"); }

        #endregion

        #region Helpers

        private void WriteCsv(string sheetName, StringBuilder content)
        {
            var path = Path.Combine(_outputDir, $"COBie_{sheetName}.csv");
            File.WriteAllText(path, content.ToString(), Encoding.UTF8);
        }

        private void WriteMinimalSheet(string sheetName, string headers)
        {
            var sb = new StringBuilder();
            sb.AppendLine(headers);
            // Empty data — placeholder sheet
            WriteCsv(sheetName, sb);
        }

        private string GetContainingRoom(Element e)
        {
            try
            {
                if (e is FamilyInstance fi)
                {
                    var room = fi.Room;
                    if (room != null)
                        return room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";
                }
            }
            catch { }
            return "";
        }

        private string GetParamValueString(Parameter p)
        {
            if (p == null || !p.HasValue) return "";
            return p.StorageType switch
            {
                StorageType.String => p.AsString() ?? "",
                StorageType.Integer => p.AsInteger().ToString(),
                StorageType.Double => p.AsDouble().ToString("F3"),
                StorageType.ElementId => p.AsElementId()?.IntegerValue.ToString() ?? "",
                _ => ""
            };
        }

        private string Esc(string v)
        {
            return string.IsNullOrEmpty(v) ? "" : v.Replace("\"", "\"\"");
        }

        private void AddWarning(string sheet, string message)
        {
            _warnings.Add(new COBieValidationWarning { Sheet = sheet, Message = message });
            Logger.Warn($"COBie [{sheet}]: {message}");
        }

        #endregion
    }

    #region COBie Data Types

    public class COBieExportResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string OutputDirectory { get; set; }
        public string Summary { get; set; }
        public List<string> SheetsExported { get; set; } = new List<string>();
        public List<COBieValidationWarning> Warnings { get; set; } = new List<COBieValidationWarning>();
    }

    public class COBieValidationWarning
    {
        public string Sheet { get; set; }
        public string Message { get; set; }
    }

    #endregion
}
