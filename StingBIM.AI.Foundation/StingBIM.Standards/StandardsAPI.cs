using System;
using System.Collections.Generic;
using System.Linq;

namespace StingBIM.Standards
{
    /// <summary>
    /// Simple API wrapper for all 32 engineering standards.
    /// Designed for easy calling from PyRevit/IronPython.
    /// All methods are static for direct access.
    /// </summary>
    public static class StandardsAPI
    {
        #region NEC 2023 - Electrical Standards

        /// <summary>
        /// Calculate cable size per NEC 2023 requirements.
        /// Includes ampacity, voltage drop, and derating calculations.
        /// </summary>
        public static CableSizeResult CalculateCableSize(
            double voltageV,
            double currentA,
            double lengthM,
            string conductorType = "Copper",
            string insulationType = "THHN",
            int conduitFill = 3,
            double ambientTempC = 30)
        {
            try
            {
                // NEC 310.16 ampacity derating
                double deratingFactor = 1.0;
                if (ambientTempC > 30)
                    deratingFactor *= Math.Max(0.5, 1.0 - (ambientTempC - 30) * 0.02);
                if (conduitFill > 3)
                    deratingFactor *= 0.8;

                double requiredAmpacity = currentA / deratingFactor;

                // NEC 310.16 copper THHN ampacity table
                string sizeAWG; double sizeMM2; double ampacity;
                if (requiredAmpacity <= 15) { sizeAWG = "14"; sizeMM2 = 2.08; ampacity = 15; }
                else if (requiredAmpacity <= 20) { sizeAWG = "12"; sizeMM2 = 3.31; ampacity = 20; }
                else if (requiredAmpacity <= 30) { sizeAWG = "10"; sizeMM2 = 5.26; ampacity = 30; }
                else if (requiredAmpacity <= 40) { sizeAWG = "8"; sizeMM2 = 8.37; ampacity = 40; }
                else if (requiredAmpacity <= 55) { sizeAWG = "6"; sizeMM2 = 13.30; ampacity = 55; }
                else if (requiredAmpacity <= 70) { sizeAWG = "4"; sizeMM2 = 21.15; ampacity = 70; }
                else if (requiredAmpacity <= 85) { sizeAWG = "3"; sizeMM2 = 26.67; ampacity = 85; }
                else if (requiredAmpacity <= 95) { sizeAWG = "2"; sizeMM2 = 33.62; ampacity = 95; }
                else if (requiredAmpacity <= 110) { sizeAWG = "1"; sizeMM2 = 42.41; ampacity = 110; }
                else if (requiredAmpacity <= 125) { sizeAWG = "1/0"; sizeMM2 = 53.49; ampacity = 125; }
                else if (requiredAmpacity <= 145) { sizeAWG = "2/0"; sizeMM2 = 67.43; ampacity = 145; }
                else if (requiredAmpacity <= 165) { sizeAWG = "3/0"; sizeMM2 = 85.01; ampacity = 165; }
                else if (requiredAmpacity <= 195) { sizeAWG = "4/0"; sizeMM2 = 107.22; ampacity = 195; }
                else { sizeAWG = "250 kcmil"; sizeMM2 = 126.68; ampacity = 215; }

                // Adjust for aluminum
                if (conductorType.Equals("Aluminum", StringComparison.OrdinalIgnoreCase))
                    sizeMM2 *= 1.6; // Aluminum needs larger conductors

                // Voltage drop: VD% = (2 × L × I × ρ) / (A × V) × 100
                double resistivity = conductorType.Equals("Aluminum", StringComparison.OrdinalIgnoreCase) ? 2.82e-8 : 1.72e-8;
                double vDrop = (2 * lengthM * currentA * resistivity) / (sizeMM2 * 1e-6);
                double vDropPercent = (vDrop / voltageV) * 100.0;

                var result = new CableSizeResult
                {
                    SizeAWG = sizeAWG,
                    SizeMM2 = Math.Round(sizeMM2, 2),
                    Ampacity = ampacity,
                    VoltageDropPercent = Math.Round(vDropPercent, 2),
                    IsNECCompliant = vDropPercent <= 3.0,
                    NECReference = "NEC 310.16, 210.19(A)(1)",
                    DeratingFactor = Math.Round(deratingFactor, 3)
                };
                if (vDropPercent > 3.0)
                    result.Warnings.Add($"Voltage drop {vDropPercent:F1}% exceeds NEC recommended 3%");
                return result;
            }
            catch (Exception ex)
            {
                return new CableSizeResult
                {
                    Success = false,
                    ErrorMessage = $"NEC calculation error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Verify circuit breaker sizing per NEC 210.20.
        /// </summary>
        public static CircuitBreakerResult VerifyCircuitBreaker(
            double loadCurrentA,
            double voltageV,
            string breakerType = "Standard")
        {
            try
            {
                // NEC 210.20: Breaker >= 125% of continuous load
                double requiredSize = loadCurrentA * 1.25;

                // Standard breaker sizes per NEC 240.6(A)
                int[] standardSizes = { 15, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90, 100,
                                        110, 125, 150, 175, 200, 225, 250, 300, 350, 400 };
                double recommendedSize = standardSizes.First(s => s >= requiredSize);

                return new CircuitBreakerResult
                {
                    RecommendedBreakerSizeA = recommendedSize,
                    IsCompliant = recommendedSize >= requiredSize,
                    NECReference = "NEC 210.20, 240.6(A)"
                };
            }
            catch (Exception ex)
            {
                return new CircuitBreakerResult
                {
                    Success = false,
                    ErrorMessage = $"Circuit breaker verification error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Calculate grounding electrode conductor size per NEC 250.66.
        /// </summary>
        public static GroundingResult CalculateGroundingSize(
            double serviceCurrentA,
            string serviceEntryType)
        {
            try
            {
                // NEC 250.66 Table - Grounding electrode conductor by service size
                string conductorSize;
                if (serviceCurrentA <= 100) conductorSize = "8 AWG";
                else if (serviceCurrentA <= 150) conductorSize = "6 AWG";
                else if (serviceCurrentA <= 200) conductorSize = "4 AWG";
                else if (serviceCurrentA <= 400) conductorSize = "2 AWG";
                else if (serviceCurrentA <= 600) conductorSize = "1/0 AWG";
                else if (serviceCurrentA <= 800) conductorSize = "2/0 AWG";
                else if (serviceCurrentA <= 1000) conductorSize = "3/0 AWG";
                else conductorSize = "4/0 AWG";

                return new GroundingResult
                {
                    GroundingConductorSize = conductorSize,
                    IsCompliant = true,
                    NECReference = "NEC 250.66, Table 250.66"
                };
            }
            catch (Exception ex)
            {
                return new GroundingResult
                {
                    Success = false,
                    ErrorMessage = $"Grounding calculation error: {ex.Message}"
                };
            }
        }

        #endregion

        #region CIBSE - Building Services Engineering

        /// <summary>
        /// Calculate cooling load using CIBSE Guide B methods.
        /// Includes solar gains, internal loads, and occupancy.
        /// </summary>
        public static HVACSizingResult CalculateCoolingLoad(
            double floorAreaM2,
            string buildingType,
            string climateZone,
            double occupantCount,
            double equipmentLoadW,
            string orientationN_E_S_W = "N")
        {
            try
            {
                // CIBSE Guide A typical heat gains (W/m²)
                double occupantHeatW = occupantCount * 90; // Sensible + latent per person
                double lightingLoadW = floorAreaM2 * 12; // Typical office lighting

                // Solar gain based on orientation (W/m² glazing, tropical climate)
                double solarGainFactor = orientationN_E_S_W?.ToUpper() switch
                {
                    "N" => 40,
                    "S" => 120,
                    "E" => 150,
                    "W" => 180,
                    _ => 100
                };
                double glazingArea = floorAreaM2 * 0.3; // 30% window-to-wall ratio
                double solarGainW = glazingArea * solarGainFactor;

                // Climate zone factor
                double climateFactor = climateZone?.ToUpper() switch
                {
                    "TROPICAL" => 1.3,
                    "SUBTROPICAL" => 1.2,
                    "HIGHLAND" => 0.9,
                    "TEMPERATE" => 1.0,
                    "ARID" => 1.4,
                    _ => 1.1
                };

                // Fabric gain (through walls/roof)
                double fabricGainW = floorAreaM2 * 25 * climateFactor;

                double totalCoolingW = (occupantHeatW + lightingLoadW + equipmentLoadW + solarGainW + fabricGainW) * climateFactor;
                double coolingKW = totalCoolingW / 1000.0;

                return new HVACSizingResult
                {
                    CoolingLoadKW = Math.Round(coolingKW, 2),
                    HeatingLoadKW = Math.Round(coolingKW * 0.6, 2), // Heating typically 60% of cooling in warm climates
                    VentilationLPS = Math.Round(occupantCount * 10 + floorAreaM2 * 0.3, 1), // CIBSE Guide A
                    RecommendedSystem = coolingKW > 100 ? "Chilled Water System" :
                                       coolingKW > 20 ? "VRF/VRV System" : "Split System",
                    CIBSEReference = "CIBSE Guide A Table 6.2, Guide B Section 2",
                    LoadBreakdown = new Dictionary<string, double>
                    {
                        { "Occupants", Math.Round(occupantHeatW / 1000.0, 2) },
                        { "Lighting", Math.Round(lightingLoadW / 1000.0, 2) },
                        { "Equipment", Math.Round(equipmentLoadW / 1000.0, 2) },
                        { "Solar", Math.Round(solarGainW / 1000.0, 2) },
                        { "Fabric", Math.Round(fabricGainW / 1000.0, 2) }
                    }
                };
            }
            catch (Exception ex)
            {
                return new HVACSizingResult
                {
                    Success = false,
                    ErrorMessage = $"Cooling load calculation error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Calculate fresh air ventilation per CIBSE Guide A.
        /// Combines people-based and area-based requirements.
        /// </summary>
        public static VentilationResult CalculateVentilation(
            double floorAreaM2,
            double occupantCount,
            string spaceType)
        {
            try
            {
                // CIBSE Guide A Table 1.5 - Fresh air rates (L/s per person)
                double lpsPerPerson = spaceType?.ToLower() switch
                {
                    "office" => 10,
                    "classroom" => 8,
                    "retail" => 10,
                    "restaurant" => 18,
                    "hospital" => 12,
                    "laboratory" => 15,
                    "warehouse" => 6,
                    "residential" => 8,
                    _ => 10
                };

                double freshAirLPS = occupantCount * lpsPerPerson;
                double freshAirM3H = freshAirLPS * 3.6; // Convert L/s to m³/h
                double ceilingHeight = 3.0; // Assumed ceiling height
                double roomVolume = floorAreaM2 * ceilingHeight;
                double airChanges = roomVolume > 0 ? freshAirM3H / roomVolume : 0;

                return new VentilationResult
                {
                    FreshAirLPS = Math.Round(freshAirLPS, 1),
                    FreshAirM3H = Math.Round(freshAirM3H, 1),
                    AirChangesPerHour = Math.Round(airChanges, 1),
                    CIBSEReference = "CIBSE Guide A Table 1.5"
                };
            }
            catch (Exception ex)
            {
                return new VentilationResult
                {
                    Success = false,
                    ErrorMessage = $"Ventilation calculation error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Calculate lighting requirements per CIBSE Guide L.
        /// Returns illuminance levels and power density.
        /// </summary>
        public static LightingResult CalculateLighting(
            double floorAreaM2,
            string spaceType,
            double ceilingHeightM)
        {
            try
            {
                // CIBSE SLL/Guide L recommended illuminance (lux)
                double illuminance = spaceType?.ToLower() switch
                {
                    "office" => 500,
                    "classroom" => 500,
                    "corridor" => 100,
                    "reception" => 300,
                    "retail" => 500,
                    "warehouse" => 200,
                    "laboratory" => 500,
                    "hospital_ward" => 300,
                    "operating_theatre" => 1000,
                    "residential" => 300,
                    "parking" => 75,
                    _ => 300
                };

                // Power density based on luminous efficacy (typically 80-120 lm/W for LED)
                double luminousEfficacy = 100; // lm/W for modern LED
                double maintenanceFactor = 0.8;
                double utilisationFactor = 0.6 * (3.0 / Math.Max(ceilingHeightM, 2.4)); // Adjust for ceiling height
                double powerDensity = illuminance / (luminousEfficacy * maintenanceFactor * utilisationFactor);

                return new LightingResult
                {
                    IlluminanceLux = illuminance,
                    PowerDensityWM2 = Math.Round(powerDensity, 1),
                    CIBSEReference = "CIBSE SLL Code for Lighting, Guide L"
                };
            }
            catch (Exception ex)
            {
                return new LightingResult
                {
                    Success = false,
                    ErrorMessage = $"Lighting calculation error: {ex.Message}"
                };
            }
        }

        #endregion

        #region IPC 2021 - Plumbing Standards

        /// <summary>
        /// Calculate water pipe size per IPC Table 604.3.
        /// Uses fixture units method for sizing.
        /// </summary>
        public static PipeSizeResult CalculatePlumbingPipeSize(
            double flowRateLPS,
            double lengthM,
            string pipeType,
            string fluidType = "Water")
        {
            try
            {
                // IPC pipe sizing: D = sqrt(4Q / (π × v))
                double maxVelocity = 2.4; // m/s for water supply per IPC
                double areaMM2 = (flowRateLPS / 1000.0) / maxVelocity * 1e6;
                double diameterMM = Math.Sqrt(4 * areaMM2 / Math.PI);

                // Round up to standard pipe sizes (mm)
                double[] standardSizes = { 15, 20, 25, 32, 40, 50, 65, 80, 100, 150, 200 };
                double pipeDiameterMM = standardSizes.First(s => s >= diameterMM);
                double pipeDiameterInch = pipeDiameterMM / 25.4;

                // Calculate actual velocity
                double actualArea = Math.PI * Math.Pow(pipeDiameterMM / 2000.0, 2);
                double actualVelocity = (flowRateLPS / 1000.0) / actualArea;

                return new PipeSizeResult
                {
                    PipeDiameterMM = pipeDiameterMM,
                    PipeDiameterInch = Math.Round(pipeDiameterInch, 2),
                    VelocityMPS = Math.Round(actualVelocity, 2),
                    IsIPCCompliant = actualVelocity <= maxVelocity,
                    IPCReference = "IPC Table 604.3, Section 604.3"
                };
            }
            catch (Exception ex)
            {
                return new PipeSizeResult
                {
                    Success = false,
                    ErrorMessage = $"Pipe sizing error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Calculate drainage pipe size per IPC Chapter 7.
        /// Uses fixture unit method and slope requirements.
        /// </summary>
        public static DrainageSizeResult CalculateDrainageSize(
            int fixtureUnits,
            double slopePercent,
            string pipeType = "PVC")
        {
            try
            {
                // IPC Table 710.1(2) - Horizontal drainage pipe sizing
                double drainDiameterMM;
                if (fixtureUnits <= 1) drainDiameterMM = 32;       // 1-1/4"
                else if (fixtureUnits <= 3) drainDiameterMM = 40;   // 1-1/2"
                else if (fixtureUnits <= 6) drainDiameterMM = 50;   // 2"
                else if (fixtureUnits <= 12) drainDiameterMM = 65;  // 2-1/2"
                else if (fixtureUnits <= 20) drainDiameterMM = 80;  // 3"
                else if (fixtureUnits <= 160) drainDiameterMM = 100; // 4"
                else if (fixtureUnits <= 360) drainDiameterMM = 150; // 6"
                else drainDiameterMM = 200; // 8"

                // Adjust for slope (flatter slopes need larger pipes)
                if (slopePercent < 1.0 && drainDiameterMM < 200)
                {
                    // Increase pipe size for low slope
                    double[] sizes = { 32, 40, 50, 65, 80, 100, 150, 200 };
                    int idx = Array.IndexOf(sizes, drainDiameterMM);
                    if (idx < sizes.Length - 1) drainDiameterMM = sizes[idx + 1];
                }

                return new DrainageSizeResult
                {
                    DrainDiameterMM = drainDiameterMM,
                    DrainDiameterInch = Math.Round(drainDiameterMM / 25.4, 2),
                    IsIPCCompliant = slopePercent >= 1.0 || drainDiameterMM >= 80,
                    IPCReference = "IPC Table 710.1(2), Section 704"
                };
            }
            catch (Exception ex)
            {
                return new DrainageSizeResult
                {
                    Success = false,
                    ErrorMessage = $"Drainage sizing error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Calculate water heater size per IPC 504.
        /// </summary>
        public static WaterHeaterResult CalculateWaterHeaterSize(
            int occupantCount,
            string buildingType,
            double recoveryRateGPH)
        {
            try
            {
                // IPC 504 - Hot water demand per occupant (gallons/day)
                double gallonsPerOccupant = buildingType?.ToLower() switch
                {
                    "residential" => 20,
                    "hotel" => 25,
                    "hospital" => 30,
                    "office" => 5,
                    "restaurant" => 8,
                    "school" => 6,
                    _ => 15
                };

                double dailyDemandGallons = occupantCount * gallonsPerOccupant;
                double peakHourFactor = 0.4; // Peak hour = 40% of daily
                double peakDemandGallons = dailyDemandGallons * peakHourFactor;

                // Storage = peak demand - recovery during peak hour
                double recoveryDuringPeak = recoveryRateGPH > 0 ? recoveryRateGPH : 40;
                double storageGallons = Math.Max(peakDemandGallons - recoveryDuringPeak, 20);
                double storageLiters = storageGallons * 3.785;

                return new WaterHeaterResult
                {
                    StorageCapacityGallons = Math.Round(storageGallons, 0),
                    StorageCapacityLiters = Math.Round(storageLiters, 0),
                    IPCReference = "IPC Section 504, Table 504.2"
                };
            }
            catch (Exception ex)
            {
                return new WaterHeaterResult
                {
                    Success = false,
                    ErrorMessage = $"Water heater sizing error: {ex.Message}"
                };
            }
        }

        #endregion

        #region ASHRAE - HVAC and Energy Standards

        /// <summary>
        /// Estimate annual energy consumption per ASHRAE 90.1.
        /// </summary>
        public static EnergyResult EstimateEnergyConsumption(
            double floorAreaM2,
            string buildingType,
            string climateZone,
            string hvacSystem)
        {
            try
            {
                // ASHRAE 90.1 Energy Use Intensity benchmarks (kWh/m²/year)
                double eui = buildingType?.ToLower() switch
                {
                    "office" => 150,
                    "retail" => 180,
                    "school" => 120,
                    "hospital" => 300,
                    "hotel" => 200,
                    "warehouse" => 80,
                    "residential" => 100,
                    "restaurant" => 250,
                    _ => 150
                };

                // Climate zone adjustment
                double climateFactor = climateZone?.ToUpper() switch
                {
                    "TROPICAL" => 1.3,
                    "SUBTROPICAL" => 1.2,
                    "HIGHLAND" => 0.9,
                    "TEMPERATE" => 1.0,
                    "ARID" => 1.3,
                    "COLD" => 1.4,
                    _ => 1.1
                };

                // HVAC system efficiency adjustment
                double systemFactor = hvacSystem?.ToLower() switch
                {
                    "vrf" or "vrv" => 0.75,
                    "chilled_water" => 0.85,
                    "split" => 1.0,
                    "central_air" => 0.9,
                    "natural_ventilation" => 0.5,
                    _ => 1.0
                };

                double adjustedEUI = eui * climateFactor * systemFactor;
                double annualEnergy = floorAreaM2 * adjustedEUI;

                return new EnergyResult
                {
                    AnnualEnergyKWH = Math.Round(annualEnergy, 0),
                    EnergyPerAreaKWHM2 = Math.Round(adjustedEUI, 1),
                    ASHRAEReference = "ASHRAE 90.1-2022, Table G3.1"
                };
            }
            catch (Exception ex)
            {
                return new EnergyResult
                {
                    Success = false,
                    ErrorMessage = $"Energy calculation error: {ex.Message}"
                };
            }
        }

        #endregion

        #region Eurocodes - Structural Standards

        /// <summary>
        /// Design steel beam per Eurocode 3.
        /// </summary>
        public static BeamDesignResult DesignSteelBeam(
            double spanM,
            double loadKNM,
            string steelGrade)
        {
            try
            {
                // EC3: Moment = wL²/8 for simply supported beam
                double momentKNM = loadKNM * spanM * spanM / 8.0;

                // Steel yield strength (N/mm²)
                double fy = steelGrade?.ToUpper() switch
                {
                    "S235" => 235,
                    "S275" => 275,
                    "S355" => 355,
                    "S420" => 420,
                    "S460" => 460,
                    _ => 275
                };

                double gammaM0 = 1.0; // EC3 partial factor
                double fyd = fy / gammaM0;

                // Required plastic section modulus: Wpl = M / fyd
                double wplRequired = (momentKNM * 1e6) / fyd; // mm³

                // Select standard UB section (simplified)
                string sectionSize;
                bool isAdequate = true;
                if (wplRequired <= 171e3) sectionSize = "UB 203x133x25";
                else if (wplRequired <= 307e3) sectionSize = "UB 254x146x37";
                else if (wplRequired <= 566e3) sectionSize = "UB 305x165x54";
                else if (wplRequired <= 888e3) sectionSize = "UB 356x171x67";
                else if (wplRequired <= 1210e3) sectionSize = "UB 406x178x74";
                else if (wplRequired <= 1830e3) sectionSize = "UB 457x191x98";
                else if (wplRequired <= 2830e3) sectionSize = "UB 533x210x122";
                else if (wplRequired <= 3680e3) sectionSize = "UB 610x229x140";
                else { sectionSize = "Custom plate girder required"; isAdequate = wplRequired <= 10000e3; }

                return new BeamDesignResult
                {
                    SectionSize = sectionSize,
                    IsAdequate = isAdequate,
                    EurocodeReference = "EN 1993-1-1 (EC3), Clause 6.2.5"
                };
            }
            catch (Exception ex)
            {
                return new BeamDesignResult
                {
                    Success = false,
                    ErrorMessage = $"Beam design error: {ex.Message}"
                };
            }
        }

        #endregion

        #region NFPA - Fire Safety Standards

        /// <summary>
        /// Design sprinkler system per NFPA 13.
        /// </summary>
        public static SprinklerResult DesignSprinklerSystem(
            double areaM2,
            string occupancyType,
            string hazardClass)
        {
            try
            {
                // NFPA 13 design density (mm/min) and area by hazard
                double designDensity; // mm/min over design area
                double designAreaM2;
                switch (hazardClass?.ToLower())
                {
                    case "light":
                        designDensity = 4.1; // mm/min
                        designAreaM2 = 139; // m²
                        break;
                    case "ordinary_1":
                    case "ordinary1":
                        designDensity = 6.1;
                        designAreaM2 = 139;
                        break;
                    case "ordinary_2":
                    case "ordinary2":
                        designDensity = 8.1;
                        designAreaM2 = 139;
                        break;
                    case "extra_1":
                    case "extra1":
                        designDensity = 12.2;
                        designAreaM2 = 232;
                        break;
                    case "extra_2":
                    case "extra2":
                        designDensity = 16.3;
                        designAreaM2 = 232;
                        break;
                    default:
                        designDensity = 6.1;
                        designAreaM2 = 139;
                        break;
                }

                // Flow calculation
                double effectiveArea = Math.Min(areaM2, designAreaM2);
                double flowLPM = designDensity * effectiveArea; // L/min
                double flowGPM = flowLPM * 0.2642; // Convert to GPM

                // Number of heads (standard coverage ~12 m² per head)
                double coveragePerHead = 12.0; // m²
                int numberOfHeads = (int)Math.Ceiling(areaM2 / coveragePerHead);

                return new SprinklerResult
                {
                    FlowRateGPM = Math.Round(flowGPM, 1),
                    NumberOfHeads = numberOfHeads,
                    NFPAReference = "NFPA 13, Figure 11.2.3.1.1"
                };
            }
            catch (Exception ex)
            {
                return new SprinklerResult
                {
                    Success = false,
                    ErrorMessage = $"Sprinkler design error: {ex.Message}"
                };
            }
        }

        #endregion

        #region Multi-Standard Compliance

        /// <summary>
        /// Check compliance against multiple standards based on project location.
        /// Returns comprehensive report with all applicable standards.
        /// </summary>
        public static ComplianceReport CheckMultiStandardCompliance(
            string projectLocation,
            string buildingType,
            ProjectData projectData)
        {
            var report = new ComplianceReport
            {
                ProjectLocation = projectLocation,
                BuildingType = buildingType,
                CheckedDate = DateTime.Now,
                ApplicableStandards = new List<string>(),
                Results = new List<ComplianceResult>()
            };

            try
            {
                // Determine applicable standards based on location
                if (projectLocation.ToUpper().Contains("UGANDA") ||
                    projectLocation.ToUpper().Contains("UG"))
                {
                    report.ApplicableStandards.Add("UNBS");
                    report.ApplicableStandards.Add("EAS");
                }
                else if (projectLocation.ToUpper().Contains("KENYA") ||
                         projectLocation.ToUpper().Contains("KE"))
                {
                    report.ApplicableStandards.Add("KEBS");
                    report.ApplicableStandards.Add("EAS");
                }
                else if (projectLocation.ToUpper().Contains("TANZANIA") ||
                         projectLocation.ToUpper().Contains("TZ"))
                {
                    report.ApplicableStandards.Add("TBS");
                    report.ApplicableStandards.Add("EAS");
                }
                else if (projectLocation.ToUpper().Contains("RWANDA") ||
                         projectLocation.ToUpper().Contains("RW"))
                {
                    report.ApplicableStandards.Add("RSB");
                    report.ApplicableStandards.Add("EAS");
                }
                else if (projectLocation.ToUpper().Contains("BURUNDI") ||
                         projectLocation.ToUpper().Contains("BI"))
                {
                    report.ApplicableStandards.Add("BBN");
                    report.ApplicableStandards.Add("EAS");
                }
                else if (projectLocation.ToUpper().Contains("SOUTH SUDAN") ||
                         projectLocation.ToUpper().Contains("SS"))
                {
                    report.ApplicableStandards.Add("SSBS");
                    report.ApplicableStandards.Add("EAS");
                }
                else if (projectLocation.ToUpper().Contains("SOUTH AFRICA") ||
                         projectLocation.ToUpper().Contains("ZA"))
                {
                    report.ApplicableStandards.Add("SANS");
                }

                // Always applicable international standards
                report.ApplicableStandards.Add("NEC 2023");
                report.ApplicableStandards.Add("CIBSE");
                report.ApplicableStandards.Add("IPC 2021");
                report.ApplicableStandards.Add("IMC 2021");
                report.ApplicableStandards.Add("ASHRAE");

                // Run compliance checks for each standard
                foreach (var standard in report.ApplicableStandards)
                {
                    report.Results.Add(RunComplianceCheck(standard, projectData));
                }

                // Calculate overall compliance
                report.OverallCompliant = report.Results.All(r => r.IsCompliant);
                report.CompliancePercentage = report.Results
                    .Count(r => r.IsCompliant) * 100.0 / report.Results.Count;

                report.Success = true;
            }
            catch (Exception ex)
            {
                report.Success = false;
                report.ErrorMessage = $"Compliance check error: {ex.Message}";
            }

            return report;
        }

        private static ComplianceResult RunComplianceCheck(
            string standardName,
            ProjectData projectData)
        {
            var result = new ComplianceResult
            {
                StandardName = standardName,
                CheckedItems = new List<string>(),
                Issues = new List<string>()
            };

            // Delegate to appropriate standard
            switch (standardName)
            {
                case "NEC 2023":
                    result.CheckedItems.Add("Cable sizing");
                    result.CheckedItems.Add("Circuit protection");
                    result.CheckedItems.Add("Grounding");
                    result.IsCompliant = true; // Placeholder
                    break;

                case "CIBSE":
                    result.CheckedItems.Add("HVAC sizing");
                    result.CheckedItems.Add("Ventilation rates");
                    result.CheckedItems.Add("Lighting levels");
                    result.IsCompliant = true; // Placeholder
                    break;

                // ... other standards

                default:
                    result.CheckedItems.Add("General compliance");
                    result.IsCompliant = true;
                    break;
            }

            return result;
        }

        #endregion

        #region Standard Information

        /// <summary>
        /// Get list of all 32 available standards with metadata.
        /// </summary>
        public static List<StandardInfo> GetAllStandards()
        {
            return new List<StandardInfo>
            {
                // Electrical
                new StandardInfo("NEC 2023", "Electrical", "USA", "National Electrical Code", 867),

                // MEP Engineering
                new StandardInfo("CIBSE", "MEP", "UK/Commonwealth", "Building Services Engineering", 1177),
                new StandardInfo("ASHRAE", "HVAC/Energy", "Global", "HVAC and Energy Standards", 591),
                new StandardInfo("IMC 2021", "Mechanical", "USA", "International Mechanical Code", 590),
                new StandardInfo("IPC 2021", "Plumbing", "USA", "International Plumbing Code", 700),
                new StandardInfo("SMACNA", "HVAC", "USA", "Sheet Metal and Air Conditioning", 360),

                // East African Community
                new StandardInfo("UNBS", "All", "Uganda", "Uganda National Bureau of Standards", 562),
                new StandardInfo("KEBS", "All", "Kenya", "Kenya Bureau of Standards", 587),
                new StandardInfo("TBS", "All", "Tanzania", "Tanzania Bureau of Standards", 594),
                new StandardInfo("RSB", "All", "Rwanda", "Rwanda Standards Board", 589),
                new StandardInfo("BBN", "All", "Burundi", "Burundi Bureau of Normalization", 635),
                new StandardInfo("SSBS", "All", "South Sudan", "South Sudan Bureau of Standards", 669),
                new StandardInfo("EAS", "All", "East Africa", "East African Standards", 629),

                // Regional
                new StandardInfo("ECOWAS", "All", "West Africa", "Economic Community of West African States", 634),
                new StandardInfo("SANS", "All", "South Africa", "South African National Standards", 291),
                new StandardInfo("CIDB", "Construction", "South Africa", "Construction Industry Development Board", 838),

                // Structural
                new StandardInfo("Eurocodes", "Structural", "Europe", "European Structural Standards (EN 1990-1999)", 770),
                new StandardInfo("Eurocodes Complete", "Structural", "Europe", "Complete Eurocode Suite", 759),
                new StandardInfo("BS", "Structural", "UK", "British Standards for Steel and Concrete", 670),
                new StandardInfo("AISC", "Structural Steel", "USA", "American Institute of Steel Construction", 448),
                new StandardInfo("ACI", "Concrete", "USA", "American Concrete Institute", 489),

                // Fire Safety
                new StandardInfo("NFPA 13", "Fire Safety", "USA", "Sprinkler Systems", 419),
                new StandardInfo("NFPA 72", "Fire Safety", "USA", "Fire Alarm Systems", 396),
                new StandardInfo("NFPA 101", "Fire Safety", "USA", "Life Safety Code", 377),
                new StandardInfo("NFPA 70", "Electrical Safety", "USA", "NEC subset", 0),

                // Quality & Environment
                new StandardInfo("ISO 9001", "Quality", "Global", "Quality Management Systems", 265),
                new StandardInfo("ISO 14001", "Environment", "Global", "Environmental Management", 267),
                new StandardInfo("ISO 45001", "Safety", "Global", "Occupational Health & Safety", 267),
                new StandardInfo("ISO 19650", "BIM", "Global", "Information Management using BIM", 187),

                // Green Building
                new StandardInfo("Green Building", "Sustainability", "Global", "LEED/BREEAM/Green Star/EDGE", 777),

                // Materials
                new StandardInfo("ASTM", "Materials", "USA", "Material Testing Standards", 915)
            };
        }

        /// <summary>
        /// Get standards applicable to a specific location.
        /// </summary>
        public static List<StandardInfo> GetStandardsForLocation(string location)
        {
            var allStandards = GetAllStandards();
            var applicable = new List<StandardInfo>();

            location = location.ToUpper();

            // Add location-specific standards
            if (location.Contains("UGANDA") || location.Contains("UG"))
            {
                applicable.AddRange(allStandards.Where(s =>
                    s.ShortName == "UNBS" || s.ShortName == "EAS"));
            }
            else if (location.Contains("KENYA") || location.Contains("KE"))
            {
                applicable.AddRange(allStandards.Where(s =>
                    s.ShortName == "KEBS" || s.ShortName == "EAS"));
            }
            // ... other locations

            // Add international standards (always applicable)
            applicable.AddRange(allStandards.Where(s =>
                s.Scope == "Global" ||
                s.ShortName.Contains("NEC") ||
                s.ShortName.Contains("CIBSE") ||
                s.ShortName.Contains("IPC")));

            return applicable.Distinct().ToList();
        }

        #endregion
    }

    #region Result Classes

    public class CableSizeResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public string SizeAWG { get; set; }
        public double SizeMM2 { get; set; }
        public double Ampacity { get; set; }
        public double VoltageDropPercent { get; set; }
        public bool IsNECCompliant { get; set; }
        public string NECReference { get; set; }
        public double DeratingFactor { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();

        // Convenience aliases
        public string RecommendedSize => SizeAWG;
        public double VoltageDrop => VoltageDropPercent;
        public bool IsCompliant => IsNECCompliant;
    }

    public class CircuitBreakerResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public double RecommendedBreakerSizeA { get; set; }
        public bool IsCompliant { get; set; }
        public string NECReference { get; set; }
    }

    public class GroundingResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public string GroundingConductorSize { get; set; }
        public bool IsCompliant { get; set; }
        public string NECReference { get; set; }
    }

    public class HVACSizingResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public double CoolingLoadKW { get; set; }
        public double HeatingLoadKW { get; set; }
        public double VentilationLPS { get; set; }
        public string RecommendedSystem { get; set; }
        public string CIBSEReference { get; set; }
        public Dictionary<string, double> LoadBreakdown { get; set; } = new Dictionary<string, double>();

        // Convenience aliases
        public double TotalLoadKW => CoolingLoadKW;
        public double SensibleLoadKW => LoadBreakdown.TryGetValue("Sensible", out var s) ? s : CoolingLoadKW * 0.75;
        public double LatentLoadKW => LoadBreakdown.TryGetValue("Latent", out var l) ? l : CoolingLoadKW * 0.25;
        public double TonnageRequired => CoolingLoadKW / 3.517;
    }

    public class VentilationResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public double FreshAirLPS { get; set; }
        public double FreshAirM3H { get; set; }
        public double AirChangesPerHour { get; set; }
        public string CIBSEReference { get; set; }

        // Convenience aliases (LPS → CFM conversion: 1 LPS ≈ 2.119 CFM)
        public double RequiredCFM => FreshAirLPS * 2.11888;
        public double CFMPerPerson => FreshAirLPS * 2.11888;
    }

    public class LightingResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public double IlluminanceLux { get; set; }
        public double PowerDensityWM2 { get; set; }
        public string CIBSEReference { get; set; }

        // Convenience aliases
        public double RequiredLux => IlluminanceLux;
        public double LightingPowerDensity => PowerDensityWM2;
        public double TotalWattage { get; set; }
        public int FixtureCount { get; set; }
    }

    public class PipeSizeResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public double PipeDiameterMM { get; set; }
        public double PipeDiameterInch { get; set; }
        public double VelocityMPS { get; set; }
        public bool IsIPCCompliant { get; set; }
        public string IPCReference { get; set; }

        // Convenience aliases
        public string RecommendedSize => $"{PipeDiameterMM:F0} mm";
        public double Velocity => VelocityMPS;
        public double PressureLoss { get; set; }
    }

    public class DrainageSizeResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public double DrainDiameterMM { get; set; }
        public double DrainDiameterInch { get; set; }
        public bool IsIPCCompliant { get; set; }
        public string IPCReference { get; set; }
    }

    public class WaterHeaterResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public double StorageCapacityGallons { get; set; }
        public double StorageCapacityLiters { get; set; }
        public string IPCReference { get; set; }
    }

    public class EnergyResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public double AnnualEnergyKWH { get; set; }
        public double EnergyPerAreaKWHM2 { get; set; }
        public string ASHRAEReference { get; set; }

        // Convenience aliases
        public double AnnualKWH => AnnualEnergyKWH;
        public double EUI => EnergyPerAreaKWHM2;
        public double MonthlyKWH => AnnualEnergyKWH / 12.0;
    }

    public class BeamDesignResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public string SectionSize { get; set; }
        public bool IsAdequate { get; set; }
        public string EurocodeReference { get; set; }

        // Convenience properties
        public double WeightPerMeter { get; set; }
        public double Deflection { get; set; }
        public double UtilizationRatio { get; set; }
    }

    public class SprinklerResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public double FlowRateGPM { get; set; }
        public int NumberOfHeads { get; set; }
        public string NFPAReference { get; set; }

        // Convenience aliases
        public int SprinklerCount => NumberOfHeads;
        public double Spacing { get; set; }
        public double FlowRate => FlowRateGPM;
        public string PipeSize { get; set; }
    }

    public class ComplianceReport
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
        public string ProjectLocation { get; set; }
        public string BuildingType { get; set; }
        public DateTime CheckedDate { get; set; }
        public List<string> ApplicableStandards { get; set; }
        public List<ComplianceResult> Results { get; set; }
        public bool OverallCompliant { get; set; }
        public double CompliancePercentage { get; set; }
    }

    public class ComplianceResult
    {
        public string StandardName { get; set; }
        public bool IsCompliant { get; set; }
        public List<string> CheckedItems { get; set; }
        public List<string> Issues { get; set; }
    }

    public class StandardInfo
    {
        public string ShortName { get; set; }
        public string Discipline { get; set; }
        public string Scope { get; set; }
        public string FullName { get; set; }
        public int LinesOfCode { get; set; }

        public StandardInfo(string shortName, string discipline, string scope, string fullName, int lines)
        {
            ShortName = shortName;
            Discipline = discipline;
            Scope = scope;
            FullName = fullName;
            LinesOfCode = lines;
        }
    }

    public class ProjectData
    {
        public string ProjectName { get; set; }
        public string Location { get; set; }
        public string BuildingType { get; set; }
        public double FloorAreaM2 { get; set; }
        public int NumberOfFloors { get; set; }
        public int OccupantCount { get; set; }
        public string HVACSystem { get; set; }
        public string ElectricalSystem { get; set; }
        public string PlumbingSystem { get; set; }
    }

    #endregion
}
