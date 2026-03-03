// ============================================================================
// StingBIM AI - Facility Management Energy Intelligence
// Advanced energy analytics, optimization, and monitoring
// Integrates with BMS for real-time energy management
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.FacilityManagement.Intelligence
{
    #region Energy Models

    /// <summary>
    /// Building energy profile
    /// </summary>
    public class BuildingEnergyProfile
    {
        public string BuildingId { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public double GrossFloorArea { get; set; } // m²
        public string BuildingType { get; set; } = string.Empty; // Office, Retail, Hospital
        public string ClimateZone { get; set; } = string.Empty;
        public int YearBuilt { get; set; }
        public int Occupancy { get; set; }
        public double OperatingHoursPerWeek { get; set; }

        // Energy Use Intensity (EUI)
        public double AnnualEUI { get; set; } // kWh/m²/year
        public double TargetEUI { get; set; }
        public double BenchmarkEUI { get; set; }
        public double EUIPercentile { get; set; } // Performance vs peers

        // Consumption breakdown
        public Dictionary<string, double> ConsumptionBySystem { get; set; } = new();
        public Dictionary<string, double> ConsumptionByFuel { get; set; } = new();
        public Dictionary<int, double> MonthlyConsumption { get; set; } = new();

        // Peak demand
        public double PeakDemandKW { get; set; }
        public DateTime PeakDemandTime { get; set; }
        public double LoadFactor { get; set; }

        // Carbon footprint
        public double AnnualCarbonKgCO2 { get; set; }
        public double CarbonIntensity { get; set; } // kgCO2/m²
    }

    /// <summary>
    /// Energy meter reading
    /// </summary>
    public class EnergyMeterReading
    {
        public string MeterId { get; set; } = string.Empty;
        public string MeterName { get; set; } = string.Empty;
        public string MeterType { get; set; } = string.Empty; // Main, Submeter, Virtual
        public string EnergyType { get; set; } = string.Empty; // Electricity, Gas, Water, Steam
        public DateTime Timestamp { get; set; }
        public double Reading { get; set; }
        public double Consumption { get; set; } // Calculated from readings
        public string Unit { get; set; } = string.Empty;
        public double? DemandKW { get; set; }
        public double? PowerFactor { get; set; }
        public bool IsEstimated { get; set; }
    }

    /// <summary>
    /// Energy baseline model
    /// </summary>
    public class EnergyBaseline
    {
        public string BaselineId { get; set; } = string.Empty;
        public string BuildingId { get; set; } = string.Empty;
        public DateTime BaselinePeriodStart { get; set; }
        public DateTime BaselinePeriodEnd { get; set; }
        public int BaselineYear { get; set; }

        // Regression model parameters
        public double BaseLoad { get; set; } // kWh, weather-independent
        public double HeatingSlope { get; set; } // kWh per HDD
        public double CoolingSlope { get; set; } // kWh per CDD
        public double HeatingChangePoint { get; set; } // °C
        public double CoolingChangePoint { get; set; } // °C

        // Model quality
        public double RSquared { get; set; }
        public double CV_RMSE { get; set; } // Coefficient of variation
        public bool MeetsASHRAE14 { get; set; }

        // Adjustment factors
        public Dictionary<string, double> OccupancyFactors { get; set; } = new();
        public Dictionary<string, double> ScheduleFactors { get; set; } = new();
    }

    /// <summary>
    /// Energy savings measurement
    /// </summary>
    public class EnergySavingsMeasurement
    {
        public string MeasurementId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string ImplementationDate { get; set; } = string.Empty;

        public DateTime ReportingPeriodStart { get; set; }
        public DateTime ReportingPeriodEnd { get; set; }

        // Baseline vs actual
        public double BaselineConsumption { get; set; } // kWh
        public double ActualConsumption { get; set; }
        public double AdjustedBaseline { get; set; } // Weather/occupancy adjusted
        public double GrossSavings { get; set; }
        public double NetSavings { get; set; }
        public double SavingsPercent { get; set; }

        // Cost savings
        public decimal EnergyCostSavings { get; set; }
        public decimal DemandCostSavings { get; set; }
        public decimal TotalCostSavings { get; set; }

        // Carbon impact
        public double CarbonSavingsKgCO2 { get; set; }

        // Verification
        public string VerificationMethod { get; set; } = string.Empty; // IPMVP Option A/B/C/D
        public double UncertaintyPercent { get; set; }
    }

    /// <summary>
    /// Energy optimization opportunity
    /// </summary>
    public class EnergyOptimizationOpportunity
    {
        public string OpportunityId { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public EnergyOptimizationType Type { get; set; }
        public string Category { get; set; } = string.Empty;

        // Systems affected
        public List<string> AffectedSystems { get; set; } = new();
        public List<string> AffectedEquipment { get; set; } = new();

        // Savings potential
        public double EstimatedAnnualSavingsKWh { get; set; }
        public decimal EstimatedAnnualCostSavings { get; set; }
        public double EstimatedCarbonSavingsKgCO2 { get; set; }
        public double SavingsConfidence { get; set; }

        // Implementation
        public decimal ImplementationCost { get; set; }
        public double SimplePaybackYears { get; set; }
        public double IRR { get; set; } // Internal rate of return
        public string ImplementationComplexity { get; set; } = string.Empty;
        public List<string> ImplementationSteps { get; set; } = new();

        // Priority
        public int PriorityScore { get; set; }
        public string PriorityRationale { get; set; } = string.Empty;

        // Evidence
        public List<string> DataSources { get; set; } = new();
        public string AnalysisMethod { get; set; } = string.Empty;
    }

    public enum EnergyOptimizationType
    {
        ScheduleOptimization,
        SetpointAdjustment,
        EquipmentUpgrade,
        LoadManagement,
        BehavioralChange,
        MaintenanceAction,
        ControlsOptimization,
        EnvelopeImprovement,
        LightingUpgrade,
        RenewableEnergy
    }

    /// <summary>
    /// Real-time energy alert
    /// </summary>
    public class EnergyAlert
    {
        public string AlertId { get; set; } = string.Empty;
        public EnergyAlertType Type { get; set; }
        public EnergyAlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }

        public string MeterId { get; set; } = string.Empty;
        public string System { get; set; } = string.Empty;
        public double CurrentValue { get; set; }
        public double ThresholdValue { get; set; }
        public string Unit { get; set; } = string.Empty;

        public string RecommendedAction { get; set; } = string.Empty;
        public decimal EstimatedCostImpact { get; set; }
        public bool IsAcknowledged { get; set; }
    }

    public enum EnergyAlertType
    {
        PeakDemand,
        HighConsumption,
        AfterHoursUsage,
        EquipmentAbnormality,
        MeterError,
        TargetExceeded,
        BaseloadIncrease,
        PowerFactorLow
    }

    public enum EnergyAlertSeverity
    {
        Critical,
        Warning,
        Information
    }

    #endregion

    #region Energy Intelligence Engine

    /// <summary>
    /// FM Energy Intelligence Engine
    /// Provides advanced energy analytics and optimization
    /// </summary>
    public class FMEnergyIntelligence
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Building profiles
        private readonly Dictionary<string, BuildingEnergyProfile> _buildingProfiles = new();
        private readonly Dictionary<string, EnergyBaseline> _baselines = new();

        // Meter data buffer
        private readonly Dictionary<string, Queue<EnergyMeterReading>> _meterReadings = new();
        private const int MaxReadingsPerMeter = 8760; // 1 year hourly

        // Alerts
        private readonly List<EnergyAlert> _activeAlerts = new();

        // Energy rates (East Africa typical)
        private decimal _electricityRate = 650m; // UGX per kWh
        private decimal _demandCharge = 25000m; // UGX per kW
        private double _carbonFactor = 0.4; // kgCO2 per kWh (grid average)

        public FMEnergyIntelligence()
        {
            InitializeSampleProfile();
            Logger.Info("FM Energy Intelligence Engine initialized");
        }

        #region Initialization

        private void InitializeSampleProfile()
        {
            var profile = new BuildingEnergyProfile
            {
                BuildingId = "BLD-001",
                BuildingName = "Main Office Building",
                GrossFloorArea = 5000,
                BuildingType = "Office",
                ClimateZone = "Tropical Highland",
                YearBuilt = 2015,
                Occupancy = 250,
                OperatingHoursPerWeek = 55,
                AnnualEUI = 180, // kWh/m²/year
                TargetEUI = 150,
                BenchmarkEUI = 200, // Regional benchmark for offices
                ConsumptionBySystem = new Dictionary<string, double>
                {
                    ["HVAC"] = 0.45,
                    ["Lighting"] = 0.20,
                    ["Plug Loads"] = 0.18,
                    ["Elevators"] = 0.08,
                    ["Pumps"] = 0.05,
                    ["Other"] = 0.04
                },
                ConsumptionByFuel = new Dictionary<string, double>
                {
                    ["Electricity"] = 0.92,
                    ["Diesel (Generator)"] = 0.08
                },
                PeakDemandKW = 450,
                LoadFactor = 0.55
            };

            profile.AnnualCarbonKgCO2 = profile.AnnualEUI * profile.GrossFloorArea * _carbonFactor;
            profile.CarbonIntensity = profile.AnnualEUI * _carbonFactor;
            profile.EUIPercentile = 65; // Better than 65% of similar buildings

            _buildingProfiles[profile.BuildingId] = profile;

            // Initialize baseline
            _baselines[profile.BuildingId] = new EnergyBaseline
            {
                BaselineId = "BL-001",
                BuildingId = profile.BuildingId,
                BaselineYear = 2024,
                BaseLoad = 15000, // kWh/month
                CoolingSlope = 800, // kWh per CDD
                HeatingSlope = 0, // No heating in tropical climate
                CoolingChangePoint = 18,
                RSquared = 0.85,
                CV_RMSE = 12,
                MeetsASHRAE14 = true
            };
        }

        #endregion

        #region Energy Analysis

        /// <summary>
        /// Analyze building energy performance
        /// </summary>
        public BuildingEnergyAnalysis AnalyzeBuildingEnergy(string buildingId)
        {
            if (!_buildingProfiles.TryGetValue(buildingId, out var profile))
            {
                profile = _buildingProfiles.Values.FirstOrDefault();
                if (profile == null)
                    return new BuildingEnergyAnalysis { BuildingId = buildingId };
            }

            var analysis = new BuildingEnergyAnalysis
            {
                BuildingId = buildingId,
                BuildingName = profile.BuildingName,
                AnalysisDate = DateTime.UtcNow,
                GrossFloorArea = profile.GrossFloorArea,
                CurrentEUI = profile.AnnualEUI,
                TargetEUI = profile.TargetEUI,
                BenchmarkEUI = profile.BenchmarkEUI
            };

            // Calculate performance scores
            analysis.EnergyStarScore = CalculateEnergyStarScore(profile);
            analysis.PerformanceRating = GetPerformanceRating(profile);

            // Breakdown analysis
            analysis.SystemBreakdown = AnalyzeSystemBreakdown(profile);
            analysis.TimeOfUseBreakdown = AnalyzeTimeOfUse(buildingId);

            // Identify waste
            analysis.IdentifiedWaste = IdentifyEnergyWaste(profile, buildingId);
            analysis.TotalWasteKWh = analysis.IdentifiedWaste.Sum(w => w.EstimatedWasteKWh);
            analysis.TotalWasteCost = analysis.IdentifiedWaste.Sum(w => w.EstimatedWasteCost);

            // Optimization opportunities
            analysis.Opportunities = IdentifyOptimizationOpportunities(profile);
            analysis.TotalSavingsPotential = analysis.Opportunities.Sum(o => o.EstimatedAnnualCostSavings);

            // Carbon analysis
            analysis.CarbonFootprint = new CarbonAnalysis
            {
                AnnualEmissionsKgCO2 = profile.AnnualCarbonKgCO2,
                IntensityKgCO2PerM2 = profile.CarbonIntensity,
                EquivalentTrees = (int)(profile.AnnualCarbonKgCO2 / 21), // ~21kg CO2 per tree per year
                EquivalentCarKm = profile.AnnualCarbonKgCO2 / 0.12 // ~120g CO2 per km
            };

            Logger.Info($"Energy analysis complete for {buildingId}: EUI={analysis.CurrentEUI:F1}, Score={analysis.EnergyStarScore}");

            return analysis;
        }

        private int CalculateEnergyStarScore(BuildingEnergyProfile profile)
        {
            // Simplified Energy Star-like calculation
            var ratio = profile.AnnualEUI / profile.BenchmarkEUI;

            if (ratio <= 0.5) return 100;
            if (ratio <= 0.6) return 90;
            if (ratio <= 0.7) return 80;
            if (ratio <= 0.8) return 70;
            if (ratio <= 0.9) return 60;
            if (ratio <= 1.0) return 50;
            if (ratio <= 1.1) return 40;
            if (ratio <= 1.2) return 30;
            if (ratio <= 1.4) return 20;
            return 10;
        }

        private string GetPerformanceRating(BuildingEnergyProfile profile)
        {
            var ratio = profile.AnnualEUI / profile.BenchmarkEUI;
            if (ratio <= 0.7) return "Excellent";
            if (ratio <= 0.85) return "Good";
            if (ratio <= 1.0) return "Average";
            if (ratio <= 1.15) return "Below Average";
            return "Poor";
        }

        private List<SystemEnergyBreakdown> AnalyzeSystemBreakdown(BuildingEnergyProfile profile)
        {
            var breakdowns = new List<SystemEnergyBreakdown>();
            var totalAnnualKWh = profile.AnnualEUI * profile.GrossFloorArea;

            // System benchmarks for offices (kWh/m²)
            var benchmarks = new Dictionary<string, double>
            {
                ["HVAC"] = 80,
                ["Lighting"] = 30,
                ["Plug Loads"] = 35,
                ["Elevators"] = 10,
                ["Pumps"] = 8,
                ["Other"] = 10
            };

            foreach (var (system, fraction) in profile.ConsumptionBySystem)
            {
                var systemKWh = totalAnnualKWh * fraction;
                var systemEUI = profile.AnnualEUI * fraction;
                var benchmark = benchmarks.GetValueOrDefault(system, profile.AnnualEUI * fraction);

                breakdowns.Add(new SystemEnergyBreakdown
                {
                    SystemName = system,
                    ConsumptionKWh = systemKWh,
                    Percentage = fraction * 100,
                    EUI = systemEUI,
                    BenchmarkEUI = benchmark,
                    PerformanceVsBenchmark = (systemEUI / benchmark - 1) * 100,
                    AnnualCost = (decimal)systemKWh * _electricityRate,
                    CarbonKgCO2 = systemKWh * _carbonFactor
                });
            }

            return breakdowns.OrderByDescending(b => b.ConsumptionKWh).ToList();
        }

        private TimeOfUseBreakdown AnalyzeTimeOfUse(string buildingId)
        {
            // Simulated time-of-use analysis
            return new TimeOfUseBreakdown
            {
                OccupiedHoursPercent = 65,
                UnoccupiedHoursPercent = 35,
                BaseloadPercent = 40,
                WeekdayAvgKWh = 2500,
                WeekendAvgKWh = 1200,
                NightAvgKW = 180,
                DayAvgKW = 380,
                PeakHourAvgKW = 420,
                AfterHoursWastePercent = 15 // Potential savings
            };
        }

        private List<EnergyWaste> IdentifyEnergyWaste(BuildingEnergyProfile profile, string buildingId)
        {
            var wastes = new List<EnergyWaste>();
            var totalAnnualKWh = profile.AnnualEUI * profile.GrossFloorArea;

            // After-hours HVAC operation
            wastes.Add(new EnergyWaste
            {
                WasteType = "After-Hours HVAC",
                Description = "HVAC running during unoccupied hours",
                EstimatedWasteKWh = totalAnnualKWh * 0.08,
                EstimatedWasteCost = (decimal)(totalAnnualKWh * 0.08) * _electricityRate,
                Confidence = 0.75,
                DetectionMethod = "Schedule analysis",
                RecommendedAction = "Optimize HVAC schedules to match occupancy"
            });

            // Lighting waste
            wastes.Add(new EnergyWaste
            {
                WasteType = "Lighting During Daylight",
                Description = "Perimeter lighting on during sufficient daylight hours",
                EstimatedWasteKWh = totalAnnualKWh * 0.03,
                EstimatedWasteCost = (decimal)(totalAnnualKWh * 0.03) * _electricityRate,
                Confidence = 0.70,
                DetectionMethod = "Daylight harvesting potential analysis",
                RecommendedAction = "Implement daylight dimming controls"
            });

            // Weekend baseload
            wastes.Add(new EnergyWaste
            {
                WasteType = "Elevated Weekend Baseload",
                Description = "Higher than necessary weekend energy consumption",
                EstimatedWasteKWh = totalAnnualKWh * 0.04,
                EstimatedWasteCost = (decimal)(totalAnnualKWh * 0.04) * _electricityRate,
                Confidence = 0.65,
                DetectionMethod = "Baseload analysis",
                RecommendedAction = "Audit weekend operations and shutdown procedures"
            });

            // Simultaneous heating/cooling (if applicable)
            if (profile.ConsumptionBySystem.ContainsKey("HVAC"))
            {
                wastes.Add(new EnergyWaste
                {
                    WasteType = "Suboptimal Setpoints",
                    Description = "HVAC setpoints not optimized for occupant comfort and efficiency",
                    EstimatedWasteKWh = totalAnnualKWh * 0.05,
                    EstimatedWasteCost = (decimal)(totalAnnualKWh * 0.05) * _electricityRate,
                    Confidence = 0.60,
                    DetectionMethod = "Setpoint analysis",
                    RecommendedAction = "Widen deadband, optimize setpoints by zone"
                });
            }

            return wastes;
        }

        /// <summary>
        /// Identify energy optimization opportunities
        /// </summary>
        public List<EnergyOptimizationOpportunity> IdentifyOptimizationOpportunities(BuildingEnergyProfile profile)
        {
            var opportunities = new List<EnergyOptimizationOpportunity>();
            var totalAnnualKWh = profile.AnnualEUI * profile.GrossFloorArea;

            // Schedule optimization
            opportunities.Add(new EnergyOptimizationOpportunity
            {
                Title = "HVAC Schedule Optimization",
                Description = "Align HVAC operation with actual occupancy patterns using BMS scheduling",
                Type = EnergyOptimizationType.ScheduleOptimization,
                Category = "Controls",
                AffectedSystems = new() { "HVAC" },
                EstimatedAnnualSavingsKWh = totalAnnualKWh * 0.12,
                EstimatedAnnualCostSavings = (decimal)(totalAnnualKWh * 0.12) * _electricityRate,
                ImplementationCost = 2000000m, // UGX
                ImplementationComplexity = "Low",
                SavingsConfidence = 0.80,
                ImplementationSteps = new()
                {
                    "Audit actual occupancy patterns",
                    "Program optimized schedules in BMS",
                    "Implement optimal start/stop",
                    "Monitor and adjust"
                }
            });

            // Setpoint adjustment
            opportunities.Add(new EnergyOptimizationOpportunity
            {
                Title = "Cooling Setpoint Increase",
                Description = "Raise cooling setpoint by 1-2°C during occupied hours",
                Type = EnergyOptimizationType.SetpointAdjustment,
                Category = "Controls",
                AffectedSystems = new() { "HVAC" },
                EstimatedAnnualSavingsKWh = totalAnnualKWh * 0.06,
                EstimatedAnnualCostSavings = (decimal)(totalAnnualKWh * 0.06) * _electricityRate,
                ImplementationCost = 500000m,
                ImplementationComplexity = "Low",
                SavingsConfidence = 0.85,
                ImplementationSteps = new()
                {
                    "Communicate change to occupants",
                    "Gradually increase setpoint",
                    "Monitor comfort feedback",
                    "Adjust as needed"
                }
            });

            // LED lighting
            var lightingKWh = totalAnnualKWh * profile.ConsumptionBySystem.GetValueOrDefault("Lighting", 0.2);
            opportunities.Add(new EnergyOptimizationOpportunity
            {
                Title = "LED Lighting Retrofit",
                Description = "Replace remaining fluorescent fixtures with high-efficiency LED",
                Type = EnergyOptimizationType.LightingUpgrade,
                Category = "Equipment",
                AffectedSystems = new() { "Lighting" },
                EstimatedAnnualSavingsKWh = lightingKWh * 0.5,
                EstimatedAnnualCostSavings = (decimal)(lightingKWh * 0.5) * _electricityRate,
                ImplementationCost = 35000000m,
                ImplementationComplexity = "Medium",
                SavingsConfidence = 0.90,
                ImplementationSteps = new()
                {
                    "Conduct lighting audit",
                    "Develop replacement schedule",
                    "Procure LED fixtures",
                    "Install in phases",
                    "Commission and verify savings"
                }
            });

            // VFD on pumps
            opportunities.Add(new EnergyOptimizationOpportunity
            {
                Title = "Variable Frequency Drives on Pumps",
                Description = "Install VFDs on chilled water and condenser water pumps",
                Type = EnergyOptimizationType.EquipmentUpgrade,
                Category = "Equipment",
                AffectedSystems = new() { "HVAC", "Pumps" },
                AffectedEquipment = new() { "CHWP-01", "CWP-01" },
                EstimatedAnnualSavingsKWh = totalAnnualKWh * 0.04,
                EstimatedAnnualCostSavings = (decimal)(totalAnnualKWh * 0.04) * _electricityRate,
                ImplementationCost = 25000000m,
                ImplementationComplexity = "Medium",
                SavingsConfidence = 0.85,
                ImplementationSteps = new()
                {
                    "Assess pump operation",
                    "Size and specify VFDs",
                    "Install VFDs",
                    "Commission and tune controls"
                }
            });

            // Demand management
            opportunities.Add(new EnergyOptimizationOpportunity
            {
                Title = "Peak Demand Management",
                Description = "Implement load shedding and staggered startup to reduce peak demand",
                Type = EnergyOptimizationType.LoadManagement,
                Category = "Controls",
                AffectedSystems = new() { "Electrical", "HVAC" },
                EstimatedAnnualSavingsKWh = 0, // Demand savings, not energy
                EstimatedAnnualCostSavings = (decimal)profile.PeakDemandKW * 0.1m * _demandCharge * 12,
                ImplementationCost = 8000000m,
                ImplementationComplexity = "Medium",
                SavingsConfidence = 0.75,
                ImplementationSteps = new()
                {
                    "Identify sheddable loads",
                    "Program demand limiting",
                    "Configure equipment staging",
                    "Test and verify"
                }
            });

            // Solar PV
            opportunities.Add(new EnergyOptimizationOpportunity
            {
                Title = "Rooftop Solar PV Installation",
                Description = "Install rooftop solar panels for on-site renewable generation",
                Type = EnergyOptimizationType.RenewableEnergy,
                Category = "Generation",
                AffectedSystems = new() { "Electrical" },
                EstimatedAnnualSavingsKWh = Math.Min(profile.GrossFloorArea * 0.3 * 1500, totalAnnualKWh * 0.25),
                EstimatedAnnualCostSavings = (decimal)Math.Min(profile.GrossFloorArea * 0.3 * 1500, totalAnnualKWh * 0.25) * _electricityRate,
                ImplementationCost = (decimal)profile.GrossFloorArea * 0.3m * 2000000m, // ~2M UGX per kWp
                ImplementationComplexity = "High",
                SavingsConfidence = 0.80,
                ImplementationSteps = new()
                {
                    "Assess roof structural capacity",
                    "Conduct solar feasibility study",
                    "Design PV system",
                    "Obtain permits",
                    "Install and commission"
                }
            });

            // Calculate simple payback and prioritize
            foreach (var opp in opportunities)
            {
                if (opp.EstimatedAnnualCostSavings > 0)
                {
                    opp.SimplePaybackYears = (double)(opp.ImplementationCost / opp.EstimatedAnnualCostSavings);
                    opp.IRR = opp.SimplePaybackYears > 0 ? (1 / opp.SimplePaybackYears) * 100 : 0;
                }
                opp.EstimatedCarbonSavingsKgCO2 = opp.EstimatedAnnualSavingsKWh * _carbonFactor;
                opp.PriorityScore = CalculatePriorityScore(opp);
            }

            return opportunities.OrderByDescending(o => o.PriorityScore).ToList();
        }

        private int CalculatePriorityScore(EnergyOptimizationOpportunity opp)
        {
            var score = 0;

            // Payback weighting (shorter is better)
            if (opp.SimplePaybackYears < 1) score += 40;
            else if (opp.SimplePaybackYears < 2) score += 30;
            else if (opp.SimplePaybackYears < 3) score += 20;
            else if (opp.SimplePaybackYears < 5) score += 10;

            // Complexity weighting (easier is better)
            score += opp.ImplementationComplexity switch
            {
                "Low" => 30,
                "Medium" => 20,
                "High" => 10,
                _ => 15
            };

            // Confidence weighting
            score += (int)(opp.SavingsConfidence * 30);

            return score;
        }

        #endregion

        #region Real-Time Monitoring

        /// <summary>
        /// Process meter reading and check for alerts
        /// </summary>
        public List<EnergyAlert> ProcessMeterReading(EnergyMeterReading reading)
        {
            var alerts = new List<EnergyAlert>();

            // Store reading
            if (!_meterReadings.ContainsKey(reading.MeterId))
                _meterReadings[reading.MeterId] = new Queue<EnergyMeterReading>();

            var buffer = _meterReadings[reading.MeterId];
            buffer.Enqueue(reading);
            while (buffer.Count > MaxReadingsPerMeter)
                buffer.Dequeue();

            // Check for alerts
            if (reading.DemandKW.HasValue)
            {
                // Peak demand alert
                var profile = _buildingProfiles.Values.FirstOrDefault();
                if (profile != null && reading.DemandKW > profile.PeakDemandKW * 0.9)
                {
                    alerts.Add(new EnergyAlert
                    {
                        AlertId = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                        Type = EnergyAlertType.PeakDemand,
                        Severity = reading.DemandKW > profile.PeakDemandKW ?
                            EnergyAlertSeverity.Critical : EnergyAlertSeverity.Warning,
                        Message = $"High demand: {reading.DemandKW:F1} kW (Peak: {profile.PeakDemandKW:F1} kW)",
                        DetectedAt = DateTime.UtcNow,
                        MeterId = reading.MeterId,
                        CurrentValue = reading.DemandKW.Value,
                        ThresholdValue = profile.PeakDemandKW * 0.9,
                        Unit = "kW",
                        RecommendedAction = "Consider load shedding to avoid peak demand charges",
                        EstimatedCostImpact = (decimal)(reading.DemandKW.Value - profile.PeakDemandKW * 0.9) * _demandCharge
                    });
                }
            }

            // Power factor alert
            if (reading.PowerFactor.HasValue && reading.PowerFactor < 0.85)
            {
                alerts.Add(new EnergyAlert
                {
                    AlertId = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                    Type = EnergyAlertType.PowerFactorLow,
                    Severity = reading.PowerFactor < 0.7 ?
                        EnergyAlertSeverity.Warning : EnergyAlertSeverity.Information,
                    Message = $"Low power factor: {reading.PowerFactor:P1}",
                    DetectedAt = DateTime.UtcNow,
                    MeterId = reading.MeterId,
                    CurrentValue = reading.PowerFactor.Value,
                    ThresholdValue = 0.85,
                    RecommendedAction = "Check for inductive loads, consider power factor correction"
                });
            }

            // After-hours usage alert
            var hour = reading.Timestamp.Hour;
            var isWeekend = reading.Timestamp.DayOfWeek == DayOfWeek.Saturday ||
                           reading.Timestamp.DayOfWeek == DayOfWeek.Sunday;
            var isAfterHours = hour < 6 || hour > 20 || isWeekend;

            if (isAfterHours && reading.Consumption > 0)
            {
                var recentReadings = buffer.TakeLast(24).ToList();
                var avgOffHours = recentReadings
                    .Where(r => r.Timestamp.Hour < 6 || r.Timestamp.Hour > 20)
                    .Select(r => r.Consumption)
                    .DefaultIfEmpty(0)
                    .Average();

                if (reading.Consumption > avgOffHours * 1.5)
                {
                    alerts.Add(new EnergyAlert
                    {
                        AlertId = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                        Type = EnergyAlertType.AfterHoursUsage,
                        Severity = EnergyAlertSeverity.Information,
                        Message = $"Elevated after-hours consumption: {reading.Consumption:F1} kWh",
                        DetectedAt = DateTime.UtcNow,
                        MeterId = reading.MeterId,
                        CurrentValue = reading.Consumption,
                        ThresholdValue = avgOffHours * 1.5,
                        Unit = "kWh",
                        RecommendedAction = "Check for equipment left running unnecessarily"
                    });
                }
            }

            foreach (var alert in alerts)
            {
                _activeAlerts.Add(alert);
            }

            return alerts;
        }

        /// <summary>
        /// Get current energy dashboard
        /// </summary>
        public EnergyDashboard GetDashboard(string buildingId = null)
        {
            var profile = string.IsNullOrEmpty(buildingId) ?
                _buildingProfiles.Values.FirstOrDefault() :
                _buildingProfiles.GetValueOrDefault(buildingId);

            if (profile == null)
                return new EnergyDashboard();

            var totalAnnualKWh = profile.AnnualEUI * profile.GrossFloorArea;

            return new EnergyDashboard
            {
                BuildingId = profile.BuildingId,
                BuildingName = profile.BuildingName,
                GeneratedAt = DateTime.UtcNow,

                // Current status
                CurrentDemandKW = profile.PeakDemandKW * 0.7, // Simulated current
                TodayConsumptionKWh = totalAnnualKWh / 365 * 0.6, // Simulated
                MonthToDateKWh = totalAnnualKWh / 12 * 0.7,
                YearToDateKWh = totalAnnualKWh * 0.8,

                // Targets
                MonthlyTargetKWh = totalAnnualKWh / 12,
                MonthlyTargetPercent = 70,
                AnnualTargetKWh = totalAnnualKWh,

                // Performance
                CurrentEUI = profile.AnnualEUI,
                TargetEUI = profile.TargetEUI,
                EnergyStarScore = CalculateEnergyStarScore(profile),
                PerformanceRating = GetPerformanceRating(profile),

                // Costs
                MonthToDateCost = (decimal)(totalAnnualKWh / 12 * 0.7) * _electricityRate,
                ProjectedMonthlyCost = (decimal)(totalAnnualKWh / 12) * _electricityRate,
                YearToDateCost = (decimal)(totalAnnualKWh * 0.8) * _electricityRate,

                // Carbon
                MonthToDateCarbonKgCO2 = totalAnnualKWh / 12 * 0.7 * _carbonFactor,
                YearToDateCarbonKgCO2 = totalAnnualKWh * 0.8 * _carbonFactor,

                // Alerts
                ActiveAlerts = _activeAlerts.Where(a => !a.IsAcknowledged).ToList(),
                CriticalAlertCount = _activeAlerts.Count(a => a.Severity == EnergyAlertSeverity.Critical && !a.IsAcknowledged)
            };
        }

        #endregion

        #region Measurement & Verification

        /// <summary>
        /// Calculate energy savings using IPMVP methodology
        /// </summary>
        public EnergySavingsMeasurement MeasureSavings(
            string buildingId,
            DateTime reportingStart,
            DateTime reportingEnd,
            string projectName)
        {
            if (!_baselines.TryGetValue(buildingId, out var baseline))
            {
                return new EnergySavingsMeasurement
                {
                    MeasurementId = "N/A",
                    ProjectName = projectName,
                    VerificationMethod = "Baseline required"
                };
            }

            var measurement = new EnergySavingsMeasurement
            {
                MeasurementId = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                ProjectName = projectName,
                ReportingPeriodStart = reportingStart,
                ReportingPeriodEnd = reportingEnd,
                VerificationMethod = "IPMVP Option C - Whole Building"
            };

            // Simulate baseline and actual (in production, would use real data)
            var days = (reportingEnd - reportingStart).Days;
            measurement.BaselineConsumption = baseline.BaseLoad * days / 30;
            measurement.ActualConsumption = measurement.BaselineConsumption * 0.88; // 12% savings

            // Weather adjustment would be applied here
            measurement.AdjustedBaseline = measurement.BaselineConsumption * 1.02; // Slightly warmer period

            measurement.GrossSavings = measurement.AdjustedBaseline - measurement.ActualConsumption;
            measurement.NetSavings = measurement.GrossSavings * 0.95; // Account for uncertainty
            measurement.SavingsPercent = measurement.NetSavings / measurement.AdjustedBaseline * 100;

            measurement.EnergyCostSavings = (decimal)measurement.NetSavings * _electricityRate;
            measurement.DemandCostSavings = measurement.EnergyCostSavings * 0.15m; // Estimated
            measurement.TotalCostSavings = measurement.EnergyCostSavings + measurement.DemandCostSavings;

            measurement.CarbonSavingsKgCO2 = measurement.NetSavings * _carbonFactor;
            measurement.UncertaintyPercent = 15; // Typical for Option C

            return measurement;
        }

        #endregion

        #endregion // Energy Intelligence Engine
    }

    #region Supporting Classes

    public class BuildingEnergyAnalysis
    {
        public string BuildingId { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public DateTime AnalysisDate { get; set; }
        public double GrossFloorArea { get; set; }
        public double CurrentEUI { get; set; }
        public double TargetEUI { get; set; }
        public double BenchmarkEUI { get; set; }
        public int EnergyStarScore { get; set; }
        public string PerformanceRating { get; set; } = string.Empty;
        public List<SystemEnergyBreakdown> SystemBreakdown { get; set; } = new();
        public TimeOfUseBreakdown TimeOfUseBreakdown { get; set; }
        public List<EnergyWaste> IdentifiedWaste { get; set; } = new();
        public double TotalWasteKWh { get; set; }
        public decimal TotalWasteCost { get; set; }
        public List<EnergyOptimizationOpportunity> Opportunities { get; set; } = new();
        public decimal TotalSavingsPotential { get; set; }
        public CarbonAnalysis CarbonFootprint { get; set; }
    }

    public class SystemEnergyBreakdown
    {
        public string SystemName { get; set; } = string.Empty;
        public double ConsumptionKWh { get; set; }
        public double Percentage { get; set; }
        public double EUI { get; set; }
        public double BenchmarkEUI { get; set; }
        public double PerformanceVsBenchmark { get; set; }
        public decimal AnnualCost { get; set; }
        public double CarbonKgCO2 { get; set; }
    }

    public class TimeOfUseBreakdown
    {
        public double OccupiedHoursPercent { get; set; }
        public double UnoccupiedHoursPercent { get; set; }
        public double BaseloadPercent { get; set; }
        public double WeekdayAvgKWh { get; set; }
        public double WeekendAvgKWh { get; set; }
        public double NightAvgKW { get; set; }
        public double DayAvgKW { get; set; }
        public double PeakHourAvgKW { get; set; }
        public double AfterHoursWastePercent { get; set; }
    }

    public class EnergyWaste
    {
        public string WasteType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double EstimatedWasteKWh { get; set; }
        public decimal EstimatedWasteCost { get; set; }
        public double Confidence { get; set; }
        public string DetectionMethod { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
    }

    public class CarbonAnalysis
    {
        public double AnnualEmissionsKgCO2 { get; set; }
        public double IntensityKgCO2PerM2 { get; set; }
        public int EquivalentTrees { get; set; }
        public double EquivalentCarKm { get; set; }
    }

    public class EnergyDashboard
    {
        public string BuildingId { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public double CurrentDemandKW { get; set; }
        public double TodayConsumptionKWh { get; set; }
        public double MonthToDateKWh { get; set; }
        public double YearToDateKWh { get; set; }
        public double MonthlyTargetKWh { get; set; }
        public double MonthlyTargetPercent { get; set; }
        public double AnnualTargetKWh { get; set; }
        public double CurrentEUI { get; set; }
        public double TargetEUI { get; set; }
        public int EnergyStarScore { get; set; }
        public string PerformanceRating { get; set; } = string.Empty;
        public decimal MonthToDateCost { get; set; }
        public decimal ProjectedMonthlyCost { get; set; }
        public decimal YearToDateCost { get; set; }
        public double MonthToDateCarbonKgCO2 { get; set; }
        public double YearToDateCarbonKgCO2 { get; set; }
        public List<EnergyAlert> ActiveAlerts { get; set; } = new();
        public int CriticalAlertCount { get; set; }
    }

    #endregion
}
