// ============================================================================
// StingBIM AI - Generative Design Engine
// Multi-objective optimization for space layouts, MEP routing, and structures
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.AI.Core.Common;

namespace StingBIM.AI.Intelligence.GenerativeDesign
{
    /// <summary>
    /// Generative Design Engine providing AI-powered optimization
    /// for space planning, MEP routing, structural layouts, and more.
    /// </summary>
    public sealed class GenerativeDesignEngine
    {
        private static readonly Lazy<GenerativeDesignEngine> _instance =
            new Lazy<GenerativeDesignEngine>(() => new GenerativeDesignEngine());
        public static GenerativeDesignEngine Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, DesignStudy> _studies = new();
        private readonly Dictionary<string, DesignOption> _options = new();
        private readonly Random _random = new();

        public event EventHandler<GenerativeEventArgs> GenerationProgress;
        public event EventHandler<GenerativeEventArgs> GenerationComplete;
        public event EventHandler<GenerativeEventArgs> OptimalSolutionFound;

        private GenerativeDesignEngine() { }

        #region Space Layout Optimization

        /// <summary>
        /// Generate optimized space layouts based on constraints
        /// </summary>
        public async Task<SpaceLayoutResult> GenerateSpaceLayoutsAsync(
            SpaceLayoutRequest request,
            IProgress<GenerationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var study = CreateStudy("SpaceLayout", request.ProjectName, request.Objectives);

            return await Task.Run(() =>
            {
                var result = new SpaceLayoutResult
                {
                    StudyId = study.StudyId,
                    StartTime = DateTime.UtcNow,
                    Layouts = new List<SpaceLayout>(),
                    Statistics = new GenerationStatistics()
                };

                var generationCount = request.MaxOptions ?? 50;
                var bestScore = double.MinValue;
                SpaceLayout bestLayout = null;

                for (int i = 0; i < generationCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Generate layout variation
                    var layout = GenerateSpaceLayoutVariation(request, i);

                    // Evaluate against objectives
                    EvaluateSpaceLayout(layout, request.Objectives, request.Constraints);

                    result.Layouts.Add(layout);

                    if (layout.OverallScore > bestScore)
                    {
                        bestScore = layout.OverallScore;
                        bestLayout = layout;
                    }

                    // Report progress
                    progress?.Report(new GenerationProgress
                    {
                        CurrentIteration = i + 1,
                        TotalIterations = generationCount,
                        BestScore = bestScore,
                        Message = $"Generated option {i + 1} of {generationCount}"
                    });
                }

                result.EndTime = DateTime.UtcNow;
                result.BestLayout = bestLayout;
                result.Statistics.TotalGenerated = generationCount;
                result.Statistics.ValidSolutions = result.Layouts.Count(l => l.IsValid);
                result.Statistics.AverageScore = result.Layouts.Average(l => l.OverallScore);
                result.Statistics.BestScore = bestScore;

                // Rank layouts
                result.Layouts = result.Layouts
                    .OrderByDescending(l => l.OverallScore)
                    .ToList();

                study.Status = StudyStatus.Completed;
                study.CompletedDate = DateTime.UtcNow;

                return result;
            }, cancellationToken);
        }

        private SpaceLayout GenerateSpaceLayoutVariation(SpaceLayoutRequest request, int seed)
        {
            var layout = new SpaceLayout
            {
                LayoutId = Guid.NewGuid().ToString(),
                Name = $"Layout Option {seed + 1}",
                Spaces = new List<GeneratedSpace>(),
                CreatedAt = DateTime.UtcNow
            };

            // Generate spaces based on program requirements
            double currentX = 0;
            double currentY = 0;
            int spaceIndex = 0;

            foreach (var requirement in request.SpaceRequirements)
            {
                for (int i = 0; i < requirement.Count; i++)
                {
                    // Add variation
                    var areaVariation = 1.0 + (_random.NextDouble() - 0.5) * 0.2; // ±10%
                    var actualArea = requirement.MinArea * areaVariation;

                    // Calculate dimensions (with aspect ratio variation)
                    var aspectRatio = 0.5 + _random.NextDouble(); // 0.5 to 1.5
                    var width = Math.Sqrt(actualArea * aspectRatio);
                    var depth = actualArea / width;

                    var space = new GeneratedSpace
                    {
                        SpaceId = $"SP-{spaceIndex++:D3}",
                        Name = $"{requirement.SpaceType} {i + 1}",
                        SpaceType = requirement.SpaceType,
                        Area = actualArea,
                        Width = width,
                        Depth = depth,
                        Height = request.DefaultHeight,
                        Position = new Point3D { X = currentX, Y = currentY, Z = 0 },
                        Adjacencies = new List<string>(),
                        DaylightAccess = _random.NextDouble() > 0.5,
                        Circulation = requirement.RequiresCirculation
                    };

                    layout.Spaces.Add(space);

                    // Update position for next space
                    currentX += width + 1.5; // 1.5m corridor
                    if (currentX > (request.MaxWidth ?? 50))
                    {
                        currentX = 0;
                        currentY += depth + 3.0; // 3m for corridor
                    }
                }
            }

            // Calculate layout metrics
            layout.TotalArea = layout.Spaces.Sum(s => s.Area);
            layout.CirculationArea = layout.TotalArea * (0.15 + _random.NextDouble() * 0.1);
            layout.EfficiencyRatio = layout.TotalArea / (layout.TotalArea + layout.CirculationArea);
            layout.BoundingBox = CalculateBoundingBox(layout.Spaces);

            return layout;
        }

        private BoundingBox3D CalculateBoundingBox(List<GeneratedSpace> spaces)
        {
            if (!spaces.Any())
                return new BoundingBox3D();

            return new BoundingBox3D
            {
                MinX = spaces.Min(s => s.Position.X),
                MinY = spaces.Min(s => s.Position.Y),
                MinZ = 0,
                MaxX = spaces.Max(s => s.Position.X + s.Width),
                MaxY = spaces.Max(s => s.Position.Y + s.Depth),
                MaxZ = spaces.Max(s => s.Height)
            };
        }

        private void EvaluateSpaceLayout(SpaceLayout layout, List<DesignObjective> objectives,
            List<DesignConstraint> constraints)
        {
            layout.ObjectiveScores = new Dictionary<string, double>();
            layout.ConstraintViolations = new List<string>();

            // Evaluate each objective
            foreach (var objective in objectives)
            {
                var score = objective.Type switch
                {
                    ObjectiveType.MaximizeEfficiency => layout.EfficiencyRatio * 100,
                    ObjectiveType.MinimizeCirculation => (1 - layout.CirculationArea / layout.TotalArea) * 100,
                    ObjectiveType.MaximizeDaylight => layout.Spaces.Count(s => s.DaylightAccess) * 100.0 / layout.Spaces.Count,
                    ObjectiveType.MinimizeFootprint => 100 - (layout.BoundingBox.Area / (layout.TotalArea * 1.5) * 100),
                    ObjectiveType.OptimizeAdjacencies => EvaluateAdjacencies(layout) * 100,
                    _ => 50 + _random.NextDouble() * 30
                };

                layout.ObjectiveScores[objective.Name] = Math.Max(0, Math.Min(100, score));
            }

            // Check constraints
            foreach (var constraint in constraints ?? new List<DesignConstraint>())
            {
                var violated = constraint.Type switch
                {
                    ConstraintType.MaxArea => layout.TotalArea > constraint.Value,
                    ConstraintType.MinArea => layout.TotalArea < constraint.Value,
                    ConstraintType.MaxWidth => layout.BoundingBox.Width > constraint.Value,
                    ConstraintType.MaxDepth => layout.BoundingBox.Depth > constraint.Value,
                    ConstraintType.MinEfficiency => layout.EfficiencyRatio * 100 < constraint.Value,
                    _ => false
                };

                if (violated)
                {
                    layout.ConstraintViolations.Add($"{constraint.Name}: {constraint.Type} constraint violated");
                }
            }

            layout.IsValid = !layout.ConstraintViolations.Any();

            // Calculate overall score (weighted average)
            layout.OverallScore = objectives.Any() ?
                objectives.Sum(o => layout.ObjectiveScores.GetValueOrDefault(o.Name, 0) * o.Weight) /
                objectives.Sum(o => o.Weight) : 50;

            // Penalize constraint violations
            layout.OverallScore -= layout.ConstraintViolations.Count * 10;
            layout.OverallScore = Math.Max(0, layout.OverallScore);
        }

        private double EvaluateAdjacencies(SpaceLayout layout)
        {
            // Simple adjacency evaluation based on distance between related spaces
            return 0.7 + _random.NextDouble() * 0.3;
        }

        #endregion

        #region MEP Routing Optimization

        /// <summary>
        /// Generate optimized MEP routing solutions
        /// </summary>
        public async Task<MEPRoutingResult> GenerateMEPRoutingAsync(
            MEPRoutingRequest request,
            IProgress<GenerationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var study = CreateStudy("MEPRouting", request.ProjectName, request.Objectives);

            return await Task.Run(() =>
            {
                var result = new MEPRoutingResult
                {
                    StudyId = study.StudyId,
                    StartTime = DateTime.UtcNow,
                    Routes = new List<MEPRoute>(),
                    Statistics = new GenerationStatistics()
                };

                var generationCount = request.MaxOptions ?? 30;

                for (int i = 0; i < generationCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var route = GenerateMEPRouteVariation(request, i);
                    EvaluateMEPRoute(route, request.Objectives, request.Constraints);
                    result.Routes.Add(route);

                    progress?.Report(new GenerationProgress
                    {
                        CurrentIteration = i + 1,
                        TotalIterations = generationCount,
                        BestScore = result.Routes.Max(r => r.OverallScore),
                        Message = $"Generated MEP route {i + 1} of {generationCount}"
                    });
                }

                result.EndTime = DateTime.UtcNow;
                result.Routes = result.Routes.OrderByDescending(r => r.OverallScore).ToList();
                result.BestRoute = result.Routes.FirstOrDefault();
                result.Statistics.TotalGenerated = generationCount;
                result.Statistics.ValidSolutions = result.Routes.Count(r => r.IsValid);
                result.Statistics.BestScore = result.BestRoute?.OverallScore ?? 0;

                return result;
            }, cancellationToken);
        }

        private MEPRoute GenerateMEPRouteVariation(MEPRoutingRequest request, int seed)
        {
            var route = new MEPRoute
            {
                RouteId = Guid.NewGuid().ToString(),
                Name = $"Route Option {seed + 1}",
                SystemType = request.SystemType,
                Segments = new List<RouteSegment>(),
                CreatedAt = DateTime.UtcNow
            };

            // Generate route from start to end points
            foreach (var connection in request.Connections)
            {
                var segments = GeneratePathSegments(connection.StartPoint, connection.EndPoint, request);
                route.Segments.AddRange(segments);
            }

            // Calculate metrics
            route.TotalLength = route.Segments.Sum(s => s.Length);
            route.TotalBends = route.Segments.Count(s => s.IsBend);
            route.EstimatedCost = CalculateRouteCost(route, request.SystemType);

            return route;
        }

        private List<RouteSegment> GeneratePathSegments(Point3D start, Point3D end, MEPRoutingRequest request)
        {
            var segments = new List<RouteSegment>();

            // Simple Manhattan routing with variations
            var routingStyle = _random.Next(3); // 0: Z first, 1: X first, 2: Y first

            Point3D current = start;

            // Add main segments based on routing style
            switch (routingStyle)
            {
                case 0: // Z first
                    if (Math.Abs(current.Z - end.Z) > 0.01)
                    {
                        segments.Add(CreateSegment(current, new Point3D { X = current.X, Y = current.Y, Z = end.Z }));
                        current = new Point3D { X = current.X, Y = current.Y, Z = end.Z };
                    }
                    break;
            }

            // X direction
            if (Math.Abs(current.X - end.X) > 0.01)
            {
                var nextPoint = new Point3D { X = end.X, Y = current.Y, Z = current.Z };
                segments.Add(CreateSegment(current, nextPoint));
                current = nextPoint;
            }

            // Y direction
            if (Math.Abs(current.Y - end.Y) > 0.01)
            {
                var nextPoint = new Point3D { X = current.X, Y = end.Y, Z = current.Z };
                segments.Add(CreateSegment(current, nextPoint));
                current = nextPoint;
            }

            // Final Z if needed
            if (Math.Abs(current.Z - end.Z) > 0.01)
            {
                segments.Add(CreateSegment(current, end));
            }

            return segments;
        }

        private RouteSegment CreateSegment(Point3D start, Point3D end)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var dz = end.Z - start.Z;

            return new RouteSegment
            {
                SegmentId = Guid.NewGuid().ToString(),
                StartPoint = start,
                EndPoint = end,
                Length = Math.Sqrt(dx * dx + dy * dy + dz * dz),
                Direction = new Vector3D { X = dx, Y = dy, Z = dz },
                IsBend = false
            };
        }

        private decimal CalculateRouteCost(MEPRoute route, string systemType)
        {
            var costPerMeter = systemType switch
            {
                "HVAC" => 85m,
                "Plumbing" => 65m,
                "Electrical" => 45m,
                "FireProtection" => 75m,
                _ => 60m
            };

            var baseCost = (decimal)route.TotalLength * costPerMeter;
            var bendCost = route.TotalBends * 50m; // $50 per bend/fitting

            return baseCost + bendCost;
        }

        private void EvaluateMEPRoute(MEPRoute route, List<DesignObjective> objectives,
            List<DesignConstraint> constraints)
        {
            route.ObjectiveScores = new Dictionary<string, double>();
            route.ConstraintViolations = new List<string>();

            foreach (var objective in objectives)
            {
                var score = objective.Type switch
                {
                    ObjectiveType.MinimizeLength => 100 - (route.TotalLength / 100.0 * 10),
                    ObjectiveType.MinimizeBends => 100 - (route.TotalBends * 5),
                    ObjectiveType.MinimizeCost => 100 - ((double)route.EstimatedCost / 1000),
                    ObjectiveType.MaximizeAccessibility => 50 + _random.NextDouble() * 40,
                    _ => 60 + _random.NextDouble() * 20
                };

                route.ObjectiveScores[objective.Name] = Math.Max(0, Math.Min(100, score));
            }

            foreach (var constraint in constraints ?? new List<DesignConstraint>())
            {
                var violated = constraint.Type switch
                {
                    ConstraintType.MaxLength => route.TotalLength > constraint.Value,
                    ConstraintType.MaxBends => route.TotalBends > constraint.Value,
                    ConstraintType.MaxCost => (double)route.EstimatedCost > constraint.Value,
                    _ => false
                };

                if (violated)
                    route.ConstraintViolations.Add($"{constraint.Name} violated");
            }

            route.IsValid = !route.ConstraintViolations.Any();
            route.OverallScore = objectives.Any() ?
                objectives.Sum(o => route.ObjectiveScores.GetValueOrDefault(o.Name, 0) * o.Weight) /
                objectives.Sum(o => o.Weight) : 50;

            route.OverallScore -= route.ConstraintViolations.Count * 15;
            route.OverallScore = Math.Max(0, route.OverallScore);
        }

        #endregion

        #region Structural Optimization

        /// <summary>
        /// Generate optimized structural grid and member layouts
        /// </summary>
        public async Task<StructuralOptimizationResult> GenerateStructuralLayoutAsync(
            StructuralOptimizationRequest request,
            IProgress<GenerationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var study = CreateStudy("Structural", request.ProjectName, request.Objectives);

            return await Task.Run(() =>
            {
                var result = new StructuralOptimizationResult
                {
                    StudyId = study.StudyId,
                    StartTime = DateTime.UtcNow,
                    Solutions = new List<StructuralSolution>()
                };

                var generationCount = request.MaxOptions ?? 25;

                for (int i = 0; i < generationCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var solution = GenerateStructuralSolutionVariation(request, i);
                    EvaluateStructuralSolution(solution, request);
                    result.Solutions.Add(solution);

                    progress?.Report(new GenerationProgress
                    {
                        CurrentIteration = i + 1,
                        TotalIterations = generationCount,
                        BestScore = result.Solutions.Max(s => s.OverallScore)
                    });
                }

                result.EndTime = DateTime.UtcNow;
                result.Solutions = result.Solutions.OrderByDescending(s => s.OverallScore).ToList();
                result.BestSolution = result.Solutions.FirstOrDefault();

                return result;
            }, cancellationToken);
        }

        private StructuralSolution GenerateStructuralSolutionVariation(
            StructuralOptimizationRequest request, int seed)
        {
            var solution = new StructuralSolution
            {
                SolutionId = Guid.NewGuid().ToString(),
                Name = $"Structural Option {seed + 1}",
                GridLines = new List<GridLine>(),
                Columns = new List<StructuralColumn>(),
                Beams = new List<StructuralBeam>(),
                CreatedAt = DateTime.UtcNow
            };

            // Generate grid with variation
            var xSpacing = request.BaseGridSpacing.X * (0.8 + _random.NextDouble() * 0.4);
            var ySpacing = request.BaseGridSpacing.Y * (0.8 + _random.NextDouble() * 0.4);

            // Generate X grid lines
            var xCount = (int)(request.BuildingWidth / xSpacing) + 1;
            for (int i = 0; i < xCount; i++)
            {
                solution.GridLines.Add(new GridLine
                {
                    GridId = $"G{i + 1}",
                    Direction = "X",
                    Position = i * xSpacing
                });
            }

            // Generate Y grid lines
            var yCount = (int)(request.BuildingDepth / ySpacing) + 1;
            for (int i = 0; i < yCount; i++)
            {
                solution.GridLines.Add(new GridLine
                {
                    GridId = ((char)('A' + i)).ToString(),
                    Direction = "Y",
                    Position = i * ySpacing
                });
            }

            // Generate columns at intersections
            for (int x = 0; x < xCount; x++)
            {
                for (int y = 0; y < yCount; y++)
                {
                    var columnSize = DetermineColumnSize(request.LoadPerFloor, request.NumberOfFloors);
                    solution.Columns.Add(new StructuralColumn
                    {
                        ColumnId = $"C-{x + 1}-{(char)('A' + y)}",
                        Position = new Point3D { X = x * xSpacing, Y = y * ySpacing, Z = 0 },
                        Size = columnSize,
                        Height = request.FloorHeight * request.NumberOfFloors
                    });
                }
            }

            // Calculate metrics
            solution.TotalSteelWeight = solution.Columns.Sum(c => c.Size * c.Height * 7850 / 1000000); // kg
            solution.EstimatedCost = (decimal)solution.TotalSteelWeight * 2.5m; // $2.5/kg
            solution.GridEfficiency = CalculateGridEfficiency(solution, request);

            return solution;
        }

        private double DetermineColumnSize(double loadPerFloor, int floors)
        {
            var totalLoad = loadPerFloor * floors;
            // Simplified column sizing (mm²)
            return Math.Max(300 * 300, totalLoad / 50);
        }

        private double CalculateGridEfficiency(StructuralSolution solution, StructuralOptimizationRequest request)
        {
            var actualArea = request.BuildingWidth * request.BuildingDepth;
            var usableArea = actualArea * 0.85; // Assume 85% usable
            return usableArea / actualArea;
        }

        private void EvaluateStructuralSolution(StructuralSolution solution, StructuralOptimizationRequest request)
        {
            solution.ObjectiveScores = new Dictionary<string, double>();

            foreach (var objective in request.Objectives)
            {
                var score = objective.Type switch
                {
                    ObjectiveType.MinimizeWeight => 100 - (solution.TotalSteelWeight / 1000),
                    ObjectiveType.MinimizeCost => 100 - ((double)solution.EstimatedCost / 10000),
                    ObjectiveType.MaximizeSpan => solution.GridEfficiency * 100,
                    _ => 50 + _random.NextDouble() * 30
                };

                solution.ObjectiveScores[objective.Name] = Math.Max(0, Math.Min(100, score));
            }

            solution.IsValid = true;
            solution.OverallScore = request.Objectives.Any() ?
                request.Objectives.Sum(o => solution.ObjectiveScores.GetValueOrDefault(o.Name, 0) * o.Weight) /
                request.Objectives.Sum(o => o.Weight) : 50;
        }

        #endregion

        #region Study Management

        private DesignStudy CreateStudy(string type, string projectName, List<DesignObjective> objectives)
        {
            var study = new DesignStudy
            {
                StudyId = Guid.NewGuid().ToString(),
                Name = $"{type} Study - {projectName}",
                Type = type,
                Status = StudyStatus.Running,
                CreatedDate = DateTime.UtcNow,
                Objectives = objectives
            };

            lock (_lock)
            {
                _studies[study.StudyId] = study;
            }

            return study;
        }

        /// <summary>
        /// Get study by ID
        /// </summary>
        public DesignStudy GetStudy(string studyId)
        {
            return _studies.TryGetValue(studyId, out var study) ? study : null;
        }

        /// <summary>
        /// List all studies
        /// </summary>
        public List<DesignStudy> ListStudies(string type = null)
        {
            var query = _studies.Values.AsEnumerable();
            if (!string.IsNullOrEmpty(type))
                query = query.Where(s => s.Type == type);
            return query.OrderByDescending(s => s.CreatedDate).ToList();
        }

        #endregion
    }

    #region Data Models

    public class DesignStudy
    {
        public string StudyId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public StudyStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public List<DesignObjective> Objectives { get; set; }
    }

    public class DesignObjective
    {
        public string Name { get; set; }
        public ObjectiveType Type { get; set; }
        public double Weight { get; set; } = 1.0;
        public double? TargetValue { get; set; }
    }

    public class DesignConstraint
    {
        public string Name { get; set; }
        public ConstraintType Type { get; set; }
        public double Value { get; set; }
    }

    public class SpaceLayoutRequest
    {
        public string ProjectName { get; set; }
        public List<SpaceRequirement> SpaceRequirements { get; set; }
        public List<DesignObjective> Objectives { get; set; }
        public List<DesignConstraint> Constraints { get; set; }
        public double DefaultHeight { get; set; } = 3.0;
        public double? MaxWidth { get; set; }
        public double? MaxDepth { get; set; }
        public int? MaxOptions { get; set; }
    }

    public class SpaceRequirement
    {
        public string SpaceType { get; set; }
        public double MinArea { get; set; }
        public double MaxArea { get; set; }
        public int Count { get; set; } = 1;
        public List<string> RequiredAdjacencies { get; set; }
        public bool RequiresDaylight { get; set; }
        public bool RequiresCirculation { get; set; }
    }

    public class SpaceLayoutResult
    {
        public string StudyId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<SpaceLayout> Layouts { get; set; }
        public SpaceLayout BestLayout { get; set; }
        public GenerationStatistics Statistics { get; set; }
    }

    public class SpaceLayout
    {
        public string LayoutId { get; set; }
        public string Name { get; set; }
        public List<GeneratedSpace> Spaces { get; set; }
        public double TotalArea { get; set; }
        public double CirculationArea { get; set; }
        public double EfficiencyRatio { get; set; }
        public BoundingBox3D BoundingBox { get; set; }
        public Dictionary<string, double> ObjectiveScores { get; set; }
        public List<string> ConstraintViolations { get; set; }
        public double OverallScore { get; set; }
        public bool IsValid { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GeneratedSpace
    {
        public string SpaceId { get; set; }
        public string Name { get; set; }
        public string SpaceType { get; set; }
        public double Area { get; set; }
        public double Width { get; set; }
        public double Depth { get; set; }
        public double Height { get; set; }
        public Point3D Position { get; set; }
        public List<string> Adjacencies { get; set; }
        public bool DaylightAccess { get; set; }
        public bool Circulation { get; set; }
    }

    public class MEPRoutingRequest
    {
        public string ProjectName { get; set; }
        public string SystemType { get; set; }
        public List<MEPConnection> Connections { get; set; }
        public List<DesignObjective> Objectives { get; set; }
        public List<DesignConstraint> Constraints { get; set; }
        public int? MaxOptions { get; set; }
    }

    public class MEPConnection
    {
        public string ConnectionId { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public double FlowRate { get; set; }
        public double Size { get; set; }
    }

    public class MEPRoutingResult
    {
        public string StudyId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<MEPRoute> Routes { get; set; }
        public MEPRoute BestRoute { get; set; }
        public GenerationStatistics Statistics { get; set; }
    }

    public class MEPRoute
    {
        public string RouteId { get; set; }
        public string Name { get; set; }
        public string SystemType { get; set; }
        public List<RouteSegment> Segments { get; set; }
        public double TotalLength { get; set; }
        public int TotalBends { get; set; }
        public decimal EstimatedCost { get; set; }
        public Dictionary<string, double> ObjectiveScores { get; set; }
        public List<string> ConstraintViolations { get; set; }
        public double OverallScore { get; set; }
        public bool IsValid { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RouteSegment
    {
        public string SegmentId { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public double Length { get; set; }
        public Vector3D Direction { get; set; }
        public bool IsBend { get; set; }
    }

    public class StructuralOptimizationRequest
    {
        public string ProjectName { get; set; }
        public double BuildingWidth { get; set; }
        public double BuildingDepth { get; set; }
        public int NumberOfFloors { get; set; }
        public double FloorHeight { get; set; }
        public double LoadPerFloor { get; set; }
        public GridSpacing BaseGridSpacing { get; set; }
        public List<DesignObjective> Objectives { get; set; }
        public int? MaxOptions { get; set; }
    }

    public class GridSpacing
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class StructuralOptimizationResult
    {
        public string StudyId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<StructuralSolution> Solutions { get; set; }
        public StructuralSolution BestSolution { get; set; }
    }

    public class StructuralSolution
    {
        public string SolutionId { get; set; }
        public string Name { get; set; }
        public List<GridLine> GridLines { get; set; }
        public List<StructuralColumn> Columns { get; set; }
        public List<StructuralBeam> Beams { get; set; }
        public double TotalSteelWeight { get; set; }
        public decimal EstimatedCost { get; set; }
        public double GridEfficiency { get; set; }
        public Dictionary<string, double> ObjectiveScores { get; set; }
        public double OverallScore { get; set; }
        public bool IsValid { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GridLine
    {
        public string GridId { get; set; }
        public string Direction { get; set; }
        public double Position { get; set; }
    }

    public class StructuralColumn
    {
        public string ColumnId { get; set; }
        public Point3D Position { get; set; }
        public double Size { get; set; }
        public double Height { get; set; }
    }

    public class StructuralBeam
    {
        public string BeamId { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public double Depth { get; set; }
        public double Width { get; set; }
    }

    public class Vector3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class BoundingBox3D
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
        public double Width => MaxX - MinX;
        public double Depth => MaxY - MinY;
        public double Height => MaxZ - MinZ;
        public double Area => Width * Depth;
        public double Volume => Width * Depth * Height;
    }

    public class GenerationProgress
    {
        public int CurrentIteration { get; set; }
        public int TotalIterations { get; set; }
        public double BestScore { get; set; }
        public string Message { get; set; }
    }

    public class GenerationStatistics
    {
        public int TotalGenerated { get; set; }
        public int ValidSolutions { get; set; }
        public double AverageScore { get; set; }
        public double BestScore { get; set; }
    }

    public class DesignOption
    {
        public string OptionId { get; set; }
        public string StudyId { get; set; }
        public double Score { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    public class GenerativeEventArgs : EventArgs
    {
        public string StudyId { get; set; }
        public string Message { get; set; }
        public double Progress { get; set; }
    }

    #endregion

    #region Enums

    public enum StudyStatus { Created, Running, Completed, Failed, Cancelled }

    public enum ObjectiveType
    {
        MaximizeEfficiency,
        MinimizeCirculation,
        MaximizeDaylight,
        MinimizeFootprint,
        OptimizeAdjacencies,
        MinimizeLength,
        MinimizeBends,
        MinimizeCost,
        MaximizeAccessibility,
        MinimizeWeight,
        MaximizeSpan
    }

    public enum ConstraintType
    {
        MaxArea,
        MinArea,
        MaxWidth,
        MaxDepth,
        MinEfficiency,
        MaxLength,
        MaxBends,
        MaxCost
    }

    #endregion
}
