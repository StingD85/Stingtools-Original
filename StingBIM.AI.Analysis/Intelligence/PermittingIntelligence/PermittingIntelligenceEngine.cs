// ===================================================================
// StingBIM Permitting Intelligence Engine
// Regulatory workflow, permit tracking, code requirements, approvals
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.PermittingIntelligence
{
    #region Enums

    public enum PermitType { Building, Demolition, Grading, Plumbing, Mechanical, Electrical, Fire, Zoning, Environmental }
    public enum PermitStatus { NotStarted, Preparing, Submitted, InReview, Corrections, Approved, Issued, Expired, Revoked }
    public enum ReviewDepartment { Building, Planning, Fire, Health, Environmental, Transportation, Utilities, Historic }
    public enum SubmissionType { Paper, Electronic, BIM }
    public enum InspectionType { Foundation, Framing, Rough_In, Insulation, Drywall, Final, Special }
    public enum InspectionStatus { Scheduled, Passed, Failed, Reinspection, Approved }

    #endregion

    #region Data Models

    public class PermittingProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string JurisdictionCode { get; set; }
        public string JurisdictionName { get; set; }
        public string Address { get; set; }
        public string ParcelNumber { get; set; }
        public string ZoningDistrict { get; set; }
        public List<Permit> Permits { get; set; } = new();
        public List<ReviewCycle> Reviews { get; set; } = new();
        public List<Inspection> Inspections { get; set; } = new();
        public PermittingTimeline Timeline { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Permit
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PermitNumber { get; set; }
        public PermitType Type { get; set; }
        public PermitStatus Status { get; set; }
        public string Description { get; set; }
        public decimal Valuation { get; set; }
        public decimal Fee { get; set; }
        public decimal PlanCheckFee { get; set; }
        public decimal ImpactFees { get; set; }
        public DateTime ApplicationDate { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public DateTime? IssuedDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public List<SubmissionDocument> Documents { get; set; } = new();
        public List<PermitCondition> Conditions { get; set; } = new();
        public List<string> RequiredApprovals { get; set; } = new();
        public string AssignedReviewer { get; set; }
    }

    public class SubmissionDocument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsRequired { get; set; }
        public bool IsSubmitted { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public string Status { get; set; }
        public string FilePath { get; set; }
        public int Version { get; set; } = 1;
        public List<string> ReviewComments { get; set; } = new();
    }

    public class PermitCondition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Code { get; set; }
        public string Description { get; set; }
        public string Department { get; set; }
        public bool IsSatisfied { get; set; }
        public DateTime? DueDate { get; set; }
        public string ResponseRequired { get; set; }
    }

    public class ReviewCycle
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PermitId { get; set; }
        public int CycleNumber { get; set; }
        public DateTime SubmissionDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public int ReviewDays { get; set; }
        public List<DepartmentReview> DepartmentReviews { get; set; } = new();
        public int TotalComments { get; set; }
        public int ResolvedComments { get; set; }
        public string Status { get; set; }
    }

    public class DepartmentReview
    {
        public ReviewDepartment Department { get; set; }
        public string Reviewer { get; set; }
        public string Status { get; set; }
        public DateTime? ReviewDate { get; set; }
        public List<ReviewComment> Comments { get; set; } = new();
        public bool IsApproved { get; set; }
    }

    public class ReviewComment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Code { get; set; }
        public string Sheet { get; set; }
        public string Location { get; set; }
        public string Comment { get; set; }
        public string CodeReference { get; set; }
        public bool IsResolved { get; set; }
        public string Response { get; set; }
    }

    public class Inspection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PermitId { get; set; }
        public InspectionType Type { get; set; }
        public InspectionStatus Status { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string Inspector { get; set; }
        public string Location { get; set; }
        public List<string> Corrections { get; set; } = new();
        public string Notes { get; set; }
        public int AttemptNumber { get; set; } = 1;
    }

    public class PermittingTimeline
    {
        public DateTime ProjectStart { get; set; }
        public DateTime DesignComplete { get; set; }
        public DateTime PermitSubmission { get; set; }
        public DateTime EstimatedApproval { get; set; }
        public DateTime ConstructionStart { get; set; }
        public DateTime ConstructionComplete { get; set; }
        public DateTime CertificateOfOccupancy { get; set; }
        public int TotalPermitDays { get; set; }
        public List<Milestone> Milestones { get; set; } = new();
    }

    public class Milestone
    {
        public string Name { get; set; }
        public DateTime PlannedDate { get; set; }
        public DateTime? ActualDate { get; set; }
        public string Status { get; set; }
        public int DaysVariance { get; set; }
    }

    public class ZoningAnalysis
    {
        public string District { get; set; }
        public string DistrictDescription { get; set; }
        public List<ZoningRequirement> Requirements { get; set; } = new();
        public List<ZoningVariance> VariancesNeeded { get; set; } = new();
        public bool IsConforming { get; set; }
        public List<string> SpecialApprovals { get; set; } = new();
    }

    public class ZoningRequirement
    {
        public string Category { get; set; }
        public string Requirement { get; set; }
        public string AllowedValue { get; set; }
        public string ProposedValue { get; set; }
        public bool IsCompliant { get; set; }
    }

    public class ZoningVariance
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public string Justification { get; set; }
        public string RequiredRelief { get; set; }
        public string ApprovalBody { get; set; }
        public DateTime? HearingDate { get; set; }
    }

    public class FeeSchedule
    {
        public string JurisdictionCode { get; set; }
        public Dictionary<PermitType, decimal> BaseFees { get; set; } = new();
        public decimal PlanCheckMultiplier { get; set; }
        public Dictionary<string, decimal> ImpactFees { get; set; } = new();
        public double ValuationRate { get; set; }
        public DateTime EffectiveDate { get; set; }
    }

    #endregion

    public sealed class PermittingIntelligenceEngine
    {
        private static readonly Lazy<PermittingIntelligenceEngine> _instance =
            new Lazy<PermittingIntelligenceEngine>(() => new PermittingIntelligenceEngine());
        public static PermittingIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, PermittingProject> _projects = new();
        private readonly Dictionary<string, FeeSchedule> _feeSchedules = new();
        private readonly object _lock = new object();

        // Typical review timeframes by jurisdiction type (days)
        private readonly Dictionary<string, int> _typicalReviewTimes = new()
        {
            ["Major_City"] = 45,
            ["Suburban"] = 30,
            ["Rural"] = 21,
            ["Expedited"] = 14,
            ["Fast_Track"] = 7
        };

        // Required documents by permit type
        private readonly Dictionary<PermitType, List<string>> _requiredDocuments = new()
        {
            [PermitType.Building] = new() { "Architectural Plans", "Structural Plans", "Site Plan", "Energy Calculations", "Soils Report", "Title 24" },
            [PermitType.Mechanical] = new() { "Mechanical Plans", "Equipment Schedules", "Load Calculations" },
            [PermitType.Electrical] = new() { "Electrical Plans", "Panel Schedules", "Load Calculations" },
            [PermitType.Plumbing] = new() { "Plumbing Plans", "Isometric Diagrams", "Fixture Schedules" },
            [PermitType.Fire] = new() { "Fire Protection Plans", "Hydraulic Calculations", "Fire Alarm Plans" },
            [PermitType.Grading] = new() { "Grading Plans", "Erosion Control Plan", "Drainage Study" }
        };

        private PermittingIntelligenceEngine()
        {
            InitializeDefaultFeeSchedules();
        }

        private void InitializeDefaultFeeSchedules()
        {
            _feeSchedules["DEFAULT"] = new FeeSchedule
            {
                JurisdictionCode = "DEFAULT",
                BaseFees = new Dictionary<PermitType, decimal>
                {
                    [PermitType.Building] = 500,
                    [PermitType.Demolition] = 200,
                    [PermitType.Grading] = 300,
                    [PermitType.Plumbing] = 200,
                    [PermitType.Mechanical] = 200,
                    [PermitType.Electrical] = 200,
                    [PermitType.Fire] = 400
                },
                PlanCheckMultiplier = 0.65m,
                ValuationRate = 0.01,
                EffectiveDate = DateTime.UtcNow
            };
        }

        public PermittingProject CreatePermittingProject(string projectId, string projectName,
            string jurisdiction, string address, string parcelNumber, string zoningDistrict)
        {
            var project = new PermittingProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                JurisdictionCode = jurisdiction,
                JurisdictionName = jurisdiction,
                Address = address,
                ParcelNumber = parcelNumber,
                ZoningDistrict = zoningDistrict
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public Permit CreatePermit(string projectId, PermitType type, string description, decimal valuation)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var permit = new Permit
                {
                    PermitNumber = $"BP-{DateTime.Now:yyyyMMdd}-{project.Permits.Count + 1:D3}",
                    Type = type,
                    Status = PermitStatus.NotStarted,
                    Description = description,
                    Valuation = valuation,
                    ApplicationDate = DateTime.UtcNow
                };

                // Calculate fees
                var feeSchedule = _feeSchedules.GetValueOrDefault(project.JurisdictionCode) ??
                    _feeSchedules["DEFAULT"];

                decimal baseFee = feeSchedule.BaseFees.GetValueOrDefault(type, 500);
                decimal valuationFee = valuation * (decimal)feeSchedule.ValuationRate;
                permit.Fee = baseFee + valuationFee;
                permit.PlanCheckFee = permit.Fee * feeSchedule.PlanCheckMultiplier;

                // Add required documents
                var requiredDocs = _requiredDocuments.GetValueOrDefault(type, new List<string>());
                foreach (var doc in requiredDocs)
                {
                    permit.Documents.Add(new SubmissionDocument
                    {
                        Name = doc,
                        Type = "Drawing Set",
                        IsRequired = true,
                        IsSubmitted = false
                    });
                }

                // Set required approvals
                permit.RequiredApprovals = type switch
                {
                    PermitType.Building => new() { "Building", "Planning", "Fire" },
                    PermitType.Fire => new() { "Fire" },
                    PermitType.Mechanical => new() { "Mechanical" },
                    PermitType.Plumbing => new() { "Plumbing" },
                    PermitType.Electrical => new() { "Electrical" },
                    PermitType.Grading => new() { "Engineering", "Environmental" },
                    _ => new() { "Building" }
                };

                project.Permits.Add(permit);
                return permit;
            }
        }

        public ReviewCycle StartReviewCycle(string projectId, string permitId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var permit = project.Permits.FirstOrDefault(p => p.Id == permitId);
                if (permit == null) return null;

                int cycleNumber = project.Reviews.Count(r => r.PermitId == permitId) + 1;

                var cycle = new ReviewCycle
                {
                    PermitId = permitId,
                    CycleNumber = cycleNumber,
                    SubmissionDate = DateTime.UtcNow,
                    Status = "In Review"
                };

                // Initialize department reviews
                foreach (var dept in permit.RequiredApprovals)
                {
                    var department = Enum.TryParse<ReviewDepartment>(dept, out var d) ? d : ReviewDepartment.Building;
                    cycle.DepartmentReviews.Add(new DepartmentReview
                    {
                        Department = department,
                        Status = "Pending"
                    });
                }

                permit.Status = PermitStatus.InReview;
                project.Reviews.Add(cycle);
                return cycle;
            }
        }

        public void AddReviewComment(string projectId, string cycleId, ReviewDepartment department,
            string code, string sheet, string comment, string codeReference)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return;

                var cycle = project.Reviews.FirstOrDefault(r => r.Id == cycleId);
                if (cycle == null) return;

                var deptReview = cycle.DepartmentReviews.FirstOrDefault(d => d.Department == department);
                if (deptReview == null) return;

                deptReview.Comments.Add(new ReviewComment
                {
                    Code = code,
                    Sheet = sheet,
                    Comment = comment,
                    CodeReference = codeReference,
                    IsResolved = false
                });

                cycle.TotalComments++;
            }
        }

        public void ResolveComment(string projectId, string cycleId, string commentId, string response)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return;

                var cycle = project.Reviews.FirstOrDefault(r => r.Id == cycleId);
                if (cycle == null) return;

                var comment = cycle.DepartmentReviews
                    .SelectMany(d => d.Comments)
                    .FirstOrDefault(c => c.Id == commentId);

                if (comment != null)
                {
                    comment.IsResolved = true;
                    comment.Response = response;
                    cycle.ResolvedComments++;
                }
            }
        }

        public Inspection ScheduleInspection(string projectId, string permitId, InspectionType type,
            DateTime scheduledDate, string location)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var inspection = new Inspection
                {
                    PermitId = permitId,
                    Type = type,
                    Status = InspectionStatus.Scheduled,
                    ScheduledDate = scheduledDate,
                    Location = location
                };

                project.Inspections.Add(inspection);
                return inspection;
            }
        }

        public void RecordInspectionResult(string projectId, string inspectionId, InspectionStatus status,
            string inspector, List<string> corrections = null)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return;

                var inspection = project.Inspections.FirstOrDefault(i => i.Id == inspectionId);
                if (inspection == null) return;

                inspection.Status = status;
                inspection.CompletedDate = DateTime.UtcNow;
                inspection.Inspector = inspector;

                if (corrections != null && corrections.Any())
                {
                    inspection.Corrections = corrections;
                }

                if (status == InspectionStatus.Failed)
                {
                    // Schedule reinspection
                    var reinspection = new Inspection
                    {
                        PermitId = inspection.PermitId,
                        Type = inspection.Type,
                        Status = InspectionStatus.Scheduled,
                        ScheduledDate = DateTime.UtcNow.AddDays(3),
                        Location = inspection.Location,
                        AttemptNumber = inspection.AttemptNumber + 1
                    };
                    project.Inspections.Add(reinspection);
                }
            }
        }

        public async Task<ZoningAnalysis> AnalyzeZoning(string projectId, double lotSize, double proposedFAR,
            double proposedHeight, double proposedSetbacks, int proposedUnits)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var analysis = new ZoningAnalysis
                    {
                        District = project.ZoningDistrict,
                        DistrictDescription = GetZoningDescription(project.ZoningDistrict)
                    };

                    // Typical requirements (would be jurisdiction-specific)
                    var requirements = GetZoningRequirements(project.ZoningDistrict);

                    analysis.Requirements.Add(new ZoningRequirement
                    {
                        Category = "FAR",
                        Requirement = "Floor Area Ratio",
                        AllowedValue = requirements.maxFAR.ToString("F1"),
                        ProposedValue = proposedFAR.ToString("F1"),
                        IsCompliant = proposedFAR <= requirements.maxFAR
                    });

                    analysis.Requirements.Add(new ZoningRequirement
                    {
                        Category = "Height",
                        Requirement = "Maximum Building Height",
                        AllowedValue = $"{requirements.maxHeight}'",
                        ProposedValue = $"{proposedHeight}'",
                        IsCompliant = proposedHeight <= requirements.maxHeight
                    });

                    analysis.Requirements.Add(new ZoningRequirement
                    {
                        Category = "Setbacks",
                        Requirement = "Minimum Setbacks",
                        AllowedValue = $"{requirements.minSetback}'",
                        ProposedValue = $"{proposedSetbacks}'",
                        IsCompliant = proposedSetbacks >= requirements.minSetback
                    });

                    analysis.Requirements.Add(new ZoningRequirement
                    {
                        Category = "Density",
                        Requirement = "Maximum Density",
                        AllowedValue = $"{requirements.maxDensity} units/acre",
                        ProposedValue = $"{proposedUnits / (lotSize / 43560):F1} units/acre",
                        IsCompliant = (proposedUnits / (lotSize / 43560)) <= requirements.maxDensity
                    });

                    // Check for variances needed
                    foreach (var req in analysis.Requirements.Where(r => !r.IsCompliant))
                    {
                        analysis.VariancesNeeded.Add(new ZoningVariance
                        {
                            Type = req.Category,
                            Description = $"{req.Category} exceeds allowed {req.AllowedValue}",
                            RequiredRelief = $"Proposed {req.ProposedValue} vs allowed {req.AllowedValue}",
                            ApprovalBody = req.Category == "Height" ? "Planning Commission" : "Zoning Board"
                        });
                    }

                    analysis.IsConforming = !analysis.VariancesNeeded.Any();

                    // Special approvals
                    if (proposedUnits > 10)
                        analysis.SpecialApprovals.Add("Design Review");
                    if (proposedHeight > 50)
                        analysis.SpecialApprovals.Add("Environmental Review");

                    return analysis;
                }
            });
        }

        private string GetZoningDescription(string district)
        {
            return district.ToUpper() switch
            {
                var d when d.StartsWith("R") => "Residential District",
                var d when d.StartsWith("C") => "Commercial District",
                var d when d.StartsWith("M") => "Industrial District",
                var d when d.StartsWith("MU") => "Mixed Use District",
                _ => "General District"
            };
        }

        private (double maxFAR, double maxHeight, double minSetback, double maxDensity) GetZoningRequirements(string district)
        {
            return district.ToUpper() switch
            {
                "R1" => (0.5, 35, 20, 4),
                "R2" => (0.8, 40, 15, 12),
                "R3" => (1.5, 45, 10, 25),
                "C1" => (2.0, 50, 0, 0),
                "C2" => (3.0, 75, 0, 0),
                "MU1" => (2.5, 65, 5, 50),
                "MU2" => (4.0, 85, 0, 100),
                _ => (1.0, 40, 10, 20)
            };
        }

        public async Task<PermittingTimeline> GenerateTimeline(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var timeline = new PermittingTimeline
                    {
                        ProjectStart = DateTime.UtcNow,
                        DesignComplete = DateTime.UtcNow.AddMonths(3),
                        PermitSubmission = DateTime.UtcNow.AddMonths(4)
                    };

                    // Estimate review time
                    int reviewDays = _typicalReviewTimes.GetValueOrDefault("Suburban", 30);
                    timeline.EstimatedApproval = timeline.PermitSubmission.AddDays(reviewDays);
                    timeline.ConstructionStart = timeline.EstimatedApproval.AddDays(14);

                    // Estimate construction based on valuation
                    decimal totalValuation = project.Permits.Sum(p => p.Valuation);
                    int constructionMonths = totalValuation switch
                    {
                        < 500000 => 6,
                        < 2000000 => 12,
                        < 10000000 => 18,
                        _ => 24
                    };

                    timeline.ConstructionComplete = timeline.ConstructionStart.AddMonths(constructionMonths);
                    timeline.CertificateOfOccupancy = timeline.ConstructionComplete.AddDays(14);

                    timeline.TotalPermitDays = (timeline.EstimatedApproval - timeline.PermitSubmission).Days;

                    // Add milestones
                    timeline.Milestones.AddRange(new[]
                    {
                        new Milestone { Name = "Design Complete", PlannedDate = timeline.DesignComplete },
                        new Milestone { Name = "Permit Submission", PlannedDate = timeline.PermitSubmission },
                        new Milestone { Name = "Permit Approval", PlannedDate = timeline.EstimatedApproval },
                        new Milestone { Name = "Construction Start", PlannedDate = timeline.ConstructionStart },
                        new Milestone { Name = "Construction Complete", PlannedDate = timeline.ConstructionComplete },
                        new Milestone { Name = "Certificate of Occupancy", PlannedDate = timeline.CertificateOfOccupancy }
                    });

                    project.Timeline = timeline;
                    return timeline;
                }
            });
        }

        public decimal CalculateTotalFees(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return 0;

                return project.Permits.Sum(p => p.Fee + p.PlanCheckFee + p.ImpactFees);
            }
        }

        public List<string> GetPendingActions(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return new List<string>();

                var actions = new List<string>();

                // Check for missing documents
                foreach (var permit in project.Permits)
                {
                    var missingDocs = permit.Documents.Where(d => d.IsRequired && !d.IsSubmitted);
                    foreach (var doc in missingDocs)
                    {
                        actions.Add($"Submit {doc.Name} for {permit.Type} permit");
                    }
                }

                // Check for unresolved comments
                foreach (var cycle in project.Reviews.Where(r => r.Status == "In Review"))
                {
                    int unresolved = cycle.TotalComments - cycle.ResolvedComments;
                    if (unresolved > 0)
                    {
                        actions.Add($"Resolve {unresolved} review comments for review cycle {cycle.CycleNumber}");
                    }
                }

                // Check for unsatisfied conditions
                foreach (var permit in project.Permits)
                {
                    var pendingConditions = permit.Conditions.Where(c => !c.IsSatisfied);
                    foreach (var condition in pendingConditions)
                    {
                        actions.Add($"Satisfy condition: {condition.Description}");
                    }
                }

                // Check for scheduled inspections
                foreach (var inspection in project.Inspections.Where(i => i.Status == InspectionStatus.Scheduled))
                {
                    actions.Add($"Prepare for {inspection.Type} inspection on {inspection.ScheduledDate:d}");
                }

                return actions;
            }
        }
    }
}
