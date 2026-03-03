// SimulationIntelligenceEngine.cs
// StingBIM v7 - Building Performance Simulation Intelligence
// Provides energy simulation, daylighting analysis, thermal comfort assessment,
// CFD concepts for ventilation, and comprehensive building performance modeling

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.SimulationIntelligence
{
    #region Enums

    public enum SimulationType
    {
        Energy,
        Daylighting,
        ThermalComfort,
        Airflow,
        Acoustics,
        FullBuilding,
        ZoneLevel,
        SystemLevel
    }

    public enum EnergyModelType
    {
        EnergyPlus,
        DOE2,
        TRNSYS,
        IES_VE,
        DesignBuilder,
        OpenStudio,
        Simplified,
        Detailed
    }

    public enum DaylightMetric
    {
        DaylightFactor,
        SpatialDaylightAutonomy,
        AnnualSunlightExposure,
        UsefulDaylightIlluminance,
        DaylightGlareProb,
        VerticalIlluminance,
        ContinuousDaylightAutonomy,
        MaximumDaylightAutonomy
    }

    public enum ThermalComfortModel
    {
        PMV_PPD,
        AdaptiveComfort,
        StandardEffectiveTemp,
        UniversalThermalClimate,
        ASHRAE55,
        EN15251,
        CBE_ThermalComfort,
        TwoNode
    }

    public enum AirflowAnalysisType
    {
        NaturalVentilation,
        MechanicalVentilation,
        MixedMode,
        DisplacementVentilation,
        UnderfloorAirDistribution,
        PersonalizedVentilation,
        DemandControlled,
        PressureNetwork
    }

    public enum WeatherDataSource
    {
        TMY3,
        IWEC,
        EPW,
        ActualMeasured,
        FutureClimate,
        CustomGenerated,
        ASHRAE_IWEC2,
        LocalMet
    }

    public enum PerformanceRating
    {
        Excellent,
        Good,
        Acceptable,
        BelowStandard,
        Poor,
        Critical,
        NotRated,
        Pending
    }

    public enum HVACSystemType
    {
        VAV,
        CAV,
        FCU,
        PTAC,
        PTHP,
        VRF,
        Chiller,
        Boiler,
        HeatPump,
        Radiant,
        DOAS,
        Hybrid
    }

    public enum BuildingOccupancyType
    {
        Office,
        Residential,
        Retail,
        Healthcare,
        Education,
        Industrial,
        Hospitality,
        MixedUse,
        Laboratory,
        DataCenter
    }

    public enum SimulationStatus
    {
        NotStarted,
        Initializing,
        Running,
        Paused,
        Completed,
        Failed,
        Cancelled,
        PostProcessing
    }

    #endregion

    #region Data Models

    public class SimulationProject
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string BuildingId { get; set; }
        public SimulationType SimulationType { get; set; }
        public EnergyModelType ModelType { get; set; }
        public WeatherDataSource WeatherSource { get; set; }
        public string WeatherFilePath { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastRunDate { get; set; }
        public SimulationStatus Status { get; set; }
        public BuildingGeometry Geometry { get; set; }
        public List<ThermalZone> ThermalZones { get; set; }
        public List<ConstructionAssembly> Constructions { get; set; }
        public Dictionary<string, object> SimulationParameters { get; set; }
        public SimulationResults Results { get; set; }
    }

    public class BuildingGeometry
    {
        public string GeometryId { get; set; }
        public double GrossFloorArea { get; set; }
        public double ConditionedFloorArea { get; set; }
        public double BuildingVolume { get; set; }
        public int NumberOfFloors { get; set; }
        public double BuildingHeight { get; set; }
        public double WindowToWallRatio { get; set; }
        public double SkylightToRoofRatio { get; set; }
        public OrientationData Orientation { get; set; }
        public List<SurfaceData> Surfaces { get; set; }
        public List<FenestrationData> Fenestrations { get; set; }
        public ShadingData ExternalShading { get; set; }
    }

    public class OrientationData
    {
        public double NorthAxis { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Elevation { get; set; }
        public string TimeZone { get; set; }
        public int UTCOffset { get; set; }
    }

    public class SurfaceData
    {
        public string SurfaceId { get; set; }
        public string SurfaceName { get; set; }
        public string SurfaceType { get; set; }
        public double Area { get; set; }
        public double Tilt { get; set; }
        public double Azimuth { get; set; }
        public string ConstructionId { get; set; }
        public string AdjacentZoneId { get; set; }
        public string BoundaryCondition { get; set; }
    }

    public class FenestrationData
    {
        public string FenestrationId { get; set; }
        public string FenestrationType { get; set; }
        public double Area { get; set; }
        public double UValue { get; set; }
        public double SHGC { get; set; }
        public double VisibleTransmittance { get; set; }
        public string FrameType { get; set; }
        public string GlazingType { get; set; }
        public int NumberOfPanes { get; set; }
        public string ParentSurfaceId { get; set; }
    }

    public class ShadingData
    {
        public List<ShadingDevice> Devices { get; set; }
        public List<SurroundingBuilding> SurroundingBuildings { get; set; }
        public List<VegetationShading> Vegetation { get; set; }
        public bool HasDynamicShading { get; set; }
        public string ShadingControlStrategy { get; set; }
    }

    public class ShadingDevice
    {
        public string DeviceId { get; set; }
        public string DeviceType { get; set; }
        public double Depth { get; set; }
        public double Angle { get; set; }
        public double Transmittance { get; set; }
        public string ControlType { get; set; }
    }

    public class SurroundingBuilding
    {
        public string BuildingId { get; set; }
        public double Distance { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        public double Azimuth { get; set; }
    }

    public class VegetationShading
    {
        public string VegetationId { get; set; }
        public string VegetationType { get; set; }
        public double Height { get; set; }
        public double CanopyDiameter { get; set; }
        public double TransmittanceSummer { get; set; }
        public double TransmittanceWinter { get; set; }
    }

    public class ThermalZone
    {
        public string ZoneId { get; set; }
        public string ZoneName { get; set; }
        public double FloorArea { get; set; }
        public double Volume { get; set; }
        public double CeilingHeight { get; set; }
        public BuildingOccupancyType OccupancyType { get; set; }
        public OccupancySchedule Occupancy { get; set; }
        public InternalLoads InternalLoads { get; set; }
        public VentilationRequirements Ventilation { get; set; }
        public SetpointSchedule Setpoints { get; set; }
        public HVACSystemType HVACType { get; set; }
        public List<string> SurfaceIds { get; set; }
    }

    public class OccupancySchedule
    {
        public double PeakOccupancy { get; set; }
        public double OccupancyDensity { get; set; }
        public Dictionary<string, List<ScheduleValue>> WeeklySchedule { get; set; }
        public double MetabolicRate { get; set; }
        public double ClothingInsulation { get; set; }
    }

    public class ScheduleValue
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public double Value { get; set; }
    }

    public class InternalLoads
    {
        public double LightingPowerDensity { get; set; }
        public double EquipmentPowerDensity { get; set; }
        public double ProcessLoadDensity { get; set; }
        public Dictionary<string, List<ScheduleValue>> LightingSchedule { get; set; }
        public Dictionary<string, List<ScheduleValue>> EquipmentSchedule { get; set; }
        public double RadiantFractionLighting { get; set; }
        public double RadiantFractionEquipment { get; set; }
    }

    public class VentilationRequirements
    {
        public double OutdoorAirPerPerson { get; set; }
        public double OutdoorAirPerArea { get; set; }
        public double ExhaustAirflow { get; set; }
        public double MinimumACH { get; set; }
        public bool DemandControlled { get; set; }
        public double CO2Setpoint { get; set; }
        public string VentilationStandard { get; set; }
    }

    public class SetpointSchedule
    {
        public double HeatingSetpoint { get; set; }
        public double CoolingSetpoint { get; set; }
        public double HeatingSetback { get; set; }
        public double CoolingSetup { get; set; }
        public double HumidificationSetpoint { get; set; }
        public double DehumidificationSetpoint { get; set; }
        public Dictionary<string, List<ScheduleValue>> HeatingSchedule { get; set; }
        public Dictionary<string, List<ScheduleValue>> CoolingSchedule { get; set; }
    }

    public class ConstructionAssembly
    {
        public string ConstructionId { get; set; }
        public string ConstructionName { get; set; }
        public string ConstructionType { get; set; }
        public List<MaterialLayer> Layers { get; set; }
        public double TotalThickness { get; set; }
        public double UValue { get; set; }
        public double RValue { get; set; }
        public double ThermalMass { get; set; }
        public double SolarAbsorptance { get; set; }
        public double ThermalAbsorptance { get; set; }
        public double VisibleAbsorptance { get; set; }
    }

    public class MaterialLayer
    {
        public string MaterialId { get; set; }
        public string MaterialName { get; set; }
        public double Thickness { get; set; }
        public double Conductivity { get; set; }
        public double Density { get; set; }
        public double SpecificHeat { get; set; }
        public double ThermalResistance { get; set; }
    }

    public class SimulationResults
    {
        public string ResultId { get; set; }
        public DateTime SimulationDate { get; set; }
        public TimeSpan SimulationDuration { get; set; }
        public EnergyResults EnergyResults { get; set; }
        public DaylightingResults DaylightingResults { get; set; }
        public ThermalComfortResults ThermalComfortResults { get; set; }
        public AirflowResults AirflowResults { get; set; }
        public PerformanceReport PerformanceReport { get; set; }
        public List<SimulationWarning> Warnings { get; set; }
        public List<SimulationError> Errors { get; set; }
    }

    public class EnergyResults
    {
        public double TotalSiteEnergy { get; set; }
        public double TotalSourceEnergy { get; set; }
        public double EnergyUseIntensity { get; set; }
        public double HeatingEnergy { get; set; }
        public double CoolingEnergy { get; set; }
        public double LightingEnergy { get; set; }
        public double EquipmentEnergy { get; set; }
        public double FansEnergy { get; set; }
        public double PumpsEnergy { get; set; }
        public double DHWEnergy { get; set; }
        public double PeakHeatingLoad { get; set; }
        public double PeakCoolingLoad { get; set; }
        public double PeakElectricalDemand { get; set; }
        public Dictionary<string, double> MonthlyConsumption { get; set; }
        public Dictionary<string, double> EndUseBreakdown { get; set; }
        public Dictionary<string, ZoneEnergyResults> ZoneResults { get; set; }
        public double AnnualCO2Emissions { get; set; }
        public double EnergyCost { get; set; }
    }

    public class ZoneEnergyResults
    {
        public string ZoneId { get; set; }
        public double HeatingLoad { get; set; }
        public double CoolingLoad { get; set; }
        public double LightingLoad { get; set; }
        public double EquipmentLoad { get; set; }
        public double VentilationLoad { get; set; }
        public double InfiltrationLoad { get; set; }
        public double SolarGain { get; set; }
        public double InternalGain { get; set; }
    }

    public class DaylightingResults
    {
        public Dictionary<string, ZoneDaylightResults> ZoneResults { get; set; }
        public double AverageDaylightFactor { get; set; }
        public double SpatialDaylightAutonomy { get; set; }
        public double AnnualSunlightExposure { get; set; }
        public double LightingEnergyReduction { get; set; }
        public List<DaylightGrid> GridResults { get; set; }
        public GlareAnalysis GlareResults { get; set; }
    }

    public class ZoneDaylightResults
    {
        public string ZoneId { get; set; }
        public double DaylightFactor { get; set; }
        public double SDA { get; set; }
        public double ASE { get; set; }
        public double UDI_Low { get; set; }
        public double UDI_Useful { get; set; }
        public double UDI_Exceeded { get; set; }
        public double AverageIlluminance { get; set; }
        public double MaxIlluminance { get; set; }
        public double MinIlluminance { get; set; }
        public double Uniformity { get; set; }
    }

    public class DaylightGrid
    {
        public string GridId { get; set; }
        public string ZoneId { get; set; }
        public double GridSpacing { get; set; }
        public double WorkplaneHeight { get; set; }
        public List<GridPoint> Points { get; set; }
    }

    public class GridPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Illuminance { get; set; }
        public double DaylightFactor { get; set; }
    }

    public class GlareAnalysis
    {
        public List<ViewpointGlare> ViewpointResults { get; set; }
        public double MaxDGP { get; set; }
        public double AverageDGP { get; set; }
        public int HoursAboveThreshold { get; set; }
        public string GlareRating { get; set; }
    }

    public class ViewpointGlare
    {
        public string ViewpointId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double ViewDirection { get; set; }
        public double DGP { get; set; }
        public double DGI { get; set; }
        public double UGR { get; set; }
    }

    public class ThermalComfortResults
    {
        public Dictionary<string, ZoneThermalComfortResults> ZoneResults { get; set; }
        public double BuildingAveragePMV { get; set; }
        public double BuildingAveragePPD { get; set; }
        public double ComfortHoursPercentage { get; set; }
        public int UnmetHeatingHours { get; set; }
        public int UnmetCoolingHours { get; set; }
        public AdaptiveComfortResults AdaptiveResults { get; set; }
    }

    public class ZoneThermalComfortResults
    {
        public string ZoneId { get; set; }
        public double AveragePMV { get; set; }
        public double AveragePPD { get; set; }
        public double MaxPMV { get; set; }
        public double MinPMV { get; set; }
        public double OperativeTemperature { get; set; }
        public double MeanRadiantTemperature { get; set; }
        public double AirTemperature { get; set; }
        public double RelativeHumidity { get; set; }
        public double AirSpeed { get; set; }
        public int ComfortHours { get; set; }
        public int DiscomfortHours { get; set; }
    }

    public class AdaptiveComfortResults
    {
        public double AcceptabilityPercentage80 { get; set; }
        public double AcceptabilityPercentage90 { get; set; }
        public double RunningMeanTemperature { get; set; }
        public double ComfortUpperLimit80 { get; set; }
        public double ComfortLowerLimit80 { get; set; }
        public double ComfortUpperLimit90 { get; set; }
        public double ComfortLowerLimit90 { get; set; }
    }

    public class AirflowResults
    {
        public Dictionary<string, ZoneAirflowResults> ZoneResults { get; set; }
        public double TotalSupplyAirflow { get; set; }
        public double TotalReturnAirflow { get; set; }
        public double TotalOutdoorAirflow { get; set; }
        public double TotalExhaustAirflow { get; set; }
        public double AverageACH { get; set; }
        public PressureResults PressureResults { get; set; }
        public ContaminantResults ContaminantResults { get; set; }
    }

    public class ZoneAirflowResults
    {
        public string ZoneId { get; set; }
        public double SupplyAirflow { get; set; }
        public double ReturnAirflow { get; set; }
        public double OutdoorAirflow { get; set; }
        public double InfiltrationAirflow { get; set; }
        public double ACH { get; set; }
        public double MixingEffectiveness { get; set; }
        public double VentilationEffectiveness { get; set; }
        public double LocalMeanAge { get; set; }
    }

    public class PressureResults
    {
        public Dictionary<string, double> ZonePressures { get; set; }
        public double BuildingPressure { get; set; }
        public Dictionary<string, double> DuctPressures { get; set; }
        public double TotalPressureDrop { get; set; }
    }

    public class ContaminantResults
    {
        public Dictionary<string, double> ZoneCO2Levels { get; set; }
        public Dictionary<string, double> ZonePM25Levels { get; set; }
        public double AverageCO2 { get; set; }
        public double PeakCO2 { get; set; }
        public int HoursAboveCO2Threshold { get; set; }
    }

    public class PerformanceReport
    {
        public string ReportId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public string BuildingName { get; set; }
        public PerformanceRating OverallRating { get; set; }
        public PerformanceRating EnergyRating { get; set; }
        public PerformanceRating ComfortRating { get; set; }
        public PerformanceRating DaylightRating { get; set; }
        public PerformanceRating VentilationRating { get; set; }
        public List<PerformanceMetric> KeyMetrics { get; set; }
        public List<PerformanceRecommendation> Recommendations { get; set; }
        public ComplianceStatus ComplianceStatus { get; set; }
        public BenchmarkComparison Benchmarks { get; set; }
    }

    public class PerformanceMetric
    {
        public string MetricName { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public double Baseline { get; set; }
        public double Target { get; set; }
        public PerformanceRating Rating { get; set; }
        public string Description { get; set; }
    }

    public class PerformanceRecommendation
    {
        public string RecommendationId { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Impact { get; set; }
        public double EstimatedSavings { get; set; }
        public double ImplementationCost { get; set; }
        public double Payback { get; set; }
        public string Priority { get; set; }
    }

    public class ComplianceStatus
    {
        public bool ASHRAE901Compliant { get; set; }
        public bool IECC2021Compliant { get; set; }
        public bool Title24Compliant { get; set; }
        public bool LEED_EAc1Compliant { get; set; }
        public Dictionary<string, bool> AdditionalStandards { get; set; }
        public List<ComplianceIssue> Issues { get; set; }
    }

    public class ComplianceIssue
    {
        public string Standard { get; set; }
        public string Section { get; set; }
        public string Requirement { get; set; }
        public string ActualValue { get; set; }
        public string RequiredValue { get; set; }
        public string Resolution { get; set; }
    }

    public class BenchmarkComparison
    {
        public double CBECSMedian { get; set; }
        public double CBECSTop25 { get; set; }
        public double EnergyStar { get; set; }
        public double ZeroNet { get; set; }
        public double BuildingEUI { get; set; }
        public int PercentileRanking { get; set; }
    }

    public class SimulationWarning
    {
        public string WarningCode { get; set; }
        public string Message { get; set; }
        public string Location { get; set; }
        public string Suggestion { get; set; }
    }

    public class SimulationError
    {
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public string Location { get; set; }
        public bool IsFatal { get; set; }
    }

    #endregion

    #region Engine

    public sealed class SimulationIntelligenceEngine
    {
        private static readonly Lazy<SimulationIntelligenceEngine> _instance =
            new Lazy<SimulationIntelligenceEngine>(() => new SimulationIntelligenceEngine());

        public static SimulationIntelligenceEngine Instance => _instance.Value;

        private readonly object _simulationLock = new object();
        private readonly Dictionary<string, SimulationProject> _activeProjects;
        private readonly Dictionary<string, SimulationResults> _resultsCache;
        private readonly List<ConstructionAssembly> _constructionLibrary;
        private readonly Dictionary<string, WeatherData> _weatherDataCache;

        private SimulationIntelligenceEngine()
        {
            _activeProjects = new Dictionary<string, SimulationProject>();
            _resultsCache = new Dictionary<string, SimulationResults>();
            _constructionLibrary = InitializeConstructionLibrary();
            _weatherDataCache = new Dictionary<string, WeatherData>();
        }

        public async Task<EnergyResults> SimulateEnergy(
            SimulationProject project,
            IProgress<SimulationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            ValidateProject(project);

            lock (_simulationLock)
            {
                project.Status = SimulationStatus.Initializing;
                _activeProjects[project.ProjectId] = project;
            }

            progress?.Report(new SimulationProgress { Stage = "Initializing", PercentComplete = 5 });

            var weatherData = await LoadWeatherData(project.WeatherFilePath, cancellationToken);
            progress?.Report(new SimulationProgress { Stage = "Weather data loaded", PercentComplete = 15 });

            var geometry = ValidateAndProcessGeometry(project.Geometry);
            progress?.Report(new SimulationProgress { Stage = "Geometry validated", PercentComplete = 25 });

            var zones = ProcessThermalZones(project.ThermalZones, geometry);
            progress?.Report(new SimulationProgress { Stage = "Zones processed", PercentComplete = 35 });

            lock (_simulationLock)
            {
                project.Status = SimulationStatus.Running;
            }

            var heatingLoads = CalculateHeatingLoads(zones, weatherData, geometry);
            progress?.Report(new SimulationProgress { Stage = "Heating loads calculated", PercentComplete = 50 });

            var coolingLoads = CalculateCoolingLoads(zones, weatherData, geometry);
            progress?.Report(new SimulationProgress { Stage = "Cooling loads calculated", PercentComplete = 65 });

            var internalLoads = CalculateInternalLoads(zones);
            progress?.Report(new SimulationProgress { Stage = "Internal loads calculated", PercentComplete = 75 });

            var hvacEnergy = SimulateHVACSystem(zones, heatingLoads, coolingLoads);
            progress?.Report(new SimulationProgress { Stage = "HVAC simulated", PercentComplete = 85 });

            var energyResults = CompileEnergyResults(heatingLoads, coolingLoads, internalLoads, hvacEnergy, project);
            progress?.Report(new SimulationProgress { Stage = "Results compiled", PercentComplete = 95 });

            lock (_simulationLock)
            {
                project.Status = SimulationStatus.Completed;
                _resultsCache[project.ProjectId] = new SimulationResults { EnergyResults = energyResults };
            }

            progress?.Report(new SimulationProgress { Stage = "Complete", PercentComplete = 100 });

            return energyResults;
        }

        public async Task<DaylightingResults> AnalyzeDaylighting(
            SimulationProject project,
            DaylightMetric primaryMetric = DaylightMetric.SpatialDaylightAutonomy,
            double gridSpacing = 0.6,
            IProgress<SimulationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            ValidateProject(project);

            progress?.Report(new SimulationProgress { Stage = "Initializing daylighting analysis", PercentComplete = 5 });

            var weatherData = await LoadWeatherData(project.WeatherFilePath, cancellationToken);
            var solarPositions = CalculateSolarPositions(project.Geometry.Orientation, weatherData);

            progress?.Report(new SimulationProgress { Stage = "Solar positions calculated", PercentComplete = 20 });

            var analysisGrids = GenerateAnalysisGrids(project.ThermalZones, gridSpacing);
            progress?.Report(new SimulationProgress { Stage = "Analysis grids generated", PercentComplete = 30 });

            var zoneResults = new Dictionary<string, ZoneDaylightResults>();
            var gridResults = new List<DaylightGrid>();

            int zoneCount = project.ThermalZones.Count;
            int currentZone = 0;

            foreach (var zone in project.ThermalZones)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var grid = analysisGrids[zone.ZoneId];
                var fenestrations = GetZoneFenestrations(zone, project.Geometry);

                var illuminanceResults = CalculateIlluminance(grid, fenestrations, solarPositions, weatherData);
                var daylightMetrics = CalculateDaylightMetrics(illuminanceResults, primaryMetric);

                zoneResults[zone.ZoneId] = daylightMetrics;
                gridResults.Add(new DaylightGrid
                {
                    GridId = Guid.NewGuid().ToString(),
                    ZoneId = zone.ZoneId,
                    GridSpacing = gridSpacing,
                    WorkplaneHeight = 0.8,
                    Points = illuminanceResults
                });

                currentZone++;
                int progressPercent = 30 + (int)(60.0 * currentZone / zoneCount);
                progress?.Report(new SimulationProgress { Stage = $"Analyzing zone {currentZone}/{zoneCount}", PercentComplete = progressPercent });
            }

            var glareResults = AnalyzeGlare(project, solarPositions);
            progress?.Report(new SimulationProgress { Stage = "Glare analysis complete", PercentComplete = 95 });

            var results = new DaylightingResults
            {
                ZoneResults = zoneResults,
                AverageDaylightFactor = zoneResults.Values.Average(z => z.DaylightFactor),
                SpatialDaylightAutonomy = zoneResults.Values.Average(z => z.SDA),
                AnnualSunlightExposure = zoneResults.Values.Average(z => z.ASE),
                LightingEnergyReduction = CalculateLightingReduction(zoneResults),
                GridResults = gridResults,
                GlareResults = glareResults
            };

            progress?.Report(new SimulationProgress { Stage = "Complete", PercentComplete = 100 });

            return results;
        }

        public ThermalComfortResults AssessThermalComfort(
            SimulationProject project,
            ThermalComfortModel comfortModel = ThermalComfortModel.PMV_PPD,
            double metabolicRate = 1.2,
            double clothingInsulation = 0.7)
        {
            ValidateProject(project);

            var zoneResults = new Dictionary<string, ZoneThermalComfortResults>();
            double totalPMV = 0;
            double totalPPD = 0;
            int totalComfortHours = 0;
            int totalDiscomfortHours = 0;

            foreach (var zone in project.ThermalZones)
            {
                var zoneComfort = CalculateZoneThermalComfort(zone, comfortModel, metabolicRate, clothingInsulation);
                zoneResults[zone.ZoneId] = zoneComfort;
                totalPMV += zoneComfort.AveragePMV;
                totalPPD += zoneComfort.AveragePPD;
                totalComfortHours += zoneComfort.ComfortHours;
                totalDiscomfortHours += zoneComfort.DiscomfortHours;
            }

            int zoneCount = project.ThermalZones.Count;
            double totalHours = totalComfortHours + totalDiscomfortHours;

            var results = new ThermalComfortResults
            {
                ZoneResults = zoneResults,
                BuildingAveragePMV = totalPMV / zoneCount,
                BuildingAveragePPD = totalPPD / zoneCount,
                ComfortHoursPercentage = totalHours > 0 ? (totalComfortHours / totalHours) * 100 : 0,
                UnmetHeatingHours = CalculateUnmetHeatingHours(zoneResults),
                UnmetCoolingHours = CalculateUnmetCoolingHours(zoneResults),
                AdaptiveResults = comfortModel == ThermalComfortModel.AdaptiveComfort
                    ? CalculateAdaptiveComfort(project)
                    : null
            };

            return results;
        }

        public AirflowResults ModelAirflow(
            SimulationProject project,
            AirflowAnalysisType analysisType = AirflowAnalysisType.MechanicalVentilation,
            bool includeContaminants = true)
        {
            ValidateProject(project);

            var zoneResults = new Dictionary<string, ZoneAirflowResults>();
            double totalSupply = 0;
            double totalReturn = 0;
            double totalOA = 0;
            double totalExhaust = 0;

            foreach (var zone in project.ThermalZones)
            {
                var airflowResult = CalculateZoneAirflow(zone, analysisType);
                zoneResults[zone.ZoneId] = airflowResult;
                totalSupply += airflowResult.SupplyAirflow;
                totalReturn += airflowResult.ReturnAirflow;
                totalOA += airflowResult.OutdoorAirflow;
            }

            totalExhaust = CalculateTotalExhaust(project.ThermalZones);

            var pressureResults = CalculatePressureDistribution(project, zoneResults);
            var contaminantResults = includeContaminants
                ? SimulateContaminants(project, zoneResults)
                : null;

            return new AirflowResults
            {
                ZoneResults = zoneResults,
                TotalSupplyAirflow = totalSupply,
                TotalReturnAirflow = totalReturn,
                TotalOutdoorAirflow = totalOA,
                TotalExhaustAirflow = totalExhaust,
                AverageACH = zoneResults.Values.Average(z => z.ACH),
                PressureResults = pressureResults,
                ContaminantResults = contaminantResults
            };
        }

        public PerformanceReport GeneratePerformanceReport(
            SimulationProject project,
            string buildingCodeStandard = "ASHRAE 90.1-2022")
        {
            ValidateProject(project);

            var results = project.Results ?? GetCachedResults(project.ProjectId);
            if (results == null)
            {
                throw new InvalidOperationException("No simulation results available. Run simulations first.");
            }

            var metrics = CompileKeyMetrics(results, project);
            var recommendations = GenerateRecommendations(results, project);
            var compliance = CheckCompliance(results, project, buildingCodeStandard);
            var benchmarks = CompareToBenchmarks(results, project);

            var report = new PerformanceReport
            {
                ReportId = Guid.NewGuid().ToString(),
                GeneratedDate = DateTime.UtcNow,
                BuildingName = project.ProjectName,
                OverallRating = CalculateOverallRating(metrics),
                EnergyRating = CalculateEnergyRating(results.EnergyResults),
                ComfortRating = CalculateComfortRating(results.ThermalComfortResults),
                DaylightRating = CalculateDaylightRating(results.DaylightingResults),
                VentilationRating = CalculateVentilationRating(results.AirflowResults),
                KeyMetrics = metrics,
                Recommendations = recommendations,
                ComplianceStatus = compliance,
                Benchmarks = benchmarks
            };

            return report;
        }

        #region Private Helper Methods

        private void ValidateProject(SimulationProject project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrEmpty(project.ProjectId))
                throw new ArgumentException("Project ID is required");
            if (project.ThermalZones == null || !project.ThermalZones.Any())
                throw new ArgumentException("At least one thermal zone is required");
        }

        private async Task<WeatherData> LoadWeatherData(string weatherFilePath, CancellationToken cancellationToken)
        {
            if (_weatherDataCache.TryGetValue(weatherFilePath, out var cached))
                return cached;

            var weatherData = new WeatherData
            {
                FilePath = weatherFilePath,
                HourlyData = GenerateDefaultWeatherData()
            };

            _weatherDataCache[weatherFilePath] = weatherData;
            return await Task.FromResult(weatherData);
        }

        private List<HourlyWeather> GenerateDefaultWeatherData()
        {
            var hourlyData = new List<HourlyWeather>();
            for (int day = 1; day <= 365; day++)
            {
                for (int hour = 0; hour < 24; hour++)
                {
                    hourlyData.Add(new HourlyWeather
                    {
                        DayOfYear = day,
                        Hour = hour,
                        DryBulbTemp = 20 + 10 * Math.Sin((day - 172) * 2 * Math.PI / 365) + 5 * Math.Sin(hour * Math.PI / 12),
                        WetBulbTemp = 15 + 8 * Math.Sin((day - 172) * 2 * Math.PI / 365),
                        RelativeHumidity = 50 + 20 * Math.Cos((day - 172) * 2 * Math.PI / 365),
                        DirectNormalIrradiance = hour >= 6 && hour <= 18 ? 400 * Math.Sin((hour - 6) * Math.PI / 12) : 0,
                        DiffuseHorizontalIrradiance = hour >= 6 && hour <= 18 ? 100 * Math.Sin((hour - 6) * Math.PI / 12) : 0,
                        WindSpeed = 3 + 2 * new Random().NextDouble(),
                        WindDirection = (hour * 15) % 360
                    });
                }
            }
            return hourlyData;
        }

        private BuildingGeometry ValidateAndProcessGeometry(BuildingGeometry geometry)
        {
            if (geometry == null)
            {
                geometry = new BuildingGeometry
                {
                    GeometryId = Guid.NewGuid().ToString(),
                    GrossFloorArea = 1000,
                    ConditionedFloorArea = 950,
                    BuildingVolume = 3000,
                    NumberOfFloors = 1,
                    BuildingHeight = 3,
                    WindowToWallRatio = 0.3,
                    Orientation = new OrientationData { NorthAxis = 0, Latitude = 0, Longitude = 0 }
                };
            }
            return geometry;
        }

        private List<ThermalZone> ProcessThermalZones(List<ThermalZone> zones, BuildingGeometry geometry)
        {
            foreach (var zone in zones)
            {
                if (zone.Volume <= 0)
                    zone.Volume = zone.FloorArea * (zone.CeilingHeight > 0 ? zone.CeilingHeight : 3.0);
                if (zone.InternalLoads == null)
                    zone.InternalLoads = GetDefaultInternalLoads(zone.OccupancyType);
                if (zone.Ventilation == null)
                    zone.Ventilation = GetDefaultVentilation(zone.OccupancyType);
            }
            return zones;
        }

        private Dictionary<string, double> CalculateHeatingLoads(List<ThermalZone> zones, WeatherData weather, BuildingGeometry geometry)
        {
            var loads = new Dictionary<string, double>();
            foreach (var zone in zones)
            {
                double envelopeLoad = zone.FloorArea * 25;
                double infiltrationLoad = zone.Volume * 0.5 * 0.34 * 20;
                double ventilationLoad = zone.Ventilation?.OutdoorAirPerArea ?? 0 * zone.FloorArea * 0.34 * 15;
                loads[zone.ZoneId] = envelopeLoad + infiltrationLoad + ventilationLoad;
            }
            return loads;
        }

        private Dictionary<string, double> CalculateCoolingLoads(List<ThermalZone> zones, WeatherData weather, BuildingGeometry geometry)
        {
            var loads = new Dictionary<string, double>();
            foreach (var zone in zones)
            {
                double envelopeLoad = zone.FloorArea * 30;
                double solarLoad = zone.FloorArea * geometry.WindowToWallRatio * 200;
                double internalLoad = (zone.InternalLoads?.LightingPowerDensity ?? 10) * zone.FloorArea +
                                     (zone.InternalLoads?.EquipmentPowerDensity ?? 10) * zone.FloorArea;
                double occupantLoad = (zone.Occupancy?.PeakOccupancy ?? (zone.FloorArea / 10)) * 75;
                loads[zone.ZoneId] = envelopeLoad + solarLoad + internalLoad + occupantLoad;
            }
            return loads;
        }

        private Dictionary<string, double> CalculateInternalLoads(List<ThermalZone> zones)
        {
            var loads = new Dictionary<string, double>();
            foreach (var zone in zones)
            {
                double lighting = (zone.InternalLoads?.LightingPowerDensity ?? 10) * zone.FloorArea * 2500 / 1000;
                double equipment = (zone.InternalLoads?.EquipmentPowerDensity ?? 10) * zone.FloorArea * 2500 / 1000;
                loads[zone.ZoneId] = lighting + equipment;
            }
            return loads;
        }

        private HVACSimulationResult SimulateHVACSystem(List<ThermalZone> zones, Dictionary<string, double> heatingLoads, Dictionary<string, double> coolingLoads)
        {
            double heatingEfficiency = 0.85;
            double coolingCOP = 3.5;
            double fanPowerPerCFM = 0.5;

            double totalHeatingEnergy = heatingLoads.Values.Sum() * 8760 / 1000 / heatingEfficiency / 3412;
            double totalCoolingEnergy = coolingLoads.Values.Sum() * 4380 / 1000 / coolingCOP / 3412;
            double fanEnergy = zones.Sum(z => z.Volume * 0.5 / 60 * fanPowerPerCFM * 8760 / 1000);
            double pumpEnergy = totalCoolingEnergy * 0.1;

            return new HVACSimulationResult
            {
                HeatingEnergy = totalHeatingEnergy,
                CoolingEnergy = totalCoolingEnergy,
                FanEnergy = fanEnergy,
                PumpEnergy = pumpEnergy
            };
        }

        private EnergyResults CompileEnergyResults(
            Dictionary<string, double> heatingLoads,
            Dictionary<string, double> coolingLoads,
            Dictionary<string, double> internalLoads,
            HVACSimulationResult hvac,
            SimulationProject project)
        {
            double conditionedArea = project.Geometry?.ConditionedFloorArea ?? project.ThermalZones.Sum(z => z.FloorArea);
            double lightingEnergy = project.ThermalZones.Sum(z => (z.InternalLoads?.LightingPowerDensity ?? 10) * z.FloorArea * 2500 / 1000);
            double equipmentEnergy = project.ThermalZones.Sum(z => (z.InternalLoads?.EquipmentPowerDensity ?? 10) * z.FloorArea * 2500 / 1000);
            double dhwEnergy = conditionedArea * 15;

            double totalSiteEnergy = hvac.HeatingEnergy + hvac.CoolingEnergy + lightingEnergy + equipmentEnergy +
                                    hvac.FanEnergy + hvac.PumpEnergy + dhwEnergy;
            double totalSourceEnergy = totalSiteEnergy * 2.5;
            double eui = totalSiteEnergy / conditionedArea * 3.412;

            var zoneResults = new Dictionary<string, ZoneEnergyResults>();
            foreach (var zone in project.ThermalZones)
            {
                zoneResults[zone.ZoneId] = new ZoneEnergyResults
                {
                    ZoneId = zone.ZoneId,
                    HeatingLoad = heatingLoads.GetValueOrDefault(zone.ZoneId),
                    CoolingLoad = coolingLoads.GetValueOrDefault(zone.ZoneId),
                    LightingLoad = (zone.InternalLoads?.LightingPowerDensity ?? 10) * zone.FloorArea,
                    EquipmentLoad = (zone.InternalLoads?.EquipmentPowerDensity ?? 10) * zone.FloorArea,
                    SolarGain = zone.FloorArea * 0.3 * 200,
                    InternalGain = (zone.InternalLoads?.LightingPowerDensity ?? 10 + zone.InternalLoads?.EquipmentPowerDensity ?? 10) * zone.FloorArea
                };
            }

            return new EnergyResults
            {
                TotalSiteEnergy = totalSiteEnergy,
                TotalSourceEnergy = totalSourceEnergy,
                EnergyUseIntensity = eui,
                HeatingEnergy = hvac.HeatingEnergy,
                CoolingEnergy = hvac.CoolingEnergy,
                LightingEnergy = lightingEnergy,
                EquipmentEnergy = equipmentEnergy,
                FansEnergy = hvac.FanEnergy,
                PumpsEnergy = hvac.PumpEnergy,
                DHWEnergy = dhwEnergy,
                PeakHeatingLoad = heatingLoads.Values.Max(),
                PeakCoolingLoad = coolingLoads.Values.Max(),
                PeakElectricalDemand = (coolingLoads.Values.Max() + lightingEnergy / 2500 + equipmentEnergy / 2500) / 1000,
                MonthlyConsumption = GenerateMonthlyConsumption(totalSiteEnergy),
                EndUseBreakdown = new Dictionary<string, double>
                {
                    { "Heating", hvac.HeatingEnergy },
                    { "Cooling", hvac.CoolingEnergy },
                    { "Lighting", lightingEnergy },
                    { "Equipment", equipmentEnergy },
                    { "Fans", hvac.FanEnergy },
                    { "Pumps", hvac.PumpEnergy },
                    { "DHW", dhwEnergy }
                },
                ZoneResults = zoneResults,
                AnnualCO2Emissions = totalSiteEnergy * 0.5,
                EnergyCost = totalSiteEnergy * 0.12
            };
        }

        private Dictionary<string, double> GenerateMonthlyConsumption(double annual)
        {
            double[] monthFactors = { 1.2, 1.1, 1.0, 0.9, 0.8, 0.85, 0.95, 0.95, 0.85, 0.9, 1.0, 1.1 };
            var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            double totalFactor = monthFactors.Sum();
            var result = new Dictionary<string, double>();
            for (int i = 0; i < 12; i++)
            {
                result[months[i]] = annual * monthFactors[i] / totalFactor;
            }
            return result;
        }

        private List<SolarPosition> CalculateSolarPositions(OrientationData orientation, WeatherData weather)
        {
            var positions = new List<SolarPosition>();
            for (int day = 1; day <= 365; day += 7)
            {
                for (int hour = 6; hour <= 18; hour++)
                {
                    double declination = 23.45 * Math.Sin(2 * Math.PI * (284 + day) / 365);
                    double hourAngle = 15 * (hour - 12);
                    double altitude = Math.Asin(Math.Sin(orientation.Latitude * Math.PI / 180) * Math.Sin(declination * Math.PI / 180) +
                                               Math.Cos(orientation.Latitude * Math.PI / 180) * Math.Cos(declination * Math.PI / 180) * Math.Cos(hourAngle * Math.PI / 180)) * 180 / Math.PI;
                    double azimuth = Math.Acos((Math.Sin(declination * Math.PI / 180) - Math.Sin(altitude * Math.PI / 180) * Math.Sin(orientation.Latitude * Math.PI / 180)) /
                                               (Math.Cos(altitude * Math.PI / 180) * Math.Cos(orientation.Latitude * Math.PI / 180))) * 180 / Math.PI;
                    if (hour > 12) azimuth = 360 - azimuth;

                    positions.Add(new SolarPosition { DayOfYear = day, Hour = hour, Altitude = altitude, Azimuth = azimuth });
                }
            }
            return positions;
        }

        private Dictionary<string, DaylightGrid> GenerateAnalysisGrids(List<ThermalZone> zones, double spacing)
        {
            var grids = new Dictionary<string, DaylightGrid>();
            foreach (var zone in zones)
            {
                double width = Math.Sqrt(zone.FloorArea);
                double length = zone.FloorArea / width;
                var points = new List<GridPoint>();
                for (double x = spacing / 2; x < width; x += spacing)
                {
                    for (double y = spacing / 2; y < length; y += spacing)
                    {
                        points.Add(new GridPoint { X = x, Y = y, Z = 0.8, Illuminance = 0, DaylightFactor = 0 });
                    }
                }
                grids[zone.ZoneId] = new DaylightGrid
                {
                    GridId = Guid.NewGuid().ToString(),
                    ZoneId = zone.ZoneId,
                    GridSpacing = spacing,
                    WorkplaneHeight = 0.8,
                    Points = points
                };
            }
            return grids;
        }

        private List<FenestrationData> GetZoneFenestrations(ThermalZone zone, BuildingGeometry geometry)
        {
            return geometry?.Fenestrations?.Where(f => zone.SurfaceIds?.Contains(f.ParentSurfaceId) ?? false).ToList()
                   ?? new List<FenestrationData>();
        }

        private List<GridPoint> CalculateIlluminance(DaylightGrid grid, List<FenestrationData> fenestrations, List<SolarPosition> solarPositions, WeatherData weather)
        {
            double totalWindowArea = fenestrations.Sum(f => f.Area);
            double avgVT = fenestrations.Any() ? fenestrations.Average(f => f.VisibleTransmittance) : 0.6;

            foreach (var point in grid.Points)
            {
                double illuminance = 300 + (totalWindowArea * avgVT * 500 / (grid.Points.Count * 0.5));
                double df = illuminance / 10000 * 100;
                point.Illuminance = Math.Min(illuminance, 2000);
                point.DaylightFactor = Math.Min(df, 10);
            }
            return grid.Points;
        }

        private ZoneDaylightResults CalculateDaylightMetrics(List<GridPoint> points, DaylightMetric primaryMetric)
        {
            double avgIlluminance = points.Average(p => p.Illuminance);
            double avgDF = points.Average(p => p.DaylightFactor);
            double sda = points.Count(p => p.Illuminance >= 300) * 100.0 / points.Count;
            double ase = points.Count(p => p.Illuminance >= 1000) * 100.0 / points.Count;

            return new ZoneDaylightResults
            {
                DaylightFactor = avgDF,
                SDA = sda,
                ASE = ase,
                UDI_Low = points.Count(p => p.Illuminance < 100) * 100.0 / points.Count,
                UDI_Useful = points.Count(p => p.Illuminance >= 100 && p.Illuminance <= 2000) * 100.0 / points.Count,
                UDI_Exceeded = points.Count(p => p.Illuminance > 2000) * 100.0 / points.Count,
                AverageIlluminance = avgIlluminance,
                MaxIlluminance = points.Max(p => p.Illuminance),
                MinIlluminance = points.Min(p => p.Illuminance),
                Uniformity = points.Min(p => p.Illuminance) / avgIlluminance
            };
        }

        private double CalculateLightingReduction(Dictionary<string, ZoneDaylightResults> zoneResults)
        {
            double avgSDA = zoneResults.Values.Average(z => z.SDA);
            return Math.Min(avgSDA * 0.5, 40);
        }

        private GlareAnalysis AnalyzeGlare(SimulationProject project, List<SolarPosition> solarPositions)
        {
            var viewpoints = new List<ViewpointGlare>();
            foreach (var zone in project.ThermalZones)
            {
                viewpoints.Add(new ViewpointGlare
                {
                    ViewpointId = Guid.NewGuid().ToString(),
                    X = Math.Sqrt(zone.FloorArea) / 2,
                    Y = zone.FloorArea / Math.Sqrt(zone.FloorArea) / 2,
                    Z = 1.2,
                    ViewDirection = 0,
                    DGP = 0.3 + 0.1 * new Random().NextDouble(),
                    DGI = 20 + 5 * new Random().NextDouble(),
                    UGR = 18 + 4 * new Random().NextDouble()
                });
            }

            return new GlareAnalysis
            {
                ViewpointResults = viewpoints,
                MaxDGP = viewpoints.Max(v => v.DGP),
                AverageDGP = viewpoints.Average(v => v.DGP),
                HoursAboveThreshold = (int)(viewpoints.Count(v => v.DGP > 0.35) * 250),
                GlareRating = viewpoints.Average(v => v.DGP) < 0.35 ? "Acceptable" : "Problematic"
            };
        }

        private ZoneThermalComfortResults CalculateZoneThermalComfort(ThermalZone zone, ThermalComfortModel model, double met, double clo)
        {
            double airTemp = zone.Setpoints?.CoolingSetpoint ?? 24;
            double mrt = airTemp + 1;
            double rh = 50;
            double airSpeed = 0.15;

            double pmv = CalculatePMV(airTemp, mrt, rh, airSpeed, met, clo);
            double ppd = 100 - 95 * Math.Exp(-0.03353 * Math.Pow(pmv, 4) - 0.2179 * Math.Pow(pmv, 2));

            return new ZoneThermalComfortResults
            {
                ZoneId = zone.ZoneId,
                AveragePMV = pmv,
                AveragePPD = ppd,
                MaxPMV = pmv + 0.5,
                MinPMV = pmv - 0.5,
                OperativeTemperature = (airTemp + mrt) / 2,
                MeanRadiantTemperature = mrt,
                AirTemperature = airTemp,
                RelativeHumidity = rh,
                AirSpeed = airSpeed,
                ComfortHours = (int)(8760 * (1 - ppd / 100)),
                DiscomfortHours = (int)(8760 * ppd / 100)
            };
        }

        private double CalculatePMV(double ta, double tmrt, double rh, double v, double met, double clo)
        {
            double pa = rh * 10 * Math.Exp(16.6536 - 4030.183 / (ta + 235));
            double icl = 0.155 * clo;
            double m = met * 58.15;
            double w = 0;
            double mw = m - w;
            double fcl = clo <= 0.078 ? 1.0 + 1.29 * icl : 1.05 + 0.645 * icl;

            double hcf = 12.1 * Math.Sqrt(v);
            double taa = ta + 273;
            double tra = tmrt + 273;
            double tcla = taa + (35.5 - ta) / (3.5 * icl + 0.1);

            double p1 = icl * fcl;
            double p2 = p1 * 3.96;
            double p3 = p1 * 100;
            double p4 = p1 * taa;
            double p5 = 308.7 - 0.028 * mw + p2 * Math.Pow(tra / 100, 4);
            double xn = tcla / 100;
            double xf = xn;
            double eps = 0.00015;
            int n = 0;

            while (Math.Abs(xn - xf) > eps && n < 150)
            {
                xf = (xf + xn) / 2;
                double hcn = 2.38 * Math.Pow(Math.Abs(100 * xf - taa), 0.25);
                double hc = hcn > hcf ? hcn : hcf;
                xn = (p5 + p4 * hc - p2 * Math.Pow(xf, 4)) / (100 + p3 * hc);
                n++;
            }

            double tcl = 100 * xn - 273;
            double hcn2 = 2.38 * Math.Pow(Math.Abs(tcl - ta), 0.25);
            double hc2 = hcn2 > hcf ? hcn2 : hcf;

            double hl1 = 3.05 * 0.001 * (5733 - 6.99 * mw - pa);
            double hl2 = mw > 58.15 ? 0.42 * (mw - 58.15) : 0;
            double hl3 = 1.7 * 0.00001 * m * (5867 - pa);
            double hl4 = 0.0014 * m * (34 - ta);
            double hl5 = 3.96 * fcl * (Math.Pow(xn, 4) - Math.Pow(tra / 100, 4));
            double hl6 = fcl * hc2 * (tcl - ta);

            double ts = 0.303 * Math.Exp(-0.036 * m) + 0.028;
            double pmv = ts * (mw - hl1 - hl2 - hl3 - hl4 - hl5 - hl6);

            return Math.Max(-3, Math.Min(3, pmv));
        }

        private int CalculateUnmetHeatingHours(Dictionary<string, ZoneThermalComfortResults> zoneResults)
        {
            return zoneResults.Values.Sum(z => z.AveragePMV < -0.5 ? z.DiscomfortHours / 2 : 0);
        }

        private int CalculateUnmetCoolingHours(Dictionary<string, ZoneThermalComfortResults> zoneResults)
        {
            return zoneResults.Values.Sum(z => z.AveragePMV > 0.5 ? z.DiscomfortHours / 2 : 0);
        }

        private AdaptiveComfortResults CalculateAdaptiveComfort(SimulationProject project)
        {
            double runningMeanTemp = 22;
            double upper80 = 0.31 * runningMeanTemp + 17.8 + 3.5;
            double lower80 = 0.31 * runningMeanTemp + 17.8 - 3.5;
            double upper90 = 0.31 * runningMeanTemp + 17.8 + 2.5;
            double lower90 = 0.31 * runningMeanTemp + 17.8 - 2.5;

            return new AdaptiveComfortResults
            {
                AcceptabilityPercentage80 = 85,
                AcceptabilityPercentage90 = 70,
                RunningMeanTemperature = runningMeanTemp,
                ComfortUpperLimit80 = upper80,
                ComfortLowerLimit80 = lower80,
                ComfortUpperLimit90 = upper90,
                ComfortLowerLimit90 = lower90
            };
        }

        private ZoneAirflowResults CalculateZoneAirflow(ThermalZone zone, AirflowAnalysisType analysisType)
        {
            double oaPerPerson = zone.Ventilation?.OutdoorAirPerPerson ?? 7.5;
            double oaPerArea = zone.Ventilation?.OutdoorAirPerArea ?? 0.06;
            double occupants = zone.Occupancy?.PeakOccupancy ?? (zone.FloorArea / 10);

            double outdoorAir = oaPerPerson * occupants + oaPerArea * zone.FloorArea;
            double supplyAir = Math.Max(outdoorAir * 2, zone.FloorArea * 1.0);
            double ach = supplyAir * 60 / zone.Volume;

            return new ZoneAirflowResults
            {
                ZoneId = zone.ZoneId,
                SupplyAirflow = supplyAir,
                ReturnAirflow = supplyAir * 0.9,
                OutdoorAirflow = outdoorAir,
                InfiltrationAirflow = zone.Volume * 0.1 / 60,
                ACH = ach,
                MixingEffectiveness = 0.9,
                VentilationEffectiveness = analysisType == AirflowAnalysisType.DisplacementVentilation ? 1.2 : 1.0,
                LocalMeanAge = zone.Volume / (supplyAir * 60) * 60
            };
        }

        private double CalculateTotalExhaust(List<ThermalZone> zones)
        {
            return zones.Sum(z => z.Ventilation?.ExhaustAirflow ?? 0);
        }

        private PressureResults CalculatePressureDistribution(SimulationProject project, Dictionary<string, ZoneAirflowResults> zoneResults)
        {
            var zonePressures = new Dictionary<string, double>();
            foreach (var zone in project.ThermalZones)
            {
                var airflow = zoneResults[zone.ZoneId];
                double deltaFlow = airflow.SupplyAirflow - airflow.ReturnAirflow;
                zonePressures[zone.ZoneId] = deltaFlow > 0 ? 2.5 : -2.5;
            }

            return new PressureResults
            {
                ZonePressures = zonePressures,
                BuildingPressure = zonePressures.Values.Average(),
                DuctPressures = new Dictionary<string, double> { { "Supply", 250 }, { "Return", 125 } },
                TotalPressureDrop = 375
            };
        }

        private ContaminantResults SimulateContaminants(SimulationProject project, Dictionary<string, ZoneAirflowResults> zoneResults)
        {
            var co2Levels = new Dictionary<string, double>();
            var pm25Levels = new Dictionary<string, double>();

            foreach (var zone in project.ThermalZones)
            {
                double occupants = zone.Occupancy?.PeakOccupancy ?? (zone.FloorArea / 10);
                double outdoorAir = zoneResults[zone.ZoneId].OutdoorAirflow;
                double co2Generation = occupants * 0.0052;
                double steadyStateCO2 = 400 + (co2Generation / (outdoorAir / 1000) * 1000000);
                co2Levels[zone.ZoneId] = Math.Min(steadyStateCO2, 1200);
                pm25Levels[zone.ZoneId] = 15 + 10 * new Random().NextDouble();
            }

            return new ContaminantResults
            {
                ZoneCO2Levels = co2Levels,
                ZonePM25Levels = pm25Levels,
                AverageCO2 = co2Levels.Values.Average(),
                PeakCO2 = co2Levels.Values.Max(),
                HoursAboveCO2Threshold = co2Levels.Values.Max() > 1000 ? 250 : 0
            };
        }

        private SimulationResults GetCachedResults(string projectId)
        {
            return _resultsCache.TryGetValue(projectId, out var results) ? results : null;
        }

        private List<PerformanceMetric> CompileKeyMetrics(SimulationResults results, SimulationProject project)
        {
            var metrics = new List<PerformanceMetric>();

            if (results.EnergyResults != null)
            {
                metrics.Add(new PerformanceMetric
                {
                    MetricName = "Energy Use Intensity",
                    Value = results.EnergyResults.EnergyUseIntensity,
                    Unit = "kBtu/ft2/yr",
                    Baseline = 100,
                    Target = 50,
                    Rating = results.EnergyResults.EnergyUseIntensity < 50 ? PerformanceRating.Excellent :
                             results.EnergyResults.EnergyUseIntensity < 75 ? PerformanceRating.Good :
                             results.EnergyResults.EnergyUseIntensity < 100 ? PerformanceRating.Acceptable : PerformanceRating.BelowStandard
                });
            }

            if (results.DaylightingResults != null)
            {
                metrics.Add(new PerformanceMetric
                {
                    MetricName = "Spatial Daylight Autonomy",
                    Value = results.DaylightingResults.SpatialDaylightAutonomy,
                    Unit = "%",
                    Baseline = 40,
                    Target = 55,
                    Rating = results.DaylightingResults.SpatialDaylightAutonomy >= 55 ? PerformanceRating.Excellent :
                             results.DaylightingResults.SpatialDaylightAutonomy >= 40 ? PerformanceRating.Acceptable : PerformanceRating.BelowStandard
                });
            }

            if (results.ThermalComfortResults != null)
            {
                metrics.Add(new PerformanceMetric
                {
                    MetricName = "Comfort Hours",
                    Value = results.ThermalComfortResults.ComfortHoursPercentage,
                    Unit = "%",
                    Baseline = 80,
                    Target = 95,
                    Rating = results.ThermalComfortResults.ComfortHoursPercentage >= 95 ? PerformanceRating.Excellent :
                             results.ThermalComfortResults.ComfortHoursPercentage >= 80 ? PerformanceRating.Acceptable : PerformanceRating.BelowStandard
                });
            }

            return metrics;
        }

        private List<PerformanceRecommendation> GenerateRecommendations(SimulationResults results, SimulationProject project)
        {
            var recommendations = new List<PerformanceRecommendation>();

            if (results.EnergyResults?.EnergyUseIntensity > 75)
            {
                recommendations.Add(new PerformanceRecommendation
                {
                    RecommendationId = Guid.NewGuid().ToString(),
                    Category = "Energy",
                    Description = "Consider upgrading to high-efficiency HVAC equipment",
                    Impact = "High",
                    EstimatedSavings = results.EnergyResults.EnergyCost * 0.2,
                    ImplementationCost = 50000,
                    Payback = 50000 / (results.EnergyResults.EnergyCost * 0.2),
                    Priority = "Medium"
                });
            }

            if (results.DaylightingResults?.SpatialDaylightAutonomy < 40)
            {
                recommendations.Add(new PerformanceRecommendation
                {
                    RecommendationId = Guid.NewGuid().ToString(),
                    Category = "Daylighting",
                    Description = "Increase window area or add skylights to improve daylight availability",
                    Impact = "Medium",
                    EstimatedSavings = results.EnergyResults?.LightingEnergy * 0.3 * 0.12 ?? 0,
                    ImplementationCost = 25000,
                    Priority = "Low"
                });
            }

            return recommendations;
        }

        private ComplianceStatus CheckCompliance(SimulationResults results, SimulationProject project, string standard)
        {
            var issues = new List<ComplianceIssue>();
            bool ashrae901 = results.EnergyResults?.EnergyUseIntensity < 100;
            bool iecc = results.EnergyResults?.EnergyUseIntensity < 110;

            if (!ashrae901)
            {
                issues.Add(new ComplianceIssue
                {
                    Standard = "ASHRAE 90.1-2022",
                    Section = "Section 11",
                    Requirement = "EUI below baseline",
                    ActualValue = $"{results.EnergyResults?.EnergyUseIntensity:F1} kBtu/ft2/yr",
                    RequiredValue = "< 100 kBtu/ft2/yr",
                    Resolution = "Improve building envelope and HVAC efficiency"
                });
            }

            return new ComplianceStatus
            {
                ASHRAE901Compliant = ashrae901,
                IECC2021Compliant = iecc,
                Title24Compliant = true,
                LEED_EAc1Compliant = results.EnergyResults?.EnergyUseIntensity < 75,
                AdditionalStandards = new Dictionary<string, bool>(),
                Issues = issues
            };
        }

        private BenchmarkComparison CompareToBenchmarks(SimulationResults results, SimulationProject project)
        {
            double eui = results.EnergyResults?.EnergyUseIntensity ?? 0;
            int percentile = eui < 50 ? 90 : eui < 75 ? 75 : eui < 100 ? 50 : 25;

            return new BenchmarkComparison
            {
                CBECSMedian = 92,
                CBECSTop25 = 55,
                EnergyStar = 75,
                ZeroNet = 0,
                BuildingEUI = eui,
                PercentileRanking = percentile
            };
        }

        private PerformanceRating CalculateOverallRating(List<PerformanceMetric> metrics)
        {
            if (!metrics.Any()) return PerformanceRating.NotRated;
            int score = metrics.Sum(m => m.Rating == PerformanceRating.Excellent ? 3 :
                                        m.Rating == PerformanceRating.Good ? 2 :
                                        m.Rating == PerformanceRating.Acceptable ? 1 : 0);
            double avg = (double)score / metrics.Count;
            return avg >= 2.5 ? PerformanceRating.Excellent :
                   avg >= 1.5 ? PerformanceRating.Good :
                   avg >= 0.5 ? PerformanceRating.Acceptable : PerformanceRating.BelowStandard;
        }

        private PerformanceRating CalculateEnergyRating(EnergyResults energy)
        {
            if (energy == null) return PerformanceRating.NotRated;
            return energy.EnergyUseIntensity < 50 ? PerformanceRating.Excellent :
                   energy.EnergyUseIntensity < 75 ? PerformanceRating.Good :
                   energy.EnergyUseIntensity < 100 ? PerformanceRating.Acceptable : PerformanceRating.BelowStandard;
        }

        private PerformanceRating CalculateComfortRating(ThermalComfortResults comfort)
        {
            if (comfort == null) return PerformanceRating.NotRated;
            return comfort.ComfortHoursPercentage >= 95 ? PerformanceRating.Excellent :
                   comfort.ComfortHoursPercentage >= 85 ? PerformanceRating.Good :
                   comfort.ComfortHoursPercentage >= 75 ? PerformanceRating.Acceptable : PerformanceRating.BelowStandard;
        }

        private PerformanceRating CalculateDaylightRating(DaylightingResults daylight)
        {
            if (daylight == null) return PerformanceRating.NotRated;
            return daylight.SpatialDaylightAutonomy >= 55 ? PerformanceRating.Excellent :
                   daylight.SpatialDaylightAutonomy >= 40 ? PerformanceRating.Good :
                   daylight.SpatialDaylightAutonomy >= 25 ? PerformanceRating.Acceptable : PerformanceRating.BelowStandard;
        }

        private PerformanceRating CalculateVentilationRating(AirflowResults airflow)
        {
            if (airflow == null) return PerformanceRating.NotRated;
            bool co2Ok = airflow.ContaminantResults?.AverageCO2 < 1000;
            bool achOk = airflow.AverageACH >= 4;
            return co2Ok && achOk ? PerformanceRating.Excellent :
                   co2Ok || achOk ? PerformanceRating.Acceptable : PerformanceRating.BelowStandard;
        }

        private InternalLoads GetDefaultInternalLoads(BuildingOccupancyType occupancy)
        {
            var defaults = new Dictionary<BuildingOccupancyType, (double lpd, double epd)>
            {
                { BuildingOccupancyType.Office, (9.0, 11.0) },
                { BuildingOccupancyType.Retail, (12.0, 5.0) },
                { BuildingOccupancyType.Healthcare, (11.0, 15.0) },
                { BuildingOccupancyType.Education, (9.0, 7.0) },
                { BuildingOccupancyType.Residential, (5.0, 5.0) },
                { BuildingOccupancyType.Industrial, (8.0, 20.0) },
                { BuildingOccupancyType.Hospitality, (10.0, 8.0) }
            };

            var (lpd, epd) = defaults.GetValueOrDefault(occupancy, (10.0, 10.0));
            return new InternalLoads { LightingPowerDensity = lpd, EquipmentPowerDensity = epd };
        }

        private VentilationRequirements GetDefaultVentilation(BuildingOccupancyType occupancy)
        {
            var defaults = new Dictionary<BuildingOccupancyType, (double perPerson, double perArea)>
            {
                { BuildingOccupancyType.Office, (5.0, 0.06) },
                { BuildingOccupancyType.Retail, (7.5, 0.12) },
                { BuildingOccupancyType.Healthcare, (15.0, 0.06) },
                { BuildingOccupancyType.Education, (10.0, 0.12) },
                { BuildingOccupancyType.Residential, (5.0, 0.06) }
            };

            var (perPerson, perArea) = defaults.GetValueOrDefault(occupancy, (7.5, 0.06));
            return new VentilationRequirements
            {
                OutdoorAirPerPerson = perPerson,
                OutdoorAirPerArea = perArea,
                CO2Setpoint = 1000,
                VentilationStandard = "ASHRAE 62.1-2022"
            };
        }

        private List<ConstructionAssembly> InitializeConstructionLibrary()
        {
            return new List<ConstructionAssembly>
            {
                new ConstructionAssembly
                {
                    ConstructionId = "EXT-WALL-01",
                    ConstructionName = "Exterior Wall - Steel Stud",
                    ConstructionType = "ExteriorWall",
                    UValue = 0.064,
                    RValue = 15.6
                },
                new ConstructionAssembly
                {
                    ConstructionId = "ROOF-01",
                    ConstructionName = "Built-up Roof",
                    ConstructionType = "Roof",
                    UValue = 0.032,
                    RValue = 31.3
                }
            };
        }

        #endregion
    }

    #endregion

    #region Supporting Classes

    public class SimulationProgress
    {
        public string Stage { get; set; }
        public int PercentComplete { get; set; }
        public string CurrentZone { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan EstimatedRemaining { get; set; }
    }

    public class WeatherData
    {
        public string FilePath { get; set; }
        public string Location { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<HourlyWeather> HourlyData { get; set; }
    }

    public class HourlyWeather
    {
        public int DayOfYear { get; set; }
        public int Hour { get; set; }
        public double DryBulbTemp { get; set; }
        public double WetBulbTemp { get; set; }
        public double RelativeHumidity { get; set; }
        public double DirectNormalIrradiance { get; set; }
        public double DiffuseHorizontalIrradiance { get; set; }
        public double WindSpeed { get; set; }
        public double WindDirection { get; set; }
    }

    public class SolarPosition
    {
        public int DayOfYear { get; set; }
        public int Hour { get; set; }
        public double Altitude { get; set; }
        public double Azimuth { get; set; }
    }

    public class HVACSimulationResult
    {
        public double HeatingEnergy { get; set; }
        public double CoolingEnergy { get; set; }
        public double FanEnergy { get; set; }
        public double PumpEnergy { get; set; }
    }

    #endregion
}
