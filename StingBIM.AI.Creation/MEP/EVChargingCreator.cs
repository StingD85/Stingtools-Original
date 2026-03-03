// StingBIM.AI.Creation.MEP.EVChargingCreator
// Handles: EV charger stations, distribution boards, cable routes, load management
// v4 Prompt Reference: Section A.8 Phase 8 — Specialist Systems
// Standards: IEC 61851, BS 7671 (Section 722), Uganda ERA connection regulations

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;
using StingBIM.AI.Creation.Pipeline;

namespace StingBIM.AI.Creation.MEP
{
    /// <summary>
    /// Creates EV charging infrastructure: charger stations, dedicated circuits,
    /// distribution boards, cable routes, and smart load management.
    ///
    /// Uganda/East Africa Context:
    ///   - EV adoption growing (government incentives in Kenya/Rwanda)
    ///   - Grid capacity constraints — load management essential
    ///   - Solar PV integration for off-grid/hybrid charging
    ///   - Typical domestic supply: single-phase 60A (14 kW)
    ///   - Commercial: three-phase 100A–400A
    ///   - Future-proofing: install conduit even if not wiring immediately
    /// </summary>
    public class EVChargingCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly FamilyResolver _familyResolver;

        private const double MM_TO_FEET = 1.0 / 304.8;

        // Charger types and specifications
        private static readonly Dictionary<string, EVChargerSpec> ChargerTypes =
            new Dictionary<string, EVChargerSpec>(StringComparer.OrdinalIgnoreCase)
            {
                ["slow"] = new EVChargerSpec
                {
                    Name = "Mode 2 — Slow (3.7 kW)",
                    PowerKW = 3.7,
                    Voltage = 230,
                    CurrentA = 16,
                    Phase = 1,
                    Connector = "Type 2 / BS 1363",
                    ChargeTime100kWh = "27 hours",
                    CostUSD = 300,
                    CircuitBreaker = "20A Type B RCBO"
                },
                ["standard"] = new EVChargerSpec
                {
                    Name = "Mode 3 — Standard (7.4 kW)",
                    PowerKW = 7.4,
                    Voltage = 230,
                    CurrentA = 32,
                    Phase = 1,
                    Connector = "Type 2 (IEC 62196)",
                    ChargeTime100kWh = "13.5 hours",
                    CostUSD = 800,
                    CircuitBreaker = "40A Type B RCBO"
                },
                ["fast"] = new EVChargerSpec
                {
                    Name = "Mode 3 — Fast (22 kW)",
                    PowerKW = 22,
                    Voltage = 400,
                    CurrentA = 32,
                    Phase = 3,
                    Connector = "Type 2 (IEC 62196)",
                    ChargeTime100kWh = "4.5 hours",
                    CostUSD = 2500,
                    CircuitBreaker = "40A 3-phase Type B RCBO"
                },
                ["rapid"] = new EVChargerSpec
                {
                    Name = "Mode 4 — Rapid DC (50 kW)",
                    PowerKW = 50,
                    Voltage = 400,
                    CurrentA = 125,
                    Phase = 3,
                    Connector = "CCS2 / CHAdeMO",
                    ChargeTime100kWh = "2 hours",
                    CostUSD = 25000,
                    CircuitBreaker = "160A 3-phase MCCB"
                },
            };

        // Cable sizing by charger power
        private static readonly List<(double MaxKW, double CableMm2, string CableType)> CableSizing =
            new List<(double, double, string)>
            {
                (3.7, 2.5, "2.5 mm² 3-core SWA"),
                (7.4, 6.0, "6 mm² 3-core SWA"),
                (22, 6.0, "6 mm² 5-core SWA"),
                (50, 25.0, "25 mm² 5-core SWA"),
                (150, 70.0, "70 mm² 5-core SWA"),
            };

        // Parking space requirements
        private const double PARKING_SPACE_WIDTH_MM = 2500;
        private const double PARKING_SPACE_LENGTH_MM = 5000;
        private const double CHARGER_POST_OFFSET_MM = 300; // offset from wall
        private const double CHARGER_HEIGHT_MM = 1200;       // socket height AFF
        private const double CABLE_REACH_M = 5.0;            // max cable reach

        // Load management
        private const double DIVERSITY_FACTOR_2_CHARGERS = 1.0;
        private const double DIVERSITY_FACTOR_5_CHARGERS = 0.8;
        private const double DIVERSITY_FACTOR_10_CHARGERS = 0.6;
        private const double DIVERSITY_FACTOR_20_CHARGERS = 0.5;

        public EVChargingCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _familyResolver = new FamilyResolver(document);
        }

        #region EV Charger Placement

        /// <summary>
        /// Designs EV charging installation for parking areas.
        /// </summary>
        public CreationPipelineResult DesignEVCharging(EVChargingCommand cmd)
        {
            var result = new CreationPipelineResult { ElementType = "EV Charger" };

            try
            {
                Logger.Info($"Designing EV charging: type={cmd.ChargerType}, count={cmd.ChargerCount}");

                var chargerType = cmd.ChargerType ?? "standard";
                var spec = ChargerTypes.ContainsKey(chargerType)
                    ? ChargerTypes[chargerType]
                    : ChargerTypes["standard"];

                int chargerCount = cmd.ChargerCount > 0 ? cmd.ChargerCount : EstimateChargerCount();
                double diversity = GetDiversityFactor(chargerCount);
                double totalLoadKW = chargerCount * spec.PowerKW * diversity;

                // Cable selection
                var cable = SelectCable(spec.PowerKW);

                // Cost estimate
                double chargerCostUSD = chargerCount * spec.CostUSD;
                double installCostUSD = chargerCount * spec.CostUSD * 0.5;
                double cableCostUSD = chargerCount * 20 * 50; // estimate 50m avg run × $20/m
                double dbCostUSD = totalLoadKW > 22 ? 2000 : 500;
                double totalCostUSD = chargerCostUSD + installCostUSD + cableCostUSD + dbCostUSD;

                result.Success = true;
                result.Message = $"EV Charging Infrastructure Design:\n\n" +
                                 $"Chargers:\n" +
                                 $"  Type: {spec.Name}\n" +
                                 $"  Quantity: {chargerCount}\n" +
                                 $"  Power: {spec.PowerKW} kW per charger ({spec.Phase}-phase)\n" +
                                 $"  Connector: {spec.Connector}\n" +
                                 $"  Charge time (100 kWh battery): {spec.ChargeTime100kWh}\n\n" +
                                 $"Electrical:\n" +
                                 $"  Total load: {totalLoadKW:F1} kW (diversity: {diversity:F1})\n" +
                                 $"  Circuit: {spec.CircuitBreaker} per charger\n" +
                                 $"  Cable: {cable.CableType}\n" +
                                 $"  Dedicated EV distribution board\n" +
                                 $"  Earth rod: TT earthing system\n" +
                                 $"  RCD: Type B (DC fault protection)\n\n" +
                                 $"Physical:\n" +
                                 $"  Mount: wall-mounted or pedestal\n" +
                                 $"  Socket height: {CHARGER_HEIGHT_MM} mm AFF\n" +
                                 $"  Cable reach: {CABLE_REACH_M} m\n" +
                                 $"  Signage: EV charging bay markings\n" +
                                 $"  Lighting: minimum 50 lux at charger\n\n" +
                                 $"Smart Features:\n" +
                                 $"  Load management: {(chargerCount > 2 ? "dynamic load balancing" : "standalone")}\n" +
                                 $"  Metering: per-charger energy metering\n" +
                                 $"  Communication: OCPP 1.6J (network-managed)\n" +
                                 $"  Payment: RFID card / app-based\n\n" +
                                 $"Cost Estimate:\n" +
                                 $"  Chargers: ${chargerCostUSD:N0}\n" +
                                 $"  Installation: ${installCostUSD:N0}\n" +
                                 $"  Cabling: ${cableCostUSD:N0}\n" +
                                 $"  Distribution: ${dbCostUSD:N0}\n" +
                                 $"  TOTAL: ${totalCostUSD:N0} (UGX {totalCostUSD * 3750:N0})";

                result.Suggestions = new List<string>();

                if (cmd.IncludeSolar)
                {
                    double solarKWp = totalLoadKW * 0.5; // cover 50% from solar
                    result.Suggestions.Add($"Add {solarKWp:F0} kWp solar canopy for green charging");
                }

                result.Suggestions.Add("Install conduit to remaining parking bays for future expansion");
                result.Suggestions.Add("Consider off-peak charging schedules (10pm–6am) for lower tariff");

                if (chargerCount >= 5)
                {
                    result.Suggestions.Add("Deploy OCPP-based load management to avoid grid upgrade");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to design EV charging");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Designs future-proofing infrastructure (conduit only, no chargers).
        /// </summary>
        public CreationPipelineResult DesignEVReadiness(int parkingSpaces)
        {
            var result = new CreationPipelineResult { ElementType = "EV Ready Infrastructure" };

            try
            {
                // UK/international guidance: 20% active, 80% passive (conduit only)
                int activeSpaces = Math.Max(1, (int)Math.Ceiling(parkingSpaces * 0.2));
                int passiveSpaces = parkingSpaces - activeSpaces;

                result.Success = true;
                result.Message = $"EV-Ready parking design ({parkingSpaces} spaces):\n\n" +
                                 $"  Active (charger installed): {activeSpaces} spaces (20%)\n" +
                                 $"  Passive (conduit + cable only): {passiveSpaces} spaces (80%)\n\n" +
                                 $"Passive provision per bay:\n" +
                                 $"  32 mm conduit from DB to bay\n" +
                                 $"  Pull cord installed in conduit\n" +
                                 $"  Space reserved on DB for future MCB\n" +
                                 $"  Floor/wall box for future charger mount\n\n" +
                                 $"DB provision:\n" +
                                 $"  Dedicated EV sub-main from main switchboard\n" +
                                 $"  Spare ways: {passiveSpaces}\n" +
                                 $"  CT metering for load management";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to design EV readiness");
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        #endregion

        #region Helpers

        private int EstimateChargerCount()
        {
            // Count parking spaces from model (if available)
            var parkingRooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .Where(r =>
                {
                    var name = (r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "").ToLowerInvariant();
                    return name.Contains("park") || name.Contains("garage") || name.Contains("carport");
                })
                .ToList();

            if (parkingRooms.Count > 0)
            {
                double totalParkingAreaM2 = parkingRooms.Sum(r => r.Area * 0.0929);
                int estimatedSpaces = (int)(totalParkingAreaM2 / 15); // ~15 m² per space
                return Math.Max(1, (int)Math.Ceiling(estimatedSpaces * 0.2)); // 20% with chargers
            }

            return 2; // default
        }

        private double GetDiversityFactor(int count)
        {
            if (count <= 2) return DIVERSITY_FACTOR_2_CHARGERS;
            if (count <= 5) return DIVERSITY_FACTOR_5_CHARGERS;
            if (count <= 10) return DIVERSITY_FACTOR_10_CHARGERS;
            return DIVERSITY_FACTOR_20_CHARGERS;
        }

        private (double CableMm2, string CableType) SelectCable(double powerKW)
        {
            foreach (var entry in CableSizing)
            {
                if (powerKW <= entry.MaxKW)
                    return (entry.CableMm2, entry.CableType);
            }
            return CableSizing[CableSizing.Count - 1].Item2 == null
                ? (70.0, "70 mm² 5-core SWA")
                : (CableSizing[CableSizing.Count - 1].CableMm2, CableSizing[CableSizing.Count - 1].CableType);
        }

        #endregion
    }

    #region Data Types

    public class EVChargerSpec
    {
        public string Name { get; set; }
        public double PowerKW { get; set; }
        public int Voltage { get; set; }
        public double CurrentA { get; set; }
        public int Phase { get; set; }
        public string Connector { get; set; }
        public string ChargeTime100kWh { get; set; }
        public double CostUSD { get; set; }
        public string CircuitBreaker { get; set; }
    }

    #endregion

    #region Command Classes

    public class EVChargingCommand
    {
        public string ChargerType { get; set; } = "standard";
        public int ChargerCount { get; set; }
        public bool IncludeSolar { get; set; }
        public string LevelName { get; set; }
        public string LocationDescription { get; set; }
    }

    #endregion
}
