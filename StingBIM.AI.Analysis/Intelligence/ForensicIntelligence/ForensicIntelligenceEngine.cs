// ===================================================================
// StingBIM Forensic Intelligence Engine
// Root cause analysis, failure investigation, dispute support
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.ForensicIntelligence
{
    #region Enums

    public enum InvestigationType { StructuralFailure, WaterIntrusion, FireDamage, ConstructionDefect, ScheduleDelay, CostOverrun }
    public enum InvestigationStatus { Initiated, DataCollection, Analysis, Reporting, Closed }
    public enum EvidenceType { Photo, Document, Sample, Measurement, Testimony, Video }
    public enum CauseFactor { Design, Material, Workmanship, Environment, Maintenance, Unknown }

    #endregion

    #region Data Models

    public class ForensicInvestigation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string CaseNumber { get; set; }
        public string Title { get; set; }
        public InvestigationType Type { get; set; }
        public InvestigationStatus Status { get; set; } = InvestigationStatus.Initiated;
        public DateTime IncidentDate { get; set; }
        public DateTime InvestigationStartDate { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedDate { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public List<Evidence> EvidenceItems { get; set; } = new();
        public List<RootCause> RootCauses { get; set; } = new();
        public List<Witness> Witnesses { get; set; } = new();
        public Timeline EventTimeline { get; set; }
        public List<Finding> Findings { get; set; } = new();
        public ForensicReport Report { get; set; }
    }

    public class Evidence
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public EvidenceType Type { get; set; }
        public string Description { get; set; }
        public DateTime CollectedDate { get; set; } = DateTime.UtcNow;
        public string CollectedBy { get; set; }
        public string Location { get; set; }
        public string FilePath { get; set; }
        public string ChainOfCustody { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public double Relevance { get; set; }
    }

    public class RootCause
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Description { get; set; }
        public CauseFactor Factor { get; set; }
        public double Confidence { get; set; }
        public List<string> ContributingFactors { get; set; } = new();
        public List<string> SupportingEvidence { get; set; } = new();
        public string ResponsibleParty { get; set; }
        public List<string> PreventiveMeasures { get; set; } = new();
    }

    public class Witness
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Role { get; set; }
        public string Company { get; set; }
        public string ContactInfo { get; set; }
        public DateTime? InterviewDate { get; set; }
        public string Statement { get; set; }
        public double Credibility { get; set; }
    }

    public class Timeline
    {
        public List<TimelineEvent> Events { get; set; } = new();
        public DateTime EarliestEvent => Events.Any() ? Events.Min(e => e.DateTime) : DateTime.MinValue;
        public DateTime LatestEvent => Events.Any() ? Events.Max(e => e.DateTime) : DateTime.MinValue;
    }

    public class TimelineEvent
    {
        public DateTime DateTime { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public string Category { get; set; }
        public bool IsCritical { get; set; }
        public List<string> RelatedEvidenceIds { get; set; } = new();
    }

    public class Finding
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Description { get; set; }
        public string Significance { get; set; }
        public List<string> SupportingEvidence { get; set; } = new();
        public double Certainty { get; set; }
    }

    public class ForensicReport
    {
        public string InvestigationId { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string ExecutiveSummary { get; set; }
        public List<string> KeyFindings { get; set; } = new();
        public List<string> Conclusions { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public decimal EstimatedDamages { get; set; }
        public List<ResponsibilityAllocation> Responsibilities { get; set; } = new();
    }

    public class ResponsibilityAllocation
    {
        public string Party { get; set; }
        public double Percentage { get; set; }
        public string Basis { get; set; }
        public decimal AllocatedDamages { get; set; }
    }

    public class FishboneDiagram
    {
        public string Problem { get; set; }
        public Dictionary<string, List<string>> Categories { get; set; } = new()
        {
            ["Methods"] = new(),
            ["Materials"] = new(),
            ["Manpower"] = new(),
            ["Machines"] = new(),
            ["Measurement"] = new(),
            ["Environment"] = new()
        };
    }

    public class FiveWhyAnalysis
    {
        public string Problem { get; set; }
        public List<WhyLevel> Levels { get; set; } = new();
        public string RootCause { get; set; }
    }

    public class WhyLevel
    {
        public int Level { get; set; }
        public string Question { get; set; }
        public string Answer { get; set; }
    }

    public class FailureModeAnalysis
    {
        public string ComponentOrSystem { get; set; }
        public List<FailureMode> FailureModes { get; set; } = new();
    }

    public class FailureMode
    {
        public string Mode { get; set; }
        public string Effect { get; set; }
        public string Cause { get; set; }
        public int Severity { get; set; }
        public int Occurrence { get; set; }
        public int Detection { get; set; }
        public int RPN => Severity * Occurrence * Detection;
        public string RecommendedAction { get; set; }
    }

    #endregion

    public sealed class ForensicIntelligenceEngine
    {
        private static readonly Lazy<ForensicIntelligenceEngine> _instance =
            new Lazy<ForensicIntelligenceEngine>(() => new ForensicIntelligenceEngine());
        public static ForensicIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, ForensicInvestigation> _investigations = new();
        private readonly object _lock = new object();

        private ForensicIntelligenceEngine() { }

        public ForensicInvestigation CreateInvestigation(string projectId, string title)
        {
            var investigation = new ForensicInvestigation
            {
                ProjectId = projectId,
                Title = title,
                CaseNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                EventTimeline = new Timeline()
            };

            lock (_lock) { _investigations[investigation.Id] = investigation; }
            return investigation;
        }

        public ForensicInvestigation InitiateInvestigation(string projectId, string title,
            InvestigationType type, DateTime incidentDate, string description, string location)
        {
            var investigation = new ForensicInvestigation
            {
                ProjectId = projectId,
                Title = title,
                CaseNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                Type = type,
                IncidentDate = incidentDate,
                Description = description,
                Location = location,
                EventTimeline = new Timeline()
            };

            investigation.EventTimeline.Events.Add(new TimelineEvent
            {
                DateTime = incidentDate,
                Description = "Incident occurred",
                Category = "Incident",
                IsCritical = true
            });

            lock (_lock) { _investigations[investigation.Id] = investigation; }
            return investigation;
        }

        public Evidence CollectEvidence(string investigationId, EvidenceType type,
            string description, string collectedBy, string location, string filePath = null)
        {
            lock (_lock)
            {
                if (!_investigations.TryGetValue(investigationId, out var investigation))
                    return null;

                var evidence = new Evidence
                {
                    Type = type,
                    Description = description,
                    CollectedBy = collectedBy,
                    Location = location,
                    FilePath = filePath,
                    ChainOfCustody = $"Collected by {collectedBy} on {DateTime.UtcNow:yyyy-MM-dd}"
                };

                investigation.EvidenceItems.Add(evidence);
                investigation.Status = InvestigationStatus.DataCollection;

                return evidence;
            }
        }

        public Witness RecordWitnessStatement(string investigationId, string name, string role,
            string company, string statement)
        {
            lock (_lock)
            {
                if (!_investigations.TryGetValue(investigationId, out var investigation))
                    return null;

                var witness = new Witness
                {
                    Name = name,
                    Role = role,
                    Company = company,
                    InterviewDate = DateTime.UtcNow,
                    Statement = statement,
                    Credibility = 0.8
                };

                investigation.Witnesses.Add(witness);
                return witness;
            }
        }

        public void AddTimelineEvent(string investigationId, DateTime dateTime,
            string description, string source, bool isCritical)
        {
            lock (_lock)
            {
                if (!_investigations.TryGetValue(investigationId, out var investigation))
                    return;

                investigation.EventTimeline.Events.Add(new TimelineEvent
                {
                    DateTime = dateTime,
                    Description = description,
                    Source = source,
                    IsCritical = isCritical
                });

                investigation.EventTimeline.Events = investigation.EventTimeline.Events
                    .OrderBy(e => e.DateTime)
                    .ToList();
            }
        }

        public async Task<FiveWhyAnalysis> PerformFiveWhyAnalysis(string investigationId, string problem)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_investigations.TryGetValue(investigationId, out var investigation))
                        return null;

                    var analysis = new FiveWhyAnalysis { Problem = problem };

                    var prompts = new[]
                    {
                        "Why did this happen?",
                        "Why did that cause occur?",
                        "Why was that condition present?",
                        "Why did that situation exist?",
                        "Why was that the case?"
                    };

                    var answers = investigation.Type switch
                    {
                        InvestigationType.WaterIntrusion => new[]
                        {
                            "Water penetrated the building envelope",
                            "The flashing detail was inadequate",
                            "The design did not account for wind-driven rain",
                            "Standard details were used without site-specific analysis",
                            "Design review process did not include envelope specialist"
                        },
                        InvestigationType.StructuralFailure => new[]
                        {
                            "Structural element failed under load",
                            "Element was undersized for actual conditions",
                            "Loads exceeded design assumptions",
                            "Actual use differed from design intent",
                            "Change in use was not communicated to structural engineer"
                        },
                        _ => new[]
                        {
                            "The immediate cause was present",
                            "Contributing factors were not controlled",
                            "Preventive measures were insufficient",
                            "Risk assessment was incomplete",
                            "Process controls were lacking"
                        }
                    };

                    for (int i = 0; i < 5; i++)
                    {
                        analysis.Levels.Add(new WhyLevel
                        {
                            Level = i + 1,
                            Question = prompts[i],
                            Answer = answers[i]
                        });
                    }

                    analysis.RootCause = answers[4];
                    return analysis;
                }
            });
        }

        public FishboneDiagram CreateFishboneDiagram(string investigationId, string problem)
        {
            lock (_lock)
            {
                if (!_investigations.TryGetValue(investigationId, out var investigation))
                    return null;

                var diagram = new FishboneDiagram { Problem = problem };

                // Populate based on investigation type
                switch (investigation.Type)
                {
                    case InvestigationType.StructuralFailure:
                        diagram.Categories["Methods"] = new List<string> { "Incorrect construction sequence", "Inadequate curing" };
                        diagram.Categories["Materials"] = new List<string> { "Substandard concrete", "Corroded reinforcement" };
                        diagram.Categories["Manpower"] = new List<string> { "Insufficient supervision", "Untrained workers" };
                        diagram.Categories["Machines"] = new List<string> { "Equipment failure", "Improper tools" };
                        diagram.Categories["Measurement"] = new List<string> { "Inadequate testing", "Survey errors" };
                        diagram.Categories["Environment"] = new List<string> { "Extreme temperatures", "Seismic activity" };
                        break;

                    case InvestigationType.WaterIntrusion:
                        diagram.Categories["Methods"] = new List<string> { "Improper installation", "Missing sealant" };
                        diagram.Categories["Materials"] = new List<string> { "Failed membrane", "Incompatible materials" };
                        diagram.Categories["Manpower"] = new List<string> { "Inexperienced installers", "Poor workmanship" };
                        diagram.Categories["Environment"] = new List<string> { "Wind-driven rain", "Thermal movement" };
                        break;
                }

                return diagram;
            }
        }

        public async Task<List<RootCause>> AnalyzeRootCauses(string investigationId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_investigations.TryGetValue(investigationId, out var investigation))
                        return new List<RootCause>();

                    var rootCauses = new List<RootCause>();

                    // Analyze evidence to determine causes
                    var evidenceByType = investigation.EvidenceItems
                        .GroupBy(e => e.Type)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    if (evidenceByType.ContainsKey(EvidenceType.Photo) && evidenceByType[EvidenceType.Photo].Count > 3)
                    {
                        rootCauses.Add(new RootCause
                        {
                            Description = "Visual evidence indicates construction defect",
                            Factor = CauseFactor.Workmanship,
                            Confidence = 0.75,
                            SupportingEvidence = evidenceByType[EvidenceType.Photo].Select(e => e.Id).ToList(),
                            PreventiveMeasures = new List<string>
                            {
                                "Enhanced quality control inspections",
                                "Mandatory hold points",
                                "Third-party inspection"
                            }
                        });
                    }

                    if (investigation.Type == InvestigationType.WaterIntrusion)
                    {
                        rootCauses.Add(new RootCause
                        {
                            Description = "Inadequate waterproofing design or installation",
                            Factor = CauseFactor.Design,
                            Confidence = 0.65,
                            ContributingFactors = new List<string>
                            {
                                "Standard details used without modification",
                                "Lack of envelope consultant",
                                "Value engineering removed critical features"
                            },
                            PreventiveMeasures = new List<string>
                            {
                                "Engage envelope consultant",
                                "Site-specific detail development",
                                "Pre-construction mockup testing"
                            }
                        });
                    }

                    investigation.RootCauses = rootCauses;
                    investigation.Status = InvestigationStatus.Analysis;

                    return rootCauses;
                }
            });
        }

        public Finding AddFinding(string investigationId, string title, string description,
            string significance, double certainty, List<string> evidenceIds)
        {
            lock (_lock)
            {
                if (!_investigations.TryGetValue(investigationId, out var investigation))
                    return null;

                var finding = new Finding
                {
                    Title = title,
                    Description = description,
                    Significance = significance,
                    Certainty = certainty,
                    SupportingEvidence = evidenceIds
                };

                investigation.Findings.Add(finding);
                return finding;
            }
        }

        public async Task<ForensicReport> GenerateReport(string investigationId, decimal estimatedDamages)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_investigations.TryGetValue(investigationId, out var investigation))
                        return null;

                    var report = new ForensicReport
                    {
                        InvestigationId = investigationId,
                        EstimatedDamages = estimatedDamages
                    };

                    report.ExecutiveSummary = $"Investigation {investigation.CaseNumber} examined {investigation.Type} " +
                        $"at {investigation.Location}. Analysis of {investigation.EvidenceItems.Count} evidence items " +
                        $"and {investigation.Witnesses.Count} witness statements identified {investigation.RootCauses.Count} root causes.";

                    foreach (var finding in investigation.Findings.OrderByDescending(f => f.Certainty))
                    {
                        report.KeyFindings.Add($"{finding.Title} (Certainty: {finding.Certainty:P0})");
                    }

                    foreach (var cause in investigation.RootCauses.OrderByDescending(c => c.Confidence))
                    {
                        report.Conclusions.Add($"{cause.Factor}: {cause.Description}");

                        if (!string.IsNullOrEmpty(cause.ResponsibleParty))
                        {
                            report.Responsibilities.Add(new ResponsibilityAllocation
                            {
                                Party = cause.ResponsibleParty,
                                Percentage = cause.Confidence * 100,
                                Basis = cause.Description,
                                AllocatedDamages = estimatedDamages * (decimal)cause.Confidence
                            });
                        }
                    }

                    report.Recommendations = investigation.RootCauses
                        .SelectMany(c => c.PreventiveMeasures)
                        .Distinct()
                        .ToList();

                    investigation.Report = report;
                    investigation.Status = InvestigationStatus.Reporting;

                    return report;
                }
            });
        }

        public FailureModeAnalysis PerformFMEA(string investigationId, string component)
        {
            var fmea = new FailureModeAnalysis { ComponentOrSystem = component };

            fmea.FailureModes = new List<FailureMode>
            {
                new FailureMode
                {
                    Mode = "Corrosion",
                    Effect = "Loss of structural capacity",
                    Cause = "Moisture ingress, inadequate cover",
                    Severity = 9,
                    Occurrence = 4,
                    Detection = 5,
                    RecommendedAction = "Regular inspection, corrosion inhibitors"
                },
                new FailureMode
                {
                    Mode = "Cracking",
                    Effect = "Water penetration, reinforcement exposure",
                    Cause = "Thermal movement, overloading",
                    Severity = 7,
                    Occurrence = 6,
                    Detection = 3,
                    RecommendedAction = "Crack monitoring, timely repair"
                },
                new FailureMode
                {
                    Mode = "Connection failure",
                    Effect = "Progressive collapse potential",
                    Cause = "Design error, construction defect",
                    Severity = 10,
                    Occurrence = 2,
                    Detection = 6,
                    RecommendedAction = "Connection testing, redundancy"
                }
            };

            return fmea;
        }

        public ForensicInvestigation CloseInvestigation(string investigationId)
        {
            lock (_lock)
            {
                if (!_investigations.TryGetValue(investigationId, out var investigation))
                    return null;

                investigation.Status = InvestigationStatus.Closed;
                investigation.CompletedDate = DateTime.UtcNow;

                return investigation;
            }
        }
    }
}
