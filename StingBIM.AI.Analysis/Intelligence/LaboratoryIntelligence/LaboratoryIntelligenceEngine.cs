// ===================================================================
// StingBIM Laboratory Intelligence Engine
// Research facilities, biosafety levels, fume hoods, clean rooms
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.LaboratoryIntelligence
{
    #region Enums

    public enum LabType { Research, Teaching, Clinical, Industrial, Cleanroom, Vivarium }
    public enum BiosaffetyLevel { BSL1, BSL2, BSL3, BSL4 }
    public enum CleanroomClass { ISO1, ISO2, ISO3, ISO4, ISO5, ISO6, ISO7, ISO8 }
    public enum FumeHoodType { Constant, Variable, HighPerformance, Perchloric, Radioisotope }
    public enum VentilationMode { ConstantVolume, VariableVolume, OnDemand }
    public enum HazardClass { Chemical, Biological, Radiological, Physical }

    #endregion

    #region Data Models

    public class LaboratoryProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public LabType PrimaryType { get; set; }
        public double TotalArea { get; set; }
        public List<LaboratoryModule> Modules { get; set; } = new();
        public List<FumeHood> FumeHoods { get; set; } = new();
        public List<BiosafetyCabinet> BSCs { get; set; } = new();
        public VentilationDesign Ventilation { get; set; }
        public LabSafetyPlan SafetyPlan { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class LaboratoryModule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public LabType Type { get; set; }
        public BiosaffetyLevel? BSL { get; set; }
        public CleanroomClass? Cleanroom { get; set; }
        public double Area { get; set; }
        public double CeilingHeight { get; set; }
        public int BenchLinearFeet { get; set; }
        public int FumeHoodCount { get; set; }
        public VentilationRequirements Ventilation { get; set; }
        public List<string> EquipmentList { get; set; } = new();
        public List<string> HazardInventory { get; set; } = new();
        public bool MeetsCriteria { get; set; }
        public List<string> Deficiencies { get; set; } = new();
    }

    public class VentilationRequirements
    {
        public VentilationMode Mode { get; set; }
        public int MinACH { get; set; }
        public int MaxACH { get; set; }
        public int DesignACH { get; set; }
        public double ExhaustCFM { get; set; }
        public double SupplyCFM { get; set; }
        public double Offset { get; set; }
        public bool NegativePressure { get; set; }
        public double PressureDifferential { get; set; }
        public bool DedicatedExhaust { get; set; }
        public bool HEPARequired { get; set; }
    }

    public class FumeHood
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Tag { get; set; }
        public string ModuleId { get; set; }
        public FumeHoodType Type { get; set; }
        public double Width { get; set; }
        public double Depth { get; set; }
        public double Height { get; set; }
        public double FaceVelocity { get; set; }
        public double ExhaustCFM { get; set; }
        public double SashHeight { get; set; }
        public double MaxSashHeight { get; set; }
        public bool HasAirfoil { get; set; }
        public bool HasBypassAir { get; set; }
        public string ExhaustConnection { get; set; }
        public double DiversityFactor { get; set; }
        public List<string> Alarms { get; set; } = new();
    }

    public class BiosafetyCabinet
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Tag { get; set; }
        public string ModuleId { get; set; }
        public string Classification { get; set; }
        public double Width { get; set; }
        public double InflowVelocity { get; set; }
        public double DownflowVelocity { get; set; }
        public bool HEPAExhaust { get; set; }
        public bool HEPASupply { get; set; }
        public string ExhaustType { get; set; }
        public double ExhaustCFM { get; set; }
        public bool HasUVLight { get; set; }
    }

    public class VentilationDesign
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public VentilationMode SystemMode { get; set; }
        public double TotalExhaustCFM { get; set; }
        public double TotalSupplyCFM { get; set; }
        public double MakeupAirCFM { get; set; }
        public double HeatRecoveryEfficiency { get; set; }
        public int ExhaustStackCount { get; set; }
        public double StackHeight { get; set; }
        public double StackVelocity { get; set; }
        public List<ExhaustSystem> ExhaustSystems { get; set; } = new();
        public double AnnualEnergy { get; set; }
        public double EnergyCost { get; set; }
    }

    public class ExhaustSystem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Type { get; set; }
        public double DesignCFM { get; set; }
        public double MinCFM { get; set; }
        public bool HasScrubber { get; set; }
        public bool HasHEPA { get; set; }
        public List<string> ServedEquipment { get; set; } = new();
    }

    public class LabSafetyPlan
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<HazardAssessment> Hazards { get; set; } = new();
        public List<SafetyEquipment> Equipment { get; set; } = new();
        public List<EmergencySystem> Emergency { get; set; } = new();
        public double EgressDistance { get; set; }
        public int ExitCount { get; set; }
        public bool MeetsNFPA45 { get; set; }
    }

    public class HazardAssessment
    {
        public string ModuleId { get; set; }
        public HazardClass Class { get; set; }
        public string Description { get; set; }
        public string RiskLevel { get; set; }
        public List<string> Controls { get; set; } = new();
    }

    public class SafetyEquipment
    {
        public string Type { get; set; }
        public string Location { get; set; }
        public int Quantity { get; set; }
        public double TravelDistance { get; set; }
        public bool MeetsCode { get; set; }
    }

    public class EmergencySystem
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public bool IsFunctional { get; set; }
    }

    public class LabComplianceReport
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public int TotalModules { get; set; }
        public int CompliantModules { get; set; }
        public double CompliancePercentage => TotalModules > 0 ? CompliantModules * 100.0 / TotalModules : 0;
        public List<ComplianceIssue> Issues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class ComplianceIssue
    {
        public string ModuleId { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Reference { get; set; }
    }

    #endregion

    public sealed class LaboratoryIntelligenceEngine
    {
        private static readonly Lazy<LaboratoryIntelligenceEngine> _instance =
            new Lazy<LaboratoryIntelligenceEngine>(() => new LaboratoryIntelligenceEngine());
        public static LaboratoryIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, LaboratoryProject> _projects = new();
        private readonly object _lock = new object();

        // ANSI/AIHA Z9.5 ACH Requirements
        private readonly Dictionary<LabType, int> _minACH = new()
        {
            [LabType.Research] = 6,
            [LabType.Teaching] = 6,
            [LabType.Clinical] = 6,
            [LabType.Industrial] = 8,
            [LabType.Cleanroom] = 20,
            [LabType.Vivarium] = 10
        };

        // BSL Requirements per CDC/NIH Guidelines
        private readonly Dictionary<BiosaffetyLevel, (int ach, bool hepa, bool negative, bool anteroom)> _bslRequirements = new()
        {
            [BiosaffetyLevel.BSL1] = (6, false, false, false),
            [BiosaffetyLevel.BSL2] = (6, false, true, false),
            [BiosaffetyLevel.BSL3] = (12, true, true, true),
            [BiosaffetyLevel.BSL4] = (15, true, true, true)
        };

        // ISO 14644-1 Cleanroom Classifications
        private readonly Dictionary<CleanroomClass, (int particles, int ach, double pressure)> _cleanroomSpecs = new()
        {
            [CleanroomClass.ISO1] = (10, 600, 0.05),
            [CleanroomClass.ISO2] = (100, 500, 0.05),
            [CleanroomClass.ISO3] = (1000, 400, 0.04),
            [CleanroomClass.ISO4] = (10000, 300, 0.04),
            [CleanroomClass.ISO5] = (100000, 240, 0.03),
            [CleanroomClass.ISO6] = (1000000, 150, 0.03),
            [CleanroomClass.ISO7] = (10000000, 60, 0.02),
            [CleanroomClass.ISO8] = (100000000, 20, 0.02)
        };

        // ANSI/ASHRAE 110 Fume Hood Face Velocities (fpm)
        private readonly Dictionary<FumeHoodType, (int min, int max, int typical)> _faceVelocities = new()
        {
            [FumeHoodType.Constant] = (80, 120, 100),
            [FumeHoodType.Variable] = (60, 100, 80),
            [FumeHoodType.HighPerformance] = (60, 80, 70),
            [FumeHoodType.Perchloric] = (100, 125, 110),
            [FumeHoodType.Radioisotope] = (100, 150, 125)
        };

        private LaboratoryIntelligenceEngine() { }

        public LaboratoryProject CreateLaboratoryProject(string projectId, string projectName, LabType primaryType)
        {
            var project = new LaboratoryProject
            {
                ProjectId = projectId,
                ProjectName = projectName,
                PrimaryType = primaryType
            };

            lock (_lock) { _projects[project.Id] = project; }
            return project;
        }

        public LaboratoryModule AddModule(string projectId, string name, LabType type,
            double area, double ceilingHeight, BiosaffetyLevel? bsl = null, CleanroomClass? cleanroom = null)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var module = new LaboratoryModule
                {
                    Name = name,
                    Type = type,
                    Area = area,
                    CeilingHeight = ceilingHeight,
                    BSL = bsl,
                    Cleanroom = cleanroom
                };

                // Set ventilation requirements
                module.Ventilation = CalculateVentilationRequirements(module);

                // Calculate bench linear feet (rule of thumb)
                module.BenchLinearFeet = (int)(area / 50);

                // Validate module
                ValidateModule(module);

                project.Modules.Add(module);
                project.TotalArea += area;
                return module;
            }
        }

        private VentilationRequirements CalculateVentilationRequirements(LaboratoryModule module)
        {
            var req = new VentilationRequirements();

            // Determine base ACH
            if (module.Cleanroom.HasValue && _cleanroomSpecs.TryGetValue(module.Cleanroom.Value, out var crSpec))
            {
                req.MinACH = crSpec.ach;
                req.MaxACH = crSpec.ach;
                req.PressureDifferential = crSpec.pressure;
                req.HEPARequired = true;
            }
            else if (module.BSL.HasValue && _bslRequirements.TryGetValue(module.BSL.Value, out var bslSpec))
            {
                req.MinACH = bslSpec.ach;
                req.MaxACH = bslSpec.ach + 6;
                req.HEPARequired = bslSpec.hepa;
                req.NegativePressure = bslSpec.negative;
                req.PressureDifferential = bslSpec.negative ? -0.03 : 0;
            }
            else
            {
                req.MinACH = _minACH.GetValueOrDefault(module.Type, 6);
                req.MaxACH = req.MinACH + 4;
            }

            req.DesignACH = (req.MinACH + req.MaxACH) / 2;

            // Calculate CFM
            double volume = module.Area * module.CeilingHeight;
            req.ExhaustCFM = volume * req.DesignACH / 60;
            req.Offset = req.NegativePressure ? 100 : -50;
            req.SupplyCFM = req.ExhaustCFM - req.Offset;

            req.Mode = module.Type == LabType.Cleanroom ? VentilationMode.ConstantVolume : VentilationMode.VariableVolume;
            req.DedicatedExhaust = module.BSL >= BiosaffetyLevel.BSL3;

            return req;
        }

        private void ValidateModule(LaboratoryModule module)
        {
            module.Deficiencies.Clear();

            // Check ceiling height
            double minHeight = module.Cleanroom.HasValue ? 10 : 9;
            if (module.CeilingHeight < minHeight)
            {
                module.Deficiencies.Add($"Ceiling height {module.CeilingHeight}' below minimum {minHeight}'");
            }

            // Check BSL3/4 requirements
            if (module.BSL >= BiosaffetyLevel.BSL3)
            {
                if (!module.Ventilation.DedicatedExhaust)
                    module.Deficiencies.Add("BSL-3/4 requires dedicated exhaust system");
                if (!module.Ventilation.HEPARequired)
                    module.Deficiencies.Add("BSL-3/4 requires HEPA filtration on exhaust");
            }

            // Minimum area checks
            double minArea = module.Type switch
            {
                LabType.Cleanroom => 200,
                LabType.Vivarium => 150,
                _ => 100
            };

            if (module.Area < minArea)
            {
                module.Deficiencies.Add($"Area {module.Area} SF below recommended minimum {minArea} SF");
            }

            module.MeetsCriteria = module.Deficiencies.Count == 0;
        }

        public FumeHood AddFumeHood(string projectId, string moduleId, string tag, FumeHoodType type,
            double width, double depth, double height)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var module = project.Modules.FirstOrDefault(m => m.Id == moduleId);
                if (module == null) return null;

                var velocitySpec = _faceVelocities.GetValueOrDefault(type, (min: 80, max: 120, typical: 100));

                var hood = new FumeHood
                {
                    Tag = tag,
                    ModuleId = moduleId,
                    Type = type,
                    Width = width,
                    Depth = depth,
                    Height = height,
                    FaceVelocity = velocitySpec.typical,
                    MaxSashHeight = 18,
                    SashHeight = 18,
                    HasAirfoil = true,
                    HasBypassAir = type != FumeHoodType.HighPerformance,
                    DiversityFactor = 0.7
                };

                // Calculate exhaust CFM
                double faceArea = (width / 12) * (hood.SashHeight / 12);
                hood.ExhaustCFM = faceArea * hood.FaceVelocity;

                // Add standard alarms
                hood.Alarms = new List<string> { "Low flow alarm", "Sash position alarm", "Face velocity alarm" };

                if (type == FumeHoodType.Perchloric)
                {
                    hood.ExhaustConnection = "Dedicated perchloric exhaust with washdown";
                    hood.Alarms.Add("Washdown system alarm");
                }
                else
                {
                    hood.ExhaustConnection = "General lab exhaust";
                }

                module.FumeHoodCount++;
                project.FumeHoods.Add(hood);
                return hood;
            }
        }

        public BiosafetyCabinet AddBSC(string projectId, string moduleId, string tag,
            string classification, double width)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var bsc = new BiosafetyCabinet
                {
                    Tag = tag,
                    ModuleId = moduleId,
                    Classification = classification,
                    Width = width
                };

                // Set parameters based on classification
                switch (classification)
                {
                    case "Class I":
                        bsc.InflowVelocity = 75;
                        bsc.DownflowVelocity = 0;
                        bsc.HEPAExhaust = true;
                        bsc.HEPASupply = false;
                        bsc.ExhaustType = "Hard-ducted";
                        break;
                    case "Class II Type A2":
                        bsc.InflowVelocity = 100;
                        bsc.DownflowVelocity = 60;
                        bsc.HEPAExhaust = true;
                        bsc.HEPASupply = true;
                        bsc.ExhaustType = "Recirculating with canopy";
                        break;
                    case "Class II Type B2":
                        bsc.InflowVelocity = 100;
                        bsc.DownflowVelocity = 60;
                        bsc.HEPAExhaust = true;
                        bsc.HEPASupply = true;
                        bsc.ExhaustType = "100% hard-ducted";
                        break;
                    case "Class III":
                        bsc.InflowVelocity = 0;
                        bsc.DownflowVelocity = 0;
                        bsc.HEPAExhaust = true;
                        bsc.HEPASupply = true;
                        bsc.ExhaustType = "Double HEPA or incineration";
                        break;
                }

                // Calculate exhaust CFM
                bsc.ExhaustCFM = classification == "Class II Type B2" ?
                    (width / 12) * 2 * bsc.InflowVelocity : (width / 12) * 0.5 * bsc.InflowVelocity;

                bsc.HasUVLight = true;

                project.BSCs.Add(bsc);
                return bsc;
            }
        }

        public async Task<VentilationDesign> DesignVentilationSystem(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var design = new VentilationDesign
                    {
                        SystemMode = project.Modules.Any(m => m.Cleanroom.HasValue) ?
                            VentilationMode.ConstantVolume : VentilationMode.VariableVolume
                    };

                    // Calculate total exhaust
                    double moduleExhaust = project.Modules.Sum(m => m.Ventilation?.ExhaustCFM ?? 0);
                    double hoodExhaust = project.FumeHoods.Sum(h => h.ExhaustCFM * h.DiversityFactor);
                    double bscExhaust = project.BSCs.Sum(b => b.ExhaustCFM);

                    design.TotalExhaustCFM = moduleExhaust + hoodExhaust + bscExhaust;
                    design.TotalSupplyCFM = design.TotalExhaustCFM - 500; // Building offset

                    // Create exhaust systems
                    var generalExhaust = new ExhaustSystem
                    {
                        Name = "General Lab Exhaust",
                        Type = "Variable Volume",
                        DesignCFM = hoodExhaust + moduleExhaust * 0.8,
                        MinCFM = hoodExhaust * 0.4
                    };
                    design.ExhaustSystems.Add(generalExhaust);

                    if (project.FumeHoods.Any(h => h.Type == FumeHoodType.Perchloric))
                    {
                        design.ExhaustSystems.Add(new ExhaustSystem
                        {
                            Name = "Perchloric Exhaust",
                            Type = "Dedicated with Washdown",
                            DesignCFM = project.FumeHoods.Where(h => h.Type == FumeHoodType.Perchloric).Sum(h => h.ExhaustCFM),
                            HasScrubber = true
                        });
                    }

                    if (project.Modules.Any(m => m.BSL >= BiosaffetyLevel.BSL3))
                    {
                        design.ExhaustSystems.Add(new ExhaustSystem
                        {
                            Name = "BSL-3 Exhaust",
                            Type = "Dedicated HEPA",
                            DesignCFM = project.Modules.Where(m => m.BSL >= BiosaffetyLevel.BSL3).Sum(m => m.Ventilation?.ExhaustCFM ?? 0),
                            HasHEPA = true
                        });
                    }

                    // Stack design
                    design.ExhaustStackCount = design.ExhaustSystems.Count;
                    design.StackHeight = 10; // feet above roof
                    design.StackVelocity = 3000; // fpm minimum

                    // Energy analysis (assuming 8760 hours/year)
                    double fanHP = design.TotalExhaustCFM / 1000; // rough estimate
                    design.AnnualEnergy = fanHP * 0.746 * 8760 * 0.8; // kWh
                    design.HeatRecoveryEfficiency = 0.65;
                    design.MakeupAirCFM = design.TotalSupplyCFM;

                    project.Ventilation = design;
                    return design;
                }
            });
        }

        public LabSafetyPlan CreateSafetyPlan(string projectId)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var plan = new LabSafetyPlan();

                // Add safety equipment
                plan.Equipment.Add(new SafetyEquipment
                {
                    Type = "Emergency Shower/Eyewash",
                    Location = "Within 10 second travel",
                    Quantity = (int)Math.Ceiling(project.TotalArea / 2500),
                    TravelDistance = 75,
                    MeetsCode = true
                });

                plan.Equipment.Add(new SafetyEquipment
                {
                    Type = "Fire Extinguisher",
                    Location = "At exit paths",
                    Quantity = (int)Math.Ceiling(project.TotalArea / 3000),
                    TravelDistance = 75,
                    MeetsCode = true
                });

                plan.Equipment.Add(new SafetyEquipment
                {
                    Type = "Spill Kit",
                    Location = "In each module",
                    Quantity = project.Modules.Count,
                    MeetsCode = true
                });

                // Add emergency systems
                plan.Emergency.Add(new EmergencySystem
                {
                    Type = "Emergency Power Off",
                    Description = "EPO stations at exits",
                    Location = "All module exits"
                });

                plan.Emergency.Add(new EmergencySystem
                {
                    Type = "Gas Shutoff",
                    Description = "Emergency gas isolation",
                    Location = "Module entries and at gas manifolds"
                });

                // Check NFPA 45 compliance
                plan.EgressDistance = 75; // feet max
                plan.ExitCount = project.TotalArea > 1000 ? 2 : 1;
                plan.MeetsNFPA45 = plan.ExitCount >= 2 || project.TotalArea <= 1000;

                project.SafetyPlan = plan;
                return plan;
            }
        }

        public async Task<LabComplianceReport> GenerateComplianceReport(string projectId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var report = new LabComplianceReport
                    {
                        ProjectId = projectId,
                        TotalModules = project.Modules.Count,
                        CompliantModules = project.Modules.Count(m => m.MeetsCriteria)
                    };

                    // Collect issues
                    foreach (var module in project.Modules.Where(m => !m.MeetsCriteria))
                    {
                        foreach (var deficiency in module.Deficiencies)
                        {
                            report.Issues.Add(new ComplianceIssue
                            {
                                ModuleId = module.Id,
                                Category = "Ventilation/Space",
                                Description = deficiency,
                                Reference = module.BSL.HasValue ? "CDC/NIH BMBL" : "ANSI/AIHA Z9.5"
                            });
                        }
                    }

                    // Check fume hood face velocities
                    foreach (var hood in project.FumeHoods)
                    {
                        var spec = _faceVelocities.GetValueOrDefault(hood.Type, (min: 80, max: 120, typical: 100));
                        if (hood.FaceVelocity < spec.min || hood.FaceVelocity > spec.max)
                        {
                            report.Issues.Add(new ComplianceIssue
                            {
                                ModuleId = hood.ModuleId,
                                Category = "Fume Hood",
                                Description = $"Hood {hood.Tag} face velocity {hood.FaceVelocity} fpm outside range {spec.min}-{spec.max}",
                                Reference = "ANSI/ASHRAE 110"
                            });
                        }
                    }

                    // Add recommendations
                    if (project.Modules.Any(m => m.Type == LabType.Research && m.Ventilation?.Mode == VentilationMode.ConstantVolume))
                    {
                        report.Recommendations.Add("Consider VAV system for research labs to reduce energy consumption");
                    }

                    if (project.FumeHoods.Count > 10 && project.FumeHoods.All(h => h.DiversityFactor == 1.0))
                    {
                        report.Recommendations.Add("Apply diversity factor to fume hood exhaust calculations");
                    }

                    return report;
                }
            });
        }
    }
}
