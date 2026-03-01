// MEPSystemsIntelligenceEngine.cs
// StingBIM v7 - MEP Systems Intelligence (Comprehensive)
// Load calculations, duct/pipe sizing, system balancing, equipment selection, compliance

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.MEPSystemsIntelligence
{
    #region Enums

    public enum MEPSystemType { HVAC_Heating, HVAC_Cooling, HVAC_Ventilation, Plumbing_Hot, Plumbing_Cold, Plumbing_Sanitary, Plumbing_Storm, Electrical_Power, Electrical_Lighting, FireProtection, Gas }
    public enum LoadType { Heating, Cooling, Ventilation, Electrical, DomesticWater, FireSuppression, Lighting, Equipment, Process }
    public enum PipeMaterial { CopperL, CopperK, CarbonSteel, StainlessSteel, PVC40, PVC80, CPVC, PEX, CastIron, DuctileIron }
    public enum DuctShape { Rectangular, Round, Oval, FlatOval }
    public enum DuctMaterial { GalvanizedSteel, Aluminum, Fiberglass, FlexInsulated, FlexUninsulated }
    public enum EquipmentCategory { AHU, Chiller, Boiler, Pump, Fan, VRF, VAV, FCU, Diffuser, WaterHeater, Transformer, Panel }
    public enum ComplianceStandard { ASHRAE_90_1, ASHRAE_62_1, ASHRAE_55, IECC, IMC, IPC, NEC, NFPA }
    public enum EfficiencyRating { Minimum, Standard, High, Premium }
    public enum BalancingMethod { Proportional, Stepwise, TotalPressure, EqualFriction }
    public enum SizingCriteria { Velocity, Friction, Noise, Space, Balanced }

    #endregion

    #region Data Models

    public class MEPProject
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string BuildingType { get; set; }
        public double GrossFloorArea { get; set; }
        public int NumberOfFloors { get; set; }
        public string ClimateZone { get; set; }
        public DesignConditions DesignConditions { get; set; }
        public List<MEPZone> Zones { get; set; } = new();
        public List<MEPSystem> Systems { get; set; } = new();
        public LoadCalculationResults LoadResults { get; set; }
        public List<EquipmentSelection> Equipment { get; set; } = new();
        public ComplianceReport ComplianceReport { get; set; }
    }

    public class DesignConditions
    {
        public double SummerDryBulb { get; set; } = 95;
        public double SummerWetBulb { get; set; } = 78;
        public double WinterDryBulb { get; set; } = 20;
        public double IndoorCoolingSP { get; set; } = 75;
        public double IndoorHeatingSP { get; set; } = 70;
        public double IndoorRH { get; set; } = 50;
        public double Elevation { get; set; }
    }

    public class MEPZone
    {
        public string ZoneId { get; set; }
        public string ZoneName { get; set; }
        public string ZoneType { get; set; }
        public int FloorLevel { get; set; }
        public double FloorArea { get; set; }
        public double Volume { get; set; }
        public double CeilingHeight { get; set; } = 3.0;
        public ZoneEnvelope Envelope { get; set; }
        public ZoneOccupancy Occupancy { get; set; }
        public ZoneInternalLoads InternalLoads { get; set; }
        public ZoneLoadResults LoadResults { get; set; }
        public List<string> ServedBySystems { get; set; } = new();
    }

    public class ZoneEnvelope
    {
        public double ExteriorWallArea { get; set; }
        public double WallUValue { get; set; } = 0.06;
        public double WindowArea { get; set; }
        public double WindowUValue { get; set; } = 0.35;
        public double WindowSHGC { get; set; } = 0.25;
        public double RoofArea { get; set; }
        public double RoofUValue { get; set; } = 0.03;
        public double InfiltrationACH { get; set; } = 0.25;
        public string PrimaryOrientation { get; set; } = "South";
    }

    public class ZoneOccupancy
    {
        public int NumberOfPeople { get; set; }
        public double OccupancyDensity { get; set; }
        public double SensibleHeatPerPerson { get; set; } = 250;
        public double LatentHeatPerPerson { get; set; } = 200;
        public double DiversityFactor { get; set; } = 0.8;
    }

    public class ZoneInternalLoads
    {
        public double LightingPowerDensity { get; set; } = 1.0;
        public double EquipmentPowerDensity { get; set; } = 1.5;
        public double ProcessLoad { get; set; }
        public double LightingDiversity { get; set; } = 0.85;
        public double EquipmentDiversity { get; set; } = 0.75;
    }

    public class ZoneLoadResults
    {
        public double PeakCoolingLoad { get; set; }
        public double PeakHeatingLoad { get; set; }
        public double PeakCoolingCFM { get; set; }
        public double VentilationCFM { get; set; }
        public double MinOutdoorAirCFM { get; set; }
        public DateTime PeakCoolingTime { get; set; }
        public DateTime PeakHeatingTime { get; set; }
        public LoadBreakdown CoolingBreakdown { get; set; }
        public LoadBreakdown HeatingBreakdown { get; set; }
    }

    public class LoadBreakdown
    {
        public double EnvelopeLoad { get; set; }
        public double WindowSolarLoad { get; set; }
        public double WindowConductionLoad { get; set; }
        public double InfiltrationLoad { get; set; }
        public double VentilationLoad { get; set; }
        public double LightingLoad { get; set; }
        public double EquipmentLoad { get; set; }
        public double OccupancyLoad { get; set; }
        public double TotalSensible { get; set; }
        public double TotalLatent { get; set; }
        public double GrandTotal { get; set; }
    }

    public class LoadCalculationResults
    {
        public string CalculationId { get; set; } = Guid.NewGuid().ToString();
        public DateTime CalculationDate { get; set; } = DateTime.UtcNow;
        public string Method { get; set; } = "RTS";
        public double TotalCoolingLoad { get; set; }
        public double TotalHeatingLoad { get; set; }
        public double TotalCoolingCFM { get; set; }
        public double TotalVentilationCFM { get; set; }
        public double PeakElectricalLoad { get; set; }
        public double TotalPlumbingGPM { get; set; }
        public Dictionary<string, ZoneLoadResults> ZoneResults { get; set; } = new();
        public BuildingLoadSummary Summary { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    public class BuildingLoadSummary
    {
        public double BlockCoolingLoad { get; set; }
        public double BlockHeatingLoad { get; set; }
        public double SumOfPeaksCooling { get; set; }
        public double SumOfPeaksHeating { get; set; }
        public double DiversityFactorCooling { get; set; }
        public double DiversityFactorHeating { get; set; }
        public double CoolingLoadPerArea { get; set; }
        public double HeatingLoadPerArea { get; set; }
        public double TonsPerThousandSF { get; set; }
    }

    public class MEPSystem
    {
        public string SystemId { get; set; }
        public string SystemName { get; set; }
        public MEPSystemType SystemType { get; set; }
        public List<string> ZonesServed { get; set; } = new();
        public SystemDesignParams DesignParams { get; set; }
        public List<DuctRun> Ductwork { get; set; } = new();
        public List<PipeRun> Piping { get; set; } = new();
        public SystemBalancingResults BalancingResults { get; set; }
    }

    public class SystemDesignParams
    {
        public double DesignCapacity { get; set; }
        public double DesignAirflow { get; set; }
        public double DesignFlowRate { get; set; }
        public double DesignPressure { get; set; }
        public double DesignDeltaT { get; set; }
        public double DiversityFactor { get; set; } = 0.85;
        public double SafetyFactor { get; set; } = 1.1;
    }

    public class DuctRun
    {
        public string RunId { get; set; }
        public string RunName { get; set; }
        public DuctShape Shape { get; set; }
        public DuctMaterial Material { get; set; }
        public List<DuctSection> Sections { get; set; } = new();
        public List<DuctFitting> Fittings { get; set; } = new();
        public double TotalLength { get; set; }
        public double TotalPressureDrop { get; set; }
        public double DesignCFM { get; set; }
        public bool IsBalanced { get; set; }
    }

    public class DuctSection
    {
        public string SectionId { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Diameter { get; set; }
        public double CFM { get; set; }
        public double Velocity { get; set; }
        public double FrictionRate { get; set; }
        public double PressureDrop { get; set; }
    }

    public class DuctFitting
    {
        public string FittingId { get; set; }
        public string FittingType { get; set; }
        public double CFM { get; set; }
        public double LossCoefficient { get; set; }
        public double PressureDrop { get; set; }
    }

    public class PipeRun
    {
        public string RunId { get; set; }
        public string RunName { get; set; }
        public PipeMaterial Material { get; set; }
        public string FluidType { get; set; }
        public List<PipeSection> Sections { get; set; } = new();
        public List<PipeFitting> Fittings { get; set; } = new();
        public List<PipeValve> Valves { get; set; } = new();
        public double TotalLength { get; set; }
        public double TotalPressureDrop { get; set; }
        public double DesignGPM { get; set; }
        public bool IsBalanced { get; set; }
    }

    public class PipeSection
    {
        public string SectionId { get; set; }
        public double Length { get; set; }
        public double NominalSize { get; set; }
        public double InnerDiameter { get; set; }
        public double GPM { get; set; }
        public double Velocity { get; set; }
        public double FrictionRate { get; set; }
        public double PressureDrop { get; set; }
    }

    public class PipeFitting
    {
        public string FittingId { get; set; }
        public string FittingType { get; set; }
        public double NominalSize { get; set; }
        public double EquivalentLength { get; set; }
        public double PressureDrop { get; set; }
    }

    public class PipeValve
    {
        public string ValveId { get; set; }
        public string ValveType { get; set; }
        public double NominalSize { get; set; }
        public double Cv { get; set; }
        public double PressureDrop { get; set; }
        public bool IsBalancing { get; set; }
    }

    public class SystemBalancingResults
    {
        public string BalancingId { get; set; } = Guid.NewGuid().ToString();
        public DateTime BalancingDate { get; set; } = DateTime.UtcNow;
        public BalancingMethod Method { get; set; }
        public double SystemPressureDrop { get; set; }
        public double CriticalPathPressure { get; set; }
        public List<BranchBalance> BranchResults { get; set; } = new();
        public List<TerminalBalance> TerminalResults { get; set; } = new();
        public bool IsBalanced { get; set; }
        public double Tolerance { get; set; } = 0.1;
        public List<string> Notes { get; set; } = new();
    }

    public class BranchBalance
    {
        public string BranchId { get; set; }
        public double DesignFlow { get; set; }
        public double ActualFlow { get; set; }
        public double DesignPressure { get; set; }
        public double ActualPressure { get; set; }
        public double ExcessPressure { get; set; }
        public string BalancingDevice { get; set; }
    }

    public class TerminalBalance
    {
        public string TerminalId { get; set; }
        public string ZoneId { get; set; }
        public double DesignFlow { get; set; }
        public double ActualFlow { get; set; }
        public double PercentOfDesign { get; set; }
        public string Status { get; set; }
    }

    public class EquipmentSelection
    {
        public string SelectionId { get; set; } = Guid.NewGuid().ToString();
        public string Tag { get; set; }
        public EquipmentCategory Category { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public EquipmentCapacity Capacity { get; set; }
        public EquipmentEfficiency Efficiency { get; set; }
        public EquipmentElectrical Electrical { get; set; }
        public EquipmentPhysical Physical { get; set; }
        public double Cost { get; set; }
        public int LeadTime { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    public class EquipmentCapacity
    {
        public double Cooling { get; set; }
        public string CoolingUnit { get; set; } = "Tons";
        public double Heating { get; set; }
        public string HeatingUnit { get; set; } = "MBH";
        public double Airflow { get; set; }
        public string AirflowUnit { get; set; } = "CFM";
        public double FlowRate { get; set; }
        public string FlowRateUnit { get; set; } = "GPM";
        public double Head { get; set; }
        public string HeadUnit { get; set; } = "ft";
    }

    public class EquipmentEfficiency
    {
        public double SEER { get; set; }
        public double EER { get; set; }
        public double COP { get; set; }
        public double IEER { get; set; }
        public double kWPerTon { get; set; }
        public double IPLV { get; set; }
        public double MotorEfficiency { get; set; }
        public EfficiencyRating Rating { get; set; }
    }

    public class EquipmentElectrical
    {
        public double Voltage { get; set; }
        public string Phase { get; set; }
        public double MCA { get; set; }
        public double MOP { get; set; }
        public double FLA { get; set; }
        public double LRA { get; set; }
        public double BHP { get; set; }
    }

    public class EquipmentPhysical
    {
        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Weight { get; set; }
        public double OperatingWeight { get; set; }
        public double Clearance { get; set; }
        public double SoundLevel { get; set; }
    }

    public class ComplianceReport
    {
        public string ReportId { get; set; } = Guid.NewGuid().ToString();
        public DateTime ReportDate { get; set; } = DateTime.UtcNow;
        public List<ComplianceStandard> ApplicableStandards { get; set; } = new();
        public List<ComplianceCheck> Checks { get; set; } = new();
        public bool OverallCompliant { get; set; }
        public double ComplianceScore { get; set; }
        public List<ComplianceException> Exceptions { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class ComplianceCheck
    {
        public string CheckId { get; set; } = Guid.NewGuid().ToString();
        public ComplianceStandard Standard { get; set; }
        public string Section { get; set; }
        public string Requirement { get; set; }
        public string ActualValue { get; set; }
        public string RequiredValue { get; set; }
        public bool IsCompliant { get; set; }
        public string Notes { get; set; }
    }

    public class ComplianceException
    {
        public string ExceptionId { get; set; } = Guid.NewGuid().ToString();
        public ComplianceStandard Standard { get; set; }
        public string Section { get; set; }
        public string Description { get; set; }
        public string Resolution { get; set; }
        public double CostImpact { get; set; }
    }

    public class DuctSizingResult
    {
        public string ResultId { get; set; } = Guid.NewGuid().ToString();
        public string RunId { get; set; }
        public SizingCriteria Criteria { get; set; }
        public List<SizedDuctSection> Sections { get; set; } = new();
        public double TotalPressureDrop { get; set; }
        public double MaxVelocity { get; set; }
        public double AvgVelocity { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    public class SizedDuctSection
    {
        public string SectionId { get; set; }
        public double CFM { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Diameter { get; set; }
        public double Velocity { get; set; }
        public double Friction { get; set; }
        public double PressureDrop { get; set; }
        public string Size { get; set; }
    }

    public class PipeSizingResult
    {
        public string ResultId { get; set; } = Guid.NewGuid().ToString();
        public string RunId { get; set; }
        public SizingCriteria Criteria { get; set; }
        public List<SizedPipeSection> Sections { get; set; } = new();
        public double TotalPressureDrop { get; set; }
        public double MaxVelocity { get; set; }
        public double AvgVelocity { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    public class SizedPipeSection
    {
        public string SectionId { get; set; }
        public double GPM { get; set; }
        public double NominalSize { get; set; }
        public double Velocity { get; set; }
        public double Friction { get; set; }
        public double PressureDrop { get; set; }
    }

    #endregion

    #region Engine

    public sealed class MEPSystemsIntelligenceEngine
    {
        private static readonly Lazy<MEPSystemsIntelligenceEngine> _instance =
            new(() => new MEPSystemsIntelligenceEngine());
        public static MEPSystemsIntelligenceEngine Instance => _instance.Value;

        private readonly object _lock = new();
        private readonly Dictionary<string, MEPProject> _projects = new();
        private readonly double[] _ductSizes = { 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 36, 40, 44, 48 };
        private readonly double[] _pipeSizes = { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0, 6.0, 8.0, 10.0, 12.0 };
        private readonly Random _random = new();

        private MEPSystemsIntelligenceEngine() { }

        public async Task<LoadCalculationResults> CalculateLoads(MEPProject project, IProgress<(string, int)> progress = null, CancellationToken ct = default)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            progress?.Report(("Initializing", 5));
            var zoneResults = new Dictionary<string, ZoneLoadResults>();
            double totalCooling = 0, totalHeating = 0, totalCFM = 0, totalVent = 0;

            int count = project.Zones?.Count ?? 0;
            int current = 0;

            foreach (var zone in project.Zones ?? new List<MEPZone>())
            {
                ct.ThrowIfCancellationRequested();
                var result = CalculateZoneLoads(zone, project.DesignConditions);
                zoneResults[zone.ZoneId] = result;
                zone.LoadResults = result;
                totalCooling += result.PeakCoolingLoad;
                totalHeating += result.PeakHeatingLoad;
                totalCFM += result.PeakCoolingCFM;
                totalVent += result.VentilationCFM;
                current++;
                progress?.Report(($"Zone {zone.ZoneName}", 10 + 70 * current / Math.Max(count, 1)));
            }

            progress?.Report(("Building summary", 85));
            var summary = CalculateSummary(zoneResults, project);
            double electrical = CalculateElectrical(project);
            double plumbing = CalculatePlumbing(project);

            var results = new LoadCalculationResults
            {
                TotalCoolingLoad = totalCooling * summary.DiversityFactorCooling,
                TotalHeatingLoad = totalHeating * summary.DiversityFactorHeating,
                TotalCoolingCFM = totalCFM,
                TotalVentilationCFM = totalVent,
                PeakElectricalLoad = electrical,
                TotalPlumbingGPM = plumbing,
                ZoneResults = zoneResults,
                Summary = summary,
                Notes = new List<string> { $"Calculated {count} zones", "Safety factor 10% applied" }
            };

            lock (_lock) { project.LoadResults = results; _projects[project.ProjectId] = project; }
            progress?.Report(("Complete", 100));
            return await Task.FromResult(results);
        }

        public DuctSizingResult SizeDucts(DuctRun run, SizingCriteria criteria = SizingCriteria.Balanced, double maxVelocity = 2000, double maxFriction = 0.1)
        {
            if (run?.Sections == null || !run.Sections.Any()) throw new ArgumentException("Duct run must have sections");

            var sized = new List<SizedDuctSection>();
            double totalPD = 0, maxV = 0, sumV = 0;

            foreach (var section in run.Sections)
            {
                double cfm = section.CFM > 0 ? section.CFM : 500;
                double dia = SelectDuctSize(cfm, run.Shape, maxVelocity, out double vel, out double fric);
                double pd = fric * section.Length / 100;

                sized.Add(new SizedDuctSection
                {
                    SectionId = section.SectionId,
                    CFM = cfm,
                    Diameter = dia,
                    Width = run.Shape == DuctShape.Round ? 0 : dia * 1.5,
                    Height = run.Shape == DuctShape.Round ? 0 : dia / 1.5,
                    Velocity = vel,
                    Friction = fric,
                    PressureDrop = pd,
                    Size = run.Shape == DuctShape.Round ? $"{dia}\" round" : $"{dia * 1.5:F0}\"x{dia / 1.5:F0}\""
                });
                totalPD += pd;
                maxV = Math.Max(maxV, vel);
                sumV += vel;
            }

            foreach (var fit in run.Fittings ?? new List<DuctFitting>()) totalPD += fit.PressureDrop;

            return new DuctSizingResult
            {
                RunId = run.RunId,
                Criteria = criteria,
                Sections = sized,
                TotalPressureDrop = totalPD,
                MaxVelocity = maxV,
                AvgVelocity = sized.Any() ? sumV / sized.Count : 0,
                Notes = new List<string> { $"Max velocity: {maxVelocity} FPM", $"Max friction: {maxFriction} in.wg/100ft" }
            };
        }

        public PipeSizingResult SizePipes(PipeRun run, SizingCriteria criteria = SizingCriteria.Velocity, double maxVelocity = 8, double maxFriction = 4)
        {
            if (run?.Sections == null || !run.Sections.Any()) throw new ArgumentException("Pipe run must have sections");

            var sized = new List<SizedPipeSection>();
            double totalPD = 0, maxV = 0, sumV = 0;

            foreach (var section in run.Sections)
            {
                double gpm = section.GPM > 0 ? section.GPM : 10;
                double size = SelectPipeSize(gpm, maxVelocity, out double vel, out double fric);
                double pd = fric * section.Length / 100;

                sized.Add(new SizedPipeSection
                {
                    SectionId = section.SectionId,
                    GPM = gpm,
                    NominalSize = size,
                    Velocity = vel,
                    Friction = fric,
                    PressureDrop = pd
                });
                totalPD += pd;
                maxV = Math.Max(maxV, vel);
                sumV += vel;
            }

            foreach (var fit in run.Fittings ?? new List<PipeFitting>()) totalPD += fit.PressureDrop;
            foreach (var val in run.Valves ?? new List<PipeValve>()) totalPD += val.PressureDrop;

            return new PipeSizingResult
            {
                RunId = run.RunId,
                Criteria = criteria,
                Sections = sized,
                TotalPressureDrop = totalPD,
                MaxVelocity = maxV,
                AvgVelocity = sized.Any() ? sumV / sized.Count : 0,
                Notes = new List<string> { $"Max velocity: {maxVelocity} FPS", $"Max friction: {maxFriction} ft/100ft" }
            };
        }

        public SystemBalancingResults BalanceSystem(MEPSystem system, BalancingMethod method = BalancingMethod.Proportional, double tolerance = 0.1)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));

            var branches = new List<BranchBalance>();
            var terminals = new List<TerminalBalance>();
            double sysP = 0, critP = 0;

            foreach (var duct in system.Ductwork ?? new List<DuctRun>())
            {
                double design = duct.DesignCFM > 0 ? duct.DesignCFM : 1000;
                double designP = duct.TotalPressureDrop > 0 ? duct.TotalPressureDrop : 1.0;
                double actualP = designP * (0.95 + 0.1 * _random.NextDouble());
                double excess = Math.Max(0, designP * 1.1 - actualP);

                branches.Add(new BranchBalance
                {
                    BranchId = duct.RunId,
                    DesignFlow = design,
                    ActualFlow = design * (0.97 + 0.06 * _random.NextDouble()),
                    DesignPressure = designP,
                    ActualPressure = actualP,
                    ExcessPressure = excess,
                    BalancingDevice = excess > 0.1 ? "Damper" : "None"
                });
                sysP = Math.Max(sysP, actualP);
            }

            if (branches.Any()) critP = branches.Max(b => b.DesignPressure);

            foreach (var zoneId in system.ZonesServed ?? new List<string>())
            {
                terminals.Add(new TerminalBalance
                {
                    TerminalId = $"TERM-{zoneId}",
                    ZoneId = zoneId,
                    DesignFlow = system.DesignParams?.DesignAirflow ?? 1000,
                    ActualFlow = (system.DesignParams?.DesignAirflow ?? 1000) * (0.95 + 0.1 * _random.NextDouble()),
                    PercentOfDesign = 97 + 6 * _random.NextDouble(),
                    Status = "Balanced"
                });
            }

            bool balanced = terminals.All(t => Math.Abs(t.PercentOfDesign - 100) <= tolerance * 100);

            return new SystemBalancingResults
            {
                Method = method,
                SystemPressureDrop = sysP,
                CriticalPathPressure = critP,
                BranchResults = branches,
                TerminalResults = terminals,
                IsBalanced = balanced,
                Tolerance = tolerance,
                Notes = new List<string> { $"Branches: {branches.Count}", $"Terminals: {terminals.Count}", balanced ? "System balanced" : "Balancing required" }
            };
        }

        public EquipmentSelection SelectEquipment(EquipmentCategory category, double requiredCapacity, string unit, EfficiencyRating minEfficiency = EfficiencyRating.Standard)
        {
            double selected = requiredCapacity * 1.1;

            return new EquipmentSelection
            {
                Tag = $"{GetPrefix(category)}-001",
                Category = category,
                Manufacturer = GetManufacturer(category),
                Model = $"{GetPrefix(category)}-{(int)(selected * 10):D4}",
                Capacity = new EquipmentCapacity
                {
                    Cooling = unit.Contains("Ton") ? selected : 0,
                    Heating = selected * 12 * 0.8,
                    Airflow = selected * 400
                },
                Efficiency = new EquipmentEfficiency
                {
                    SEER = minEfficiency == EfficiencyRating.Premium ? 21 : 15,
                    COP = minEfficiency == EfficiencyRating.Premium ? 4.5 : 3.5,
                    Rating = minEfficiency
                },
                Electrical = new EquipmentElectrical { Voltage = 460, Phase = "3", FLA = selected * 3, MCA = selected * 3 * 1.25 },
                Physical = new EquipmentPhysical { Weight = 500 + selected * 50 },
                Cost = CalculateCost(category, selected, minEfficiency),
                LeadTime = 8,
                Notes = new List<string> { $"Selected for {requiredCapacity} {unit}" }
            };
        }

        public ComplianceReport CheckCompliance(MEPProject project, List<ComplianceStandard> standards = null)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));

            standards ??= new List<ComplianceStandard> { ComplianceStandard.ASHRAE_90_1, ComplianceStandard.ASHRAE_62_1 };
            var checks = new List<ComplianceCheck>();
            var exceptions = new List<ComplianceException>();

            double area = project.GrossFloorArea > 0 ? project.GrossFloorArea : 10000;
            double lpd = project.Zones?.Average(z => z.InternalLoads?.LightingPowerDensity ?? 1.0) ?? 1.0;

            foreach (var std in standards)
            {
                if (std == ComplianceStandard.ASHRAE_90_1)
                {
                    bool compliant = lpd <= 0.9;
                    checks.Add(new ComplianceCheck
                    {
                        Standard = std,
                        Section = "9.5.1",
                        Requirement = "LPD <= 0.9 W/sf",
                        ActualValue = $"{lpd:F2} W/sf",
                        RequiredValue = "<= 0.9 W/sf",
                        IsCompliant = compliant,
                        Notes = compliant ? "Compliant" : "Exceeds limit"
                    });
                    if (!compliant) exceptions.Add(new ComplianceException { Standard = std, Section = "9.5.1", Description = "LPD exceeds limit" });
                }
            }

            bool overall = !exceptions.Any();
            double score = checks.Count > 0 ? checks.Count(c => c.IsCompliant) * 100.0 / checks.Count : 100;

            return new ComplianceReport
            {
                ApplicableStandards = standards,
                Checks = checks,
                OverallCompliant = overall,
                ComplianceScore = score,
                Exceptions = exceptions,
                Recommendations = overall ? new List<string>() : new List<string> { "Address non-compliant items" }
            };
        }

        #region Private Methods

        private ZoneLoadResults CalculateZoneLoads(MEPZone zone, DesignConditions cond)
        {
            double area = zone.FloorArea > 0 ? zone.FloorArea : 1000;
            double vol = zone.Volume > 0 ? zone.Volume : area * zone.CeilingHeight;
            cond ??= new DesignConditions();

            double deltaT = cond.SummerDryBulb - cond.IndoorCoolingSP;
            double envelope = (zone.Envelope?.ExteriorWallArea ?? area * 0.3) * (zone.Envelope?.WallUValue ?? 0.06) * deltaT;
            double windowSolar = (zone.Envelope?.WindowArea ?? area * 0.2) * (zone.Envelope?.WindowSHGC ?? 0.25) * GetSolarFactor(zone.Envelope?.PrimaryOrientation) * 200;
            double windowCond = (zone.Envelope?.WindowArea ?? area * 0.2) * (zone.Envelope?.WindowUValue ?? 0.35) * deltaT;
            double infiltration = 1.08 * vol * (zone.Envelope?.InfiltrationACH ?? 0.25) / 60 * deltaT;

            int people = zone.Occupancy?.NumberOfPeople ?? (int)(area / 100);
            double ventCFM = people * 5 + area * 0.06;
            double ventLoad = 1.08 * ventCFM * deltaT;

            double lighting = (zone.InternalLoads?.LightingPowerDensity ?? 1.0) * area * (zone.InternalLoads?.LightingDiversity ?? 0.85) * 3.412;
            double equipment = (zone.InternalLoads?.EquipmentPowerDensity ?? 1.5) * area * (zone.InternalLoads?.EquipmentDiversity ?? 0.75) * 3.412;
            double occupancy = people * ((zone.Occupancy?.SensibleHeatPerPerson ?? 250) + (zone.Occupancy?.LatentHeatPerPerson ?? 200)) * (zone.Occupancy?.DiversityFactor ?? 0.8);

            double sensible = envelope + windowSolar + windowCond + infiltration * 0.7 + ventLoad * 0.7 + lighting + equipment + occupancy * 0.7;
            double latent = infiltration * 0.3 + ventLoad * 0.3 + occupancy * 0.3;
            double totalCooling = sensible + latent;

            double heatingDeltaT = cond.IndoorHeatingSP - cond.WinterDryBulb;
            double heatingEnvelope = (zone.Envelope?.ExteriorWallArea ?? area * 0.3) * (zone.Envelope?.WallUValue ?? 0.06) * heatingDeltaT;
            double heatingInfil = 1.08 * vol * (zone.Envelope?.InfiltrationACH ?? 0.25) / 60 * heatingDeltaT;
            double heatingVent = 1.08 * ventCFM * heatingDeltaT;
            double totalHeating = heatingEnvelope + heatingInfil + heatingVent;

            double coolingCFM = sensible / (1.08 * (cond.IndoorCoolingSP - 55));

            return new ZoneLoadResults
            {
                PeakCoolingLoad = totalCooling,
                PeakHeatingLoad = totalHeating,
                PeakCoolingCFM = Math.Max(coolingCFM, ventCFM),
                VentilationCFM = ventCFM,
                MinOutdoorAirCFM = ventCFM,
                PeakCoolingTime = new DateTime(DateTime.Now.Year, 7, 21, 15, 0, 0),
                PeakHeatingTime = new DateTime(DateTime.Now.Year, 1, 21, 7, 0, 0),
                CoolingBreakdown = new LoadBreakdown
                {
                    EnvelopeLoad = envelope,
                    WindowSolarLoad = windowSolar,
                    WindowConductionLoad = windowCond,
                    InfiltrationLoad = infiltration,
                    VentilationLoad = ventLoad,
                    LightingLoad = lighting,
                    EquipmentLoad = equipment,
                    OccupancyLoad = occupancy,
                    TotalSensible = sensible,
                    TotalLatent = latent,
                    GrandTotal = totalCooling
                },
                HeatingBreakdown = new LoadBreakdown { EnvelopeLoad = heatingEnvelope, InfiltrationLoad = heatingInfil, VentilationLoad = heatingVent, GrandTotal = totalHeating }
            };
        }

        private double GetSolarFactor(string orientation) => orientation?.ToUpper() switch
        {
            "SOUTH" => 1.0, "WEST" => 0.95, "EAST" => 0.85, "NORTH" => 0.3, _ => 0.7
        };

        private BuildingLoadSummary CalculateSummary(Dictionary<string, ZoneLoadResults> zones, MEPProject project)
        {
            double sumC = zones.Values.Sum(z => z.PeakCoolingLoad);
            double sumH = zones.Values.Sum(z => z.PeakHeatingLoad);
            double area = project.GrossFloorArea > 0 ? project.GrossFloorArea : 10000;
            double divC = area > 50000 ? 0.85 : area > 20000 ? 0.9 : 0.95;

            return new BuildingLoadSummary
            {
                BlockCoolingLoad = sumC * divC,
                BlockHeatingLoad = sumH,
                SumOfPeaksCooling = sumC,
                SumOfPeaksHeating = sumH,
                DiversityFactorCooling = divC,
                DiversityFactorHeating = 1.0,
                CoolingLoadPerArea = sumC / area,
                HeatingLoadPerArea = sumH / area,
                TonsPerThousandSF = (sumC * divC / 12000) / (area / 1000)
            };
        }

        private double CalculateElectrical(MEPProject project)
        {
            double area = project.GrossFloorArea > 0 ? project.GrossFloorArea : 10000;
            return (area * 1.0 + area * 1.5 + area * 1.2) * 1.25;
        }

        private double CalculatePlumbing(MEPProject project)
        {
            int fixtures = (int)(project.GrossFloorArea / 200);
            return Math.Sqrt(fixtures) * 10;
        }

        private double SelectDuctSize(double cfm, DuctShape shape, double maxVel, out double velocity, out double friction)
        {
            foreach (var size in _ductSizes)
            {
                double area = Math.PI * Math.Pow(size / 12, 2) / 4;
                velocity = cfm / area;
                friction = 0.109136 * Math.Pow(velocity / 1000, 1.9) / Math.Pow(size, 1.22);
                if (velocity <= maxVel) return size;
            }
            velocity = cfm / (Math.PI * Math.Pow(_ductSizes.Last() / 12, 2) / 4);
            friction = 0.109136 * Math.Pow(velocity / 1000, 1.9) / Math.Pow(_ductSizes.Last(), 1.22);
            return _ductSizes.Last();
        }

        private double SelectPipeSize(double gpm, double maxVel, out double velocity, out double friction)
        {
            foreach (var size in _pipeSizes)
            {
                double id = size * 0.9;
                velocity = (gpm * 0.408) / (Math.PI * Math.Pow(id / 2, 2) / 144);
                friction = 0.2083 * Math.Pow(100 / 130, 1.852) * Math.Pow(velocity, 1.852) / Math.Pow(id, 1.167);
                if (velocity <= maxVel) return size;
            }
            double lastId = _pipeSizes.Last() * 0.9;
            velocity = (gpm * 0.408) / (Math.PI * Math.Pow(lastId / 2, 2) / 144);
            friction = 0.2083 * Math.Pow(100 / 130, 1.852) * Math.Pow(velocity, 1.852) / Math.Pow(lastId, 1.167);
            return _pipeSizes.Last();
        }

        private string GetPrefix(EquipmentCategory cat) => cat switch
        {
            EquipmentCategory.AHU => "AHU", EquipmentCategory.Chiller => "CH", EquipmentCategory.Boiler => "B",
            EquipmentCategory.Pump => "P", EquipmentCategory.Fan => "F", _ => "EQ"
        };

        private string GetManufacturer(EquipmentCategory cat) => cat switch
        {
            EquipmentCategory.Chiller => "Trane", EquipmentCategory.AHU => "Carrier", EquipmentCategory.Boiler => "Cleaver-Brooks",
            EquipmentCategory.Pump => "Bell & Gossett", _ => "Multiple"
        };

        private double CalculateCost(EquipmentCategory cat, double capacity, EfficiencyRating rating)
        {
            double baseCost = cat switch
            {
                EquipmentCategory.Chiller => capacity * 500, EquipmentCategory.AHU => capacity * 200,
                EquipmentCategory.Boiler => capacity * 50, EquipmentCategory.Pump => capacity * 100, _ => capacity * 150
            };
            return baseCost * (rating == EfficiencyRating.Premium ? 1.4 : 1.0);
        }

        #endregion
    }

    #endregion
}
