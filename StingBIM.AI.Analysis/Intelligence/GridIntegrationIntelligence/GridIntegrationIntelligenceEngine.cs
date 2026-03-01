// ===================================================================
// StingBIM Grid Integration Intelligence Engine
// Smart grid, demand response, microgrids, energy storage, V2G
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.GridIntegrationIntelligence
{
    #region Enums

    public enum GridConnectionType { Utility, Microgrid, Islanded, Hybrid, VirtualPowerPlant }
    public enum DERType { Solar, Wind, Battery, EVCharger, Generator, FuelCell, CHP, LoadControl }
    public enum TariffType { FlatRate, TimeOfUse, RealTimePricing, DemandCharge, NetMetering, FeedInTariff }
    public enum DemandResponseEvent { PeakShaving, FrequencyRegulation, SpinningReserve, LoadShifting, EmergencyCurtailment }
    public enum GridServiceType { EnergyArbitrage, CapacityMarket, AncillaryServices, DemandResponse, PeakShaving }
    public enum ChargingMode { Standard, Smart, V2G, V2B, V2H }

    #endregion

    #region Data Models

    public class BuildingGridConnection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string BuildingId { get; set; }
        public string BuildingName { get; set; }
        public GridConnectionType ConnectionType { get; set; }
        public double MaxImportCapacity { get; set; } // kW
        public double MaxExportCapacity { get; set; } // kW
        public string UtilityProvider { get; set; }
        public string AccountNumber { get; set; }
        public string MeterType { get; set; }
        public bool HasSmartMeter { get; set; }
        public bool HasNetMetering { get; set; }
        public List<TariffSchedule> Tariffs { get; set; } = new();
        public List<DistributedEnergyResource> DERs { get; set; } = new();
        public GridServiceEnrollment Services { get; set; }
        public DateTime ConnectedSince { get; set; }
    }

    public class TariffSchedule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public TariffType Type { get; set; }
        public List<TariffPeriod> Periods { get; set; } = new();
        public double DemandCharge { get; set; } // $/kW
        public double ConnectionFee { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime EffectiveTo { get; set; }
    }

    public class TariffPeriod
    {
        public string Name { get; set; } // Peak, Off-Peak, Shoulder
        public double Rate { get; set; } // $/kWh
        public double ExportRate { get; set; } // $/kWh for export
        public int StartHour { get; set; }
        public int EndHour { get; set; }
        public List<int> ApplicableDays { get; set; } = new(); // 0=Sunday
        public List<int> ApplicableMonths { get; set; } = new();
    }

    public class DistributedEnergyResource
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public DERType Type { get; set; }
        public double RatedCapacity { get; set; } // kW or kWh for storage
        public double CurrentOutput { get; set; }
        public double Efficiency { get; set; }
        public bool IsControllable { get; set; }
        public bool IsGridInteractive { get; set; }
        public string InverterType { get; set; }
        public DateTime InstallDate { get; set; }
        public double LifetimeGeneration { get; set; }
        public DERStatus Status { get; set; }
    }

    public class DERStatus
    {
        public bool IsOnline { get; set; }
        public double CurrentPower { get; set; }
        public double StateOfCharge { get; set; } // For batteries
        public double Temperature { get; set; }
        public string OperatingMode { get; set; }
        public DateTime LastUpdate { get; set; }
        public List<string> Alerts { get; set; } = new();
    }

    public class GridServiceEnrollment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<DemandResponseProgram> DRPrograms { get; set; } = new();
        public List<AncillaryService> AncillaryServices { get; set; } = new();
        public VirtualPowerPlantMembership VPPMembership { get; set; }
        public double TotalRevenue { get; set; }
        public int EventsParticipated { get; set; }
    }

    public class DemandResponseProgram
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProgramName { get; set; }
        public string UtilityProvider { get; set; }
        public DemandResponseEvent EventType { get; set; }
        public double CommittedCapacity { get; set; } // kW
        public double IncentiveRate { get; set; } // $/kW or $/kWh
        public int MaxEventsPerYear { get; set; }
        public int EventsThisYear { get; set; }
        public double NotificationTime { get; set; } // Hours
        public double MaxDuration { get; set; } // Hours
        public double PenaltyRate { get; set; }
        public DateTime EnrollmentDate { get; set; }
    }

    public class AncillaryService
    {
        public string ServiceName { get; set; }
        public GridServiceType Type { get; set; }
        public double Capacity { get; set; }
        public double MarketPrice { get; set; }
        public double Revenue { get; set; }
        public bool IsActive { get; set; }
    }

    public class VirtualPowerPlantMembership
    {
        public string VPPName { get; set; }
        public string AggregatorName { get; set; }
        public double ContributedCapacity { get; set; }
        public double RevenueShare { get; set; }
        public List<string> ParticipatingDERs { get; set; } = new();
        public DateTime JoinDate { get; set; }
    }

    public class EnergyStorageSystem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Technology { get; set; } // Li-ion, Flow, etc.
        public double CapacityKWh { get; set; }
        public double PowerKW { get; set; }
        public double RoundTripEfficiency { get; set; }
        public double StateOfCharge { get; set; }
        public double StateOfHealth { get; set; }
        public int CycleCount { get; set; }
        public int MaxCycles { get; set; }
        public double DepthOfDischarge { get; set; }
        public BatterySchedule Schedule { get; set; }
        public List<GridServiceType> EnabledServices { get; set; } = new();
    }

    public class BatterySchedule
    {
        public List<BatteryAction> DailySchedule { get; set; } = new();
        public string OptimizationMode { get; set; } // TOU, SelfConsumption, GridServices, Backup
        public double MinSOC { get; set; }
        public double MaxSOC { get; set; }
    }

    public class BatteryAction
    {
        public int Hour { get; set; }
        public string Action { get; set; } // Charge, Discharge, Idle
        public double Power { get; set; }
        public string Reason { get; set; }
    }

    public class EVChargingInfrastructure
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string BuildingId { get; set; }
        public List<EVCharger> Chargers { get; set; } = new();
        public double TotalCapacity { get; set; }
        public bool HasLoadManagement { get; set; }
        public bool SupportsV2G { get; set; }
        public double MaxSimultaneousLoad { get; set; }
        public ChargingOptimization Optimization { get; set; }
    }

    public class EVCharger
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public double MaxPower { get; set; } // kW
        public string ConnectorType { get; set; }
        public ChargingMode Mode { get; set; }
        public bool IsOccupied { get; set; }
        public double CurrentPower { get; set; }
        public string ConnectedVehicle { get; set; }
        public DateTime? SessionStart { get; set; }
        public double EnergyDelivered { get; set; }
    }

    public class ChargingOptimization
    {
        public bool SmartChargingEnabled { get; set; }
        public bool V2GEnabled { get; set; }
        public double MaxBuildingLoad { get; set; }
        public string PricingStrategy { get; set; }
        public List<ChargingSchedule> Schedules { get; set; } = new();
    }

    public class ChargingSchedule
    {
        public string VehicleId { get; set; }
        public DateTime DepartureTime { get; set; }
        public double RequiredSOC { get; set; }
        public List<BatteryAction> ChargingPlan { get; set; } = new();
        public double EstimatedCost { get; set; }
    }

    public class MicrogridConfiguration
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public double TotalGenerationCapacity { get; set; }
        public double TotalStorageCapacity { get; set; }
        public double TotalLoadCapacity { get; set; }
        public List<string> DERIds { get; set; } = new();
        public List<string> CriticalLoads { get; set; } = new();
        public bool CanIsland { get; set; }
        public double IslandingDuration { get; set; } // Hours at full load
        public MicrogridController Controller { get; set; }
    }

    public class MicrogridController
    {
        public string ControllerType { get; set; }
        public string OperatingMode { get; set; } // GridConnected, Islanded, Transition
        public double GridImport { get; set; }
        public double GridExport { get; set; }
        public double LocalGeneration { get; set; }
        public double LocalConsumption { get; set; }
        public double SelfConsumptionRate { get; set; }
        public bool IsStable { get; set; }
        public double Frequency { get; set; }
        public double Voltage { get; set; }
    }

    public class GridOptimizationResult
    {
        public string BuildingId { get; set; }
        public DateTime OptimizationDate { get; set; }
        public double BaselineCost { get; set; }
        public double OptimizedCost { get; set; }
        public double CostSavings { get; set; }
        public double PeakReduction { get; set; }
        public double SelfConsumption { get; set; }
        public double GridRevenue { get; set; }
        public List<OptimizationAction> Actions { get; set; } = new();
    }

    public class OptimizationAction
    {
        public int Hour { get; set; }
        public string Resource { get; set; }
        public string Action { get; set; }
        public double Power { get; set; }
        public double Value { get; set; }
        public string Reason { get; set; }
    }

    public class DemandResponseEventResult
    {
        public string EventId { get; set; }
        public string ProgramName { get; set; }
        public DateTime EventStart { get; set; }
        public DateTime EventEnd { get; set; }
        public double CommittedReduction { get; set; }
        public double ActualReduction { get; set; }
        public double PerformanceRatio { get; set; }
        public double Incentive { get; set; }
        public double Penalty { get; set; }
        public double NetRevenue { get; set; }
        public List<string> ParticipatingResources { get; set; } = new();
    }

    #endregion

    public sealed class GridIntegrationIntelligenceEngine
    {
        private static readonly Lazy<GridIntegrationIntelligenceEngine> _instance =
            new Lazy<GridIntegrationIntelligenceEngine>(() => new GridIntegrationIntelligenceEngine());
        public static GridIntegrationIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, BuildingGridConnection> _connections = new();
        private readonly Dictionary<string, EnergyStorageSystem> _batteries = new();
        private readonly Dictionary<string, MicrogridConfiguration> _microgrids = new();
        private readonly object _lock = new object();

        // Typical TOU rates
        private readonly Dictionary<string, double> _typicalRates = new()
        {
            ["OffPeak"] = 0.08,
            ["Shoulder"] = 0.12,
            ["Peak"] = 0.25,
            ["SuperPeak"] = 0.40
        };

        private GridIntegrationIntelligenceEngine() { }

        public BuildingGridConnection CreateConnection(string buildingId, string buildingName,
            GridConnectionType type, double importCapacity, double exportCapacity, string utility)
        {
            var connection = new BuildingGridConnection
            {
                BuildingId = buildingId,
                BuildingName = buildingName,
                ConnectionType = type,
                MaxImportCapacity = importCapacity,
                MaxExportCapacity = exportCapacity,
                UtilityProvider = utility,
                HasSmartMeter = true,
                ConnectedSince = DateTime.UtcNow,
                Services = new GridServiceEnrollment()
            };

            // Add default TOU tariff
            connection.Tariffs.Add(CreateDefaultTOUTariff());

            lock (_lock) { _connections[connection.Id] = connection; }
            return connection;
        }

        private TariffSchedule CreateDefaultTOUTariff()
        {
            return new TariffSchedule
            {
                Name = "Standard TOU",
                Type = TariffType.TimeOfUse,
                DemandCharge = 15,
                ConnectionFee = 12,
                EffectiveFrom = DateTime.UtcNow,
                EffectiveTo = DateTime.UtcNow.AddYears(1),
                Periods = new List<TariffPeriod>
                {
                    new TariffPeriod
                    {
                        Name = "Off-Peak",
                        Rate = _typicalRates["OffPeak"],
                        ExportRate = 0.04,
                        StartHour = 23,
                        EndHour = 7,
                        ApplicableDays = new List<int> { 0, 1, 2, 3, 4, 5, 6 },
                        ApplicableMonths = Enumerable.Range(1, 12).ToList()
                    },
                    new TariffPeriod
                    {
                        Name = "Shoulder",
                        Rate = _typicalRates["Shoulder"],
                        ExportRate = 0.06,
                        StartHour = 7,
                        EndHour = 14,
                        ApplicableDays = new List<int> { 1, 2, 3, 4, 5 },
                        ApplicableMonths = Enumerable.Range(1, 12).ToList()
                    },
                    new TariffPeriod
                    {
                        Name = "Peak",
                        Rate = _typicalRates["Peak"],
                        ExportRate = 0.10,
                        StartHour = 14,
                        EndHour = 20,
                        ApplicableDays = new List<int> { 1, 2, 3, 4, 5 },
                        ApplicableMonths = Enumerable.Range(1, 12).ToList()
                    },
                    new TariffPeriod
                    {
                        Name = "Evening",
                        Rate = _typicalRates["Shoulder"],
                        ExportRate = 0.06,
                        StartHour = 20,
                        EndHour = 23,
                        ApplicableDays = new List<int> { 1, 2, 3, 4, 5 },
                        ApplicableMonths = Enumerable.Range(1, 12).ToList()
                    }
                }
            };
        }

        public DistributedEnergyResource AddDER(string connectionId, string name, DERType type,
            double capacity, bool controllable, bool gridInteractive)
        {
            lock (_lock)
            {
                if (!_connections.TryGetValue(connectionId, out var connection))
                    return null;

                var der = new DistributedEnergyResource
                {
                    Name = name,
                    Type = type,
                    RatedCapacity = capacity,
                    Efficiency = type switch
                    {
                        DERType.Solar => 0.20,
                        DERType.Wind => 0.35,
                        DERType.Battery => 0.90,
                        DERType.FuelCell => 0.60,
                        DERType.CHP => 0.85,
                        _ => 0.95
                    },
                    IsControllable = controllable,
                    IsGridInteractive = gridInteractive,
                    InstallDate = DateTime.UtcNow,
                    Status = new DERStatus { IsOnline = true, LastUpdate = DateTime.UtcNow }
                };

                connection.DERs.Add(der);
                return der;
            }
        }

        public EnergyStorageSystem AddBattery(string connectionId, string name, string technology,
            double capacityKWh, double powerKW, double efficiency)
        {
            var battery = new EnergyStorageSystem
            {
                Name = name,
                Technology = technology,
                CapacityKWh = capacityKWh,
                PowerKW = powerKW,
                RoundTripEfficiency = efficiency,
                StateOfCharge = 0.5,
                StateOfHealth = 1.0,
                CycleCount = 0,
                MaxCycles = technology.Contains("Li") ? 6000 : 10000,
                DepthOfDischarge = 0.8,
                Schedule = new BatterySchedule
                {
                    OptimizationMode = "TOU",
                    MinSOC = 0.1,
                    MaxSOC = 0.9
                },
                EnabledServices = new List<GridServiceType> { GridServiceType.PeakShaving, GridServiceType.EnergyArbitrage }
            };

            lock (_lock)
            {
                _batteries[battery.Id] = battery;

                if (_connections.TryGetValue(connectionId, out var connection))
                {
                    connection.DERs.Add(new DistributedEnergyResource
                    {
                        Id = battery.Id,
                        Name = name,
                        Type = DERType.Battery,
                        RatedCapacity = capacityKWh,
                        Efficiency = efficiency,
                        IsControllable = true,
                        IsGridInteractive = true,
                        InstallDate = DateTime.UtcNow,
                        Status = new DERStatus { IsOnline = true, StateOfCharge = 0.5, LastUpdate = DateTime.UtcNow }
                    });
                }
            }

            return battery;
        }

        public DemandResponseProgram EnrollInDRProgram(string connectionId, string programName,
            DemandResponseEvent eventType, double capacity, double incentiveRate)
        {
            lock (_lock)
            {
                if (!_connections.TryGetValue(connectionId, out var connection))
                    return null;

                var program = new DemandResponseProgram
                {
                    ProgramName = programName,
                    UtilityProvider = connection.UtilityProvider,
                    EventType = eventType,
                    CommittedCapacity = capacity,
                    IncentiveRate = incentiveRate,
                    MaxEventsPerYear = eventType switch
                    {
                        DemandResponseEvent.EmergencyCurtailment => 10,
                        DemandResponseEvent.PeakShaving => 50,
                        DemandResponseEvent.FrequencyRegulation => 365,
                        _ => 30
                    },
                    NotificationTime = eventType == DemandResponseEvent.EmergencyCurtailment ? 0.5 : 24,
                    MaxDuration = eventType switch
                    {
                        DemandResponseEvent.FrequencyRegulation => 0.25,
                        DemandResponseEvent.EmergencyCurtailment => 4,
                        _ => 2
                    },
                    EnrollmentDate = DateTime.UtcNow
                };

                connection.Services.DRPrograms.Add(program);
                return program;
            }
        }

        public async Task<List<BatteryAction>> OptimizeBatterySchedule(string batteryId,
            TariffSchedule tariff, double[] loadProfile, double[] solarProfile)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_batteries.TryGetValue(batteryId, out var battery))
                        return new List<BatteryAction>();

                    var schedule = new List<BatteryAction>();
                    double soc = battery.StateOfCharge;
                    double maxEnergy = battery.CapacityKWh * battery.DepthOfDischarge;
                    double maxPower = battery.PowerKW;

                    for (int hour = 0; hour < 24; hour++)
                    {
                        double load = hour < loadProfile.Length ? loadProfile[hour] : 0;
                        double solar = hour < solarProfile.Length ? solarProfile[hour] : 0;
                        double netLoad = load - solar;

                        var period = tariff.Periods.FirstOrDefault(p => hour >= p.StartHour && hour < p.EndHour);
                        double rate = period?.Rate ?? _typicalRates["Shoulder"];

                        string action;
                        double power;
                        string reason;

                        if (rate >= _typicalRates["Peak"] && soc > battery.Schedule.MinSOC && netLoad > 0)
                        {
                            // Discharge during peak
                            action = "Discharge";
                            power = Math.Min(maxPower, Math.Min(netLoad, (soc - battery.Schedule.MinSOC) * battery.CapacityKWh));
                            soc -= power / battery.CapacityKWh;
                            reason = "Peak rate - discharging to reduce grid import";
                        }
                        else if (rate <= _typicalRates["OffPeak"] && soc < battery.Schedule.MaxSOC)
                        {
                            // Charge during off-peak
                            action = "Charge";
                            power = Math.Min(maxPower, (battery.Schedule.MaxSOC - soc) * battery.CapacityKWh);
                            soc += power * battery.RoundTripEfficiency / battery.CapacityKWh;
                            reason = "Off-peak rate - charging from grid";
                        }
                        else if (solar > load && soc < battery.Schedule.MaxSOC)
                        {
                            // Charge from excess solar
                            action = "Charge";
                            power = Math.Min(maxPower, Math.Min(solar - load, (battery.Schedule.MaxSOC - soc) * battery.CapacityKWh));
                            soc += power * battery.RoundTripEfficiency / battery.CapacityKWh;
                            reason = "Excess solar - charging";
                        }
                        else
                        {
                            action = "Idle";
                            power = 0;
                            reason = "No optimization opportunity";
                        }

                        schedule.Add(new BatteryAction
                        {
                            Hour = hour,
                            Action = action,
                            Power = power,
                            Reason = reason
                        });
                    }

                    battery.Schedule.DailySchedule = schedule;
                    return schedule;
                }
            });
        }

        public async Task<GridOptimizationResult> OptimizeGridInteraction(string connectionId,
            double[] loadProfile, double[] solarProfile)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_connections.TryGetValue(connectionId, out var connection))
                        return null;

                    var tariff = connection.Tariffs.FirstOrDefault(t => t.EffectiveTo > DateTime.UtcNow);
                    if (tariff == null) return null;

                    var result = new GridOptimizationResult
                    {
                        BuildingId = connection.BuildingId,
                        OptimizationDate = DateTime.UtcNow,
                        Actions = new List<OptimizationAction>()
                    };

                    double baselineCost = 0;
                    double optimizedCost = 0;
                    double peakDemand = 0;
                    double optimizedPeak = 0;
                    double solarUsed = 0;
                    double gridRevenue = 0;

                    var batteries = connection.DERs.Where(d => d.Type == DERType.Battery).ToList();
                    double totalBatteryCapacity = batteries.Sum(b => b.RatedCapacity);

                    for (int hour = 0; hour < 24; hour++)
                    {
                        double load = hour < loadProfile.Length ? loadProfile[hour] : 0;
                        double solar = hour < solarProfile.Length ? solarProfile[hour] : 0;

                        var period = tariff.Periods.FirstOrDefault(p => hour >= p.StartHour && hour < p.EndHour);
                        double rate = period?.Rate ?? _typicalRates["Shoulder"];
                        double exportRate = period?.ExportRate ?? 0.05;

                        // Baseline: no optimization
                        double baselineImport = Math.Max(0, load - solar);
                        double baselineExport = Math.Max(0, solar - load);
                        baselineCost += baselineImport * rate;
                        peakDemand = Math.Max(peakDemand, baselineImport);

                        // Optimized scenario
                        double batteryAction = 0;
                        if (rate >= _typicalRates["Peak"] && totalBatteryCapacity > 0)
                        {
                            batteryAction = -Math.Min(totalBatteryCapacity * 0.2, load * 0.5); // Discharge
                        }
                        else if (rate <= _typicalRates["OffPeak"] && solar < load)
                        {
                            batteryAction = Math.Min(totalBatteryCapacity * 0.15, connection.MaxImportCapacity * 0.3); // Charge
                        }

                        double optimizedImport = Math.Max(0, load - solar + batteryAction);
                        double optimizedExport = Math.Max(0, solar - load - batteryAction);

                        optimizedCost += optimizedImport * rate;
                        gridRevenue += optimizedExport * exportRate;
                        optimizedPeak = Math.Max(optimizedPeak, optimizedImport);
                        solarUsed += Math.Min(solar, load);

                        if (Math.Abs(batteryAction) > 0)
                        {
                            result.Actions.Add(new OptimizationAction
                            {
                                Hour = hour,
                                Resource = "Battery",
                                Action = batteryAction < 0 ? "Discharge" : "Charge",
                                Power = Math.Abs(batteryAction),
                                Value = Math.Abs(batteryAction) * rate,
                                Reason = batteryAction < 0 ? "Peak shaving" : "Off-peak charging"
                            });
                        }
                    }

                    // Add demand charge savings
                    double demandSavings = (peakDemand - optimizedPeak) * tariff.DemandCharge;
                    optimizedCost -= demandSavings;

                    result.BaselineCost = baselineCost + peakDemand * tariff.DemandCharge;
                    result.OptimizedCost = optimizedCost;
                    result.CostSavings = result.BaselineCost - result.OptimizedCost;
                    result.PeakReduction = peakDemand > 0 ? (peakDemand - optimizedPeak) / peakDemand * 100 : 0;
                    result.SelfConsumption = solarProfile.Sum() > 0 ? solarUsed / solarProfile.Sum() * 100 : 0;
                    result.GridRevenue = gridRevenue;

                    return result;
                }
            });
        }

        public DemandResponseEventResult SimulateDREvent(string connectionId, string programId,
            double duration, double[] baselineLoad)
        {
            lock (_lock)
            {
                if (!_connections.TryGetValue(connectionId, out var connection))
                    return null;

                var program = connection.Services.DRPrograms.FirstOrDefault(p => p.Id == programId);
                if (program == null) return null;

                var result = new DemandResponseEventResult
                {
                    EventId = Guid.NewGuid().ToString(),
                    ProgramName = program.ProgramName,
                    EventStart = DateTime.UtcNow,
                    EventEnd = DateTime.UtcNow.AddHours(duration),
                    CommittedReduction = program.CommittedCapacity,
                    ParticipatingResources = new List<string>()
                };

                // Calculate available reduction
                double availableReduction = 0;

                // From controllable loads
                var controllableLoads = connection.DERs.Where(d => d.Type == DERType.LoadControl && d.IsControllable).ToList();
                double loadReduction = controllableLoads.Sum(l => l.RatedCapacity * 0.8);
                availableReduction += loadReduction;
                result.ParticipatingResources.AddRange(controllableLoads.Select(l => l.Name));

                // From batteries
                var batteries = connection.DERs.Where(d => d.Type == DERType.Battery && d.IsControllable).ToList();
                double batteryContribution = batteries.Sum(b => b.RatedCapacity * 0.5);
                availableReduction += batteryContribution;
                result.ParticipatingResources.AddRange(batteries.Select(b => b.Name));

                // From EV chargers (if V2G capable)
                var evChargers = connection.DERs.Where(d => d.Type == DERType.EVCharger && d.IsGridInteractive).ToList();
                double evContribution = evChargers.Sum(e => e.RatedCapacity * 0.3);
                availableReduction += evContribution;
                result.ParticipatingResources.AddRange(evChargers.Select(e => e.Name));

                result.ActualReduction = Math.Min(availableReduction, program.CommittedCapacity);
                result.PerformanceRatio = result.ActualReduction / result.CommittedReduction;

                // Calculate incentive/penalty
                if (result.PerformanceRatio >= 1.0)
                {
                    result.Incentive = result.ActualReduction * duration * program.IncentiveRate;
                    result.Penalty = 0;
                }
                else if (result.PerformanceRatio >= 0.8)
                {
                    result.Incentive = result.ActualReduction * duration * program.IncentiveRate;
                    result.Penalty = 0;
                }
                else
                {
                    result.Incentive = result.ActualReduction * duration * program.IncentiveRate * 0.5;
                    result.Penalty = (program.CommittedCapacity - result.ActualReduction) * program.PenaltyRate;
                }

                result.NetRevenue = result.Incentive - result.Penalty;
                program.EventsThisYear++;

                return result;
            }
        }

        public MicrogridConfiguration CreateMicrogrid(string connectionId, string name,
            List<string> derIds, List<string> criticalLoads)
        {
            lock (_lock)
            {
                if (!_connections.TryGetValue(connectionId, out var connection))
                    return null;

                var ders = connection.DERs.Where(d => derIds.Contains(d.Id)).ToList();

                var microgrid = new MicrogridConfiguration
                {
                    Name = name,
                    DERIds = derIds,
                    CriticalLoads = criticalLoads,
                    TotalGenerationCapacity = ders.Where(d => d.Type == DERType.Solar || d.Type == DERType.Wind || d.Type == DERType.Generator).Sum(d => d.RatedCapacity),
                    TotalStorageCapacity = ders.Where(d => d.Type == DERType.Battery).Sum(d => d.RatedCapacity),
                    TotalLoadCapacity = 0, // Would be calculated from loads
                    CanIsland = ders.Any(d => d.Type == DERType.Battery || d.Type == DERType.Generator),
                    Controller = new MicrogridController
                    {
                        ControllerType = "Distributed",
                        OperatingMode = "GridConnected",
                        IsStable = true,
                        Frequency = 60.0,
                        Voltage = 480
                    }
                };

                // Calculate islanding duration
                double storageKWh = microgrid.TotalStorageCapacity;
                double criticalLoad = 50; // Assume 50 kW critical load
                microgrid.IslandingDuration = storageKWh / criticalLoad;

                _microgrids[microgrid.Id] = microgrid;
                return microgrid;
            }
        }

        public double CalculateGridServicesRevenue(string connectionId, int months)
        {
            lock (_lock)
            {
                if (!_connections.TryGetValue(connectionId, out var connection))
                    return 0;

                double revenue = 0;

                // DR program revenue
                foreach (var program in connection.Services.DRPrograms)
                {
                    double eventsPerMonth = program.MaxEventsPerYear / 12.0;
                    double avgDuration = program.MaxDuration * 0.7;
                    double performance = 0.9;
                    revenue += eventsPerMonth * avgDuration * program.CommittedCapacity * program.IncentiveRate * performance * months;
                }

                // Ancillary services revenue
                foreach (var service in connection.Services.AncillaryServices.Where(s => s.IsActive))
                {
                    revenue += service.Capacity * service.MarketPrice * 24 * 30 * months;
                }

                return revenue;
            }
        }

        public double CalculatePeakShavingPotential(string connectionId)
        {
            lock (_lock)
            {
                if (!_connections.TryGetValue(connectionId, out var connection))
                    return 0;

                double batteryCapacity = connection.DERs
                    .Where(d => d.Type == DERType.Battery)
                    .Sum(d => d.RatedCapacity);

                double controllableLoad = connection.DERs
                    .Where(d => d.Type == DERType.LoadControl && d.IsControllable)
                    .Sum(d => d.RatedCapacity);

                return batteryCapacity * 0.5 + controllableLoad * 0.8;
            }
        }
    }
}
