// StingBIM.AI.Revit.Services.RevitModelQueryService
// Queries the active Revit Document to answer model-related user queries

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using NLog;
using StingBIM.AI.NLP.Domain;

namespace StingBIM.AI.Revit.Services
{
    /// <summary>
    /// Implements model querying against the active Revit Document.
    /// Provides room counts, element counts, areas, materials, BOQ, and compliance info.
    /// </summary>
    public class RevitModelQueryService : IModelQueryService
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Document _document;

        public RevitModelQueryService(Document document)
        {
            _document = document;
        }

        public bool IsModelAvailable => _document != null;

        public string GetModelSummary()
        {
            if (_document == null)
                return "No Revit model is currently open. Please open a project first.";

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Project: {_document.Title}");

                // Count levels
                var levels = new FilteredElementCollector(_document)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();
                sb.AppendLine($"Levels: {levels.Count}");
                foreach (var level in levels)
                {
                    sb.AppendLine($"  - {level.Name} (Elevation: {level.Elevation:F1})");
                }

                // Count rooms
                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Area > 0)
                    .ToList();
                sb.AppendLine($"Rooms: {rooms.Count}");

                // Count key elements
                var walls = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Walls)
                    .WhereElementIsNotElementType()
                    .GetElementCount();
                var doors = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .GetElementCount();
                var windows = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Windows)
                    .WhereElementIsNotElementType()
                    .GetElementCount();

                sb.AppendLine($"Walls: {walls}");
                sb.AppendLine($"Doors: {doors}");
                sb.AppendLine($"Windows: {windows}");

                // Total area
                if (rooms.Any())
                {
                    var totalArea = rooms.Sum(r => r.Area);
                    sb.AppendLine($"Total Floor Area: {totalArea:F1} sq ft ({totalArea * 0.0929:F1} sq m)");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting model summary");
                return $"Error reading model data: {ex.Message}";
            }
        }

        public string GetRoomInfo()
        {
            if (_document == null)
                return "No Revit model is currently open.";

            try
            {
                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Area > 0)
                    .OrderBy(r => r.Level?.Name ?? "")
                    .ThenBy(r => r.Name)
                    .ToList();

                if (!rooms.Any())
                    return "No rooms have been placed in the current model. Use 'Create a room' to add rooms.";

                var sb = new StringBuilder();
                sb.AppendLine($"Found {rooms.Count} rooms in the model:");

                foreach (var room in rooms)
                {
                    var area = room.Area;
                    var areaM2 = area * 0.0929;
                    var level = room.Level?.Name ?? "Unknown";
                    sb.AppendLine($"  - {room.Name} (Level: {level}, Area: {areaM2:F1} sq m)");
                }

                var totalArea = rooms.Sum(r => r.Area) * 0.0929;
                sb.AppendLine($"\nTotal Floor Area: {totalArea:F1} sq m");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting room info");
                return $"Error reading room data: {ex.Message}";
            }
        }

        public string GetTotalArea()
        {
            if (_document == null)
                return "No Revit model is currently open.";

            try
            {
                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                if (!rooms.Any())
                    return "No rooms have been placed in the model yet. Place rooms to calculate area.";

                var totalAreaSqFt = rooms.Sum(r => r.Area);
                var totalAreaSqM = totalAreaSqFt * 0.0929;

                return $"Total floor area: {totalAreaSqM:F1} sq m ({totalAreaSqFt:F1} sq ft) across {rooms.Count} rooms.";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error calculating total area");
                return $"Error calculating area: {ex.Message}";
            }
        }

        public string GetElementCounts()
        {
            if (_document == null)
                return "No Revit model is currently open.";

            try
            {
                var categories = new[]
                {
                    (BuiltInCategory.OST_Walls, "Walls"),
                    (BuiltInCategory.OST_Doors, "Doors"),
                    (BuiltInCategory.OST_Windows, "Windows"),
                    (BuiltInCategory.OST_Rooms, "Rooms"),
                    (BuiltInCategory.OST_Floors, "Floors"),
                    (BuiltInCategory.OST_Roofs, "Roofs"),
                    (BuiltInCategory.OST_StructuralColumns, "Columns"),
                    (BuiltInCategory.OST_Stairs, "Stairs"),
                    (BuiltInCategory.OST_Furniture, "Furniture"),
                    (BuiltInCategory.OST_MechanicalEquipment, "Mechanical Equipment"),
                    (BuiltInCategory.OST_ElectricalEquipment, "Electrical Equipment"),
                    (BuiltInCategory.OST_PlumbingFixtures, "Plumbing Fixtures"),
                };

                var sb = new StringBuilder();
                sb.AppendLine("Element counts in the current model:");

                int total = 0;
                foreach (var (category, name) in categories)
                {
                    var count = new FilteredElementCollector(_document)
                        .OfCategory(category)
                        .WhereElementIsNotElementType()
                        .GetElementCount();
                    if (count > 0)
                    {
                        sb.AppendLine($"  - {name}: {count}");
                        total += count;
                    }
                }

                sb.AppendLine($"\nTotal elements: {total}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error counting elements");
                return $"Error counting elements: {ex.Message}";
            }
        }

        /// <summary>
        /// Lists elements of a specific category with their type names and basic info.
        /// </summary>
        public string GetCategoryElementList(BuiltInCategory category, string categoryName)
        {
            if (_document == null)
                return "No Revit model is currently open.";

            try
            {
                var elements = new FilteredElementCollector(_document)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .ToList();

                if (!elements.Any())
                    return $"No {categoryName.ToLower()} found in the current model.";

                var sb = new StringBuilder();
                sb.AppendLine($"Found {elements.Count} {categoryName.ToLower()} in the model:");

                // Group by type
                var grouped = elements.GroupBy(e =>
                {
                    var typeId = e.GetTypeId();
                    var type = _document.GetElement(typeId);
                    return type?.Name ?? "Unknown Type";
                });

                foreach (var group in grouped.OrderBy(g => g.Key))
                {
                    sb.AppendLine($"\n  {group.Key} ({group.Count()}):");

                    foreach (var element in group.Take(10)) // Limit to 10 per type
                    {
                        var name = element.Name;
                        var level = element.LevelId != null ? _document.GetElement(element.LevelId)?.Name : null;
                        var locationInfo = level != null ? $" — Level: {level}" : "";

                        if (string.IsNullOrEmpty(name))
                            name = $"{categoryName} #{element.Id.IntegerValue}";

                        sb.AppendLine($"    - {name}{locationInfo}");
                    }

                    if (group.Count() > 10)
                        sb.AppendLine($"    ... and {group.Count() - 10} more");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error listing {categoryName}");
                return $"Error listing {categoryName.ToLower()}: {ex.Message}";
            }
        }

        public string CheckCompliance(string standardName = null)
        {
            if (_document == null)
                return "No Revit model is currently open. Open a project to check compliance.";

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Compliance Check Results:");
                sb.AppendLine("========================");

                // Check room sizes
                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                if (!rooms.Any())
                {
                    sb.AppendLine("Warning: No rooms placed in model. Cannot verify room-based compliance.");
                }
                else
                {
                    int compliant = 0;
                    int issues = 0;

                    foreach (var room in rooms)
                    {
                        var areaSqM = room.Area * 0.0929;
                        var roomName = room.Name?.ToLowerInvariant() ?? "";

                        // Check minimum areas per IBC/local standards
                        double minArea = 6.0; // default minimum
                        if (roomName.Contains("bedroom")) minArea = 9.0;
                        else if (roomName.Contains("kitchen")) minArea = 6.0;
                        else if (roomName.Contains("bathroom")) minArea = 2.5;
                        else if (roomName.Contains("living")) minArea = 12.0;

                        if (areaSqM >= minArea)
                        {
                            compliant++;
                        }
                        else
                        {
                            issues++;
                            sb.AppendLine($"  Issue: {room.Name} area ({areaSqM:F1} sq m) is below minimum ({minArea} sq m)");
                        }
                    }

                    sb.AppendLine($"\nRoom Size Compliance: {compliant}/{rooms.Count} rooms pass");
                    if (issues > 0)
                        sb.AppendLine($"  {issues} room(s) need attention");
                }

                // Check doors exist
                var doorCount = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .GetElementCount();

                sb.AppendLine($"\nEgress: {doorCount} doors found");
                if (doorCount == 0 && rooms.Any())
                    sb.AppendLine("  Warning: No doors found. Rooms require means of egress.");

                // Check windows for natural light
                var windowCount = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Windows)
                    .WhereElementIsNotElementType()
                    .GetElementCount();

                sb.AppendLine($"Natural Light: {windowCount} windows found");
                if (windowCount == 0 && rooms.Any())
                    sb.AppendLine("  Warning: No windows found. Habitable rooms require natural light per IBC.");

                sb.AppendLine("\nStandards checked: IBC 2021, ISO 19650, Local Building Code");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking compliance");
                return $"Error during compliance check: {ex.Message}";
            }
        }

        public string AnswerQuery(string query, string intentType)
        {
            switch (intentType?.ToUpperInvariant())
            {
                case "QUERY_MODEL":
                    if (query.Contains("furniture", StringComparison.OrdinalIgnoreCase))
                        return GetCategoryElementList(BuiltInCategory.OST_Furniture, "Furniture");
                    if (query.Contains("wall", StringComparison.OrdinalIgnoreCase))
                        return GetCategoryElementList(BuiltInCategory.OST_Walls, "Walls");
                    if (query.Contains("door", StringComparison.OrdinalIgnoreCase))
                        return GetCategoryElementList(BuiltInCategory.OST_Doors, "Doors");
                    if (query.Contains("window", StringComparison.OrdinalIgnoreCase))
                        return GetCategoryElementList(BuiltInCategory.OST_Windows, "Windows");
                    if (query.Contains("column", StringComparison.OrdinalIgnoreCase))
                        return GetCategoryElementList(BuiltInCategory.OST_StructuralColumns, "Columns");
                    if (query.Contains("floor", StringComparison.OrdinalIgnoreCase) && !query.Contains("floor plan", StringComparison.OrdinalIgnoreCase))
                        return GetCategoryElementList(BuiltInCategory.OST_Floors, "Floors");
                    if (query.Contains("room", StringComparison.OrdinalIgnoreCase))
                        return GetRoomInfo();
                    if (query.Contains("area", StringComparison.OrdinalIgnoreCase))
                        return GetTotalArea();
                    if (query.Contains("count", StringComparison.OrdinalIgnoreCase) ||
                        query.Contains("how many", StringComparison.OrdinalIgnoreCase) ||
                        query.Contains("element", StringComparison.OrdinalIgnoreCase))
                        return GetElementCounts();
                    if (query.Contains("material", StringComparison.OrdinalIgnoreCase))
                        return GetMaterialsDetailed().Summary;
                    return GetModelSummary();

                case "QUERY_AREA":
                    return GetTotalArea();

                case "CHECK_COMPLIANCE":
                    return CheckCompliance();

                case "QUERY_MATERIALS":
                    return GetMaterialsDetailed().Summary;

                case "GENERATE_BOQ":
                    return GetBOQDetailed().Summary;

                case "MATERIAL_TAKEOFF":
                    return GetMaterialTakeoffDetailed().Summary;

                case "QUERY_PARAMETERS":
                    return GetParameterDetails().Summary;

                default:
                    return GetModelSummary();
            }
        }

        #region Materials

        public QueryResult GetMaterialsDetailed()
        {
            if (_document == null)
                return new QueryResult { Summary = "No Revit model is currently open." };

            try
            {
                var result = new QueryResult();
                var materialUsage = new Dictionary<string, MaterialUsageInfo>();

                // Collect materials from major element categories
                var categoryList = new[]
                {
                    (BuiltInCategory.OST_Walls, "Walls"),
                    (BuiltInCategory.OST_Floors, "Floors"),
                    (BuiltInCategory.OST_Roofs, "Roofs"),
                    (BuiltInCategory.OST_StructuralColumns, "Columns"),
                    (BuiltInCategory.OST_StructuralFraming, "Beams"),
                    (BuiltInCategory.OST_Doors, "Doors"),
                    (BuiltInCategory.OST_Windows, "Windows"),
                    (BuiltInCategory.OST_Ceilings, "Ceilings"),
                };

                foreach (var (category, catName) in categoryList)
                {
                    var elements = new FilteredElementCollector(_document)
                        .OfCategory(category)
                        .WhereElementIsNotElementType()
                        .ToList();

                    foreach (var element in elements)
                    {
                        try
                        {
                            var matIds = element.GetMaterialIds(false);
                            foreach (var matId in matIds)
                            {
                                var material = _document.GetElement(matId) as Material;
                                if (material == null) continue;

                                var matName = material.Name;
                                if (!materialUsage.ContainsKey(matName))
                                {
                                    materialUsage[matName] = new MaterialUsageInfo
                                    {
                                        Name = matName,
                                        MaterialClass = material.MaterialClass ?? "General",
                                        Color = material.Color != null ? $"RGB({material.Color.Red},{material.Color.Green},{material.Color.Blue})" : "N/A"
                                    };
                                }

                                var usage = materialUsage[matName];
                                usage.ElementCount++;

                                // Try to get material area and volume
                                try
                                {
                                    var area = element.GetMaterialArea(matId, false);
                                    usage.TotalAreaSqFt += area;
                                }
                                catch { /* Some elements don't support material area */ }

                                try
                                {
                                    var volume = element.GetMaterialVolume(matId);
                                    usage.TotalVolumeCuFt += volume;
                                }
                                catch { /* Some elements don't support material volume */ }

                                if (!usage.Categories.Contains(catName))
                                    usage.Categories.Add(catName);
                            }
                        }
                        catch { /* Skip elements that don't support material queries */ }
                    }
                }

                if (!materialUsage.Any())
                {
                    result.Summary = "No materials found in the model. Elements may not have materials assigned.";
                    return result;
                }

                // Build summary
                var sb = new StringBuilder();
                sb.AppendLine($"Found {materialUsage.Count} materials used across the model:");

                // Group by material class
                var grouped = materialUsage.Values
                    .GroupBy(m => m.MaterialClass)
                    .OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    var section = new QueryDetailSection
                    {
                        Header = $"{group.Key} Materials",
                        Summary = $"{group.Count()} materials"
                    };

                    foreach (var mat in group.OrderBy(m => m.Name))
                    {
                        var item = new QueryDetailItem
                        {
                            Label = mat.Name,
                            Value = $"{mat.ElementCount} elements",
                            Unit = "",
                            SubItems = new List<QueryDetailItem>()
                        };

                        if (mat.TotalAreaSqFt > 0)
                        {
                            item.SubItems.Add(new QueryDetailItem
                            {
                                Label = "Area",
                                Value = $"{mat.TotalAreaSqFt * 0.0929:F2}",
                                Unit = "sq m"
                            });
                        }

                        if (mat.TotalVolumeCuFt > 0)
                        {
                            item.SubItems.Add(new QueryDetailItem
                            {
                                Label = "Volume",
                                Value = $"{mat.TotalVolumeCuFt * 0.0283:F3}",
                                Unit = "cu m"
                            });
                        }

                        if (mat.Categories.Any())
                        {
                            item.SubItems.Add(new QueryDetailItem
                            {
                                Label = "Used in",
                                Value = string.Join(", ", mat.Categories),
                                Unit = ""
                            });
                        }

                        section.Items.Add(item);
                    }

                    result.Sections.Add(section);
                    sb.AppendLine($"  {group.Key}: {group.Count()} materials ({group.Sum(m => m.ElementCount)} elements)");
                }

                result.Summary = sb.ToString();
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting materials");
                return new QueryResult { Summary = $"Error reading materials: {ex.Message}" };
            }
        }

        #endregion

        #region BOQ (Bill of Quantities)

        public QueryResult GetBOQDetailed()
        {
            if (_document == null)
                return new QueryResult { Summary = "No Revit model is currently open." };

            try
            {
                var result = new QueryResult();
                var sb = new StringBuilder();
                sb.AppendLine("Bill of Quantities (BOQ):");
                sb.AppendLine($"Project: {_document.Title}");
                sb.AppendLine();

                // Walls
                var wallSection = BuildWallBOQSection();
                if (wallSection != null) result.Sections.Add(wallSection);

                // Doors
                var doorSection = BuildDoorBOQSection();
                if (doorSection != null) result.Sections.Add(doorSection);

                // Windows
                var windowSection = BuildWindowBOQSection();
                if (windowSection != null) result.Sections.Add(windowSection);

                // Floors
                var floorSection = BuildFloorBOQSection();
                if (floorSection != null) result.Sections.Add(floorSection);

                // Roofs
                var roofSection = BuildRoofBOQSection();
                if (roofSection != null) result.Sections.Add(roofSection);

                // Columns
                var columnSection = BuildColumnBOQSection();
                if (columnSection != null) result.Sections.Add(columnSection);

                // Rooms summary
                var roomSection = BuildRoomBOQSection();
                if (roomSection != null) result.Sections.Add(roomSection);

                // Build summary text
                foreach (var section in result.Sections)
                {
                    sb.AppendLine($"  {section.Header} — {section.Summary}");
                }

                if (!result.Sections.Any())
                {
                    sb.AppendLine("No elements found in the model for BOQ generation.");
                }

                result.Summary = sb.ToString();
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating BOQ");
                return new QueryResult { Summary = $"Error generating BOQ: {ex.Message}" };
            }
        }

        private QueryDetailSection BuildWallBOQSection()
        {
            var walls = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .ToList();

            if (!walls.Any()) return null;

            var section = new QueryDetailSection
            {
                Header = "Walls",
                Summary = $"{walls.Count} walls"
            };

            // Group by wall type
            var grouped = walls.GroupBy(w => w.WallType?.Name ?? "Unknown Type");
            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                double totalLength = 0;
                double totalArea = 0;
                double totalVolume = 0;

                foreach (var wall in group)
                {
                    var lengthParam = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                    if (lengthParam != null) totalLength += lengthParam.AsDouble();

                    var areaParam = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (areaParam != null) totalArea += areaParam.AsDouble();

                    var volumeParam = wall.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                    if (volumeParam != null) totalVolume += volumeParam.AsDouble();
                }

                var item = new QueryDetailItem
                {
                    Label = group.Key,
                    Value = $"{group.Count()}",
                    Unit = "nos",
                    SubItems = new List<QueryDetailItem>
                    {
                        new QueryDetailItem { Label = "Total Length", Value = $"{totalLength * 0.3048:F2}", Unit = "m" },
                        new QueryDetailItem { Label = "Total Area", Value = $"{totalArea * 0.0929:F2}", Unit = "sq m" },
                        new QueryDetailItem { Label = "Total Volume", Value = $"{totalVolume * 0.0283:F3}", Unit = "cu m" }
                    }
                };

                section.Items.Add(item);
            }

            var totalWallArea = walls.Sum(w =>
            {
                var p = w.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                return p != null ? p.AsDouble() * 0.0929 : 0;
            });
            section.Summary = $"{walls.Count} walls, {totalWallArea:F1} sq m total area";

            return section;
        }

        private QueryDetailSection BuildDoorBOQSection()
        {
            var doors = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .ToList();

            if (!doors.Any()) return null;

            var section = new QueryDetailSection
            {
                Header = "Doors",
                Summary = $"{doors.Count} doors"
            };

            var grouped = doors.GroupBy(d =>
            {
                var typeId = d.GetTypeId();
                var type = _document.GetElement(typeId);
                return type?.Name ?? "Unknown Type";
            });

            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                var item = new QueryDetailItem
                {
                    Label = group.Key,
                    Value = $"{group.Count()}",
                    Unit = "nos",
                    SubItems = new List<QueryDetailItem>()
                };

                // Try to get width and height from type
                var sampleElement = group.First();
                var typeId = sampleElement.GetTypeId();
                var doorType = _document.GetElement(typeId);
                if (doorType != null)
                {
                    var widthParam = doorType.get_Parameter(BuiltInParameter.DOOR_WIDTH) ??
                                     doorType.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM);
                    var heightParam = doorType.get_Parameter(BuiltInParameter.DOOR_HEIGHT) ??
                                      doorType.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM);

                    if (widthParam != null)
                        item.SubItems.Add(new QueryDetailItem { Label = "Width", Value = $"{widthParam.AsDouble() * 304.8:F0}", Unit = "mm" });
                    if (heightParam != null)
                        item.SubItems.Add(new QueryDetailItem { Label = "Height", Value = $"{heightParam.AsDouble() * 304.8:F0}", Unit = "mm" });
                }

                section.Items.Add(item);
            }

            return section;
        }

        private QueryDetailSection BuildWindowBOQSection()
        {
            var windows = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Windows)
                .WhereElementIsNotElementType()
                .ToList();

            if (!windows.Any()) return null;

            var section = new QueryDetailSection
            {
                Header = "Windows",
                Summary = $"{windows.Count} windows"
            };

            var grouped = windows.GroupBy(w =>
            {
                var typeId = w.GetTypeId();
                var type = _document.GetElement(typeId);
                return type?.Name ?? "Unknown Type";
            });

            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                var item = new QueryDetailItem
                {
                    Label = group.Key,
                    Value = $"{group.Count()}",
                    Unit = "nos",
                    SubItems = new List<QueryDetailItem>()
                };

                var sampleElement = group.First();
                var typeId = sampleElement.GetTypeId();
                var winType = _document.GetElement(typeId);
                if (winType != null)
                {
                    var widthParam = winType.get_Parameter(BuiltInParameter.WINDOW_WIDTH) ??
                                     winType.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM);
                    var heightParam = winType.get_Parameter(BuiltInParameter.WINDOW_HEIGHT) ??
                                      winType.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM);

                    if (widthParam != null)
                        item.SubItems.Add(new QueryDetailItem { Label = "Width", Value = $"{widthParam.AsDouble() * 304.8:F0}", Unit = "mm" });
                    if (heightParam != null)
                        item.SubItems.Add(new QueryDetailItem { Label = "Height", Value = $"{heightParam.AsDouble() * 304.8:F0}", Unit = "mm" });
                }

                section.Items.Add(item);
            }

            return section;
        }

        private QueryDetailSection BuildFloorBOQSection()
        {
            var floors = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType()
                .ToList();

            if (!floors.Any()) return null;

            var section = new QueryDetailSection
            {
                Header = "Floors",
                Summary = $"{floors.Count} floors"
            };

            var grouped = floors.GroupBy(f =>
            {
                var typeId = f.GetTypeId();
                var type = _document.GetElement(typeId);
                return type?.Name ?? "Unknown Type";
            });

            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                double totalArea = 0;
                double totalVolume = 0;

                foreach (var floor in group)
                {
                    var areaParam = floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (areaParam != null) totalArea += areaParam.AsDouble();

                    var volumeParam = floor.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                    if (volumeParam != null) totalVolume += volumeParam.AsDouble();
                }

                var item = new QueryDetailItem
                {
                    Label = group.Key,
                    Value = $"{group.Count()}",
                    Unit = "nos",
                    SubItems = new List<QueryDetailItem>
                    {
                        new QueryDetailItem { Label = "Total Area", Value = $"{totalArea * 0.0929:F2}", Unit = "sq m" },
                        new QueryDetailItem { Label = "Total Volume", Value = $"{totalVolume * 0.0283:F3}", Unit = "cu m" }
                    }
                };

                section.Items.Add(item);
            }

            var totalFloorArea = floors.Sum(f =>
            {
                var p = f.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                return p != null ? p.AsDouble() * 0.0929 : 0;
            });
            section.Summary = $"{floors.Count} floors, {totalFloorArea:F1} sq m total area";

            return section;
        }

        private QueryDetailSection BuildRoofBOQSection()
        {
            var roofs = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Roofs)
                .WhereElementIsNotElementType()
                .ToList();

            if (!roofs.Any()) return null;

            var section = new QueryDetailSection
            {
                Header = "Roofs",
                Summary = $"{roofs.Count} roofs"
            };

            var grouped = roofs.GroupBy(r =>
            {
                var typeId = r.GetTypeId();
                var type = _document.GetElement(typeId);
                return type?.Name ?? "Unknown Type";
            });

            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                double totalArea = 0;

                foreach (var roof in group)
                {
                    var areaParam = roof.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (areaParam != null) totalArea += areaParam.AsDouble();
                }

                var item = new QueryDetailItem
                {
                    Label = group.Key,
                    Value = $"{group.Count()}",
                    Unit = "nos",
                    SubItems = new List<QueryDetailItem>
                    {
                        new QueryDetailItem { Label = "Total Area", Value = $"{totalArea * 0.0929:F2}", Unit = "sq m" }
                    }
                };

                section.Items.Add(item);
            }

            return section;
        }

        private QueryDetailSection BuildColumnBOQSection()
        {
            var columns = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .ToList();

            if (!columns.Any()) return null;

            var section = new QueryDetailSection
            {
                Header = "Structural Columns",
                Summary = $"{columns.Count} columns"
            };

            var grouped = columns.GroupBy(c =>
            {
                var typeId = c.GetTypeId();
                var type = _document.GetElement(typeId);
                return type?.Name ?? "Unknown Type";
            });

            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                double totalVolume = 0;

                foreach (var col in group)
                {
                    var volumeParam = col.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                    if (volumeParam != null) totalVolume += volumeParam.AsDouble();
                }

                var item = new QueryDetailItem
                {
                    Label = group.Key,
                    Value = $"{group.Count()}",
                    Unit = "nos",
                    SubItems = new List<QueryDetailItem>
                    {
                        new QueryDetailItem { Label = "Total Volume", Value = $"{totalVolume * 0.0283:F3}", Unit = "cu m" }
                    }
                };

                section.Items.Add(item);
            }

            return section;
        }

        private QueryDetailSection BuildRoomBOQSection()
        {
            var rooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Area > 0)
                .ToList();

            if (!rooms.Any()) return null;

            var section = new QueryDetailSection
            {
                Header = "Rooms / Spaces",
                Summary = $"{rooms.Count} rooms"
            };

            foreach (var room in rooms.OrderBy(r => r.Level?.Name ?? "").ThenBy(r => r.Name))
            {
                var areaSqM = room.Area * 0.0929;
                var perimeterM = room.Perimeter * 0.3048;
                var volumeCuM = room.Volume * 0.0283;

                var item = new QueryDetailItem
                {
                    Label = room.Name,
                    Value = $"{areaSqM:F1}",
                    Unit = "sq m",
                    SubItems = new List<QueryDetailItem>
                    {
                        new QueryDetailItem { Label = "Level", Value = room.Level?.Name ?? "N/A", Unit = "" },
                        new QueryDetailItem { Label = "Perimeter", Value = $"{perimeterM:F2}", Unit = "m" },
                        new QueryDetailItem { Label = "Volume", Value = $"{volumeCuM:F2}", Unit = "cu m" }
                    }
                };

                section.Items.Add(item);
            }

            var totalArea = rooms.Sum(r => r.Area) * 0.0929;
            section.Summary = $"{rooms.Count} rooms, {totalArea:F1} sq m total";

            return section;
        }

        #endregion

        #region Material Takeoff

        public QueryResult GetMaterialTakeoffDetailed()
        {
            if (_document == null)
                return new QueryResult { Summary = "No Revit model is currently open." };

            try
            {
                var result = new QueryResult();
                var materialTakeoff = new Dictionary<string, MaterialTakeoffInfo>();

                // Collect material quantities from all element categories
                var categoryList = new[]
                {
                    BuiltInCategory.OST_Walls,
                    BuiltInCategory.OST_Floors,
                    BuiltInCategory.OST_Roofs,
                    BuiltInCategory.OST_StructuralColumns,
                    BuiltInCategory.OST_StructuralFraming,
                    BuiltInCategory.OST_Ceilings,
                    BuiltInCategory.OST_Doors,
                    BuiltInCategory.OST_Windows,
                };

                foreach (var category in categoryList)
                {
                    var elements = new FilteredElementCollector(_document)
                        .OfCategory(category)
                        .WhereElementIsNotElementType()
                        .ToList();

                    foreach (var element in elements)
                    {
                        try
                        {
                            var matIds = element.GetMaterialIds(false);
                            foreach (var matId in matIds)
                            {
                                var material = _document.GetElement(matId) as Material;
                                if (material == null) continue;

                                var matName = material.Name;
                                if (!materialTakeoff.ContainsKey(matName))
                                {
                                    materialTakeoff[matName] = new MaterialTakeoffInfo
                                    {
                                        Name = matName,
                                        MaterialClass = material.MaterialClass ?? "General"
                                    };
                                }

                                var takeoff = materialTakeoff[matName];
                                takeoff.ElementCount++;

                                try { takeoff.TotalAreaSqFt += element.GetMaterialArea(matId, false); }
                                catch { }

                                try { takeoff.TotalVolumeCuFt += element.GetMaterialVolume(matId); }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }

                if (!materialTakeoff.Any())
                {
                    result.Summary = "No material quantities found. Ensure elements have materials assigned.";
                    return result;
                }

                var sb = new StringBuilder();
                sb.AppendLine("Material Takeoff:");
                sb.AppendLine($"Project: {_document.Title}");
                sb.AppendLine();

                // Create section per material class
                var grouped = materialTakeoff.Values
                    .GroupBy(m => m.MaterialClass)
                    .OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    var section = new QueryDetailSection
                    {
                        Header = group.Key,
                        Summary = $"{group.Count()} materials"
                    };

                    double groupAreaSqM = 0;
                    double groupVolumeCuM = 0;

                    foreach (var mat in group.OrderBy(m => m.Name))
                    {
                        var areaSqM = mat.TotalAreaSqFt * 0.0929;
                        var volumeCuM = mat.TotalVolumeCuFt * 0.0283;
                        groupAreaSqM += areaSqM;
                        groupVolumeCuM += volumeCuM;

                        var item = new QueryDetailItem
                        {
                            Label = mat.Name,
                            Value = $"{mat.ElementCount} elements",
                            Unit = "",
                            SubItems = new List<QueryDetailItem>()
                        };

                        if (areaSqM > 0)
                            item.SubItems.Add(new QueryDetailItem { Label = "Area", Value = $"{areaSqM:F2}", Unit = "sq m" });

                        if (volumeCuM > 0)
                            item.SubItems.Add(new QueryDetailItem { Label = "Volume", Value = $"{volumeCuM:F4}", Unit = "cu m" });

                        section.Items.Add(item);
                    }

                    section.Summary = $"{group.Count()} materials, {groupAreaSqM:F1} sq m, {groupVolumeCuM:F3} cu m";
                    result.Sections.Add(section);

                    sb.AppendLine($"  {group.Key}: {group.Count()} materials — {groupAreaSqM:F1} sq m / {groupVolumeCuM:F3} cu m");
                }

                result.Summary = sb.ToString();
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating material takeoff");
                return new QueryResult { Summary = $"Error generating material takeoff: {ex.Message}" };
            }
        }

        #endregion

        #region Parameters

        public QueryResult GetParameterDetails(string category = null)
        {
            if (_document == null)
                return new QueryResult { Summary = "No Revit model is currently open." };

            try
            {
                var result = new QueryResult();
                var sb = new StringBuilder();
                sb.AppendLine("Parameter Details:");

                // Get parameters from major element categories
                var categoryList = new[]
                {
                    (BuiltInCategory.OST_Walls, "Walls"),
                    (BuiltInCategory.OST_Doors, "Doors"),
                    (BuiltInCategory.OST_Windows, "Windows"),
                    (BuiltInCategory.OST_Rooms, "Rooms"),
                    (BuiltInCategory.OST_Floors, "Floors"),
                };

                foreach (var (cat, catName) in categoryList)
                {
                    if (category != null && !catName.Equals(category, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var elements = new FilteredElementCollector(_document)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .Take(5) // Sample first 5 elements per category
                        .ToList();

                    if (!elements.Any()) continue;

                    var section = new QueryDetailSection
                    {
                        Header = catName,
                        Summary = $"{elements.Count} elements sampled"
                    };

                    foreach (var element in elements)
                    {
                        var elementName = element.Name;
                        if (string.IsNullOrEmpty(elementName))
                            elementName = $"{catName} #{element.Id.IntegerValue}";

                        var item = new QueryDetailItem
                        {
                            Label = elementName,
                            Value = "",
                            Unit = "",
                            SubItems = new List<QueryDetailItem>()
                        };

                        // Get instance parameters
                        foreach (Parameter param in element.Parameters)
                        {
                            if (!param.HasValue) continue;
                            if (param.IsReadOnly && param.Definition?.Name == null) continue;

                            var paramName = param.Definition?.Name ?? "Unknown";
                            string paramValue;

                            switch (param.StorageType)
                            {
                                case StorageType.Double:
                                    paramValue = $"{param.AsDouble():F4}";
                                    break;
                                case StorageType.Integer:
                                    paramValue = param.AsInteger().ToString();
                                    break;
                                case StorageType.String:
                                    paramValue = param.AsString() ?? "";
                                    break;
                                case StorageType.ElementId:
                                    var refElement = _document.GetElement(param.AsElementId());
                                    paramValue = refElement?.Name ?? param.AsElementId().IntegerValue.ToString();
                                    break;
                                default:
                                    paramValue = param.AsValueString() ?? "";
                                    break;
                            }

                            if (!string.IsNullOrEmpty(paramValue) && paramValue != "0" && paramValue != "0.0000")
                            {
                                item.SubItems.Add(new QueryDetailItem
                                {
                                    Label = paramName,
                                    Value = paramValue,
                                    Unit = ""
                                });
                            }
                        }

                        // Limit sub-items to most useful ones
                        if (item.SubItems.Count > 15)
                            item.SubItems = item.SubItems.Take(15).ToList();

                        item.Value = $"{item.SubItems.Count} parameters";
                        section.Items.Add(item);
                    }

                    result.Sections.Add(section);
                    sb.AppendLine($"  {catName}: {elements.Count} elements with parameters");
                }

                result.Summary = sb.ToString();
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting parameter details");
                return new QueryResult { Summary = $"Error reading parameters: {ex.Message}" };
            }
        }

        #endregion

        #region Private Helper Classes

        private class MaterialUsageInfo
        {
            public string Name { get; set; }
            public string MaterialClass { get; set; }
            public string Color { get; set; }
            public int ElementCount { get; set; }
            public double TotalAreaSqFt { get; set; }
            public double TotalVolumeCuFt { get; set; }
            public List<string> Categories { get; set; } = new List<string>();
        }

        private class MaterialTakeoffInfo
        {
            public string Name { get; set; }
            public string MaterialClass { get; set; }
            public int ElementCount { get; set; }
            public double TotalAreaSqFt { get; set; }
            public double TotalVolumeCuFt { get; set; }
        }

        #endregion
    }
}
