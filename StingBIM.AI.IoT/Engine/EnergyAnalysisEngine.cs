// StingBIM.AI.IoT.Engine.EnergyAnalysisEngine
// Energy modeling, carbon tracking, and sustainability analysis engine.
// Implements ISO 50001 (Energy Management), ASHRAE 90.1 (Energy Standard),
// WELL Building Standard, and lifecycle carbon analysis per EN 15978.
// Includes multi-regional grid emission factors for Uganda, Kenya, South Africa, UK, and US.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.IoT.Models;

namespace StingBIM.AI.IoT.Engine
{
    /// <summary>
    /// Comprehensive energy analysis engine providing Energy Use Intensity calculations,
    /// carbon emission tracking, ASHRAE 90.1 benchmarking, ISO 50001 audit reports,
    /// ML-based energy prediction, renewable contribution analysis, lifecycle carbon
    /// assessment, and WELL Building Standard scoring.
    /// </summary>
    public class EnergyAnalysisEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        // Dependencies
        private readonly SensorIntegrationEngine _sensorEngine;

        // Grid emission factors by region (kgCO2e per kWh)
        // Sources: IEA 2024, Uganda ERA, Kenya Power, Eskom, BEIS, EPA eGRID
        private readonly Dictionary<string, double> _gridEmissionFactors =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Uganda"] = 0.028,         // Predominantly hydro (95%+)
                ["Kenya"] = 0.332,          // Geothermal + hydro + thermal mix
                ["SouthAfrica"] = 0.928,    // Coal-heavy (Eskom grid)
                ["Tanzania"] = 0.420,       // Gas + hydro mix
                ["Rwanda"] = 0.110,         // Hydro-dominant
                ["UK"] = 0.207,             // Gas + renewable + nuclear mix
                ["US_Average"] = 0.386,     // National average
                ["US_California"] = 0.221,  // Clean grid state
                ["US_Texas"] = 0.394,       // Gas-heavy ERCOT
                ["EU_Average"] = 0.230,     // European average
                ["India"] = 0.708,          // Coal-heavy
                ["China"] = 0.555,          // Coal + hydro mix
                ["Default"] = 0.400         // World average fallback
            };

        // Fuel emission factors (kgCO2e per unit)
        private readonly Dictionary<string, (double Factor, string Unit)> _fuelEmissionFactors =
            new Dictionary<string, (double, string)>(StringComparer.OrdinalIgnoreCase)
            {
                ["NaturalGas"] = (2.02, "kgCO2e/m3"),
                ["Diesel"] = (2.68, "kgCO2e/liter"),
                ["LPG"] = (1.51, "kgCO2e/liter"),
                ["FuelOil"] = (2.96, "kgCO2e/liter"),
                ["Charcoal"] = (3.30, "kgCO2e/kg"),
                ["Biomass"] = (0.39, "kgCO2e/kg")
            };

        // ASHRAE 90.1 baseline EUIs by building type (kWh/m2/yr)
        // Values from ASHRAE 90.1-2022 Appendix G baseline models
        private readonly Dictionary<string, double> _ashrae901Baselines =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Office_Small"] = 145.0,
                ["Office_Medium"] = 165.0,
                ["Office_Large"] = 185.0,
                ["Retail"] = 175.0,
                ["School_Primary"] = 130.0,
                ["School_Secondary"] = 150.0,
                ["University"] = 195.0,
                ["Hospital"] = 340.0,
                ["Hotel"] = 200.0,
                ["Residential_Apartment"] = 120.0,
                ["Residential_House"] = 100.0,
                ["Warehouse"] = 65.0,
                ["Restaurant"] = 280.0,
                ["Supermarket"] = 310.0,
                ["DataCenter"] = 650.0,
                ["Laboratory"] = 390.0,
                ["Assembly"] = 125.0,
                ["Default"] = 170.0
            };

        // Embodied carbon factors (kgCO2e per unit) per EN 15978
        private readonly Dictionary<string, double> _embodiedCarbonFactors =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Concrete_C30"] = 260.0,       // kgCO2e/m3
                ["Steel_Structural"] = 1.46,     // kgCO2e/kg
                ["Steel_Rebar"] = 1.99,          // kgCO2e/kg
                ["Aluminum"] = 6.67,             // kgCO2e/kg
                ["Timber_Softwood"] = -1.03,     // kgCO2e/kg (carbon sequestration)
                ["Timber_Hardwood"] = -0.86,     // kgCO2e/kg
                ["Glass_Float"] = 1.20,          // kgCO2e/kg
                ["Brick"] = 0.24,                // kgCO2e/kg
                ["Insulation_EPS"] = 3.29,       // kgCO2e/kg
                ["Insulation_Mineral"] = 1.28,   // kgCO2e/kg
                ["Copper"] = 3.83,               // kgCO2e/kg
                ["PVC_Pipe"] = 3.10,             // kgCO2e/kg
                ["Plasterboard"] = 0.39,         // kgCO2e/kg
                ["Cement"] = 0.83                // kgCO2e/kg
            };

        // Degree-day base temperatures
        private const double HeatingBaseTemp = 15.5; // degC (HDD base)
        private const double CoolingBaseTemp = 18.3;  // degC (CDD base)

        // Building registry
        private readonly Dictionary<string, BuildingProfile> _buildings =
            new Dictionary<string, BuildingProfile>(StringComparer.OrdinalIgnoreCase);

        // Monthly energy history for trending
        private readonly ConcurrentDictionary<string, List<MonthlyEnergy>> _monthlyHistory =
            new ConcurrentDictionary<string, List<MonthlyEnergy>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes the EnergyAnalysisEngine with a reference to the sensor engine.
        /// </summary>
        public EnergyAnalysisEngine(SensorIntegrationEngine sensorEngine)
        {
            _sensorEngine = sensorEngine ?? throw new ArgumentNullException(nameof(sensorEngine));
            Logger.Info("EnergyAnalysisEngine initialized with {GridRegions} grid regions, " +
                        "{BuildingTypes} building type baselines",
                _gridEmissionFactors.Count, _ashrae901Baselines.Count);
        }

        #region Building Profile Management

        /// <summary>
        /// Registers a building profile for energy analysis.
        /// </summary>
        public void RegisterBuilding(BuildingProfile building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            lock (_lockObject)
            {
                _buildings[building.BuildingId] = building;
            }
            Logger.Info("Registered building {Id} ({Name}): {Area}m2, type={Type}, region={Region}",
                building.BuildingId, building.Name, building.GrossFloorAreaSqM,
                building.BuildingType, building.Region);
        }

        #endregion

        #region Energy Use Intensity

        /// <summary>
        /// Calculates the Energy Use Intensity (EUI) for a building in kWh/m2/year.
        /// EUI is the primary metric for building energy performance comparison.
        /// Annualizes from available data if less than 12 months of readings exist.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <returns>EUI in kWh/m2/year, or -1 if insufficient data.</returns>
        public double CalculateEnergyUseIntensity(string buildingId)
        {
            BuildingProfile building;
            lock (_lockObject)
            {
                if (!_buildings.TryGetValue(buildingId, out building))
                {
                    Logger.Warn("Building {Id} not found for EUI calculation.", buildingId);
                    return -1;
                }
            }

            if (building.GrossFloorAreaSqM <= 0)
            {
                Logger.Warn("Building {Id} has zero floor area. Cannot calculate EUI.", buildingId);
                return -1;
            }

            // Get all energy readings for the past year
            double totalKWh = 0;
            double dataSpanDays = 0;

            var powerSensors = _sensorEngine.GetRegisteredSensors()
                .Where(s => s.Type == SensorCategory.Power &&
                            building.MeterSensorIds.Contains(s.Id, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (powerSensors.Count == 0)
            {
                Logger.Warn("No power meters registered for building {Id}.", buildingId);
                return -1;
            }

            DateTime yearAgo = DateTime.UtcNow.AddYears(-1);
            DateTime now = DateTime.UtcNow;

            foreach (var sensor in powerSensors)
            {
                var readings = _sensorEngine.GetHistoricalReadings(sensor.Id, yearAgo, now);
                if (readings.Count < 2) continue;

                // Trapezoidal integration of power (kW) over time to get energy (kWh)
                for (int i = 1; i < readings.Count; i++)
                {
                    double dtHours = (readings[i].Timestamp - readings[i - 1].Timestamp).TotalHours;
                    double avgKW = (readings[i].Value + readings[i - 1].Value) / 2.0;
                    totalKWh += avgKW * dtHours;
                }

                double sensorSpanDays = (readings[readings.Count - 1].Timestamp - readings[0].Timestamp).TotalDays;
                if (sensorSpanDays > dataSpanDays)
                    dataSpanDays = sensorSpanDays;
            }

            if (dataSpanDays < 1)
            {
                Logger.Warn("Insufficient data span ({Days} days) for EUI calculation.", dataSpanDays);
                return -1;
            }

            // Annualize if less than 365 days of data
            double annualKWh = totalKWh * (365.0 / dataSpanDays);
            double eui = annualKWh / building.GrossFloorAreaSqM;

            Logger.Info("Building {Id} EUI: {EUI:F1} kWh/m2/yr (based on {Days:F0} days of data, " +
                        "{Sensors} meters, {Area}m2)",
                buildingId, eui, dataSpanDays, powerSensors.Count, building.GrossFloorAreaSqM);

            return Math.Round(eui, 1);
        }

        #endregion

        #region Carbon Emissions

        /// <summary>
        /// Tracks operational carbon emissions over a time range, broken down by fuel type.
        /// Uses region-specific grid emission factors for electricity.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <param name="from">Start of time range (UTC).</param>
        /// <param name="to">End of time range (UTC).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Carbon emission breakdown in kgCO2e.</returns>
        public async Task<CarbonEmissionResult> TrackCarbonEmissionsAsync(
            string buildingId,
            DateTime from, DateTime to,
            CancellationToken cancellationToken = default)
        {
            var result = new CarbonEmissionResult { BuildingId = buildingId, From = from, To = to };

            BuildingProfile building;
            lock (_lockObject)
            {
                if (!_buildings.TryGetValue(buildingId, out building))
                {
                    Logger.Warn("Building {Id} not found for carbon tracking.", buildingId);
                    return result;
                }
            }

            // Get grid emission factor for this building's region
            string region = building.Region ?? "Default";
            double gridFactor = _gridEmissionFactors.TryGetValue(region, out var factor)
                ? factor : _gridEmissionFactors["Default"];

            await Task.Run(() =>
            {
                // Electricity emissions
                double electricityKWh = 0;
                var powerSensors = _sensorEngine.GetRegisteredSensors()
                    .Where(s => s.Type == SensorCategory.Power &&
                                building.MeterSensorIds.Contains(s.Id, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                foreach (var sensor in powerSensors)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var readings = _sensorEngine.GetHistoricalReadings(sensor.Id, from, to);
                    if (readings.Count < 2) continue;

                    for (int i = 1; i < readings.Count; i++)
                    {
                        double dtHours = (readings[i].Timestamp - readings[i - 1].Timestamp).TotalHours;
                        double avgKW = (readings[i].Value + readings[i - 1].Value) / 2.0;
                        electricityKWh += avgKW * dtHours;
                    }
                }

                double electricityCO2 = electricityKWh * gridFactor;
                result.ElectricityKWh = Math.Round(electricityKWh, 2);
                result.ElectricityCO2kg = Math.Round(electricityCO2, 2);
                result.GridEmissionFactor = gridFactor;
                result.Region = region;

                // On-site fuel emissions (from building profile)
                foreach (var fuel in building.FuelConsumption)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (_fuelEmissionFactors.TryGetValue(fuel.Key, out var fuelFactor))
                    {
                        // Prorate fuel consumption to the requested time range
                        double periodFraction = (to - from).TotalDays / 365.0;
                        double periodConsumption = fuel.Value * periodFraction;
                        double fuelCO2 = periodConsumption * fuelFactor.Factor;

                        result.FuelBreakdown[fuel.Key] = new FuelEmission
                        {
                            FuelType = fuel.Key,
                            Consumption = Math.Round(periodConsumption, 2),
                            Unit = fuelFactor.Unit.Split('/')[1], // Extract unit after "kgCO2e/"
                            EmissionFactor = fuelFactor.Factor,
                            CO2kg = Math.Round(fuelCO2, 2)
                        };
                        result.FuelCO2kg += fuelCO2;
                    }
                }

                result.FuelCO2kg = Math.Round(result.FuelCO2kg, 2);
                result.TotalCO2kg = Math.Round(result.ElectricityCO2kg + result.FuelCO2kg, 2);

                // Carbon intensity (kgCO2e/m2)
                if (building.GrossFloorAreaSqM > 0)
                {
                    result.CarbonIntensity = Math.Round(
                        result.TotalCO2kg / building.GrossFloorAreaSqM, 2);
                }

            }, cancellationToken).ConfigureAwait(false);

            Logger.Info("Carbon emissions for {Id} ({From} to {To}): total={CO2:F1} kgCO2e, " +
                        "electricity={Elec:F1} kgCO2e ({Grid:F3} factor), fuel={Fuel:F1} kgCO2e",
                buildingId, from, to, result.TotalCO2kg,
                result.ElectricityCO2kg, gridFactor, result.FuelCO2kg);

            return result;
        }

        #endregion

        #region ASHRAE 90.1 Benchmarking

        /// <summary>
        /// Benchmarks a building's energy performance against ASHRAE 90.1 baselines
        /// for its building type. Calculates performance ratio and energy star equivalent.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <returns>Benchmark result with performance ratio and rating.</returns>
        public BenchmarkResult BenchmarkPerformance(string buildingId)
        {
            BuildingProfile building;
            lock (_lockObject)
            {
                if (!_buildings.TryGetValue(buildingId, out building))
                    return new BenchmarkResult { BuildingId = buildingId, Status = "BuildingNotFound" };
            }

            double actualEUI = CalculateEnergyUseIntensity(buildingId);
            if (actualEUI < 0)
                return new BenchmarkResult { BuildingId = buildingId, Status = "InsufficientData" };

            string buildingType = building.BuildingType ?? "Default";
            double baselineEUI = _ashrae901Baselines.TryGetValue(buildingType, out var baseline)
                ? baseline : _ashrae901Baselines["Default"];

            double performanceRatio = actualEUI / baselineEUI;

            // Rating scale: <0.6 = Excellent, <0.8 = Good, <1.0 = Average, <1.2 = Below Average, >1.2 = Poor
            string rating;
            int energyScore; // 0-100 scale similar to Energy Star
            if (performanceRatio < 0.6)
            {
                rating = "Excellent";
                energyScore = 90 + (int)((0.6 - performanceRatio) / 0.6 * 10);
            }
            else if (performanceRatio < 0.8)
            {
                rating = "Good";
                energyScore = 75 + (int)((0.8 - performanceRatio) / 0.2 * 15);
            }
            else if (performanceRatio < 1.0)
            {
                rating = "Average";
                energyScore = 50 + (int)((1.0 - performanceRatio) / 0.2 * 25);
            }
            else if (performanceRatio < 1.2)
            {
                rating = "BelowAverage";
                energyScore = 25 + (int)((1.2 - performanceRatio) / 0.2 * 25);
            }
            else
            {
                rating = "Poor";
                energyScore = Math.Max(1, 25 - (int)((performanceRatio - 1.2) / 0.5 * 25));
            }

            var result = new BenchmarkResult
            {
                BuildingId = buildingId,
                BuildingType = buildingType,
                ActualEUI = Math.Round(actualEUI, 1),
                BaselineEUI = baselineEUI,
                PerformanceRatio = Math.Round(performanceRatio, 3),
                Rating = rating,
                EnergyScore = Math.Min(100, Math.Max(1, energyScore)),
                PotentialSavingsKWh = performanceRatio > 1.0
                    ? Math.Round((actualEUI - baselineEUI) * building.GrossFloorAreaSqM, 0) : 0,
                Status = "Complete"
            };

            // Recommendations based on performance
            if (performanceRatio > 1.0)
            {
                double excessPercent = (performanceRatio - 1.0) * 100;
                result.Recommendations.Add($"Building exceeds ASHRAE 90.1 baseline by {excessPercent:F0}%. " +
                                           "Consider energy audit.");
            }
            if (performanceRatio > 1.2)
            {
                result.Recommendations.Add("HVAC system optimization could reduce consumption by 15-25%.");
                result.Recommendations.Add("Evaluate building envelope thermal performance.");
                result.Recommendations.Add("Review lighting power density against ASHRAE 90.1 Section 9.");
            }

            Logger.Info("Benchmark for {Id}: EUI={EUI:F1} vs baseline={Baseline:F1} kWh/m2/yr, " +
                        "ratio={Ratio:F2}, rating={Rating}, score={Score}",
                buildingId, actualEUI, baselineEUI, performanceRatio, rating, energyScore);

            return result;
        }

        #endregion

        #region Energy Audit Report

        /// <summary>
        /// Generates an ISO 50001 compliant energy audit report including EUI analysis,
        /// end-use breakdown, carbon footprint, benchmark comparison, and recommendations.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Comprehensive energy audit report.</returns>
        public async Task<EnergyAuditReport> GenerateEnergyAuditReport(
            string buildingId,
            CancellationToken cancellationToken = default)
        {
            var report = new EnergyAuditReport
            {
                BuildingId = buildingId,
                ReportDate = DateTime.UtcNow,
                Standard = "ISO 50001:2018"
            };

            BuildingProfile building;
            lock (_lockObject)
            {
                if (!_buildings.TryGetValue(buildingId, out building))
                {
                    report.Status = "BuildingNotFound";
                    return report;
                }
            }

            report.BuildingName = building.Name;
            report.GrossFloorAreaSqM = building.GrossFloorAreaSqM;
            report.BuildingType = building.BuildingType;
            report.Region = building.Region;

            // Section 1: EUI
            report.EUI = CalculateEnergyUseIntensity(buildingId);

            // Section 2: Carbon emissions (past 12 months)
            report.CarbonResult = await TrackCarbonEmissionsAsync(
                buildingId,
                DateTime.UtcNow.AddYears(-1), DateTime.UtcNow,
                cancellationToken).ConfigureAwait(false);

            // Section 3: Benchmark
            report.Benchmark = BenchmarkPerformance(buildingId);

            // Section 4: End-use breakdown estimation
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                EstimateEndUseBreakdown(building, report);
            }, cancellationToken).ConfigureAwait(false);

            // Section 5: Energy conservation measures
            GenerateConservationMeasures(report, building);

            report.Status = "Complete";

            Logger.Info("Generated ISO 50001 energy audit for {Id}: EUI={EUI:F1}, " +
                        "CO2={CO2:F0} kgCO2e/yr, score={Score}",
                buildingId, report.EUI, report.CarbonResult?.TotalCO2kg ?? 0,
                report.Benchmark?.EnergyScore ?? 0);

            return report;
        }

        /// <summary>
        /// Estimates end-use breakdown (HVAC, lighting, plug loads, etc.)
        /// based on building type and regional climate data.
        /// </summary>
        private void EstimateEndUseBreakdown(BuildingProfile building, EnergyAuditReport report)
        {
            // Typical end-use splits by building type (approximations from CBECS/DOE data)
            double hvacPct, lightPct, plugPct, dhwPct, otherPct;

            switch (building.BuildingType?.ToLower())
            {
                case "office_small":
                case "office_medium":
                case "office_large":
                    hvacPct = 0.40; lightPct = 0.25; plugPct = 0.20; dhwPct = 0.05; otherPct = 0.10;
                    break;
                case "hospital":
                    hvacPct = 0.35; lightPct = 0.15; plugPct = 0.25; dhwPct = 0.15; otherPct = 0.10;
                    break;
                case "hotel":
                    hvacPct = 0.35; lightPct = 0.20; plugPct = 0.10; dhwPct = 0.25; otherPct = 0.10;
                    break;
                case "retail":
                    hvacPct = 0.35; lightPct = 0.35; plugPct = 0.15; dhwPct = 0.05; otherPct = 0.10;
                    break;
                default:
                    hvacPct = 0.38; lightPct = 0.22; plugPct = 0.18; dhwPct = 0.10; otherPct = 0.12;
                    break;
            }

            double totalKWh = report.EUI > 0 ? report.EUI * building.GrossFloorAreaSqM : 0;
            report.EndUseBreakdown["HVAC"] = Math.Round(totalKWh * hvacPct, 0);
            report.EndUseBreakdown["Lighting"] = Math.Round(totalKWh * lightPct, 0);
            report.EndUseBreakdown["PlugLoads"] = Math.Round(totalKWh * plugPct, 0);
            report.EndUseBreakdown["DomesticHotWater"] = Math.Round(totalKWh * dhwPct, 0);
            report.EndUseBreakdown["Other"] = Math.Round(totalKWh * otherPct, 0);
        }

        /// <summary>
        /// Generates energy conservation measures (ECMs) based on audit findings.
        /// </summary>
        private void GenerateConservationMeasures(EnergyAuditReport report, BuildingProfile building)
        {
            var ecms = report.ConservationMeasures;

            if (report.Benchmark != null && report.Benchmark.PerformanceRatio > 1.0)
            {
                ecms.Add(new ConservationMeasure
                {
                    Category = "HVAC",
                    Description = "Upgrade to high-efficiency chillers (COP > 6.0)",
                    EstimatedSavingsPercent = 15,
                    PaybackYears = 5.0,
                    Priority = "High"
                });

                ecms.Add(new ConservationMeasure
                {
                    Category = "Lighting",
                    Description = "Retrofit to LED with daylight harvesting controls",
                    EstimatedSavingsPercent = 8,
                    PaybackYears = 2.5,
                    Priority = "High"
                });

                ecms.Add(new ConservationMeasure
                {
                    Category = "Envelope",
                    Description = "Apply solar control window film (SHGC < 0.3)",
                    EstimatedSavingsPercent = 5,
                    PaybackYears = 4.0,
                    Priority = "Medium"
                });

                ecms.Add(new ConservationMeasure
                {
                    Category = "Controls",
                    Description = "Implement demand-controlled ventilation with CO2 sensors",
                    EstimatedSavingsPercent = 10,
                    PaybackYears = 3.0,
                    Priority = "High"
                });

                ecms.Add(new ConservationMeasure
                {
                    Category = "PlugLoads",
                    Description = "Deploy smart power strips and equipment scheduling",
                    EstimatedSavingsPercent = 3,
                    PaybackYears = 1.5,
                    Priority = "Low"
                });
            }

            // Renewable energy recommendation for sunny regions
            if (building.Region != null && (
                building.Region.Equals("Uganda", StringComparison.OrdinalIgnoreCase) ||
                building.Region.Equals("Kenya", StringComparison.OrdinalIgnoreCase) ||
                building.Region.Equals("SouthAfrica", StringComparison.OrdinalIgnoreCase)))
            {
                ecms.Add(new ConservationMeasure
                {
                    Category = "Renewable",
                    Description = $"Install rooftop solar PV ({building.GrossFloorAreaSqM * 0.1:F0} m2 " +
                                  "array, ~5 kWh/m2/day in East Africa)",
                    EstimatedSavingsPercent = 20,
                    PaybackYears = 6.0,
                    Priority = "Medium"
                });
            }
        }

        #endregion

        #region Energy Prediction

        /// <summary>
        /// Predicts future energy consumption using a linear regression model
        /// trained on historical sensor data and degree-day normalization.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <param name="forecastDays">Number of days to forecast.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Daily energy predictions with confidence intervals.</returns>
        public async Task<EnergyForecast> PredictEnergyConsumption(
            string buildingId, int forecastDays,
            CancellationToken cancellationToken = default)
        {
            var forecast = new EnergyForecast
            {
                BuildingId = buildingId,
                ForecastDays = forecastDays,
                GeneratedAt = DateTime.UtcNow
            };

            BuildingProfile building;
            lock (_lockObject)
            {
                if (!_buildings.TryGetValue(buildingId, out building))
                {
                    forecast.Status = "BuildingNotFound";
                    return forecast;
                }
            }

            await Task.Run(() =>
            {
                // Collect daily energy totals for the past 90 days
                var dailyEnergy = new List<(DateTime Date, double KWh)>();
                DateTime start = DateTime.UtcNow.AddDays(-90);

                var powerSensors = _sensorEngine.GetRegisteredSensors()
                    .Where(s => s.Type == SensorCategory.Power &&
                                building.MeterSensorIds.Contains(s.Id, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                for (int day = 0; day < 90; day++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DateTime dayStart = start.AddDays(day);
                    DateTime dayEnd = dayStart.AddDays(1);
                    double dayKWh = 0;

                    foreach (var sensor in powerSensors)
                    {
                        var readings = _sensorEngine.GetHistoricalReadings(sensor.Id, dayStart, dayEnd);
                        if (readings.Count < 2) continue;

                        for (int i = 1; i < readings.Count; i++)
                        {
                            double dt = (readings[i].Timestamp - readings[i - 1].Timestamp).TotalHours;
                            double avg = (readings[i].Value + readings[i - 1].Value) / 2.0;
                            dayKWh += avg * dt;
                        }
                    }

                    if (dayKWh > 0)
                        dailyEnergy.Add((dayStart, dayKWh));
                }

                if (dailyEnergy.Count < 14)
                {
                    forecast.Status = "InsufficientData";
                    return;
                }

                // Calculate statistics
                double mean = dailyEnergy.Average(d => d.KWh);
                double variance = dailyEnergy.Sum(d => (d.KWh - mean) * (d.KWh - mean)) / dailyEnergy.Count;
                double stdDev = Math.Sqrt(variance);

                // Simple trend (linear regression on day index)
                double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
                for (int i = 0; i < dailyEnergy.Count; i++)
                {
                    sumX += i;
                    sumY += dailyEnergy[i].KWh;
                    sumXY += i * dailyEnergy[i].KWh;
                    sumX2 += i * i;
                }
                int n = dailyEnergy.Count;
                double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
                double intercept = (sumY - slope * sumX) / n;

                // Day-of-week adjustment factors
                var dowFactors = new double[7];
                var dowCounts = new int[7];
                foreach (var day in dailyEnergy)
                {
                    int dow = (int)day.Date.DayOfWeek;
                    dowFactors[dow] += day.KWh;
                    dowCounts[dow]++;
                }
                for (int i = 0; i < 7; i++)
                {
                    dowFactors[i] = dowCounts[i] > 0 ? dowFactors[i] / dowCounts[i] / mean : 1.0;
                }

                // Generate forecast
                double totalForecastKWh = 0;
                for (int day = 0; day < forecastDays; day++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DateTime forecastDate = DateTime.UtcNow.AddDays(day);
                    int dayIndex = dailyEnergy.Count + day;
                    int dow = (int)forecastDate.DayOfWeek;

                    double baseValue = slope * dayIndex + intercept;
                    double adjusted = baseValue * dowFactors[dow];
                    adjusted = Math.Max(0, adjusted); // Energy cannot be negative

                    double lower = adjusted - 1.96 * stdDev;
                    double upper = adjusted + 1.96 * stdDev;

                    forecast.DailyPredictions.Add(new DailyEnergyPrediction
                    {
                        Date = forecastDate,
                        PredictedKWh = Math.Round(adjusted, 1),
                        LowerBoundKWh = Math.Round(Math.Max(0, lower), 1),
                        UpperBoundKWh = Math.Round(upper, 1)
                    });

                    totalForecastKWh += adjusted;
                }

                forecast.TotalPredictedKWh = Math.Round(totalForecastKWh, 1);
                forecast.AverageDailyKWh = Math.Round(totalForecastKWh / forecastDays, 1);
                forecast.TrendSlopeKWhPerDay = Math.Round(slope, 3);
                forecast.Status = "Complete";

            }, cancellationToken).ConfigureAwait(false);

            Logger.Info("Energy forecast for {Id}: {Days} days, total={KWh:F0} kWh, " +
                        "avg={Avg:F0} kWh/day, trend={Slope:F3} kWh/day",
                buildingId, forecastDays, forecast.TotalPredictedKWh,
                forecast.AverageDailyKWh, forecast.TrendSlopeKWhPerDay);

            return forecast;
        }

        #endregion

        #region Renewable Contribution

        /// <summary>
        /// Calculates the percentage of total energy supplied by renewable sources
        /// (solar PV, wind, etc.) based on generation meter readings.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <returns>Renewable contribution percentage and breakdown.</returns>
        public RenewableContribution CalculateRenewableContribution(string buildingId)
        {
            var result = new RenewableContribution { BuildingId = buildingId };

            BuildingProfile building;
            lock (_lockObject)
            {
                if (!_buildings.TryGetValue(buildingId, out building))
                    return result;
            }

            DateTime monthAgo = DateTime.UtcNow.AddDays(-30);
            DateTime now = DateTime.UtcNow;
            double totalConsumptionKWh = 0;
            double totalRenewableKWh = 0;

            var powerSensors = _sensorEngine.GetRegisteredSensors()
                .Where(s => s.Type == SensorCategory.Power)
                .ToList();

            foreach (var sensor in powerSensors)
            {
                var readings = _sensorEngine.GetHistoricalReadings(sensor.Id, monthAgo, now);
                if (readings.Count < 2) continue;

                double sensorKWh = 0;
                for (int i = 1; i < readings.Count; i++)
                {
                    double dt = (readings[i].Timestamp - readings[i - 1].Timestamp).TotalHours;
                    double avg = (readings[i].Value + readings[i - 1].Value) / 2.0;
                    sensorKWh += avg * dt;
                }

                bool isRenewable = sensor.Name.Contains("Solar", StringComparison.OrdinalIgnoreCase) ||
                                   sensor.Name.Contains("PV", StringComparison.OrdinalIgnoreCase) ||
                                   sensor.Name.Contains("Wind", StringComparison.OrdinalIgnoreCase) ||
                                   building.RenewableSensorIds.Contains(sensor.Id, StringComparer.OrdinalIgnoreCase);

                if (isRenewable)
                {
                    totalRenewableKWh += sensorKWh;
                    result.BySource[sensor.Name] = Math.Round(sensorKWh, 2);
                }

                totalConsumptionKWh += sensorKWh;
            }

            result.TotalConsumptionKWh = Math.Round(totalConsumptionKWh, 2);
            result.TotalRenewableKWh = Math.Round(totalRenewableKWh, 2);
            result.RenewablePercentage = totalConsumptionKWh > 0
                ? Math.Round(totalRenewableKWh / totalConsumptionKWh * 100, 1) : 0;
            result.GridKWh = Math.Round(totalConsumptionKWh - totalRenewableKWh, 2);

            Logger.Info("Renewable contribution for {Id}: {Pct:F1}% ({Renewable:F0} of {Total:F0} kWh)",
                buildingId, result.RenewablePercentage, totalRenewableKWh, totalConsumptionKWh);

            return result;
        }

        #endregion

        #region Lifecycle Carbon Analysis

        /// <summary>
        /// Performs whole lifecycle carbon analysis (embodied + operational) over the
        /// building's expected lifespan per EN 15978 methodology.
        /// Stages: A1-A3 (Product), A4-A5 (Construction), B1-B7 (Use), C1-C4 (End of Life).
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Lifecycle carbon analysis result in kgCO2e.</returns>
        public async Task<LifecycleCarbonResult> LifecycleCarbonAnalysis(
            string buildingId,
            CancellationToken cancellationToken = default)
        {
            var result = new LifecycleCarbonResult { BuildingId = buildingId };

            BuildingProfile building;
            lock (_lockObject)
            {
                if (!_buildings.TryGetValue(buildingId, out building))
                {
                    result.Status = "BuildingNotFound";
                    return result;
                }
            }

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Stage A1-A3: Embodied carbon in materials (Product stage)
                double embodiedCO2 = 0;
                foreach (var material in building.MaterialQuantities)
                {
                    if (_embodiedCarbonFactors.TryGetValue(material.Key, out var factor))
                    {
                        double materialCO2 = material.Value * factor;
                        embodiedCO2 += materialCO2;
                        result.MaterialBreakdown[material.Key] = Math.Round(materialCO2, 0);
                    }
                }
                result.EmbodiedCarbonA1A3 = Math.Round(embodiedCO2, 0);

                // Stage A4-A5: Construction process (estimated as 5% of A1-A3)
                result.ConstructionCarbonA4A5 = Math.Round(embodiedCO2 * 0.05, 0);

                // Stage B6: Operational energy over building lifespan
                double annualEUI = CalculateEnergyUseIntensity(buildingId);
                if (annualEUI <= 0) annualEUI = 170; // Default fallback

                string region = building.Region ?? "Default";
                double gridFactor = _gridEmissionFactors.TryGetValue(region, out var gf) ? gf : 0.4;

                double annualOperationalCO2 = annualEUI * building.GrossFloorAreaSqM * gridFactor;
                int lifespanYears = building.DesignLifeYears > 0 ? building.DesignLifeYears : 50;

                // Account for grid decarbonization trend (~2% per year reduction)
                double totalOperationalCO2 = 0;
                for (int year = 0; year < lifespanYears; year++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    double yearFactor = gridFactor * Math.Pow(0.98, year);
                    totalOperationalCO2 += annualEUI * building.GrossFloorAreaSqM * yearFactor;
                }
                result.OperationalCarbonB6 = Math.Round(totalOperationalCO2, 0);

                // Stage B1-B5: Maintenance, repair, replacement (estimated as 15% of A1-A3 over lifespan)
                result.MaintenanceCarbonB1B5 = Math.Round(embodiedCO2 * 0.15, 0);

                // Stage C1-C4: End of life (demolition, transport, disposal)
                result.EndOfLifeCarbonC1C4 = Math.Round(embodiedCO2 * 0.03, 0);

                // Totals
                result.TotalLifecycleCO2 = Math.Round(
                    result.EmbodiedCarbonA1A3 +
                    result.ConstructionCarbonA4A5 +
                    result.MaintenanceCarbonB1B5 +
                    result.OperationalCarbonB6 +
                    result.EndOfLifeCarbonC1C4, 0);

                result.OperationalPercentage = result.TotalLifecycleCO2 > 0
                    ? Math.Round(result.OperationalCarbonB6 / result.TotalLifecycleCO2 * 100, 1) : 0;
                result.EmbodiedPercentage = result.TotalLifecycleCO2 > 0
                    ? Math.Round((result.EmbodiedCarbonA1A3 + result.ConstructionCarbonA4A5) /
                                 result.TotalLifecycleCO2 * 100, 1) : 0;

                result.LifespanYears = lifespanYears;
                result.CarbonPerSqM = building.GrossFloorAreaSqM > 0
                    ? Math.Round(result.TotalLifecycleCO2 / building.GrossFloorAreaSqM, 0) : 0;
                result.Status = "Complete";

            }, cancellationToken).ConfigureAwait(false);

            Logger.Info("Lifecycle carbon for {Id}: total={Total:F0} kgCO2e over {Years} years, " +
                        "embodied={Embodied:F0} ({EmbPct:F1}%), operational={Oper:F0} ({OpPct:F1}%)",
                buildingId, result.TotalLifecycleCO2, result.LifespanYears,
                result.EmbodiedCarbonA1A3, result.EmbodiedPercentage,
                result.OperationalCarbonB6, result.OperationalPercentage);

            return result;
        }

        #endregion

        #region WELL Building Score

        /// <summary>
        /// Calculates a WELL Building Standard score covering Air, Water, Light,
        /// and Thermal Comfort categories. Based on real-time sensor data.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>WELL score breakdown by category.</returns>
        public async Task<WellBuildingScore> WELLBuildingScoreAsync(
            string buildingId,
            CancellationToken cancellationToken = default)
        {
            var score = new WellBuildingScore { BuildingId = buildingId };

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Air Quality (CO2, VOC levels)
                var co2Readings = GetSensorReadingsByType(SensorCategory.CO2);
                if (co2Readings.Count > 0)
                {
                    double avgCO2 = co2Readings.Average(r => r.Value);
                    score.AirScore = avgCO2 <= 600 ? 100 :
                                     avgCO2 <= 800 ? 80 :
                                     avgCO2 <= 1000 ? 60 :
                                     avgCO2 <= 1500 ? 30 : 10;
                    score.AirDetails = $"Avg CO2: {avgCO2:F0} ppm (WELL target: <800 ppm)";
                }

                // Water Quality (flow monitoring, temperature for Legionella prevention)
                var waterSensors = _sensorEngine.GetRegisteredSensors()
                    .Where(s => s.Type == SensorCategory.Water)
                    .ToList();
                if (waterSensors.Count > 0)
                {
                    // WELL requires monitoring and filtration documentation
                    score.WaterScore = 70; // Base score for having monitoring
                    score.WaterDetails = $"Active water monitoring on {waterSensors.Count} points.";
                }
                else
                {
                    score.WaterScore = 30;
                    score.WaterDetails = "No water quality monitoring detected.";
                }

                // Light (illuminance levels per EN 12464-1 / WELL v2)
                var lightReadings = GetSensorReadingsByType(SensorCategory.Light);
                if (lightReadings.Count > 0)
                {
                    double avgLux = lightReadings.Average(r => r.Value);
                    score.LightScore = avgLux >= 300 && avgLux <= 500 ? 100 :
                                       avgLux >= 200 && avgLux <= 750 ? 75 :
                                       avgLux >= 100 ? 50 : 25;
                    score.LightDetails = $"Avg illuminance: {avgLux:F0} lux (WELL target: 300-500 lux)";
                }

                // Thermal Comfort (ASHRAE 55 compliance)
                var tempReadings = GetSensorReadingsByType(SensorCategory.Temperature);
                var humidityReadings = GetSensorReadingsByType(SensorCategory.Humidity);
                if (tempReadings.Count > 0)
                {
                    double avgTemp = tempReadings.Average(r => r.Value);
                    double avgHumidity = humidityReadings.Count > 0
                        ? humidityReadings.Average(r => r.Value) : 50.0;

                    bool tempOk = avgTemp >= 20 && avgTemp <= 26;
                    bool humidityOk = avgHumidity >= 30 && avgHumidity <= 60;

                    score.ThermalScore = (tempOk && humidityOk) ? 100 :
                                         (tempOk || humidityOk) ? 65 : 30;
                    score.ThermalDetails = $"Avg temp: {avgTemp:F1}degC, humidity: {avgHumidity:F0}% " +
                                           $"(WELL: 20-26degC, 30-60% RH)";
                }

                // Composite WELL score (weighted average)
                int factorCount = 0;
                double totalWeighted = 0;
                if (score.AirScore > 0) { totalWeighted += score.AirScore * 0.30; factorCount++; }
                if (score.WaterScore > 0) { totalWeighted += score.WaterScore * 0.20; factorCount++; }
                if (score.LightScore > 0) { totalWeighted += score.LightScore * 0.25; factorCount++; }
                if (score.ThermalScore > 0) { totalWeighted += score.ThermalScore * 0.25; factorCount++; }

                score.CompositeScore = factorCount > 0 ? Math.Round(totalWeighted / 0.25 / factorCount * 0.25, 1) : 0;

                // WELL certification level estimate
                score.CertificationLevel = score.CompositeScore >= 80 ? "Platinum" :
                                           score.CompositeScore >= 60 ? "Gold" :
                                           score.CompositeScore >= 40 ? "Silver" : "Bronze";

            }, cancellationToken).ConfigureAwait(false);

            Logger.Info("WELL score for {Id}: composite={Score:F1}, air={Air}, water={Water}, " +
                        "light={Light}, thermal={Thermal}, level={Level}",
                buildingId, score.CompositeScore, score.AirScore, score.WaterScore,
                score.LightScore, score.ThermalScore, score.CertificationLevel);

            return score;
        }

        /// <summary>
        /// Gets the latest readings for all sensors of a given type.
        /// </summary>
        private List<SensorReading> GetSensorReadingsByType(SensorCategory category)
        {
            var readings = new List<SensorReading>();
            var sensors = _sensorEngine.GetRegisteredSensors()
                .Where(s => s.Type == category)
                .ToList();

            foreach (var sensor in sensors)
            {
                var reading = _sensorEngine.GetLatestReading(sensor.Id);
                if (reading != null && reading.Quality != SensorDataQuality.Bad)
                    readings.Add(reading);
            }
            return readings;
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Building profile for energy analysis.
    /// </summary>
    public class BuildingProfile
    {
        public string BuildingId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string BuildingType { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public double GrossFloorAreaSqM { get; set; }
        public int DesignLifeYears { get; set; } = 50;
        public List<string> MeterSensorIds { get; set; } = new List<string>();
        public List<string> RenewableSensorIds { get; set; } = new List<string>();
        public Dictionary<string, double> FuelConsumption { get; set; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> MaterialQuantities { get; set; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Monthly energy data for trending.
    /// </summary>
    public class MonthlyEnergy
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public double KWh { get; set; }
        public double PeakKW { get; set; }
        public double HeatingDegreeDays { get; set; }
        public double CoolingDegreeDays { get; set; }
    }

    /// <summary>
    /// Carbon emission tracking result.
    /// </summary>
    public class CarbonEmissionResult
    {
        public string BuildingId { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string Region { get; set; } = string.Empty;
        public double GridEmissionFactor { get; set; }
        public double ElectricityKWh { get; set; }
        public double ElectricityCO2kg { get; set; }
        public double FuelCO2kg { get; set; }
        public double TotalCO2kg { get; set; }
        public double CarbonIntensity { get; set; }
        public Dictionary<string, FuelEmission> FuelBreakdown { get; set; } =
            new Dictionary<string, FuelEmission>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Fuel-specific emission data.
    /// </summary>
    public class FuelEmission
    {
        public string FuelType { get; set; } = string.Empty;
        public double Consumption { get; set; }
        public string Unit { get; set; } = string.Empty;
        public double EmissionFactor { get; set; }
        public double CO2kg { get; set; }
    }

    /// <summary>
    /// Energy benchmark comparison result.
    /// </summary>
    public class BenchmarkResult
    {
        public string BuildingId { get; set; } = string.Empty;
        public string BuildingType { get; set; } = string.Empty;
        public double ActualEUI { get; set; }
        public double BaselineEUI { get; set; }
        public double PerformanceRatio { get; set; }
        public string Rating { get; set; } = string.Empty;
        public int EnergyScore { get; set; }
        public double PotentialSavingsKWh { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new List<string>();
    }

    /// <summary>
    /// ISO 50001 energy audit report.
    /// </summary>
    public class EnergyAuditReport
    {
        public string BuildingId { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public string BuildingType { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public double GrossFloorAreaSqM { get; set; }
        public DateTime ReportDate { get; set; }
        public string Standard { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double EUI { get; set; }
        public CarbonEmissionResult CarbonResult { get; set; }
        public BenchmarkResult Benchmark { get; set; }
        public Dictionary<string, double> EndUseBreakdown { get; set; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public List<ConservationMeasure> ConservationMeasures { get; set; } = new List<ConservationMeasure>();
    }

    /// <summary>
    /// Energy conservation measure recommendation.
    /// </summary>
    public class ConservationMeasure
    {
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double EstimatedSavingsPercent { get; set; }
        public double PaybackYears { get; set; }
        public string Priority { get; set; } = string.Empty;
    }

    /// <summary>
    /// Energy consumption forecast result.
    /// </summary>
    public class EnergyForecast
    {
        public string BuildingId { get; set; } = string.Empty;
        public int ForecastDays { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public double TotalPredictedKWh { get; set; }
        public double AverageDailyKWh { get; set; }
        public double TrendSlopeKWhPerDay { get; set; }
        public List<DailyEnergyPrediction> DailyPredictions { get; set; } = new List<DailyEnergyPrediction>();
    }

    /// <summary>
    /// Single day energy prediction with confidence interval.
    /// </summary>
    public class DailyEnergyPrediction
    {
        public DateTime Date { get; set; }
        public double PredictedKWh { get; set; }
        public double LowerBoundKWh { get; set; }
        public double UpperBoundKWh { get; set; }
    }

    /// <summary>
    /// Renewable energy contribution analysis result.
    /// </summary>
    public class RenewableContribution
    {
        public string BuildingId { get; set; } = string.Empty;
        public double TotalConsumptionKWh { get; set; }
        public double TotalRenewableKWh { get; set; }
        public double GridKWh { get; set; }
        public double RenewablePercentage { get; set; }
        public Dictionary<string, double> BySource { get; set; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Lifecycle carbon analysis result per EN 15978.
    /// </summary>
    public class LifecycleCarbonResult
    {
        public string BuildingId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int LifespanYears { get; set; }
        public double EmbodiedCarbonA1A3 { get; set; }
        public double ConstructionCarbonA4A5 { get; set; }
        public double MaintenanceCarbonB1B5 { get; set; }
        public double OperationalCarbonB6 { get; set; }
        public double EndOfLifeCarbonC1C4 { get; set; }
        public double TotalLifecycleCO2 { get; set; }
        public double OperationalPercentage { get; set; }
        public double EmbodiedPercentage { get; set; }
        public double CarbonPerSqM { get; set; }
        public Dictionary<string, double> MaterialBreakdown { get; set; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// WELL Building Standard score breakdown.
    /// </summary>
    public class WellBuildingScore
    {
        public string BuildingId { get; set; } = string.Empty;
        public double CompositeScore { get; set; }
        public double AirScore { get; set; }
        public string AirDetails { get; set; } = string.Empty;
        public double WaterScore { get; set; }
        public string WaterDetails { get; set; } = string.Empty;
        public double LightScore { get; set; }
        public string LightDetails { get; set; } = string.Empty;
        public double ThermalScore { get; set; }
        public string ThermalDetails { get; set; } = string.Empty;
        public string CertificationLevel { get; set; } = string.Empty;
    }

    #endregion
}
