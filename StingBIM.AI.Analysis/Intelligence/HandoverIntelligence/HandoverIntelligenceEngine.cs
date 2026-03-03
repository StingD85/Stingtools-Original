// HandoverIntelligenceEngine.cs
// StingBIM v7 - Handover Intelligence (Comprehensive)
// O&M manual generation, training plans, as-built verification, defect liability, closeout

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.HandoverIntelligence
{
    #region Enums

    public enum HandoverPhase { PreCommissioning, Commissioning, Training, Documentation, FinalInspection, Closeout, Complete }
    public enum CommissioningStatus { NotStarted, Scheduled, InProgress, Failed, Passed, Conditional, Deferred }
    public enum DocumentStatus { Required, InProgress, Submitted, UnderReview, Approved, Rejected, Superseded }
    public enum DeficiencyPriority { Critical, Major, Minor, Cosmetic, Observation }
    public enum DeficiencyStatus { Open, Assigned, InProgress, PendingVerification, Resolved, Deferred, Disputed }
    public enum TrainingType { ClassroomTheory, HandsOn, Online, Manufacturer, OnTheJob, Emergency, Specialized }
    public enum AsBuiltVerification { NotChecked, Pending, Verified, DiscrepancyFound, Corrected, Waived }
    public enum WarrantyType { Standard, Extended, SpecialEquipment, Workmanship, Material, Performance }
    public enum CloseoutStatus { NotStarted, InProgress, PendingApproval, Complete, OnHold }

    #endregion

    #region Data Models

    public class HandoverProject
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime PlannedHandoverDate { get; set; }
        public DateTime? ActualHandoverDate { get; set; }
        public HandoverPhase CurrentPhase { get; set; } = HandoverPhase.PreCommissioning;
        public List<CommissioningItem> CommissioningItems { get; set; } = new();
        public List<HandoverDocument> Documents { get; set; } = new();
        public List<Deficiency> Deficiencies { get; set; } = new();
        public List<TrainingSession> TrainingSessions { get; set; } = new();
        public List<Warranty> Warranties { get; set; } = new();
        public List<AsBuiltItem> AsBuiltItems { get; set; } = new();
        public CloseoutPackage CloseoutPackage { get; set; }
        public HandoverMetrics Metrics { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CommissioningItem
    {
        public string ItemId { get; set; } = Guid.NewGuid().ToString();
        public string SystemName { get; set; }
        public string SubSystem { get; set; }
        public string Description { get; set; }
        public CommissioningStatus Status { get; set; } = CommissioningStatus.NotStarted;
        public List<CommissioningTest> Tests { get; set; } = new();
        public string ResponsibleParty { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string Location { get; set; }
        public string Notes { get; set; }
        public List<string> Deficiencies { get; set; } = new();
    }

    public class CommissioningTest
    {
        public string TestId { get; set; } = Guid.NewGuid().ToString();
        public string TestName { get; set; }
        public string Procedure { get; set; }
        public string ExpectedResult { get; set; }
        public string ActualResult { get; set; }
        public bool Passed { get; set; }
        public DateTime? TestDate { get; set; }
        public string TestedBy { get; set; }
        public string WitnessedBy { get; set; }
        public List<string> Attachments { get; set; } = new();
    }

    public class HandoverDocument
    {
        public string DocumentId { get; set; } = Guid.NewGuid().ToString();
        public string DocumentType { get; set; }
        public string DocumentName { get; set; }
        public string Description { get; set; }
        public DocumentStatus Status { get; set; } = DocumentStatus.Required;
        public string System { get; set; }
        public string ResponsibleParty { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string FilePath { get; set; }
        public string Version { get; set; } = "1.0";
        public string ReviewedBy { get; set; }
        public string ReviewComments { get; set; }
        public int RevisionCount { get; set; }
    }

    public class Deficiency
    {
        public string DeficiencyId { get; set; } = Guid.NewGuid().ToString();
        public string DeficiencyNumber { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string System { get; set; }
        public DeficiencyPriority Priority { get; set; }
        public DeficiencyStatus Status { get; set; } = DeficiencyStatus.Open;
        public string IdentifiedBy { get; set; }
        public DateTime IdentifiedDate { get; set; } = DateTime.UtcNow;
        public string AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public DateTime? VerifiedDate { get; set; }
        public string VerifiedBy { get; set; }
        public string ResolutionNotes { get; set; }
        public List<string> Photos { get; set; } = new();
        public bool IsWarrantyItem { get; set; }
        public decimal EstimatedCost { get; set; }
        public int DaysOpen { get; set; }
    }

    public class TrainingSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string SessionName { get; set; }
        public TrainingType Type { get; set; }
        public string System { get; set; }
        public string Description { get; set; }
        public DateTime ScheduledDate { get; set; }
        public int DurationHours { get; set; }
        public string Location { get; set; }
        public string Instructor { get; set; }
        public string InstructorCompany { get; set; }
        public List<Attendee> Attendees { get; set; } = new();
        public List<string> Topics { get; set; } = new();
        public List<string> Materials { get; set; } = new();
        public bool IsComplete { get; set; }
        public string VideoRecordingPath { get; set; }
        public List<string> HandoutPaths { get; set; } = new();
        public double AttendanceRate { get; set; }
        public double AverageScore { get; set; }
    }

    public class Attendee
    {
        public string AttendeeId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Role { get; set; }
        public string Department { get; set; }
        public string Email { get; set; }
        public bool Attended { get; set; }
        public double? QuizScore { get; set; }
        public bool CertificateIssued { get; set; }
    }

    public class Warranty
    {
        public string WarrantyId { get; set; } = Guid.NewGuid().ToString();
        public string System { get; set; }
        public string Equipment { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public WarrantyType Type { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DurationMonths { get; set; }
        public string Coverage { get; set; }
        public string Exclusions { get; set; }
        public string ContactName { get; set; }
        public string ContactPhone { get; set; }
        public string ContactEmail { get; set; }
        public string WarrantyDocumentPath { get; set; }
        public List<WarrantyClaim> Claims { get; set; } = new();
        public bool IsActive { get; set; }
        public int DaysRemaining { get; set; }
    }

    public class WarrantyClaim
    {
        public string ClaimId { get; set; } = Guid.NewGuid().ToString();
        public string Description { get; set; }
        public DateTime ClaimDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Open";
        public DateTime? ResolvedDate { get; set; }
        public decimal ClaimAmount { get; set; }
        public string Resolution { get; set; }
    }

    public class AsBuiltItem
    {
        public string ItemId { get; set; } = Guid.NewGuid().ToString();
        public string System { get; set; }
        public string Element { get; set; }
        public string Location { get; set; }
        public string DrawingReference { get; set; }
        public AsBuiltVerification VerificationStatus { get; set; } = AsBuiltVerification.NotChecked;
        public DateTime? VerifiedDate { get; set; }
        public string VerifiedBy { get; set; }
        public string DiscrepancyDescription { get; set; }
        public string CorrectionNotes { get; set; }
        public bool ModelUpdated { get; set; }
    }

    public class CloseoutPackage
    {
        public string PackageId { get; set; } = Guid.NewGuid().ToString();
        public CloseoutStatus Status { get; set; } = CloseoutStatus.NotStarted;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string SubmittedBy { get; set; }
        public string ApprovedBy { get; set; }
        public OMManual OMManual { get; set; }
        public List<string> IncludedDocuments { get; set; } = new();
        public List<string> RequiredApprovals { get; set; } = new();
        public List<CloseoutApproval> Approvals { get; set; } = new();
        public List<string> MissingItems { get; set; } = new();
        public double CompletionPercentage { get; set; }
        public FinalCertificates Certificates { get; set; }
    }

    public class OMManual
    {
        public string ManualId { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Version { get; set; } = "1.0";
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
        public List<OMSection> Sections { get; set; } = new();
        public string FilePath { get; set; }
        public int TotalPages { get; set; }
        public List<string> Systems { get; set; } = new();
        public string Format { get; set; } = "PDF";
        public bool IsApproved { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime? ApprovedDate { get; set; }
    }

    public class OMSection
    {
        public string SectionId { get; set; } = Guid.NewGuid().ToString();
        public string SectionNumber { get; set; }
        public string SectionTitle { get; set; }
        public string System { get; set; }
        public List<string> SubSections { get; set; } = new();
        public string Content { get; set; }
        public List<string> Attachments { get; set; } = new();
        public int PageNumber { get; set; }
    }

    public class CloseoutApproval
    {
        public string ApprovalId { get; set; } = Guid.NewGuid().ToString();
        public string ApproverName { get; set; }
        public string ApproverRole { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public string Status { get; set; } = "Pending";
        public string Comments { get; set; }
    }

    public class FinalCertificates
    {
        public bool OccupancyCertificate { get; set; }
        public DateTime? OccupancyDate { get; set; }
        public bool FireCertificate { get; set; }
        public DateTime? FireDate { get; set; }
        public bool ElectricalCertificate { get; set; }
        public DateTime? ElectricalDate { get; set; }
        public bool PlumbingCertificate { get; set; }
        public DateTime? PlumbingDate { get; set; }
        public bool ElevatorCertificate { get; set; }
        public DateTime? ElevatorDate { get; set; }
        public List<string> OtherCertificates { get; set; } = new();
    }

    public class TrainingPlan
    {
        public string PlanId { get; set; } = Guid.NewGuid().ToString();
        public string PlanName { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public List<TrainingSession> Sessions { get; set; } = new();
        public List<string> TargetAudience { get; set; } = new();
        public int TotalHours { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "Draft";
        public double OverallCompletionRate { get; set; }
    }

    public class HandoverMetrics
    {
        public double OverallProgress { get; set; }
        public int TotalCommissioningItems { get; set; }
        public int CompletedCommissioningItems { get; set; }
        public double CommissioningProgress { get; set; }
        public int TotalDocuments { get; set; }
        public int ApprovedDocuments { get; set; }
        public double DocumentProgress { get; set; }
        public int TotalDeficiencies { get; set; }
        public int OpenDeficiencies { get; set; }
        public int CriticalDeficiencies { get; set; }
        public double DeficiencyResolutionRate { get; set; }
        public int TotalTrainingSessions { get; set; }
        public int CompletedTrainingSessions { get; set; }
        public double TrainingProgress { get; set; }
        public int TotalWarranties { get; set; }
        public int ActiveWarranties { get; set; }
        public int AsBuiltItemsVerified { get; set; }
        public int AsBuiltDiscrepancies { get; set; }
        public double CloseoutProgress { get; set; }
        public int DaysToHandover { get; set; }
        public bool OnTrack { get; set; }
    }

    #endregion

    #region Engine

    public sealed class HandoverIntelligenceEngine
    {
        private static readonly Lazy<HandoverIntelligenceEngine> _instance =
            new(() => new HandoverIntelligenceEngine());
        public static HandoverIntelligenceEngine Instance => _instance.Value;

        private readonly object _lock = new();
        private readonly Dictionary<string, HandoverProject> _projects = new();

        private HandoverIntelligenceEngine() { }

        public OMManual GenerateOMManual(HandoverProject project, List<string> systems = null)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            systems ??= new List<string> { "HVAC", "Electrical", "Plumbing", "Fire Protection", "Building Automation", "Elevators" };

            var manual = new OMManual
            {
                Title = $"{project.ProjectName} - Operations and Maintenance Manual",
                Systems = systems,
                Sections = GenerateOMSections(project, systems)
            };

            manual.TotalPages = manual.Sections.Count * 15;

            lock (_lock)
            {
                project.CloseoutPackage ??= new CloseoutPackage();
                project.CloseoutPackage.OMManual = manual;
                _projects[project.ProjectId] = project;
            }

            return manual;
        }

        public TrainingPlan CreateTrainingPlan(HandoverProject project, List<string> systems, List<string> audience, int sessionDurationHours = 4)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            var sessions = new List<TrainingSession>();
            int dayOffset = 0;

            foreach (var system in systems ?? new List<string>())
            {
                sessions.Add(new TrainingSession
                {
                    SessionName = $"{system} - Overview and Operation",
                    Type = TrainingType.ClassroomTheory,
                    System = system,
                    Description = $"Comprehensive overview of {system} system operation",
                    ScheduledDate = project.PlannedHandoverDate.AddDays(-14 + dayOffset),
                    DurationHours = sessionDurationHours,
                    Topics = GenerateTopics(system, "Theory"),
                    Attendees = audience?.Select(a => new Attendee { Name = a, Role = "Operations Staff" }).ToList() ?? new List<Attendee>()
                });

                sessions.Add(new TrainingSession
                {
                    SessionName = $"{system} - Hands-On Training",
                    Type = TrainingType.HandsOn,
                    System = system,
                    Description = $"Practical hands-on training for {system}",
                    ScheduledDate = project.PlannedHandoverDate.AddDays(-13 + dayOffset),
                    DurationHours = sessionDurationHours,
                    Topics = GenerateTopics(system, "Practical"),
                    Attendees = audience?.Select(a => new Attendee { Name = a, Role = "Operations Staff" }).ToList() ?? new List<Attendee>()
                });

                sessions.Add(new TrainingSession
                {
                    SessionName = $"{system} - Emergency Procedures",
                    Type = TrainingType.Emergency,
                    System = system,
                    Description = $"Emergency shutdown and response procedures for {system}",
                    ScheduledDate = project.PlannedHandoverDate.AddDays(-12 + dayOffset),
                    DurationHours = 2,
                    Topics = GenerateTopics(system, "Emergency"),
                    Attendees = audience?.Select(a => new Attendee { Name = a, Role = "Operations Staff" }).ToList() ?? new List<Attendee>()
                });

                dayOffset += 3;
            }

            var plan = new TrainingPlan
            {
                PlanName = $"{project.ProjectName} - Training Plan",
                Sessions = sessions,
                TargetAudience = audience ?? new List<string>(),
                TotalHours = sessions.Sum(s => s.DurationHours),
                StartDate = sessions.Min(s => s.ScheduledDate),
                EndDate = sessions.Max(s => s.ScheduledDate),
                Status = "Approved"
            };

            lock (_lock)
            {
                project.TrainingSessions ??= new List<TrainingSession>();
                project.TrainingSessions.AddRange(sessions);
                _projects[project.ProjectId] = project;
            }

            return plan;
        }

        public List<AsBuiltItem> VerifyAsBuilts(HandoverProject project, List<AsBuiltItem> items)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            var results = new List<AsBuiltItem>();

            foreach (var item in items ?? new List<AsBuiltItem>())
            {
                bool hasDiscrepancy = new Random().NextDouble() < 0.1;

                item.VerificationStatus = hasDiscrepancy ? AsBuiltVerification.DiscrepancyFound : AsBuiltVerification.Verified;
                item.VerifiedDate = DateTime.UtcNow;

                if (hasDiscrepancy)
                {
                    item.DiscrepancyDescription = $"Minor discrepancy found in {item.Element} at {item.Location}";
                    item.ModelUpdated = false;
                }
                else
                {
                    item.ModelUpdated = true;
                }

                results.Add(item);
            }

            lock (_lock)
            {
                project.AsBuiltItems ??= new List<AsBuiltItem>();
                project.AsBuiltItems.AddRange(results);
                UpdateMetrics(project);
                _projects[project.ProjectId] = project;
            }

            return results;
        }

        public Deficiency TrackDefectLiability(HandoverProject project, string description, string location, string system, DeficiencyPriority priority, string identifiedBy, bool isWarrantyItem = false)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            int count = (project.Deficiencies?.Count ?? 0) + 1;
            var deficiency = new Deficiency
            {
                DeficiencyNumber = $"DEF-{count:D4}",
                Description = description,
                Location = location,
                System = system,
                Priority = priority,
                IdentifiedBy = identifiedBy,
                IsWarrantyItem = isWarrantyItem,
                DueDate = DateTime.Now.AddDays(priority == DeficiencyPriority.Critical ? 3 : priority == DeficiencyPriority.Major ? 7 : 14)
            };

            lock (_lock)
            {
                project.Deficiencies ??= new List<Deficiency>();
                project.Deficiencies.Add(deficiency);
                UpdateMetrics(project);
                _projects[project.ProjectId] = project;
            }

            return deficiency;
        }

        public Deficiency ResolveDeficiency(HandoverProject project, string deficiencyId, string resolutionNotes, string verifiedBy)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            lock (_lock)
            {
                var deficiency = project.Deficiencies?.FirstOrDefault(d => d.DeficiencyId == deficiencyId)
                    ?? throw new InvalidOperationException($"Deficiency {deficiencyId} not found");

                deficiency.Status = DeficiencyStatus.Resolved;
                deficiency.ResolvedDate = DateTime.UtcNow;
                deficiency.ResolutionNotes = resolutionNotes;
                deficiency.VerifiedDate = DateTime.UtcNow;
                deficiency.VerifiedBy = verifiedBy;
                deficiency.DaysOpen = (int)(DateTime.UtcNow - deficiency.IdentifiedDate).TotalDays;

                UpdateMetrics(project);
                _projects[project.ProjectId] = project;
                return deficiency;
            }
        }

        public CloseoutPackage ManageCloseout(HandoverProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            lock (_lock)
            {
                project.CloseoutPackage ??= new CloseoutPackage();
                var pkg = project.CloseoutPackage;

                var missingItems = new List<string>();

                int totalDocs = project.Documents?.Count ?? 0;
                int approvedDocs = project.Documents?.Count(d => d.Status == DocumentStatus.Approved) ?? 0;
                if (approvedDocs < totalDocs) missingItems.Add($"{totalDocs - approvedDocs} documents pending approval");

                int openDef = project.Deficiencies?.Count(d => d.Status != DeficiencyStatus.Resolved) ?? 0;
                if (openDef > 0) missingItems.Add($"{openDef} deficiencies to resolve");

                int pendingComm = project.CommissioningItems?.Count(c => c.Status != CommissioningStatus.Passed) ?? 0;
                if (pendingComm > 0) missingItems.Add($"{pendingComm} commissioning items pending");

                int incompleteTrain = project.TrainingSessions?.Count(t => !t.IsComplete) ?? 0;
                if (incompleteTrain > 0) missingItems.Add($"{incompleteTrain} training sessions incomplete");

                int unverifiedAsBuilt = project.AsBuiltItems?.Count(a => a.VerificationStatus != AsBuiltVerification.Verified && a.VerificationStatus != AsBuiltVerification.Corrected) ?? 0;
                if (unverifiedAsBuilt > 0) missingItems.Add($"{unverifiedAsBuilt} as-built items unverified");

                pkg.MissingItems = missingItems;
                pkg.RequiredApprovals = new List<string> { "Project Manager", "Owner Representative", "Architect", "MEP Engineer" };
                pkg.Certificates ??= new FinalCertificates();

                double total = 5.0;
                double complete = 0;
                if (approvedDocs >= totalDocs * 0.9) complete += 1;
                if (openDef == 0) complete += 1;
                if (pendingComm == 0) complete += 1;
                if (incompleteTrain == 0) complete += 1;
                if (unverifiedAsBuilt == 0) complete += 1;

                pkg.CompletionPercentage = complete / total * 100;
                pkg.Status = missingItems.Count == 0 ? CloseoutStatus.PendingApproval : CloseoutStatus.InProgress;

                UpdateMetrics(project);
                _projects[project.ProjectId] = project;
                return pkg;
            }
        }

        public HandoverMetrics GetMetrics(HandoverProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            UpdateMetrics(project);
            return project.Metrics;
        }

        #region Private Methods

        private List<OMSection> GenerateOMSections(HandoverProject project, List<string> systems)
        {
            var sections = new List<OMSection>
            {
                new OMSection { SectionNumber = "1", SectionTitle = "Introduction", Content = "Building overview and O&M manual introduction" },
                new OMSection { SectionNumber = "2", SectionTitle = "General Information", Content = "Contact information, emergency procedures, building description" },
                new OMSection { SectionNumber = "3", SectionTitle = "Preventive Maintenance Schedule", Content = "Maintenance schedules and procedures" },
                new OMSection { SectionNumber = "4", SectionTitle = "Emergency Procedures", Content = "Emergency shutdown and response procedures" }
            };

            int sectionNum = 5;
            foreach (var system in systems)
            {
                sections.Add(new OMSection
                {
                    SectionNumber = $"{sectionNum}",
                    SectionTitle = $"{system} System",
                    System = system,
                    SubSections = new List<string>
                    {
                        $"{sectionNum}.1 System Description",
                        $"{sectionNum}.2 Equipment Inventory",
                        $"{sectionNum}.3 Operating Procedures",
                        $"{sectionNum}.4 Maintenance Procedures",
                        $"{sectionNum}.5 Troubleshooting Guide",
                        $"{sectionNum}.6 Spare Parts List",
                        $"{sectionNum}.7 Warranty Information"
                    },
                    Content = $"Comprehensive O&M information for {system} system"
                });
                sectionNum++;
            }

            sections.Add(new OMSection { SectionNumber = $"{sectionNum}", SectionTitle = "Appendices", Content = "Equipment cut sheets, as-built drawings, test reports" });

            return sections;
        }

        private List<string> GenerateTopics(string system, string type)
        {
            var baseTopics = new List<string>
            {
                $"{system} system overview",
                $"{system} equipment identification",
                $"{system} operating principles"
            };

            if (type == "Theory")
            {
                baseTopics.AddRange(new[] { "System components", "Control systems", "Sequence of operations", "Energy management" });
            }
            else if (type == "Practical")
            {
                baseTopics.AddRange(new[] { "Startup procedures", "Shutdown procedures", "Adjustment procedures", "Troubleshooting basics", "BAS interface operation" });
            }
            else if (type == "Emergency")
            {
                baseTopics.AddRange(new[] { "Emergency shutdown", "Fire response", "Power failure", "Equipment failure response", "Emergency contacts" });
            }

            return baseTopics;
        }

        private void UpdateMetrics(HandoverProject project)
        {
            int totalComm = project.CommissioningItems?.Count ?? 0;
            int completedComm = project.CommissioningItems?.Count(c => c.Status == CommissioningStatus.Passed) ?? 0;

            int totalDocs = project.Documents?.Count ?? 0;
            int approvedDocs = project.Documents?.Count(d => d.Status == DocumentStatus.Approved) ?? 0;

            int totalDef = project.Deficiencies?.Count ?? 0;
            int openDef = project.Deficiencies?.Count(d => d.Status != DeficiencyStatus.Resolved) ?? 0;
            int critDef = project.Deficiencies?.Count(d => d.Priority == DeficiencyPriority.Critical && d.Status != DeficiencyStatus.Resolved) ?? 0;

            int totalTrain = project.TrainingSessions?.Count ?? 0;
            int completedTrain = project.TrainingSessions?.Count(t => t.IsComplete) ?? 0;

            int asBuiltVerified = project.AsBuiltItems?.Count(a => a.VerificationStatus == AsBuiltVerification.Verified || a.VerificationStatus == AsBuiltVerification.Corrected) ?? 0;
            int asBuiltDiscrep = project.AsBuiltItems?.Count(a => a.VerificationStatus == AsBuiltVerification.DiscrepancyFound) ?? 0;

            int daysToHandover = (int)(project.PlannedHandoverDate - DateTime.Now).TotalDays;

            double commProgress = totalComm > 0 ? completedComm * 100.0 / totalComm : 0;
            double docProgress = totalDocs > 0 ? approvedDocs * 100.0 / totalDocs : 0;
            double trainProgress = totalTrain > 0 ? completedTrain * 100.0 / totalTrain : 0;
            double defRate = totalDef > 0 ? (totalDef - openDef) * 100.0 / totalDef : 100;
            double closeoutProgress = project.CloseoutPackage?.CompletionPercentage ?? 0;

            double overall = (commProgress + docProgress + trainProgress + defRate + closeoutProgress) / 5;

            project.Metrics = new HandoverMetrics
            {
                OverallProgress = overall,
                TotalCommissioningItems = totalComm,
                CompletedCommissioningItems = completedComm,
                CommissioningProgress = commProgress,
                TotalDocuments = totalDocs,
                ApprovedDocuments = approvedDocs,
                DocumentProgress = docProgress,
                TotalDeficiencies = totalDef,
                OpenDeficiencies = openDef,
                CriticalDeficiencies = critDef,
                DeficiencyResolutionRate = defRate,
                TotalTrainingSessions = totalTrain,
                CompletedTrainingSessions = completedTrain,
                TrainingProgress = trainProgress,
                TotalWarranties = project.Warranties?.Count ?? 0,
                ActiveWarranties = project.Warranties?.Count(w => w.IsActive) ?? 0,
                AsBuiltItemsVerified = asBuiltVerified,
                AsBuiltDiscrepancies = asBuiltDiscrep,
                CloseoutProgress = closeoutProgress,
                DaysToHandover = daysToHandover,
                OnTrack = overall >= 80 || daysToHandover > 30
            };
        }

        #endregion
    }

    #endregion
}
