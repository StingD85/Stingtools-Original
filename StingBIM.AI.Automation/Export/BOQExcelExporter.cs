// StingBIM.AI.Automation.Export.BOQExcelExporter
// Export Bill of Quantities to CSV/Excel format with cost breakdown
// v4 Prompt Reference: Phase 7 — BOQ Excel export with NRM2/SMM7 sections

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using NLog;
using StingBIM.AI.Automation.Budget;

namespace StingBIM.AI.Automation.Export
{
    /// <summary>
    /// Exports a priced Bill of Quantities from the Revit model to CSV format.
    /// Organized by NRM2 work sections with regional unit costs from the cost database.
    /// Output: CSV file suitable for import into Excel or costing software.
    /// </summary>
    public class BOQExcelExporter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly RegionalCostDatabase _costDb;
        private string _region;
        private string _currency;

        private const decimal PRELIMINARIES_RATE = 0.12m;
        private const decimal CONTINGENCY_RATE = 0.05m;
        private const decimal OVERHEADS_RATE = 0.08m;
        private const decimal PROFIT_RATE = 0.05m;
        private const decimal VAT_RATE = 0.18m;

        public BOQExcelExporter(string costCsvPath = null, string region = "Uganda")
        {
            _costDb = new RegionalCostDatabase();
            _region = region;
            _currency = RegionalCostDatabase.RegionCurrencies.GetValueOrDefault(region, "UGX");

            if (!string.IsNullOrEmpty(costCsvPath) && File.Exists(costCsvPath))
                _costDb.LoadFromCsv(costCsvPath);
        }

        /// <summary>
        /// Export a full priced BOQ from the Revit document.
        /// </summary>
        public ExportResult ExportBOQ(Document doc, string outputPath)
        {
            if (doc == null)
                return ExportResult.Failed("No active Revit document.");

            try
            {
                var sb = new StringBuilder();

                // Header
                sb.AppendLine("BILL OF QUANTITIES");
                sb.AppendLine($"Project:,{EscapeCsv(doc.Title ?? "Untitled")}");
                sb.AppendLine($"Date:,{DateTime.Now:yyyy-MM-dd}");
                sb.AppendLine($"Region:,{_region}");
                sb.AppendLine($"Currency:,{_currency}");
                sb.AppendLine($"Measurement Standard:,NRM2");
                sb.AppendLine();

                // Column headers
                sb.AppendLine("Section,Item Ref,Description,Unit,Quantity,Rate,Amount,Level,Notes");

                decimal grandDirectCost = 0;

                // Extract by NRM2 work sections
                var sections = GetNRM2Sections();
                foreach (var section in sections)
                {
                    var items = ExtractSectionItems(doc, section);
                    if (items.Count == 0) continue;

                    // Section header
                    sb.AppendLine($"\"{section.Code} — {section.Title}\",,,,,,,,");

                    decimal sectionTotal = 0;
                    int itemNum = 1;
                    foreach (var item in items)
                    {
                        var itemRef = $"{section.Code}/{itemNum:D3}";
                        sb.AppendLine(
                            $"\"{section.Code}\",\"{itemRef}\",\"{EscapeCsv(item.Description)}\"," +
                            $"\"{item.Unit}\",{item.Quantity:F2},{item.UnitRate:F0},{item.Amount:F0}," +
                            $"\"{EscapeCsv(item.Level)}\",\"{EscapeCsv(item.Notes)}\"");
                        sectionTotal += item.Amount;
                        itemNum++;
                    }

                    // Section subtotal
                    sb.AppendLine($"\"{section.Code}\",,,,,\"Section Total:\",{sectionTotal:F0},,");
                    sb.AppendLine();
                    grandDirectCost += sectionTotal;
                }

                // Cost summary
                sb.AppendLine();
                sb.AppendLine("COST SUMMARY,,,,,,,,");
                sb.AppendLine($",,\"Direct Cost\",,,,,{grandDirectCost:F0},");

                var preliminaries = grandDirectCost * PRELIMINARIES_RATE;
                sb.AppendLine($",,\"Preliminaries (12%)\",,,,,{preliminaries:F0},");

                var contingency = grandDirectCost * CONTINGENCY_RATE;
                sb.AppendLine($",,\"Contingency (5%)\",,,,,{contingency:F0},");

                var subtotal = grandDirectCost + preliminaries + contingency;
                sb.AppendLine($",,\"Sub-Total\",,,,,{subtotal:F0},");

                var overheads = grandDirectCost * OVERHEADS_RATE;
                sb.AppendLine($",,\"Overheads (8%)\",,,,,{overheads:F0},");

                var profit = grandDirectCost * PROFIT_RATE;
                sb.AppendLine($",,\"Profit (5%)\",,,,,{profit:F0},");

                var totalBeforeVat = subtotal + overheads + profit;
                sb.AppendLine($",,\"Total before VAT\",,,,,{totalBeforeVat:F0},");

                var vat = totalBeforeVat * VAT_RATE;
                sb.AppendLine($",,\"VAT (18%)\",,,,,{vat:F0},");

                var grandTotal = totalBeforeVat + vat;
                sb.AppendLine($",,\"GRAND TOTAL ({_currency})\",,,,,{grandTotal:F0},");

                File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
                Logger.Info($"BOQ exported to: {outputPath}");

                return ExportResult.Succeeded(outputPath,
                    $"BOQ exported: {_currency} {grandTotal:N0} grand total");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "BOQ export failed");
                return ExportResult.Failed($"BOQ export failed: {ex.Message}");
            }
        }

        #region NRM2 Work Sections

        private List<NRM2Section> GetNRM2Sections()
        {
            return new List<NRM2Section>
            {
                new NRM2Section { Code = "1", Title = "Preliminaries", Categories = new List<string>() },
                new NRM2Section { Code = "2", Title = "Substructure", Categories = new List<string> { "Structural Foundations" } },
                new NRM2Section { Code = "3", Title = "Concrete Work", Categories = new List<string> { "Floors", "Structural Columns" } },
                new NRM2Section { Code = "4", Title = "Masonry", Categories = new List<string> { "Walls" } },
                new NRM2Section { Code = "5", Title = "Structural Steelwork", Categories = new List<string> { "Structural Framing" } },
                new NRM2Section { Code = "6", Title = "Carpentry", Categories = new List<string>() },
                new NRM2Section { Code = "7", Title = "Roofing", Categories = new List<string> { "Roofs" } },
                new NRM2Section { Code = "8", Title = "Windows, Doors & Stairs", Categories = new List<string> { "Windows", "Doors", "Stairs" } },
                new NRM2Section { Code = "9", Title = "Finishes", Categories = new List<string> { "Ceilings" } },
                new NRM2Section { Code = "10", Title = "Furniture & Equipment", Categories = new List<string> { "Furniture" } },
                new NRM2Section { Code = "11", Title = "Plumbing", Categories = new List<string> { "Plumbing Fixtures", "Pipe Segments" } },
                new NRM2Section { Code = "12", Title = "Mechanical Services", Categories = new List<string> { "Mechanical Equipment", "Duct Systems" } },
                new NRM2Section { Code = "13", Title = "Electrical Services", Categories = new List<string> { "Electrical Fixtures", "Electrical Equipment", "Conduit Runs" } },
                new NRM2Section { Code = "14", Title = "Fire Protection", Categories = new List<string> { "Sprinklers", "Fire Alarm Devices" } },
            };
        }

        private List<BOQLineItem> ExtractSectionItems(Document doc, NRM2Section section)
        {
            var items = new List<BOQLineItem>();

            foreach (var catName in section.Categories)
            {
                try
                {
                    var builtIn = ResolveCategory(catName);
                    if (builtIn == null) continue;

                    var collector = new FilteredElementCollector(doc)
                        .OfCategory(builtIn.Value)
                        .WhereElementIsNotElementType();

                    var typeGroups = collector.ToList()
                        .GroupBy(e => e.Name ?? "Unknown");

                    foreach (var group in typeGroups)
                    {
                        var elements = group.ToList();
                        var qty = ComputeQuantity(elements, catName);
                        var costEst = _costDb.EstimateElementCost(
                            catName, group.Key, qty.Quantity, qty.Unit, _region);

                        var level = GetPrimaryLevel(elements);

                        items.Add(new BOQLineItem
                        {
                            Description = $"{catName}: {group.Key} ({elements.Count} nr)",
                            Unit = qty.Unit,
                            Quantity = qty.Quantity,
                            UnitRate = costEst.UnitRate,
                            Amount = costEst.TotalCost,
                            Level = level,
                            Notes = costEst.IsEstimated ? "Rate estimated" : ""
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Skip BOQ category {catName}: {ex.Message}");
                }
            }

            return items;
        }

        #endregion

        #region Helpers

        private (double Quantity, string Unit) ComputeQuantity(List<Element> elements, string catName)
        {
            var cat = catName.ToLowerInvariant();

            if (cat.Contains("wall") || cat.Contains("floor") || cat.Contains("roof") ||
                cat.Contains("ceiling"))
            {
                double totalArea = 0;
                foreach (var e in elements)
                {
                    var p = e.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (p != null) totalArea += p.AsDouble() * 0.0929;
                }
                return (Math.Max(totalArea, elements.Count), totalArea > 0 ? "m²" : "nr");
            }

            if (cat.Contains("column") || cat.Contains("framing"))
            {
                double totalVol = 0;
                foreach (var e in elements)
                {
                    var p = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                    if (p != null) totalVol += p.AsDouble() * 0.0283;
                }
                return (totalVol > 0 ? totalVol : elements.Count, totalVol > 0 ? "m³" : "nr");
            }

            if (cat.Contains("pipe") || cat.Contains("conduit") || cat.Contains("duct"))
            {
                double totalLen = 0;
                foreach (var e in elements)
                {
                    var p = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                    if (p != null) totalLen += p.AsDouble() * 0.3048;
                }
                return (totalLen > 0 ? totalLen : elements.Count, totalLen > 0 ? "m" : "nr");
            }

            return (elements.Count, "nr");
        }

        private string GetPrimaryLevel(List<Element> elements)
        {
            foreach (var e in elements)
            {
                var levelParam = e.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)
                    ?? e.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                if (levelParam != null)
                {
                    var levelId = levelParam.AsElementId();
                    if (levelId != null && levelId != ElementId.InvalidElementId)
                    {
                        var doc = e.Document;
                        var level = doc.GetElement(levelId);
                        return level?.Name ?? "";
                    }
                }
            }
            return "";
        }

        private BuiltInCategory? ResolveCategory(string name)
        {
            var n = name.ToLowerInvariant();
            if (n.Contains("wall")) return BuiltInCategory.OST_Walls;
            if (n.Contains("floor")) return BuiltInCategory.OST_Floors;
            if (n.Contains("roof")) return BuiltInCategory.OST_Roofs;
            if (n.Contains("door")) return BuiltInCategory.OST_Doors;
            if (n.Contains("window")) return BuiltInCategory.OST_Windows;
            if (n == "structural columns") return BuiltInCategory.OST_StructuralColumns;
            if (n == "structural framing") return BuiltInCategory.OST_StructuralFraming;
            if (n.Contains("ceiling")) return BuiltInCategory.OST_Ceilings;
            if (n.Contains("stair")) return BuiltInCategory.OST_Stairs;
            if (n.Contains("furniture")) return BuiltInCategory.OST_Furniture;
            if (n.Contains("plumbing")) return BuiltInCategory.OST_PlumbingFixtures;
            if (n.Contains("pipe")) return BuiltInCategory.OST_PipeCurves;
            if (n.Contains("mechanical")) return BuiltInCategory.OST_MechanicalEquipment;
            if (n.Contains("duct")) return BuiltInCategory.OST_DuctCurves;
            if (n.Contains("electrical fix")) return BuiltInCategory.OST_ElectricalFixtures;
            if (n.Contains("electrical equip")) return BuiltInCategory.OST_ElectricalEquipment;
            if (n.Contains("conduit")) return BuiltInCategory.OST_Conduit;
            if (n.Contains("sprinkler")) return BuiltInCategory.OST_Sprinklers;
            if (n.Contains("fire alarm")) return BuiltInCategory.OST_FireAlarmDevices;
            return null;
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\"", "\"\"");
        }

        #endregion
    }

    /// <summary>
    /// Exports room, door, window, and equipment schedules to CSV.
    /// </summary>
    public class ScheduleExporter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Export a room schedule.
        /// </summary>
        public ExportResult ExportRoomSchedule(Document doc, string outputPath)
        {
            if (doc == null) return ExportResult.Failed("No active document.");

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Room Schedule");
                sb.AppendLine($"Project:,{doc.Title ?? "Untitled"}");
                sb.AppendLine($"Date:,{DateTime.Now:yyyy-MM-dd}");
                sb.AppendLine();
                sb.AppendLine("Room Number,Room Name,Level,Area (m²),Perimeter (m),Volume (m³),Department,Occupancy");

                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .ToList();

                foreach (var r in rooms)
                {
                    var number = r.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "";
                    var name = r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";
                    var area = (r.get_Parameter(BuiltInParameter.ROOM_AREA)?.AsDouble() ?? 0) * 0.0929;
                    var perimeter = (r.get_Parameter(BuiltInParameter.ROOM_PERIMETER)?.AsDouble() ?? 0) * 0.3048;
                    var volume = (r.get_Parameter(BuiltInParameter.ROOM_VOLUME)?.AsDouble() ?? 0) * 0.0283;
                    var dept = r.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT)?.AsString() ?? "";
                    var occupancy = r.get_Parameter(BuiltInParameter.ROOM_OCCUPANCY)?.AsString() ?? "";

                    var levelId = r.get_Parameter(BuiltInParameter.ROOM_LEVEL_ID)?.AsElementId();
                    var level = levelId != null && levelId != ElementId.InvalidElementId
                        ? doc.GetElement(levelId)?.Name ?? "" : "";

                    sb.AppendLine($"\"{number}\",\"{name}\",\"{level}\",{area:F2},{perimeter:F2},{volume:F2},\"{dept}\",\"{occupancy}\"");
                }

                File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
                return ExportResult.Succeeded(outputPath, $"Room schedule exported: {rooms.Count} rooms");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Room schedule export failed");
                return ExportResult.Failed($"Export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Export a door schedule.
        /// </summary>
        public ExportResult ExportDoorSchedule(Document doc, string outputPath)
        {
            return ExportCategorySchedule(doc, outputPath, BuiltInCategory.OST_Doors, "Door Schedule",
                new[] { "Mark", "Type", "Level", "Width (mm)", "Height (mm)", "Host Wall", "Fire Rating" },
                e => ExtractDoorWindowData(doc, e));
        }

        /// <summary>
        /// Export a window schedule.
        /// </summary>
        public ExportResult ExportWindowSchedule(Document doc, string outputPath)
        {
            return ExportCategorySchedule(doc, outputPath, BuiltInCategory.OST_Windows, "Window Schedule",
                new[] { "Mark", "Type", "Level", "Width (mm)", "Height (mm)", "Sill Height (mm)", "Host Wall" },
                e => ExtractDoorWindowData(doc, e));
        }

        private ExportResult ExportCategorySchedule(Document doc, string outputPath,
            BuiltInCategory category, string title, string[] headers,
            Func<Element, string[]> dataExtractor)
        {
            if (doc == null) return ExportResult.Failed("No active document.");

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(title);
                sb.AppendLine($"Project:,{doc.Title ?? "Untitled"}");
                sb.AppendLine($"Date:,{DateTime.Now:yyyy-MM-dd}");
                sb.AppendLine();
                sb.AppendLine(string.Join(",", headers));

                var elements = new FilteredElementCollector(doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .ToList();

                foreach (var e in elements)
                {
                    try
                    {
                        var data = dataExtractor(e);
                        sb.AppendLine(string.Join(",", data.Select(d => $"\"{EscapeCsv(d)}\"")));
                    }
                    catch { }
                }

                File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
                return ExportResult.Succeeded(outputPath, $"{title} exported: {elements.Count} items");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"{title} export failed");
                return ExportResult.Failed($"Export failed: {ex.Message}");
            }
        }

        private string[] ExtractDoorWindowData(Document doc, Element e)
        {
            var mark = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "";
            var typeName = e.Name ?? "";

            var levelParam = e.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
            var level = "";
            if (levelParam != null)
            {
                var lid = levelParam.AsElementId();
                if (lid != null && lid != ElementId.InvalidElementId)
                    level = doc.GetElement(lid)?.Name ?? "";
            }

            var width = (e.get_Parameter(BuiltInParameter.DOOR_WIDTH)
                ?? e.get_Parameter(BuiltInParameter.WINDOW_WIDTH))?.AsDouble() ?? 0;
            var height = (e.get_Parameter(BuiltInParameter.DOOR_HEIGHT)
                ?? e.get_Parameter(BuiltInParameter.WINDOW_HEIGHT))?.AsDouble() ?? 0;

            var widthMm = (width * 304.8).ToString("F0");
            var heightMm = (height * 304.8).ToString("F0");

            var hostWall = "";
            if (e is FamilyInstance fi && fi.Host != null)
                hostWall = fi.Host.Name ?? "";

            var sillHeight = "";
            var sillParam = e.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
            if (sillParam != null)
                sillHeight = (sillParam.AsDouble() * 304.8).ToString("F0");

            var fireRating = e.LookupParameter("Fire Rating")?.AsString() ?? "";

            return new[] { mark, typeName, level, widthMm, heightMm,
                string.IsNullOrEmpty(sillHeight) ? fireRating : sillHeight, hostWall };
        }

        private string EscapeCsv(string v)
        {
            return string.IsNullOrEmpty(v) ? "" : v.Replace("\"", "\"\"");
        }
    }

    #region Export Data Types

    public class NRM2Section
    {
        public string Code { get; set; }
        public string Title { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
    }

    public class BOQLineItem
    {
        public string Description { get; set; }
        public string Unit { get; set; }
        public double Quantity { get; set; }
        public decimal UnitRate { get; set; }
        public decimal Amount { get; set; }
        public string Level { get; set; }
        public string Notes { get; set; }
    }

    public class ExportResult
    {
        public bool Success { get; set; }
        public string OutputPath { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }

        public static ExportResult Succeeded(string path, string message)
        {
            return new ExportResult { Success = true, OutputPath = path, Message = message };
        }

        public static ExportResult Failed(string error)
        {
            return new ExportResult { Success = false, Error = error, Message = error };
        }
    }

    #endregion
}
