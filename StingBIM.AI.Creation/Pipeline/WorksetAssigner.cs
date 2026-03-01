// StingBIM.AI.Creation.Pipeline.WorksetAssigner
// Auto-assigns every element to the correct discipline workset
// v4 Prompt Reference: Section C.1 Workset Auto-Assignment

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;

namespace StingBIM.AI.Creation.Pipeline
{
    /// <summary>
    /// Automatically assigns newly created elements to the correct discipline workset.
    /// Called after every element creation.
    /// </summary>
    public class WorksetAssigner
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly object _cacheLock = new object();
        private Dictionary<string, WorksetId> _worksetCache;

        // Category → Workset name mapping (from v4 spec Section C.1)
        private static readonly Dictionary<BuiltInCategory, string> CategoryWorksetMap =
            new Dictionary<BuiltInCategory, string>
            {
                // Architecture
                [BuiltInCategory.OST_Walls] = "Architecture",
                [BuiltInCategory.OST_Floors] = "Architecture",
                [BuiltInCategory.OST_Ceilings] = "Architecture",
                [BuiltInCategory.OST_Roofs] = "Architecture",
                [BuiltInCategory.OST_Doors] = "Architecture",
                [BuiltInCategory.OST_Windows] = "Architecture",
                [BuiltInCategory.OST_Stairs] = "Architecture",
                [BuiltInCategory.OST_StairsRailing] = "Architecture",
                [BuiltInCategory.OST_Ramps] = "Architecture",
                [BuiltInCategory.OST_Rooms] = "Architecture",
                [BuiltInCategory.OST_CurtainWallPanels] = "Architecture",
                [BuiltInCategory.OST_CurtainWallMullions] = "Architecture",

                // Structure
                [BuiltInCategory.OST_StructuralColumns] = "Structure",
                [BuiltInCategory.OST_StructuralFraming] = "Structure",
                [BuiltInCategory.OST_StructuralFoundation] = "Structure",
                [BuiltInCategory.OST_Grids] = "Shared Levels & Grids",
                [BuiltInCategory.OST_Levels] = "Shared Levels & Grids",

                // MEP - Electrical
                [BuiltInCategory.OST_LightingFixtures] = "MEP - Electrical",
                [BuiltInCategory.OST_ElectricalFixtures] = "MEP - Electrical",
                [BuiltInCategory.OST_ElectricalEquipment] = "MEP - Electrical",
                [BuiltInCategory.OST_Conduit] = "MEP - Electrical",
                [BuiltInCategory.OST_CableTray] = "MEP - Electrical",

                // MEP - Plumbing
                [BuiltInCategory.OST_PipeCurves] = "MEP - Plumbing",
                [BuiltInCategory.OST_PlumbingFixtures] = "MEP - Plumbing",
                [BuiltInCategory.OST_PipeAccessory] = "MEP - Plumbing",

                // MEP - HVAC
                [BuiltInCategory.OST_DuctCurves] = "MEP - HVAC",
                [BuiltInCategory.OST_MechanicalEquipment] = "MEP - HVAC",
                [BuiltInCategory.OST_DuctAccessory] = "MEP - HVAC",
                [BuiltInCategory.OST_DuctTerminal] = "MEP - HVAC",

                // MEP - Fire Protection
                [BuiltInCategory.OST_Sprinklers] = "MEP - Fire Protection",
                [BuiltInCategory.OST_FireAlarmDevices] = "MEP - Fire Protection",

                // Interior & Furniture
                [BuiltInCategory.OST_Furniture] = "Interior Finishes",
                [BuiltInCategory.OST_FurnitureSystems] = "Interior Finishes",
                [BuiltInCategory.OST_Casework] = "Interior Finishes",
                [BuiltInCategory.OST_SpecialityEquipment] = "Furniture & Equipment",

                // Site
                [BuiltInCategory.OST_Topography] = "Site & Landscape",
                [BuiltInCategory.OST_Planting] = "Site & Landscape",
                [BuiltInCategory.OST_Site] = "Site & Landscape",
                [BuiltInCategory.OST_Parking] = "Site & Landscape"
            };

        public WorksetAssigner(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>
        /// Assigns an element to the correct workset based on its category.
        /// Only operates if the document is workshared.
        /// </summary>
        public bool AssignToCorrectWorkset(Element element)
        {
            if (element == null) return false;
            if (!_document.IsWorkshared) return false;

            try
            {
                var category = element.Category;
                if (category == null) return false;

                var builtInCat = (BuiltInCategory)category.Id.IntegerValue;
                if (!CategoryWorksetMap.TryGetValue(builtInCat, out var worksetName))
                {
                    Logger.Debug($"No workset mapping for category {category.Name}");
                    return false;
                }

                var worksetId = GetOrCreateWorkset(worksetName);
                if (worksetId == WorksetId.InvalidWorksetId) return false;

                var worksetParam = element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                if (worksetParam != null && !worksetParam.IsReadOnly)
                {
                    worksetParam.Set(worksetId.IntegerValue);
                    Logger.Debug($"Assigned {element.Id} to workset '{worksetName}'");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to assign workset for element {element.Id}");
                return false;
            }
        }

        /// <summary>
        /// Assigns multiple elements to their correct worksets.
        /// Must be called within an active transaction.
        /// </summary>
        public int AssignBatch(IEnumerable<Element> elements)
        {
            int count = 0;
            foreach (var element in elements)
            {
                if (AssignToCorrectWorkset(element))
                    count++;
            }
            return count;
        }

        private WorksetId GetOrCreateWorkset(string worksetName)
        {
            lock (_cacheLock)
            {
                if (_worksetCache == null)
                {
                    _worksetCache = new Dictionary<string, WorksetId>(StringComparer.OrdinalIgnoreCase);
                    var worksets = new FilteredWorksetCollector(_document)
                        .OfKind(WorksetKind.UserWorkset)
                        .ToWorksets();
                    foreach (var ws in worksets)
                    {
                        _worksetCache[ws.Name] = ws.Id;
                    }
                }

                if (_worksetCache.TryGetValue(worksetName, out var id))
                    return id;

                // Workset doesn't exist yet — we don't create it here (that requires a transaction)
                // Fall back to "Architecture" or first available
                if (_worksetCache.TryGetValue("Architecture", out var archId))
                    return archId;

                return _worksetCache.Values.FirstOrDefault();
            }
        }

        /// <summary>
        /// Creates the default worksets for StingBIM projects.
        /// Must be called within an active transaction.
        /// </summary>
        public static void CreateDefaultWorksets(Document doc)
        {
            if (!doc.IsWorkshared) return;

            var defaultWorksets = new[]
            {
                "Shared Levels & Grids",
                "Architecture",
                "Structure",
                "MEP - Electrical",
                "MEP - Plumbing",
                "MEP - HVAC",
                "MEP - Fire Protection",
                "Interior Finishes",
                "Site & Landscape",
                "Furniture & Equipment"
            };

            var existing = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToWorksets()
                .Select(w => w.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var name in defaultWorksets)
            {
                if (!existing.Contains(name))
                {
                    Workset.Create(doc, name);
                    LogManager.GetCurrentClassLogger().Info($"Created workset: {name}");
                }
            }
        }
    }
}
