// ===================================================================
// StingBIM Digital Delivery Intelligence Engine
// IFC/OpenBIM, COBie, model exchange, interoperability
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.DigitalDeliveryIntelligence
{
    #region Enums

    public enum ExchangeFormat { IFC2x3, IFC4, IFC4x3, COBie, BCF, gbXML, CityGML, LandXML, GeoJSON }
    public enum MVDType { ReferenceView, DesignTransfer, CoordinationView, QuantityTakeoff, FacilityManagement }
    public enum ValidationSeverity { Information, Warning, Error, Critical }
    public enum DeliverableStatus { Draft, ForReview, Approved, Issued, Superseded }
    public enum ExchangeDirection { Export, Import, RoundTrip }
    public enum PropertySetType { Standard, Custom, COBie, Classification }

    #endregion

    #region Data Models

    public class DigitalDeliveryProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public List<ModelExchange> Exchanges { get; set; } = new();
        public List<IFCModel> Models { get; set; } = new();
        public List<COBieDeliverable> COBieDeliverables { get; set; } = new();
        public ExchangeRequirements Requirements { get; set; }
        public DataDropSchedule Schedule { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ModelExchange
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public ExchangeFormat Format { get; set; }
        public MVDType MVD { get; set; }
        public ExchangeDirection Direction { get; set; }
        public string SourceApplication { get; set; }
        public string TargetApplication { get; set; }
        public DateTime ExchangeDate { get; set; }
        public string FilePath { get; set; }
        public double FileSize { get; set; }
        public ValidationReport Validation { get; set; }
        public List<ExchangeIssue> Issues { get; set; } = new();
    }

    public class IFCModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public ExchangeFormat SchemaVersion { get; set; }
        public MVDType MVD { get; set; }
        public string FilePath { get; set; }
        public double FileSize { get; set; }
        public IFCStatistics Statistics { get; set; }
        public List<IFCSpatialStructure> SpatialStructure { get; set; } = new();
        public List<IFCPropertySetDefinition> PropertySets { get; set; } = new();
        public GeoreferenceInfo Georeference { get; set; }
        public DateTime CreatedDate { get; set; }
        public string AuthoringApplication { get; set; }
    }

    public class IFCStatistics
    {
        public int TotalEntities { get; set; }
        public int TotalRelationships { get; set; }
        public Dictionary<string, int> EntityCounts { get; set; } = new();
        public int PropertySetCount { get; set; }
        public int MaterialCount { get; set; }
        public int TypeCount { get; set; }
        public int GeometryCount { get; set; }
        public long TriangleCount { get; set; }
    }

    public class IFCSpatialStructure
    {
        public string GlobalId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string LongName { get; set; }
        public List<IFCSpatialStructure> Children { get; set; } = new();
        public int ElementCount { get; set; }
    }

    public class IFCPropertySetDefinition
    {
        public string Name { get; set; }
        public PropertySetType Type { get; set; }
        public List<string> Properties { get; set; } = new();
        public int UsageCount { get; set; }
        public bool IsStandard { get; set; }
    }

    public class GeoreferenceInfo
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Elevation { get; set; }
        public string CoordinateSystem { get; set; }
        public string EPSG { get; set; }
        public double TrueNorthAngle { get; set; }
        public bool IsGeoreferenced { get; set; }
    }

    public class ValidationReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ModelId { get; set; }
        public DateTime ValidationDate { get; set; }
        public string ValidatorTool { get; set; }
        public MVDType MVDChecked { get; set; }
        public bool IsValid { get; set; }
        public int TotalChecks { get; set; }
        public int PassedChecks { get; set; }
        public int FailedChecks { get; set; }
        public List<ValidationIssue> Issues { get; set; } = new();
        public double ComplianceScore { get; set; }
    }

    public class ValidationIssue
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ValidationSeverity Severity { get; set; }
        public string RuleId { get; set; }
        public string RuleDescription { get; set; }
        public string EntityGuid { get; set; }
        public string EntityType { get; set; }
        public string Message { get; set; }
        public string Location { get; set; }
        public string Suggestion { get; set; }
    }

    public class ExchangeIssue
    {
        public string Category { get; set; }
        public string Description { get; set; }
        public int AffectedElements { get; set; }
        public string Resolution { get; set; }
    }

    public class COBieDeliverable
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Version { get; set; }
        public DeliverableStatus Status { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public List<COBieSheet> Sheets { get; set; } = new();
        public COBieValidation Validation { get; set; }
        public string FilePath { get; set; }
    }

    public class COBieSheet
    {
        public string Name { get; set; }
        public int RowCount { get; set; }
        public int RequiredFields { get; set; }
        public int PopulatedFields { get; set; }
        public double CompletionPercentage => RequiredFields > 0 ? PopulatedFields * 100.0 / RequiredFields : 0;
        public List<string> MissingRequiredFields { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class COBieValidation
    {
        public bool IsValid { get; set; }
        public int TotalErrors { get; set; }
        public int TotalWarnings { get; set; }
        public double OverallCompleteness { get; set; }
        public Dictionary<string, double> SheetCompleteness { get; set; } = new();
    }

    public class ExchangeRequirements
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ExchangeFormat PrimaryFormat { get; set; }
        public MVDType RequiredMVD { get; set; }
        public List<string> RequiredPropertySets { get; set; } = new();
        public List<string> RequiredClassifications { get; set; } = new();
        public bool RequireGeoreference { get; set; }
        public double MaxFileSize { get; set; }
        public List<string> RequiredLOD { get; set; } = new();
        public string NamingConvention { get; set; }
    }

    public class DataDropSchedule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<DataDrop> Drops { get; set; } = new();
    }

    public class DataDrop
    {
        public string Name { get; set; }
        public string Phase { get; set; }
        public DateTime DueDate { get; set; }
        public List<string> RequiredDeliverables { get; set; } = new();
        public DeliverableStatus Status { get; set; }
        public string Notes { get; set; }
    }

    public class InteroperabilityReport
    {
        public string SourceFormat { get; set; }
        public string TargetFormat { get; set; }
        public int TotalElements { get; set; }
        public int SuccessfullyMapped { get; set; }
        public int PartiallyMapped { get; set; }
        public int FailedToMap { get; set; }
        public double MappingSuccessRate { get; set; }
        public List<MappingIssue> Issues { get; set; } = new();
    }

    public class MappingIssue
    {
        public string SourceElement { get; set; }
        public string Issue { get; set; }
        public string Recommendation { get; set; }
    }

    #endregion

    public sealed class DigitalDeliveryIntelligenceEngine
    {
        private static readonly Lazy<DigitalDeliveryIntelligenceEngine> _instance =
            new Lazy<DigitalDeliveryIntelligenceEngine>(() => new DigitalDeliveryIntelligenceEngine());
        public static DigitalDeliveryIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, DigitalDeliveryProject> _projects = new();
        private readonly object _lock = new object();

        // Standard IFC property sets
        private readonly List<string> _standardPropertySets = new()
        {
            "Pset_WallCommon", "Pset_DoorCommon", "Pset_WindowCommon", "Pset_SlabCommon",
            "Pset_ColumnCommon", "Pset_BeamCommon", "Pset_RoofCommon", "Pset_StairCommon",
            "Pset_SpaceCommon", "Pset_BuildingCommon", "Pset_SiteCommon"
        };

        // COBie required sheets
        private readonly List<string> _cobieSheets = new()
        {
            "Contact", "Facility", "Floor", "Space", "Zone", "Type", "Component",
            "System", "Assembly", "Spare", "Resource", "Job", "Document", "Attribute"
        };

        private DigitalDeliveryIntelligenceEngine() { }

        public DigitalDeliveryProject CreateProject(string projectId, string projectName)
        {
            var project = new DigitalDeliveryProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                Requirements = new ExchangeRequirements
                {
                    PrimaryFormat = ExchangeFormat.IFC4,
                    RequiredMVD = MVDType.ReferenceView,
                    RequireGeoreference = true
                }
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public IFCModel RegisterModel(string projectId, string name, ExchangeFormat schema,
            string filePath, double fileSize, string authoringApp)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var model = new IFCModel
                {
                    Name = name,
                    SchemaVersion = schema,
                    FilePath = filePath,
                    FileSize = fileSize,
                    AuthoringApplication = authoringApp,
                    CreatedDate = DateTime.UtcNow,
                    Statistics = new IFCStatistics()
                };

                project.Models.Add(model);
                return model;
            }
        }

        public async Task<ValidationReport> ValidateIFCModel(string projectId, string modelId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var model = project.Models.FirstOrDefault(m => m.Id == modelId);
                    if (model == null) return null;

                    var report = new ValidationReport
                    {
                        ModelId = modelId,
                        ValidationDate = DateTime.UtcNow,
                        ValidatorTool = "StingBIM IFC Validator",
                        MVDChecked = project.Requirements?.RequiredMVD ?? MVDType.ReferenceView
                    };

                    var random = new Random();

                    // Simulate validation checks
                    report.TotalChecks = 150;
                    report.FailedChecks = random.Next(0, 15);
                    report.PassedChecks = report.TotalChecks - report.FailedChecks;
                    report.IsValid = report.FailedChecks < 5;
                    report.ComplianceScore = report.PassedChecks * 100.0 / report.TotalChecks;

                    // Generate sample issues
                    var issueTypes = new[]
                    {
                        ("Missing GlobalId", ValidationSeverity.Error),
                        ("Invalid property value", ValidationSeverity.Warning),
                        ("Missing required property set", ValidationSeverity.Error),
                        ("Geometry validation failed", ValidationSeverity.Warning),
                        ("Missing classification reference", ValidationSeverity.Information)
                    };

                    for (int i = 0; i < report.FailedChecks; i++)
                    {
                        var (msg, sev) = issueTypes[random.Next(issueTypes.Length)];
                        report.Issues.Add(new ValidationIssue
                        {
                            Severity = sev,
                            RuleId = $"MVD-{random.Next(100, 999)}",
                            Message = msg,
                            EntityType = "IfcWall",
                            Suggestion = "Review and correct the identified issue"
                        });
                    }

                    return report;
                }
            });
        }

        public COBieDeliverable CreateCOBieDeliverable(string projectId, string name, DateTime dueDate)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var deliverable = new COBieDeliverable
                {
                    Name = name,
                    Version = "2.4",
                    DueDate = dueDate,
                    Status = DeliverableStatus.Draft
                };

                // Initialize sheets
                foreach (var sheetName in _cobieSheets)
                {
                    deliverable.Sheets.Add(new COBieSheet
                    {
                        Name = sheetName,
                        RequiredFields = GetRequiredFieldCount(sheetName)
                    });
                }

                project.COBieDeliverables.Add(deliverable);
                return deliverable;
            }
        }

        private int GetRequiredFieldCount(string sheetName)
        {
            return sheetName switch
            {
                "Contact" => 8,
                "Facility" => 12,
                "Floor" => 6,
                "Space" => 10,
                "Zone" => 5,
                "Type" => 15,
                "Component" => 12,
                "System" => 6,
                "Assembly" => 5,
                "Spare" => 8,
                "Resource" => 6,
                "Job" => 10,
                "Document" => 8,
                "Attribute" => 6,
                _ => 5
            };
        }

        public async Task<COBieValidation> ValidateCOBie(string projectId, string deliverableId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var deliverable = project.COBieDeliverables.FirstOrDefault(d => d.Id == deliverableId);
                    if (deliverable == null) return null;

                    var validation = new COBieValidation();
                    var random = new Random();

                    foreach (var sheet in deliverable.Sheets)
                    {
                        sheet.PopulatedFields = (int)(sheet.RequiredFields * (0.6 + random.NextDouble() * 0.35));
                        validation.SheetCompleteness[sheet.Name] = sheet.CompletionPercentage;

                        if (sheet.CompletionPercentage < 100)
                        {
                            sheet.MissingRequiredFields.Add($"{sheet.Name}_Field1");
                            validation.TotalWarnings++;
                        }
                        if (sheet.CompletionPercentage < 80)
                        {
                            sheet.Errors.Add($"Critical data missing in {sheet.Name}");
                            validation.TotalErrors++;
                        }
                    }

                    validation.OverallCompleteness = validation.SheetCompleteness.Values.Average();
                    validation.IsValid = validation.TotalErrors == 0 && validation.OverallCompleteness >= 90;

                    deliverable.Validation = validation;
                    return validation;
                }
            });
        }

        public ModelExchange RecordExchange(string projectId, string name, ExchangeFormat format,
            ExchangeDirection direction, string sourceApp, string targetApp)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var exchange = new ModelExchange
                {
                    Name = name,
                    Format = format,
                    Direction = direction,
                    SourceApplication = sourceApp,
                    TargetApplication = targetApp,
                    ExchangeDate = DateTime.UtcNow
                };

                project.Exchanges.Add(exchange);
                return exchange;
            }
        }

        public async Task<InteroperabilityReport> AnalyzeInteroperability(string projectId,
            string sourceModelId, string targetFormat)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var model = project.Models.FirstOrDefault(m => m.Id == sourceModelId);
                    if (model == null) return null;

                    var random = new Random();
                    int totalElements = model.Statistics?.TotalEntities ?? 1000;

                    var report = new InteroperabilityReport
                    {
                        SourceFormat = model.SchemaVersion.ToString(),
                        TargetFormat = targetFormat,
                        TotalElements = totalElements,
                        SuccessfullyMapped = (int)(totalElements * (0.85 + random.NextDouble() * 0.1)),
                        PartiallyMapped = (int)(totalElements * random.NextDouble() * 0.08)
                    };

                    report.FailedToMap = totalElements - report.SuccessfullyMapped - report.PartiallyMapped;
                    report.MappingSuccessRate = report.SuccessfullyMapped * 100.0 / totalElements;

                    // Common interoperability issues
                    if (targetFormat == "Revit" && model.SchemaVersion == ExchangeFormat.IFC2x3)
                    {
                        report.Issues.Add(new MappingIssue
                        {
                            SourceElement = "IfcBuildingElementProxy",
                            Issue = "Generic elements may lose specific type information",
                            Recommendation = "Use IFC4 with proper entity types"
                        });
                    }

                    if (report.PartiallyMapped > 0)
                    {
                        report.Issues.Add(new MappingIssue
                        {
                            SourceElement = "Custom PropertySets",
                            Issue = "Non-standard property sets may not transfer",
                            Recommendation = "Use IFC standard property sets where possible"
                        });
                    }

                    return report;
                }
            });
        }

        public void SetExchangeRequirements(string projectId, ExchangeFormat format, MVDType mvd,
            List<string> propertySets, bool requireGeoref)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return;

                project.Requirements = new ExchangeRequirements
                {
                    PrimaryFormat = format,
                    RequiredMVD = mvd,
                    RequiredPropertySets = propertySets ?? new List<string>(),
                    RequireGeoreference = requireGeoref
                };
            }
        }

        public DataDropSchedule CreateDataDropSchedule(string projectId, List<(string name, string phase, DateTime due)> drops)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var schedule = new DataDropSchedule();

                foreach (var (name, phase, due) in drops)
                {
                    schedule.Drops.Add(new DataDrop
                    {
                        Name = name,
                        Phase = phase,
                        DueDate = due,
                        Status = DeliverableStatus.Draft,
                        RequiredDeliverables = new List<string> { "IFC Model", "COBie Spreadsheet", "Validation Report" }
                    });
                }

                project.Schedule = schedule;
                return schedule;
            }
        }

        public List<string> GetStandardPropertySets() => new List<string>(_standardPropertySets);

        public List<string> GetCOBieSheets() => new List<string>(_cobieSheets);

        public string GetRecommendedFormat(string useCase)
        {
            return useCase.ToLower() switch
            {
                "coordination" => "IFC4 Reference View",
                "design transfer" => "IFC4 Design Transfer View",
                "facility management" => "COBie 2.4 + IFC4",
                "quantity takeoff" => "IFC4 QTO View",
                "energy analysis" => "gbXML",
                "gis integration" => "IFC4x3 + CityGML",
                _ => "IFC4 Reference View"
            };
        }
    }
}
