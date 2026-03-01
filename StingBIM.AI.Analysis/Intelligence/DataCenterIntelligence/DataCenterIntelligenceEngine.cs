// ===================================================================
// StingBIM Data Center Intelligence Engine
// Mission critical facilities, Uptime Institute tiers, PUE analysis
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.DataCenterIntelligence
{
    #region Enums

    public enum UptimeTier { Tier1, Tier2, Tier3, Tier4 }
    public enum CoolingStrategy { CRAC, CRAH, InRow, RearDoor, Immersion, DirectToChip, FreeCooling }
    public enum PowerDistribution { UPS_N, UPS_N1, UPS_2N, UPS_2N1 }
    public enum RackDensity { Low, Medium, High, UltraHigh }
    public enum CableType { Copper, Fiber, Power, Hybrid }
    public enum ContainmentType { HotAisle, ColdAisle, Chimney, None }

    #endregion

    #region Data Models

    public class DataCenterProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public UptimeTier TargetTier { get; set; }
        public double WhiteSpaceArea { get; set; }
        public double TotalITLoad { get; set; }
        public List<DataHall> DataHalls { get; set; } = new();
        public PowerInfrastructure Power { get; set; }
        public CoolingInfrastructure Cooling { get; set; }
        public PUEAnalysis PUE { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class DataHall
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public double Area { get; set; }
        public double CeilingHeight { get; set; }
        public double RaisedFloorHeight { get; set; }
        public int RackCount { get; set; }
        public RackDensity Density { get; set; }
        public double DesignPowerPerRack { get; set; }
        public double TotalPowerCapacity { get; set; }
        public ContainmentType Containment { get; set; }
        public List<CabinetRow> Rows { get; set; } = new();
        public List<string> ComplianceNotes { get; set; } = new();
    }

    public class CabinetRow
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public int CabinetCount { get; set; }
        public double CabinetWidth { get; set; } = 600;
        public double CabinetDepth { get; set; } = 1200;
        public double CabinetHeight { get; set; } = 2100;
        public bool IsHotAisle { get; set; }
        public double AisleWidth { get; set; }
        public List<Cabinet> Cabinets { get; set; } = new();
    }

    public class Cabinet
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public int RackUnits { get; set; } = 42;
        public double DesignPower { get; set; }
        public double CurrentPower { get; set; }
        public double Utilization => DesignPower > 0 ? CurrentPower / DesignPower : 0;
        public List<PowerConnection> PowerConnections { get; set; } = new();
        public List<NetworkConnection> NetworkConnections { get; set; } = new();
        public double SupplyAirTemp { get; set; }
        public double ReturnAirTemp { get; set; }
        public double DeltaT => ReturnAirTemp - SupplyAirTemp;
    }

    public class PowerInfrastructure
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public PowerDistribution Topology { get; set; }
        public double UtilityCapacity { get; set; }
        public int GeneratorCount { get; set; }
        public double GeneratorCapacity { get; set; }
        public double UPSCapacity { get; set; }
        public double BatteryRuntimeMinutes { get; set; }
        public List<PowerPath> PowerPaths { get; set; } = new();
        public double NRedundancy { get; set; }
        public bool ConcurrentlyMaintainable { get; set; }
        public bool FaultTolerant { get; set; }
    }

    public class PowerPath
    {
        public string Name { get; set; }
        public double Capacity { get; set; }
        public string Source { get; set; }
        public List<string> Components { get; set; } = new();
        public bool IsActive { get; set; }
    }

    public class PowerConnection
    {
        public string Feed { get; set; }
        public double Capacity { get; set; }
        public string ConnectorType { get; set; }
        public int Quantity { get; set; }
    }

    public class NetworkConnection
    {
        public CableType Type { get; set; }
        public string Category { get; set; }
        public int PortCount { get; set; }
        public string Speed { get; set; }
    }

    public class CoolingInfrastructure
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public CoolingStrategy PrimaryStrategy { get; set; }
        public CoolingStrategy BackupStrategy { get; set; }
        public double TotalCoolingCapacity { get; set; }
        public double DesignSupplyTemp { get; set; }
        public double DesignReturnTemp { get; set; }
        public double NRedundancy { get; set; }
        public List<CoolingUnit> Units { get; set; } = new();
        public double FreeCoolingHours { get; set; }
        public double WaterUsage { get; set; }
    }

    public class CoolingUnit
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public CoolingStrategy Type { get; set; }
        public double Capacity { get; set; }
        public double SensibleCapacity { get; set; }
        public double AirflowCFM { get; set; }
        public double EER { get; set; }
        public bool IsRedundant { get; set; }
    }

    public class PUEAnalysis
    {
        public double DesignPUE { get; set; }
        public double AnnualPUE { get; set; }
        public double ITLoad { get; set; }
        public double CoolingLoad { get; set; }
        public double PowerDistributionLoss { get; set; }
        public double LightingLoad { get; set; }
        public double OtherLoad { get; set; }
        public double TotalFacilityLoad => ITLoad + CoolingLoad + PowerDistributionLoss + LightingLoad + OtherLoad;
        public double CalculatedPUE => ITLoad > 0 ? TotalFacilityLoad / ITLoad : 0;
        public string PUERating { get; set; }
        public List<PUEOptimization> Optimizations { get; set; } = new();
    }

    public class PUEOptimization
    {
        public string Description { get; set; }
        public double CurrentValue { get; set; }
        public double OptimizedValue { get; set; }
        public double PUEImprovement { get; set; }
        public double AnnualSavings { get; set; }
        public double ImplementationCost { get; set; }
        public double PaybackYears { get; set; }
    }

    public class TierComplianceReport
    {
        public UptimeTier TargetTier { get; set; }
        public bool MeetsRequirements { get; set; }
        public double UptimePercentage { get; set; }
        public double AllowedDowntimeHours { get; set; }
        public List<TierRequirement> Requirements { get; set; } = new();
        public List<string> Gaps { get; set; } = new();
    }

    public class TierRequirement
    {
        public string Category { get; set; }
        public string Requirement { get; set; }
        public bool IsMet { get; set; }
        public string ActualValue { get; set; }
        public string Notes { get; set; }
    }

    #endregion

    public sealed class DataCenterIntelligenceEngine
    {
        private static readonly Lazy<DataCenterIntelligenceEngine> _instance =
            new Lazy<DataCenterIntelligenceEngine>(() => new DataCenterIntelligenceEngine());
        public static DataCenterIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, DataCenterProject> _projects = new();
        private readonly object _lock = new object();

        // Uptime Institute Tier Requirements
        private readonly Dictionary<UptimeTier, (double uptime, double downtime, string redundancy)> _tierRequirements = new()
        {
            [UptimeTier.Tier1] = (99.671, 28.8, "N"),
            [UptimeTier.Tier2] = (99.741, 22.0, "N+1"),
            [UptimeTier.Tier3] = (99.982, 1.6, "N+1, Concurrently Maintainable"),
            [UptimeTier.Tier4] = (99.995, 0.4, "2N+1, Fault Tolerant")
        };

        // Power density by rack classification (kW/rack)
        private readonly Dictionary<RackDensity, (double min, double max, double typical)> _powerDensity = new()
        {
            [RackDensity.Low] = (2, 5, 3),
            [RackDensity.Medium] = (5, 10, 7),
            [RackDensity.High] = (10, 25, 15),
            [RackDensity.UltraHigh] = (25, 100, 40)
        };

        // ASHRAE thermal guidelines
        private readonly Dictionary<string, (double supplyMin, double supplyMax, double rhMin, double rhMax)> _ashraeClasses = new()
        {
            ["A1"] = (15, 32, 20, 80),
            ["A2"] = (10, 35, 20, 80),
            ["A3"] = (5, 40, 8, 85),
            ["A4"] = (5, 45, 8, 90)
        };

        private DataCenterIntelligenceEngine() { }

        public DataCenterProject CreateDataCenterProject(string projectId, string projectName,
            UptimeTier targetTier, double whiteSpaceArea)
        {
            var project = new DataCenterProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                TargetTier = targetTier,
                WhiteSpaceArea = whiteSpaceArea
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public DataHall AddDataHall(string projectId, string name, double area,
            double ceilingHeight, RackDensity density, ContainmentType containment)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var densitySpec = _powerDensity.GetValueOrDefault(density, (5, 10, 7));

                var hall = new DataHall
                {
                    Name = name,
                    Area = area,
                    CeilingHeight = ceilingHeight,
                    RaisedFloorHeight = 24, // inches standard
                    Density = density,
                    DesignPowerPerRack = densitySpec.typical,
                    Containment = containment
                };

                // Calculate rack count based on area
                double rackFootprint = 30; // sq ft per rack including aisle
                hall.RackCount = (int)(area * 0.6 / rackFootprint);
                hall.TotalPowerCapacity = hall.RackCount * hall.DesignPowerPerRack;

                // Add compliance notes based on density
                if (density == RackDensity.High || density == RackDensity.UltraHigh)
                {
                    hall.ComplianceNotes.Add("High density requires enhanced cooling strategy");
                    hall.ComplianceNotes.Add("Consider in-row or rear-door cooling");
                    hall.ComplianceNotes.Add("Monitor for hot spots with CFD analysis");
                }

                project.DataHalls.Add(hall);
                return hall;
            }
        }

        public CabinetRow AddCabinetRow(string projectId, string hallId, string name,
            int cabinetCount, bool isHotAisle, double aisleWidth)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var hall = project.DataHalls.FirstOrDefault(h => h.Id == hallId);
                if (hall == null) return null;

                var row = new CabinetRow
                {
                    Name = name,
                    CabinetCount = cabinetCount,
                    IsHotAisle = isHotAisle,
                    AisleWidth = aisleWidth
                };

                // Create cabinets
                for (int i = 0; i < cabinetCount; i++)
                {
                    row.Cabinets.Add(new Cabinet
                    {
                        Name = $"{name}-{i + 1:D2}",
                        DesignPower = hall.DesignPowerPerRack
                    });
                }

                hall.Rows.Add(row);
                return row;
            }
        }

        public PowerInfrastructure DesignPowerInfrastructure(string projectId, double totalITLoad)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var tierReq = _tierRequirements.GetValueOrDefault(project.TargetTier, (99.671, 28.8, "N"));

                var power = new PowerInfrastructure();

                // Set topology based on tier
                power.Topology = project.TargetTier switch
                {
                    UptimeTier.Tier1 => PowerDistribution.UPS_N,
                    UptimeTier.Tier2 => PowerDistribution.UPS_N1,
                    UptimeTier.Tier3 => PowerDistribution.UPS_N1,
                    UptimeTier.Tier4 => PowerDistribution.UPS_2N1,
                    _ => PowerDistribution.UPS_N
                };

                // Calculate capacity with overhead
                double overhead = power.Topology switch
                {
                    PowerDistribution.UPS_N => 1.2,
                    PowerDistribution.UPS_N1 => 1.5,
                    PowerDistribution.UPS_2N => 2.2,
                    PowerDistribution.UPS_2N1 => 2.5,
                    _ => 1.2
                };

                power.UPSCapacity = totalITLoad * overhead;
                power.UtilityCapacity = power.UPSCapacity * 1.15;
                power.GeneratorCapacity = power.UPSCapacity * 1.25;

                // Generator count
                power.GeneratorCount = project.TargetTier switch
                {
                    UptimeTier.Tier1 => 1,
                    UptimeTier.Tier2 => 2,
                    UptimeTier.Tier3 => 2,
                    UptimeTier.Tier4 => 4,
                    _ => 1
                };

                // Battery runtime
                power.BatteryRuntimeMinutes = project.TargetTier >= UptimeTier.Tier3 ? 15 : 10;

                // Set flags
                power.ConcurrentlyMaintainable = project.TargetTier >= UptimeTier.Tier3;
                power.FaultTolerant = project.TargetTier == UptimeTier.Tier4;
                power.NRedundancy = power.Topology == PowerDistribution.UPS_2N1 ? 2 : 1;

                // Create power paths
                power.PowerPaths.Add(new PowerPath
                {
                    Name = "Path A",
                    Capacity = totalITLoad * 1.2,
                    Source = "Utility A + Generator A",
                    Components = new List<string> { "MV Switchgear A", "Transformer A", "UPS A", "PDU A" },
                    IsActive = true
                });

                if (power.Topology >= PowerDistribution.UPS_2N)
                {
                    power.PowerPaths.Add(new PowerPath
                    {
                        Name = "Path B",
                        Capacity = totalITLoad * 1.2,
                        Source = "Utility B + Generator B",
                        Components = new List<string> { "MV Switchgear B", "Transformer B", "UPS B", "PDU B" },
                        IsActive = true
                    });
                }

                project.Power = power;
                project.TotalITLoad = totalITLoad;
                return power;
            }
        }

        public CoolingInfrastructure DesignCoolingInfrastructure(string projectId, CoolingStrategy primaryStrategy)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var cooling = new CoolingInfrastructure
                {
                    PrimaryStrategy = primaryStrategy,
                    DesignSupplyTemp = 64, // °F
                    DesignReturnTemp = 80  // °F
                };

                // Calculate cooling capacity (IT load + overhead)
                double coolingLoad = project.TotalITLoad * 1.15;

                // Set redundancy based on tier
                cooling.NRedundancy = project.TargetTier switch
                {
                    UptimeTier.Tier1 => 0,
                    UptimeTier.Tier2 => 1,
                    UptimeTier.Tier3 => 1,
                    UptimeTier.Tier4 => 1,
                    _ => 0
                };

                // Calculate unit count
                double unitCapacity = primaryStrategy switch
                {
                    CoolingStrategy.CRAC => 100, // kW
                    CoolingStrategy.CRAH => 250, // kW
                    CoolingStrategy.InRow => 40, // kW
                    _ => 100
                };

                int unitCount = (int)Math.Ceiling(coolingLoad / unitCapacity);
                int redundantUnits = (int)cooling.NRedundancy + 1;
                int totalUnits = unitCount + redundantUnits;

                cooling.TotalCoolingCapacity = totalUnits * unitCapacity;

                // Add cooling units
                for (int i = 0; i < totalUnits; i++)
                {
                    cooling.Units.Add(new CoolingUnit
                    {
                        Name = $"{primaryStrategy}-{i + 1:D2}",
                        Type = primaryStrategy,
                        Capacity = unitCapacity,
                        SensibleCapacity = unitCapacity * 0.85,
                        AirflowCFM = unitCapacity * 150, // Approximate
                        EER = 12,
                        IsRedundant = i >= unitCount
                    });
                }

                // Estimate free cooling hours based on strategy
                cooling.FreeCoolingHours = primaryStrategy == CoolingStrategy.FreeCooling ? 6000 : 2000;

                // Set backup strategy
                cooling.BackupStrategy = primaryStrategy == CoolingStrategy.CRAH ?
                    CoolingStrategy.FreeCooling : CoolingStrategy.CRAH;

                project.Cooling = cooling;
                return cooling;
            }
        }

        public async Task<PUEAnalysis> CalculatePUE(string projectId, double electricityRate)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var pue = new PUEAnalysis
                    {
                        ITLoad = project.TotalITLoad
                    };

                    // Estimate component loads
                    pue.CoolingLoad = project.TotalITLoad * 0.35; // 35% for cooling
                    pue.PowerDistributionLoss = project.TotalITLoad * 0.08; // 8% losses
                    pue.LightingLoad = project.TotalITLoad * 0.02; // 2% lighting
                    pue.OtherLoad = project.TotalITLoad * 0.05; // 5% other

                    // Calculate PUE
                    pue.DesignPUE = pue.CalculatedPUE;

                    // Rate the PUE
                    pue.PUERating = pue.DesignPUE switch
                    {
                        < 1.2 => "Best-in-Class",
                        < 1.4 => "Efficient",
                        < 1.6 => "Average",
                        < 2.0 => "Below Average",
                        _ => "Inefficient"
                    };

                    // Generate optimization recommendations
                    if (project.Cooling?.PrimaryStrategy != CoolingStrategy.FreeCooling)
                    {
                        double savingsKW = project.TotalITLoad * 0.15;
                        pue.Optimizations.Add(new PUEOptimization
                        {
                            Description = "Implement economizer/free cooling",
                            CurrentValue = pue.CoolingLoad,
                            OptimizedValue = pue.CoolingLoad * 0.7,
                            PUEImprovement = 0.1,
                            AnnualSavings = savingsKW * 8760 * electricityRate,
                            ImplementationCost = savingsKW * 500,
                            PaybackYears = (savingsKW * 500) / (savingsKW * 8760 * electricityRate)
                        });
                    }

                    if (project.DataHalls.Any(h => h.Containment == ContainmentType.None))
                    {
                        pue.Optimizations.Add(new PUEOptimization
                        {
                            Description = "Add hot/cold aisle containment",
                            CurrentValue = pue.CoolingLoad,
                            OptimizedValue = pue.CoolingLoad * 0.85,
                            PUEImprovement = 0.05,
                            AnnualSavings = project.TotalITLoad * 0.05 * 8760 * electricityRate,
                            ImplementationCost = project.DataHalls.Sum(h => h.RackCount) * 200
                        });
                    }

                    pue.Optimizations.Add(new PUEOptimization
                    {
                        Description = "Increase supply air temperature to 80°F",
                        CurrentValue = project.Cooling?.DesignSupplyTemp ?? 64,
                        OptimizedValue = 80,
                        PUEImprovement = 0.08,
                        AnnualSavings = project.TotalITLoad * 0.08 * 8760 * electricityRate
                    });

                    project.PUE = pue;
                    return pue;
                }
            });
        }

        public async Task<TierComplianceReport> GenerateTierComplianceReport(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var tierSpec = _tierRequirements.GetValueOrDefault(project.TargetTier, (uptime: 99.671, downtime: 28.8, redundancy: "N"));

                    var report = new TierComplianceReport
                    {
                        TargetTier = project.TargetTier,
                        UptimePercentage = tierSpec.uptime,
                        AllowedDowntimeHours = tierSpec.downtime
                    };

                    // Check power requirements
                    report.Requirements.Add(new TierRequirement
                    {
                        Category = "Power",
                        Requirement = $"Redundancy: {tierSpec.redundancy}",
                        IsMet = project.Power?.Topology switch
                        {
                            PowerDistribution.UPS_2N1 => true,
                            PowerDistribution.UPS_2N => project.TargetTier <= UptimeTier.Tier3,
                            PowerDistribution.UPS_N1 => project.TargetTier <= UptimeTier.Tier2,
                            _ => project.TargetTier == UptimeTier.Tier1
                        },
                        ActualValue = project.Power?.Topology.ToString()
                    });

                    // Check concurrent maintainability
                    if (project.TargetTier >= UptimeTier.Tier3)
                    {
                        report.Requirements.Add(new TierRequirement
                        {
                            Category = "Maintainability",
                            Requirement = "Concurrently Maintainable",
                            IsMet = project.Power?.ConcurrentlyMaintainable ?? false,
                            ActualValue = project.Power?.ConcurrentlyMaintainable.ToString()
                        });
                    }

                    // Check fault tolerance
                    if (project.TargetTier == UptimeTier.Tier4)
                    {
                        report.Requirements.Add(new TierRequirement
                        {
                            Category = "Fault Tolerance",
                            Requirement = "Fault Tolerant Power and Cooling",
                            IsMet = project.Power?.FaultTolerant ?? false,
                            ActualValue = project.Power?.FaultTolerant.ToString()
                        });
                    }

                    // Check cooling redundancy
                    report.Requirements.Add(new TierRequirement
                    {
                        Category = "Cooling",
                        Requirement = project.TargetTier >= UptimeTier.Tier2 ? "N+1 Cooling" : "N Cooling",
                        IsMet = project.Cooling?.NRedundancy >= (project.TargetTier >= UptimeTier.Tier2 ? 1 : 0),
                        ActualValue = $"N+{project.Cooling?.NRedundancy ?? 0}"
                    });

                    // Check generator requirements
                    int requiredGens = project.TargetTier switch
                    {
                        UptimeTier.Tier1 => 0,
                        UptimeTier.Tier2 => 1,
                        UptimeTier.Tier3 => 2,
                        UptimeTier.Tier4 => 2,
                        _ => 0
                    };

                    report.Requirements.Add(new TierRequirement
                    {
                        Category = "Generator",
                        Requirement = $"Minimum {requiredGens} generators",
                        IsMet = (project.Power?.GeneratorCount ?? 0) >= requiredGens,
                        ActualValue = project.Power?.GeneratorCount.ToString()
                    });

                    // Determine overall compliance
                    report.MeetsRequirements = report.Requirements.All(r => r.IsMet);

                    // List gaps
                    foreach (var req in report.Requirements.Where(r => !r.IsMet))
                    {
                        report.Gaps.Add($"{req.Category}: {req.Requirement} (Current: {req.ActualValue})");
                    }

                    return report;
                }
            });
        }

        public double CalculateCFMPerRack(double powerKW, double deltaT)
        {
            // CFM = (kW * 3412) / (1.08 * deltaT)
            return (powerKW * 3412) / (1.08 * deltaT);
        }

        public double CalculateRaisedFloorPressure(double totalCFM, double openArea)
        {
            // Static pressure in inches WC
            // P = (CFM / (4005 * A))^2
            return Math.Pow(totalCFM / (4005 * openArea), 2);
        }
    }
}
