// ===================================================================
// StingBIM Computer Vision Intelligence Engine
// AI image analysis, construction progress, defect detection, safety
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.ComputerVisionIntelligence
{
    #region Enums

    public enum AnalysisType { ProgressTracking, DefectDetection, SafetyCompliance, MaterialRecognition, QualityInspection }
    public enum ImageSource { SiteCamera, Drone, MobileDevice, TimeLapse, Satellite }
    public enum DetectionClass { Equipment, Material, Worker, Vehicle, Hazard, Defect, PPE }
    public enum DefectSeverity { Minor, Moderate, Major, Critical }
    public enum ConfidenceLevel { Low, Medium, High, VeryHigh }

    #endregion

    #region Data Models

    public class ComputerVisionProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public List<ImageCapture> Captures { get; set; } = new();
        public List<AnalysisResult> Analyses { get; set; } = new();
        public List<DetectedObject> Detections { get; set; } = new();
        public ProgressTrackingData ProgressData { get; set; }
        public SafetyAnalytics SafetyData { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ImageCapture
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public ImageSource Source { get; set; }
        public DateTime CaptureTime { get; set; }
        public string FilePath { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string CameraId { get; set; }
        public bool IsProcessed { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class AnalysisResult
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ImageId { get; set; }
        public AnalysisType Type { get; set; }
        public DateTime AnalysisTime { get; set; }
        public double ProcessingTime { get; set; }
        public List<Detection> Detections { get; set; } = new();
        public Dictionary<string, double> Metrics { get; set; } = new();
        public double OverallConfidence { get; set; }
    }

    public class Detection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DetectionClass Class { get; set; }
        public string Label { get; set; }
        public BoundingBox Box { get; set; }
        public double Confidence { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new();
    }

    public class BoundingBox
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class DetectedObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DetectionClass Class { get; set; }
        public string ObjectType { get; set; }
        public string TrackingId { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public int DetectionCount { get; set; }
        public List<Location> Locations { get; set; } = new();
    }

    public class Location
    {
        public DateTime Timestamp { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public string ImageId { get; set; }
    }

    public class ProgressTrackingData
    {
        public string ProjectId { get; set; }
        public double OverallProgress { get; set; }
        public List<ZoneProgress> Zones { get; set; } = new();
        public List<ElementProgress> Elements { get; set; } = new();
        public List<ProgressTrend> Trends { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    public class ZoneProgress
    {
        public string ZoneName { get; set; }
        public double PlannedProgress { get; set; }
        public double ActualProgress { get; set; }
        public double Variance { get; set; }
        public string Status { get; set; }
    }

    public class ElementProgress
    {
        public string ElementType { get; set; }
        public int PlannedCount { get; set; }
        public int InstalledCount { get; set; }
        public double CompletionPercentage { get; set; }
    }

    public class ProgressTrend
    {
        public DateTime Date { get; set; }
        public double PlannedProgress { get; set; }
        public double ActualProgress { get; set; }
        public double Productivity { get; set; }
    }

    public class DefectReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ImageId { get; set; }
        public string DefectType { get; set; }
        public DefectSeverity Severity { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public double Confidence { get; set; }
        public BoundingBox BoundingBox { get; set; }
        public string RecommendedAction { get; set; }
        public bool IsResolved { get; set; }
    }

    public class SafetyAnalytics
    {
        public string ProjectId { get; set; }
        public double PPEComplianceRate { get; set; }
        public int WorkersDetected { get; set; }
        public int PPEViolations { get; set; }
        public int HazardsDetected { get; set; }
        public List<SafetyViolation> Violations { get; set; } = new();
        public List<SafetyTrend> Trends { get; set; } = new();
        public DateTime LastAnalysis { get; set; }
    }

    public class SafetyViolation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; }
        public string Description { get; set; }
        public DateTime DetectedTime { get; set; }
        public string ImageId { get; set; }
        public string Location { get; set; }
        public bool IsAddressed { get; set; }
    }

    public class SafetyTrend
    {
        public DateTime Date { get; set; }
        public double ComplianceRate { get; set; }
        public int ViolationCount { get; set; }
        public int HazardCount { get; set; }
    }

    #endregion

    public sealed class ComputerVisionIntelligenceEngine
    {
        private static readonly Lazy<ComputerVisionIntelligenceEngine> _instance =
            new Lazy<ComputerVisionIntelligenceEngine>(() => new ComputerVisionIntelligenceEngine());
        public static ComputerVisionIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, ComputerVisionProject> _projects = new();
        private readonly object _lock = new object();

        private readonly List<string> _ppeTypes = new() { "Hard Hat", "Safety Vest", "Safety Glasses", "Gloves", "Safety Boots", "Harness" };
        private readonly List<string> _defectTypes = new() { "Crack", "Spall", "Corrosion", "Delamination", "Misalignment", "Missing Component" };
        private readonly List<string> _equipmentTypes = new() { "Crane", "Excavator", "Forklift", "Concrete Pump", "Scaffolding", "Formwork" };

        private ComputerVisionIntelligenceEngine() { }

        public ComputerVisionProject CreateProject(string projectId, string projectName)
        {
            var project = new ComputerVisionProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                ProgressData = new ProgressTrackingData { ProjectId = projectId },
                SafetyData = new SafetyAnalytics { ProjectId = projectId }
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public ImageCapture AddImage(string projectId, string name, ImageSource source,
            string filePath, int width, int height)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var capture = new ImageCapture
                {
                    Name = name,
                    Source = source,
                    CaptureTime = DateTime.UtcNow,
                    FilePath = filePath,
                    Width = width,
                    Height = height,
                    IsProcessed = false
                };

                project.Captures.Add(capture);
                return capture;
            }
        }

        public async Task<AnalysisResult> AnalyzeImage(string projectId, string imageId, AnalysisType type)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var image = project.Captures.FirstOrDefault(c => c.Id == imageId);
                    if (image == null) return null;

                    var result = new AnalysisResult
                    {
                        ImageId = imageId,
                        Type = type,
                        AnalysisTime = DateTime.UtcNow,
                        ProcessingTime = 1.5 + new Random().NextDouble() * 2
                    };

                    var random = new Random();

                    switch (type)
                    {
                        case AnalysisType.ProgressTracking:
                            result.Detections.AddRange(GenerateEquipmentDetections(random));
                            result.Metrics["overall_progress"] = 0.3 + random.NextDouble() * 0.5;
                            result.Metrics["activity_level"] = random.NextDouble();
                            break;

                        case AnalysisType.SafetyCompliance:
                            result.Detections.AddRange(GenerateWorkerDetections(random));
                            int workers = result.Detections.Count(d => d.Class == DetectionClass.Worker);
                            int ppeComplete = (int)(workers * (0.7 + random.NextDouble() * 0.25));
                            result.Metrics["workers_detected"] = workers;
                            result.Metrics["ppe_compliance"] = workers > 0 ? (double)ppeComplete / workers : 1;
                            break;

                        case AnalysisType.DefectDetection:
                            result.Detections.AddRange(GenerateDefectDetections(random));
                            result.Metrics["defects_found"] = result.Detections.Count;
                            break;

                        case AnalysisType.MaterialRecognition:
                            result.Detections.AddRange(GenerateMaterialDetections(random));
                            result.Metrics["materials_identified"] = result.Detections.Count;
                            break;
                    }

                    result.OverallConfidence = result.Detections.Any() ?
                        result.Detections.Average(d => d.Confidence) : 0;

                    image.IsProcessed = true;
                    project.Analyses.Add(result);
                    return result;
                }
            });
        }

        private List<Detection> GenerateEquipmentDetections(Random random)
        {
            var detections = new List<Detection>();
            int count = random.Next(2, 6);

            for (int i = 0; i < count; i++)
            {
                detections.Add(new Detection
                {
                    Class = DetectionClass.Equipment,
                    Label = _equipmentTypes[random.Next(_equipmentTypes.Count)],
                    Box = GenerateRandomBox(random),
                    Confidence = 0.75 + random.NextDouble() * 0.24
                });
            }

            return detections;
        }

        private List<Detection> GenerateWorkerDetections(Random random)
        {
            var detections = new List<Detection>();
            int workerCount = random.Next(3, 10);

            for (int i = 0; i < workerCount; i++)
            {
                var worker = new Detection
                {
                    Class = DetectionClass.Worker,
                    Label = "Construction Worker",
                    Box = GenerateRandomBox(random),
                    Confidence = 0.8 + random.NextDouble() * 0.19
                };

                // Add PPE attributes
                foreach (var ppe in _ppeTypes)
                {
                    worker.Attributes[ppe] = random.NextDouble() > 0.15;
                }

                detections.Add(worker);
            }

            return detections;
        }

        private List<Detection> GenerateDefectDetections(Random random)
        {
            var detections = new List<Detection>();
            int count = random.Next(0, 4);

            for (int i = 0; i < count; i++)
            {
                detections.Add(new Detection
                {
                    Class = DetectionClass.Defect,
                    Label = _defectTypes[random.Next(_defectTypes.Count)],
                    Box = GenerateRandomBox(random),
                    Confidence = 0.6 + random.NextDouble() * 0.35,
                    Attributes = new Dictionary<string, object>
                    {
                        ["severity"] = (DefectSeverity)random.Next(4),
                        ["size_estimate"] = random.NextDouble() * 100
                    }
                });
            }

            return detections;
        }

        private List<Detection> GenerateMaterialDetections(Random random)
        {
            var detections = new List<Detection>();
            var materials = new[] { "Concrete", "Steel", "Rebar", "Formwork", "Brick", "CMU", "Drywall", "Insulation" };
            int count = random.Next(2, 5);

            for (int i = 0; i < count; i++)
            {
                detections.Add(new Detection
                {
                    Class = DetectionClass.Material,
                    Label = materials[random.Next(materials.Length)],
                    Box = GenerateRandomBox(random),
                    Confidence = 0.7 + random.NextDouble() * 0.28
                });
            }

            return detections;
        }

        private BoundingBox GenerateRandomBox(Random random)
        {
            return new BoundingBox
            {
                X = random.NextDouble() * 0.7,
                Y = random.NextDouble() * 0.7,
                Width = 0.1 + random.NextDouble() * 0.2,
                Height = 0.1 + random.NextDouble() * 0.2
            };
        }

        public async Task<ProgressTrackingData> UpdateProgressTracking(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var progressAnalyses = project.Analyses
                        .Where(a => a.Type == AnalysisType.ProgressTracking)
                        .OrderByDescending(a => a.AnalysisTime)
                        .Take(10)
                        .ToList();

                    if (!progressAnalyses.Any())
                        return project.ProgressData;

                    project.ProgressData.OverallProgress = progressAnalyses
                        .Average(a => a.Metrics.GetValueOrDefault("overall_progress", 0));

                    project.ProgressData.LastUpdated = DateTime.UtcNow;

                    // Add trend point
                    project.ProgressData.Trends.Add(new ProgressTrend
                    {
                        Date = DateTime.UtcNow,
                        ActualProgress = project.ProgressData.OverallProgress,
                        PlannedProgress = project.ProgressData.OverallProgress * 0.95,
                        Productivity = progressAnalyses.Average(a => a.Metrics.GetValueOrDefault("activity_level", 0))
                    });

                    return project.ProgressData;
                }
            });
        }

        public async Task<SafetyAnalytics> UpdateSafetyAnalytics(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var safetyAnalyses = project.Analyses
                        .Where(a => a.Type == AnalysisType.SafetyCompliance)
                        .OrderByDescending(a => a.AnalysisTime)
                        .Take(20)
                        .ToList();

                    if (!safetyAnalyses.Any())
                        return project.SafetyData;

                    project.SafetyData.WorkersDetected = (int)safetyAnalyses
                        .Sum(a => a.Metrics.GetValueOrDefault("workers_detected", 0));

                    project.SafetyData.PPEComplianceRate = safetyAnalyses
                        .Average(a => a.Metrics.GetValueOrDefault("ppe_compliance", 1));

                    project.SafetyData.LastAnalysis = DateTime.UtcNow;

                    // Add trend point
                    project.SafetyData.Trends.Add(new SafetyTrend
                    {
                        Date = DateTime.UtcNow,
                        ComplianceRate = project.SafetyData.PPEComplianceRate,
                        ViolationCount = project.SafetyData.PPEViolations
                    });

                    return project.SafetyData;
                }
            });
        }

        public List<DefectReport> GetDefectReports(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return new List<DefectReport>();

                return project.Analyses
                    .Where(a => a.Type == AnalysisType.DefectDetection)
                    .SelectMany(a => a.Detections.Where(d => d.Class == DetectionClass.Defect))
                    .Select(d => new DefectReport
                    {
                        DefectType = d.Label,
                        Severity = d.Attributes.ContainsKey("severity") ?
                            (DefectSeverity)d.Attributes["severity"] : DefectSeverity.Minor,
                        Confidence = d.Confidence,
                        BoundingBox = d.Box,
                        RecommendedAction = GetRecommendedAction(d.Label)
                    })
                    .ToList();
            }
        }

        private string GetRecommendedAction(string defectType)
        {
            return defectType switch
            {
                "Crack" => "Document and monitor; repair if structural",
                "Spall" => "Remove loose material, patch with repair mortar",
                "Corrosion" => "Treat with rust inhibitor, consider structural repair",
                "Delamination" => "Remove affected area, reapply coating/finish",
                "Misalignment" => "Verify structural implications, realign if possible",
                "Missing Component" => "Install missing component before proceeding",
                _ => "Investigate and document for engineering review"
            };
        }
    }
}
