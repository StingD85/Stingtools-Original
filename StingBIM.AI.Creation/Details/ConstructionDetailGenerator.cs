// ============================================================================
// StingBIM.AI.Creation - Construction Detail Generator
// Automatic generation of construction details from model assemblies
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Creation.Details
{
    /// <summary>
    /// Generates construction details from building assemblies.
    /// Supports wall sections, window/door details, roof details, and junctions.
    /// </summary>
    public class ConstructionDetailGenerator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, DetailTemplate> _detailTemplates;
        private readonly Dictionary<string, LayerDefinition> _layerLibrary;
        private readonly Dictionary<string, MaterialHatch> _hatchPatterns;
        private readonly DetailGeneratorSettings _settings;

        public ConstructionDetailGenerator(DetailGeneratorSettings settings = null)
        {
            _settings = settings ?? new DetailGeneratorSettings();
            _detailTemplates = InitializeDetailTemplates();
            _layerLibrary = InitializeLayerLibrary();
            _hatchPatterns = InitializeHatchPatterns();

            Logger.Info("ConstructionDetailGenerator initialized with {0} templates, {1} layers, {2} hatch patterns",
                _detailTemplates.Count, _layerLibrary.Count, _hatchPatterns.Count);
        }

        #region Main Generation Methods

        /// <summary>
        /// Generates a complete wall section detail from foundation to roof.
        /// </summary>
        public async Task<DetailResult> GenerateWallSectionDetailAsync(
            WallSectionRequest request,
            IProgress<DetailProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Generating wall section detail: {0}", request.DetailName);

            var result = new DetailResult
            {
                DetailType = DetailType.WallSection,
                DetailName = request.DetailName,
                Scale = request.Scale,
                Elements = new List<DetailElement>(),
                Annotations = new List<DetailAnnotation>(),
                Dimensions = new List<DetailDimension>()
            };

            try
            {
                progress?.Report(new DetailProgress { Stage = "Analyzing wall assembly", Percentage = 10 });

                // 1. Generate foundation detail
                if (request.IncludeFoundation)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var foundationElements = await GenerateFoundationDetailAsync(request.FoundationType, request.WallAssembly);
                    result.Elements.AddRange(foundationElements);
                    progress?.Report(new DetailProgress { Stage = "Foundation detail complete", Percentage = 25 });
                }

                // 2. Generate wall layers
                cancellationToken.ThrowIfCancellationRequested();
                var wallElements = await GenerateWallLayersDetailAsync(request.WallAssembly, request.WallHeight);
                result.Elements.AddRange(wallElements);
                progress?.Report(new DetailProgress { Stage = "Wall layers complete", Percentage = 45 });

                // 3. Generate floor junction if multi-story
                if (request.IncludeFloorJunction)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var floorElements = await GenerateFloorJunctionDetailAsync(request.FloorAssembly, request.WallAssembly);
                    result.Elements.AddRange(floorElements);
                    progress?.Report(new DetailProgress { Stage = "Floor junction complete", Percentage = 60 });
                }

                // 4. Generate roof/parapet detail
                if (request.IncludeRoof)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var roofElements = await GenerateRoofDetailAsync(request.RoofType, request.WallAssembly, request.RoofAssembly);
                    result.Elements.AddRange(roofElements);
                    progress?.Report(new DetailProgress { Stage = "Roof detail complete", Percentage = 75 });
                }

                // 5. Add waterproofing and insulation annotations
                cancellationToken.ThrowIfCancellationRequested();
                var waterproofing = await GenerateWaterproofingDetailAsync(request);
                result.Elements.AddRange(waterproofing);
                progress?.Report(new DetailProgress { Stage = "Waterproofing complete", Percentage = 85 });

                // 6. Generate dimensions and annotations
                cancellationToken.ThrowIfCancellationRequested();
                result.Dimensions = GenerateWallSectionDimensions(result.Elements, request);
                result.Annotations = GenerateWallSectionAnnotations(result.Elements, request);
                progress?.Report(new DetailProgress { Stage = "Annotations complete", Percentage = 95 });

                result.IsSuccess = true;
                result.GeneratedAt = DateTime.UtcNow;

                Logger.Info("Wall section detail generated with {0} elements, {1} annotations, {2} dimensions",
                    result.Elements.Count, result.Annotations.Count, result.Dimensions.Count);

                progress?.Report(new DetailProgress { Stage = "Complete", Percentage = 100 });
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Wall section detail generation cancelled");
                result.IsSuccess = false;
                result.ErrorMessage = "Operation cancelled";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to generate wall section detail");
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Generates window detail (jamb, head, and sill).
        /// </summary>
        public async Task<DetailResult> GenerateWindowDetailAsync(
            WindowDetailRequest request,
            IProgress<DetailProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Generating window detail: {0} ({1})", request.DetailName, request.DetailView);

            var result = new DetailResult
            {
                DetailType = DetailType.WindowDetail,
                DetailName = request.DetailName,
                Scale = request.Scale,
                Elements = new List<DetailElement>(),
                Annotations = new List<DetailAnnotation>(),
                Dimensions = new List<DetailDimension>()
            };

            try
            {
                progress?.Report(new DetailProgress { Stage = "Analyzing window assembly", Percentage = 10 });

                // Get wall context for the window
                var wallContext = await AnalyzeWallContextAsync(request.WallAssembly);

                switch (request.DetailView)
                {
                    case WindowDetailView.Jamb:
                        var jambElements = await GenerateWindowJambDetailAsync(request, wallContext);
                        result.Elements.AddRange(jambElements);
                        break;

                    case WindowDetailView.Head:
                        var headElements = await GenerateWindowHeadDetailAsync(request, wallContext);
                        result.Elements.AddRange(headElements);
                        break;

                    case WindowDetailView.Sill:
                        var sillElements = await GenerateWindowSillDetailAsync(request, wallContext);
                        result.Elements.AddRange(sillElements);
                        break;

                    case WindowDetailView.All:
                        progress?.Report(new DetailProgress { Stage = "Generating jamb detail", Percentage = 25 });
                        result.Elements.AddRange(await GenerateWindowJambDetailAsync(request, wallContext));

                        progress?.Report(new DetailProgress { Stage = "Generating head detail", Percentage = 50 });
                        result.Elements.AddRange(await GenerateWindowHeadDetailAsync(request, wallContext));

                        progress?.Report(new DetailProgress { Stage = "Generating sill detail", Percentage = 75 });
                        result.Elements.AddRange(await GenerateWindowSillDetailAsync(request, wallContext));
                        break;
                }

                // Add dimensions and annotations
                result.Dimensions = GenerateWindowDimensions(result.Elements, request);
                result.Annotations = GenerateWindowAnnotations(result.Elements, request);

                result.IsSuccess = true;
                result.GeneratedAt = DateTime.UtcNow;

                progress?.Report(new DetailProgress { Stage = "Complete", Percentage = 100 });

                Logger.Info("Window detail generated with {0} elements", result.Elements.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to generate window detail");
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Generates door detail (jamb, head, and threshold).
        /// </summary>
        public async Task<DetailResult> GenerateDoorDetailAsync(
            DoorDetailRequest request,
            IProgress<DetailProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Generating door detail: {0} ({1})", request.DetailName, request.DetailView);

            var result = new DetailResult
            {
                DetailType = DetailType.DoorDetail,
                DetailName = request.DetailName,
                Scale = request.Scale,
                Elements = new List<DetailElement>(),
                Annotations = new List<DetailAnnotation>(),
                Dimensions = new List<DetailDimension>()
            };

            try
            {
                progress?.Report(new DetailProgress { Stage = "Analyzing door assembly", Percentage = 10 });

                var wallContext = await AnalyzeWallContextAsync(request.WallAssembly);

                switch (request.DetailView)
                {
                    case DoorDetailView.Jamb:
                        result.Elements.AddRange(await GenerateDoorJambDetailAsync(request, wallContext));
                        break;

                    case DoorDetailView.Head:
                        result.Elements.AddRange(await GenerateDoorHeadDetailAsync(request, wallContext));
                        break;

                    case DoorDetailView.Threshold:
                        result.Elements.AddRange(await GenerateDoorThresholdDetailAsync(request, wallContext));
                        break;

                    case DoorDetailView.All:
                        progress?.Report(new DetailProgress { Stage = "Generating jamb detail", Percentage = 30 });
                        result.Elements.AddRange(await GenerateDoorJambDetailAsync(request, wallContext));

                        progress?.Report(new DetailProgress { Stage = "Generating head detail", Percentage = 60 });
                        result.Elements.AddRange(await GenerateDoorHeadDetailAsync(request, wallContext));

                        progress?.Report(new DetailProgress { Stage = "Generating threshold detail", Percentage = 85 });
                        result.Elements.AddRange(await GenerateDoorThresholdDetailAsync(request, wallContext));
                        break;
                }

                result.Dimensions = GenerateDoorDimensions(result.Elements, request);
                result.Annotations = GenerateDoorAnnotations(result.Elements, request);

                result.IsSuccess = true;
                result.GeneratedAt = DateTime.UtcNow;

                progress?.Report(new DetailProgress { Stage = "Complete", Percentage = 100 });

                Logger.Info("Door detail generated with {0} elements", result.Elements.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to generate door detail");
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Generates roof detail (eave, ridge, or verge).
        /// </summary>
        public async Task<DetailResult> GenerateRoofDetailAsync(
            RoofDetailRequest request,
            IProgress<DetailProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Generating roof detail: {0} ({1})", request.DetailName, request.DetailView);

            var result = new DetailResult
            {
                DetailType = DetailType.RoofDetail,
                DetailName = request.DetailName,
                Scale = request.Scale,
                Elements = new List<DetailElement>(),
                Annotations = new List<DetailAnnotation>(),
                Dimensions = new List<DetailDimension>()
            };

            try
            {
                progress?.Report(new DetailProgress { Stage = "Analyzing roof assembly", Percentage = 10 });

                switch (request.DetailView)
                {
                    case RoofDetailView.Eave:
                        result.Elements.AddRange(await GenerateRoofEaveDetailAsync(request));
                        break;

                    case RoofDetailView.Ridge:
                        result.Elements.AddRange(await GenerateRoofRidgeDetailAsync(request));
                        break;

                    case RoofDetailView.Verge:
                        result.Elements.AddRange(await GenerateRoofVergeDetailAsync(request));
                        break;

                    case RoofDetailView.Valley:
                        result.Elements.AddRange(await GenerateRoofValleyDetailAsync(request));
                        break;

                    case RoofDetailView.Hip:
                        result.Elements.AddRange(await GenerateRoofHipDetailAsync(request));
                        break;

                    case RoofDetailView.FlatRoofEdge:
                        result.Elements.AddRange(await GenerateFlatRoofEdgeDetailAsync(request));
                        break;

                    case RoofDetailView.All:
                        result.Elements.AddRange(await GenerateRoofEaveDetailAsync(request));
                        result.Elements.AddRange(await GenerateRoofRidgeDetailAsync(request));
                        result.Elements.AddRange(await GenerateRoofVergeDetailAsync(request));
                        break;
                }

                result.Dimensions = GenerateRoofDimensions(result.Elements, request);
                result.Annotations = GenerateRoofAnnotations(result.Elements, request);

                result.IsSuccess = true;
                result.GeneratedAt = DateTime.UtcNow;

                progress?.Report(new DetailProgress { Stage = "Complete", Percentage = 100 });

                Logger.Info("Roof detail generated with {0} elements", result.Elements.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to generate roof detail");
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Generates floor junction detail.
        /// </summary>
        public async Task<DetailResult> GenerateFloorJunctionDetailAsync(
            FloorJunctionRequest request,
            IProgress<DetailProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Generating floor junction detail: {0}", request.DetailName);

            var result = new DetailResult
            {
                DetailType = DetailType.FloorJunction,
                DetailName = request.DetailName,
                Scale = request.Scale,
                Elements = new List<DetailElement>(),
                Annotations = new List<DetailAnnotation>(),
                Dimensions = new List<DetailDimension>()
            };

            try
            {
                progress?.Report(new DetailProgress { Stage = "Analyzing junction", Percentage = 20 });

                // Generate wall-floor junction
                var junctionElements = await GenerateWallFloorJunctionAsync(
                    request.WallAssembly,
                    request.FloorAssembly,
                    request.JunctionType);

                result.Elements.AddRange(junctionElements);

                // Add fire stopping if required
                if (request.IncludeFireStopping)
                {
                    var fireStopElements = await GenerateFireStoppingDetailAsync(request);
                    result.Elements.AddRange(fireStopElements);
                }

                // Add acoustic separation if required
                if (request.IncludeAcousticSeparation)
                {
                    var acousticElements = await GenerateAcousticSeparationDetailAsync(request);
                    result.Elements.AddRange(acousticElements);
                }

                result.Dimensions = GenerateFloorJunctionDimensions(result.Elements, request);
                result.Annotations = GenerateFloorJunctionAnnotations(result.Elements, request);

                result.IsSuccess = true;
                result.GeneratedAt = DateTime.UtcNow;

                progress?.Report(new DetailProgress { Stage = "Complete", Percentage = 100 });

                Logger.Info("Floor junction detail generated with {0} elements", result.Elements.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to generate floor junction detail");
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Auto-generates all standard details for a wall type.
        /// </summary>
        public async Task<List<DetailResult>> AutoGenerateWallDetailsAsync(
            WallAssembly wallAssembly,
            AutoDetailOptions options,
            IProgress<DetailProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Auto-generating details for wall: {0}", wallAssembly.TypeName);

            var results = new List<DetailResult>();
            var detailsToGenerate = new List<string>();

            // Determine which details to generate based on wall type
            if (options.IncludeWallSection)
                detailsToGenerate.Add("WallSection");

            if (options.IncludeFoundation && wallAssembly.IsExterior)
                detailsToGenerate.Add("Foundation");

            if (options.IncludeRoofJunction)
                detailsToGenerate.Add("RoofJunction");

            if (options.IncludeFloorJunction)
                detailsToGenerate.Add("FloorJunction");

            if (options.IncludeOpeningDetails)
            {
                detailsToGenerate.Add("WindowJamb");
                detailsToGenerate.Add("WindowHead");
                detailsToGenerate.Add("WindowSill");
                detailsToGenerate.Add("DoorJamb");
                detailsToGenerate.Add("DoorHead");
                detailsToGenerate.Add("DoorThreshold");
            }

            int completed = 0;
            foreach (var detailType in detailsToGenerate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var percentage = (int)((completed / (double)detailsToGenerate.Count) * 100);
                progress?.Report(new DetailProgress { Stage = $"Generating {detailType}", Percentage = percentage });

                var detail = await GenerateDetailByTypeAsync(detailType, wallAssembly, options);
                if (detail.IsSuccess)
                {
                    results.Add(detail);
                }

                completed++;
            }

            Logger.Info("Auto-generated {0} details for wall type {1}", results.Count, wallAssembly.TypeName);
            return results;
        }

        #endregion

        #region Foundation Detail Generation

        private async Task<List<DetailElement>> GenerateFoundationDetailAsync(
            FoundationType foundationType,
            WallAssembly wallAssembly)
        {
            var elements = new List<DetailElement>();

            switch (foundationType)
            {
                case FoundationType.StripFoundation:
                    elements.AddRange(GenerateStripFoundation(wallAssembly));
                    break;

                case FoundationType.PadFoundation:
                    elements.AddRange(GeneratePadFoundation(wallAssembly));
                    break;

                case FoundationType.RaftFoundation:
                    elements.AddRange(GenerateRaftFoundation(wallAssembly));
                    break;

                case FoundationType.PileFoundation:
                    elements.AddRange(GeneratePileFoundation(wallAssembly));
                    break;

                case FoundationType.StemWall:
                    elements.AddRange(GenerateStemWallFoundation(wallAssembly));
                    break;

                case FoundationType.GradeBeam:
                    elements.AddRange(GenerateGradeBeamFoundation(wallAssembly));
                    break;
            }

            // Add DPC (Damp Proof Course)
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.Line,
                Layer = "DPC",
                LineStyle = LineStyle.DashDot,
                MaterialName = "DPC Membrane",
                Description = "Damp proof course (min 150mm above ground level)",
                Geometry = new DetailGeometry
                {
                    StartPoint = new Point2D(-wallAssembly.TotalThickness / 2 - 50, 150),
                    EndPoint = new Point2D(wallAssembly.TotalThickness / 2 + 50, 150)
                }
            });

            // Add ground level indicator
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.Line,
                Layer = "Ground",
                LineStyle = LineStyle.GroundLine,
                Description = "Finished ground level",
                Geometry = new DetailGeometry
                {
                    StartPoint = new Point2D(-500, 0),
                    EndPoint = new Point2D(500, 0)
                }
            });

            return await Task.FromResult(elements);
        }

        private List<DetailElement> GenerateStripFoundation(WallAssembly wallAssembly)
        {
            var elements = new List<DetailElement>();
            var wallThickness = wallAssembly.TotalThickness;
            var foundationWidth = wallThickness + 300; // 150mm projection each side
            var foundationDepth = 300; // Standard strip foundation depth
            var concreteBlinding = 50;

            // Concrete blinding
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Concrete-Blinding",
                HatchPattern = "Concrete",
                MaterialName = "Concrete Blinding (50mm)",
                Description = "50mm concrete blinding",
                Geometry = new DetailGeometry
                {
                    Points = new List<Point2D>
                    {
                        new Point2D(-foundationWidth / 2 - 50, -foundationDepth - concreteBlinding),
                        new Point2D(foundationWidth / 2 + 50, -foundationDepth - concreteBlinding),
                        new Point2D(foundationWidth / 2 + 50, -foundationDepth),
                        new Point2D(-foundationWidth / 2 - 50, -foundationDepth)
                    }
                }
            });

            // Strip foundation concrete
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Concrete-Foundation",
                HatchPattern = "Concrete",
                MaterialName = "C25/30 Concrete",
                Description = "Reinforced concrete strip foundation",
                Geometry = new DetailGeometry
                {
                    Points = new List<Point2D>
                    {
                        new Point2D(-foundationWidth / 2, -foundationDepth),
                        new Point2D(foundationWidth / 2, -foundationDepth),
                        new Point2D(foundationWidth / 2, 0),
                        new Point2D(-foundationWidth / 2, 0)
                    }
                }
            });

            // Reinforcement bars
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.ReinforcementBar,
                Layer = "Reinforcement",
                MaterialName = "Y12 Rebar",
                Description = "Y12 bottom reinforcement bars",
                Geometry = new DetailGeometry
                {
                    StartPoint = new Point2D(-foundationWidth / 2 + 50, -foundationDepth + 50),
                    EndPoint = new Point2D(foundationWidth / 2 - 50, -foundationDepth + 50),
                    Diameter = 12
                }
            });

            // Hardcore fill below blinding
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Hardcore",
                HatchPattern = "Gravel",
                MaterialName = "Compacted Hardcore (150mm)",
                Description = "150mm compacted hardcore",
                Geometry = new DetailGeometry
                {
                    Points = new List<Point2D>
                    {
                        new Point2D(-foundationWidth / 2 - 100, -foundationDepth - concreteBlinding - 150),
                        new Point2D(foundationWidth / 2 + 100, -foundationDepth - concreteBlinding - 150),
                        new Point2D(foundationWidth / 2 + 100, -foundationDepth - concreteBlinding),
                        new Point2D(-foundationWidth / 2 - 100, -foundationDepth - concreteBlinding)
                    }
                }
            });

            return elements;
        }

        private List<DetailElement> GeneratePadFoundation(WallAssembly wallAssembly)
        {
            var elements = new List<DetailElement>();
            // Pad foundation implementation
            var padWidth = 1000;
            var padDepth = 400;

            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Concrete-Foundation",
                HatchPattern = "Concrete",
                MaterialName = "C30/37 Concrete",
                Description = "Reinforced concrete pad foundation",
                Geometry = new DetailGeometry
                {
                    Points = new List<Point2D>
                    {
                        new Point2D(-padWidth / 2, -padDepth),
                        new Point2D(padWidth / 2, -padDepth),
                        new Point2D(padWidth / 2, 0),
                        new Point2D(-padWidth / 2, 0)
                    }
                }
            });

            return elements;
        }

        private List<DetailElement> GenerateRaftFoundation(WallAssembly wallAssembly)
        {
            var elements = new List<DetailElement>();
            var raftThickness = 300;

            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Concrete-Foundation",
                HatchPattern = "Concrete",
                MaterialName = "C30/37 Concrete",
                Description = "Reinforced concrete raft foundation",
                Geometry = new DetailGeometry
                {
                    Points = new List<Point2D>
                    {
                        new Point2D(-1000, -raftThickness),
                        new Point2D(1000, -raftThickness),
                        new Point2D(1000, 0),
                        new Point2D(-1000, 0)
                    }
                }
            });

            // Top reinforcement mesh
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.ReinforcementMesh,
                Layer = "Reinforcement",
                MaterialName = "A393 Mesh",
                Description = "A393 top reinforcement mesh",
                Geometry = new DetailGeometry
                {
                    StartPoint = new Point2D(-900, -50),
                    EndPoint = new Point2D(900, -50)
                }
            });

            // Bottom reinforcement mesh
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.ReinforcementMesh,
                Layer = "Reinforcement",
                MaterialName = "A393 Mesh",
                Description = "A393 bottom reinforcement mesh",
                Geometry = new DetailGeometry
                {
                    StartPoint = new Point2D(-900, -raftThickness + 50),
                    EndPoint = new Point2D(900, -raftThickness + 50)
                }
            });

            return elements;
        }

        private List<DetailElement> GeneratePileFoundation(WallAssembly wallAssembly)
        {
            var elements = new List<DetailElement>();

            // Pile cap
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Concrete-Foundation",
                HatchPattern = "Concrete",
                MaterialName = "C35/45 Concrete",
                Description = "Reinforced concrete pile cap",
                Geometry = new DetailGeometry
                {
                    Points = new List<Point2D>
                    {
                        new Point2D(-600, -500),
                        new Point2D(600, -500),
                        new Point2D(600, 0),
                        new Point2D(-600, 0)
                    }
                }
            });

            // Pile (shown dashed below pile cap)
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Concrete-Pile",
                HatchPattern = "Concrete",
                LineStyle = LineStyle.Dashed,
                MaterialName = "C40/50 Concrete",
                Description = "Bored concrete pile (300mm dia)",
                Geometry = new DetailGeometry
                {
                    Center = new Point2D(0, -500),
                    Radius = 150,
                    Height = 3000
                }
            });

            return elements;
        }

        private List<DetailElement> GenerateStemWallFoundation(WallAssembly wallAssembly)
        {
            var elements = new List<DetailElement>();
            var footingWidth = wallAssembly.TotalThickness + 400;
            var footingDepth = 300;
            var stemHeight = 600;

            // Footing
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Concrete-Foundation",
                HatchPattern = "Concrete",
                MaterialName = "C25/30 Concrete",
                Description = "Reinforced concrete footing",
                Geometry = new DetailGeometry
                {
                    Points = new List<Point2D>
                    {
                        new Point2D(-footingWidth / 2, -footingDepth - stemHeight),
                        new Point2D(footingWidth / 2, -footingDepth - stemHeight),
                        new Point2D(footingWidth / 2, -stemHeight),
                        new Point2D(-footingWidth / 2, -stemHeight)
                    }
                }
            });

            // Stem wall
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Concrete-Foundation",
                HatchPattern = "Concrete",
                MaterialName = "C25/30 Concrete",
                Description = "Concrete stem wall",
                Geometry = new DetailGeometry
                {
                    Points = new List<Point2D>
                    {
                        new Point2D(-wallAssembly.TotalThickness / 2, -stemHeight),
                        new Point2D(wallAssembly.TotalThickness / 2, -stemHeight),
                        new Point2D(wallAssembly.TotalThickness / 2, 0),
                        new Point2D(-wallAssembly.TotalThickness / 2, 0)
                    }
                }
            });

            return elements;
        }

        private List<DetailElement> GenerateGradeBeamFoundation(WallAssembly wallAssembly)
        {
            var elements = new List<DetailElement>();
            var beamWidth = wallAssembly.TotalThickness + 100;
            var beamDepth = 450;

            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Concrete-Foundation",
                HatchPattern = "Concrete",
                MaterialName = "C30/37 Concrete",
                Description = "Reinforced concrete grade beam",
                Geometry = new DetailGeometry
                {
                    Points = new List<Point2D>
                    {
                        new Point2D(-beamWidth / 2, -beamDepth),
                        new Point2D(beamWidth / 2, -beamDepth),
                        new Point2D(beamWidth / 2, 0),
                        new Point2D(-beamWidth / 2, 0)
                    }
                }
            });

            // Reinforcement cage
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.ReinforcementBar,
                Layer = "Reinforcement",
                MaterialName = "Y16 Rebar",
                Description = "4Y16 main bars with Y8 links @ 200mm c/c"
            });

            return elements;
        }

        #endregion

        #region Wall Layers Detail Generation

        private async Task<List<DetailElement>> GenerateWallLayersDetailAsync(
            WallAssembly wallAssembly,
            double wallHeight)
        {
            var elements = new List<DetailElement>();
            double currentOffset = -wallAssembly.TotalThickness / 2;

            foreach (var layer in wallAssembly.Layers.OrderBy(l => l.LayerOrder))
            {
                var layerElement = new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = GetLayerCategory(layer.Function),
                    HatchPattern = GetHatchForMaterial(layer.MaterialName),
                    MaterialName = layer.MaterialName,
                    Description = $"{layer.MaterialName} ({layer.Thickness}mm)",
                    Geometry = new DetailGeometry
                    {
                        Points = new List<Point2D>
                        {
                            new Point2D(currentOffset, 0),
                            new Point2D(currentOffset + layer.Thickness, 0),
                            new Point2D(currentOffset + layer.Thickness, wallHeight),
                            new Point2D(currentOffset, wallHeight)
                        }
                    },
                    Properties = new Dictionary<string, object>
                    {
                        ["Thickness"] = layer.Thickness,
                        ["ThermalConductivity"] = layer.ThermalConductivity,
                        ["Function"] = layer.Function.ToString()
                    }
                };

                elements.Add(layerElement);
                currentOffset += layer.Thickness;
            }

            return await Task.FromResult(elements);
        }

        #endregion

        #region Roof Detail Generation

        private async Task<List<DetailElement>> GenerateRoofDetailAsync(
            RoofType roofType,
            WallAssembly wallAssembly,
            RoofAssembly roofAssembly)
        {
            var elements = new List<DetailElement>();

            switch (roofType)
            {
                case RoofType.PitchedWithEave:
                    elements.AddRange(await GeneratePitchedRoofEaveAsync(wallAssembly, roofAssembly));
                    break;

                case RoofType.FlatWithParapet:
                    elements.AddRange(await GenerateFlatRoofParapetAsync(wallAssembly, roofAssembly));
                    break;

                case RoofType.FlatWithFascia:
                    elements.AddRange(await GenerateFlatRoofFasciaAsync(wallAssembly, roofAssembly));
                    break;

                case RoofType.MonoPitch:
                    elements.AddRange(await GenerateMonoPitchRoofAsync(wallAssembly, roofAssembly));
                    break;

                case RoofType.Mansard:
                    elements.AddRange(await GenerateMansardRoofAsync(wallAssembly, roofAssembly));
                    break;
            }

            return elements;
        }

        private async Task<List<DetailElement>> GeneratePitchedRoofEaveAsync(
            WallAssembly wallAssembly,
            RoofAssembly roofAssembly)
        {
            var elements = new List<DetailElement>();
            var wallThickness = wallAssembly.TotalThickness;
            var roofPitch = roofAssembly?.Pitch ?? 30.0;
            var eaveOverhang = roofAssembly?.EaveOverhang ?? 450.0;

            // Wall plate
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Timber",
                HatchPattern = "Wood",
                MaterialName = "Treated Softwood Wall Plate (100x50mm)",
                Description = "100x50mm treated softwood wall plate",
                Geometry = new DetailGeometry
                {
                    Points = new List<Point2D>
                    {
                        new Point2D(-50, 0),
                        new Point2D(50, 0),
                        new Point2D(50, 50),
                        new Point2D(-50, 50)
                    }
                }
            });

            // Rafter
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Timber",
                HatchPattern = "Wood",
                MaterialName = "C24 Softwood Rafter (150x50mm)",
                Description = "150x50mm C24 softwood rafter",
                Rotation = roofPitch
            });

            // Fascia board
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Timber",
                HatchPattern = "Wood",
                MaterialName = "Treated Softwood Fascia (175x25mm)",
                Description = "175x25mm treated softwood fascia board"
            });

            // Soffit
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Soffit",
                HatchPattern = "Plywood",
                MaterialName = "uPVC Soffit Board (9mm)",
                Description = "9mm uPVC soffit board with ventilation"
            });

            // Roof covering layers
            foreach (var layer in roofAssembly?.Layers ?? GetDefaultRoofLayers())
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = "Roofing",
                    HatchPattern = GetHatchForMaterial(layer.MaterialName),
                    MaterialName = layer.MaterialName,
                    Description = $"{layer.MaterialName} ({layer.Thickness}mm)"
                });
            }

            // Gutter
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.Line,
                Layer = "Rainwater",
                LineStyle = LineStyle.Medium,
                MaterialName = "uPVC Half-Round Gutter (112mm)",
                Description = "112mm uPVC half-round gutter"
            });

            // Ventilation path annotation
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.BreakLine,
                Layer = "Annotation",
                Description = "25mm continuous ventilation gap"
            });

            return await Task.FromResult(elements);
        }

        private async Task<List<DetailElement>> GenerateFlatRoofParapetAsync(
            WallAssembly wallAssembly,
            RoofAssembly roofAssembly)
        {
            var elements = new List<DetailElement>();
            var parapetHeight = 600;
            var cappingWidth = wallAssembly.TotalThickness + 100;

            // Parapet wall continuation
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Masonry",
                HatchPattern = GetHatchForMaterial(wallAssembly.Layers.First().MaterialName),
                MaterialName = wallAssembly.Layers.First().MaterialName,
                Description = $"Parapet wall ({parapetHeight}mm high)"
            });

            // Coping stone/capping
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Stone",
                HatchPattern = "Concrete",
                MaterialName = "Precast Concrete Coping",
                Description = $"Precast concrete coping ({cappingWidth}mm wide) with throating"
            });

            // Flat roof build-up
            elements.AddRange(GenerateFlatRoofBuildUp(roofAssembly));

            // Upstand and flashing
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.Line,
                Layer = "Flashing",
                LineStyle = LineStyle.Thick,
                MaterialName = "Lead Flashing (Code 4)",
                Description = "Code 4 lead flashing with 150mm upstand"
            });

            return await Task.FromResult(elements);
        }

        private async Task<List<DetailElement>> GenerateFlatRoofFasciaAsync(
            WallAssembly wallAssembly,
            RoofAssembly roofAssembly)
        {
            var elements = new List<DetailElement>();

            elements.AddRange(GenerateFlatRoofBuildUp(roofAssembly));

            // Fascia/edge trim
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Metal",
                HatchPattern = "Steel",
                MaterialName = "Aluminium Edge Trim",
                Description = "Aluminium edge trim with drip detail"
            });

            return await Task.FromResult(elements);
        }

        private async Task<List<DetailElement>> GenerateMonoPitchRoofAsync(
            WallAssembly wallAssembly,
            RoofAssembly roofAssembly)
        {
            var elements = new List<DetailElement>();

            // Similar to pitched but single slope
            elements.AddRange(await GeneratePitchedRoofEaveAsync(wallAssembly, roofAssembly));

            return elements;
        }

        private async Task<List<DetailElement>> GenerateMansardRoofAsync(
            WallAssembly wallAssembly,
            RoofAssembly roofAssembly)
        {
            var elements = new List<DetailElement>();

            // Steep lower section
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Roofing",
                MaterialName = "Slate Tiles",
                Description = "Natural slate tiles on steep pitch (70°)",
                Rotation = 70
            });

            // Shallow upper section
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Roofing",
                MaterialName = "Lead Sheet (Code 5)",
                Description = "Code 5 lead sheet on shallow pitch (15°)",
                Rotation = 15
            });

            return await Task.FromResult(elements);
        }

        private List<DetailElement> GenerateFlatRoofBuildUp(RoofAssembly roofAssembly)
        {
            var elements = new List<DetailElement>();

            // Default flat roof build-up if no assembly provided
            var layers = roofAssembly?.Layers ?? new List<RoofLayer>
            {
                new RoofLayer { MaterialName = "Concrete Deck", Thickness = 150 },
                new RoofLayer { MaterialName = "Vapour Control Layer", Thickness = 1 },
                new RoofLayer { MaterialName = "PIR Insulation (tapered)", Thickness = 150 },
                new RoofLayer { MaterialName = "Single-Ply Membrane", Thickness = 1.5 }
            };

            foreach (var layer in layers)
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = "Roofing",
                    HatchPattern = GetHatchForMaterial(layer.MaterialName),
                    MaterialName = layer.MaterialName,
                    Description = $"{layer.MaterialName} ({layer.Thickness}mm)"
                });
            }

            return elements;
        }

        private async Task<List<DetailElement>> GenerateRoofEaveDetailAsync(RoofDetailRequest request)
        {
            return await GeneratePitchedRoofEaveAsync(request.WallAssembly, request.RoofAssembly);
        }

        private async Task<List<DetailElement>> GenerateRoofRidgeDetailAsync(RoofDetailRequest request)
        {
            var elements = new List<DetailElement>();

            // Ridge board
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Timber",
                HatchPattern = "Wood",
                MaterialName = "C24 Softwood Ridge Board (200x32mm)",
                Description = "200x32mm C24 softwood ridge board"
            });

            // Ridge tile
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Roofing",
                HatchPattern = "Clay",
                MaterialName = "Clay Ridge Tile",
                Description = "Half-round clay ridge tile bedded on mortar"
            });

            // Rafters meeting at ridge
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Timber",
                HatchPattern = "Wood",
                MaterialName = "C24 Softwood Rafter",
                Description = "Rafters birdsmouthed to ridge board"
            });

            // Ridge ventilation if required
            if (request.IncludeVentilation)
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.Component,
                    Layer = "Ventilation",
                    MaterialName = "Dry Ridge Ventilation System",
                    Description = "Proprietary dry ridge ventilation system"
                });
            }

            return await Task.FromResult(elements);
        }

        private async Task<List<DetailElement>> GenerateRoofVergeDetailAsync(RoofDetailRequest request)
        {
            var elements = new List<DetailElement>();

            // Bargeboard
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Timber",
                HatchPattern = "Wood",
                MaterialName = "Treated Softwood Bargeboard (175x25mm)",
                Description = "175x25mm treated softwood bargeboard"
            });

            // Verge tile
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Roofing",
                MaterialName = "Verge Tile",
                Description = "Dry verge system with clip fixings"
            });

            return await Task.FromResult(elements);
        }

        private async Task<List<DetailElement>> GenerateRoofValleyDetailAsync(RoofDetailRequest request)
        {
            var elements = new List<DetailElement>();

            // Valley board
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Timber",
                HatchPattern = "Plywood",
                MaterialName = "WBP Plywood Valley Board (18mm)",
                Description = "18mm WBP plywood valley board"
            });

            // Valley gutter/lining
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Metal",
                HatchPattern = "Lead",
                MaterialName = "Lead Valley Lining (Code 5)",
                Description = "Code 5 lead valley lining with welted edges"
            });

            // Tile battens cut to valley
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Timber",
                HatchPattern = "Wood",
                MaterialName = "Treated Softwood Battens",
                Description = "Tile battens cut and fitted to valley"
            });

            return await Task.FromResult(elements);
        }

        private async Task<List<DetailElement>> GenerateRoofHipDetailAsync(RoofDetailRequest request)
        {
            var elements = new List<DetailElement>();

            // Hip rafter
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Timber",
                HatchPattern = "Wood",
                MaterialName = "C24 Softwood Hip Rafter (200x50mm)",
                Description = "200x50mm C24 softwood hip rafter"
            });

            // Hip tile
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Roofing",
                HatchPattern = "Clay",
                MaterialName = "Clay Hip Tile",
                Description = "Angular clay hip tile mechanically fixed"
            });

            // Jack rafters
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Timber",
                HatchPattern = "Wood",
                MaterialName = "C24 Softwood Jack Rafters",
                Description = "Jack rafters birdsmouthed to hip rafter"
            });

            return await Task.FromResult(elements);
        }

        private async Task<List<DetailElement>> GenerateFlatRoofEdgeDetailAsync(RoofDetailRequest request)
        {
            return await GenerateFlatRoofFasciaAsync(request.WallAssembly, request.RoofAssembly);
        }

        #endregion

        #region Window Detail Generation

        private async Task<List<DetailElement>> GenerateWindowJambDetailAsync(
            WindowDetailRequest request,
            WallContext wallContext)
        {
            var elements = new List<DetailElement>();
            var windowFrame = request.WindowType ?? WindowFrameType.uPVC;

            // Wall layers (cut through)
            foreach (var layer in wallContext.Layers)
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = GetLayerCategory(layer.Function),
                    HatchPattern = GetHatchForMaterial(layer.MaterialName),
                    MaterialName = layer.MaterialName,
                    Description = $"{layer.MaterialName} (jamb section)"
                });
            }

            // Window frame profile
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Window",
                HatchPattern = GetWindowFrameHatch(windowFrame),
                MaterialName = GetWindowFrameMaterial(windowFrame),
                Description = $"{windowFrame} window frame profile"
            });

            // Glazing
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Glazing",
                HatchPattern = "Glass",
                MaterialName = request.GlazingType ?? "Double glazed unit (4-16-4mm)",
                Description = "Double glazed sealed unit"
            });

            // DPC/cavity closer
            if (wallContext.HasCavity)
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = "DPC",
                    MaterialName = "Insulated Cavity Closer",
                    Description = "Proprietary insulated cavity closer"
                });
            }

            // Internal reveal finish
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Finish",
                HatchPattern = "Plaster",
                MaterialName = "Plaster Reveal",
                Description = "Plastered reveal with angle bead"
            });

            // Sealant
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.Line,
                Layer = "Sealant",
                LineStyle = LineStyle.Thin,
                MaterialName = "Silicone Sealant",
                Description = "Silicone sealant (internal and external)"
            });

            return await Task.FromResult(elements);
        }

        private async Task<List<DetailElement>> GenerateWindowHeadDetailAsync(
            WindowDetailRequest request,
            WallContext wallContext)
        {
            var elements = new List<DetailElement>();

            // Lintel
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Structure",
                HatchPattern = "Steel",
                MaterialName = request.LintelType ?? "Steel Lintel (Catnic CG90/100)",
                Description = "Cavity lintel supporting outer leaf and inner leaf"
            });

            // Wall layers above lintel
            foreach (var layer in wallContext.Layers)
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = GetLayerCategory(layer.Function),
                    HatchPattern = GetHatchForMaterial(layer.MaterialName),
                    MaterialName = layer.MaterialName,
                    Description = $"{layer.MaterialName} (above lintel)"
                });
            }

            // Window frame (head section)
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Window",
                HatchPattern = GetWindowFrameHatch(request.WindowType ?? WindowFrameType.uPVC),
                MaterialName = GetWindowFrameMaterial(request.WindowType ?? WindowFrameType.uPVC),
                Description = "Window frame head section"
            });

            // Glazing
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Glazing",
                HatchPattern = "Glass",
                MaterialName = "Double glazed unit",
                Description = "Double glazed sealed unit"
            });

            // Weep holes in lintel
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.Symbol,
                Layer = "Annotation",
                Description = "Weep holes at max 450mm centres"
            });

            return await Task.FromResult(elements);
        }

        private async Task<List<DetailElement>> GenerateWindowSillDetailAsync(
            WindowDetailRequest request,
            WallContext wallContext)
        {
            var elements = new List<DetailElement>();

            // External sill
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Stone",
                HatchPattern = "Stone",
                MaterialName = request.SillType ?? "Precast Concrete Sill",
                Description = "Precast concrete sill with throating and end dams"
            });

            // Wall layers below window
            foreach (var layer in wallContext.Layers)
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = GetLayerCategory(layer.Function),
                    HatchPattern = GetHatchForMaterial(layer.MaterialName),
                    MaterialName = layer.MaterialName,
                    Description = $"{layer.MaterialName} (below sill)"
                });
            }

            // DPC under sill
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.Line,
                Layer = "DPC",
                LineStyle = LineStyle.DashDot,
                MaterialName = "DPC",
                Description = "DPC under sill extending into cavity"
            });

            // Window frame (sill section)
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Window",
                MaterialName = GetWindowFrameMaterial(request.WindowType ?? WindowFrameType.uPVC),
                Description = "Window frame sill section with drainage"
            });

            // Internal window board
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Finish",
                HatchPattern = "MDF",
                MaterialName = "MDF Window Board (25mm)",
                Description = "25mm MDF window board with bullnose edge"
            });

            return await Task.FromResult(elements);
        }

        #endregion

        #region Door Detail Generation

        private async Task<List<DetailElement>> GenerateDoorJambDetailAsync(
            DoorDetailRequest request,
            WallContext wallContext)
        {
            var elements = new List<DetailElement>();

            // Wall layers
            foreach (var layer in wallContext.Layers)
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = GetLayerCategory(layer.Function),
                    HatchPattern = GetHatchForMaterial(layer.MaterialName),
                    MaterialName = layer.MaterialName
                });
            }

            // Door frame
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Door",
                HatchPattern = "Wood",
                MaterialName = request.FrameType ?? "Softwood Door Frame (95x70mm)",
                Description = "Door frame with planted stop"
            });

            // Door leaf
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Door",
                HatchPattern = "Wood",
                MaterialName = request.DoorType ?? "44mm Solid Core Door",
                Description = "44mm solid core door leaf"
            });

            // Architrave
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Finish",
                HatchPattern = "MDF",
                MaterialName = "MDF Architrave (70x18mm)",
                Description = "70x18mm MDF architrave"
            });

            // Seals/weatherstripping for external doors
            if (request.IsExternalDoor)
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.Line,
                    Layer = "Seal",
                    MaterialName = "Weatherseal",
                    Description = "Compression weatherseal"
                });
            }

            return await Task.FromResult(elements);
        }

        private async Task<List<DetailElement>> GenerateDoorHeadDetailAsync(
            DoorDetailRequest request,
            WallContext wallContext)
        {
            var elements = new List<DetailElement>();

            // Lintel
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Structure",
                HatchPattern = request.IsExternalDoor ? "Steel" : "Concrete",
                MaterialName = request.IsExternalDoor ? "Steel Lintel" : "Precast Concrete Lintel",
                Description = "Lintel over door opening"
            });

            // Wall above
            foreach (var layer in wallContext.Layers)
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = GetLayerCategory(layer.Function),
                    HatchPattern = GetHatchForMaterial(layer.MaterialName),
                    MaterialName = layer.MaterialName
                });
            }

            // Door frame head
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Door",
                HatchPattern = "Wood",
                MaterialName = "Door Frame Head",
                Description = "Door frame head with planted stop"
            });

            // Door leaf (shown in section)
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Door",
                HatchPattern = "Wood",
                MaterialName = "Door Leaf",
                Description = "Door leaf section"
            });

            return await Task.FromResult(elements);
        }

        private async Task<List<DetailElement>> GenerateDoorThresholdDetailAsync(
            DoorDetailRequest request,
            WallContext wallContext)
        {
            var elements = new List<DetailElement>();

            if (request.IsExternalDoor)
            {
                // External threshold
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = "Metal",
                    HatchPattern = "Aluminium",
                    MaterialName = "Aluminium Threshold",
                    Description = "Low threshold with weather bar and drainage"
                });

                // DPC at threshold
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.Line,
                    Layer = "DPC",
                    LineStyle = LineStyle.DashDot,
                    MaterialName = "DPC",
                    Description = "DPC at threshold level"
                });

                // Drainage channel
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.Line,
                    Layer = "Drainage",
                    MaterialName = "Linear Drain",
                    Description = "Linear drainage channel"
                });
            }
            else
            {
                // Internal threshold/saddle
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = "Finish",
                    HatchPattern = "Wood",
                    MaterialName = "Hardwood Threshold Strip",
                    Description = "Hardwood threshold/saddle strip"
                });
            }

            // Floor buildup either side
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Floor",
                HatchPattern = "Screed",
                MaterialName = "Floor Finish",
                Description = "Floor finish to threshold"
            });

            return await Task.FromResult(elements);
        }

        #endregion

        #region Floor Junction Detail Generation

        private async Task<List<DetailElement>> GenerateFloorJunctionDetailAsync(
            FloorAssembly floorAssembly,
            WallAssembly wallAssembly)
        {
            var elements = new List<DetailElement>();

            // Floor slab/deck
            foreach (var layer in floorAssembly?.Layers ?? GetDefaultFloorLayers())
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = "Floor",
                    HatchPattern = GetHatchForMaterial(layer.MaterialName),
                    MaterialName = layer.MaterialName,
                    Description = $"{layer.MaterialName} ({layer.Thickness}mm)"
                });
            }

            // Wall continuation
            foreach (var layer in wallAssembly.Layers)
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = GetLayerCategory(layer.Function),
                    HatchPattern = GetHatchForMaterial(layer.MaterialName),
                    MaterialName = layer.MaterialName
                });
            }

            // Floor/wall junction detail
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.Line,
                Layer = "Sealant",
                MaterialName = "Fire Sealant",
                Description = "Intumescent fire sealant at junction"
            });

            return await Task.FromResult(elements);
        }

        private async Task<List<DetailElement>> GenerateWallFloorJunctionAsync(
            WallAssembly wallAssembly,
            FloorAssembly floorAssembly,
            JunctionType junctionType)
        {
            var elements = new List<DetailElement>();

            switch (junctionType)
            {
                case JunctionType.Supported:
                    // Wall supports floor
                    elements.Add(new DetailElement
                    {
                        ElementType = DetailElementType.FilledRegion,
                        Layer = "Structure",
                        MaterialName = "Floor bearing on wall",
                        Description = "Floor bearing min 90mm on inner leaf"
                    });
                    break;

                case JunctionType.Bypassing:
                    // Wall bypasses floor (balloon frame)
                    elements.Add(new DetailElement
                    {
                        ElementType = DetailElementType.FilledRegion,
                        Layer = "Structure",
                        MaterialName = "Floor joist hangers",
                        Description = "Floor joists on hangers, wall continuous"
                    });
                    break;

                case JunctionType.Interrupted:
                    // Floor interrupts wall
                    elements.Add(new DetailElement
                    {
                        ElementType = DetailElementType.FilledRegion,
                        Layer = "Structure",
                        MaterialName = "Floor slab",
                        Description = "Floor slab between wall sections"
                    });
                    break;
            }

            elements.AddRange(await GenerateFloorJunctionDetailAsync(floorAssembly, wallAssembly));

            return elements;
        }

        private async Task<List<DetailElement>> GenerateFireStoppingDetailAsync(FloorJunctionRequest request)
        {
            var elements = new List<DetailElement>();

            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "FireStopping",
                HatchPattern = "Insulation",
                MaterialName = "Mineral Wool Fire Stop",
                Description = "Mineral wool fire stopping in cavity"
            });

            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.Line,
                Layer = "FireStopping",
                MaterialName = "Intumescent Sealant",
                Description = "Intumescent fire sealant at junction"
            });

            return await Task.FromResult(elements);
        }

        private async Task<List<DetailElement>> GenerateAcousticSeparationDetailAsync(FloorJunctionRequest request)
        {
            var elements = new List<DetailElement>();

            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.FilledRegion,
                Layer = "Acoustic",
                HatchPattern = "Insulation",
                MaterialName = "Acoustic Mineral Wool",
                Description = "Acoustic mineral wool in floor void"
            });

            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.Line,
                Layer = "Acoustic",
                MaterialName = "Resilient Strip",
                Description = "Resilient strip under wall sole plate"
            });

            return await Task.FromResult(elements);
        }

        #endregion

        #region Waterproofing Detail Generation

        private async Task<List<DetailElement>> GenerateWaterproofingDetailAsync(WallSectionRequest request)
        {
            var elements = new List<DetailElement>();

            // Below ground waterproofing
            if (request.BelowGroundWaterproofing)
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.Line,
                    Layer = "Waterproofing",
                    LineStyle = LineStyle.Thick,
                    MaterialName = "Tanking Membrane",
                    Description = "Cementitious tanking membrane to basement wall"
                });

                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.FilledRegion,
                    Layer = "Drainage",
                    HatchPattern = "Gravel",
                    MaterialName = "Drainage Layer",
                    Description = "Drainage board with geotextile filter"
                });

                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.Line,
                    Layer = "Drainage",
                    MaterialName = "Land Drain",
                    Description = "Perforated land drain in pea gravel"
                });
            }

            // Above ground moisture protection
            elements.Add(new DetailElement
            {
                ElementType = DetailElementType.Line,
                Layer = "DPC",
                LineStyle = LineStyle.DashDot,
                MaterialName = "DPC",
                Description = "DPC min 150mm above finished ground level"
            });

            // Cavity tray at vulnerable positions
            if (request.WallAssembly.Layers.Any(l => l.Function == LayerFunction.AirGap))
            {
                elements.Add(new DetailElement
                {
                    ElementType = DetailElementType.Line,
                    Layer = "DPC",
                    LineStyle = LineStyle.DashDot,
                    MaterialName = "Cavity Tray",
                    Description = "Cavity tray with stop ends and weep holes"
                });
            }

            return await Task.FromResult(elements);
        }

        #endregion

        #region Dimension and Annotation Generation

        private List<DetailDimension> GenerateWallSectionDimensions(
            List<DetailElement> elements,
            WallSectionRequest request)
        {
            var dimensions = new List<DetailDimension>();

            // Overall wall thickness
            dimensions.Add(new DetailDimension
            {
                DimensionType = DimensionType.Linear,
                Value = request.WallAssembly.TotalThickness,
                Units = "mm",
                Label = "Wall thickness",
                Position = DimensionPosition.Left
            });

            // Individual layer thicknesses
            foreach (var layer in request.WallAssembly.Layers)
            {
                dimensions.Add(new DetailDimension
                {
                    DimensionType = DimensionType.Linear,
                    Value = layer.Thickness,
                    Units = "mm",
                    Label = layer.MaterialName,
                    Position = DimensionPosition.Right
                });
            }

            // Heights
            dimensions.Add(new DetailDimension
            {
                DimensionType = DimensionType.Linear,
                Value = request.WallHeight,
                Units = "mm",
                Label = "Floor to floor height",
                Position = DimensionPosition.Right
            });

            // DPC height
            dimensions.Add(new DetailDimension
            {
                DimensionType = DimensionType.Linear,
                Value = 150,
                Units = "mm",
                Label = "DPC above ground",
                Position = DimensionPosition.Left
            });

            return dimensions;
        }

        private List<DetailAnnotation> GenerateWallSectionAnnotations(
            List<DetailElement> elements,
            WallSectionRequest request)
        {
            var annotations = new List<DetailAnnotation>();

            // Material keynotes
            foreach (var element in elements.Where(e => !string.IsNullOrEmpty(e.MaterialName)))
            {
                annotations.Add(new DetailAnnotation
                {
                    AnnotationType = AnnotationType.MaterialKeynote,
                    Text = element.MaterialName,
                    Description = element.Description
                });
            }

            // General notes
            annotations.Add(new DetailAnnotation
            {
                AnnotationType = AnnotationType.GeneralNote,
                Text = "All dimensions in millimetres unless noted otherwise"
            });

            annotations.Add(new DetailAnnotation
            {
                AnnotationType = AnnotationType.GeneralNote,
                Text = $"Scale {request.Scale}"
            });

            // Standards reference
            annotations.Add(new DetailAnnotation
            {
                AnnotationType = AnnotationType.StandardReference,
                Text = "Comply with Building Regulations Part L and Part E"
            });

            return annotations;
        }

        private List<DetailDimension> GenerateWindowDimensions(
            List<DetailElement> elements,
            WindowDetailRequest request)
        {
            var dimensions = new List<DetailDimension>();

            dimensions.Add(new DetailDimension
            {
                DimensionType = DimensionType.Linear,
                Value = request.RevealDepth ?? 100,
                Units = "mm",
                Label = "Reveal depth"
            });

            dimensions.Add(new DetailDimension
            {
                DimensionType = DimensionType.Linear,
                Value = 10,
                Units = "mm",
                Label = "Sealant gap"
            });

            return dimensions;
        }

        private List<DetailAnnotation> GenerateWindowAnnotations(
            List<DetailElement> elements,
            WindowDetailRequest request)
        {
            var annotations = new List<DetailAnnotation>();

            foreach (var element in elements.Where(e => !string.IsNullOrEmpty(e.MaterialName)))
            {
                annotations.Add(new DetailAnnotation
                {
                    AnnotationType = AnnotationType.MaterialKeynote,
                    Text = element.MaterialName,
                    Description = element.Description
                });
            }

            return annotations;
        }

        private List<DetailDimension> GenerateDoorDimensions(
            List<DetailElement> elements,
            DoorDetailRequest request)
        {
            return new List<DetailDimension>
            {
                new DetailDimension
                {
                    DimensionType = DimensionType.Linear,
                    Value = 44,
                    Units = "mm",
                    Label = "Door thickness"
                },
                new DetailDimension
                {
                    DimensionType = DimensionType.Linear,
                    Value = 3,
                    Units = "mm",
                    Label = "Door gap"
                }
            };
        }

        private List<DetailAnnotation> GenerateDoorAnnotations(
            List<DetailElement> elements,
            DoorDetailRequest request)
        {
            var annotations = new List<DetailAnnotation>();

            foreach (var element in elements.Where(e => !string.IsNullOrEmpty(e.MaterialName)))
            {
                annotations.Add(new DetailAnnotation
                {
                    AnnotationType = AnnotationType.MaterialKeynote,
                    Text = element.MaterialName,
                    Description = element.Description
                });
            }

            if (request.IsExternalDoor)
            {
                annotations.Add(new DetailAnnotation
                {
                    AnnotationType = AnnotationType.GeneralNote,
                    Text = "External door to achieve U-value ≤ 1.0 W/m²K"
                });
            }

            return annotations;
        }

        private List<DetailDimension> GenerateRoofDimensions(
            List<DetailElement> elements,
            RoofDetailRequest request)
        {
            return new List<DetailDimension>
            {
                new DetailDimension
                {
                    DimensionType = DimensionType.Angular,
                    Value = request.RoofAssembly?.Pitch ?? 30,
                    Units = "°",
                    Label = "Roof pitch"
                },
                new DetailDimension
                {
                    DimensionType = DimensionType.Linear,
                    Value = request.RoofAssembly?.EaveOverhang ?? 450,
                    Units = "mm",
                    Label = "Eave overhang"
                }
            };
        }

        private List<DetailAnnotation> GenerateRoofAnnotations(
            List<DetailElement> elements,
            RoofDetailRequest request)
        {
            var annotations = new List<DetailAnnotation>();

            foreach (var element in elements.Where(e => !string.IsNullOrEmpty(e.MaterialName)))
            {
                annotations.Add(new DetailAnnotation
                {
                    AnnotationType = AnnotationType.MaterialKeynote,
                    Text = element.MaterialName,
                    Description = element.Description
                });
            }

            annotations.Add(new DetailAnnotation
            {
                AnnotationType = AnnotationType.GeneralNote,
                Text = "Maintain continuous ventilation path from eave to ridge"
            });

            return annotations;
        }

        private List<DetailDimension> GenerateFloorJunctionDimensions(
            List<DetailElement> elements,
            FloorJunctionRequest request)
        {
            return new List<DetailDimension>
            {
                new DetailDimension
                {
                    DimensionType = DimensionType.Linear,
                    Value = 90,
                    Units = "mm",
                    Label = "Min bearing"
                }
            };
        }

        private List<DetailAnnotation> GenerateFloorJunctionAnnotations(
            List<DetailElement> elements,
            FloorJunctionRequest request)
        {
            var annotations = new List<DetailAnnotation>();

            if (request.IncludeFireStopping)
            {
                annotations.Add(new DetailAnnotation
                {
                    AnnotationType = AnnotationType.GeneralNote,
                    Text = "Fire stopping to achieve compartmentation requirements"
                });
            }

            if (request.IncludeAcousticSeparation)
            {
                annotations.Add(new DetailAnnotation
                {
                    AnnotationType = AnnotationType.GeneralNote,
                    Text = "Acoustic detailing to achieve Part E requirements"
                });
            }

            return annotations;
        }

        #endregion

        #region Helper Methods

        private async Task<WallContext> AnalyzeWallContextAsync(WallAssembly wallAssembly)
        {
            var context = new WallContext
            {
                Layers = wallAssembly.Layers,
                TotalThickness = wallAssembly.TotalThickness,
                HasCavity = wallAssembly.Layers.Any(l => l.Function == LayerFunction.AirGap),
                IsExterior = wallAssembly.IsExterior
            };

            return await Task.FromResult(context);
        }

        private async Task<DetailResult> GenerateDetailByTypeAsync(
            string detailType,
            WallAssembly wallAssembly,
            AutoDetailOptions options)
        {
            switch (detailType)
            {
                case "WallSection":
                    return await GenerateWallSectionDetailAsync(new WallSectionRequest
                    {
                        DetailName = $"{wallAssembly.TypeName} - Wall Section",
                        WallAssembly = wallAssembly,
                        WallHeight = options.DefaultWallHeight,
                        IncludeFoundation = options.IncludeFoundation,
                        IncludeRoof = options.IncludeRoofJunction,
                        Scale = options.DefaultScale
                    });

                case "WindowJamb":
                case "WindowHead":
                case "WindowSill":
                    return await GenerateWindowDetailAsync(new WindowDetailRequest
                    {
                        DetailName = $"{wallAssembly.TypeName} - {detailType}",
                        WallAssembly = wallAssembly,
                        DetailView = Enum.Parse<WindowDetailView>(detailType.Replace("Window", "")),
                        Scale = options.DefaultScale
                    });

                case "DoorJamb":
                case "DoorHead":
                case "DoorThreshold":
                    return await GenerateDoorDetailAsync(new DoorDetailRequest
                    {
                        DetailName = $"{wallAssembly.TypeName} - {detailType}",
                        WallAssembly = wallAssembly,
                        DetailView = Enum.Parse<DoorDetailView>(detailType.Replace("Door", "")),
                        Scale = options.DefaultScale
                    });

                default:
                    return new DetailResult { IsSuccess = false, ErrorMessage = $"Unknown detail type: {detailType}" };
            }
        }

        private string GetLayerCategory(LayerFunction function)
        {
            return function switch
            {
                LayerFunction.Structure => "Structure",
                LayerFunction.Substrate => "Substrate",
                LayerFunction.ThermalInsulation => "Insulation",
                LayerFunction.AirGap => "Cavity",
                LayerFunction.Finish => "Finish",
                LayerFunction.Membrane => "Membrane",
                _ => "General"
            };
        }

        private string GetHatchForMaterial(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return "Solid";

            var lowerName = materialName.ToLower();

            if (lowerName.Contains("concrete")) return "Concrete";
            if (lowerName.Contains("brick")) return "Brick";
            if (lowerName.Contains("block")) return "CMU";
            if (lowerName.Contains("timber") || lowerName.Contains("wood")) return "Wood";
            if (lowerName.Contains("steel") || lowerName.Contains("metal")) return "Steel";
            if (lowerName.Contains("insulation") || lowerName.Contains("wool")) return "Insulation";
            if (lowerName.Contains("plaster") || lowerName.Contains("render")) return "Plaster";
            if (lowerName.Contains("stone")) return "Stone";
            if (lowerName.Contains("glass")) return "Glass";
            if (lowerName.Contains("membrane") || lowerName.Contains("dpc")) return "Membrane";
            if (lowerName.Contains("gravel") || lowerName.Contains("hardcore")) return "Gravel";
            if (lowerName.Contains("sand")) return "Sand";
            if (lowerName.Contains("earth") || lowerName.Contains("soil")) return "Earth";

            return "Solid";
        }

        private string GetWindowFrameHatch(WindowFrameType frameType)
        {
            return frameType switch
            {
                WindowFrameType.Timber => "Wood",
                WindowFrameType.Aluminium => "Aluminium",
                WindowFrameType.Steel => "Steel",
                WindowFrameType.uPVC => "Plastic",
                WindowFrameType.Composite => "Composite",
                _ => "Solid"
            };
        }

        private string GetWindowFrameMaterial(WindowFrameType frameType)
        {
            return frameType switch
            {
                WindowFrameType.Timber => "Hardwood Window Frame",
                WindowFrameType.Aluminium => "Aluminium Window Frame (thermally broken)",
                WindowFrameType.Steel => "Steel Window Frame",
                WindowFrameType.uPVC => "uPVC Window Frame",
                WindowFrameType.Composite => "Composite Window Frame",
                _ => "Window Frame"
            };
        }

        private List<RoofLayer> GetDefaultRoofLayers()
        {
            return new List<RoofLayer>
            {
                new RoofLayer { MaterialName = "Roof Tiles", Thickness = 15 },
                new RoofLayer { MaterialName = "Tile Battens", Thickness = 25 },
                new RoofLayer { MaterialName = "Counter Battens", Thickness = 25 },
                new RoofLayer { MaterialName = "Breathable Membrane", Thickness = 1 },
                new RoofLayer { MaterialName = "Rafter Insulation", Thickness = 150 }
            };
        }

        private List<FloorLayer> GetDefaultFloorLayers()
        {
            return new List<FloorLayer>
            {
                new FloorLayer { MaterialName = "Floor Finish", Thickness = 15 },
                new FloorLayer { MaterialName = "Screed", Thickness = 65 },
                new FloorLayer { MaterialName = "Insulation", Thickness = 100 },
                new FloorLayer { MaterialName = "Concrete Slab", Thickness = 150 }
            };
        }

        #endregion

        #region Template and Library Initialization

        private Dictionary<string, DetailTemplate> InitializeDetailTemplates()
        {
            return new Dictionary<string, DetailTemplate>
            {
                // Wall section templates
                ["WallSection_Cavity"] = new DetailTemplate
                {
                    TemplateName = "Cavity Wall Section",
                    DetailType = DetailType.WallSection,
                    DefaultScale = "1:5",
                    Description = "Standard cavity wall section from foundation to roof"
                },
                ["WallSection_Solid"] = new DetailTemplate
                {
                    TemplateName = "Solid Wall Section",
                    DetailType = DetailType.WallSection,
                    DefaultScale = "1:5"
                },
                ["WallSection_Timber"] = new DetailTemplate
                {
                    TemplateName = "Timber Frame Wall Section",
                    DetailType = DetailType.WallSection,
                    DefaultScale = "1:5"
                },
                ["WallSection_SIP"] = new DetailTemplate
                {
                    TemplateName = "SIP Wall Section",
                    DetailType = DetailType.WallSection,
                    DefaultScale = "1:5"
                },

                // Window templates
                ["Window_Jamb_Cavity"] = new DetailTemplate
                {
                    TemplateName = "Window Jamb - Cavity Wall",
                    DetailType = DetailType.WindowDetail,
                    DefaultScale = "1:2"
                },
                ["Window_Head_Cavity"] = new DetailTemplate
                {
                    TemplateName = "Window Head - Cavity Wall",
                    DetailType = DetailType.WindowDetail,
                    DefaultScale = "1:2"
                },
                ["Window_Sill_Cavity"] = new DetailTemplate
                {
                    TemplateName = "Window Sill - Cavity Wall",
                    DetailType = DetailType.WindowDetail,
                    DefaultScale = "1:2"
                },

                // Door templates
                ["Door_Jamb_Int"] = new DetailTemplate
                {
                    TemplateName = "Internal Door Jamb",
                    DetailType = DetailType.DoorDetail,
                    DefaultScale = "1:2"
                },
                ["Door_Jamb_Ext"] = new DetailTemplate
                {
                    TemplateName = "External Door Jamb",
                    DetailType = DetailType.DoorDetail,
                    DefaultScale = "1:2"
                },
                ["Door_Threshold_Ext"] = new DetailTemplate
                {
                    TemplateName = "External Door Threshold",
                    DetailType = DetailType.DoorDetail,
                    DefaultScale = "1:2"
                },

                // Roof templates
                ["Roof_Eave_Pitched"] = new DetailTemplate
                {
                    TemplateName = "Pitched Roof Eave",
                    DetailType = DetailType.RoofDetail,
                    DefaultScale = "1:5"
                },
                ["Roof_Ridge"] = new DetailTemplate
                {
                    TemplateName = "Roof Ridge Detail",
                    DetailType = DetailType.RoofDetail,
                    DefaultScale = "1:5"
                },
                ["Roof_Verge"] = new DetailTemplate
                {
                    TemplateName = "Roof Verge Detail",
                    DetailType = DetailType.RoofDetail,
                    DefaultScale = "1:5"
                },
                ["Roof_Flat_Parapet"] = new DetailTemplate
                {
                    TemplateName = "Flat Roof Parapet",
                    DetailType = DetailType.RoofDetail,
                    DefaultScale = "1:5"
                },

                // Floor junction templates
                ["Floor_Junction_Intermediate"] = new DetailTemplate
                {
                    TemplateName = "Intermediate Floor Junction",
                    DetailType = DetailType.FloorJunction,
                    DefaultScale = "1:5"
                },
                ["Floor_Junction_Ground"] = new DetailTemplate
                {
                    TemplateName = "Ground Floor Junction",
                    DetailType = DetailType.FloorJunction,
                    DefaultScale = "1:5"
                }
            };
        }

        private Dictionary<string, LayerDefinition> InitializeLayerLibrary()
        {
            return new Dictionary<string, LayerDefinition>
            {
                // Structural layers
                ["Concrete-Foundation"] = new LayerDefinition { Name = "Concrete-Foundation", Color = System.Drawing.Color.Gray, LineWeight = 2 },
                ["Concrete-Slab"] = new LayerDefinition { Name = "Concrete-Slab", Color = System.Drawing.Color.Gray, LineWeight = 2 },
                ["Structure"] = new LayerDefinition { Name = "Structure", Color = System.Drawing.Color.DarkGray, LineWeight = 2 },
                ["Reinforcement"] = new LayerDefinition { Name = "Reinforcement", Color = System.Drawing.Color.Red, LineWeight = 1 },

                // Wall layers
                ["Masonry"] = new LayerDefinition { Name = "Masonry", Color = System.Drawing.Color.Brown, LineWeight = 2 },
                ["Insulation"] = new LayerDefinition { Name = "Insulation", Color = System.Drawing.Color.Yellow, LineWeight = 1 },
                ["Cavity"] = new LayerDefinition { Name = "Cavity", Color = System.Drawing.Color.White, LineWeight = 1 },
                ["Finish"] = new LayerDefinition { Name = "Finish", Color = System.Drawing.Color.LightGray, LineWeight = 1 },

                // Timber
                ["Timber"] = new LayerDefinition { Name = "Timber", Color = System.Drawing.Color.SandyBrown, LineWeight = 2 },

                // Roofing
                ["Roofing"] = new LayerDefinition { Name = "Roofing", Color = System.Drawing.Color.DarkRed, LineWeight = 2 },
                ["Soffit"] = new LayerDefinition { Name = "Soffit", Color = System.Drawing.Color.Tan, LineWeight = 1 },

                // Openings
                ["Window"] = new LayerDefinition { Name = "Window", Color = System.Drawing.Color.Blue, LineWeight = 2 },
                ["Door"] = new LayerDefinition { Name = "Door", Color = System.Drawing.Color.SaddleBrown, LineWeight = 2 },
                ["Glazing"] = new LayerDefinition { Name = "Glazing", Color = System.Drawing.Color.LightBlue, LineWeight = 1 },

                // Waterproofing
                ["DPC"] = new LayerDefinition { Name = "DPC", Color = System.Drawing.Color.Black, LineWeight = 1 },
                ["Waterproofing"] = new LayerDefinition { Name = "Waterproofing", Color = System.Drawing.Color.DarkBlue, LineWeight = 1 },
                ["Flashing"] = new LayerDefinition { Name = "Flashing", Color = System.Drawing.Color.Silver, LineWeight = 1 },

                // Ground
                ["Ground"] = new LayerDefinition { Name = "Ground", Color = System.Drawing.Color.DarkGreen, LineWeight = 2 },
                ["Hardcore"] = new LayerDefinition { Name = "Hardcore", Color = System.Drawing.Color.DarkGray, LineWeight = 1 },
                ["Drainage"] = new LayerDefinition { Name = "Drainage", Color = System.Drawing.Color.Cyan, LineWeight = 1 },

                // Fire and acoustic
                ["FireStopping"] = new LayerDefinition { Name = "FireStopping", Color = System.Drawing.Color.OrangeRed, LineWeight = 2 },
                ["Acoustic"] = new LayerDefinition { Name = "Acoustic", Color = System.Drawing.Color.Purple, LineWeight = 1 },

                // Annotation
                ["Annotation"] = new LayerDefinition { Name = "Annotation", Color = System.Drawing.Color.Black, LineWeight = 1 },
                ["Dimension"] = new LayerDefinition { Name = "Dimension", Color = System.Drawing.Color.DarkBlue, LineWeight = 1 }
            };
        }

        private Dictionary<string, MaterialHatch> InitializeHatchPatterns()
        {
            return new Dictionary<string, MaterialHatch>
            {
                ["Concrete"] = new MaterialHatch { PatternName = "Concrete", Scale = 1.0, Angle = 45 },
                ["Brick"] = new MaterialHatch { PatternName = "Brick", Scale = 1.0, Angle = 0 },
                ["CMU"] = new MaterialHatch { PatternName = "CMU", Scale = 1.0, Angle = 0 },
                ["Wood"] = new MaterialHatch { PatternName = "Wood", Scale = 1.0, Angle = 0 },
                ["Steel"] = new MaterialHatch { PatternName = "Steel", Scale = 0.5, Angle = 45 },
                ["Aluminium"] = new MaterialHatch { PatternName = "Aluminium", Scale = 0.5, Angle = 45 },
                ["Insulation"] = new MaterialHatch { PatternName = "Insulation", Scale = 1.0, Angle = 0 },
                ["Plaster"] = new MaterialHatch { PatternName = "Sand", Scale = 0.5, Angle = 0 },
                ["Stone"] = new MaterialHatch { PatternName = "Stone", Scale = 1.0, Angle = 0 },
                ["Glass"] = new MaterialHatch { PatternName = "Glass", Scale = 1.0, Angle = 45 },
                ["Membrane"] = new MaterialHatch { PatternName = "Solid", Scale = 1.0, Angle = 0 },
                ["Gravel"] = new MaterialHatch { PatternName = "Gravel", Scale = 1.0, Angle = 0 },
                ["Sand"] = new MaterialHatch { PatternName = "Sand", Scale = 1.0, Angle = 0 },
                ["Earth"] = new MaterialHatch { PatternName = "Earth", Scale = 1.0, Angle = 0 },
                ["Lead"] = new MaterialHatch { PatternName = "Metal", Scale = 0.5, Angle = 45 },
                ["Plywood"] = new MaterialHatch { PatternName = "Plywood", Scale = 1.0, Angle = 0 },
                ["MDF"] = new MaterialHatch { PatternName = "Wood", Scale = 0.5, Angle = 90 },
                ["Clay"] = new MaterialHatch { PatternName = "Clay", Scale = 1.0, Angle = 0 },
                ["Plastic"] = new MaterialHatch { PatternName = "Plastic", Scale = 1.0, Angle = 0 },
                ["Composite"] = new MaterialHatch { PatternName = "Composite", Scale = 1.0, Angle = 45 },
                ["Solid"] = new MaterialHatch { PatternName = "Solid", Scale = 1.0, Angle = 0 },
                ["Screed"] = new MaterialHatch { PatternName = "Sand", Scale = 0.75, Angle = 0 }
            };
        }

        #endregion
    }

    #region Data Models

    public class DetailGeneratorSettings
    {
        public string DefaultScale { get; set; } = "1:5";
        public bool AutoAnnotate { get; set; } = true;
        public bool AutoDimension { get; set; } = true;
        public string OutputFormat { get; set; } = "Revit";
        public bool IncludeKeynotes { get; set; } = true;
    }

    public class DetailResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public DetailType DetailType { get; set; }
        public string DetailName { get; set; }
        public string Scale { get; set; }
        public List<DetailElement> Elements { get; set; }
        public List<DetailAnnotation> Annotations { get; set; }
        public List<DetailDimension> Dimensions { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class DetailElement
    {
        public DetailElementType ElementType { get; set; }
        public string Layer { get; set; }
        public string HatchPattern { get; set; }
        public LineStyle LineStyle { get; set; }
        public string MaterialName { get; set; }
        public string Description { get; set; }
        public DetailGeometry Geometry { get; set; }
        public double Rotation { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    public class DetailGeometry
    {
        public List<Point2D> Points { get; set; }
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
        public Point2D Center { get; set; }
        public double Radius { get; set; }
        public double Height { get; set; }
        public double Diameter { get; set; }
    }

    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point2D() { }
        public Point2D(double x, double y) { X = x; Y = y; }
    }

    public class DetailAnnotation
    {
        public AnnotationType AnnotationType { get; set; }
        public string Text { get; set; }
        public string Description { get; set; }
        public Point2D Position { get; set; }
    }

    public class DetailDimension
    {
        public DimensionType DimensionType { get; set; }
        public double Value { get; set; }
        public string Units { get; set; }
        public string Label { get; set; }
        public DimensionPosition Position { get; set; }
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
    }

    public class DetailTemplate
    {
        public string TemplateName { get; set; }
        public DetailType DetailType { get; set; }
        public string DefaultScale { get; set; }
        public string Description { get; set; }
        public List<string> RequiredLayers { get; set; }
    }

    public class LayerDefinition
    {
        public string Name { get; set; }
        public System.Drawing.Color Color { get; set; }
        public int LineWeight { get; set; }
    }

    public class MaterialHatch
    {
        public string PatternName { get; set; }
        public double Scale { get; set; }
        public double Angle { get; set; }
    }

    public class DetailProgress
    {
        public string Stage { get; set; }
        public int Percentage { get; set; }
    }

    #region Request Models

    public class WallSectionRequest
    {
        public string DetailName { get; set; }
        public WallAssembly WallAssembly { get; set; }
        public double WallHeight { get; set; } = 2700;
        public bool IncludeFoundation { get; set; } = true;
        public FoundationType FoundationType { get; set; } = FoundationType.StripFoundation;
        public bool IncludeFloorJunction { get; set; }
        public FloorAssembly FloorAssembly { get; set; }
        public bool IncludeRoof { get; set; } = true;
        public RoofType RoofType { get; set; } = RoofType.PitchedWithEave;
        public RoofAssembly RoofAssembly { get; set; }
        public bool BelowGroundWaterproofing { get; set; }
        public string Scale { get; set; } = "1:5";
    }

    public class WindowDetailRequest
    {
        public string DetailName { get; set; }
        public WallAssembly WallAssembly { get; set; }
        public WindowDetailView DetailView { get; set; }
        public WindowFrameType? WindowType { get; set; }
        public string GlazingType { get; set; }
        public string LintelType { get; set; }
        public string SillType { get; set; }
        public double? RevealDepth { get; set; }
        public string Scale { get; set; } = "1:2";
    }

    public class DoorDetailRequest
    {
        public string DetailName { get; set; }
        public WallAssembly WallAssembly { get; set; }
        public DoorDetailView DetailView { get; set; }
        public string FrameType { get; set; }
        public string DoorType { get; set; }
        public bool IsExternalDoor { get; set; }
        public string Scale { get; set; } = "1:2";
    }

    public class RoofDetailRequest
    {
        public string DetailName { get; set; }
        public WallAssembly WallAssembly { get; set; }
        public RoofAssembly RoofAssembly { get; set; }
        public RoofDetailView DetailView { get; set; }
        public bool IncludeVentilation { get; set; } = true;
        public string Scale { get; set; } = "1:5";
    }

    public class FloorJunctionRequest
    {
        public string DetailName { get; set; }
        public WallAssembly WallAssembly { get; set; }
        public FloorAssembly FloorAssembly { get; set; }
        public JunctionType JunctionType { get; set; }
        public bool IncludeFireStopping { get; set; }
        public bool IncludeAcousticSeparation { get; set; }
        public string Scale { get; set; } = "1:5";
    }

    public class AutoDetailOptions
    {
        public bool IncludeWallSection { get; set; } = true;
        public bool IncludeFoundation { get; set; } = true;
        public bool IncludeRoofJunction { get; set; } = true;
        public bool IncludeFloorJunction { get; set; } = true;
        public bool IncludeOpeningDetails { get; set; } = true;
        public double DefaultWallHeight { get; set; } = 2700;
        public string DefaultScale { get; set; } = "1:5";
    }

    #endregion

    #region Assembly Models

    public class WallAssembly
    {
        public string TypeName { get; set; }
        public List<WallLayer> Layers { get; set; } = new List<WallLayer>();
        public double TotalThickness => Layers?.Sum(l => l.Thickness) ?? 0;
        public bool IsExterior { get; set; }
    }

    public class WallLayer
    {
        public int LayerOrder { get; set; }
        public string MaterialName { get; set; }
        public double Thickness { get; set; }
        public LayerFunction Function { get; set; }
        public double ThermalConductivity { get; set; }
    }

    public class RoofAssembly
    {
        public string TypeName { get; set; }
        public List<RoofLayer> Layers { get; set; } = new List<RoofLayer>();
        public double Pitch { get; set; }
        public double EaveOverhang { get; set; }
    }

    public class RoofLayer
    {
        public string MaterialName { get; set; }
        public double Thickness { get; set; }
    }

    public class FloorAssembly
    {
        public string TypeName { get; set; }
        public List<FloorLayer> Layers { get; set; } = new List<FloorLayer>();
    }

    public class FloorLayer
    {
        public string MaterialName { get; set; }
        public double Thickness { get; set; }
    }

    public class WallContext
    {
        public List<WallLayer> Layers { get; set; }
        public double TotalThickness { get; set; }
        public bool HasCavity { get; set; }
        public bool IsExterior { get; set; }
    }

    #endregion

    #region Enumerations

    public enum DetailType
    {
        WallSection,
        WindowDetail,
        DoorDetail,
        RoofDetail,
        FloorJunction,
        Foundation,
        Staircase,
        Balcony,
        Custom
    }

    public enum DetailElementType
    {
        FilledRegion,
        Line,
        Arc,
        Polyline,
        ReinforcementBar,
        ReinforcementMesh,
        Component,
        Symbol,
        BreakLine,
        Text
    }

    public enum LineStyle
    {
        Thin,
        Medium,
        Thick,
        Dashed,
        DashDot,
        Hidden,
        CenterLine,
        GroundLine,
        CutLine
    }

    public enum AnnotationType
    {
        MaterialKeynote,
        GeneralNote,
        StandardReference,
        DimensionNote,
        LeaderNote,
        Symbol
    }

    public enum DimensionType
    {
        Linear,
        Angular,
        Radial,
        Diameter,
        Ordinate
    }

    public enum DimensionPosition
    {
        Left,
        Right,
        Top,
        Bottom,
        Inside
    }

    public enum LayerFunction
    {
        Structure,
        Substrate,
        ThermalInsulation,
        AirGap,
        Finish,
        Membrane,
        FireResistance
    }

    public enum FoundationType
    {
        StripFoundation,
        PadFoundation,
        RaftFoundation,
        PileFoundation,
        StemWall,
        GradeBeam
    }

    public enum RoofType
    {
        PitchedWithEave,
        FlatWithParapet,
        FlatWithFascia,
        MonoPitch,
        Mansard,
        Butterfly,
        GreenRoof
    }

    public enum WindowDetailView
    {
        Jamb,
        Head,
        Sill,
        All
    }

    public enum DoorDetailView
    {
        Jamb,
        Head,
        Threshold,
        All
    }

    public enum RoofDetailView
    {
        Eave,
        Ridge,
        Verge,
        Valley,
        Hip,
        FlatRoofEdge,
        All
    }

    public enum JunctionType
    {
        Supported,
        Bypassing,
        Interrupted
    }

    public enum WindowFrameType
    {
        Timber,
        Aluminium,
        Steel,
        uPVC,
        Composite
    }

    #endregion

    #endregion
}
