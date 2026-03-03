// ============================================================================
// StingBIM Revit - Model Query Utilities
// Static helper methods for querying Revit model data
// Used by ChatCommandRouter and ChatPanelControl
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace StingBIM.Revit.Commands
{
    /// <summary>
    /// Static utility class for common Revit model queries.
    /// Provides element counting, area calculation, and parameter inspection.
    /// </summary>
    internal static class RevitModelQuery
    {
        /// <summary>
        /// Counts element instances in a category.
        /// </summary>
        public static int CountInstances(Document doc, BuiltInCategory category)
        {
            if (doc == null) return 0;

            try
            {
                return new FilteredElementCollector(doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .GetElementCount();
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the total area (in m²) of elements in a category.
        /// </summary>
        public static double GetTotalArea(Document doc, BuiltInCategory category)
        {
            if (doc == null) return 0;

            try
            {
                var elements = new FilteredElementCollector(doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .ToElements();

                double totalAreaFt2 = 0;

                foreach (var elem in elements)
                {
                    var areaParam = elem.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (areaParam != null && areaParam.HasValue)
                    {
                        totalAreaFt2 += areaParam.AsDouble();
                    }
                }

                // Convert from ft² to m²
                return totalAreaFt2 * 0.092903;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the number of model warnings.
        /// </summary>
        public static int GetWarningCount(Document doc)
        {
            if (doc == null) return 0;

            try
            {
                return doc.GetWarnings()?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Counts elements in a category that have an empty or missing parameter.
        /// </summary>
        public static int CountEmptyParameter(
            Document doc,
            BuiltInCategory category,
            string parameterName)
        {
            if (doc == null) return 0;

            try
            {
                var elements = new FilteredElementCollector(doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .ToElements();

                int count = 0;

                foreach (var elem in elements)
                {
                    var param = elem.LookupParameter(parameterName);
                    if (param == null || !param.HasValue ||
                        (param.StorageType == StorageType.String &&
                         string.IsNullOrWhiteSpace(param.AsString())))
                    {
                        count++;
                    }
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets a summary of element counts by category.
        /// Returns a dictionary of category name → count.
        /// </summary>
        public static Dictionary<string, int> GetCategorySummary(Document doc)
        {
            var summary = new Dictionary<string, int>();

            if (doc == null) return summary;

            var categories = new (string Name, BuiltInCategory Category)[]
            {
                ("Walls", BuiltInCategory.OST_Walls),
                ("Floors", BuiltInCategory.OST_Floors),
                ("Roofs", BuiltInCategory.OST_Roofs),
                ("Ceilings", BuiltInCategory.OST_Ceilings),
                ("Doors", BuiltInCategory.OST_Doors),
                ("Windows", BuiltInCategory.OST_Windows),
                ("Rooms", BuiltInCategory.OST_Rooms),
                ("Columns", BuiltInCategory.OST_Columns),
                ("Structural Columns", BuiltInCategory.OST_StructuralColumns),
                ("Structural Framing", BuiltInCategory.OST_StructuralFraming),
                ("Stairs", BuiltInCategory.OST_Stairs),
                ("Furniture", BuiltInCategory.OST_Furniture),
                ("Plumbing", BuiltInCategory.OST_PlumbingFixtures),
                ("Mech Equipment", BuiltInCategory.OST_MechanicalEquipment),
                ("Elec Fixtures", BuiltInCategory.OST_ElectricalFixtures),
                ("Duct Curves", BuiltInCategory.OST_DuctCurves),
                ("Pipe Curves", BuiltInCategory.OST_PipeCurves),
            };

            foreach (var (name, category) in categories)
            {
                var count = CountInstances(doc, category);
                if (count > 0)
                {
                    summary[name] = count;
                }
            }

            return summary;
        }
    }
}
