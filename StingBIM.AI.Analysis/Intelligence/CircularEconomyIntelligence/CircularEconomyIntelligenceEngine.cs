// ===================================================================
// StingBIM Circular Economy Intelligence Engine
// Material passports, lifecycle tracking, reuse optimization, waste reduction
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.CircularEconomyIntelligence
{
    #region Enums

    public enum MaterialLifecycleStage { Extraction, Manufacturing, Construction, Use, EndOfLife }
    public enum DisassemblyLevel { NonDisassemblable, Destructive, PartialReuse, FullReuse }
    public enum RecyclingCategory { NotRecyclable, Downcycled, Recycled, Upcycled, Reusable }
    public enum ToxicityLevel { None, Low, Medium, High, Hazardous }
    public enum CircularStrategy { Refuse, Reduce, Reuse, Repair, Refurbish, Remanufacture, Repurpose, Recycle, Recover }
    public enum CertificationType { C2C, EPD, Declare, HPD, LivingProduct, RedList }

    #endregion

    #region Data Models

    public class MaterialPassport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MaterialId { get; set; }
        public string MaterialName { get; set; }
        public string Manufacturer { get; set; }
        public string ProductCode { get; set; }
        public Composition Composition { get; set; }
        public List<LifecycleData> Lifecycle { get; set; } = new();
        public HealthProfile Health { get; set; }
        public CircularityMetrics Circularity { get; set; }
        public List<Certification> Certifications { get; set; } = new();
        public DisassemblyInfo Disassembly { get; set; }
        public EndOfLifeOptions EndOfLife { get; set; }
        public LocationHistory LocationHistory { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Composition
    {
        public List<MaterialComponent> Components { get; set; } = new();
        public double RecycledContent { get; set; }
        public double RenewableContent { get; set; }
        public double BiogenicContent { get; set; }
        public string PrimaryMaterial { get; set; }
        public double TotalWeight { get; set; }
    }

    public class MaterialComponent
    {
        public string Name { get; set; }
        public double Percentage { get; set; }
        public string Origin { get; set; }
        public bool IsRecycled { get; set; }
        public bool IsRenewable { get; set; }
        public bool IsBiogenic { get; set; }
        public ToxicityLevel Toxicity { get; set; }
        public bool IsRedListFree { get; set; }
    }

    public class LifecycleData
    {
        public MaterialLifecycleStage Stage { get; set; }
        public double CarbonFootprint { get; set; }
        public double EnergyConsumption { get; set; }
        public double WaterConsumption { get; set; }
        public double WasteGenerated { get; set; }
        public string Location { get; set; }
        public DateTime Date { get; set; }
        public string Notes { get; set; }
    }

    public class HealthProfile
    {
        public ToxicityLevel OverallToxicity { get; set; }
        public bool ContainsVOCs { get; set; }
        public double VOCEmissions { get; set; }
        public bool ContainsFormaldehyde { get; set; }
        public bool ContainsHeavyMetals { get; set; }
        public bool IsRedListFree { get; set; }
        public List<string> HazardousSubstances { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string SafetyDataSheet { get; set; }
    }

    public class CircularityMetrics
    {
        public double CircularityIndex { get; set; }
        public double MaterialUtilization { get; set; }
        public double LifespanUtilization { get; set; }
        public double RecyclabilityScore { get; set; }
        public double ReusePotential { get; set; }
        public double WasteScore { get; set; }
        public double LinearFlowIndex { get; set; }
        public RecyclingCategory Category { get; set; }
        public List<CircularStrategy> ApplicableStrategies { get; set; } = new();
    }

    public class Certification
    {
        public CertificationType Type { get; set; }
        public string CertificationId { get; set; }
        public string Level { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string CertifyingBody { get; set; }
        public string DocumentUrl { get; set; }
        public bool IsValid => ExpiryDate > DateTime.UtcNow;
    }

    public class DisassemblyInfo
    {
        public DisassemblyLevel Level { get; set; }
        public int EstimatedTime { get; set; } // Minutes
        public List<string> ToolsRequired { get; set; } = new();
        public List<string> Instructions { get; set; } = new();
        public double LaborCost { get; set; }
        public double RecoveryRate { get; set; }
        public List<RecoverableComponent> Components { get; set; } = new();
    }

    public class RecoverableComponent
    {
        public string Name { get; set; }
        public double Weight { get; set; }
        public double RecoveryRate { get; set; }
        public RecyclingCategory Category { get; set; }
        public double EstimatedValue { get; set; }
        public List<string> PotentialBuyers { get; set; } = new();
    }

    public class EndOfLifeOptions
    {
        public List<EndOfLifeScenario> Scenarios { get; set; } = new();
        public string RecommendedScenario { get; set; }
        public double TakeBackValue { get; set; }
        public bool HasTakeBackProgram { get; set; }
        public string DisposalInstructions { get; set; }
    }

    public class EndOfLifeScenario
    {
        public string Name { get; set; }
        public CircularStrategy Strategy { get; set; }
        public double RecoveryRate { get; set; }
        public double EnvironmentalBenefit { get; set; }
        public double EconomicValue { get; set; }
        public double ProcessingCost { get; set; }
        public string ProcessDescription { get; set; }
        public List<string> OutputProducts { get; set; } = new();
    }

    public class LocationHistory
    {
        public string CurrentLocation { get; set; }
        public string CurrentProject { get; set; }
        public string CurrentBuilding { get; set; }
        public string CurrentElement { get; set; }
        public DateTime InstallationDate { get; set; }
        public List<LocationEntry> History { get; set; } = new();
    }

    public class LocationEntry
    {
        public string Location { get; set; }
        public string Project { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Reason { get; set; }
    }

    public class BuildingMaterialInventory
    {
        public string BuildingId { get; set; }
        public string BuildingName { get; set; }
        public List<MaterialPassport> Materials { get; set; } = new();
        public double TotalWeight { get; set; }
        public double TotalCarbonFootprint { get; set; }
        public double AverageCircularity { get; set; }
        public double RecycledContentPercent { get; set; }
        public double EndOfLifeValue { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class CircularityAssessment
    {
        public string AssessmentId { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public double OverallCircularityScore { get; set; }
        public double MaterialHealthScore { get; set; }
        public double MaterialReuseScore { get; set; }
        public double RenewableEnergyScore { get; set; }
        public double WaterStewardshipScore { get; set; }
        public double SocialFairnessScore { get; set; }
        public List<string> Strengths { get; set; } = new();
        public List<string> Improvements { get; set; } = new();
        public List<MaterialRecommendation> Recommendations { get; set; } = new();
    }

    public class MaterialRecommendation
    {
        public string CurrentMaterial { get; set; }
        public string RecommendedMaterial { get; set; }
        public string Reason { get; set; }
        public double CircularityImprovement { get; set; }
        public double CostDifference { get; set; }
        public double CarbonSaving { get; set; }
    }

    public class WasteStream
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string WasteType { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public CircularStrategy Destination { get; set; }
        public double DiversionRate { get; set; }
        public double Value { get; set; }
        public double ProcessingCost { get; set; }
        public string Handler { get; set; }
        public DateTime GeneratedDate { get; set; }
    }

    public class MaterialMarketplace
    {
        public List<MaterialListing> Listings { get; set; } = new();
        public List<MaterialRequest> Requests { get; set; } = new();
    }

    public class MaterialListing
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PassportId { get; set; }
        public string MaterialName { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public string Condition { get; set; }
        public string Location { get; set; }
        public double AskingPrice { get; set; }
        public DateTime AvailableFrom { get; set; }
        public DateTime ListingExpiry { get; set; }
        public string SellerContact { get; set; }
        public bool IsActive { get; set; }
    }

    public class MaterialRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MaterialType { get; set; }
        public double QuantityNeeded { get; set; }
        public string Unit { get; set; }
        public string Location { get; set; }
        public double MaxPrice { get; set; }
        public DateTime NeededBy { get; set; }
        public string BuyerContact { get; set; }
        public List<string> AcceptedConditions { get; set; } = new();
    }

    #endregion

    public sealed class CircularEconomyIntelligenceEngine
    {
        private static readonly Lazy<CircularEconomyIntelligenceEngine> _instance =
            new Lazy<CircularEconomyIntelligenceEngine>(() => new CircularEconomyIntelligenceEngine());
        public static CircularEconomyIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, MaterialPassport> _passports = new();
        private readonly Dictionary<string, BuildingMaterialInventory> _inventories = new();
        private readonly List<WasteStream> _wasteStreams = new();
        private readonly MaterialMarketplace _marketplace = new();
        private readonly object _lock = new object();

        // Circularity scoring weights
        private readonly Dictionary<string, double> _circularityWeights = new()
        {
            ["RecycledContent"] = 0.2,
            ["Recyclability"] = 0.2,
            ["Disassembly"] = 0.15,
            ["Lifespan"] = 0.15,
            ["TakeBack"] = 0.1,
            ["Toxicity"] = 0.1,
            ["RenewableContent"] = 0.1
        };

        private CircularEconomyIntelligenceEngine() { }

        public MaterialPassport CreatePassport(string materialId, string name, string manufacturer,
            Composition composition, HealthProfile health)
        {
            var passport = new MaterialPassport
            {
                MaterialId = materialId,
                MaterialName = name,
                Manufacturer = manufacturer,
                Composition = composition,
                Health = health,
                Circularity = CalculateCircularityMetrics(composition, health),
                EndOfLife = GenerateEndOfLifeOptions(composition)
            };

            lock (_lock) { _passports[passport.Id] = passport; }
            return passport;
        }

        private CircularityMetrics CalculateCircularityMetrics(Composition composition, HealthProfile health)
        {
            var metrics = new CircularityMetrics();

            // Material utilization (recycled + renewable content)
            metrics.MaterialUtilization = composition.RecycledContent + composition.RenewableContent;

            // Recyclability based on composition complexity
            int componentCount = composition.Components.Count;
            double mixedMaterials = composition.Components.Count(c => !c.IsRecycled && !c.IsRenewable);
            metrics.RecyclabilityScore = Math.Max(0, 100 - (mixedMaterials * 15) - (componentCount > 5 ? 20 : 0));

            // Reuse potential
            bool hasHazardous = composition.Components.Any(c => c.Toxicity >= ToxicityLevel.High);
            metrics.ReusePotential = hasHazardous ? 30 : 80;

            // Waste score (inverse)
            metrics.WasteScore = 100 - metrics.RecyclabilityScore;

            // Linear flow
            metrics.LinearFlowIndex = 100 - metrics.MaterialUtilization;

            // Lifespan utilization (assume standard)
            metrics.LifespanUtilization = 0.85;

            // Calculate overall circularity index
            metrics.CircularityIndex = (
                metrics.MaterialUtilization * _circularityWeights["RecycledContent"] +
                metrics.RecyclabilityScore * _circularityWeights["Recyclability"] +
                metrics.ReusePotential * _circularityWeights["Disassembly"] +
                metrics.LifespanUtilization * 100 * _circularityWeights["Lifespan"] +
                (health.IsRedListFree ? 100 : 50) * _circularityWeights["Toxicity"] +
                composition.RenewableContent * _circularityWeights["RenewableContent"]
            );

            // Determine category
            metrics.Category = metrics.CircularityIndex switch
            {
                >= 80 => RecyclingCategory.Reusable,
                >= 60 => RecyclingCategory.Upcycled,
                >= 40 => RecyclingCategory.Recycled,
                >= 20 => RecyclingCategory.Downcycled,
                _ => RecyclingCategory.NotRecyclable
            };

            // Applicable strategies
            metrics.ApplicableStrategies = DetermineStrategies(metrics);

            return metrics;
        }

        private List<CircularStrategy> DetermineStrategies(CircularityMetrics metrics)
        {
            var strategies = new List<CircularStrategy>();

            if (metrics.ReusePotential >= 70)
                strategies.Add(CircularStrategy.Reuse);

            if (metrics.RecyclabilityScore >= 60)
            {
                strategies.Add(CircularStrategy.Recycle);
                if (metrics.RecyclabilityScore >= 80)
                    strategies.Add(CircularStrategy.Remanufacture);
            }

            if (metrics.LifespanUtilization < 0.5)
                strategies.Add(CircularStrategy.Repair);

            strategies.Add(CircularStrategy.Repurpose);

            if (metrics.Category == RecyclingCategory.NotRecyclable)
                strategies.Add(CircularStrategy.Recover);

            return strategies;
        }

        private EndOfLifeOptions GenerateEndOfLifeOptions(Composition composition)
        {
            var options = new EndOfLifeOptions
            {
                Scenarios = new List<EndOfLifeScenario>()
            };

            // Direct reuse scenario
            options.Scenarios.Add(new EndOfLifeScenario
            {
                Name = "Direct Reuse",
                Strategy = CircularStrategy.Reuse,
                RecoveryRate = 0.95,
                EnvironmentalBenefit = 0.9,
                EconomicValue = composition.TotalWeight * 0.5,
                ProcessingCost = composition.TotalWeight * 0.1,
                ProcessDescription = "Careful disassembly and direct reinstallation",
                OutputProducts = new List<string> { "Reused component" }
            });

            // Recycling scenario
            double recyclablePercent = composition.Components.Where(c => c.IsRecycled || c.Toxicity <= ToxicityLevel.Low).Sum(c => c.Percentage) / 100;
            options.Scenarios.Add(new EndOfLifeScenario
            {
                Name = "Material Recycling",
                Strategy = CircularStrategy.Recycle,
                RecoveryRate = recyclablePercent * 0.85,
                EnvironmentalBenefit = 0.6,
                EconomicValue = composition.TotalWeight * 0.15 * recyclablePercent,
                ProcessingCost = composition.TotalWeight * 0.2,
                ProcessDescription = "Separation and material recycling",
                OutputProducts = composition.Components.Where(c => c.Toxicity <= ToxicityLevel.Low).Select(c => $"Recycled {c.Name}").ToList()
            });

            // Energy recovery
            options.Scenarios.Add(new EndOfLifeScenario
            {
                Name = "Energy Recovery",
                Strategy = CircularStrategy.Recover,
                RecoveryRate = 0.3,
                EnvironmentalBenefit = 0.2,
                EconomicValue = composition.TotalWeight * 0.02,
                ProcessingCost = composition.TotalWeight * 0.08,
                ProcessDescription = "Incineration with energy capture",
                OutputProducts = new List<string> { "Energy", "Ash" }
            });

            // Select recommendation
            options.RecommendedScenario = options.Scenarios.OrderByDescending(s => s.EnvironmentalBenefit).First().Name;

            return options;
        }

        public void AddDisassemblyInfo(string passportId, DisassemblyLevel level, int estimatedTime,
            List<string> tools, List<RecoverableComponent> components)
        {
            lock (_lock)
            {
                if (_passports.TryGetValue(passportId, out var passport))
                {
                    passport.Disassembly = new DisassemblyInfo
                    {
                        Level = level,
                        EstimatedTime = estimatedTime,
                        ToolsRequired = tools,
                        Components = components,
                        RecoveryRate = components.Average(c => c.RecoveryRate),
                        LaborCost = estimatedTime * 0.75 // Per minute labor rate
                    };

                    // Update circularity based on disassembly
                    double disassemblyBonus = level switch
                    {
                        DisassemblyLevel.FullReuse => 20,
                        DisassemblyLevel.PartialReuse => 10,
                        DisassemblyLevel.Destructive => 5,
                        _ => 0
                    };
                    passport.Circularity.CircularityIndex = Math.Min(100, passport.Circularity.CircularityIndex + disassemblyBonus);
                    passport.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        public void AddCertification(string passportId, CertificationType type, string certId,
            string level, DateTime issue, DateTime expiry, string body)
        {
            lock (_lock)
            {
                if (_passports.TryGetValue(passportId, out var passport))
                {
                    passport.Certifications.Add(new Certification
                    {
                        Type = type,
                        CertificationId = certId,
                        Level = level,
                        IssueDate = issue,
                        ExpiryDate = expiry,
                        CertifyingBody = body
                    });
                    passport.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        public void TrackLocation(string passportId, string location, string project,
            string building, string element)
        {
            lock (_lock)
            {
                if (_passports.TryGetValue(passportId, out var passport))
                {
                    if (passport.LocationHistory == null)
                    {
                        passport.LocationHistory = new LocationHistory { History = new List<LocationEntry>() };
                    }

                    // Archive current location
                    if (!string.IsNullOrEmpty(passport.LocationHistory.CurrentLocation))
                    {
                        passport.LocationHistory.History.Add(new LocationEntry
                        {
                            Location = passport.LocationHistory.CurrentLocation,
                            Project = passport.LocationHistory.CurrentProject,
                            FromDate = passport.LocationHistory.InstallationDate,
                            ToDate = DateTime.UtcNow,
                            Reason = "Relocated"
                        });
                    }

                    // Update current
                    passport.LocationHistory.CurrentLocation = location;
                    passport.LocationHistory.CurrentProject = project;
                    passport.LocationHistory.CurrentBuilding = building;
                    passport.LocationHistory.CurrentElement = element;
                    passport.LocationHistory.InstallationDate = DateTime.UtcNow;
                    passport.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        public BuildingMaterialInventory CreateInventory(string buildingId, string buildingName,
            List<string> passportIds)
        {
            lock (_lock)
            {
                var materials = passportIds
                    .Where(id => _passports.ContainsKey(id))
                    .Select(id => _passports[id])
                    .ToList();

                var inventory = new BuildingMaterialInventory
                {
                    BuildingId = buildingId,
                    BuildingName = buildingName,
                    Materials = materials,
                    TotalWeight = materials.Sum(m => m.Composition?.TotalWeight ?? 0),
                    TotalCarbonFootprint = materials.Sum(m => m.Lifecycle.Sum(l => l.CarbonFootprint)),
                    AverageCircularity = materials.Any() ? materials.Average(m => m.Circularity?.CircularityIndex ?? 0) : 0,
                    RecycledContentPercent = materials.Any() ? materials.Average(m => m.Composition?.RecycledContent ?? 0) : 0,
                    EndOfLifeValue = materials.Sum(m => m.EndOfLife?.TakeBackValue ?? 0),
                    LastUpdated = DateTime.UtcNow
                };

                _inventories[buildingId] = inventory;
                return inventory;
            }
        }

        public async Task<CircularityAssessment> AssessProjectCircularity(string projectId,
            List<string> passportIds)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    var materials = passportIds
                        .Where(id => _passports.ContainsKey(id))
                        .Select(id => _passports[id])
                        .ToList();

                    if (!materials.Any())
                        return null;

                    var assessment = new CircularityAssessment
                    {
                        ProjectId = projectId,
                        OverallCircularityScore = materials.Average(m => m.Circularity?.CircularityIndex ?? 0),
                        MaterialHealthScore = CalculateHealthScore(materials),
                        MaterialReuseScore = materials.Average(m => m.Circularity?.ReusePotential ?? 0),
                        Strengths = new List<string>(),
                        Improvements = new List<string>(),
                        Recommendations = new List<MaterialRecommendation>()
                    };

                    // Identify strengths
                    if (assessment.OverallCircularityScore >= 70)
                        assessment.Strengths.Add("High overall circularity");
                    if (materials.Average(m => m.Composition?.RecycledContent ?? 0) >= 50)
                        assessment.Strengths.Add("Strong recycled content utilization");
                    if (materials.All(m => m.Health?.IsRedListFree == true))
                        assessment.Strengths.Add("All materials Red List free");
                    if (materials.Average(m => m.Disassembly?.RecoveryRate ?? 0) >= 0.8)
                        assessment.Strengths.Add("High disassembly recovery rates");

                    // Identify improvements
                    var lowCircularity = materials.Where(m => m.Circularity?.CircularityIndex < 40).ToList();
                    if (lowCircularity.Any())
                    {
                        assessment.Improvements.Add($"{lowCircularity.Count} materials have low circularity scores");
                        foreach (var mat in lowCircularity.Take(3))
                        {
                            assessment.Recommendations.Add(new MaterialRecommendation
                            {
                                CurrentMaterial = mat.MaterialName,
                                RecommendedMaterial = $"Higher circularity alternative for {mat.MaterialName}",
                                Reason = "Low circularity score",
                                CircularityImprovement = 40 - (mat.Circularity?.CircularityIndex ?? 0),
                                CostDifference = 0.05,
                                CarbonSaving = 0.3
                            });
                        }
                    }

                    var hazardous = materials.Where(m => m.Health?.OverallToxicity >= ToxicityLevel.High).ToList();
                    if (hazardous.Any())
                    {
                        assessment.Improvements.Add($"{hazardous.Count} materials contain hazardous substances");
                    }

                    var noCert = materials.Where(m => !m.Certifications.Any()).ToList();
                    if (noCert.Count > materials.Count * 0.3)
                    {
                        assessment.Improvements.Add("Many materials lack environmental certifications");
                    }

                    return assessment;
                }
            });
        }

        private double CalculateHealthScore(List<MaterialPassport> materials)
        {
            double score = 100;

            int hazardCount = materials.Count(m => m.Health?.OverallToxicity >= ToxicityLevel.High);
            score -= hazardCount * 10;

            int vocCount = materials.Count(m => m.Health?.ContainsVOCs == true);
            score -= vocCount * 5;

            int notRedListFree = materials.Count(m => m.Health?.IsRedListFree != true);
            score -= notRedListFree * 3;

            return Math.Max(0, score);
        }

        public WasteStream RecordWasteStream(string projectId, string wasteType, double quantity,
            string unit, CircularStrategy destination)
        {
            var stream = new WasteStream
            {
                ProjectId = projectId,
                WasteType = wasteType,
                Quantity = quantity,
                Unit = unit,
                Destination = destination,
                GeneratedDate = DateTime.UtcNow,
                DiversionRate = destination switch
                {
                    CircularStrategy.Reuse => 1.0,
                    CircularStrategy.Recycle => 0.9,
                    CircularStrategy.Repurpose => 0.85,
                    CircularStrategy.Recover => 0.3,
                    _ => 0
                }
            };

            lock (_lock) { _wasteStreams.Add(stream); }
            return stream;
        }

        public double CalculateProjectDiversionRate(string projectId)
        {
            lock (_lock)
            {
                var projectWaste = _wasteStreams.Where(w => w.ProjectId == projectId).ToList();
                if (!projectWaste.Any()) return 0;

                double totalWaste = projectWaste.Sum(w => w.Quantity);
                double divertedWaste = projectWaste.Sum(w => w.Quantity * w.DiversionRate);

                return divertedWaste / totalWaste * 100;
            }
        }

        public MaterialListing ListMaterialForReuse(string passportId, double quantity, string unit,
            string condition, double askingPrice, DateTime availableFrom)
        {
            lock (_lock)
            {
                if (!_passports.TryGetValue(passportId, out var passport))
                    return null;

                var listing = new MaterialListing
                {
                    PassportId = passportId,
                    MaterialName = passport.MaterialName,
                    Quantity = quantity,
                    Unit = unit,
                    Condition = condition,
                    Location = passport.LocationHistory?.CurrentLocation ?? "Unknown",
                    AskingPrice = askingPrice,
                    AvailableFrom = availableFrom,
                    ListingExpiry = availableFrom.AddMonths(3),
                    IsActive = true
                };

                _marketplace.Listings.Add(listing);
                return listing;
            }
        }

        public MaterialRequest CreateMaterialRequest(string materialType, double quantity, string unit,
            string location, double maxPrice, DateTime neededBy)
        {
            var request = new MaterialRequest
            {
                MaterialType = materialType,
                QuantityNeeded = quantity,
                Unit = unit,
                Location = location,
                MaxPrice = maxPrice,
                NeededBy = neededBy,
                AcceptedConditions = new List<string> { "Good", "Excellent", "Like New" }
            };

            lock (_lock) { _marketplace.Requests.Add(request); }
            return request;
        }

        public List<MaterialListing> FindMatchingMaterials(MaterialRequest request)
        {
            lock (_lock)
            {
                return _marketplace.Listings
                    .Where(l => l.IsActive &&
                        l.MaterialName.Contains(request.MaterialType, StringComparison.OrdinalIgnoreCase) &&
                        l.Quantity >= request.QuantityNeeded &&
                        l.AskingPrice <= request.MaxPrice &&
                        l.AvailableFrom <= request.NeededBy &&
                        request.AcceptedConditions.Contains(l.Condition))
                    .OrderBy(l => l.AskingPrice)
                    .ToList();
            }
        }

        public double EstimateEndOfLifeValue(string passportId)
        {
            lock (_lock)
            {
                if (!_passports.TryGetValue(passportId, out var passport))
                    return 0;

                var recommended = passport.EndOfLife?.Scenarios
                    .OrderByDescending(s => s.EconomicValue - s.ProcessingCost)
                    .FirstOrDefault();

                if (recommended == null) return 0;

                return recommended.EconomicValue - recommended.ProcessingCost;
            }
        }

        public double CalculateCarbonSavings(string passportId)
        {
            lock (_lock)
            {
                if (!_passports.TryGetValue(passportId, out var passport))
                    return 0;

                double embodiedCarbon = passport.Lifecycle
                    .Where(l => l.Stage == MaterialLifecycleStage.Manufacturing)
                    .Sum(l => l.CarbonFootprint);

                double reuseRate = passport.Circularity?.ReusePotential / 100 ?? 0.5;

                return embodiedCarbon * reuseRate;
            }
        }
    }
}
