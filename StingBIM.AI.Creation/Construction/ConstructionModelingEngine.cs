// ===================================================================================
// StingBIM Construction Modeling Engine
// Temporary works, scaffolding, site logistics, and construction phasing
// ===================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Construction
{
    /// <summary>
    /// Comprehensive construction modeling engine for generating temporary works,
    /// scaffolding systems, site logistics, safety zones, and construction sequences.
    /// Supports 4D BIM integration with time-based phasing.
    /// </summary>
    public class ConstructionModelingEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ScaffoldingDesigner _scaffoldingDesigner;
        private readonly TemporaryWorksDesigner _tempWorksDesigner;
        private readonly SiteLogisticsPlanner _siteLogisticsPlanner;
        private readonly SafetyZoneGenerator _safetyZoneGenerator;
        private readonly ConstructionSequencer _sequencer;
        private readonly ConstructionModelingSettings _settings;

        public ConstructionModelingEngine(ConstructionModelingSettings settings = null)
        {
            _settings = settings ?? new ConstructionModelingSettings();
            _scaffoldingDesigner = new ScaffoldingDesigner(_settings);
            _tempWorksDesigner = new TemporaryWorksDesigner(_settings);
            _siteLogisticsPlanner = new SiteLogisticsPlanner(_settings);
            _safetyZoneGenerator = new SafetyZoneGenerator(_settings);
            _sequencer = new ConstructionSequencer(_settings);

            Logger.Info("ConstructionModelingEngine initialized");
        }

        #region Main Methods

        /// <summary>
        /// Generate complete construction model from architectural/structural model
        /// </summary>
        public async Task<ConstructionModelResult> GenerateConstructionModelAsync(
            BuildingModel buildingModel,
            SiteModel siteModel,
            ConstructionOptions options = null,
            IProgress<ConstructionProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ConstructionModelResult
            {
                BuildingId = buildingModel.Id,
                GenerationStartTime = DateTime.Now
            };

            try
            {
                Logger.Info("Starting construction model generation for: {0}", buildingModel.Name);
                options ??= new ConstructionOptions();

                // Analyze building for construction requirements
                progress?.Report(new ConstructionProgress(5, "Analyzing building..."));
                var buildingAnalysis = await AnalyzeBuildingAsync(buildingModel, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // Generate construction sequence
                progress?.Report(new ConstructionProgress(15, "Creating construction sequence..."));
                var sequence = await _sequencer.GenerateSequenceAsync(
                    buildingModel,
                    buildingAnalysis,
                    options,
                    cancellationToken);
                result.ConstructionSequence = sequence;

                cancellationToken.ThrowIfCancellationRequested();

                // Design scaffolding systems
                progress?.Report(new ConstructionProgress(30, "Designing scaffolding..."));
                var scaffolding = await _scaffoldingDesigner.DesignScaffoldingAsync(
                    buildingModel,
                    sequence,
                    options,
                    cancellationToken);
                result.ScaffoldingSystems = scaffolding;

                cancellationToken.ThrowIfCancellationRequested();

                // Design temporary works
                progress?.Report(new ConstructionProgress(50, "Designing temporary works..."));
                var tempWorks = await _tempWorksDesigner.DesignTemporaryWorksAsync(
                    buildingModel,
                    sequence,
                    options,
                    cancellationToken);
                result.TemporaryWorks = tempWorks;

                cancellationToken.ThrowIfCancellationRequested();

                // Plan site logistics
                progress?.Report(new ConstructionProgress(70, "Planning site logistics..."));
                var logistics = await _siteLogisticsPlanner.PlanLogisticsAsync(
                    siteModel,
                    buildingModel,
                    sequence,
                    options,
                    cancellationToken);
                result.SiteLogistics = logistics;

                cancellationToken.ThrowIfCancellationRequested();

                // Generate safety zones
                progress?.Report(new ConstructionProgress(85, "Generating safety zones..."));
                var safetyZones = await _safetyZoneGenerator.GenerateZonesAsync(
                    siteModel,
                    buildingModel,
                    scaffolding,
                    logistics,
                    cancellationToken);
                result.SafetyZones = safetyZones;

                // Calculate statistics
                progress?.Report(new ConstructionProgress(95, "Finalizing..."));
                result.Statistics = CalculateStatistics(result);

                progress?.Report(new ConstructionProgress(100, "Generation complete"));
                result.Success = true;
                result.GenerationEndTime = DateTime.Now;

                Logger.Info("Construction model generated: {0} phases, {1} scaffolds, {2} temp works",
                    sequence.Phases.Count, scaffolding.Count, tempWorks.Count);
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Errors.Add("Generation cancelled");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Generation failed: {ex.Message}");
                Logger.Error(ex, "Construction model generation failed");
            }

            return result;
        }

        /// <summary>
        /// Generate scaffolding only for specific building faces
        /// </summary>
        public async Task<List<ScaffoldingSystem>> GenerateScaffoldingAsync(
            BuildingModel buildingModel,
            IEnumerable<BuildingFace> faces,
            ScaffoldingOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return await _scaffoldingDesigner.DesignForFacesAsync(
                buildingModel,
                faces,
                options ?? new ScaffoldingOptions(),
                cancellationToken);
        }

        /// <summary>
        /// Generate temporary works for specific construction phase
        /// </summary>
        public async Task<List<TemporaryWork>> GenerateTemporaryWorksAsync(
            BuildingModel buildingModel,
            ConstructionPhase phase,
            TemporaryWorksOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return await _tempWorksDesigner.DesignForPhaseAsync(
                buildingModel,
                phase,
                options ?? new TemporaryWorksOptions(),
                cancellationToken);
        }

        /// <summary>
        /// Analyze building model for construction requirements
        /// </summary>
        private async Task<BuildingAnalysis> AnalyzeBuildingAsync(
            BuildingModel model,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var analysis = new BuildingAnalysis
                {
                    TotalHeight = model.Levels.Max(l => l.Elevation + l.Height),
                    TotalFootprint = CalculateFootprint(model),
                    LevelCount = model.Levels.Count,
                    StructuralSystem = DetermineStructuralSystem(model),
                    FacadeType = DetermineFacadeType(model),
                    ComplexityScore = CalculateComplexity(model)
                };

                // Identify heavy lifts
                analysis.HeavyLifts = IdentifyHeavyLifts(model);

                // Identify critical paths
                analysis.CriticalPaths = IdentifyCriticalPaths(model);

                // Identify access requirements
                analysis.AccessRequirements = IdentifyAccessRequirements(model);

                return analysis;
            }, cancellationToken);
        }

        private double CalculateFootprint(BuildingModel model)
        {
            var groundLevel = model.Levels.FirstOrDefault(l => l.Elevation == 0);
            if (groundLevel == null) return 0;

            var floors = model.Elements.Where(e => e.ElementType == "Floor" && e.Level == groundLevel.Name);
            return floors.Sum(f => f.Area);
        }

        private StructuralSystem DetermineStructuralSystem(BuildingModel model)
        {
            var columns = model.Elements.Where(e => e.ElementType == "Column").ToList();
            var walls = model.Elements.Where(e => e.ElementType == "Wall" && e.IsStructural).ToList();

            if (columns.Count > walls.Count)
                return StructuralSystem.SteelFrame;
            if (walls.Count > 0)
                return StructuralSystem.ConcreteShearWall;

            return StructuralSystem.LoadBearingMasonry;
        }

        private FacadeType DetermineFacadeType(BuildingModel model)
        {
            var exteriorWalls = model.Elements.Where(e => e.ElementType == "Wall" && e.IsExterior).ToList();
            var curtainWalls = model.Elements.Where(e => e.ElementType == "CurtainWall").ToList();

            if (curtainWalls.Count > exteriorWalls.Count / 2)
                return FacadeType.CurtainWall;
            return FacadeType.Masonry;
        }

        private double CalculateComplexity(BuildingModel model)
        {
            // Score based on various factors
            double score = 0;
            score += model.Levels.Count * 0.1;
            score += model.Elements.Count * 0.01;
            score += model.Elements.Where(e => e.ElementType == "Column").Count() * 0.05;

            return Math.Min(10, score);
        }

        private List<HeavyLift> IdentifyHeavyLifts(BuildingModel model)
        {
            var heavyLifts = new List<HeavyLift>();

            // Find heavy elements (steel beams, precast, equipment)
            var heavyElements = model.Elements.Where(e =>
                e.Weight > 1000 || // Over 1 tonne
                e.ElementType == "SteelBeam" ||
                e.ElementType == "PrecastPanel" ||
                e.ElementType == "MechanicalEquipment");

            foreach (var element in heavyElements)
            {
                heavyLifts.Add(new HeavyLift
                {
                    ElementId = element.Id,
                    ElementType = element.ElementType,
                    Weight = element.Weight,
                    Dimensions = element.BoundingBox,
                    FinalPosition = element.Location,
                    Level = element.Level,
                    RequiredCraneCapacity = CalculateRequiredCraneCapacity(element)
                });
            }

            return heavyLifts;
        }

        private double CalculateRequiredCraneCapacity(BuildingElement element)
        {
            // Add safety factor and rigging weight
            return element.Weight * 1.25;
        }

        private List<CriticalPath> IdentifyCriticalPaths(BuildingModel model)
        {
            var paths = new List<CriticalPath>();

            // Core construction is typically critical
            var cores = model.Elements.Where(e => e.Zone == "Core").ToList();
            if (cores.Any())
            {
                paths.Add(new CriticalPath
                {
                    Name = "Core Construction",
                    Elements = cores.Select(e => e.Id).ToList(),
                    Priority = 1
                });
            }

            // Structural frame is critical
            var structure = model.Elements.Where(e => e.IsStructural).ToList();
            paths.Add(new CriticalPath
            {
                Name = "Structural Frame",
                Elements = structure.Select(e => e.Id).ToList(),
                Priority = 2
            });

            return paths;
        }

        private List<AccessRequirement> IdentifyAccessRequirements(BuildingModel model)
        {
            var requirements = new List<AccessRequirement>();

            foreach (var level in model.Levels)
            {
                requirements.Add(new AccessRequirement
                {
                    Level = level.Name,
                    Elevation = level.Elevation,
                    RequiredAccess = level.Elevation > 0 ? AccessType.Scaffolding : AccessType.Ground,
                    MinimumClearance = 2000
                });
            }

            return requirements;
        }

        private ConstructionStatistics CalculateStatistics(ConstructionModelResult result)
        {
            return new ConstructionStatistics
            {
                TotalPhases = result.ConstructionSequence?.Phases.Count ?? 0,
                TotalScaffoldingSystems = result.ScaffoldingSystems?.Count ?? 0,
                TotalTemporaryWorks = result.TemporaryWorks?.Count ?? 0,
                TotalSafetyZones = result.SafetyZones?.Count ?? 0,
                EstimatedScaffoldingArea = result.ScaffoldingSystems?.Sum(s => s.TotalArea) ?? 0,
                EstimatedFormworkArea = result.TemporaryWorks?.Where(t => t.Type == TemporaryWorkType.Formwork).Sum(t => t.Area) ?? 0,
                CraneLocations = result.SiteLogistics?.CranePositions.Count ?? 0,
                MaterialStorageAreas = result.SiteLogistics?.StorageAreas.Count ?? 0
            };
        }

        #endregion
    }

    #region Scaffolding Designer

    /// <summary>
    /// Designs scaffolding systems for building construction
    /// </summary>
    internal class ScaffoldingDesigner
    {
        private readonly ConstructionModelingSettings _settings;

        public ScaffoldingDesigner(ConstructionModelingSettings settings)
        {
            _settings = settings;
        }

        public async Task<List<ScaffoldingSystem>> DesignScaffoldingAsync(
            BuildingModel buildingModel,
            ConstructionSequence sequence,
            ConstructionOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var systems = new List<ScaffoldingSystem>();

                // Get building faces requiring scaffolding
                var faces = GetBuildingFaces(buildingModel);

                foreach (var face in faces)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var system = DesignScaffoldForFace(face, buildingModel, options);
                    if (system != null)
                    {
                        // Assign to construction phases
                        AssignToPhases(system, sequence);
                        systems.Add(system);
                    }
                }

                // Add internal scaffolding for high spaces
                var internalScaffolds = DesignInternalScaffolding(buildingModel, options);
                systems.AddRange(internalScaffolds);

                return systems;
            }, cancellationToken);
        }

        public async Task<List<ScaffoldingSystem>> DesignForFacesAsync(
            BuildingModel buildingModel,
            IEnumerable<BuildingFace> faces,
            ScaffoldingOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var systems = new List<ScaffoldingSystem>();

                foreach (var face in faces)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var system = DesignScaffoldForFace(face, buildingModel, new ConstructionOptions
                    {
                        ScaffoldingType = options.PreferredType,
                        MaxScaffoldHeight = options.MaxHeight
                    });

                    if (system != null)
                        systems.Add(system);
                }

                return systems;
            }, cancellationToken);
        }

        private List<BuildingFace> GetBuildingFaces(BuildingModel model)
        {
            var faces = new List<BuildingFace>();

            // Get exterior walls and create face definitions
            var exteriorWalls = model.Elements.Where(e => e.ElementType == "Wall" && e.IsExterior);

            // Group walls by orientation
            var orientations = new Dictionary<string, List<BuildingElement>>();

            foreach (var wall in exteriorWalls)
            {
                var orientation = GetWallOrientation(wall);
                if (!orientations.ContainsKey(orientation))
                    orientations[orientation] = new List<BuildingElement>();
                orientations[orientation].Add(wall);
            }

            foreach (var kvp in orientations)
            {
                var faceWalls = kvp.Value;
                var bbox = CalculateBoundingBox(faceWalls);

                faces.Add(new BuildingFace
                {
                    Id = $"FACE_{kvp.Key}",
                    Orientation = kvp.Key,
                    Width = bbox.Width,
                    Height = bbox.Height,
                    BaseElevation = bbox.MinZ,
                    Walls = faceWalls.Select(w => w.Id).ToList()
                });
            }

            return faces;
        }

        private string GetWallOrientation(BuildingElement wall)
        {
            var dx = wall.EndPoint.X - wall.StartPoint.X;
            var dy = wall.EndPoint.Y - wall.StartPoint.Y;

            if (Math.Abs(dx) > Math.Abs(dy))
                return dy > 0 ? "South" : "North";
            return dx > 0 ? "East" : "West";
        }

        private (double Width, double Height, double MinZ) CalculateBoundingBox(List<BuildingElement> elements)
        {
            var minX = elements.Min(e => Math.Min(e.StartPoint.X, e.EndPoint.X));
            var maxX = elements.Max(e => Math.Max(e.StartPoint.X, e.EndPoint.X));
            var minY = elements.Min(e => Math.Min(e.StartPoint.Y, e.EndPoint.Y));
            var maxY = elements.Max(e => Math.Max(e.StartPoint.Y, e.EndPoint.Y));
            var minZ = elements.Min(e => e.BaseLevel);
            var maxZ = elements.Max(e => e.TopLevel);

            return (Math.Max(maxX - minX, maxY - minY), maxZ - minZ, minZ);
        }

        private ScaffoldingSystem DesignScaffoldForFace(
            BuildingFace face,
            BuildingModel model,
            ConstructionOptions options)
        {
            // Determine appropriate scaffold type
            var scaffoldType = DetermineScaffoldType(face, options);

            var system = new ScaffoldingSystem
            {
                Id = $"SCAF_{face.Id}",
                Type = scaffoldType,
                FaceId = face.Id,
                Orientation = face.Orientation,
                BaseElevation = face.BaseElevation,
                TotalHeight = face.Height + _settings.ScaffoldOverheadHeight,
                TotalWidth = face.Width + _settings.ScaffoldSideExtension * 2
            };

            // Calculate bay layout
            system.Bays = CalculateBays(system, face);

            // Calculate lift levels
            system.Lifts = CalculateLifts(system);

            // Calculate ties to building
            system.Ties = CalculateTies(system, model);

            // Calculate base plates/foundations
            system.Foundations = CalculateFoundations(system);

            // Calculate area and components
            system.TotalArea = system.TotalWidth * system.TotalHeight;
            system.ComponentCount = CalculateComponentCount(system);

            return system;
        }

        private ScaffoldType DetermineScaffoldType(BuildingFace face, ConstructionOptions options)
        {
            if (options.ScaffoldingType != ScaffoldType.None)
                return options.ScaffoldingType;

            // Height-based selection
            if (face.Height > 50000)
                return ScaffoldType.SystemScaffold;
            if (face.Height > 30000)
                return ScaffoldType.CuplockSystem;
            if (face.Height > 15000)
                return ScaffoldType.TubeAndFitting;

            return ScaffoldType.MobileAccessTower;
        }

        private List<ScaffoldBay> CalculateBays(ScaffoldingSystem system, BuildingFace face)
        {
            var bays = new List<ScaffoldBay>();
            var bayWidth = _settings.StandardBayWidth;
            var bayCount = (int)Math.Ceiling(system.TotalWidth / bayWidth);

            for (int i = 0; i < bayCount; i++)
            {
                bays.Add(new ScaffoldBay
                {
                    Index = i,
                    Width = i == bayCount - 1 ?
                        system.TotalWidth - (bayCount - 1) * bayWidth : bayWidth,
                    StartOffset = i * bayWidth
                });
            }

            return bays;
        }

        private List<ScaffoldLift> CalculateLifts(ScaffoldingSystem system)
        {
            var lifts = new List<ScaffoldLift>();
            var liftHeight = _settings.StandardLiftHeight;
            var liftCount = (int)Math.Ceiling(system.TotalHeight / liftHeight);

            for (int i = 0; i < liftCount; i++)
            {
                lifts.Add(new ScaffoldLift
                {
                    Index = i,
                    Height = i == liftCount - 1 ?
                        system.TotalHeight - (liftCount - 1) * liftHeight : liftHeight,
                    BaseElevation = system.BaseElevation + i * liftHeight,
                    HasBoarding = true,
                    HasGuardRails = true
                });
            }

            return lifts;
        }

        private List<ScaffoldTie> CalculateTies(ScaffoldingSystem system, BuildingModel model)
        {
            var ties = new List<ScaffoldTie>();

            // Ties every 2 lifts vertically and every 2 bays horizontally
            var verticalSpacing = _settings.TieVerticalSpacing;
            var horizontalSpacing = _settings.TieHorizontalSpacing;

            var tieElevations = Enumerable.Range(1, (int)(system.TotalHeight / verticalSpacing))
                .Select(i => system.BaseElevation + i * verticalSpacing)
                .ToList();

            var tieOffsets = Enumerable.Range(0, (int)(system.TotalWidth / horizontalSpacing) + 1)
                .Select(i => i * horizontalSpacing)
                .ToList();

            foreach (var elevation in tieElevations)
            {
                foreach (var offset in tieOffsets)
                {
                    ties.Add(new ScaffoldTie
                    {
                        Elevation = elevation,
                        HorizontalOffset = offset,
                        TieType = TieType.ThroughTie,
                        LoadCapacity = _settings.StandardTieCapacity
                    });
                }
            }

            return ties;
        }

        private List<ScaffoldFoundation> CalculateFoundations(ScaffoldingSystem system)
        {
            var foundations = new List<ScaffoldFoundation>();

            foreach (var bay in system.Bays)
            {
                // Base plate at each standard position
                foundations.Add(new ScaffoldFoundation
                {
                    Location = new Point3D(bay.StartOffset, 0, system.BaseElevation),
                    Type = system.BaseElevation > 0 ? FoundationType.SuspendedBracket : FoundationType.BasePlate,
                    LoadCapacity = CalculateStandardLoad(system)
                });

                foundations.Add(new ScaffoldFoundation
                {
                    Location = new Point3D(bay.StartOffset + bay.Width, 0, system.BaseElevation),
                    Type = system.BaseElevation > 0 ? FoundationType.SuspendedBracket : FoundationType.BasePlate,
                    LoadCapacity = CalculateStandardLoad(system)
                });
            }

            return foundations;
        }

        private double CalculateStandardLoad(ScaffoldingSystem system)
        {
            // Calculate load per standard based on height
            var deadLoad = system.TotalHeight * _settings.ScaffoldSelfWeight;
            var liveLoad = _settings.ScaffoldLiveLoad * (system.TotalWidth / system.Bays.Count);
            return deadLoad + liveLoad;
        }

        private int CalculateComponentCount(ScaffoldingSystem system)
        {
            // Estimate component count
            int standards = system.Bays.Count * 2 * system.Lifts.Count;
            int ledgers = system.Bays.Count * system.Lifts.Count;
            int transoms = ledgers;
            int boards = system.Bays.Count * system.Lifts.Count * 4;

            return standards + ledgers + transoms + boards;
        }

        private List<ScaffoldingSystem> DesignInternalScaffolding(BuildingModel model, ConstructionOptions options)
        {
            var internalScaffolds = new List<ScaffoldingSystem>();

            // Find high internal spaces (atriums, lobbies, etc.)
            var highSpaces = model.Elements
                .Where(e => e.ElementType == "Room" && e.Height > _settings.InternalScaffoldThreshold)
                .ToList();

            foreach (var space in highSpaces)
            {
                internalScaffolds.Add(new ScaffoldingSystem
                {
                    Id = $"SCAF_INT_{space.Id}",
                    Type = ScaffoldType.MobileAccessTower,
                    IsInternal = true,
                    TotalHeight = space.Height,
                    TotalWidth = Math.Min(space.Width, 3000),
                    TotalArea = space.Area,
                    Notes = $"Internal scaffold for {space.Name}"
                });
            }

            return internalScaffolds;
        }

        private void AssignToPhases(ScaffoldingSystem system, ConstructionSequence sequence)
        {
            // Find relevant phases for this scaffold location
            var relevantPhases = sequence.Phases
                .Where(p => p.Type == PhaseType.Structure ||
                           p.Type == PhaseType.Envelope ||
                           p.Type == PhaseType.Finishes)
                .ToList();

            if (relevantPhases.Any())
            {
                system.ErectionPhase = relevantPhases.First().Id;
                system.StrikePhase = relevantPhases.Last().Id;
            }
        }
    }

    #endregion

    #region Temporary Works Designer

    /// <summary>
    /// Designs temporary works including formwork, shoring, and bracing
    /// </summary>
    internal class TemporaryWorksDesigner
    {
        private readonly ConstructionModelingSettings _settings;

        public TemporaryWorksDesigner(ConstructionModelingSettings settings)
        {
            _settings = settings;
        }

        public async Task<List<TemporaryWork>> DesignTemporaryWorksAsync(
            BuildingModel buildingModel,
            ConstructionSequence sequence,
            ConstructionOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var works = new List<TemporaryWork>();

                // Design formwork for concrete elements
                var formwork = DesignFormwork(buildingModel, sequence, options);
                works.AddRange(formwork);

                // Design shoring for suspended slabs
                var shoring = DesignShoring(buildingModel, sequence, options);
                works.AddRange(shoring);

                // Design bracing for stability
                var bracing = DesignBracing(buildingModel, sequence, options);
                works.AddRange(bracing);

                // Design protection systems
                var protection = DesignProtection(buildingModel, options);
                works.AddRange(protection);

                // Design dewatering if needed
                if (HasBasement(buildingModel))
                {
                    var dewatering = DesignDewatering(buildingModel, options);
                    works.AddRange(dewatering);
                }

                return works;
            }, cancellationToken);
        }

        public async Task<List<TemporaryWork>> DesignForPhaseAsync(
            BuildingModel buildingModel,
            ConstructionPhase phase,
            TemporaryWorksOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var works = new List<TemporaryWork>();

                // Filter elements for this phase
                var phaseElements = buildingModel.Elements
                    .Where(e => e.ConstructionPhase == phase.Id)
                    .ToList();

                // Design formwork for phase elements
                foreach (var element in phaseElements.Where(e => RequiresFormwork(e)))
                {
                    works.Add(DesignElementFormwork(element, options));
                }

                // Design shoring for phase
                var slabs = phaseElements.Where(e => e.ElementType == "Floor" || e.ElementType == "Slab");
                foreach (var slab in slabs)
                {
                    works.Add(DesignSlabShoring(slab, options));
                }

                return works;
            }, cancellationToken);
        }

        private List<TemporaryWork> DesignFormwork(
            BuildingModel model,
            ConstructionSequence sequence,
            ConstructionOptions options)
        {
            var formwork = new List<TemporaryWork>();

            // Get concrete elements
            var concreteElements = model.Elements
                .Where(e => e.Material?.Contains("Concrete") == true)
                .ToList();

            foreach (var element in concreteElements)
            {
                if (!RequiresFormwork(element)) continue;

                var fw = new TemporaryWork
                {
                    Id = $"FW_{element.Id}",
                    Type = TemporaryWorkType.Formwork,
                    TargetElementId = element.Id,
                    Description = $"Formwork for {element.ElementType} {element.Name}",
                    Area = CalculateFormworkArea(element),
                    System = DetermineFormworkSystem(element, options)
                };

                // Assign to phase
                fw.ConstructionPhase = element.ConstructionPhase;

                // Calculate striking time
                fw.StrikingTime = CalculateStrikingTime(element);

                formwork.Add(fw);
            }

            return formwork;
        }

        private bool RequiresFormwork(BuildingElement element)
        {
            return element.Material?.Contains("Concrete") == true &&
                   (element.ElementType == "Column" ||
                    element.ElementType == "Beam" ||
                    element.ElementType == "Wall" ||
                    element.ElementType == "Floor" ||
                    element.ElementType == "Slab");
        }

        private double CalculateFormworkArea(BuildingElement element)
        {
            return element.ElementType switch
            {
                "Column" => element.Perimeter * element.Height,
                "Beam" => (element.Width + element.Height * 2) * element.Length / 1000,
                "Wall" => element.Area * 2,
                "Floor" or "Slab" => element.Area * 1.1, // Include edge formwork
                _ => 0
            };
        }

        private FormworkSystem DetermineFormworkSystem(BuildingElement element, ConstructionOptions options)
        {
            return element.ElementType switch
            {
                "Column" => FormworkSystem.ColumnForm,
                "Beam" => FormworkSystem.BeamForm,
                "Wall" => element.Height > 3000 ? FormworkSystem.ClimbingForm : FormworkSystem.WallPanel,
                "Floor" or "Slab" => FormworkSystem.TableForm,
                _ => FormworkSystem.Traditional
            };
        }

        private TimeSpan CalculateStrikingTime(BuildingElement element)
        {
            // Based on element type and concrete strength gain
            return element.ElementType switch
            {
                "Column" => TimeSpan.FromDays(1),
                "Beam" => TimeSpan.FromDays(7),
                "Wall" => TimeSpan.FromDays(2),
                "Floor" or "Slab" => TimeSpan.FromDays(14),
                _ => TimeSpan.FromDays(7)
            };
        }

        private TemporaryWork DesignElementFormwork(BuildingElement element, TemporaryWorksOptions options)
        {
            return new TemporaryWork
            {
                Id = $"FW_{element.Id}",
                Type = TemporaryWorkType.Formwork,
                TargetElementId = element.Id,
                Area = CalculateFormworkArea(element),
                System = DetermineFormworkSystem(element, new ConstructionOptions())
            };
        }

        private List<TemporaryWork> DesignShoring(
            BuildingModel model,
            ConstructionSequence sequence,
            ConstructionOptions options)
        {
            var shoring = new List<TemporaryWork>();

            // Get suspended slabs
            var slabs = model.Elements
                .Where(e => (e.ElementType == "Floor" || e.ElementType == "Slab") && e.BaseLevel > 0)
                .ToList();

            foreach (var slab in slabs)
            {
                shoring.Add(DesignSlabShoring(slab, new TemporaryWorksOptions()));
            }

            return shoring;
        }

        private TemporaryWork DesignSlabShoring(BuildingElement slab, TemporaryWorksOptions options)
        {
            var shoringHeight = slab.BaseLevel;
            var shoringType = DetermineShoringType(shoringHeight, slab.Area);

            return new TemporaryWork
            {
                Id = $"SH_{slab.Id}",
                Type = TemporaryWorkType.Shoring,
                TargetElementId = slab.Id,
                Description = $"Shoring for {slab.Name}",
                Height = shoringHeight,
                Area = slab.Area,
                ShoringSystem = shoringType,
                LoadCapacity = CalculateShoringLoad(slab),
                PropCount = CalculatePropCount(slab, shoringType)
            };
        }

        private ShoringType DetermineShoringType(double height, double area)
        {
            if (height > 6000)
                return ShoringType.FalseworkTower;
            if (area > 100)
                return ShoringType.AluminiumBeam;
            return ShoringType.AdjustableProp;
        }

        private double CalculateShoringLoad(BuildingElement slab)
        {
            var deadLoad = slab.Area * slab.Thickness / 1000 * 25; // kN
            var constructionLoad = slab.Area * 2.5; // kN/m² construction load
            return deadLoad + constructionLoad;
        }

        private int CalculatePropCount(BuildingElement slab, ShoringType type)
        {
            var spacing = type == ShoringType.AdjustableProp ? 1.5 : 2.5; // meters
            return (int)Math.Ceiling(slab.Area / (spacing * spacing));
        }

        private List<TemporaryWork> DesignBracing(
            BuildingModel model,
            ConstructionSequence sequence,
            ConstructionOptions options)
        {
            var bracing = new List<TemporaryWork>();

            // Add temporary bracing for steel structures during erection
            var steelFrames = model.Elements
                .Where(e => e.ElementType == "Column" && e.Material?.Contains("Steel") == true)
                .ToList();

            if (steelFrames.Any())
            {
                bracing.Add(new TemporaryWork
                {
                    Id = "BR_ERECTION",
                    Type = TemporaryWorkType.Bracing,
                    Description = "Temporary erection bracing for steel frame",
                    ConstructionPhase = sequence.Phases.FirstOrDefault(p => p.Type == PhaseType.Structure)?.Id
                });
            }

            return bracing;
        }

        private List<TemporaryWork> DesignProtection(BuildingModel model, ConstructionOptions options)
        {
            var protection = new List<TemporaryWork>();

            // Edge protection
            var edges = IdentifyEdges(model);
            foreach (var edge in edges)
            {
                protection.Add(new TemporaryWork
                {
                    Id = $"EP_{edge.Id}",
                    Type = TemporaryWorkType.EdgeProtection,
                    Description = $"Edge protection at {edge.Location}",
                    Length = edge.Length
                });
            }

            // Penetration protection
            var penetrations = model.Elements.Where(e => e.ElementType == "Opening" && e.Width > 500);
            foreach (var pen in penetrations)
            {
                protection.Add(new TemporaryWork
                {
                    Id = $"PP_{pen.Id}",
                    Type = TemporaryWorkType.PenetrationCover,
                    TargetElementId = pen.Id,
                    Area = pen.Area
                });
            }

            return protection;
        }

        private List<EdgeDefinition> IdentifyEdges(BuildingModel model)
        {
            var edges = new List<EdgeDefinition>();

            foreach (var level in model.Levels)
            {
                var floorElements = model.Elements
                    .Where(e => e.Level == level.Name && e.ElementType == "Floor")
                    .ToList();

                // Identify slab edges at each level
                // Simplified: create edge at building perimeter
                if (floorElements.Any())
                {
                    var perimeter = CalculatePerimeter(floorElements);
                    edges.Add(new EdgeDefinition
                    {
                        Id = $"EDGE_{level.Name}",
                        Level = level.Name,
                        Location = level.Name,
                        Length = perimeter
                    });
                }
            }

            return edges;
        }

        private double CalculatePerimeter(List<BuildingElement> floors)
        {
            // Simplified perimeter calculation
            var totalArea = floors.Sum(f => f.Area);
            return Math.Sqrt(totalArea) * 4; // Approximate square building
        }

        private List<TemporaryWork> DesignDewatering(BuildingModel model, ConstructionOptions options)
        {
            var dewatering = new List<TemporaryWork>();

            var basementLevels = model.Levels.Where(l => l.Elevation < 0).ToList();

            if (basementLevels.Any())
            {
                var deepestLevel = basementLevels.Min(l => l.Elevation);

                dewatering.Add(new TemporaryWork
                {
                    Id = "DW_SYSTEM",
                    Type = TemporaryWorkType.Dewatering,
                    Description = "Dewatering system for basement construction",
                    Depth = Math.Abs(deepestLevel),
                    PumpCapacity = CalculateRequiredPumpCapacity(model, deepestLevel)
                });
            }

            return dewatering;
        }

        private double CalculateRequiredPumpCapacity(BuildingModel model, double depth)
        {
            // Simplified calculation based on excavation size and soil type
            var footprint = model.Levels.First(l => l.Elevation == 0).Area;
            var perimeter = Math.Sqrt(footprint) * 4;
            var infiltration = perimeter * Math.Abs(depth) * 0.1; // L/min per m² estimate

            return infiltration * 1.5; // Safety factor
        }

        private bool HasBasement(BuildingModel model)
        {
            return model.Levels.Any(l => l.Elevation < 0);
        }
    }

    #endregion

    #region Site Logistics Planner

    /// <summary>
    /// Plans site logistics including crane placement, storage, and access
    /// </summary>
    internal class SiteLogisticsPlanner
    {
        private readonly ConstructionModelingSettings _settings;

        public SiteLogisticsPlanner(ConstructionModelingSettings settings)
        {
            _settings = settings;
        }

        public async Task<SiteLogistics> PlanLogisticsAsync(
            SiteModel siteModel,
            BuildingModel buildingModel,
            ConstructionSequence sequence,
            ConstructionOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var logistics = new SiteLogistics
                {
                    SiteId = siteModel.Id
                };

                // Plan crane positions
                logistics.CranePositions = PlanCranePositions(siteModel, buildingModel, options);

                // Plan material storage areas
                logistics.StorageAreas = PlanStorageAreas(siteModel, buildingModel, sequence);

                // Plan access routes
                logistics.AccessRoutes = PlanAccessRoutes(siteModel, logistics.CranePositions, logistics.StorageAreas);

                // Plan welfare facilities
                logistics.WelfareFacilities = PlanWelfareFacilities(siteModel, options);

                // Plan temporary services
                logistics.TemporaryServices = PlanTemporaryServices(siteModel, buildingModel);

                // Plan traffic management
                logistics.TrafficManagement = PlanTrafficManagement(siteModel, logistics);

                return logistics;
            }, cancellationToken);
        }

        private List<CranePosition> PlanCranePositions(
            SiteModel site,
            BuildingModel building,
            ConstructionOptions options)
        {
            var positions = new List<CranePosition>();

            // Calculate required crane coverage
            var buildingBounds = GetBuildingBounds(building);
            var maxRadius = Math.Max(buildingBounds.Width, buildingBounds.Depth) / 2 + _settings.CraneOverreach;
            var buildingHeight = building.Levels.Max(l => l.Elevation + l.Height);

            // Determine crane type
            var craneType = DetermineCraneType(buildingHeight, maxRadius, options);

            // Calculate optimal positions
            var optimalPositions = CalculateOptimalCranePositions(
                buildingBounds,
                site.Boundary,
                craneType,
                options);

            int craneId = 1;
            foreach (var pos in optimalPositions)
            {
                positions.Add(new CranePosition
                {
                    Id = $"CRANE_{craneId++}",
                    Location = pos,
                    Type = craneType,
                    MaxRadius = GetCraneRadius(craneType),
                    MaxCapacity = GetCraneCapacity(craneType),
                    RequiredClearance = GetCraneClearance(craneType),
                    FoundationType = buildingHeight > 50000 ? CraneFoundationType.Climbing : CraneFoundationType.Static
                });
            }

            return positions;
        }

        private BoundingBox3D GetBuildingBounds(BuildingModel building)
        {
            var elements = building.Elements.Where(e => e.StartPoint != null).ToList();

            return new BoundingBox3D
            {
                MinX = elements.Min(e => Math.Min(e.StartPoint.X, e.EndPoint?.X ?? e.StartPoint.X)),
                MaxX = elements.Max(e => Math.Max(e.StartPoint.X, e.EndPoint?.X ?? e.StartPoint.X)),
                MinY = elements.Min(e => Math.Min(e.StartPoint.Y, e.EndPoint?.Y ?? e.StartPoint.Y)),
                MaxY = elements.Max(e => Math.Max(e.StartPoint.Y, e.EndPoint?.Y ?? e.StartPoint.Y)),
                MinZ = building.Levels.Min(l => l.Elevation),
                MaxZ = building.Levels.Max(l => l.Elevation + l.Height)
            };
        }

        private CraneType DetermineCraneType(double height, double radius, ConstructionOptions options)
        {
            if (options.PreferredCraneType != CraneType.None)
                return options.PreferredCraneType;

            if (height > 100000) // >100m
                return CraneType.LufferTower;
            if (height > 50000) // >50m
                return CraneType.TowerCrane;
            if (radius > 30000) // >30m reach needed
                return CraneType.TowerCrane;

            return CraneType.MobileCrane;
        }

        private List<Point3D> CalculateOptimalCranePositions(
            BoundingBox3D buildingBounds,
            List<Point3D> siteBoundary,
            CraneType craneType,
            ConstructionOptions options)
        {
            var positions = new List<Point3D>();
            var craneRadius = GetCraneRadius(craneType);

            // For tower cranes, position at corners with maximum coverage
            if (craneType == CraneType.TowerCrane || craneType == CraneType.LufferTower)
            {
                var buildingCenter = new Point3D(
                    (buildingBounds.MinX + buildingBounds.MaxX) / 2,
                    (buildingBounds.MinY + buildingBounds.MaxY) / 2,
                    0);

                // Check if one crane can cover entire building
                if (craneRadius > Math.Max(buildingBounds.Width, buildingBounds.Depth) / 2 + _settings.CraneMinClearance)
                {
                    // Single crane position - offset from building
                    positions.Add(new Point3D(
                        buildingBounds.MaxX + _settings.CraneMinClearance,
                        buildingCenter.Y,
                        0));
                }
                else
                {
                    // Multiple cranes needed - position at opposite corners
                    positions.Add(new Point3D(
                        buildingBounds.MaxX + _settings.CraneMinClearance,
                        buildingBounds.MaxY + _settings.CraneMinClearance,
                        0));

                    positions.Add(new Point3D(
                        buildingBounds.MinX - _settings.CraneMinClearance,
                        buildingBounds.MinY - _settings.CraneMinClearance,
                        0));
                }
            }
            else
            {
                // Mobile crane - plan for access from site entrance
                var entrancePoint = siteBoundary.FirstOrDefault() ?? new Point3D(buildingBounds.MaxX + 10000, 0, 0);
                positions.Add(entrancePoint);
            }

            return positions;
        }

        private double GetCraneRadius(CraneType type)
        {
            return type switch
            {
                CraneType.TowerCrane => 60000, // 60m
                CraneType.LufferTower => 50000, // 50m
                CraneType.MobileCrane => 40000, // 40m
                _ => 30000
            };
        }

        private double GetCraneCapacity(CraneType type)
        {
            return type switch
            {
                CraneType.TowerCrane => 12000, // 12 tonnes at tip
                CraneType.LufferTower => 8000,
                CraneType.MobileCrane => 50000, // 50 tonnes close
                _ => 5000
            };
        }

        private double GetCraneClearance(CraneType type)
        {
            return type switch
            {
                CraneType.TowerCrane => 3000,
                CraneType.MobileCrane => 5000,
                _ => 3000
            };
        }

        private List<StorageArea> PlanStorageAreas(
            SiteModel site,
            BuildingModel building,
            ConstructionSequence sequence)
        {
            var areas = new List<StorageArea>();

            var buildingBounds = GetBuildingBounds(building);
            var availableArea = CalculateAvailableArea(site.Boundary, buildingBounds);

            // Steel storage
            areas.Add(new StorageArea
            {
                Id = "STORE_STEEL",
                MaterialType = MaterialType.Steel,
                RequiredArea = building.Elements.Count(e => e.Material?.Contains("Steel") == true) * 10, // 10m² per element
                Location = FindStorageLocation(availableArea, "steel"),
                RequiresHardstanding = true
            });

            // Concrete / Rebar storage
            areas.Add(new StorageArea
            {
                Id = "STORE_REBAR",
                MaterialType = MaterialType.Rebar,
                RequiredArea = 200, // Standard rebar storage
                Location = FindStorageLocation(availableArea, "rebar"),
                RequiresHardstanding = true
            });

            // General materials
            areas.Add(new StorageArea
            {
                Id = "STORE_GENERAL",
                MaterialType = MaterialType.General,
                RequiredArea = 500,
                Location = FindStorageLocation(availableArea, "general"),
                RequiresHardstanding = false
            });

            // Waste/skip area
            areas.Add(new StorageArea
            {
                Id = "STORE_WASTE",
                MaterialType = MaterialType.Waste,
                RequiredArea = 100,
                Location = FindStorageLocation(availableArea, "waste"),
                RequiresHardstanding = true,
                Notes = "Position near site exit for skip collection"
            });

            return areas;
        }

        private double CalculateAvailableArea(List<Point3D> siteBoundary, BoundingBox3D buildingBounds)
        {
            // Simplified: calculate site area minus building footprint
            var siteArea = CalculatePolygonArea(siteBoundary);
            var buildingArea = buildingBounds.Width * buildingBounds.Depth / 1000000;
            return siteArea - buildingArea;
        }

        private double CalculatePolygonArea(List<Point3D> polygon)
        {
            if (polygon.Count < 3) return 0;

            double area = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                int j = (i + 1) % polygon.Count;
                area += polygon[i].X * polygon[j].Y;
                area -= polygon[j].X * polygon[i].Y;
            }

            return Math.Abs(area / 2) / 1000000; // Convert to m²
        }

        private Point3D FindStorageLocation(double availableArea, string type)
        {
            // Simplified: return offset positions
            return type switch
            {
                "steel" => new Point3D(10000, 5000, 0),
                "rebar" => new Point3D(10000, 15000, 0),
                "general" => new Point3D(10000, 25000, 0),
                "waste" => new Point3D(0, 30000, 0),
                _ => new Point3D(10000, 10000, 0)
            };
        }

        private List<AccessRoute> PlanAccessRoutes(
            SiteModel site,
            List<CranePosition> cranes,
            List<StorageArea> storage)
        {
            var routes = new List<AccessRoute>();

            // Main site access
            routes.Add(new AccessRoute
            {
                Id = "ROUTE_MAIN",
                Type = RouteType.VehicleAccess,
                Width = _settings.MainAccessWidth,
                StartPoint = site.Entrance,
                EndPoint = storage.First().Location,
                SurfaceType = SurfaceType.Hardstanding,
                MaxWeight = 44000 // 44 tonne trucks
            });

            // Crane access routes
            foreach (var crane in cranes)
            {
                routes.Add(new AccessRoute
                {
                    Id = $"ROUTE_CRANE_{crane.Id}",
                    Type = RouteType.CraneAccess,
                    Width = _settings.CraneAccessWidth,
                    StartPoint = routes.First().EndPoint,
                    EndPoint = crane.Location,
                    SurfaceType = SurfaceType.Hardstanding,
                    MaxWeight = 100000 // For crane mobilization
                });
            }

            // Emergency access
            routes.Add(new AccessRoute
            {
                Id = "ROUTE_EMERGENCY",
                Type = RouteType.EmergencyAccess,
                Width = _settings.EmergencyAccessWidth,
                MustRemainClear = true,
                Notes = "Keep clear at all times for emergency vehicle access"
            });

            return routes;
        }

        private List<WelfareFacility> PlanWelfareFacilities(SiteModel site, ConstructionOptions options)
        {
            var facilities = new List<WelfareFacility>();

            var peakWorkers = options.PeakWorkforce > 0 ? options.PeakWorkforce : 50;

            // Site offices
            facilities.Add(new WelfareFacility
            {
                Id = "WF_OFFICE",
                Type = FacilityType.SiteOffice,
                Capacity = 10,
                Size = 30 // m²
            });

            // Toilets (1 per 7 workers)
            var toiletCount = (int)Math.Ceiling(peakWorkers / 7.0);
            facilities.Add(new WelfareFacility
            {
                Id = "WF_TOILET",
                Type = FacilityType.Toilets,
                Capacity = toiletCount,
                Size = toiletCount * 2
            });

            // Canteen/mess (0.5m² per worker)
            facilities.Add(new WelfareFacility
            {
                Id = "WF_CANTEEN",
                Type = FacilityType.Canteen,
                Capacity = peakWorkers,
                Size = peakWorkers * 0.5
            });

            // Drying room
            facilities.Add(new WelfareFacility
            {
                Id = "WF_DRYING",
                Type = FacilityType.DryingRoom,
                Capacity = peakWorkers,
                Size = 15
            });

            // First aid room
            facilities.Add(new WelfareFacility
            {
                Id = "WF_FIRSTAID",
                Type = FacilityType.FirstAid,
                Capacity = 4,
                Size = 10
            });

            return facilities;
        }

        private List<TemporaryService> PlanTemporaryServices(SiteModel site, BuildingModel building)
        {
            var services = new List<TemporaryService>();

            // Temporary power
            var powerLoad = CalculateTempPowerLoad(building);
            services.Add(new TemporaryService
            {
                Id = "SVC_POWER",
                Type = ServiceType.Power,
                Capacity = powerLoad,
                ConnectionPoint = site.Entrance,
                DistributionPoints = GeneratePowerDistributionPoints(building)
            });

            // Temporary water
            services.Add(new TemporaryService
            {
                Id = "SVC_WATER",
                Type = ServiceType.Water,
                Capacity = 5000, // L/day
                ConnectionPoint = site.Entrance
            });

            // Temporary drainage
            services.Add(new TemporaryService
            {
                Id = "SVC_DRAIN",
                Type = ServiceType.Drainage,
                Notes = "Silt traps required before discharge"
            });

            // Temporary telecom/data
            services.Add(new TemporaryService
            {
                Id = "SVC_TELECOM",
                Type = ServiceType.Telecom,
                Notes = "Fibre connection to site office"
            });

            return services;
        }

        private double CalculateTempPowerLoad(BuildingModel building)
        {
            // Base load + crane + welding + tools + lighting
            double baseLoad = 50; // kW
            double craneLoad = 100; // kW per crane
            double toolsLoad = building.Elements.Count * 0.1; // kW

            return baseLoad + craneLoad + toolsLoad;
        }

        private List<Point3D> GeneratePowerDistributionPoints(BuildingModel building)
        {
            var points = new List<Point3D>();

            foreach (var level in building.Levels.Take(3)) // First 3 levels
            {
                points.Add(new Point3D(0, 0, level.Elevation));
            }

            return points;
        }

        private TrafficManagementPlan PlanTrafficManagement(SiteModel site, SiteLogistics logistics)
        {
            return new TrafficManagementPlan
            {
                OneWaySystem = true,
                SpeedLimit = 10, // km/h
                PedestrianRoutes = new List<PedestrianRoute>
                {
                    new PedestrianRoute
                    {
                        Id = "PED_MAIN",
                        Width = 1200,
                        Notes = "Main pedestrian route from entrance to welfare"
                    }
                },
                SignageLocations = GenerateSignageLocations(site, logistics),
                BanksmansRequired = logistics.CranePositions.Any()
            };
        }

        private List<SignageLocation> GenerateSignageLocations(SiteModel site, SiteLogistics logistics)
        {
            var signs = new List<SignageLocation>();

            // Entrance signs
            signs.Add(new SignageLocation
            {
                Location = site.Entrance,
                SignType = "Site entrance - All visitors report to office",
                Size = "Large"
            });

            // PPE signs
            signs.Add(new SignageLocation
            {
                Location = site.Entrance,
                SignType = "PPE required beyond this point",
                Size = "Large"
            });

            // Speed limit
            signs.Add(new SignageLocation
            {
                Location = site.Entrance,
                SignType = "Speed limit 10 km/h",
                Size = "Medium"
            });

            return signs;
        }
    }

    #endregion

    #region Safety Zone Generator

    /// <summary>
    /// Generates safety exclusion zones for construction sites
    /// </summary>
    internal class SafetyZoneGenerator
    {
        private readonly ConstructionModelingSettings _settings;

        public SafetyZoneGenerator(ConstructionModelingSettings settings)
        {
            _settings = settings;
        }

        public async Task<List<SafetyZone>> GenerateZonesAsync(
            SiteModel site,
            BuildingModel building,
            List<ScaffoldingSystem> scaffolding,
            SiteLogistics logistics,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var zones = new List<SafetyZone>();

                // Crane exclusion zones
                foreach (var crane in logistics.CranePositions)
                {
                    zones.Add(GenerateCraneExclusionZone(crane));
                    zones.Add(GenerateCraneLoadZone(crane));
                }

                // Scaffold exclusion zones
                foreach (var scaffold in scaffolding)
                {
                    zones.Add(GenerateScaffoldDropZone(scaffold));
                }

                // Building perimeter drop zone
                zones.Add(GeneratePerimeterDropZone(building));

                // Material delivery zones
                zones.AddRange(GenerateDeliveryZones(logistics));

                // Hot works zones
                zones.Add(GenerateHotWorksZone(building));

                return zones;
            }, cancellationToken);
        }

        private SafetyZone GenerateCraneExclusionZone(CranePosition crane)
        {
            return new SafetyZone
            {
                Id = $"ZONE_CRANE_EXCL_{crane.Id}",
                Type = ZoneType.CraneExclusion,
                Center = crane.Location,
                Radius = crane.RequiredClearance,
                Description = "Crane exclusion zone - No entry during crane operations",
                Restrictions = new List<string>
                {
                    "No unauthorized personnel",
                    "No static equipment storage",
                    "Slinger/signaller required for crane ops"
                }
            };
        }

        private SafetyZone GenerateCraneLoadZone(CranePosition crane)
        {
            return new SafetyZone
            {
                Id = $"ZONE_CRANE_LOAD_{crane.Id}",
                Type = ZoneType.LoadingZone,
                Center = new Point3D(crane.Location.X + 5000, crane.Location.Y, 0),
                Radius = 5000,
                Description = "Crane loading zone",
                Restrictions = new List<string>
                {
                    "Authorized loading/unloading only",
                    "Banksman required",
                    "Clear zone when load overhead"
                }
            };
        }

        private SafetyZone GenerateScaffoldDropZone(ScaffoldingSystem scaffold)
        {
            return new SafetyZone
            {
                Id = $"ZONE_SCAFFOLD_{scaffold.Id}",
                Type = ZoneType.DropZone,
                Width = scaffold.TotalWidth + _settings.ScaffoldDropZoneWidth * 2,
                Length = _settings.ScaffoldDropZoneWidth,
                Height = scaffold.TotalHeight,
                Description = "Scaffold drop zone - Falling objects hazard",
                Restrictions = new List<string>
                {
                    "Hard hats mandatory",
                    "No storage in drop zone",
                    "Fans/netting required at platform edges"
                }
            };
        }

        private SafetyZone GeneratePerimeterDropZone(BuildingModel building)
        {
            var bounds = new BoundingBox3D
            {
                MinX = building.Elements.Min(e => e.StartPoint?.X ?? 0),
                MaxX = building.Elements.Max(e => e.EndPoint?.X ?? e.StartPoint?.X ?? 0),
                MinY = building.Elements.Min(e => e.StartPoint?.Y ?? 0),
                MaxY = building.Elements.Max(e => e.EndPoint?.Y ?? e.StartPoint?.Y ?? 0)
            };

            return new SafetyZone
            {
                Id = "ZONE_PERIMETER",
                Type = ZoneType.DropZone,
                Boundary = new List<Point3D>
                {
                    new Point3D(bounds.MinX - _settings.PerimeterDropZoneWidth, bounds.MinY - _settings.PerimeterDropZoneWidth, 0),
                    new Point3D(bounds.MaxX + _settings.PerimeterDropZoneWidth, bounds.MinY - _settings.PerimeterDropZoneWidth, 0),
                    new Point3D(bounds.MaxX + _settings.PerimeterDropZoneWidth, bounds.MaxY + _settings.PerimeterDropZoneWidth, 0),
                    new Point3D(bounds.MinX - _settings.PerimeterDropZoneWidth, bounds.MaxY + _settings.PerimeterDropZoneWidth, 0)
                },
                Description = "Building perimeter drop zone",
                Restrictions = new List<string>
                {
                    "Hard hats mandatory",
                    "Edge protection required at all levels",
                    "No storage in drop zone"
                }
            };
        }

        private List<SafetyZone> GenerateDeliveryZones(SiteLogistics logistics)
        {
            var zones = new List<SafetyZone>();

            foreach (var storage in logistics.StorageAreas)
            {
                zones.Add(new SafetyZone
                {
                    Id = $"ZONE_DELIVERY_{storage.Id}",
                    Type = ZoneType.DeliveryZone,
                    Center = storage.Location,
                    Radius = 10000,
                    Description = $"Delivery zone for {storage.MaterialType}",
                    Restrictions = new List<string>
                    {
                        "Authorized vehicles only",
                        "Banksman required for reversing",
                        "PPE mandatory"
                    }
                });
            }

            return zones;
        }

        private SafetyZone GenerateHotWorksZone(BuildingModel building)
        {
            return new SafetyZone
            {
                Id = "ZONE_HOTWORKS",
                Type = ZoneType.HotWorks,
                Description = "Designated hot works area",
                Restrictions = new List<string>
                {
                    "Hot works permit required",
                    "Fire extinguisher within 5m",
                    "Fire watch 30 min after works complete",
                    "Remove combustibles within 10m"
                }
            };
        }
    }

    #endregion

    #region Construction Sequencer

    /// <summary>
    /// Generates construction sequences and 4D schedules
    /// </summary>
    internal class ConstructionSequencer
    {
        private readonly ConstructionModelingSettings _settings;

        public ConstructionSequencer(ConstructionModelingSettings settings)
        {
            _settings = settings;
        }

        public async Task<ConstructionSequence> GenerateSequenceAsync(
            BuildingModel buildingModel,
            BuildingAnalysis analysis,
            ConstructionOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var sequence = new ConstructionSequence
                {
                    BuildingId = buildingModel.Id,
                    Phases = new List<ConstructionPhase>()
                };

                int phaseOrder = 1;

                // Site preparation
                sequence.Phases.Add(new ConstructionPhase
                {
                    Id = $"PHASE_{phaseOrder}",
                    Order = phaseOrder++,
                    Name = "Site Preparation",
                    Type = PhaseType.SitePrep,
                    Duration = TimeSpan.FromDays(14),
                    Activities = GenerateSitePrepActivities()
                });

                // Substructure
                if (analysis.TotalHeight > 0 || HasBasement(buildingModel))
                {
                    sequence.Phases.Add(new ConstructionPhase
                    {
                        Id = $"PHASE_{phaseOrder}",
                        Order = phaseOrder++,
                        Name = "Substructure",
                        Type = PhaseType.Substructure,
                        Duration = EstimateSubstructureDuration(buildingModel),
                        Activities = GenerateSubstructureActivities(buildingModel)
                    });
                }

                // Structure (per level)
                foreach (var level in buildingModel.Levels.OrderBy(l => l.Elevation))
                {
                    sequence.Phases.Add(new ConstructionPhase
                    {
                        Id = $"PHASE_{phaseOrder}",
                        Order = phaseOrder++,
                        Name = $"Structure - {level.Name}",
                        Type = PhaseType.Structure,
                        Level = level.Name,
                        Duration = EstimateLevelDuration(level, analysis),
                        Activities = GenerateStructureActivities(level, analysis)
                    });
                }

                // Envelope
                sequence.Phases.Add(new ConstructionPhase
                {
                    Id = $"PHASE_{phaseOrder}",
                    Order = phaseOrder++,
                    Name = "Building Envelope",
                    Type = PhaseType.Envelope,
                    Duration = EstimateEnvelopeDuration(buildingModel),
                    Activities = GenerateEnvelopeActivities(buildingModel)
                });

                // MEP rough-in
                sequence.Phases.Add(new ConstructionPhase
                {
                    Id = $"PHASE_{phaseOrder}",
                    Order = phaseOrder++,
                    Name = "MEP Rough-In",
                    Type = PhaseType.MEP,
                    Duration = EstimateMEPDuration(buildingModel),
                    Activities = GenerateMEPActivities(buildingModel)
                });

                // Finishes (per level)
                foreach (var level in buildingModel.Levels.OrderBy(l => l.Elevation))
                {
                    sequence.Phases.Add(new ConstructionPhase
                    {
                        Id = $"PHASE_{phaseOrder}",
                        Order = phaseOrder++,
                        Name = $"Finishes - {level.Name}",
                        Type = PhaseType.Finishes,
                        Level = level.Name,
                        Duration = EstimateFinishesDuration(level),
                        Activities = GenerateFinishesActivities(level)
                    });
                }

                // External works
                sequence.Phases.Add(new ConstructionPhase
                {
                    Id = $"PHASE_{phaseOrder}",
                    Order = phaseOrder++,
                    Name = "External Works",
                    Type = PhaseType.External,
                    Duration = TimeSpan.FromDays(30),
                    Activities = GenerateExternalWorksActivities()
                });

                // Commissioning
                sequence.Phases.Add(new ConstructionPhase
                {
                    Id = $"PHASE_{phaseOrder}",
                    Order = phaseOrder++,
                    Name = "Commissioning",
                    Type = PhaseType.Commissioning,
                    Duration = TimeSpan.FromDays(14),
                    Activities = GenerateCommissioningActivities()
                });

                // Calculate total duration
                sequence.TotalDuration = TimeSpan.FromDays(
                    sequence.Phases.Sum(p => p.Duration.TotalDays));

                return sequence;
            }, cancellationToken);
        }

        private bool HasBasement(BuildingModel model)
        {
            return model.Levels.Any(l => l.Elevation < 0);
        }

        private TimeSpan EstimateSubstructureDuration(BuildingModel model)
        {
            var basementLevels = model.Levels.Count(l => l.Elevation < 0);
            return TimeSpan.FromDays(21 + basementLevels * 14);
        }

        private TimeSpan EstimateLevelDuration(BuildingLevel level, BuildingAnalysis analysis)
        {
            var baseDuration = analysis.StructuralSystem switch
            {
                StructuralSystem.SteelFrame => 7,
                StructuralSystem.ConcreteFrame => 14,
                _ => 10
            };

            return TimeSpan.FromDays(baseDuration);
        }

        private TimeSpan EstimateEnvelopeDuration(BuildingModel model)
        {
            var area = model.Elements.Where(e => e.IsExterior).Sum(e => e.Area);
            return TimeSpan.FromDays(Math.Max(14, area / 100)); // ~100m² per day
        }

        private TimeSpan EstimateMEPDuration(BuildingModel model)
        {
            var levelCount = model.Levels.Count;
            return TimeSpan.FromDays(levelCount * 7);
        }

        private TimeSpan EstimateFinishesDuration(BuildingLevel level)
        {
            return TimeSpan.FromDays(21);
        }

        private List<ConstructionActivity> GenerateSitePrepActivities()
        {
            return new List<ConstructionActivity>
            {
                new ConstructionActivity { Name = "Site clearance", Duration = TimeSpan.FromDays(3) },
                new ConstructionActivity { Name = "Temporary fencing", Duration = TimeSpan.FromDays(2) },
                new ConstructionActivity { Name = "Site offices setup", Duration = TimeSpan.FromDays(3) },
                new ConstructionActivity { Name = "Temporary services", Duration = TimeSpan.FromDays(5) },
                new ConstructionActivity { Name = "Ground investigation", Duration = TimeSpan.FromDays(3) }
            };
        }

        private List<ConstructionActivity> GenerateSubstructureActivities(BuildingModel model)
        {
            return new List<ConstructionActivity>
            {
                new ConstructionActivity { Name = "Excavation", Duration = TimeSpan.FromDays(7) },
                new ConstructionActivity { Name = "Piling/foundations", Duration = TimeSpan.FromDays(14) },
                new ConstructionActivity { Name = "Ground beams", Duration = TimeSpan.FromDays(7) },
                new ConstructionActivity { Name = "Basement walls", Duration = TimeSpan.FromDays(14) },
                new ConstructionActivity { Name = "Waterproofing", Duration = TimeSpan.FromDays(5) },
                new ConstructionActivity { Name = "Backfill", Duration = TimeSpan.FromDays(3) }
            };
        }

        private List<ConstructionActivity> GenerateStructureActivities(BuildingLevel level, BuildingAnalysis analysis)
        {
            return new List<ConstructionActivity>
            {
                new ConstructionActivity { Name = $"Columns {level.Name}", Duration = TimeSpan.FromDays(3) },
                new ConstructionActivity { Name = $"Beams {level.Name}", Duration = TimeSpan.FromDays(3) },
                new ConstructionActivity { Name = $"Slab {level.Name}", Duration = TimeSpan.FromDays(5) },
                new ConstructionActivity { Name = $"Core walls {level.Name}", Duration = TimeSpan.FromDays(4) }
            };
        }

        private List<ConstructionActivity> GenerateEnvelopeActivities(BuildingModel model)
        {
            return new List<ConstructionActivity>
            {
                new ConstructionActivity { Name = "External walls", Duration = TimeSpan.FromDays(21) },
                new ConstructionActivity { Name = "Windows installation", Duration = TimeSpan.FromDays(14) },
                new ConstructionActivity { Name = "Roof", Duration = TimeSpan.FromDays(14) },
                new ConstructionActivity { Name = "External doors", Duration = TimeSpan.FromDays(5) }
            };
        }

        private List<ConstructionActivity> GenerateMEPActivities(BuildingModel model)
        {
            return new List<ConstructionActivity>
            {
                new ConstructionActivity { Name = "Electrical first fix", Duration = TimeSpan.FromDays(14) },
                new ConstructionActivity { Name = "Plumbing first fix", Duration = TimeSpan.FromDays(14) },
                new ConstructionActivity { Name = "HVAC ductwork", Duration = TimeSpan.FromDays(21) },
                new ConstructionActivity { Name = "Fire protection", Duration = TimeSpan.FromDays(10) }
            };
        }

        private List<ConstructionActivity> GenerateFinishesActivities(BuildingLevel level)
        {
            return new List<ConstructionActivity>
            {
                new ConstructionActivity { Name = $"Partitions {level.Name}", Duration = TimeSpan.FromDays(7) },
                new ConstructionActivity { Name = $"Ceilings {level.Name}", Duration = TimeSpan.FromDays(5) },
                new ConstructionActivity { Name = $"Floor finishes {level.Name}", Duration = TimeSpan.FromDays(5) },
                new ConstructionActivity { Name = $"Painting {level.Name}", Duration = TimeSpan.FromDays(5) },
                new ConstructionActivity { Name = $"Joinery {level.Name}", Duration = TimeSpan.FromDays(5) }
            };
        }

        private List<ConstructionActivity> GenerateExternalWorksActivities()
        {
            return new List<ConstructionActivity>
            {
                new ConstructionActivity { Name = "Paving", Duration = TimeSpan.FromDays(10) },
                new ConstructionActivity { Name = "Landscaping", Duration = TimeSpan.FromDays(7) },
                new ConstructionActivity { Name = "Fencing", Duration = TimeSpan.FromDays(5) },
                new ConstructionActivity { Name = "Signage", Duration = TimeSpan.FromDays(3) }
            };
        }

        private List<ConstructionActivity> GenerateCommissioningActivities()
        {
            return new List<ConstructionActivity>
            {
                new ConstructionActivity { Name = "MEP commissioning", Duration = TimeSpan.FromDays(7) },
                new ConstructionActivity { Name = "Fire system testing", Duration = TimeSpan.FromDays(3) },
                new ConstructionActivity { Name = "Snagging", Duration = TimeSpan.FromDays(5) },
                new ConstructionActivity { Name = "Handover", Duration = TimeSpan.FromDays(2) }
            };
        }
    }

    #endregion

    #region Data Models

    // Settings
    public class ConstructionModelingSettings
    {
        // Scaffolding
        public double StandardBayWidth { get; set; } = 2500; // mm
        public double StandardLiftHeight { get; set; } = 2000; // mm
        public double TieVerticalSpacing { get; set; } = 4000; // mm
        public double TieHorizontalSpacing { get; set; } = 5000; // mm
        public double StandardTieCapacity { get; set; } = 12.5; // kN
        public double ScaffoldSelfWeight { get; set; } = 0.5; // kN/m height
        public double ScaffoldLiveLoad { get; set; } = 2.0; // kN/m²
        public double ScaffoldOverheadHeight { get; set; } = 1000; // mm
        public double ScaffoldSideExtension { get; set; } = 300; // mm
        public double InternalScaffoldThreshold { get; set; } = 4000; // mm

        // Cranes
        public double CraneOverreach { get; set; } = 5000; // mm
        public double CraneMinClearance { get; set; } = 3000; // mm

        // Access
        public double MainAccessWidth { get; set; } = 6000; // mm
        public double CraneAccessWidth { get; set; } = 8000; // mm
        public double EmergencyAccessWidth { get; set; } = 4000; // mm

        // Safety zones
        public double ScaffoldDropZoneWidth { get; set; } = 3000; // mm
        public double PerimeterDropZoneWidth { get; set; } = 2000; // mm
    }

    public class ConstructionOptions
    {
        public ScaffoldType ScaffoldingType { get; set; } = ScaffoldType.None;
        public double MaxScaffoldHeight { get; set; } = 50000;
        public CraneType PreferredCraneType { get; set; } = CraneType.None;
        public int PeakWorkforce { get; set; } = 50;
    }

    public class ScaffoldingOptions
    {
        public ScaffoldType PreferredType { get; set; } = ScaffoldType.SystemScaffold;
        public double MaxHeight { get; set; } = 50000;
        public bool IncludeProtection { get; set; } = true;
    }

    public class TemporaryWorksOptions
    {
        public FormworkSystem PreferredFormwork { get; set; } = FormworkSystem.SystemForm;
        public bool IncludeBackpropping { get; set; } = true;
    }

    // Models
    public class BuildingModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<BuildingLevel> Levels { get; set; } = new();
        public List<BuildingElement> Elements { get; set; } = new();
    }

    public class BuildingLevel
    {
        public string Name { get; set; }
        public double Elevation { get; set; }
        public double Height { get; set; }
        public double Area { get; set; }
    }

    public class BuildingElement
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ElementType { get; set; }
        public string Level { get; set; }
        public string Material { get; set; }
        public string Zone { get; set; }
        public string ConstructionPhase { get; set; }
        public bool IsStructural { get; set; }
        public bool IsExterior { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public Point3D Location { get; set; }
        public BoundingBox3D BoundingBox { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Length { get; set; }
        public double Thickness { get; set; }
        public double Perimeter { get; set; }
        public double Area { get; set; }
        public double Weight { get; set; }
        public double BaseLevel { get; set; }
        public double TopLevel { get; set; }
    }

    public class SiteModel
    {
        public string Id { get; set; }
        public List<Point3D> Boundary { get; set; } = new();
        public Point3D Entrance { get; set; }
        public double Area { get; set; }
    }

    public class BuildingFace
    {
        public string Id { get; set; }
        public string Orientation { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double BaseElevation { get; set; }
        public List<string> Walls { get; set; } = new();
    }

    // Analysis
    public class BuildingAnalysis
    {
        public double TotalHeight { get; set; }
        public double TotalFootprint { get; set; }
        public int LevelCount { get; set; }
        public StructuralSystem StructuralSystem { get; set; }
        public FacadeType FacadeType { get; set; }
        public double ComplexityScore { get; set; }
        public List<HeavyLift> HeavyLifts { get; set; } = new();
        public List<CriticalPath> CriticalPaths { get; set; } = new();
        public List<AccessRequirement> AccessRequirements { get; set; } = new();
    }

    public class HeavyLift
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public double Weight { get; set; }
        public BoundingBox3D Dimensions { get; set; }
        public Point3D FinalPosition { get; set; }
        public string Level { get; set; }
        public double RequiredCraneCapacity { get; set; }
    }

    public class CriticalPath
    {
        public string Name { get; set; }
        public List<string> Elements { get; set; } = new();
        public int Priority { get; set; }
    }

    public class AccessRequirement
    {
        public string Level { get; set; }
        public double Elevation { get; set; }
        public AccessType RequiredAccess { get; set; }
        public double MinimumClearance { get; set; }
    }

    // Scaffolding
    public class ScaffoldingSystem
    {
        public string Id { get; set; }
        public ScaffoldType Type { get; set; }
        public string FaceId { get; set; }
        public string Orientation { get; set; }
        public bool IsInternal { get; set; }
        public double BaseElevation { get; set; }
        public double TotalHeight { get; set; }
        public double TotalWidth { get; set; }
        public double TotalArea { get; set; }
        public List<ScaffoldBay> Bays { get; set; } = new();
        public List<ScaffoldLift> Lifts { get; set; } = new();
        public List<ScaffoldTie> Ties { get; set; } = new();
        public List<ScaffoldFoundation> Foundations { get; set; } = new();
        public int ComponentCount { get; set; }
        public string ErectionPhase { get; set; }
        public string StrikePhase { get; set; }
        public string Notes { get; set; }
    }

    public class ScaffoldBay
    {
        public int Index { get; set; }
        public double Width { get; set; }
        public double StartOffset { get; set; }
    }

    public class ScaffoldLift
    {
        public int Index { get; set; }
        public double Height { get; set; }
        public double BaseElevation { get; set; }
        public bool HasBoarding { get; set; }
        public bool HasGuardRails { get; set; }
    }

    public class ScaffoldTie
    {
        public double Elevation { get; set; }
        public double HorizontalOffset { get; set; }
        public TieType TieType { get; set; }
        public double LoadCapacity { get; set; }
    }

    public class ScaffoldFoundation
    {
        public Point3D Location { get; set; }
        public FoundationType Type { get; set; }
        public double LoadCapacity { get; set; }
    }

    // Temporary Works
    public class TemporaryWork
    {
        public string Id { get; set; }
        public TemporaryWorkType Type { get; set; }
        public string TargetElementId { get; set; }
        public string Description { get; set; }
        public string ConstructionPhase { get; set; }
        public double Area { get; set; }
        public double Height { get; set; }
        public double Length { get; set; }
        public double Depth { get; set; }
        public FormworkSystem System { get; set; }
        public ShoringType ShoringSystem { get; set; }
        public TimeSpan StrikingTime { get; set; }
        public double LoadCapacity { get; set; }
        public int PropCount { get; set; }
        public double PumpCapacity { get; set; }
    }

    public class EdgeDefinition
    {
        public string Id { get; set; }
        public string Level { get; set; }
        public string Location { get; set; }
        public double Length { get; set; }
    }

    // Site Logistics
    public class SiteLogistics
    {
        public string SiteId { get; set; }
        public List<CranePosition> CranePositions { get; set; } = new();
        public List<StorageArea> StorageAreas { get; set; } = new();
        public List<AccessRoute> AccessRoutes { get; set; } = new();
        public List<WelfareFacility> WelfareFacilities { get; set; } = new();
        public List<TemporaryService> TemporaryServices { get; set; } = new();
        public TrafficManagementPlan TrafficManagement { get; set; }
    }

    public class CranePosition
    {
        public string Id { get; set; }
        public Point3D Location { get; set; }
        public CraneType Type { get; set; }
        public double MaxRadius { get; set; }
        public double MaxCapacity { get; set; }
        public double RequiredClearance { get; set; }
        public CraneFoundationType FoundationType { get; set; }
    }

    public class StorageArea
    {
        public string Id { get; set; }
        public MaterialType MaterialType { get; set; }
        public double RequiredArea { get; set; }
        public Point3D Location { get; set; }
        public bool RequiresHardstanding { get; set; }
        public string Notes { get; set; }
    }

    public class AccessRoute
    {
        public string Id { get; set; }
        public RouteType Type { get; set; }
        public double Width { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public SurfaceType SurfaceType { get; set; }
        public double MaxWeight { get; set; }
        public bool MustRemainClear { get; set; }
        public string Notes { get; set; }
    }

    public class WelfareFacility
    {
        public string Id { get; set; }
        public FacilityType Type { get; set; }
        public int Capacity { get; set; }
        public double Size { get; set; }
    }

    public class TemporaryService
    {
        public string Id { get; set; }
        public ServiceType Type { get; set; }
        public double Capacity { get; set; }
        public Point3D ConnectionPoint { get; set; }
        public List<Point3D> DistributionPoints { get; set; } = new();
        public string Notes { get; set; }
    }

    public class TrafficManagementPlan
    {
        public bool OneWaySystem { get; set; }
        public double SpeedLimit { get; set; }
        public List<PedestrianRoute> PedestrianRoutes { get; set; } = new();
        public List<SignageLocation> SignageLocations { get; set; } = new();
        public bool BanksmansRequired { get; set; }
    }

    public class PedestrianRoute
    {
        public string Id { get; set; }
        public double Width { get; set; }
        public string Notes { get; set; }
    }

    public class SignageLocation
    {
        public Point3D Location { get; set; }
        public string SignType { get; set; }
        public string Size { get; set; }
    }

    // Safety Zones
    public class SafetyZone
    {
        public string Id { get; set; }
        public ZoneType Type { get; set; }
        public Point3D Center { get; set; }
        public double Radius { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
        public double Height { get; set; }
        public List<Point3D> Boundary { get; set; }
        public string Description { get; set; }
        public List<string> Restrictions { get; set; } = new();
    }

    // Construction Sequence
    public class ConstructionSequence
    {
        public string BuildingId { get; set; }
        public List<ConstructionPhase> Phases { get; set; } = new();
        public TimeSpan TotalDuration { get; set; }
    }

    public class ConstructionPhase
    {
        public string Id { get; set; }
        public int Order { get; set; }
        public string Name { get; set; }
        public PhaseType Type { get; set; }
        public string Level { get; set; }
        public TimeSpan Duration { get; set; }
        public List<ConstructionActivity> Activities { get; set; } = new();
    }

    public class ConstructionActivity
    {
        public string Name { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> Predecessors { get; set; } = new();
    }

    // Results
    public class ConstructionModelResult
    {
        public bool Success { get; set; }
        public string BuildingId { get; set; }
        public DateTime GenerationStartTime { get; set; }
        public DateTime GenerationEndTime { get; set; }
        public ConstructionSequence ConstructionSequence { get; set; }
        public List<ScaffoldingSystem> ScaffoldingSystems { get; set; } = new();
        public List<TemporaryWork> TemporaryWorks { get; set; } = new();
        public SiteLogistics SiteLogistics { get; set; }
        public List<SafetyZone> SafetyZones { get; set; } = new();
        public ConstructionStatistics Statistics { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class ConstructionProgress
    {
        public int PercentComplete { get; set; }
        public string Status { get; set; }

        public ConstructionProgress(int percent, string status)
        {
            PercentComplete = percent;
            Status = status;
        }
    }

    public class ConstructionStatistics
    {
        public int TotalPhases { get; set; }
        public int TotalScaffoldingSystems { get; set; }
        public int TotalTemporaryWorks { get; set; }
        public int TotalSafetyZones { get; set; }
        public double EstimatedScaffoldingArea { get; set; }
        public double EstimatedFormworkArea { get; set; }
        public int CraneLocations { get; set; }
        public int MaterialStorageAreas { get; set; }
    }

    // Geometry

    public class BoundingBox3D
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }

        public double Width => MaxX - MinX;
        public double Depth => MaxY - MinY;
        public double Height => MaxZ - MinZ;
    }

    #endregion

    #region Enumerations

    public enum StructuralSystem
    {
        SteelFrame,
        ConcreteFrame,
        ConcreteShearWall,
        LoadBearingMasonry,
        Timber,
        Hybrid
    }

    public enum FacadeType
    {
        Masonry,
        CurtainWall,
        Rainscreen,
        Precast,
        EIFS
    }

    public enum ScaffoldType
    {
        None,
        TubeAndFitting,
        CuplockSystem,
        SystemScaffold,
        Ringlock,
        MobileAccessTower,
        SuspendedScaffold,
        MastClimber
    }

    public enum TieType
    {
        ThroughTie,
        BoxTie,
        LipTie,
        RevealTie
    }

    public enum FoundationType
    {
        BasePlate,
        SoleBoard,
        SuspendedBracket,
        Cantilever
    }

    public enum TemporaryWorkType
    {
        Formwork,
        Shoring,
        Bracing,
        EdgeProtection,
        PenetrationCover,
        Dewatering,
        Hoarding,
        WeatherProtection
    }

    public enum FormworkSystem
    {
        Traditional,
        SystemForm,
        TableForm,
        ClimbingForm,
        SlipForm,
        ColumnForm,
        BeamForm,
        WallPanel
    }

    public enum ShoringType
    {
        AdjustableProp,
        AluminiumBeam,
        FalseworkTower,
        PostShore
    }

    public enum AccessType
    {
        Ground,
        Scaffolding,
        MastClimber,
        Crane
    }

    public enum CraneType
    {
        None,
        TowerCrane,
        LufferTower,
        MobileCrane,
        CrawlerCrane,
        HoistCrane
    }

    public enum CraneFoundationType
    {
        Static,
        Rail,
        Climbing
    }

    public enum MaterialType
    {
        Steel,
        Rebar,
        Formwork,
        Concrete,
        Masonry,
        MEP,
        Finishes,
        General,
        Waste
    }

    public enum RouteType
    {
        VehicleAccess,
        CraneAccess,
        EmergencyAccess,
        Pedestrian
    }

    public enum SurfaceType
    {
        Hardstanding,
        Gravel,
        Temporary,
        Grass
    }

    public enum FacilityType
    {
        SiteOffice,
        Toilets,
        Canteen,
        DryingRoom,
        FirstAid,
        Storage,
        Security
    }

    public enum ServiceType
    {
        Power,
        Water,
        Drainage,
        Telecom,
        Gas
    }

    public enum ZoneType
    {
        CraneExclusion,
        LoadingZone,
        DropZone,
        DeliveryZone,
        HotWorks,
        Excavation,
        Confined
    }

    public enum PhaseType
    {
        SitePrep,
        Demolition,
        Substructure,
        Structure,
        Envelope,
        MEP,
        Finishes,
        External,
        Commissioning
    }

    #endregion
}
