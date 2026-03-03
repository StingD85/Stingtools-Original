// ============================================================================
// StingBIM AI - Facility Management Sustainability & Carbon Tracking
// Environmental impact monitoring, carbon accounting, and sustainability reporting
// Aligned with GHG Protocol, ISO 14064, and GRESB frameworks
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.FacilityManagement.Intelligence
{
    #region Sustainability Models

    /// <summary>
    /// Building carbon footprint
    /// </summary>
    public class CarbonFootprint
    {
        public string BuildingId { get; set; } = string.Empty;
        public int Year { get; set; }
        public string ReportingPeriod { get; set; } = string.Empty;

        // Scope 1 - Direct emissions
        public double Scope1Total { get; set; } // tCO2e
        public Dictionary<string, double> Scope1BySource { get; set; } = new();

        // Scope 2 - Indirect (purchased electricity, steam, etc.)
        public double Scope2LocationBased { get; set; } // tCO2e
        public double Scope2MarketBased { get; set; }
        public Dictionary<string, double> Scope2BySource { get; set; } = new();

        // Scope 3 - Other indirect (optional for buildings)
        public double Scope3Total { get; set; }
        public Dictionary<string, double> Scope3ByCategory { get; set; } = new();

        // Totals
        public double TotalEmissions { get; set; }
        public double IntensityPerM2 { get; set; }
        public double IntensityPerOccupant { get; set; }

        // Targets and progress
        public double BaselineEmissions { get; set; }
        public double TargetEmissions { get; set; }
        public double ReductionFromBaseline { get; set; }
        public double ReductionPercent { get; set; }
        public bool OnTrackForTarget { get; set; }

        // Offsets
        public double CarbonOffsets { get; set; }
        public double NetEmissions { get; set; }
    }

    /// <summary>
    /// Emission factor for carbon calculation
    /// </summary>
    public class EmissionFactor
    {
        public string FactorId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty; // Electricity, Diesel, Natural Gas
        public string Region { get; set; } = string.Empty;
        public double Factor { get; set; } // kgCO2e per unit
        public string Unit { get; set; } = string.Empty; // kWh, liter, m³
        public int Year { get; set; }
        public string DataSource { get; set; } = string.Empty;
    }

    /// <summary>
    /// Sustainability initiative
    /// </summary>
    public class SustainabilityInitiative
    {
        public string InitiativeId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SustainabilityCategory Category { get; set; }
        public InitiativeStatus Status { get; set; }

        // Timeline
        public DateTime StartDate { get; set; }
        public DateTime? CompletionDate { get; set; }

        // Impact
        public double EstimatedCarbonReduction { get; set; } // tCO2e/year
        public double ActualCarbonReduction { get; set; }
        public double EstimatedEnergySavings { get; set; } // kWh/year
        public double ActualEnergySavings { get; set; }
        public double EstimatedWaterSavings { get; set; } // m³/year
        public double ActualWaterSavings { get; set; }

        // Financials
        public decimal InvestmentCost { get; set; }
        public decimal AnnualSavings { get; set; }
        public double PaybackYears { get; set; }

        // Verification
        public bool IsVerified { get; set; }
        public string VerificationMethod { get; set; } = string.Empty;
    }

    public enum SustainabilityCategory
    {
        EnergyEfficiency,
        RenewableEnergy,
        WaterConservation,
        WasteReduction,
        GreenProcurement,
        TransportEmissions,
        RefrigerantManagement,
        CarbonOffset
    }

    public enum InitiativeStatus
    {
        Planned,
        InProgress,
        Completed,
        OnHold,
        Cancelled
    }

    /// <summary>
    /// Water consumption tracking
    /// </summary>
    public class WaterConsumption
    {
        public string BuildingId { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }

        public double TotalConsumptionM3 { get; set; }
        public double DomesticUseM3 { get; set; }
        public double IrrigationM3 { get; set; }
        public double CoolingTowerM3 { get; set; }

        public double IntensityPerM2 { get; set; }
        public double IntensityPerOccupant { get; set; }

        public double RecycledWaterM3 { get; set; }
        public double RainwaterHarvestedM3 { get; set; }
        public double RecyclingPercent { get; set; }

        public double BenchmarkIntensity { get; set; }
        public double PerformanceVsBenchmark { get; set; }
    }

    /// <summary>
    /// Waste management tracking
    /// </summary>
    public class WasteManagement
    {
        public string BuildingId { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }

        public double TotalWasteKg { get; set; }
        public double LandfillKg { get; set; }
        public double RecycledKg { get; set; }
        public double CompostedKg { get; set; }
        public double HazardousKg { get; set; }

        public double DiversionRate { get; set; } // % diverted from landfill
        public double RecyclingRate { get; set; }

        public double IntensityPerM2 { get; set; }
        public double IntensityPerOccupant { get; set; }
    }

    /// <summary>
    /// Sustainability certification tracking
    /// </summary>
    public class SustainabilityCertification
    {
        public string CertificationId { get; set; } = string.Empty;
        public string BuildingId { get; set; } = string.Empty;
        public string CertificationType { get; set; } = string.Empty; // LEED, BREEAM, Green Mark, EDGE
        public string Level { get; set; } = string.Empty; // Certified, Silver, Gold, Platinum
        public int Score { get; set; }
        public DateTime CertificationDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public Dictionary<string, int> CategoryScores { get; set; } = new();
        public List<string> Prerequisites { get; set; } = new();
        public List<string> CreditsAchieved { get; set; } = new();
    }

    /// <summary>
    /// ESG (Environmental, Social, Governance) score
    /// </summary>
    public class ESGScore
    {
        public string BuildingId { get; set; } = string.Empty;
        public int Year { get; set; }

        // Environmental
        public double EnvironmentalScore { get; set; } // 0-100
        public double EnergyScore { get; set; }
        public double WaterScore { get; set; }
        public double WasteScore { get; set; }
        public double CarbonScore { get; set; }

        // Social
        public double SocialScore { get; set; }
        public double HealthSafetyScore { get; set; }
        public double OccupantWellbeingScore { get; set; }
        public double CommunityEngagementScore { get; set; }

        // Governance
        public double GovernanceScore { get; set; }
        public double ComplianceScore { get; set; }
        public double TransparencyScore { get; set; }

        // Overall
        public double OverallESGScore { get; set; }
        public string ESGRating { get; set; } = string.Empty; // A, B, C, D
        public int GRESBScore { get; set; } // If applicable
    }

    #endregion

    #region Sustainability Intelligence Engine

    /// <summary>
    /// FM Sustainability Intelligence Engine
    /// Tracks environmental impact and manages sustainability initiatives
    /// </summary>
    public class FMSustainability
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Data storage
        private readonly Dictionary<string, CarbonFootprint> _carbonData = new();
        private readonly List<SustainabilityInitiative> _initiatives = new();
        private readonly Dictionary<string, List<WaterConsumption>> _waterData = new();
        private readonly Dictionary<string, List<WasteManagement>> _wasteData = new();
        private readonly Dictionary<string, SustainabilityCertification> _certifications = new();

        // Emission factors (East Africa specific)
        private readonly Dictionary<string, EmissionFactor> _emissionFactors = new();

        public FMSustainability()
        {
            InitializeEmissionFactors();
            InitializeSampleData();
            Logger.Info("FM Sustainability Intelligence Engine initialized");
        }

        #region Initialization

        private void InitializeEmissionFactors()
        {
            // East Africa grid emission factors
            _emissionFactors["Electricity-UG"] = new EmissionFactor
            {
                FactorId = "EF-UG-ELEC",
                Source = "Electricity",
                Region = "Uganda",
                Factor = 0.12, // kgCO2e/kWh (largely hydro)
                Unit = "kWh",
                Year = 2024,
                DataSource = "Uganda Electricity Regulatory Authority"
            };

            _emissionFactors["Electricity-KE"] = new EmissionFactor
            {
                FactorId = "EF-KE-ELEC",
                Source = "Electricity",
                Region = "Kenya",
                Factor = 0.35, // kgCO2e/kWh (geothermal + thermal mix)
                Unit = "kWh",
                Year = 2024,
                DataSource = "Kenya Power"
            };

            _emissionFactors["Diesel"] = new EmissionFactor
            {
                FactorId = "EF-DIESEL",
                Source = "Diesel",
                Region = "Global",
                Factor = 2.68, // kgCO2e/liter
                Unit = "liter",
                Year = 2024,
                DataSource = "GHG Protocol"
            };

            _emissionFactors["LPG"] = new EmissionFactor
            {
                FactorId = "EF-LPG",
                Source = "LPG",
                Region = "Global",
                Factor = 1.51, // kgCO2e/kg
                Unit = "kg",
                Year = 2024,
                DataSource = "GHG Protocol"
            };

            _emissionFactors["Refrigerant-R410A"] = new EmissionFactor
            {
                FactorId = "EF-R410A",
                Source = "Refrigerant",
                Region = "Global",
                Factor = 2088, // kgCO2e/kg (GWP)
                Unit = "kg",
                Year = 2024,
                DataSource = "IPCC AR5"
            };
        }

        private void InitializeSampleData()
        {
            // Sample carbon footprint
            var footprint = new CarbonFootprint
            {
                BuildingId = "BLD-001",
                Year = 2025,
                ReportingPeriod = "Annual",
                Scope1BySource = new Dictionary<string, double>
                {
                    ["Generator Diesel"] = 15.5,
                    ["Company Vehicles"] = 8.2,
                    ["Refrigerant Leakage"] = 2.1
                },
                Scope2BySource = new Dictionary<string, double>
                {
                    ["Purchased Electricity"] = 108.0
                },
                Scope3ByCategory = new Dictionary<string, double>
                {
                    ["Employee Commuting"] = 45.0,
                    ["Waste Disposal"] = 5.5,
                    ["Business Travel"] = 12.0
                },
                BaselineEmissions = 200,
                TargetEmissions = 150
            };

            footprint.Scope1Total = footprint.Scope1BySource.Values.Sum();
            footprint.Scope2LocationBased = footprint.Scope2BySource.Values.Sum();
            footprint.Scope2MarketBased = footprint.Scope2LocationBased;
            footprint.Scope3Total = footprint.Scope3ByCategory.Values.Sum();
            footprint.TotalEmissions = footprint.Scope1Total + footprint.Scope2LocationBased + footprint.Scope3Total;
            footprint.IntensityPerM2 = footprint.TotalEmissions / 5000 * 1000; // kg/m²
            footprint.ReductionFromBaseline = footprint.BaselineEmissions - footprint.TotalEmissions;
            footprint.ReductionPercent = footprint.ReductionFromBaseline / footprint.BaselineEmissions * 100;
            footprint.OnTrackForTarget = footprint.TotalEmissions <= footprint.TargetEmissions;
            footprint.NetEmissions = footprint.TotalEmissions;

            _carbonData[$"{footprint.BuildingId}-{footprint.Year}"] = footprint;

            // Sample initiatives
            _initiatives.AddRange(new[]
            {
                new SustainabilityInitiative
                {
                    InitiativeId = "SI-001",
                    Name = "LED Lighting Retrofit",
                    Description = "Replace all fluorescent lighting with LED fixtures",
                    Category = SustainabilityCategory.EnergyEfficiency,
                    Status = InitiativeStatus.Completed,
                    StartDate = new DateTime(2024, 3, 1),
                    CompletionDate = new DateTime(2024, 8, 15),
                    EstimatedCarbonReduction = 12,
                    ActualCarbonReduction = 14.5,
                    EstimatedEnergySavings = 120000,
                    ActualEnergySavings = 145000,
                    InvestmentCost = 35000000,
                    AnnualSavings = 18000000,
                    PaybackYears = 1.9,
                    IsVerified = true
                },
                new SustainabilityInitiative
                {
                    InitiativeId = "SI-002",
                    Name = "Rooftop Solar PV Installation",
                    Description = "Install 100kWp solar PV system on rooftop",
                    Category = SustainabilityCategory.RenewableEnergy,
                    Status = InitiativeStatus.InProgress,
                    StartDate = new DateTime(2025, 1, 15),
                    EstimatedCarbonReduction = 35,
                    EstimatedEnergySavings = 150000,
                    InvestmentCost = 300000000,
                    AnnualSavings = 45000000,
                    PaybackYears = 6.7
                },
                new SustainabilityInitiative
                {
                    InitiativeId = "SI-003",
                    Name = "Rainwater Harvesting System",
                    Description = "Install rainwater collection and treatment for toilet flushing",
                    Category = SustainabilityCategory.WaterConservation,
                    Status = InitiativeStatus.Planned,
                    EstimatedWaterSavings = 500,
                    InvestmentCost = 25000000,
                    AnnualSavings = 3000000,
                    PaybackYears = 8.3
                }
            });

            Logger.Info("Initialized sample sustainability data");
        }

        #endregion

        #region Carbon Accounting

        /// <summary>
        /// Calculate carbon footprint
        /// </summary>
        public CarbonFootprint CalculateCarbonFootprint(
            string buildingId,
            int year,
            double electricityKWh,
            double dieselLiters = 0,
            double lpgKg = 0,
            double refrigerantLeakKg = 0,
            string region = "Uganda")
        {
            var footprint = new CarbonFootprint
            {
                BuildingId = buildingId,
                Year = year,
                ReportingPeriod = "Annual"
            };

            // Scope 1 - Direct emissions
            var dieselEmissions = dieselLiters * _emissionFactors["Diesel"].Factor / 1000;
            var lpgEmissions = lpgKg * _emissionFactors["LPG"].Factor / 1000;
            var refrigerantEmissions = refrigerantLeakKg * _emissionFactors["Refrigerant-R410A"].Factor / 1000;

            footprint.Scope1BySource = new Dictionary<string, double>
            {
                ["Generator Diesel"] = dieselEmissions,
                ["LPG Cooking"] = lpgEmissions,
                ["Refrigerant Leakage"] = refrigerantEmissions
            };
            footprint.Scope1Total = footprint.Scope1BySource.Values.Sum();

            // Scope 2 - Electricity
            var electricityFactor = _emissionFactors.GetValueOrDefault($"Electricity-{region.ToUpper()[..2]}") ??
                                   _emissionFactors["Electricity-UG"];
            var electricityEmissions = electricityKWh * electricityFactor.Factor / 1000;

            footprint.Scope2BySource = new Dictionary<string, double>
            {
                ["Purchased Electricity"] = electricityEmissions
            };
            footprint.Scope2LocationBased = electricityEmissions;
            footprint.Scope2MarketBased = electricityEmissions;

            // Calculate totals
            footprint.TotalEmissions = footprint.Scope1Total + footprint.Scope2LocationBased;
            footprint.NetEmissions = footprint.TotalEmissions;

            // Store
            _carbonData[$"{buildingId}-{year}"] = footprint;

            Logger.Info($"Carbon footprint calculated for {buildingId} ({year}): {footprint.TotalEmissions:F1} tCO2e");

            return footprint;
        }

        /// <summary>
        /// Get carbon footprint
        /// </summary>
        public CarbonFootprint GetCarbonFootprint(string buildingId, int year)
        {
            return _carbonData.GetValueOrDefault($"{buildingId}-{year}");
        }

        /// <summary>
        /// Project carbon trajectory
        /// </summary>
        public CarbonTrajectory ProjectCarbonTrajectory(string buildingId, int targetYear, double targetReductionPercent)
        {
            var currentFootprint = _carbonData.Values
                .Where(f => f.BuildingId == buildingId)
                .OrderByDescending(f => f.Year)
                .FirstOrDefault();

            if (currentFootprint == null)
                return null;

            var trajectory = new CarbonTrajectory
            {
                BuildingId = buildingId,
                BaselineYear = currentFootprint.Year,
                BaselineEmissions = currentFootprint.TotalEmissions,
                TargetYear = targetYear,
                TargetReductionPercent = targetReductionPercent,
                TargetEmissions = currentFootprint.TotalEmissions * (1 - targetReductionPercent / 100)
            };

            // Linear pathway
            var yearsToTarget = targetYear - currentFootprint.Year;
            var annualReduction = (currentFootprint.TotalEmissions - trajectory.TargetEmissions) / yearsToTarget;

            for (int y = 0; y <= yearsToTarget; y++)
            {
                var year = currentFootprint.Year + y;
                trajectory.YearlyProjections[year] = currentFootprint.TotalEmissions - (annualReduction * y);
            }

            // Identify reduction opportunities
            trajectory.RequiredAnnualReduction = annualReduction;
            trajectory.IdentifiedReductionPotential = _initiatives
                .Where(i => i.Status == InitiativeStatus.Planned || i.Status == InitiativeStatus.InProgress)
                .Sum(i => i.EstimatedCarbonReduction);
            trajectory.Gap = trajectory.RequiredAnnualReduction * yearsToTarget - trajectory.IdentifiedReductionPotential;

            return trajectory;
        }

        #endregion

        #region Water & Waste Tracking

        /// <summary>
        /// Record water consumption
        /// </summary>
        public void RecordWaterConsumption(WaterConsumption data)
        {
            if (!_waterData.ContainsKey(data.BuildingId))
                _waterData[data.BuildingId] = new List<WaterConsumption>();

            data.IntensityPerM2 = data.TotalConsumptionM3 / 5000; // Assume 5000m²
            data.RecyclingPercent = data.TotalConsumptionM3 > 0 ?
                (data.RecycledWaterM3 + data.RainwaterHarvestedM3) / data.TotalConsumptionM3 * 100 : 0;

            _waterData[data.BuildingId].Add(data);
        }

        /// <summary>
        /// Record waste management
        /// </summary>
        public void RecordWasteManagement(WasteManagement data)
        {
            if (!_wasteData.ContainsKey(data.BuildingId))
                _wasteData[data.BuildingId] = new List<WasteManagement>();

            data.DiversionRate = data.TotalWasteKg > 0 ?
                (data.RecycledKg + data.CompostedKg) / data.TotalWasteKg * 100 : 0;
            data.RecyclingRate = data.TotalWasteKg > 0 ?
                data.RecycledKg / data.TotalWasteKg * 100 : 0;

            _wasteData[data.BuildingId].Add(data);
        }

        #endregion

        #region Initiatives Management

        /// <summary>
        /// Add sustainability initiative
        /// </summary>
        public void AddInitiative(SustainabilityInitiative initiative)
        {
            initiative.InitiativeId = $"SI-{_initiatives.Count + 1:D3}";
            if (initiative.AnnualSavings > 0 && initiative.InvestmentCost > 0)
                initiative.PaybackYears = (double)(initiative.InvestmentCost / initiative.AnnualSavings);
            _initiatives.Add(initiative);
        }

        /// <summary>
        /// Get all initiatives
        /// </summary>
        public List<SustainabilityInitiative> GetInitiatives(SustainabilityCategory? category = null, InitiativeStatus? status = null)
        {
            var query = _initiatives.AsEnumerable();
            if (category.HasValue)
                query = query.Where(i => i.Category == category.Value);
            if (status.HasValue)
                query = query.Where(i => i.Status == status.Value);
            return query.ToList();
        }

        /// <summary>
        /// Calculate ESG score
        /// </summary>
        public ESGScore CalculateESGScore(string buildingId, int year)
        {
            var carbon = GetCarbonFootprint(buildingId, year);
            var water = _waterData.GetValueOrDefault(buildingId)?.Where(w => w.Year == year).ToList();
            var waste = _wasteData.GetValueOrDefault(buildingId)?.Where(w => w.Year == year).ToList();

            var score = new ESGScore
            {
                BuildingId = buildingId,
                Year = year
            };

            // Environmental scores (simplified)
            score.CarbonScore = carbon != null && carbon.OnTrackForTarget ? 80 : 60;
            score.WaterScore = water?.Any() == true ? 70 : 50;
            score.WasteScore = waste?.Any() == true && waste.Average(w => w.DiversionRate) > 50 ? 75 : 55;
            score.EnergyScore = 70; // Based on EUI performance

            score.EnvironmentalScore = (score.CarbonScore + score.WaterScore + score.WasteScore + score.EnergyScore) / 4;

            // Social scores (placeholder)
            score.HealthSafetyScore = 80;
            score.OccupantWellbeingScore = 75;
            score.CommunityEngagementScore = 65;
            score.SocialScore = (score.HealthSafetyScore + score.OccupantWellbeingScore + score.CommunityEngagementScore) / 3;

            // Governance scores (placeholder)
            score.ComplianceScore = 85;
            score.TransparencyScore = 70;
            score.GovernanceScore = (score.ComplianceScore + score.TransparencyScore) / 2;

            // Overall
            score.OverallESGScore = score.EnvironmentalScore * 0.5 + score.SocialScore * 0.3 + score.GovernanceScore * 0.2;
            score.ESGRating = score.OverallESGScore >= 80 ? "A" :
                             score.OverallESGScore >= 65 ? "B" :
                             score.OverallESGScore >= 50 ? "C" : "D";

            return score;
        }

        #endregion

        #region Dashboard

        /// <summary>
        /// Get sustainability dashboard
        /// </summary>
        public SustainabilityDashboard GetDashboard(string buildingId = null)
        {
            var carbon = buildingId != null ?
                _carbonData.Values.Where(c => c.BuildingId == buildingId).ToList() :
                _carbonData.Values.ToList();

            var latestCarbon = carbon.OrderByDescending(c => c.Year).FirstOrDefault();

            var dashboard = new SustainabilityDashboard
            {
                GeneratedAt = DateTime.UtcNow,
                BuildingId = buildingId ?? "Portfolio",

                // Carbon summary
                TotalEmissions = latestCarbon?.TotalEmissions ?? 0,
                Scope1Emissions = latestCarbon?.Scope1Total ?? 0,
                Scope2Emissions = latestCarbon?.Scope2LocationBased ?? 0,
                CarbonIntensity = latestCarbon?.IntensityPerM2 ?? 0,
                ReductionFromBaseline = latestCarbon?.ReductionPercent ?? 0,
                OnTrackForTarget = latestCarbon?.OnTrackForTarget ?? false,

                // Initiatives
                TotalInitiatives = _initiatives.Count,
                CompletedInitiatives = _initiatives.Count(i => i.Status == InitiativeStatus.Completed),
                InProgressInitiatives = _initiatives.Count(i => i.Status == InitiativeStatus.InProgress),
                PlannedCarbonReduction = _initiatives.Sum(i => i.EstimatedCarbonReduction),
                AchievedCarbonReduction = _initiatives
                    .Where(i => i.Status == InitiativeStatus.Completed)
                    .Sum(i => i.ActualCarbonReduction),
                TotalInvestment = _initiatives.Sum(i => i.InvestmentCost),
                TotalAnnualSavings = _initiatives.Sum(i => i.AnnualSavings),

                // Top initiatives
                TopInitiatives = _initiatives
                    .OrderByDescending(i => i.EstimatedCarbonReduction)
                    .Take(5)
                    .ToList(),

                // ESG
                ESGScore = CalculateESGScore(buildingId ?? "BLD-001", DateTime.Now.Year)
            };

            return dashboard;
        }

        #endregion

        #endregion // Sustainability Intelligence Engine
    }

    #region Supporting Classes

    public class CarbonTrajectory
    {
        public string BuildingId { get; set; } = string.Empty;
        public int BaselineYear { get; set; }
        public double BaselineEmissions { get; set; }
        public int TargetYear { get; set; }
        public double TargetReductionPercent { get; set; }
        public double TargetEmissions { get; set; }
        public Dictionary<int, double> YearlyProjections { get; set; } = new();
        public double RequiredAnnualReduction { get; set; }
        public double IdentifiedReductionPotential { get; set; }
        public double Gap { get; set; }
    }

    public class SustainabilityDashboard
    {
        public DateTime GeneratedAt { get; set; }
        public string BuildingId { get; set; } = string.Empty;

        // Carbon
        public double TotalEmissions { get; set; }
        public double Scope1Emissions { get; set; }
        public double Scope2Emissions { get; set; }
        public double CarbonIntensity { get; set; }
        public double ReductionFromBaseline { get; set; }
        public bool OnTrackForTarget { get; set; }

        // Initiatives
        public int TotalInitiatives { get; set; }
        public int CompletedInitiatives { get; set; }
        public int InProgressInitiatives { get; set; }
        public double PlannedCarbonReduction { get; set; }
        public double AchievedCarbonReduction { get; set; }
        public decimal TotalInvestment { get; set; }
        public decimal TotalAnnualSavings { get; set; }

        public List<SustainabilityInitiative> TopInitiatives { get; set; } = new();

        // ESG
        public ESGScore ESGScore { get; set; }
    }

    #endregion
}
