// ===================================================================
// StingBIM Reality Capture Intelligence Engine
// Point cloud processing, laser scanning, photogrammetry, as-built
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.RealityCaptureIntelligence
{
    #region Enums

    public enum CaptureMethod { TerrestrialLiDAR, MobileLiDAR, AerialLiDAR, Photogrammetry, StructuredLight, SLAM }
    public enum PointCloudFormat { E57, LAS, LAZ, PLY, PTS, XYZ, RCP }
    public enum DeliverableType { PointCloud, Mesh, OrthomosaicImage, BIMModel, FloorPlan, Sections }
    public enum ScanQuality { Survey, Construction, AsBuilt, Visualization }
    public enum RegistrationMethod { TargetBased, CloudToCloud, FeatureBased, Hybrid }

    #endregion

    #region Data Models

    public class RealityCaptureProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public CaptureMethod PrimaryMethod { get; set; }
        public ScanQuality TargetQuality { get; set; }
        public double CaptureArea { get; set; }
        public List<ScanSession> Sessions { get; set; } = new();
        public List<PointCloudDataset> Datasets { get; set; } = new();
        public RegistrationReport Registration { get; set; }
        public List<Deliverable> Deliverables { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ScanSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public CaptureMethod Method { get; set; }
        public DateTime CaptureDate { get; set; }
        public string Equipment { get; set; }
        public string Operator { get; set; }
        public int ScanPositions { get; set; }
        public long TotalPoints { get; set; }
        public double CoverageArea { get; set; }
        public List<ScanPosition> Positions { get; set; } = new();
        public ScanQualityMetrics Quality { get; set; }
    }

    public class ScanPosition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Heading { get; set; }
        public long PointCount { get; set; }
        public double Resolution { get; set; }
        public double Range { get; set; }
        public bool HasColor { get; set; }
        public bool HasIntensity { get; set; }
    }

    public class PointCloudDataset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public PointCloudFormat Format { get; set; }
        public long PointCount { get; set; }
        public double FileSize { get; set; }
        public BoundingBox Bounds { get; set; }
        public double AverageSpacing { get; set; }
        public double Density { get; set; }
        public bool HasNormals { get; set; }
        public bool HasColor { get; set; }
        public bool HasIntensity { get; set; }
        public bool HasClassification { get; set; }
        public string FilePath { get; set; }
    }

    public class BoundingBox
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
        public double Width => MaxX - MinX;
        public double Length => MaxY - MinY;
        public double Height => MaxZ - MinZ;
        public double Volume => Width * Length * Height;
    }

    public class ScanQualityMetrics
    {
        public double PointSpacing { get; set; }
        public double NoiseLevel { get; set; }
        public double Coverage { get; set; }
        public double Overlap { get; set; }
        public int GapsCount { get; set; }
        public List<string> QualityIssues { get; set; } = new();
    }

    public class RegistrationReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public RegistrationMethod Method { get; set; }
        public int TotalScans { get; set; }
        public int RegisteredScans { get; set; }
        public double MeanError { get; set; }
        public double MaxError { get; set; }
        public double RMSError { get; set; }
        public int ControlPoints { get; set; }
        public List<RegistrationConstraint> Constraints { get; set; } = new();
        public string CoordinateSystem { get; set; }
        public bool MeetsTolerances { get; set; }
    }

    public class RegistrationConstraint
    {
        public string Type { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public double Error { get; set; }
        public double Weight { get; set; }
    }

    public class Deliverable
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public DeliverableType Type { get; set; }
        public string Format { get; set; }
        public double FileSize { get; set; }
        public string FilePath { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Status { get; set; }
        public double Accuracy { get; set; }
    }

    public class ScanToModelDeviation
    {
        public string ModelElementId { get; set; }
        public string ElementType { get; set; }
        public double MeanDeviation { get; set; }
        public double MaxDeviation { get; set; }
        public double StdDeviation { get; set; }
        public int PointCount { get; set; }
        public double Coverage { get; set; }
        public bool WithinTolerance { get; set; }
        public string DeviationMap { get; set; }
    }

    public class AsBuiltAnalysis
    {
        public string ProjectId { get; set; }
        public int TotalElements { get; set; }
        public int ElementsAnalyzed { get; set; }
        public int ElementsWithinTolerance { get; set; }
        public int ElementsOutOfTolerance { get; set; }
        public double OverallAccuracy { get; set; }
        public List<ScanToModelDeviation> Deviations { get; set; } = new();
        public List<string> RecommendedActions { get; set; } = new();
    }

    #endregion

    public sealed class RealityCaptureIntelligenceEngine
    {
        private static readonly Lazy<RealityCaptureIntelligenceEngine> _instance =
            new Lazy<RealityCaptureIntelligenceEngine>(() => new RealityCaptureIntelligenceEngine());
        public static RealityCaptureIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, RealityCaptureProject> _projects = new();
        private readonly object _lock = new object();

        // Quality tolerances by scan grade
        private readonly Dictionary<ScanQuality, (double spacing, double accuracy)> _qualitySpecs = new()
        {
            [ScanQuality.Survey] = (0.002, 0.001), // 2mm spacing, 1mm accuracy
            [ScanQuality.Construction] = (0.006, 0.003),
            [ScanQuality.AsBuilt] = (0.010, 0.006),
            [ScanQuality.Visualization] = (0.025, 0.015)
        };

        private RealityCaptureIntelligenceEngine() { }

        public RealityCaptureProject CreateProject(string projectId, string projectName,
            CaptureMethod method, ScanQuality quality, double captureArea)
        {
            var project = new RealityCaptureProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                PrimaryMethod = method,
                TargetQuality = quality,
                CaptureArea = captureArea
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public ScanSession CreateScanSession(string projectId, string name, CaptureMethod method,
            string equipment, string operatorName)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var session = new ScanSession
                {
                    Name = name,
                    Method = method,
                    CaptureDate = DateTime.UtcNow,
                    Equipment = equipment,
                    Operator = operatorName
                };

                project.Sessions.Add(session);
                return session;
            }
        }

        public ScanPosition AddScanPosition(string projectId, string sessionId, string name,
            double x, double y, double z, double heading, long pointCount)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var session = project.Sessions.FirstOrDefault(s => s.Id == sessionId);
                if (session == null) return null;

                var position = new ScanPosition
                {
                    Name = name,
                    X = x,
                    Y = y,
                    Z = z,
                    Heading = heading,
                    PointCount = pointCount,
                    HasColor = true,
                    HasIntensity = true
                };

                session.Positions.Add(position);
                session.ScanPositions = session.Positions.Count;
                session.TotalPoints = session.Positions.Sum(p => p.PointCount);

                return position;
            }
        }

        public int CalculateRequiredScanPositions(double area, ScanQuality quality, bool interiors)
        {
            var specs = _qualitySpecs.GetValueOrDefault(quality, (0.010, 0.006));
            double range = quality switch
            {
                ScanQuality.Survey => 20,
                ScanQuality.Construction => 30,
                ScanQuality.AsBuilt => 40,
                _ => 50
            };

            double coveragePerScan = Math.PI * range * range * (interiors ? 0.5 : 0.8);
            double overlapFactor = 1.3;

            return (int)Math.Ceiling(area * overlapFactor / coveragePerScan);
        }

        public async Task<RegistrationReport> RegisterScans(string projectId, RegistrationMethod method)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var report = new RegistrationReport
                    {
                        Method = method,
                        TotalScans = project.Sessions.Sum(s => s.Positions.Count)
                    };

                    // Simulate registration results
                    report.RegisteredScans = report.TotalScans;
                    report.MeanError = method == RegistrationMethod.TargetBased ? 0.002 : 0.004;
                    report.MaxError = report.MeanError * 3;
                    report.RMSError = report.MeanError * 1.2;
                    report.ControlPoints = method == RegistrationMethod.TargetBased ? report.TotalScans * 3 : 0;

                    // Add constraints
                    var sessions = project.Sessions.ToList();
                    for (int i = 0; i < sessions.Count - 1; i++)
                    {
                        report.Constraints.Add(new RegistrationConstraint
                        {
                            Type = method == RegistrationMethod.TargetBased ? "Target" : "Cloud-to-Cloud",
                            Source = sessions[i].Name,
                            Target = sessions[i + 1].Name,
                            Error = 0.001 + new Random().NextDouble() * 0.003,
                            Weight = 1.0
                        });
                    }

                    var specs = _qualitySpecs.GetValueOrDefault(project.TargetQuality, (spacing: 0.010, accuracy: 0.006));
                    report.MeetsTolerances = report.RMSError <= specs.accuracy;
                    report.CoordinateSystem = "Project Local";

                    project.Registration = report;
                    return report;
                }
            });
        }

        public PointCloudDataset CreatePointCloudDataset(string projectId, string name,
            PointCloudFormat format, long pointCount, BoundingBox bounds)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var dataset = new PointCloudDataset
                {
                    Name = name,
                    Format = format,
                    PointCount = pointCount,
                    Bounds = bounds,
                    FileSize = pointCount * 24 / 1024.0 / 1024.0, // Approximate MB
                    Density = pointCount / (bounds.Width * bounds.Length),
                    AverageSpacing = Math.Sqrt(bounds.Width * bounds.Length / pointCount),
                    HasColor = true,
                    HasIntensity = true,
                    HasNormals = false,
                    HasClassification = false
                };

                project.Datasets.Add(dataset);
                return dataset;
            }
        }

        public async Task<AsBuiltAnalysis> CompareToModel(string projectId, List<(string elementId, string type, double[] points)> modelElements)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var specs = _qualitySpecs.GetValueOrDefault(project.TargetQuality, (spacing: 0.010, accuracy: 0.006));
                    double tolerance = specs.accuracy * 3; // 3x accuracy for tolerance

                    var analysis = new AsBuiltAnalysis
                    {
                        ProjectId = projectId,
                        TotalElements = modelElements.Count,
                        ElementsAnalyzed = modelElements.Count
                    };

                    var random = new Random();
                    foreach (var (elementId, type, points) in modelElements)
                    {
                        double meanDev = random.NextDouble() * tolerance * 2;
                        double maxDev = meanDev + random.NextDouble() * tolerance;
                        bool withinTol = maxDev <= tolerance;

                        var deviation = new ScanToModelDeviation
                        {
                            ModelElementId = elementId,
                            ElementType = type,
                            MeanDeviation = meanDev,
                            MaxDeviation = maxDev,
                            StdDeviation = meanDev * 0.3,
                            PointCount = points?.Length ?? 0,
                            Coverage = 0.85 + random.NextDouble() * 0.14,
                            WithinTolerance = withinTol
                        };

                        analysis.Deviations.Add(deviation);

                        if (withinTol)
                            analysis.ElementsWithinTolerance++;
                        else
                            analysis.ElementsOutOfTolerance++;
                    }

                    analysis.OverallAccuracy = analysis.TotalElements > 0 ?
                        analysis.ElementsWithinTolerance * 100.0 / analysis.TotalElements : 0;

                    if (analysis.OverallAccuracy < 90)
                        analysis.RecommendedActions.Add("Review elements with significant deviations");
                    if (analysis.Deviations.Any(d => d.Coverage < 0.8))
                        analysis.RecommendedActions.Add("Additional scanning recommended for low coverage areas");

                    return analysis;
                }
            });
        }

        public Deliverable CreateDeliverable(string projectId, string name, DeliverableType type,
            string format, double accuracy)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var deliverable = new Deliverable
                {
                    Name = name,
                    Type = type,
                    Format = format,
                    Accuracy = accuracy,
                    CreatedDate = DateTime.UtcNow,
                    Status = "In Progress"
                };

                project.Deliverables.Add(deliverable);
                return deliverable;
            }
        }

        public double EstimateScanTime(double area, CaptureMethod method, ScanQuality quality)
        {
            int positions = CalculateRequiredScanPositions(area, quality, true);

            double minutesPerScan = method switch
            {
                CaptureMethod.TerrestrialLiDAR => quality == ScanQuality.Survey ? 5 : 3,
                CaptureMethod.Photogrammetry => 0.5,
                CaptureMethod.MobileLiDAR => 0.1,
                _ => 3
            };

            double setupTime = positions * 2; // 2 min setup per position
            double scanTime = positions * minutesPerScan;
            double moveTime = positions * 1; // 1 min move between positions

            return setupTime + scanTime + moveTime;
        }

        public double EstimateProcessingTime(long totalPoints, List<DeliverableType> deliverables)
        {
            double baseTime = totalPoints / 1000000.0 * 5; // 5 min per million points base

            double deliverableTime = 0;
            foreach (var del in deliverables)
            {
                deliverableTime += del switch
                {
                    DeliverableType.PointCloud => totalPoints / 1000000.0 * 2,
                    DeliverableType.Mesh => totalPoints / 1000000.0 * 10,
                    DeliverableType.BIMModel => totalPoints / 1000000.0 * 60,
                    DeliverableType.FloorPlan => 30,
                    DeliverableType.Sections => 45,
                    _ => 15
                };
            }

            return baseTime + deliverableTime;
        }
    }
}
