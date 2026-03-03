// ===================================================================
// StingBIM Regulatory Intelligence Engine
// Permit tracking, code compliance, and approval workflows
// Copyright (c) 2026 StingBIM. All rights reserved.
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.RegulatoryIntelligence
{
    /// <summary>
    /// Comprehensive regulatory intelligence for permit management,
    /// code compliance tracking, and approval workflow management
    /// </summary>
    public sealed class RegulatoryIntelligenceEngine
    {
        private static readonly Lazy<RegulatoryIntelligenceEngine> _instance =
            new Lazy<RegulatoryIntelligenceEngine>(() => new RegulatoryIntelligenceEngine());
        public static RegulatoryIntelligenceEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, RegulatoryProject> _projects;
        private readonly ConcurrentDictionary<string, Permit> _permits;
        private readonly ConcurrentDictionary<string, Jurisdiction> _jurisdictions;
        private readonly ConcurrentDictionary<string, CodeRequirement> _codeRequirements;
        private readonly object _lockObject = new object();

        public event EventHandler<RegulatoryAlertEventArgs> RegulatoryAlertRaised;
        public event EventHandler<PermitEventArgs> PermitStatusChanged;

        private RegulatoryIntelligenceEngine()
        {
            _projects = new ConcurrentDictionary<string, RegulatoryProject>();
            _permits = new ConcurrentDictionary<string, Permit>();
            _jurisdictions = new ConcurrentDictionary<string, Jurisdiction>();
            _codeRequirements = new ConcurrentDictionary<string, CodeRequirement>();

            InitializeJurisdictions();
            InitializeCodeRequirements();
        }

        #region Initialization

        private void InitializeJurisdictions()
        {
            var jurisdictions = new List<Jurisdiction>
            {
                new Jurisdiction
                {
                    Id = "NYC",
                    Name = "New York City",
                    State = "New York",
                    Country = "USA",
                    BuildingCode = "NYC Building Code 2022",
                    ZoningCode = "NYC Zoning Resolution",
                    FireCode = "NYC Fire Code 2022",
                    EnergyCode = "NYC Energy Conservation Code",
                    PermitTypes = new List<PermitType>
                    {
                        new PermitType { Code = "NB", Name = "New Building", TypicalDuration = 90, Fee = 0.026m },
                        new PermitType { Code = "ALT1", Name = "Alteration Type 1", TypicalDuration = 60, Fee = 0.026m },
                        new PermitType { Code = "ALT2", Name = "Alteration Type 2", TypicalDuration = 45, Fee = 0.013m },
                        new PermitType { Code = "ALT3", Name = "Alteration Type 3", TypicalDuration = 30, Fee = 0.0065m },
                        new PermitType { Code = "DM", Name = "Demolition", TypicalDuration = 30, Fee = 500 }
                    },
                    RequiredApprovals = new List<string> { "DOB", "FDNY", "DEP", "DOT", "LPC" },
                    SpecialRequirements = new List<string> { "FDNY review for high-rise", "Landmarks approval if applicable" }
                },
                new Jurisdiction
                {
                    Id = "LA",
                    Name = "Los Angeles",
                    State = "California",
                    Country = "USA",
                    BuildingCode = "LA Building Code (CBC 2022)",
                    ZoningCode = "LA Municipal Code",
                    FireCode = "LA Fire Code",
                    EnergyCode = "Title 24",
                    PermitTypes = new List<PermitType>
                    {
                        new PermitType { Code = "BLDG", Name = "Building Permit", TypicalDuration = 60, Fee = 0.02m },
                        new PermitType { Code = "ELEC", Name = "Electrical Permit", TypicalDuration = 30, Fee = 0.01m },
                        new PermitType { Code = "PLMB", Name = "Plumbing Permit", TypicalDuration = 30, Fee = 0.01m },
                        new PermitType { Code = "MECH", Name = "Mechanical Permit", TypicalDuration = 30, Fee = 0.01m },
                        new PermitType { Code = "FIRE", Name = "Fire Permit", TypicalDuration = 45, Fee = 0.015m }
                    },
                    RequiredApprovals = new List<string> { "LADBS", "LAFD", "Planning", "Public Works" }
                },
                new Jurisdiction
                {
                    Id = "CHICAGO",
                    Name = "Chicago",
                    State = "Illinois",
                    Country = "USA",
                    BuildingCode = "Chicago Building Code",
                    ZoningCode = "Chicago Zoning Ordinance",
                    FireCode = "Chicago Fire Prevention Code",
                    EnergyCode = "Chicago Energy Code",
                    PermitTypes = new List<PermitType>
                    {
                        new PermitType { Code = "STD", Name = "Standard Permit", TypicalDuration = 45, Fee = 0.018m },
                        new PermitType { Code = "EASY", Name = "Easy Permit", TypicalDuration = 14, Fee = 0.009m },
                        new PermitType { Code = "ELEC", Name = "Electrical Permit", TypicalDuration = 21, Fee = 0.008m }
                    },
                    RequiredApprovals = new List<string> { "DOB", "Fire Department", "Zoning" }
                },
                new Jurisdiction
                {
                    Id = "LONDON",
                    Name = "London",
                    State = "England",
                    Country = "UK",
                    BuildingCode = "Building Regulations 2010",
                    ZoningCode = "Town and Country Planning Act",
                    FireCode = "Regulatory Reform (Fire Safety) Order",
                    EnergyCode = "Part L Building Regulations",
                    PermitTypes = new List<PermitType>
                    {
                        new PermitType { Code = "PP", Name = "Planning Permission", TypicalDuration = 56, Fee = 462 },
                        new PermitType { Code = "BC", Name = "Building Control", TypicalDuration = 35, Fee = 0.015m },
                        new PermitType { Code = "PD", Name = "Permitted Development", TypicalDuration = 28, Fee = 0 }
                    },
                    RequiredApprovals = new List<string> { "Local Planning Authority", "Building Control", "Fire Authority" }
                },
                new Jurisdiction
                {
                    Id = "KAMPALA",
                    Name = "Kampala",
                    State = "Central",
                    Country = "Uganda",
                    BuildingCode = "Uganda National Building Code",
                    ZoningCode = "Kampala Physical Planning Act",
                    FireCode = "Fire Prevention Act",
                    EnergyCode = "Uganda Energy Code",
                    PermitTypes = new List<PermitType>
                    {
                        new PermitType { Code = "DEV", Name = "Development Permit", TypicalDuration = 30, Fee = 0.01m },
                        new PermitType { Code = "BLDG", Name = "Building Permit", TypicalDuration = 45, Fee = 0.015m },
                        new PermitType { Code = "OCC", Name = "Occupancy Certificate", TypicalDuration = 21, Fee = 0.005m }
                    },
                    RequiredApprovals = new List<string> { "KCCA", "NEMA", "Fire Brigade" }
                },
                new Jurisdiction
                {
                    Id = "NAIROBI",
                    Name = "Nairobi",
                    State = "Nairobi County",
                    Country = "Kenya",
                    BuildingCode = "Kenya Building Code",
                    ZoningCode = "Physical Planning Act",
                    FireCode = "Fire Risk Reduction Rules",
                    EnergyCode = "Kenya Energy Efficiency Standards",
                    PermitTypes = new List<PermitType>
                    {
                        new PermitType { Code = "PLAN", Name = "Planning Approval", TypicalDuration = 60, Fee = 0.01m },
                        new PermitType { Code = "BLDG", Name = "Building Permit", TypicalDuration = 30, Fee = 0.015m },
                        new PermitType { Code = "NEMA", Name = "Environmental Approval", TypicalDuration = 90, Fee = 0.005m }
                    },
                    RequiredApprovals = new List<string> { "NCC", "NEMA", "Fire Department", "County Government" }
                }
            };

            foreach (var jurisdiction in jurisdictions)
            {
                _jurisdictions.TryAdd(jurisdiction.Id, jurisdiction);
            }
        }

        private void InitializeCodeRequirements()
        {
            var requirements = new List<CodeRequirement>
            {
                // Accessibility
                new CodeRequirement { Id = "ADA-01", Code = "ADA", Category = "Accessibility", Requirement = "Accessible route from site arrival to building entrance", Applicability = "All public buildings" },
                new CodeRequirement { Id = "ADA-02", Code = "ADA", Category = "Accessibility", Requirement = "Accessible toilet facilities on each floor", Applicability = "All public buildings" },
                new CodeRequirement { Id = "ADA-03", Code = "ADA", Category = "Accessibility", Requirement = "Elevator access to all floors", Applicability = "Buildings > 3 stories" },

                // Fire Safety
                new CodeRequirement { Id = "NFPA-01", Code = "NFPA 13", Category = "Fire Protection", Requirement = "Automatic sprinkler system throughout", Applicability = "Buildings > 5,000 SF" },
                new CodeRequirement { Id = "NFPA-02", Code = "NFPA 72", Category = "Fire Protection", Requirement = "Fire alarm and detection system", Applicability = "All commercial buildings" },
                new CodeRequirement { Id = "NFPA-03", Code = "NFPA 101", Category = "Fire Protection", Requirement = "Two means of egress from each floor", Applicability = "Occupant load > 50" },

                // Energy
                new CodeRequirement { Id = "ASHRAE-01", Code = "ASHRAE 90.1", Category = "Energy", Requirement = "Building envelope thermal performance", Applicability = "All new construction" },
                new CodeRequirement { Id = "ASHRAE-02", Code = "ASHRAE 90.1", Category = "Energy", Requirement = "HVAC system efficiency requirements", Applicability = "All mechanically conditioned buildings" },
                new CodeRequirement { Id = "ASHRAE-03", Code = "ASHRAE 90.1", Category = "Energy", Requirement = "Lighting power density limits", Applicability = "All buildings" },

                // Structural
                new CodeRequirement { Id = "IBC-01", Code = "IBC", Category = "Structural", Requirement = "Seismic design per ASCE 7", Applicability = "All structures" },
                new CodeRequirement { Id = "IBC-02", Code = "IBC", Category = "Structural", Requirement = "Wind load design per ASCE 7", Applicability = "All structures" },
                new CodeRequirement { Id = "IBC-03", Code = "IBC", Category = "Structural", Requirement = "Special inspections for structural systems", Applicability = "Per Chapter 17" },

                // Plumbing
                new CodeRequirement { Id = "IPC-01", Code = "IPC", Category = "Plumbing", Requirement = "Minimum fixture count per occupancy", Applicability = "All occupied buildings" },
                new CodeRequirement { Id = "IPC-02", Code = "IPC", Category = "Plumbing", Requirement = "Backflow prevention on potable water", Applicability = "All buildings" },

                // Electrical
                new CodeRequirement { Id = "NEC-01", Code = "NEC", Category = "Electrical", Requirement = "GFCI protection in wet locations", Applicability = "All buildings" },
                new CodeRequirement { Id = "NEC-02", Code = "NEC", Category = "Electrical", Requirement = "Emergency lighting and exit signs", Applicability = "All commercial buildings" },
                new CodeRequirement { Id = "NEC-03", Code = "NEC", Category = "Electrical", Requirement = "Arc-fault protection in dwelling units", Applicability = "Residential" }
            };

            foreach (var req in requirements)
            {
                _codeRequirements.TryAdd(req.Id, req);
            }
        }

        #endregion

        #region Project Management

        public RegulatoryProject CreateProject(RegulatoryProjectRequest request)
        {
            var project = new RegulatoryProject
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Address = request.Address,
                JurisdictionId = request.JurisdictionId,
                ProjectType = request.ProjectType,
                OccupancyType = request.OccupancyType,
                GrossArea = request.GrossArea,
                Stories = request.Stories,
                ConstructionType = request.ConstructionType,
                CreatedDate = DateTime.UtcNow,
                Status = ProjectStatus.Planning,
                Permits = new List<string>(),
                Inspections = new List<string>(),
                ComplianceItems = new List<string>()
            };

            // Determine required permits
            project.RequiredPermits = DetermineRequiredPermits(project);

            _projects.TryAdd(project.Id, project);
            return project;
        }

        private List<RequiredPermit> DetermineRequiredPermits(RegulatoryProject project)
        {
            var permits = new List<RequiredPermit>();

            if (_jurisdictions.TryGetValue(project.JurisdictionId, out var jurisdiction))
            {
                // Building permit always required
                var buildingPermit = jurisdiction.PermitTypes.FirstOrDefault(p =>
                    p.Name.Contains("Building") || p.Code == "NB" || p.Code == "BLDG");

                if (buildingPermit != null)
                {
                    permits.Add(new RequiredPermit
                    {
                        PermitType = buildingPermit.Code,
                        PermitName = buildingPermit.Name,
                        Required = true,
                        EstimatedDuration = buildingPermit.TypicalDuration,
                        EstimatedFee = CalculatePermitFee(buildingPermit, project.GrossArea)
                    });
                }

                // Trade permits
                var tradePermits = new[] { "ELEC", "PLMB", "MECH", "FIRE" };
                foreach (var tradeCode in tradePermits)
                {
                    var tradePermit = jurisdiction.PermitTypes.FirstOrDefault(p => p.Code == tradeCode);
                    if (tradePermit != null)
                    {
                        permits.Add(new RequiredPermit
                        {
                            PermitType = tradePermit.Code,
                            PermitName = tradePermit.Name,
                            Required = true,
                            EstimatedDuration = tradePermit.TypicalDuration,
                            EstimatedFee = CalculatePermitFee(tradePermit, project.GrossArea)
                        });
                    }
                }

                // Environmental review if large project
                if (project.GrossArea > 50000)
                {
                    permits.Add(new RequiredPermit
                    {
                        PermitType = "ENV",
                        PermitName = "Environmental Review",
                        Required = true,
                        EstimatedDuration = 120,
                        EstimatedFee = 5000
                    });
                }
            }

            return permits;
        }

        private decimal CalculatePermitFee(PermitType permitType, decimal grossArea)
        {
            if (permitType.Fee < 100) // Percentage-based
            {
                return grossArea * permitType.Fee * 200; // Assume $200/SF construction cost
            }
            return permitType.Fee;
        }

        #endregion

        #region Permit Management

        public Permit CreatePermit(PermitRequest request)
        {
            var permit = new Permit
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                PermitNumber = GeneratePermitNumber(request),
                PermitType = request.PermitType,
                PermitName = request.PermitName,
                JurisdictionId = request.JurisdictionId,
                Status = PermitStatus.Draft,
                CreatedDate = DateTime.UtcNow,
                Submittals = new List<Submittal>(),
                ReviewCycles = new List<ReviewCycle>(),
                Conditions = new List<PermitCondition>(),
                Fees = new List<PermitFee>(),
                Timeline = new List<PermitMilestone>()
            };

            // Add standard milestones
            permit.Timeline.AddRange(new List<PermitMilestone>
            {
                new PermitMilestone { Name = "Application Submitted", PlannedDate = DateTime.UtcNow.AddDays(7) },
                new PermitMilestone { Name = "Completeness Review", PlannedDate = DateTime.UtcNow.AddDays(14) },
                new PermitMilestone { Name = "Plan Review Complete", PlannedDate = DateTime.UtcNow.AddDays(45) },
                new PermitMilestone { Name = "Permit Issued", PlannedDate = DateTime.UtcNow.AddDays(60) }
            });

            _permits.TryAdd(permit.Id, permit);

            if (_projects.TryGetValue(request.ProjectId, out var project))
            {
                project.Permits.Add(permit.Id);
            }

            return permit;
        }

        private string GeneratePermitNumber(PermitRequest request)
        {
            return $"{request.JurisdictionId}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
        }

        public Permit SubmitPermit(string permitId, SubmittalRequest request)
        {
            if (!_permits.TryGetValue(permitId, out var permit))
                return null;

            var submittal = new Submittal
            {
                Id = Guid.NewGuid().ToString(),
                SubmittalNumber = permit.Submittals.Count + 1,
                SubmittedDate = DateTime.UtcNow,
                SubmittedBy = request.SubmittedBy,
                Documents = request.Documents ?? new List<SubmittalDocument>(),
                Status = SubmittalStatus.Submitted
            };

            lock (_lockObject)
            {
                permit.Submittals.Add(submittal);
                permit.Status = PermitStatus.Submitted;
                permit.SubmittedDate = DateTime.UtcNow;

                // Update milestone
                var submitMilestone = permit.Timeline.FirstOrDefault(m => m.Name == "Application Submitted");
                if (submitMilestone != null)
                {
                    submitMilestone.ActualDate = DateTime.UtcNow;
                    submitMilestone.Complete = true;
                }
            }

            PermitStatusChanged?.Invoke(this, new PermitEventArgs { Permit = permit, NewStatus = permit.Status });

            return permit;
        }

        public ReviewCycle StartReview(string permitId, ReviewStartRequest request)
        {
            if (!_permits.TryGetValue(permitId, out var permit))
                return null;

            var review = new ReviewCycle
            {
                Id = Guid.NewGuid().ToString(),
                CycleNumber = permit.ReviewCycles.Count + 1,
                StartDate = DateTime.UtcNow,
                Reviewer = request.Reviewer,
                ReviewType = request.ReviewType,
                Status = ReviewStatus.InProgress,
                Comments = new List<ReviewComment>()
            };

            lock (_lockObject)
            {
                permit.ReviewCycles.Add(review);
                permit.Status = PermitStatus.UnderReview;
            }

            return review;
        }

        public ReviewCycle CompleteReview(string permitId, string reviewId, ReviewCompleteRequest request)
        {
            if (!_permits.TryGetValue(permitId, out var permit))
                return null;

            var review = permit.ReviewCycles.FirstOrDefault(r => r.Id == reviewId);
            if (review == null) return null;

            review.EndDate = DateTime.UtcNow;
            review.Result = request.Result;
            review.Comments = request.Comments ?? new List<ReviewComment>();
            review.Status = request.Result == ReviewResult.Approved ? ReviewStatus.Complete : ReviewStatus.RevisionRequired;

            // Update permit status
            if (request.Result == ReviewResult.Approved)
            {
                var allApproved = permit.ReviewCycles.All(r => r.Result == ReviewResult.Approved);
                permit.Status = allApproved ? PermitStatus.Approved : PermitStatus.UnderReview;
            }
            else
            {
                permit.Status = PermitStatus.RevisionRequired;

                RegulatoryAlertRaised?.Invoke(this, new RegulatoryAlertEventArgs
                {
                    ProjectId = permit.ProjectId,
                    PermitId = permitId,
                    AlertType = "Review Comments",
                    Message = $"Permit {permit.PermitNumber} requires revisions - {request.Comments?.Count ?? 0} comments"
                });
            }

            return review;
        }

        public Permit IssuePermit(string permitId, PermitIssuanceRequest request)
        {
            if (!_permits.TryGetValue(permitId, out var permit))
                return null;

            permit.Status = PermitStatus.Issued;
            permit.IssuedDate = DateTime.UtcNow;
            permit.ExpirationDate = DateTime.UtcNow.AddYears(1);
            permit.IssuedBy = request.IssuedBy;

            // Add conditions
            if (request.Conditions != null)
            {
                permit.Conditions.AddRange(request.Conditions);
            }

            // Update milestone
            var issueMilestone = permit.Timeline.FirstOrDefault(m => m.Name == "Permit Issued");
            if (issueMilestone != null)
            {
                issueMilestone.ActualDate = DateTime.UtcNow;
                issueMilestone.Complete = true;
            }

            PermitStatusChanged?.Invoke(this, new PermitEventArgs { Permit = permit, NewStatus = permit.Status });

            return permit;
        }

        #endregion

        #region Inspection Management

        public InspectionRequest CreateInspectionRequest(InspectionCreateRequest request)
        {
            var inspection = new InspectionRequest
            {
                Id = Guid.NewGuid().ToString(),
                PermitId = request.PermitId,
                ProjectId = request.ProjectId,
                InspectionType = request.InspectionType,
                RequestedDate = request.RequestedDate,
                RequestedBy = request.RequestedBy,
                Location = request.Location,
                Notes = request.Notes,
                Status = InspectionStatus.Requested,
                CreatedDate = DateTime.UtcNow
            };

            if (_projects.TryGetValue(request.ProjectId, out var project))
            {
                project.Inspections.Add(inspection.Id);
            }

            return inspection;
        }

        public InspectionRequest ScheduleInspection(string inspectionId, InspectionScheduleRequest request)
        {
            var inspection = new InspectionRequest
            {
                Id = inspectionId,
                ScheduledDate = request.ScheduledDate,
                Inspector = request.Inspector,
                Status = InspectionStatus.Scheduled
            };

            return inspection;
        }

        public InspectionResult RecordInspectionResult(string inspectionId, InspectionResultRequest request)
        {
            var result = new InspectionResult
            {
                Id = Guid.NewGuid().ToString(),
                InspectionId = inspectionId,
                InspectionDate = DateTime.UtcNow,
                Inspector = request.Inspector,
                Result = request.Result,
                Findings = request.Findings ?? new List<InspectionFinding>(),
                Photos = request.Photos ?? new List<string>(),
                Notes = request.Notes,
                NextInspectionRequired = request.NextInspectionRequired
            };

            if (request.Result == InspResult.Failed)
            {
                RegulatoryAlertRaised?.Invoke(this, new RegulatoryAlertEventArgs
                {
                    AlertType = "Failed Inspection",
                    Message = $"Inspection failed with {request.Findings?.Count ?? 0} findings"
                });
            }

            return result;
        }

        #endregion

        #region Compliance Tracking

        public ComplianceAssessment AssessCompliance(string projectId)
        {
            if (!_projects.TryGetValue(projectId, out var project))
                return null;

            var assessment = new ComplianceAssessment
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                AssessmentDate = DateTime.UtcNow,
                Requirements = new List<ComplianceItem>(),
                OverallStatus = ComplianceStatus.Compliant
            };

            // Check applicable code requirements
            foreach (var req in _codeRequirements.Values)
            {
                if (IsRequirementApplicable(req, project))
                {
                    var item = new ComplianceItem
                    {
                        RequirementId = req.Id,
                        Code = req.Code,
                        Category = req.Category,
                        Requirement = req.Requirement,
                        Status = ComplianceStatus.NotAssessed,
                        Evidence = new List<string>()
                    };

                    assessment.Requirements.Add(item);
                }
            }

            // Calculate statistics
            assessment.TotalRequirements = assessment.Requirements.Count;
            assessment.CompliantCount = assessment.Requirements.Count(r => r.Status == ComplianceStatus.Compliant);
            assessment.NonCompliantCount = assessment.Requirements.Count(r => r.Status == ComplianceStatus.NonCompliant);
            assessment.PendingCount = assessment.Requirements.Count(r => r.Status == ComplianceStatus.NotAssessed || r.Status == ComplianceStatus.InProgress);
            assessment.CompliancePercent = assessment.TotalRequirements > 0
                ? (decimal)assessment.CompliantCount / assessment.TotalRequirements * 100
                : 0;

            if (assessment.NonCompliantCount > 0)
                assessment.OverallStatus = ComplianceStatus.NonCompliant;
            else if (assessment.PendingCount > 0)
                assessment.OverallStatus = ComplianceStatus.InProgress;

            return assessment;
        }

        private bool IsRequirementApplicable(CodeRequirement requirement, RegulatoryProject project)
        {
            if (requirement.Applicability == "All" || requirement.Applicability == "All buildings")
                return true;

            if (requirement.Applicability.Contains("commercial") && project.OccupancyType?.Contains("Business") == true)
                return true;

            if (requirement.Applicability.Contains("5,000 SF") && project.GrossArea > 5000)
                return true;

            if (requirement.Applicability.Contains("3 stories") && project.Stories > 3)
                return true;

            return false;
        }

        public ComplianceItem UpdateComplianceItem(string projectId, string requirementId, ComplianceUpdateRequest request)
        {
            var item = new ComplianceItem
            {
                RequirementId = requirementId,
                Status = request.Status,
                AssessedDate = DateTime.UtcNow,
                AssessedBy = request.AssessedBy,
                Notes = request.Notes,
                Evidence = request.Evidence ?? new List<string>(),
                CorrectiveAction = request.CorrectiveAction,
                DueDate = request.DueDate
            };

            return item;
        }

        #endregion

        #region Code Research

        public CodeResearchResult ResearchCode(CodeResearchRequest request)
        {
            var result = new CodeResearchResult
            {
                Id = Guid.NewGuid().ToString(),
                Query = request.Query,
                JurisdictionId = request.JurisdictionId,
                SearchDate = DateTime.UtcNow,
                Results = new List<CodeReference>()
            };

            // Search code requirements
            var queryLower = request.Query.ToLower();
            var matches = _codeRequirements.Values
                .Where(c => c.Requirement.ToLower().Contains(queryLower) ||
                           c.Category.ToLower().Contains(queryLower) ||
                           c.Code.ToLower().Contains(queryLower))
                .Select(c => new CodeReference
                {
                    Code = c.Code,
                    Section = c.Id,
                    Category = c.Category,
                    Text = c.Requirement,
                    Applicability = c.Applicability
                })
                .ToList();

            result.Results = matches;

            // Add jurisdiction-specific information
            if (_jurisdictions.TryGetValue(request.JurisdictionId, out var jurisdiction))
            {
                result.ApplicableCodes = new List<string>
                {
                    jurisdiction.BuildingCode,
                    jurisdiction.ZoningCode,
                    jurisdiction.FireCode,
                    jurisdiction.EnergyCode
                };
            }

            return result;
        }

        #endregion

        #region Regulatory Dashboard

        public RegulatoryDashboard GetProjectDashboard(string projectId)
        {
            if (!_projects.TryGetValue(projectId, out var project))
                return null;

            var permits = project.Permits
                .Select(id => _permits.TryGetValue(id, out var p) ? p : null)
                .Where(p => p != null)
                .ToList();

            var dashboard = new RegulatoryDashboard
            {
                ProjectId = projectId,
                GeneratedDate = DateTime.UtcNow,
                TotalPermits = permits.Count,
                PermitsIssued = permits.Count(p => p.Status == PermitStatus.Issued),
                PermitsPending = permits.Count(p => p.Status != PermitStatus.Issued && p.Status != PermitStatus.Closed),
                PermitsExpiring = permits.Count(p => p.ExpirationDate.HasValue && p.ExpirationDate.Value < DateTime.UtcNow.AddDays(30)),
                TotalFees = permits.Sum(p => p.Fees.Sum(f => f.Amount)),
                FeesPaid = permits.Sum(p => p.Fees.Where(f => f.Paid).Sum(f => f.Amount)),
                UpcomingInspections = new List<UpcomingInspection>(),
                PermitStatus = new List<PermitStatusSummary>(),
                ActionItems = new List<RegulatoryActionItem>()
            };

            // Permit status breakdown
            foreach (var permit in permits)
            {
                dashboard.PermitStatus.Add(new PermitStatusSummary
                {
                    PermitId = permit.Id,
                    PermitNumber = permit.PermitNumber,
                    PermitType = permit.PermitType,
                    Status = permit.Status,
                    SubmittedDate = permit.SubmittedDate,
                    IssuedDate = permit.IssuedDate,
                    ExpirationDate = permit.ExpirationDate,
                    DaysInReview = permit.SubmittedDate.HasValue && permit.Status == PermitStatus.UnderReview
                        ? (int)(DateTime.UtcNow - permit.SubmittedDate.Value).TotalDays
                        : 0
                });
            }

            // Generate action items
            foreach (var permit in permits)
            {
                if (permit.Status == PermitStatus.RevisionRequired)
                {
                    dashboard.ActionItems.Add(new RegulatoryActionItem
                    {
                        Type = "Revision Required",
                        Description = $"Permit {permit.PermitNumber} requires revisions",
                        Priority = "High",
                        DueDate = DateTime.UtcNow.AddDays(7)
                    });
                }

                if (permit.ExpirationDate.HasValue && permit.ExpirationDate.Value < DateTime.UtcNow.AddDays(30))
                {
                    dashboard.ActionItems.Add(new RegulatoryActionItem
                    {
                        Type = "Permit Expiring",
                        Description = $"Permit {permit.PermitNumber} expires {permit.ExpirationDate:d}",
                        Priority = permit.ExpirationDate.Value < DateTime.UtcNow.AddDays(7) ? "High" : "Medium",
                        DueDate = permit.ExpirationDate.Value
                    });
                }

                var unpaidFees = permit.Fees.Where(f => !f.Paid).Sum(f => f.Amount);
                if (unpaidFees > 0)
                {
                    dashboard.ActionItems.Add(new RegulatoryActionItem
                    {
                        Type = "Fees Due",
                        Description = $"Permit {permit.PermitNumber} has ${unpaidFees:N0} in unpaid fees",
                        Priority = "Medium"
                    });
                }
            }

            return dashboard;
        }

        #endregion

        #region Helper Methods

        public Jurisdiction GetJurisdiction(string jurisdictionId)
        {
            _jurisdictions.TryGetValue(jurisdictionId, out var jurisdiction);
            return jurisdiction;
        }

        public List<Jurisdiction> GetAllJurisdictions()
        {
            return _jurisdictions.Values.ToList();
        }

        public Permit GetPermit(string permitId)
        {
            _permits.TryGetValue(permitId, out var permit);
            return permit;
        }

        public RegulatoryProject GetProject(string projectId)
        {
            _projects.TryGetValue(projectId, out var project);
            return project;
        }

        public List<CodeRequirement> GetCodeRequirements(string category = null)
        {
            var query = _codeRequirements.Values.AsQueryable();

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(c => c.Category == category);
            }

            return query.ToList();
        }

        #endregion
    }

    #region Data Models

    public class RegulatoryProject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string JurisdictionId { get; set; }
        public string ProjectType { get; set; }
        public string OccupancyType { get; set; }
        public decimal GrossArea { get; set; }
        public int Stories { get; set; }
        public string ConstructionType { get; set; }
        public DateTime CreatedDate { get; set; }
        public ProjectStatus Status { get; set; }
        public List<RequiredPermit> RequiredPermits { get; set; }
        public List<string> Permits { get; set; }
        public List<string> Inspections { get; set; }
        public List<string> ComplianceItems { get; set; }
    }

    public class RegulatoryProjectRequest
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string JurisdictionId { get; set; }
        public string ProjectType { get; set; }
        public string OccupancyType { get; set; }
        public decimal GrossArea { get; set; }
        public int Stories { get; set; }
        public string ConstructionType { get; set; }
    }

    public class RequiredPermit
    {
        public string PermitType { get; set; }
        public string PermitName { get; set; }
        public bool Required { get; set; }
        public int EstimatedDuration { get; set; }
        public decimal EstimatedFee { get; set; }
    }

    public class Jurisdiction
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string BuildingCode { get; set; }
        public string ZoningCode { get; set; }
        public string FireCode { get; set; }
        public string EnergyCode { get; set; }
        public List<PermitType> PermitTypes { get; set; }
        public List<string> RequiredApprovals { get; set; }
        public List<string> SpecialRequirements { get; set; }
    }

    public class PermitType
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public int TypicalDuration { get; set; }
        public decimal Fee { get; set; }
    }

    public class CodeRequirement
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Category { get; set; }
        public string Requirement { get; set; }
        public string Applicability { get; set; }
    }

    public class Permit
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string PermitNumber { get; set; }
        public string PermitType { get; set; }
        public string PermitName { get; set; }
        public string JurisdictionId { get; set; }
        public PermitStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public DateTime? IssuedDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string IssuedBy { get; set; }
        public List<Submittal> Submittals { get; set; }
        public List<ReviewCycle> ReviewCycles { get; set; }
        public List<PermitCondition> Conditions { get; set; }
        public List<PermitFee> Fees { get; set; }
        public List<PermitMilestone> Timeline { get; set; }
    }

    public class PermitRequest
    {
        public string ProjectId { get; set; }
        public string PermitType { get; set; }
        public string PermitName { get; set; }
        public string JurisdictionId { get; set; }
    }

    public class Submittal
    {
        public string Id { get; set; }
        public int SubmittalNumber { get; set; }
        public DateTime SubmittedDate { get; set; }
        public string SubmittedBy { get; set; }
        public List<SubmittalDocument> Documents { get; set; }
        public SubmittalStatus Status { get; set; }
    }

    public class SubmittalRequest
    {
        public string SubmittedBy { get; set; }
        public List<SubmittalDocument> Documents { get; set; }
    }

    public class SubmittalDocument
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string FilePath { get; set; }
        public DateTime UploadDate { get; set; }
    }

    public class ReviewCycle
    {
        public string Id { get; set; }
        public int CycleNumber { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Reviewer { get; set; }
        public string ReviewType { get; set; }
        public ReviewStatus Status { get; set; }
        public ReviewResult Result { get; set; }
        public List<ReviewComment> Comments { get; set; }
    }

    public class ReviewStartRequest
    {
        public string Reviewer { get; set; }
        public string ReviewType { get; set; }
    }

    public class ReviewCompleteRequest
    {
        public ReviewResult Result { get; set; }
        public List<ReviewComment> Comments { get; set; }
    }

    public class ReviewComment
    {
        public string Id { get; set; }
        public string Sheet { get; set; }
        public string Location { get; set; }
        public string Comment { get; set; }
        public CommentSeverity Severity { get; set; }
        public string Response { get; set; }
        public bool Resolved { get; set; }
    }

    public class PermitCondition
    {
        public string Id { get; set; }
        public string Condition { get; set; }
        public string Category { get; set; }
        public bool Acknowledged { get; set; }
    }

    public class PermitFee
    {
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public bool Paid { get; set; }
        public DateTime? PaidDate { get; set; }
    }

    public class PermitMilestone
    {
        public string Name { get; set; }
        public DateTime PlannedDate { get; set; }
        public DateTime? ActualDate { get; set; }
        public bool Complete { get; set; }
    }

    public class PermitIssuanceRequest
    {
        public string IssuedBy { get; set; }
        public List<PermitCondition> Conditions { get; set; }
    }

    public class InspectionRequest
    {
        public string Id { get; set; }
        public string PermitId { get; set; }
        public string ProjectId { get; set; }
        public string InspectionType { get; set; }
        public DateTime RequestedDate { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public string RequestedBy { get; set; }
        public string Inspector { get; set; }
        public string Location { get; set; }
        public string Notes { get; set; }
        public InspectionStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class InspectionCreateRequest
    {
        public string PermitId { get; set; }
        public string ProjectId { get; set; }
        public string InspectionType { get; set; }
        public DateTime RequestedDate { get; set; }
        public string RequestedBy { get; set; }
        public string Location { get; set; }
        public string Notes { get; set; }
    }

    public class InspectionScheduleRequest
    {
        public DateTime ScheduledDate { get; set; }
        public string Inspector { get; set; }
    }

    public class InspectionResult
    {
        public string Id { get; set; }
        public string InspectionId { get; set; }
        public DateTime InspectionDate { get; set; }
        public string Inspector { get; set; }
        public InspResult Result { get; set; }
        public List<InspectionFinding> Findings { get; set; }
        public List<string> Photos { get; set; }
        public string Notes { get; set; }
        public bool NextInspectionRequired { get; set; }
    }

    public class InspectionResultRequest
    {
        public string Inspector { get; set; }
        public InspResult Result { get; set; }
        public List<InspectionFinding> Findings { get; set; }
        public List<string> Photos { get; set; }
        public string Notes { get; set; }
        public bool NextInspectionRequired { get; set; }
    }

    public class InspectionFinding
    {
        public string Code { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public FindingSeverity Severity { get; set; }
        public string CorrectiveAction { get; set; }
    }

    public class ComplianceAssessment
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public DateTime AssessmentDate { get; set; }
        public List<ComplianceItem> Requirements { get; set; }
        public int TotalRequirements { get; set; }
        public int CompliantCount { get; set; }
        public int NonCompliantCount { get; set; }
        public int PendingCount { get; set; }
        public decimal CompliancePercent { get; set; }
        public ComplianceStatus OverallStatus { get; set; }
    }

    public class ComplianceItem
    {
        public string RequirementId { get; set; }
        public string Code { get; set; }
        public string Category { get; set; }
        public string Requirement { get; set; }
        public ComplianceStatus Status { get; set; }
        public DateTime? AssessedDate { get; set; }
        public string AssessedBy { get; set; }
        public string Notes { get; set; }
        public List<string> Evidence { get; set; }
        public string CorrectiveAction { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class ComplianceUpdateRequest
    {
        public ComplianceStatus Status { get; set; }
        public string AssessedBy { get; set; }
        public string Notes { get; set; }
        public List<string> Evidence { get; set; }
        public string CorrectiveAction { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class CodeResearchRequest
    {
        public string Query { get; set; }
        public string JurisdictionId { get; set; }
        public string Category { get; set; }
    }

    public class CodeResearchResult
    {
        public string Id { get; set; }
        public string Query { get; set; }
        public string JurisdictionId { get; set; }
        public DateTime SearchDate { get; set; }
        public List<CodeReference> Results { get; set; }
        public List<string> ApplicableCodes { get; set; }
    }

    public class CodeReference
    {
        public string Code { get; set; }
        public string Section { get; set; }
        public string Category { get; set; }
        public string Text { get; set; }
        public string Applicability { get; set; }
    }

    public class RegulatoryDashboard
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalPermits { get; set; }
        public int PermitsIssued { get; set; }
        public int PermitsPending { get; set; }
        public int PermitsExpiring { get; set; }
        public decimal TotalFees { get; set; }
        public decimal FeesPaid { get; set; }
        public List<UpcomingInspection> UpcomingInspections { get; set; }
        public List<PermitStatusSummary> PermitStatus { get; set; }
        public List<RegulatoryActionItem> ActionItems { get; set; }
    }

    public class PermitStatusSummary
    {
        public string PermitId { get; set; }
        public string PermitNumber { get; set; }
        public string PermitType { get; set; }
        public PermitStatus Status { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public DateTime? IssuedDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public int DaysInReview { get; set; }
    }

    public class UpcomingInspection
    {
        public string InspectionType { get; set; }
        public DateTime ScheduledDate { get; set; }
        public string Location { get; set; }
    }

    public class RegulatoryActionItem
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public string Priority { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class RegulatoryAlertEventArgs : EventArgs
    {
        public string ProjectId { get; set; }
        public string PermitId { get; set; }
        public string AlertType { get; set; }
        public string Message { get; set; }
    }

    public class PermitEventArgs : EventArgs
    {
        public Permit Permit { get; set; }
        public PermitStatus NewStatus { get; set; }
    }

    public enum ProjectStatus { Planning, Permitting, Construction, Complete, Cancelled }
    public enum PermitStatus { Draft, Submitted, UnderReview, RevisionRequired, Approved, Issued, Expired, Closed }
    public enum SubmittalStatus { Submitted, UnderReview, Approved, RevisionRequired }
    public enum ReviewStatus { InProgress, Complete, RevisionRequired }
    public enum ReviewResult { Approved, ApprovedWithComments, RevisionRequired, Denied }
    public enum CommentSeverity { Minor, Major, Critical }
    public enum InspectionStatus { Requested, Scheduled, Completed, Cancelled }
    public enum InspResult { Passed, PassedWithConditions, Failed, Cancelled }
    public enum FindingSeverity { Minor, Major, Critical }
    public enum ComplianceStatus { NotAssessed, Compliant, NonCompliant, InProgress, NotApplicable }

    #endregion
}
