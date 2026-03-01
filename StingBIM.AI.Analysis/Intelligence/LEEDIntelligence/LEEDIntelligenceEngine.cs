// ===================================================================
// StingBIM LEED Intelligence Engine
// LEED v4.1 certification tracking, credit analysis, documentation
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.LEEDIntelligence
{
    #region Enums

    public enum LEEDRatingSystem { BD_C, ID_C, O_M, ND, Homes, Cities }
    public enum LEEDVersion { v4, v4_1 }
    public enum CertificationLevel { Certified, Silver, Gold, Platinum }
    public enum CreditCategory { Location, Sustainable, Water, Energy, Materials, IndoorQuality, Innovation, Regional }
    public enum CreditStatus { NotPursued, Pursuing, Achieved, Denied, Pending }
    public enum DocumentStatus { NotStarted, InProgress, ReadyForReview, Submitted, Approved, Rejected }

    #endregion

    #region Data Models

    public class LEEDProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public LEEDRatingSystem RatingSystem { get; set; }
        public LEEDVersion Version { get; set; }
        public double GrossArea { get; set; }
        public CertificationLevel TargetLevel { get; set; }
        public List<LEEDCredit> Credits { get; set; } = new();
        public List<Prerequisite> Prerequisites { get; set; } = new();
        public CertificationStatus Status { get; set; }
        public DateTime RegistrationDate { get; set; }
        public DateTime TargetCertificationDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class LEEDCredit
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CreditId { get; set; }
        public string Name { get; set; }
        public CreditCategory Category { get; set; }
        public int MaxPoints { get; set; }
        public int TargetPoints { get; set; }
        public int AchievedPoints { get; set; }
        public CreditStatus Status { get; set; }
        public List<CreditOption> Options { get; set; } = new();
        public List<CreditRequirement> Requirements { get; set; } = new();
        public List<CreditDocument> Documents { get; set; } = new();
        public string ResponsibleParty { get; set; }
        public List<string> Notes { get; set; } = new();
        public double EstimatedCost { get; set; }
    }

    public class CreditOption
    {
        public string Name { get; set; }
        public int Points { get; set; }
        public string Description { get; set; }
        public List<string> Requirements { get; set; } = new();
        public bool IsSelected { get; set; }
    }

    public class CreditRequirement
    {
        public string Description { get; set; }
        public string Threshold { get; set; }
        public string ActualValue { get; set; }
        public bool IsMet { get; set; }
        public string CalculationMethod { get; set; }
    }

    public class CreditDocument
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public DocumentStatus Status { get; set; }
        public string ResponsibleParty { get; set; }
        public DateTime DueDate { get; set; }
        public string FilePath { get; set; }
    }

    public class Prerequisite
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PrereqId { get; set; }
        public string Name { get; set; }
        public CreditCategory Category { get; set; }
        public bool IsMet { get; set; }
        public List<CreditRequirement> Requirements { get; set; } = new();
        public List<CreditDocument> Documents { get; set; } = new();
        public string Notes { get; set; }
    }

    public class CertificationStatus
    {
        public int TotalPossiblePoints { get; set; }
        public int TargetPoints { get; set; }
        public int AchievedPoints { get; set; }
        public int PendingPoints { get; set; }
        public int DeniedPoints { get; set; }
        public CertificationLevel CurrentLevel { get; set; }
        public CertificationLevel ProjectedLevel { get; set; }
        public bool AllPrerequisitesMet { get; set; }
        public double ConfidenceScore { get; set; }
        public Dictionary<CreditCategory, int> PointsByCategory { get; set; } = new();
    }

    public class EnergyPerformance
    {
        public double BaselineEUI { get; set; }
        public double ProposedEUI { get; set; }
        public double PercentReduction { get; set; }
        public int EnergyPoints { get; set; }
        public double RenewablePercentage { get; set; }
        public double CostSavings { get; set; }
        public string ModelPath { get; set; }
    }

    public class WaterPerformance
    {
        public double BaselineGPF { get; set; }
        public double ProposedGPF { get; set; }
        public double IndoorReduction { get; set; }
        public double OutdoorReduction { get; set; }
        public int WaterPoints { get; set; }
        public double ProcessWaterReduction { get; set; }
    }

    public class MaterialsAnalysis
    {
        public double TotalMaterialCost { get; set; }
        public double RegionalMaterialCost { get; set; }
        public double RecycledContentCost { get; set; }
        public double RapidlyRenewableCost { get; set; }
        public double FSCWoodCost { get; set; }
        public double EPDCoverage { get; set; }
        public int MaterialsPoints { get; set; }
    }

    public class IEQAnalysis
    {
        public bool MeetsASHRAE62_1 { get; set; }
        public double OutdoorAirRate { get; set; }
        public bool HasCO2Monitoring { get; set; }
        public bool MeetsLowEmittingMaterials { get; set; }
        public double DaylitArea { get; set; }
        public double ViewsArea { get; set; }
        public double ThermalComfortCompliance { get; set; }
        public int IEQPoints { get; set; }
    }

    public class CostBenefitAnalysis
    {
        public double CertificationCost { get; set; }
        public double PremiumCost { get; set; }
        public double DocumentationCost { get; set; }
        public double TotalIncrementalCost { get; set; }
        public double AnnualEnergySavings { get; set; }
        public double AnnualWaterSavings { get; set; }
        public double AnnualOperationalSavings { get; set; }
        public double SimplePayback { get; set; }
        public double PropertyValuePremium { get; set; }
        public List<string> IntangibleBenefits { get; set; } = new();
    }

    #endregion

    public sealed class LEEDIntelligenceEngine
    {
        private static readonly Lazy<LEEDIntelligenceEngine> _instance =
            new Lazy<LEEDIntelligenceEngine>(() => new LEEDIntelligenceEngine());
        public static LEEDIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, LEEDProject> _projects = new();
        private readonly object _lock = new object();

        // LEED v4.1 BD+C Point Thresholds
        private readonly Dictionary<CertificationLevel, int> _pointThresholds = new()
        {
            [CertificationLevel.Certified] = 40,
            [CertificationLevel.Silver] = 50,
            [CertificationLevel.Gold] = 60,
            [CertificationLevel.Platinum] = 80
        };

        // LEED v4.1 BD+C Maximum Points by Category
        private readonly Dictionary<CreditCategory, int> _maxPointsByCategory = new()
        {
            [CreditCategory.Location] = 16,
            [CreditCategory.Sustainable] = 10,
            [CreditCategory.Water] = 11,
            [CreditCategory.Energy] = 33,
            [CreditCategory.Materials] = 13,
            [CreditCategory.IndoorQuality] = 16,
            [CreditCategory.Innovation] = 6,
            [CreditCategory.Regional] = 4
        };

        // Energy points by percent improvement
        private readonly Dictionary<int, int> _energyPointTable = new()
        {
            [6] = 1, [8] = 2, [10] = 3, [12] = 4, [14] = 5,
            [16] = 6, [18] = 7, [20] = 8, [22] = 9, [24] = 10,
            [26] = 11, [29] = 12, [32] = 13, [35] = 14, [38] = 15,
            [42] = 16, [46] = 17, [50] = 18
        };

        private LEEDIntelligenceEngine() { }

        public LEEDProject CreateLEEDProject(string projectId, string projectName,
            LEEDRatingSystem ratingSystem, double grossArea, CertificationLevel targetLevel)
        {
            var project = new LEEDProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                RatingSystem = ratingSystem,
                Version = LEEDVersion.v4_1,
                GrossArea = grossArea,
                TargetLevel = targetLevel,
                RegistrationDate = DateTime.UtcNow
            };

            // Initialize with standard BD+C credits
            if (ratingSystem == LEEDRatingSystem.BD_C)
            {
                InitializeBDCCredits(project);
                InitializeBDCPrerequisites(project);
            }

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        private void InitializeBDCCredits(LEEDProject project)
        {
            // Location and Transportation
            project.Credits.AddRange(new[]
            {
                new LEEDCredit { CreditId = "LT-C1", Name = "Sensitive Land Protection", Category = CreditCategory.Location, MaxPoints = 1 },
                new LEEDCredit { CreditId = "LT-C2", Name = "High Priority Site", Category = CreditCategory.Location, MaxPoints = 2 },
                new LEEDCredit { CreditId = "LT-C3", Name = "Surrounding Density and Diverse Uses", Category = CreditCategory.Location, MaxPoints = 5 },
                new LEEDCredit { CreditId = "LT-C4", Name = "Access to Quality Transit", Category = CreditCategory.Location, MaxPoints = 5 },
                new LEEDCredit { CreditId = "LT-C5", Name = "Bicycle Facilities", Category = CreditCategory.Location, MaxPoints = 1 },
                new LEEDCredit { CreditId = "LT-C6", Name = "Reduced Parking Footprint", Category = CreditCategory.Location, MaxPoints = 1 },
                new LEEDCredit { CreditId = "LT-C7", Name = "Electric Vehicles", Category = CreditCategory.Location, MaxPoints = 1 }
            });

            // Sustainable Sites
            project.Credits.AddRange(new[]
            {
                new LEEDCredit { CreditId = "SS-C1", Name = "Site Assessment", Category = CreditCategory.Sustainable, MaxPoints = 1 },
                new LEEDCredit { CreditId = "SS-C2", Name = "Protect or Restore Habitat", Category = CreditCategory.Sustainable, MaxPoints = 2 },
                new LEEDCredit { CreditId = "SS-C3", Name = "Open Space", Category = CreditCategory.Sustainable, MaxPoints = 1 },
                new LEEDCredit { CreditId = "SS-C4", Name = "Rainwater Management", Category = CreditCategory.Sustainable, MaxPoints = 3 },
                new LEEDCredit { CreditId = "SS-C5", Name = "Heat Island Reduction", Category = CreditCategory.Sustainable, MaxPoints = 2 },
                new LEEDCredit { CreditId = "SS-C6", Name = "Light Pollution Reduction", Category = CreditCategory.Sustainable, MaxPoints = 1 }
            });

            // Water Efficiency
            project.Credits.AddRange(new[]
            {
                new LEEDCredit { CreditId = "WE-C1", Name = "Outdoor Water Use Reduction", Category = CreditCategory.Water, MaxPoints = 2 },
                new LEEDCredit { CreditId = "WE-C2", Name = "Indoor Water Use Reduction", Category = CreditCategory.Water, MaxPoints = 6 },
                new LEEDCredit { CreditId = "WE-C3", Name = "Cooling Tower Water Use", Category = CreditCategory.Water, MaxPoints = 2 },
                new LEEDCredit { CreditId = "WE-C4", Name = "Water Metering", Category = CreditCategory.Water, MaxPoints = 1 }
            });

            // Energy and Atmosphere
            project.Credits.AddRange(new[]
            {
                new LEEDCredit { CreditId = "EA-C1", Name = "Enhanced Commissioning", Category = CreditCategory.Energy, MaxPoints = 6 },
                new LEEDCredit { CreditId = "EA-C2", Name = "Optimize Energy Performance", Category = CreditCategory.Energy, MaxPoints = 18 },
                new LEEDCredit { CreditId = "EA-C3", Name = "Advanced Energy Metering", Category = CreditCategory.Energy, MaxPoints = 1 },
                new LEEDCredit { CreditId = "EA-C4", Name = "Demand Response", Category = CreditCategory.Energy, MaxPoints = 2 },
                new LEEDCredit { CreditId = "EA-C5", Name = "Renewable Energy", Category = CreditCategory.Energy, MaxPoints = 5 },
                new LEEDCredit { CreditId = "EA-C6", Name = "Enhanced Refrigerant Management", Category = CreditCategory.Energy, MaxPoints = 1 }
            });

            // Materials and Resources
            project.Credits.AddRange(new[]
            {
                new LEEDCredit { CreditId = "MR-C1", Name = "Building Life-Cycle Impact Reduction", Category = CreditCategory.Materials, MaxPoints = 5 },
                new LEEDCredit { CreditId = "MR-C2", Name = "BPDO - EPD", Category = CreditCategory.Materials, MaxPoints = 2 },
                new LEEDCredit { CreditId = "MR-C3", Name = "BPDO - Sourcing", Category = CreditCategory.Materials, MaxPoints = 2 },
                new LEEDCredit { CreditId = "MR-C4", Name = "BPDO - Material Ingredients", Category = CreditCategory.Materials, MaxPoints = 2 },
                new LEEDCredit { CreditId = "MR-C5", Name = "Construction Waste Management", Category = CreditCategory.Materials, MaxPoints = 2 }
            });

            // Indoor Environmental Quality
            project.Credits.AddRange(new[]
            {
                new LEEDCredit { CreditId = "EQ-C1", Name = "Enhanced Indoor Air Quality Strategies", Category = CreditCategory.IndoorQuality, MaxPoints = 2 },
                new LEEDCredit { CreditId = "EQ-C2", Name = "Low-Emitting Materials", Category = CreditCategory.IndoorQuality, MaxPoints = 3 },
                new LEEDCredit { CreditId = "EQ-C3", Name = "Construction IAQ Management Plan", Category = CreditCategory.IndoorQuality, MaxPoints = 1 },
                new LEEDCredit { CreditId = "EQ-C4", Name = "Indoor Air Quality Assessment", Category = CreditCategory.IndoorQuality, MaxPoints = 2 },
                new LEEDCredit { CreditId = "EQ-C5", Name = "Thermal Comfort", Category = CreditCategory.IndoorQuality, MaxPoints = 1 },
                new LEEDCredit { CreditId = "EQ-C6", Name = "Interior Lighting", Category = CreditCategory.IndoorQuality, MaxPoints = 2 },
                new LEEDCredit { CreditId = "EQ-C7", Name = "Daylight", Category = CreditCategory.IndoorQuality, MaxPoints = 3 },
                new LEEDCredit { CreditId = "EQ-C8", Name = "Quality Views", Category = CreditCategory.IndoorQuality, MaxPoints = 1 },
                new LEEDCredit { CreditId = "EQ-C9", Name = "Acoustic Performance", Category = CreditCategory.IndoorQuality, MaxPoints = 1 }
            });

            // Innovation
            project.Credits.AddRange(new[]
            {
                new LEEDCredit { CreditId = "IN-C1", Name = "Innovation", Category = CreditCategory.Innovation, MaxPoints = 5 },
                new LEEDCredit { CreditId = "IN-C2", Name = "LEED Accredited Professional", Category = CreditCategory.Innovation, MaxPoints = 1 }
            });

            // Regional Priority
            project.Credits.Add(new LEEDCredit { CreditId = "RP-C1", Name = "Regional Priority", Category = CreditCategory.Regional, MaxPoints = 4 });
        }

        private void InitializeBDCPrerequisites(LEEDProject project)
        {
            project.Prerequisites.AddRange(new[]
            {
                new Prerequisite { PrereqId = "LT-P1", Name = "Construction Activity Pollution Prevention", Category = CreditCategory.Location },
                new Prerequisite { PrereqId = "WE-P1", Name = "Outdoor Water Use Reduction", Category = CreditCategory.Water },
                new Prerequisite { PrereqId = "WE-P2", Name = "Indoor Water Use Reduction", Category = CreditCategory.Water },
                new Prerequisite { PrereqId = "WE-P3", Name = "Building-Level Water Metering", Category = CreditCategory.Water },
                new Prerequisite { PrereqId = "EA-P1", Name = "Fundamental Commissioning", Category = CreditCategory.Energy },
                new Prerequisite { PrereqId = "EA-P2", Name = "Minimum Energy Performance", Category = CreditCategory.Energy },
                new Prerequisite { PrereqId = "EA-P3", Name = "Building-Level Energy Metering", Category = CreditCategory.Energy },
                new Prerequisite { PrereqId = "EA-P4", Name = "Fundamental Refrigerant Management", Category = CreditCategory.Energy },
                new Prerequisite { PrereqId = "MR-P1", Name = "Storage and Collection of Recyclables", Category = CreditCategory.Materials },
                new Prerequisite { PrereqId = "MR-P2", Name = "Construction Waste Management Planning", Category = CreditCategory.Materials },
                new Prerequisite { PrereqId = "EQ-P1", Name = "Minimum Indoor Air Quality Performance", Category = CreditCategory.IndoorQuality },
                new Prerequisite { PrereqId = "EQ-P2", Name = "Environmental Tobacco Smoke Control", Category = CreditCategory.IndoorQuality }
            });
        }

        public void SetCreditTarget(string projectId, string creditId, int targetPoints, CreditStatus status)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return;

                var credit = project.Credits.FirstOrDefault(c => c.CreditId == creditId);
                if (credit != null)
                {
                    credit.TargetPoints = Math.Min(targetPoints, credit.MaxPoints);
                    credit.Status = status;
                }
            }
        }

        public async Task<CertificationStatus> CalculateCertificationStatus(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var status = new CertificationStatus
                    {
                        TotalPossiblePoints = project.Credits.Sum(c => c.MaxPoints),
                        AllPrerequisitesMet = project.Prerequisites.All(p => p.IsMet)
                    };

                    // Calculate points
                    status.AchievedPoints = project.Credits
                        .Where(c => c.Status == CreditStatus.Achieved)
                        .Sum(c => c.AchievedPoints);

                    status.PendingPoints = project.Credits
                        .Where(c => c.Status == CreditStatus.Pursuing || c.Status == CreditStatus.Pending)
                        .Sum(c => c.TargetPoints);

                    status.DeniedPoints = project.Credits
                        .Where(c => c.Status == CreditStatus.Denied)
                        .Sum(c => c.TargetPoints);

                    status.TargetPoints = project.Credits.Sum(c => c.TargetPoints);

                    // Points by category
                    foreach (var category in Enum.GetValues<CreditCategory>())
                    {
                        status.PointsByCategory[category] = project.Credits
                            .Where(c => c.Category == category && c.Status == CreditStatus.Achieved)
                            .Sum(c => c.AchievedPoints);
                    }

                    // Determine current level
                    status.CurrentLevel = GetCertificationLevel(status.AchievedPoints);

                    // Determine projected level
                    int projectedPoints = status.AchievedPoints + status.PendingPoints;
                    status.ProjectedLevel = GetCertificationLevel(projectedPoints);

                    // Calculate confidence score
                    int achieved = status.AchievedPoints;
                    int pending = status.PendingPoints;
                    int targetThreshold = _pointThresholds.GetValueOrDefault(project.TargetLevel, 40);
                    status.ConfidenceScore = achieved >= targetThreshold ? 95 :
                        (achieved + pending) >= targetThreshold ?
                            50 + (achieved * 50.0 / targetThreshold) : 30;

                    project.Status = status;
                    return status;
                }
            });
        }

        private CertificationLevel GetCertificationLevel(int points)
        {
            if (points >= 80) return CertificationLevel.Platinum;
            if (points >= 60) return CertificationLevel.Gold;
            if (points >= 50) return CertificationLevel.Silver;
            return CertificationLevel.Certified;
        }

        public EnergyPerformance CalculateEnergyPerformance(string projectId, double baselineEUI, double proposedEUI)
        {
            var performance = new EnergyPerformance
            {
                BaselineEUI = baselineEUI,
                ProposedEUI = proposedEUI,
                PercentReduction = (baselineEUI - proposedEUI) / baselineEUI * 100
            };

            // Look up points
            int improvement = (int)performance.PercentReduction;
            performance.EnergyPoints = _energyPointTable
                .Where(kv => improvement >= kv.Key)
                .Select(kv => kv.Value)
                .LastOrDefault();

            return performance;
        }

        public WaterPerformance CalculateWaterPerformance(string projectId, double baselineGPF, double proposedGPF,
            double outdoorReduction)
        {
            var performance = new WaterPerformance
            {
                BaselineGPF = baselineGPF,
                ProposedGPF = proposedGPF,
                IndoorReduction = (baselineGPF - proposedGPF) / baselineGPF * 100,
                OutdoorReduction = outdoorReduction
            };

            // Indoor water points
            if (performance.IndoorReduction >= 50)
                performance.WaterPoints = 6;
            else if (performance.IndoorReduction >= 45)
                performance.WaterPoints = 5;
            else if (performance.IndoorReduction >= 40)
                performance.WaterPoints = 4;
            else if (performance.IndoorReduction >= 35)
                performance.WaterPoints = 3;
            else if (performance.IndoorReduction >= 30)
                performance.WaterPoints = 2;
            else if (performance.IndoorReduction >= 25)
                performance.WaterPoints = 1;

            return performance;
        }

        public async Task<CostBenefitAnalysis> AnalyzeCostBenefit(string projectId, double energyRate, double waterRate)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var analysis = new CostBenefitAnalysis
                    {
                        CertificationCost = 15000 + project.GrossArea * 0.15,
                        DocumentationCost = project.Credits.Count * 500,
                        PremiumCost = project.Credits.Sum(c => c.EstimatedCost)
                    };

                    analysis.TotalIncrementalCost = analysis.CertificationCost +
                        analysis.DocumentationCost + analysis.PremiumCost;

                    // Estimate savings based on target level
                    double savingsMultiplier = project.TargetLevel switch
                    {
                        CertificationLevel.Platinum => 0.45,
                        CertificationLevel.Gold => 0.35,
                        CertificationLevel.Silver => 0.25,
                        _ => 0.15
                    };

                    analysis.AnnualEnergySavings = project.GrossArea * 2 * energyRate * savingsMultiplier;
                    analysis.AnnualWaterSavings = project.GrossArea * 0.05 * waterRate * savingsMultiplier;
                    analysis.AnnualOperationalSavings = analysis.AnnualEnergySavings + analysis.AnnualWaterSavings;

                    analysis.SimplePayback = analysis.TotalIncrementalCost / analysis.AnnualOperationalSavings;

                    // Property value premium
                    analysis.PropertyValuePremium = project.TargetLevel switch
                    {
                        CertificationLevel.Platinum => 0.12,
                        CertificationLevel.Gold => 0.08,
                        CertificationLevel.Silver => 0.05,
                        _ => 0.03
                    };

                    analysis.IntangibleBenefits.Add("Marketing and branding value");
                    analysis.IntangibleBenefits.Add("Tenant attraction and retention");
                    analysis.IntangibleBenefits.Add("Improved occupant health and productivity");
                    analysis.IntangibleBenefits.Add("Risk mitigation for future regulations");

                    return analysis;
                }
            });
        }

        public List<LEEDCredit> GetRecommendedCredits(string projectId, int additionalPointsNeeded)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return new List<LEEDCredit>();

                return project.Credits
                    .Where(c => c.Status == CreditStatus.NotPursued)
                    .OrderByDescending(c => (double)c.MaxPoints / (c.EstimatedCost > 0 ? c.EstimatedCost : 1000))
                    .Take(10)
                    .ToList();
            }
        }

        public List<CreditDocument> GetPendingDocuments(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return new List<CreditDocument>();

                return project.Credits
                    .SelectMany(c => c.Documents)
                    .Where(d => d.Status == DocumentStatus.NotStarted || d.Status == DocumentStatus.InProgress)
                    .OrderBy(d => d.DueDate)
                    .ToList();
            }
        }
    }
}
