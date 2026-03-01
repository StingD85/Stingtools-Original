// ===================================================================
// StingBIM Passive House Intelligence Engine
// Passivhaus/PHIUS certification, energy modeling, thermal bridging
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.PassiveHouseIntelligence
{
    #region Enums

    public enum PHStandard { PHI_Classic, PHI_Plus, PHI_Premium, PHIUS_Core, PHIUS_Plus, PHIUS_Zero }
    public enum ClimateZone { CZ1, CZ2, CZ3, CZ4, CZ5, CZ6, CZ7, CZ8 }
    public enum BuildingType { SingleFamily, Multifamily, Office, School, Retail, Mixed }
    public enum ComponentType { Wall, Roof, Floor, Window, Door }
    public enum ThermalBridgeType { Corner, Window, Slab, Roof, Balcony, Penetration }
    public enum VentilationType { HRV, ERV }

    #endregion

    #region Data Models

    public class PassiveHouseProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public PHStandard TargetStandard { get; set; }
        public ClimateZone Climate { get; set; }
        public BuildingType Type { get; set; }
        public double TreatedFloorArea { get; set; }
        public double Volume { get; set; }
        public double FormFactor { get; set; }
        public EnvelopeDesign Envelope { get; set; }
        public VentilationDesign Ventilation { get; set; }
        public EnergyBalance Energy { get; set; }
        public PHCertificationStatus Certification { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class EnvelopeDesign
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<EnvelopeComponent> Components { get; set; } = new();
        public List<ThermalBridge> ThermalBridges { get; set; } = new();
        public double TotalEnvelopeArea { get; set; }
        public double AverageUValue { get; set; }
        public double AirtightnessACH50 { get; set; }
        public double ThermalBridgeLoss { get; set; }
    }

    public class EnvelopeComponent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public ComponentType Type { get; set; }
        public double Area { get; set; }
        public double UValue { get; set; }
        public double RValue => UValue > 0 ? 1 / UValue : 0;
        public List<MaterialLayer> Layers { get; set; } = new();
        public double FrameFraction { get; set; }
        public double GlazingFraction => 1 - FrameFraction;
        public double SHGC { get; set; }
        public double VT { get; set; }
        public double PsiInstall { get; set; }
    }

    public class MaterialLayer
    {
        public string Material { get; set; }
        public double Thickness { get; set; }
        public double Conductivity { get; set; }
        public double RSI => Conductivity > 0 ? Thickness / 1000 / Conductivity : 0;
    }

    public class ThermalBridge
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public ThermalBridgeType Type { get; set; }
        public double Length { get; set; }
        public double PsiValue { get; set; }
        public double HeatLoss => Length * PsiValue;
        public string MitigationStrategy { get; set; }
    }

    public class VentilationDesign
    {
        public VentilationType Type { get; set; }
        public double Airflow { get; set; }
        public double HeatRecoveryEfficiency { get; set; }
        public double MoistureRecoveryEfficiency { get; set; }
        public double FanPower { get; set; }
        public double SFP { get; set; }
        public double AnnualEnergy { get; set; }
        public double SummerBypass { get; set; }
        public bool HasDemandControl { get; set; }
        public double SupplyTemp { get; set; }
        public double ExhaustTemp { get; set; }
    }

    public class EnergyBalance
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Losses
        public double TransmissionLoss { get; set; }
        public double VentilationLoss { get; set; }
        public double InfiltrationLoss { get; set; }
        public double TotalHeatLoss { get; set; }

        // Gains
        public double SolarGain { get; set; }
        public double InternalGain { get; set; }
        public double TotalHeatGain { get; set; }

        // Demands
        public double HeatingDemand { get; set; }
        public double CoolingDemand { get; set; }
        public double PrimaryEnergy { get; set; }
        public double PrimaryEnergyRenewable { get; set; }

        // Peak Loads
        public double PeakHeatingLoad { get; set; }
        public double PeakCoolingLoad { get; set; }

        // Metrics
        public double HeatingDemandPerArea { get; set; }
        public double CoolingDemandPerArea { get; set; }
        public double PrimaryEnergyPerArea { get; set; }
        public double RenewableGeneration { get; set; }
    }

    public class PHCertificationStatus
    {
        public PHStandard Standard { get; set; }
        public bool MeetsHeatingDemand { get; set; }
        public bool MeetsCoolingDemand { get; set; }
        public bool MeetsPrimaryEnergy { get; set; }
        public bool MeetsAirtightness { get; set; }
        public bool MeetsPeakLoad { get; set; }
        public bool IsCertifiable { get; set; }
        public List<string> Deficiencies { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class WindowOptimization
    {
        public string WindowId { get; set; }
        public string Orientation { get; set; }
        public double CurrentArea { get; set; }
        public double OptimalArea { get; set; }
        public double NetEnergyBalance { get; set; }
        public double WinterSolarGain { get; set; }
        public double SummerSolarGain { get; set; }
        public double AnnualHeatLoss { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    public class ShadingAnalysis
    {
        public string WindowId { get; set; }
        public double UnobstructedSolarAccess { get; set; }
        public double WinterShading { get; set; }
        public double SummerShading { get; set; }
        public double OverhangDepth { get; set; }
        public double OptimalOverhangDepth { get; set; }
        public bool HasExternalShading { get; set; }
        public string ShadingType { get; set; }
    }

    public class PHIUSTargets
    {
        public double HeatingDemand { get; set; }
        public double CoolingDemand { get; set; }
        public double PeakHeatingLoad { get; set; }
        public double PeakCoolingLoad { get; set; }
        public double SourceEnergy { get; set; }
        public double Airtightness { get; set; }
    }

    #endregion

    public sealed class PassiveHouseIntelligenceEngine
    {
        private static readonly Lazy<PassiveHouseIntelligenceEngine> _instance =
            new Lazy<PassiveHouseIntelligenceEngine>(() => new PassiveHouseIntelligenceEngine());
        public static PassiveHouseIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, PassiveHouseProject> _projects = new();
        private readonly object _lock = new object();

        // PHI Classic Thresholds (kWh/m²a)
        private readonly Dictionary<PHStandard, (double heating, double cooling, double primary, double ach50)> _phiThresholds = new()
        {
            [PHStandard.PHI_Classic] = (15, 15, 120, 0.6),
            [PHStandard.PHI_Plus] = (15, 15, 45, 0.6),
            [PHStandard.PHI_Premium] = (15, 15, 0, 0.6)
        };

        // PHIUS+ 2021 Climate-Specific (base values, adjusted by climate)
        private readonly Dictionary<ClimateZone, (double heating, double cooling)> _phiusBaseTargets = new()
        {
            [ClimateZone.CZ1] = (4.75, 12.0),
            [ClimateZone.CZ2] = (6.5, 10.5),
            [ClimateZone.CZ3] = (9.0, 9.0),
            [ClimateZone.CZ4] = (12.0, 7.5),
            [ClimateZone.CZ5] = (15.0, 6.0),
            [ClimateZone.CZ6] = (18.0, 4.5),
            [ClimateZone.CZ7] = (21.0, 3.0),
            [ClimateZone.CZ8] = (24.0, 2.0)
        };

        // Typical thermal bridge Psi values (W/mK)
        private readonly Dictionary<ThermalBridgeType, (double standard, double optimized)> _psiValues = new()
        {
            [ThermalBridgeType.Corner] = (0.05, 0.01),
            [ThermalBridgeType.Window] = (0.04, 0.01),
            [ThermalBridgeType.Slab] = (0.20, 0.05),
            [ThermalBridgeType.Roof] = (0.08, 0.02),
            [ThermalBridgeType.Balcony] = (0.50, 0.10),
            [ThermalBridgeType.Penetration] = (0.10, 0.03)
        };

        private PassiveHouseIntelligenceEngine() { }

        public PassiveHouseProject CreateProject(string projectId, string projectName, PHStandard standard,
            ClimateZone climate, BuildingType type, double treatedFloorArea, double volume)
        {
            var project = new PassiveHouseProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                TargetStandard = standard,
                Climate = climate,
                Type = type,
                TreatedFloorArea = treatedFloorArea,
                Volume = volume,
                FormFactor = CalculateFormFactor(treatedFloorArea, volume)
            };

            project.Envelope = new EnvelopeDesign();
            project.Ventilation = new VentilationDesign
            {
                Type = VentilationType.HRV,
                HeatRecoveryEfficiency = 0.85,
                FanPower = 0.45,
                HasDemandControl = true
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        private double CalculateFormFactor(double area, double volume)
        {
            // Simplified form factor calculation
            // A/V ratio approximation
            double estimatedEnvelopeArea = Math.Pow(volume, 2.0 / 3.0) * 6;
            return estimatedEnvelopeArea / area;
        }

        public EnvelopeComponent AddEnvelopeComponent(string projectId, string name, ComponentType type,
            double area, double uValue, double shgc = 0, double vt = 0)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var component = new EnvelopeComponent
                {
                    Name = name,
                    Type = type,
                    Area = area,
                    UValue = uValue,
                    SHGC = shgc,
                    VT = vt
                };

                // Set typical installation Psi based on component type
                component.PsiInstall = type switch
                {
                    ComponentType.Window => 0.04,
                    ComponentType.Door => 0.05,
                    _ => 0
                };

                project.Envelope.Components.Add(component);
                UpdateEnvelopeMetrics(project);
                return component;
            }
        }

        public ThermalBridge AddThermalBridge(string projectId, string name, ThermalBridgeType type,
            double length, double psiValue = -1)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                // Use default if not specified
                if (psiValue < 0)
                {
                    psiValue = _psiValues.GetValueOrDefault(type, (standard: 0.1, optimized: 0.03)).standard;
                }

                var bridge = new ThermalBridge
                {
                    Name = name,
                    Type = type,
                    Length = length,
                    PsiValue = psiValue
                };

                // Add mitigation recommendation
                var optimized = _psiValues.GetValueOrDefault(type, (standard: 0.1, optimized: 0.03)).optimized;
                if (psiValue > optimized * 1.5)
                {
                    bridge.MitigationStrategy = type switch
                    {
                        ThermalBridgeType.Slab => "Install thermal break at slab edge",
                        ThermalBridgeType.Balcony => "Use structural thermal break connector",
                        ThermalBridgeType.Window => "Optimize frame installation detail",
                        ThermalBridgeType.Corner => "Extend exterior insulation at corners",
                        _ => "Review detail for thermal bridge mitigation"
                    };
                }

                project.Envelope.ThermalBridges.Add(bridge);
                UpdateEnvelopeMetrics(project);
                return bridge;
            }
        }

        private void UpdateEnvelopeMetrics(PassiveHouseProject project)
        {
            project.Envelope.TotalEnvelopeArea = project.Envelope.Components.Sum(c => c.Area);
            project.Envelope.AverageUValue = project.Envelope.Components.Sum(c => c.Area * c.UValue) /
                (project.Envelope.TotalEnvelopeArea > 0 ? project.Envelope.TotalEnvelopeArea : 1);
            project.Envelope.ThermalBridgeLoss = project.Envelope.ThermalBridges.Sum(tb => tb.HeatLoss);
        }

        public void SetVentilationSystem(string projectId, VentilationType type, double airflow,
            double heatRecoveryEfficiency, double fanPower)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return;

                project.Ventilation.Type = type;
                project.Ventilation.Airflow = airflow;
                project.Ventilation.HeatRecoveryEfficiency = heatRecoveryEfficiency;
                project.Ventilation.FanPower = fanPower;
                project.Ventilation.SFP = airflow > 0 ? fanPower / airflow * 1000 : 0;
            }
        }

        public async Task<EnergyBalance> CalculateEnergyBalance(string projectId, double heatingDegreeDays,
            double coolingDegreeDays, double solarRadiation)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var energy = new EnergyBalance();

                    // Transmission losses
                    energy.TransmissionLoss = project.Envelope.Components.Sum(c => c.Area * c.UValue) *
                        heatingDegreeDays * 24 / 1000;

                    // Add thermal bridge losses
                    energy.TransmissionLoss += project.Envelope.ThermalBridgeLoss * heatingDegreeDays * 24 / 1000;

                    // Ventilation losses (accounting for heat recovery)
                    double effectiveVentRate = project.Volume * project.Ventilation.Airflow / 3600;
                    double ventHeatLoss = effectiveVentRate * 0.34 * (1 - project.Ventilation.HeatRecoveryEfficiency);
                    energy.VentilationLoss = ventHeatLoss * heatingDegreeDays * 24 / 1000;

                    // Infiltration losses
                    double infiltrationRate = project.Volume * project.Envelope.AirtightnessACH50 / 20; // n50 to natural
                    energy.InfiltrationLoss = infiltrationRate * 0.34 * heatingDegreeDays * 24 / 1000;

                    energy.TotalHeatLoss = energy.TransmissionLoss + energy.VentilationLoss + energy.InfiltrationLoss;

                    // Solar gains (simplified)
                    var windows = project.Envelope.Components.Where(c => c.Type == ComponentType.Window);
                    energy.SolarGain = windows.Sum(w => w.Area * w.SHGC * solarRadiation * 0.9) / 1000;

                    // Internal gains (typical values)
                    energy.InternalGain = project.TreatedFloorArea * 2.1 * 8760 / 1000;

                    energy.TotalHeatGain = energy.SolarGain + energy.InternalGain;

                    // Net demands
                    energy.HeatingDemand = Math.Max(0, energy.TotalHeatLoss - energy.TotalHeatGain * 0.9);
                    energy.CoolingDemand = Math.Max(0, energy.TotalHeatGain * 0.3 - energy.TotalHeatLoss * 0.1);

                    // Per area metrics
                    energy.HeatingDemandPerArea = energy.HeatingDemand / project.TreatedFloorArea;
                    energy.CoolingDemandPerArea = energy.CoolingDemand / project.TreatedFloorArea;

                    // Primary energy (simplified)
                    double auxiliaryEnergy = project.Ventilation.FanPower * 8760 / 1000;
                    double hotWater = project.TreatedFloorArea * 20 / 1000; // 20 kWh/m²a typical
                    energy.PrimaryEnergy = (energy.HeatingDemand + energy.CoolingDemand + auxiliaryEnergy + hotWater) * 2.5;
                    energy.PrimaryEnergyPerArea = energy.PrimaryEnergy / project.TreatedFloorArea;

                    // Peak loads (simplified)
                    double designTemp = -15; // Design temperature
                    energy.PeakHeatingLoad = project.Envelope.Components.Sum(c => c.Area * c.UValue) * (20 - designTemp) +
                        project.Envelope.ThermalBridgeLoss * (20 - designTemp);

                    project.Energy = energy;
                    return energy;
                }
            });
        }

        public async Task<PHCertificationStatus> CheckCertification(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var status = new PHCertificationStatus
                    {
                        Standard = project.TargetStandard
                    };

                    // Get thresholds based on standard
                    (double heatingLimit, double coolingLimit, double primaryLimit, double achLimit) limits;

                    if (project.TargetStandard.ToString().StartsWith("PHI"))
                    {
                        limits = _phiThresholds.GetValueOrDefault(project.TargetStandard, (15, 15, 120, 0.6));
                    }
                    else
                    {
                        var baseTargets = _phiusBaseTargets.GetValueOrDefault(project.Climate, (15, 6));
                        limits = (baseTargets.heating, baseTargets.cooling, 6200, 0.06); // PHIUS uses cfm50/sf
                    }

                    var energy = project.Energy;
                    if (energy != null)
                    {
                        status.MeetsHeatingDemand = energy.HeatingDemandPerArea <= limits.heatingLimit;
                        status.MeetsCoolingDemand = energy.CoolingDemandPerArea <= limits.coolingLimit;
                        status.MeetsPrimaryEnergy = energy.PrimaryEnergyPerArea <= limits.primaryLimit ||
                            project.TargetStandard == PHStandard.PHI_Premium;
                        status.MeetsPeakLoad = energy.PeakHeatingLoad / project.TreatedFloorArea <= 10;

                        if (!status.MeetsHeatingDemand)
                            status.Deficiencies.Add($"Heating demand {energy.HeatingDemandPerArea:F1} exceeds {limits.heatingLimit} kWh/m²a");
                        if (!status.MeetsCoolingDemand)
                            status.Deficiencies.Add($"Cooling demand {energy.CoolingDemandPerArea:F1} exceeds {limits.coolingLimit} kWh/m²a");
                        if (!status.MeetsPrimaryEnergy)
                            status.Deficiencies.Add($"Primary energy {energy.PrimaryEnergyPerArea:F1} exceeds {limits.primaryLimit} kWh/m²a");
                    }

                    status.MeetsAirtightness = project.Envelope.AirtightnessACH50 <= limits.achLimit;
                    if (!status.MeetsAirtightness)
                        status.Deficiencies.Add($"Airtightness {project.Envelope.AirtightnessACH50:F2} exceeds {limits.achLimit} ACH50");

                    status.IsCertifiable = status.MeetsHeatingDemand && status.MeetsCoolingDemand &&
                        status.MeetsPrimaryEnergy && status.MeetsAirtightness;

                    // Recommendations
                    if (!status.MeetsHeatingDemand)
                    {
                        status.Recommendations.Add("Increase wall insulation R-value");
                        status.Recommendations.Add("Improve window U-values");
                        status.Recommendations.Add("Reduce thermal bridging at details");
                    }
                    if (!status.MeetsAirtightness)
                    {
                        status.Recommendations.Add("Improve air barrier continuity");
                        status.Recommendations.Add("Seal penetrations and transitions");
                    }

                    project.Certification = status;
                    return status;
                }
            });
        }

        public WindowOptimization OptimizeWindow(string projectId, string componentId, string orientation,
            double latitude, double solarRadiation)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var window = project.Envelope.Components.FirstOrDefault(c => c.Id == componentId);
                if (window == null) return null;

                var optimization = new WindowOptimization
                {
                    WindowId = componentId,
                    Orientation = orientation,
                    CurrentArea = window.Area
                };

                // Calculate solar gains
                double orientationFactor = orientation.ToLower() switch
                {
                    "south" => 1.0,
                    "east" => 0.55,
                    "west" => 0.55,
                    "north" => 0.25,
                    _ => 0.5
                };

                optimization.WinterSolarGain = window.Area * window.SHGC * solarRadiation * orientationFactor * 0.5; // kWh
                optimization.SummerSolarGain = window.Area * window.SHGC * solarRadiation * orientationFactor * 0.8;

                // Calculate heat loss
                optimization.AnnualHeatLoss = window.Area * window.UValue * 4000 * 24 / 1000; // Simplified HDD

                optimization.NetEnergyBalance = optimization.WinterSolarGain - optimization.AnnualHeatLoss;

                // Optimal area (where net balance is maximized)
                if (orientationFactor >= 0.8 && optimization.NetEnergyBalance > 0)
                {
                    optimization.OptimalArea = window.Area * 1.2;
                    optimization.Recommendations.Add("Consider increasing south-facing glazing for net energy gain");
                }
                else if (orientationFactor < 0.4)
                {
                    optimization.OptimalArea = window.Area * 0.8;
                    optimization.Recommendations.Add("Consider reducing north-facing glazing to minimize heat loss");
                }
                else
                {
                    optimization.OptimalArea = window.Area;
                    optimization.Recommendations.Add("Current glazing area is near optimal for this orientation");
                }

                return optimization;
            }
        }

        public PHIUSTargets GetPHIUSTargets(ClimateZone climate, double treatedFloorArea)
        {
            var baseTargets = _phiusBaseTargets.GetValueOrDefault(climate, (15, 6));

            return new PHIUSTargets
            {
                HeatingDemand = baseTargets.heating,
                CoolingDemand = baseTargets.cooling,
                PeakHeatingLoad = 10, // W/sf typical target
                PeakCoolingLoad = 10,
                SourceEnergy = 6200 / treatedFloorArea, // kBtu/sf
                Airtightness = 0.06 // cfm50/sf
            };
        }
    }
}
