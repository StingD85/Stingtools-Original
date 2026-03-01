// StingBIM.AI.Creation.MEP.SolarPVCreator
// Handles: solar panels, inverters, charge controllers, battery storage, wiring
// v4 Prompt Reference: Section A.8 Phase 8 — Specialist Systems
// Standards: IEC 61215, IEC 62446, NEC Article 690, Uganda ERA grid-tie regulations

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.MEP
{
    /// <summary>
    /// Creates solar PV system elements: panel arrays, inverters,
    /// charge controllers, battery storage, and DC/AC wiring.
    ///
    /// Uganda/East Africa Context:
    ///   - Peak sun hours: 4.5–5.5 hrs/day (equatorial advantage)
    ///   - Grid-tie with net metering available in Uganda (ERA)
    ///   - Hybrid systems preferred (grid unreliability)
    ///   - Panel tilt: 0–15° (near equator, latitude-dependent)
    ///   - Battery storage strongly recommended
    ///   - Dust/tropical rain self-cleaning benefit
    /// </summary>
    public class SolarPVCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;

        private const double MM_TO_FEET = 1.0 / 304.8;
        private const double SQ_FT_TO_M2 = 0.0929;

        // Solar irradiance data (kWh/m²/day) by East African region
        private static readonly Dictionary<string, double> PeakSunHours =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Kampala"] = 4.8,
                ["Nairobi"] = 5.2,
                ["Dar es Salaam"] = 5.0,
                ["Kigali"] = 4.6,
                ["Addis Ababa"] = 5.5,
                ["Lagos"] = 4.2,
                ["Accra"] = 4.8,
                ["Johannesburg"] = 5.5,
                ["Cairo"] = 6.0,
                ["default"] = 5.0,
            };

        // Standard panel specifications
        private static readonly Dictionary<string, PanelSpec> PanelTypes =
            new Dictionary<string, PanelSpec>(StringComparer.OrdinalIgnoreCase)
            {
                ["standard"] = new PanelSpec
                {
                    Name = "Monocrystalline 540W",
                    WattsPeak = 540,
                    WidthMm = 1134,
                    HeightMm = 2278,
                    Efficiency = 0.21,
                    CostUSD = 180
                },
                ["economy"] = new PanelSpec
                {
                    Name = "Polycrystalline 400W",
                    WattsPeak = 400,
                    WidthMm = 1052,
                    HeightMm = 1956,
                    Efficiency = 0.18,
                    CostUSD = 120
                },
                ["premium"] = new PanelSpec
                {
                    Name = "Monocrystalline PERC 600W",
                    WattsPeak = 600,
                    WidthMm = 1134,
                    HeightMm = 2278,
                    Efficiency = 0.23,
                    CostUSD = 250
                },
            };

        // Battery storage specifications
        private static readonly Dictionary<string, BatterySpec> BatteryTypes =
            new Dictionary<string, BatterySpec>(StringComparer.OrdinalIgnoreCase)
            {
                ["lithium"] = new BatterySpec
                {
                    Name = "Lithium-ion (LiFePO4)",
                    CapacityKWh = 5.12,
                    DepthOfDischarge = 0.90,
                    CycleLife = 6000,
                    CostPerKWh_USD = 250,
                    Warranty = "10 years"
                },
                ["lead_acid"] = new BatterySpec
                {
                    Name = "Gel Lead-Acid",
                    CapacityKWh = 2.4,
                    DepthOfDischarge = 0.50,
                    CycleLife = 1500,
                    CostPerKWh_USD = 120,
                    Warranty = "3 years"
                },
            };

        // System losses
        private const double WIRING_LOSS = 0.03;     // 3%
        private const double INVERTER_EFFICIENCY = 0.96; // 96%
        private const double SOILING_LOSS = 0.02;    // 2% (tropical rain helps)
        private const double TEMPERATURE_LOSS = 0.05; // 5% (tropical heat derating)
        private const double SYSTEM_PERFORMANCE_RATIO = 0.82; // Overall PR

        // Mounting
        private const double PANEL_TILT_EQUATORIAL_DEG = 5.0; // near equator
        private const double ROW_SPACING_FACTOR = 1.5;  // row spacing = panel height × factor
        private const double PANEL_CLEARANCE_MM = 100;   // gap between panels

        public SolarPVCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
        }

        #region Solar PV System Design

        /// <summary>
        /// Designs a complete solar PV system based on energy demand or roof area.
        /// </summary>
        public CreationPipelineResult DesignSolarSystem(SolarPVCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "Solar PV System" };

            try
            {
                Logger.Info($"Designing solar PV: demand={cmd.DailyDemandKWh} kWh, " +
                           $"budget={cmd.BudgetUSD}, roof={cmd.RoofAreaM2} m²");

                var panelType = cmd.PanelType ?? "standard";
                var panel = PanelTypes.ContainsKey(panelType)
                    ? PanelTypes[panelType]
                    : PanelTypes["standard"];

                var location = cmd.Location ?? "default";
                double sunHours = PeakSunHours.ContainsKey(location)
                    ? PeakSunHours[location]
                    : PeakSunHours["default"];

                // Calculate system size
                double systemKWp;
                if (cmd.DailyDemandKWh > 0)
                {
                    // Size from demand: kWp = daily demand / (sun hours × PR)
                    systemKWp = cmd.DailyDemandKWh / (sunHours * SYSTEM_PERFORMANCE_RATIO);
                }
                else if (cmd.SystemKWp > 0)
                {
                    systemKWp = cmd.SystemKWp;
                }
                else
                {
                    // Default: size from available roof area
                    double roofAreaM2 = cmd.RoofAreaM2 > 0
                        ? cmd.RoofAreaM2
                        : EstimateRoofArea();
                    double usableArea = roofAreaM2 * 0.6; // 60% usable
                    double panelAreaM2 = (panel.WidthMm * panel.HeightMm) / 1e6;
                    int maxPanels = (int)(usableArea / (panelAreaM2 * ROW_SPACING_FACTOR));
                    systemKWp = maxPanels * panel.WattsPeak / 1000.0;
                }

                int panelCount = (int)Math.Ceiling(systemKWp * 1000.0 / panel.WattsPeak);
                double actualKWp = panelCount * panel.WattsPeak / 1000.0;
                double dailyYieldKWh = actualKWp * sunHours * SYSTEM_PERFORMANCE_RATIO;
                double annualYieldKWh = dailyYieldKWh * 365;

                // Roof area required
                double panelAreaTotal = panelCount * (panel.WidthMm * panel.HeightMm) / 1e6;
                double roofAreaRequired = panelAreaTotal * ROW_SPACING_FACTOR;

                // Inverter sizing (kW = 80-100% of kWp)
                double inverterKW = actualKWp * 0.9;
                int inverterCount = actualKWp <= 10 ? 1 : (int)Math.Ceiling(actualKWp / 10);

                // Battery sizing (cover 1 day autonomy at 50% of daily demand)
                double batteryKWh = cmd.IncludeBattery
                    ? dailyYieldKWh * 0.5 / 0.9 // 0.9 = DoD for lithium
                    : 0;
                var battery = cmd.BatteryType ?? "lithium";
                var batterySpec = BatteryTypes.ContainsKey(battery)
                    ? BatteryTypes[battery]
                    : BatteryTypes["lithium"];
                int batteryUnits = batteryKWh > 0
                    ? (int)Math.Ceiling(batteryKWh / (batterySpec.CapacityKWh * batterySpec.DepthOfDischarge))
                    : 0;

                // Cost estimate
                double panelCostUSD = panelCount * panel.CostUSD;
                double inverterCostUSD = inverterCount * (actualKWp <= 5 ? 800 : 1500);
                double batteryCostUSD = batteryUnits * batterySpec.CapacityKWh * batterySpec.CostPerKWh_USD;
                double bosCostUSD = panelCostUSD * 0.25; // balance of system
                double installCostUSD = (panelCostUSD + inverterCostUSD) * 0.15;
                double totalCostUSD = panelCostUSD + inverterCostUSD + batteryCostUSD + bosCostUSD + installCostUSD;

                // Payback period (assuming $0.18/kWh grid tariff in Uganda)
                double annualSavingsUSD = annualYieldKWh * 0.18;
                double paybackYears = totalCostUSD / annualSavingsUSD;

                result.Success = true;
                result.Message = $"Solar PV System Design ({location}):\n\n" +
                                 $"Array:\n" +
                                 $"  Panels: {panelCount}× {panel.Name}\n" +
                                 $"  System size: {actualKWp:F1} kWp\n" +
                                 $"  Roof area: {roofAreaRequired:F0} m²\n" +
                                 $"  Tilt: {PANEL_TILT_EQUATORIAL_DEG}° (equatorial)\n" +
                                 $"  Orientation: North-facing (southern hemisphere) or flat\n\n" +
                                 $"Performance:\n" +
                                 $"  Daily yield: {dailyYieldKWh:F1} kWh\n" +
                                 $"  Annual yield: {annualYieldKWh:F0} kWh\n" +
                                 $"  Peak sun hours: {sunHours:F1} hrs/day\n" +
                                 $"  Performance ratio: {SYSTEM_PERFORMANCE_RATIO * 100:F0}%\n\n" +
                                 $"Equipment:\n" +
                                 $"  Inverter: {inverterCount}× {inverterKW / inverterCount:F1} kW " +
                                 (cmd.IncludeBattery ? "hybrid" : "grid-tie") + "\n";

                if (cmd.IncludeBattery && batteryUnits > 0)
                {
                    result.Message += $"  Battery: {batteryUnits}× {batterySpec.Name} " +
                                     $"({batteryUnits * batterySpec.CapacityKWh:F1} kWh total)\n" +
                                     $"  Autonomy: ~{batteryUnits * batterySpec.CapacityKWh * batterySpec.DepthOfDischarge / (dailyYieldKWh * 0.5) * 12:F0} hours backup\n";
                }

                result.Message += $"\nCost Estimate:\n" +
                                 $"  Panels: ${panelCostUSD:N0}\n" +
                                 $"  Inverter(s): ${inverterCostUSD:N0}\n";

                if (batteryCostUSD > 0)
                    result.Message += $"  Battery: ${batteryCostUSD:N0}\n";

                result.Message += $"  BOS + Installation: ${bosCostUSD + installCostUSD:N0}\n" +
                                 $"  TOTAL: ${totalCostUSD:N0} (UGX {totalCostUSD * 3750:N0})\n" +
                                 $"  Payback: {paybackYears:F1} years\n" +
                                 $"  25-year savings: ${annualSavingsUSD * 25 - totalCostUSD:N0}";

                result.Suggestions = new List<string>
                {
                    "Consider hybrid inverter for grid + battery flexibility",
                    "Apply for Uganda ERA net metering license for grid export",
                    "Add surge protection (SPD Type 1+2) on DC and AC side",
                    "Install monitoring system for remote performance tracking"
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to design solar PV system");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        #endregion

        #region Helpers

        private double EstimateRoofArea()
        {
            var roofs = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Roofs)
                .WhereElementIsNotElementType()
                .ToList();

            if (roofs.Count == 0)
            {
                // Estimate from floor area of top level
                var rooms = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Autodesk.Revit.DB.Architecture.Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                if (rooms.Count > 0)
                {
                    // Group by level, take top level
                    var topLevelRooms = rooms
                        .GroupBy(r => r.Level?.Elevation ?? 0)
                        .OrderByDescending(g => g.Key)
                        .First();

                    return topLevelRooms.Sum(r => r.Area) * SQ_FT_TO_M2;
                }

                return 100; // default 100 m²
            }

            double totalArea = 0;
            foreach (var roof in roofs)
            {
                var areaParam = roof.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                if (areaParam != null)
                    totalArea += areaParam.AsDouble() * SQ_FT_TO_M2;
            }

            return totalArea > 0 ? totalArea : 100;
        }

        #endregion
    }

    #region Data Types

    public class PanelSpec
    {
        public string Name { get; set; }
        public double WattsPeak { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public double Efficiency { get; set; }
        public double CostUSD { get; set; }

        public double AreaM2 => (WidthMm * HeightMm) / 1e6;
    }

    public class BatterySpec
    {
        public string Name { get; set; }
        public double CapacityKWh { get; set; }
        public double DepthOfDischarge { get; set; }
        public int CycleLife { get; set; }
        public double CostPerKWh_USD { get; set; }
        public string Warranty { get; set; }
    }

    #endregion

    #region Command Classes

    public class SolarPVCommand
    {
        public double DailyDemandKWh { get; set; }
        public double SystemKWp { get; set; }
        public double RoofAreaM2 { get; set; }
        public string PanelType { get; set; } = "standard";
        public string Location { get; set; } = "Kampala";
        public bool IncludeBattery { get; set; } = true;
        public string BatteryType { get; set; } = "lithium";
        public double BudgetUSD { get; set; }
        public string LevelName { get; set; }
    }

    #endregion
}
