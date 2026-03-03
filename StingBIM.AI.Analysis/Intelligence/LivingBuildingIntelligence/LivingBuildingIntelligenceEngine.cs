// ===================================================================
// StingBIM Living Building Intelligence Engine
// Living Building Challenge certification, regenerative design, petals
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.LivingBuildingIntelligence
{
    #region Enums

    public enum LBCVersion { v3_1, v4_0 }
    public enum CertificationType { LivingBuilding, PetalCertification, CoreGreenBuilding, Zero_Energy, Zero_Carbon }
    public enum Petal { Place, Water, Energy, Health, Materials, Equity, Beauty }
    public enum ImperativeStatus { NotStarted, InProgress, Documented, Achieved, Exception }
    public enum TransectZone { L1_Natural, L2_Rural, L3_Village, L4_GeneralUrban, L5_UrbanCenter, L6_UrbanCore }
    public enum RedListStatus { Compliant, Temporary_Exception, Permanent_Exception }

    #endregion

    #region Data Models

    public class LBCProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public LBCVersion Version { get; set; } = LBCVersion.v4_0;
        public CertificationType TargetCertification { get; set; }
        public TransectZone Transect { get; set; }
        public double GrossArea { get; set; }
        public List<LBCPetal> Petals { get; set; } = new();
        public LBCScorecard Scorecard { get; set; }
        public DateTime RegistrationDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class LBCPetal
    {
        public Petal Type { get; set; }
        public string Description { get; set; }
        public List<Imperative> Imperatives { get; set; } = new();
        public bool IsComplete { get; set; }
        public int TotalImperatives { get; set; }
        public int CompletedImperatives { get; set; }
    }

    public class Imperative
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Number { get; set; }
        public string Name { get; set; }
        public Petal Petal { get; set; }
        public string Description { get; set; }
        public ImperativeStatus Status { get; set; }
        public List<string> Requirements { get; set; } = new();
        public List<string> Documentation { get; set; } = new();
        public string ExceptionJustification { get; set; }
        public double CompliancePercentage { get; set; }
    }

    public class LBCScorecard
    {
        public int TotalImperatives { get; set; }
        public int AchievedImperatives { get; set; }
        public int PendingImperatives { get; set; }
        public Dictionary<Petal, PetalStatus> PetalStatuses { get; set; } = new();
        public bool QualifiesForLivingBuilding { get; set; }
        public List<string> PathToLiving { get; set; } = new();
    }

    public class PetalStatus
    {
        public Petal Petal { get; set; }
        public int TotalImperatives { get; set; }
        public int AchievedImperatives { get; set; }
        public bool IsComplete { get; set; }
        public double CompletionPercentage => TotalImperatives > 0 ?
            AchievedImperatives * 100.0 / TotalImperatives : 0;
    }

    public class NetPositiveEnergy
    {
        public double AnnualEnergyUse { get; set; }
        public double OnSiteGeneration { get; set; }
        public double NetEnergy => OnSiteGeneration - AnnualEnergyUse;
        public bool IsNetPositive => NetEnergy >= 0;
        public double ExcessGeneration { get; set; }
        public List<RenewableSystem> Renewables { get; set; } = new();
        public double StorageCapacity { get; set; }
        public double EnergyIntensity { get; set; }
    }

    public class RenewableSystem
    {
        public string Type { get; set; }
        public double Capacity { get; set; }
        public double AnnualGeneration { get; set; }
        public double AreaRequired { get; set; }
        public bool IsOnSite { get; set; }
    }

    public class NetPositiveWater
    {
        public double AnnualWaterUse { get; set; }
        public double RainwaterCapture { get; set; }
        public double GreywaterReuse { get; set; }
        public double BlackwaterTreatment { get; set; }
        public double NetWater { get; set; }
        public bool IsNetPositive => NetWater >= 0;
        public double StormwaterManaged { get; set; }
        public WaterBalance Balance { get; set; }
    }

    public class WaterBalance
    {
        public double PotableUse { get; set; }
        public double NonPotableUse { get; set; }
        public double RainwaterHarvest { get; set; }
        public double GreywaterRecycle { get; set; }
        public double BlackwaterTreat { get; set; }
        public double Infiltration { get; set; }
        public double Evapotranspiration { get; set; }
    }

    public class RedListCompliance
    {
        public int TotalMaterials { get; set; }
        public int CompliantMaterials { get; set; }
        public int TemporaryExceptions { get; set; }
        public List<RedListMaterial> FlaggedMaterials { get; set; } = new();
        public double CompliancePercentage => TotalMaterials > 0 ?
            CompliantMaterials * 100.0 / TotalMaterials : 0;
    }

    public class RedListMaterial
    {
        public string ProductName { get; set; }
        public string Manufacturer { get; set; }
        public string Category { get; set; }
        public string ChemicalConcern { get; set; }
        public RedListStatus Status { get; set; }
        public string Alternative { get; set; }
    }

    public class BiophiliaAssessment
    {
        public bool HasDirectNatureConnection { get; set; }
        public bool HasIndirectNatureConnection { get; set; }
        public bool HasSpaceSpatialVariation { get; set; }
        public double ViewToNaturePercentage { get; set; }
        public double PlantCoverage { get; set; }
        public double NaturalMaterialPercentage { get; set; }
        public List<string> BiophilicElements { get; set; } = new();
        public int BiophiliaScore { get; set; }
    }

    public class EquityAnalysis
    {
        public bool HasUniversalAccess { get; set; }
        public bool IncludesAffordableUnits { get; set; }
        public double AffordablePercentage { get; set; }
        public bool HasCommunityEngagement { get; set; }
        public bool SupportsLocalEconomy { get; set; }
        public List<string> InclusionMeasures { get; set; } = new();
    }

    #endregion

    public sealed class LivingBuildingIntelligenceEngine
    {
        private static readonly Lazy<LivingBuildingIntelligenceEngine> _instance =
            new Lazy<LivingBuildingIntelligenceEngine>(() => new LivingBuildingIntelligenceEngine());
        public static LivingBuildingIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, LBCProject> _projects = new();
        private readonly object _lock = new object();

        // Red List chemicals to avoid
        private readonly List<string> _redListChemicals = new()
        {
            "Asbestos", "Cadmium", "Chlorinated Polyethylene", "Chloroprene",
            "Chlorosulfonated Polyethylene", "Formaldehyde", "Halogenated Flame Retardants",
            "Lead", "Mercury", "Petrochemical Fertilizers", "Phthalates",
            "PVC", "Wood treatments with creosote, arsenic, or pentachlorophenol"
        };

        private LivingBuildingIntelligenceEngine() { }

        public LBCProject CreateLBCProject(string projectId, string projectName,
            CertificationType certification, TransectZone transect, double grossArea)
        {
            var project = new LBCProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                TargetCertification = certification,
                Transect = transect,
                GrossArea = grossArea,
                RegistrationDate = DateTime.UtcNow
            };

            InitializePetals(project);

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        private void InitializePetals(LBCProject project)
        {
            // Place Petal
            var place = new LBCPetal { Type = Petal.Place, Description = "Restoring a healthy relationship between nature and the built environment" };
            place.Imperatives.AddRange(new[]
            {
                new Imperative { Number = "01", Name = "Ecology of Place", Petal = Petal.Place,
                    Requirements = new() { "Restore native species", "Remove invasive species", "Create habitat corridors" }},
                new Imperative { Number = "02", Name = "Urban Agriculture", Petal = Petal.Place,
                    Requirements = new() { "Dedicate space for food production", "Support community gardens" }},
                new Imperative { Number = "03", Name = "Habitat Exchange", Petal = Petal.Place,
                    Requirements = new() { "Offset habitat impacts", "Support conservation land" }},
                new Imperative { Number = "04", Name = "Human-Scaled Living", Petal = Petal.Place,
                    Requirements = new() { "Promote walkable communities", "Support public transit" }}
            });
            place.TotalImperatives = place.Imperatives.Count;
            project.Petals.Add(place);

            // Water Petal
            var water = new LBCPetal { Type = Petal.Water, Description = "Creating developments that operate within the water balance of the site" };
            water.Imperatives.AddRange(new[]
            {
                new Imperative { Number = "05", Name = "Responsible Water Use", Petal = Petal.Water,
                    Requirements = new() { "Net positive water", "Rainwater as primary source", "Treat all water on site" }},
                new Imperative { Number = "06", Name = "Net Positive Water", Petal = Petal.Water,
                    Requirements = new() { "105% of water needs from captured precipitation", "All stormwater managed on site" }}
            });
            water.TotalImperatives = water.Imperatives.Count;
            project.Petals.Add(water);

            // Energy Petal
            var energy = new LBCPetal { Type = Petal.Energy, Description = "Relying only on current solar income" };
            energy.Imperatives.AddRange(new[]
            {
                new Imperative { Number = "07", Name = "Net Positive Energy", Petal = Petal.Energy,
                    Requirements = new() { "105% of energy from on-site renewables", "No combustion allowed" }},
                new Imperative { Number = "08", Name = "Energy + Carbon Reduction", Petal = Petal.Energy,
                    Requirements = new() { "Meet EUI targets", "Reduce embodied carbon" }}
            });
            energy.TotalImperatives = energy.Imperatives.Count;
            project.Petals.Add(energy);

            // Health + Happiness Petal
            var health = new LBCPetal { Type = Petal.Health, Description = "Creating environments that optimize physical and psychological health" };
            health.Imperatives.AddRange(new[]
            {
                new Imperative { Number = "09", Name = "Civilized Environment", Petal = Petal.Health,
                    Requirements = new() { "Operable windows", "Daylight in all occupied spaces", "Fresh air access" }},
                new Imperative { Number = "10", Name = "Healthy Interior Environment", Petal = Petal.Health,
                    Requirements = new() { "No smoking", "Clean air", "Low-emitting materials" }},
                new Imperative { Number = "11", Name = "Biophilic Environment", Petal = Petal.Health,
                    Requirements = new() { "Direct nature connection", "Natural materials", "Living systems" }}
            });
            health.TotalImperatives = health.Imperatives.Count;
            project.Petals.Add(health);

            // Materials Petal
            var materials = new LBCPetal { Type = Petal.Materials, Description = "Endorsing products that are safe for all species through time" };
            materials.Imperatives.AddRange(new[]
            {
                new Imperative { Number = "12", Name = "Responsible Materials", Petal = Petal.Materials,
                    Requirements = new() { "Red List free", "Declare labels", "Material transparency" }},
                new Imperative { Number = "13", Name = "Net Positive Waste", Petal = Petal.Materials,
                    Requirements = new() { "90% construction waste diversion", "Design for disassembly" }},
                new Imperative { Number = "14", Name = "Living Economy Sourcing", Petal = Petal.Materials,
                    Requirements = new() { "Local sourcing", "Fair labor practices" }}
            });
            materials.TotalImperatives = materials.Imperatives.Count;
            project.Petals.Add(materials);

            // Equity Petal
            var equity = new LBCPetal { Type = Petal.Equity, Description = "Supporting a just, equitable world" };
            equity.Imperatives.AddRange(new[]
            {
                new Imperative { Number = "15", Name = "Human Scale + Humane Places", Petal = Petal.Equity,
                    Requirements = new() { "Universal access", "Inclusive design" }},
                new Imperative { Number = "16", Name = "Universal Access to Nature", Petal = Petal.Equity,
                    Requirements = new() { "Public access to green space", "Democratic spaces" }},
                new Imperative { Number = "17", Name = "Equitable Investment", Petal = Petal.Equity,
                    Requirements = new() { "Support underserved communities", "Affordable housing component" }},
                new Imperative { Number = "18", Name = "Just Organizations", Petal = Petal.Equity,
                    Requirements = new() { "Living wage", "Fair employment practices" }}
            });
            equity.TotalImperatives = equity.Imperatives.Count;
            project.Petals.Add(equity);

            // Beauty Petal
            var beauty = new LBCPetal { Type = Petal.Beauty, Description = "Recognizing the need for beauty as a precursor to caring about a place" };
            beauty.Imperatives.AddRange(new[]
            {
                new Imperative { Number = "19", Name = "Beauty + Biophilia", Petal = Petal.Beauty,
                    Requirements = new() { "Meaningful design", "Connection to place", "Celebration of culture" }},
                new Imperative { Number = "20", Name = "Inspiration + Education", Petal = Petal.Beauty,
                    Requirements = new() { "Educational signage", "Public tours", "Open data sharing" }}
            });
            beauty.TotalImperatives = beauty.Imperatives.Count;
            project.Petals.Add(beauty);
        }

        public void UpdateImperativeStatus(string projectId, string imperativeNumber, ImperativeStatus status,
            double compliancePercentage = 100)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return;

                var imperative = project.Petals
                    .SelectMany(p => p.Imperatives)
                    .FirstOrDefault(i => i.Number == imperativeNumber);

                if (imperative != null)
                {
                    imperative.Status = status;
                    imperative.CompliancePercentage = compliancePercentage;
                    UpdatePetalStatus(project);
                }
            }
        }

        private void UpdatePetalStatus(LBCProject project)
        {
            foreach (var petal in project.Petals)
            {
                petal.CompletedImperatives = petal.Imperatives.Count(i => i.Status == ImperativeStatus.Achieved);
                petal.IsComplete = petal.CompletedImperatives == petal.TotalImperatives;
            }
        }

        public async Task<LBCScorecard> GenerateScorecard(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var scorecard = new LBCScorecard();

                    foreach (var petal in project.Petals)
                    {
                        scorecard.PetalStatuses[petal.Type] = new PetalStatus
                        {
                            Petal = petal.Type,
                            TotalImperatives = petal.TotalImperatives,
                            AchievedImperatives = petal.CompletedImperatives,
                            IsComplete = petal.IsComplete
                        };
                    }

                    scorecard.TotalImperatives = project.Petals.Sum(p => p.TotalImperatives);
                    scorecard.AchievedImperatives = project.Petals.Sum(p => p.CompletedImperatives);
                    scorecard.PendingImperatives = scorecard.TotalImperatives - scorecard.AchievedImperatives;

                    // Check if qualifies for Living Building certification
                    scorecard.QualifiesForLivingBuilding = project.Petals.All(p => p.IsComplete);

                    // Generate path to certification
                    foreach (var petal in project.Petals.Where(p => !p.IsComplete))
                    {
                        var incomplete = petal.Imperatives.Where(i => i.Status != ImperativeStatus.Achieved);
                        foreach (var imp in incomplete)
                        {
                            scorecard.PathToLiving.Add($"{petal.Type}: Complete Imperative {imp.Number} - {imp.Name}");
                        }
                    }

                    project.Scorecard = scorecard;
                    return scorecard;
                }
            });
        }

        public NetPositiveEnergy CalculateNetPositiveEnergy(string projectId, double annualEnergyUse,
            List<(string type, double capacity, double generation)> renewables)
        {
            var npe = new NetPositiveEnergy
            {
                AnnualEnergyUse = annualEnergyUse
            };

            foreach (var (type, capacity, generation) in renewables)
            {
                npe.Renewables.Add(new RenewableSystem
                {
                    Type = type,
                    Capacity = capacity,
                    AnnualGeneration = generation,
                    IsOnSite = true
                });
            }

            npe.OnSiteGeneration = npe.Renewables.Sum(r => r.AnnualGeneration);
            npe.ExcessGeneration = Math.Max(0, npe.OnSiteGeneration * 1.05 - annualEnergyUse);

            lock (_lock)
            {
                if (_projects.TryGetValue(projectId, out var project))
                {
                    npe.EnergyIntensity = annualEnergyUse / project.GrossArea;
                }
            }

            return npe;
        }

        public NetPositiveWater CalculateNetPositiveWater(string projectId, double annualWaterUse,
            double roofArea, double annualRainfall, double greywaterRate, double blackwaterRate)
        {
            var npw = new NetPositiveWater
            {
                AnnualWaterUse = annualWaterUse
            };

            // Rainwater capture (roof area in SF, rainfall in inches)
            npw.RainwaterCapture = roofArea * annualRainfall * 0.623 * 0.85; // 85% efficiency

            // Greywater reuse
            npw.GreywaterReuse = annualWaterUse * greywaterRate * 0.9; // 90% of greywater recoverable

            // Blackwater treatment
            npw.BlackwaterTreatment = annualWaterUse * blackwaterRate * 0.5; // 50% recovery after treatment

            npw.NetWater = npw.RainwaterCapture + npw.GreywaterReuse + npw.BlackwaterTreatment - annualWaterUse;

            npw.Balance = new WaterBalance
            {
                PotableUse = annualWaterUse * (1 - greywaterRate - blackwaterRate),
                NonPotableUse = annualWaterUse * (greywaterRate + blackwaterRate),
                RainwaterHarvest = npw.RainwaterCapture,
                GreywaterRecycle = npw.GreywaterReuse,
                BlackwaterTreat = npw.BlackwaterTreatment
            };

            return npw;
        }

        public RedListCompliance CheckRedListCompliance(List<(string product, string manufacturer, string category)> materials)
        {
            var compliance = new RedListCompliance
            {
                TotalMaterials = materials.Count
            };

            foreach (var (product, manufacturer, category) in materials)
            {
                bool hasRedListChemical = _redListChemicals.Any(c =>
                    product.Contains(c, StringComparison.OrdinalIgnoreCase) ||
                    category.Contains(c, StringComparison.OrdinalIgnoreCase));

                if (hasRedListChemical)
                {
                    compliance.FlaggedMaterials.Add(new RedListMaterial
                    {
                        ProductName = product,
                        Manufacturer = manufacturer,
                        Category = category,
                        ChemicalConcern = _redListChemicals.FirstOrDefault(c =>
                            product.Contains(c, StringComparison.OrdinalIgnoreCase)),
                        Status = RedListStatus.Temporary_Exception,
                        Alternative = "Seek Red List Free alternative"
                    });
                }
                else
                {
                    compliance.CompliantMaterials++;
                }
            }

            compliance.TemporaryExceptions = compliance.FlaggedMaterials.Count;

            return compliance;
        }

        public BiophiliaAssessment AssessBiophilia(bool hasViews, double viewPercentage, double plantCoverage,
            double naturalMaterials, List<string> biophilicElements)
        {
            var assessment = new BiophiliaAssessment
            {
                HasDirectNatureConnection = hasViews || plantCoverage > 0.1,
                HasIndirectNatureConnection = naturalMaterials > 0.2 || biophilicElements.Any(),
                ViewToNaturePercentage = viewPercentage,
                PlantCoverage = plantCoverage,
                NaturalMaterialPercentage = naturalMaterials,
                BiophilicElements = biophilicElements
            };

            // Calculate biophilia score
            int score = 0;
            if (assessment.HasDirectNatureConnection) score += 25;
            if (assessment.HasIndirectNatureConnection) score += 25;
            if (viewPercentage >= 75) score += 20;
            else if (viewPercentage >= 50) score += 10;
            if (plantCoverage >= 0.2) score += 15;
            if (naturalMaterials >= 0.3) score += 15;

            assessment.BiophiliaScore = score;
            assessment.HasSpaceSpatialVariation = biophilicElements.Count >= 5;

            return assessment;
        }

        public CertificationType DetermineBestCertificationPath(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return CertificationType.CoreGreenBuilding;

                var scorecard = project.Scorecard;
                if (scorecard == null)
                    return CertificationType.CoreGreenBuilding;

                // Check for full Living Building
                if (scorecard.QualifiesForLivingBuilding)
                    return CertificationType.LivingBuilding;

                // Check for Energy Petal only
                var energyStatus = scorecard.PetalStatuses.GetValueOrDefault(Petal.Energy);
                if (energyStatus?.IsComplete == true)
                    return CertificationType.Zero_Energy;

                // Check for 3+ completed petals
                int completePetals = scorecard.PetalStatuses.Values.Count(p => p.IsComplete);
                if (completePetals >= 3)
                    return CertificationType.PetalCertification;

                return CertificationType.CoreGreenBuilding;
            }
        }
    }
}
