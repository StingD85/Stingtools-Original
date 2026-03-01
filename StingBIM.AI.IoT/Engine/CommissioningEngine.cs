// StingBIM.AI.IoT.Engine.CommissioningEngine
// Building commissioning (Cx) workflow engine implementing ASHRAE Guideline 0
// and ISO 19650-3 handover documentation. Manages commissioning plans, checklists,
// functional testing, punch list tracking, TAB verification, and seasonal checks.

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
    /// Manages the complete building commissioning lifecycle from pre-design through
    /// ongoing operations. Creates commissioning plans per ASHRAE Guideline 0,
    /// tracks functional performance testing, generates ISO 19650-3 compliant handover
    /// packages, verifies TAB reports, and manages seasonal commissioning checks.
    /// </summary>
    public class CommissioningEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        // Commissioning plans by building ID
        private readonly Dictionary<string, CommissioningPlan> _plans =
            new Dictionary<string, CommissioningPlan>(StringComparer.OrdinalIgnoreCase);

        // Test result history
        private readonly ConcurrentDictionary<string, List<FunctionalTestResult>> _testHistory =
            new ConcurrentDictionary<string, List<FunctionalTestResult>>(StringComparer.OrdinalIgnoreCase);

        // Sensor engine reference for live verification
        private readonly SensorIntegrationEngine _sensorEngine;

        // System type templates for checklist generation
        private readonly Dictionary<string, List<string>> _preFunctionalTemplates =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _functionalTemplates =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _seasonalTemplates =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes the CommissioningEngine with checklist templates and sensor integration.
        /// </summary>
        /// <param name="sensorEngine">Sensor engine for live data verification.</param>
        public CommissioningEngine(SensorIntegrationEngine sensorEngine)
        {
            _sensorEngine = sensorEngine ?? throw new ArgumentNullException(nameof(sensorEngine));
            InitializeChecklistTemplates();
            Logger.Info("CommissioningEngine initialized with {PreFunc} pre-functional, " +
                        "{Func} functional, {Seasonal} seasonal checklist templates",
                _preFunctionalTemplates.Count, _functionalTemplates.Count, _seasonalTemplates.Count);
        }

        /// <summary>
        /// Populates checklist templates for common building system types.
        /// Based on ASHRAE Guideline 0-2019 and ASHRAE Guideline 1.1-2007.
        /// </summary>
        private void InitializeChecklistTemplates()
        {
            // Pre-functional checklists (installation verification)
            _preFunctionalTemplates["HVAC_AHU"] = new List<string>
            {
                "Verify AHU model and size match design documents",
                "Confirm ductwork connections are sealed and insulated",
                "Check filter installation and type (MERV rating)",
                "Verify belt tension and alignment on fan motors",
                "Confirm VFD installation and programming",
                "Check condensate drain pan and piping slope",
                "Verify outside air damper operation",
                "Confirm temperature sensors installed per drawings",
                "Check vibration isolation mounts",
                "Verify electrical connections and overcurrent protection"
            };

            _preFunctionalTemplates["HVAC_Chiller"] = new List<string>
            {
                "Verify chiller model and capacity match design",
                "Confirm refrigerant charge and type",
                "Check condenser water piping connections",
                "Verify chilled water piping connections and flow direction",
                "Confirm safety controls and pressure relief",
                "Check oil level and heater operation",
                "Verify VFD on compressor (if applicable)",
                "Confirm flow switches installed",
                "Check vibration isolation",
                "Verify control wiring to BMS"
            };

            _preFunctionalTemplates["Electrical_Switchgear"] = new List<string>
            {
                "Verify switchgear rating matches design load",
                "Confirm bus bar torque per manufacturer specs",
                "Check grounding and bonding connections",
                "Verify protective relay settings",
                "Confirm metering CT/PT ratios",
                "Check arc flash labels installed",
                "Verify working clearances per NEC 110.26",
                "Confirm emergency generator transfer interlock",
                "Check surge protection devices",
                "Verify BMS communication wiring"
            };

            _preFunctionalTemplates["Plumbing_DomesticWater"] = new List<string>
            {
                "Verify pipe material and size per design",
                "Confirm pressure test completed and documented",
                "Check backflow preventers installed and tested",
                "Verify water heater capacity and setpoint",
                "Confirm TMV (thermostatic mixing valve) installation",
                "Check insulation on hot water piping",
                "Verify PRV (pressure reducing valve) settings",
                "Confirm isolation valves accessible",
                "Check water meter installation",
                "Verify Legionella prevention measures"
            };

            _preFunctionalTemplates["FireProtection"] = new List<string>
            {
                "Verify sprinkler head type and spacing per NFPA 13",
                "Confirm fire pump capacity and controller",
                "Check standpipe connections and hose valves",
                "Verify fire alarm initiating devices installed",
                "Confirm notification appliance coverage",
                "Check fire alarm control panel programming",
                "Verify smoke damper installation and wiring",
                "Confirm stairwell pressurization fans",
                "Check fire department connections",
                "Verify fire-rated penetration sealing"
            };

            // Functional test checklists (operational verification)
            _functionalTemplates["HVAC_AHU"] = new List<string>
            {
                "Verify supply air temperature control within +/- 1 degC of setpoint",
                "Test outside air damper modulation (0-100%)",
                "Verify economizer changeover temperature",
                "Test static pressure control and VFD response",
                "Verify supply air temperature reset schedule",
                "Test morning warm-up / cool-down sequence",
                "Verify night setback operation",
                "Test fire/smoke shutdown sequence",
                "Verify freeze protection operation",
                "Test BMS alarm generation and acknowledgment",
                "Measure supply and return air temperatures",
                "Verify mixed air temperature calculation"
            };

            _functionalTemplates["HVAC_Chiller"] = new List<string>
            {
                "Verify chiller start/stop sequence",
                "Test chilled water supply temperature control",
                "Verify condenser water temperature control",
                "Test load staging and sequencing (multi-chiller)",
                "Verify safety shutdown on high head pressure",
                "Test low chilled water flow protection",
                "Verify BMS communication and point mapping",
                "Measure COP at partial load conditions",
                "Test failover to backup chiller",
                "Verify demand limiting response"
            };

            _functionalTemplates["Electrical_Switchgear"] = new List<string>
            {
                "Perform circuit breaker trip testing",
                "Verify generator auto-start on utility loss",
                "Test automatic transfer switch operation",
                "Verify load shedding sequence",
                "Test UPS bypass and battery runtime",
                "Verify power quality monitoring (THD, PF)",
                "Test ground fault detection system",
                "Verify demand response capability",
                "Test emergency lighting and exit signs",
                "Verify BMS power monitoring accuracy"
            };

            _functionalTemplates["Plumbing_DomesticWater"] = new List<string>
            {
                "Verify hot water delivery temperature at fixtures",
                "Test recirculation pump control and schedule",
                "Verify TMV outlet temperature (38-43 degC)",
                "Test backflow preventer function",
                "Verify water pressure at remote fixtures",
                "Test water heater recovery time",
                "Verify flow rates at representative fixtures",
                "Test emergency shower / eyewash stations",
                "Verify grease trap operation (if applicable)",
                "Test BMS water leak detection"
            };

            _functionalTemplates["FireProtection"] = new List<string>
            {
                "Test fire pump start on pressure drop",
                "Verify fire alarm detection and notification",
                "Test smoke damper closure on alarm",
                "Verify elevator recall on fire alarm",
                "Test stairwell pressurization fan operation",
                "Verify fire alarm monitoring station communication",
                "Test sprinkler flow switch activation",
                "Verify voice evacuation system operation",
                "Test fire door hold-open release",
                "Verify fire command center operation"
            };

            // Seasonal commissioning checklists
            _seasonalTemplates["Summer"] = new List<string>
            {
                "Verify cooling capacity meets peak demand",
                "Check chiller performance (COP) at design conditions",
                "Verify economizer lockout above setpoint",
                "Test demand limiting during peak hours",
                "Check cooling tower fan staging and water treatment",
                "Verify condenser water temperature control",
                "Measure zone temperatures during peak cooling",
                "Check humidity control in critical zones",
                "Verify solar shading device operation",
                "Review energy consumption vs cooling degree days"
            };

            _seasonalTemplates["Winter"] = new List<string>
            {
                "Verify heating capacity meets design conditions",
                "Test boiler sequencing and efficiency",
                "Check freeze protection systems",
                "Verify morning warm-up scheduling",
                "Test snow/ice melting systems (if applicable)",
                "Check heating coil valve operation",
                "Verify humidity control (minimum RH)",
                "Test building envelope integrity (drafts)",
                "Review energy consumption vs heating degree days",
                "Check glycol concentration in exposed piping"
            };

            _seasonalTemplates["Transition"] = new List<string>
            {
                "Verify economizer operation and switchover",
                "Test simultaneous heating/cooling prevention",
                "Check changeover valve operation",
                "Verify optimal start/stop scheduling",
                "Test demand-controlled ventilation response",
                "Check zone temperature stability during swing conditions",
                "Verify BMS trend data collection active",
                "Review occupancy schedule adjustments",
                "Test after-hours HVAC request system",
                "Verify energy baseline comparison"
            };
        }

        #region Commissioning Plan Creation

        /// <summary>
        /// Creates a comprehensive commissioning plan for a building per ASHRAE Guideline 0.
        /// Generates pre-functional and functional checklists for all specified systems.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <param name="buildingName">Human-readable building name.</param>
        /// <param name="systemTypes">List of system types to commission (e.g., "HVAC_AHU", "Electrical_Switchgear").</param>
        /// <returns>The created commissioning plan with all checklists populated.</returns>
        public CommissioningPlan CreateCxPlan(string buildingId, string buildingName = "",
            List<string> systemTypes = null)
        {
            if (string.IsNullOrWhiteSpace(buildingId))
                throw new ArgumentException("Building ID cannot be empty.", nameof(buildingId));

            var plan = new CommissioningPlan
            {
                BuildingId = buildingId,
                BuildingName = buildingName,
                CurrentPhase = CommissioningPhase.PreFunctional
            };

            // Default to all system types if none specified
            var systems = systemTypes ?? _preFunctionalTemplates.Keys.ToList();

            foreach (string systemType in systems)
            {
                // Pre-functional checklist
                if (_preFunctionalTemplates.TryGetValue(systemType, out var preFuncItems))
                {
                    var preChecklist = new CxChecklist
                    {
                        SystemId = systemType,
                        SystemName = FormatSystemName(systemType),
                        Phase = CommissioningPhase.PreFunctional,
                        Items = preFuncItems.Select(item => new CxChecklistItem
                        {
                            Description = item,
                            Criteria = "Pass/Fail per specification"
                        }).ToList()
                    };
                    plan.Checklists.Add(preChecklist);
                }

                // Functional test checklist
                if (_functionalTemplates.TryGetValue(systemType, out var funcItems))
                {
                    var funcChecklist = new CxChecklist
                    {
                        SystemId = systemType,
                        SystemName = FormatSystemName(systemType),
                        Phase = CommissioningPhase.Functional,
                        Items = funcItems.Select(item => new CxChecklistItem
                        {
                            Description = item,
                            Criteria = "Per design intent and sequences of operation"
                        }).ToList()
                    };
                    plan.Checklists.Add(funcChecklist);
                }
            }

            lock (_lockObject)
            {
                _plans[buildingId] = plan;
            }

            Logger.Info("Created Cx plan for {BuildingId} ({BuildingName}): {ChecklistCount} checklists, " +
                        "{ItemCount} total items across {SystemCount} systems",
                buildingId, buildingName, plan.Checklists.Count,
                plan.Checklists.Sum(c => c.Items.Count), systems.Count);

            return plan;
        }

        /// <summary>
        /// Converts system type codes to human-readable names.
        /// </summary>
        private string FormatSystemName(string systemType)
        {
            return systemType.Replace("_", " - ") switch
            {
                var s when s.Contains("HVAC") => s.Replace("HVAC - ", "HVAC: "),
                var s when s.Contains("Electrical") => s.Replace("Electrical - ", "Electrical: "),
                var s when s.Contains("Plumbing") => s.Replace("Plumbing - ", "Plumbing: "),
                var s => s
            };
        }

        #endregion

        #region Functional Testing

        /// <summary>
        /// Records the result of a functional performance test for a specific system.
        /// Updates the corresponding checklist item and generates alerts for failures.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <param name="systemId">System type being tested.</param>
        /// <param name="testResult">The functional test result to record.</param>
        /// <returns>True if the test result was recorded successfully.</returns>
        public bool TrackFunctionalTest(string buildingId, string systemId, FunctionalTestResult testResult)
        {
            if (testResult == null) throw new ArgumentNullException(nameof(testResult));

            CommissioningPlan plan;
            lock (_lockObject)
            {
                if (!_plans.TryGetValue(buildingId, out plan))
                {
                    Logger.Warn("No Cx plan found for building {Id}. Cannot track test.", buildingId);
                    return false;
                }
            }

            // Find the matching checklist and item
            var checklist = plan.Checklists.FirstOrDefault(c =>
                c.SystemId.Equals(systemId, StringComparison.OrdinalIgnoreCase) &&
                c.Phase == CommissioningPhase.Functional);

            if (checklist == null)
            {
                Logger.Warn("No functional checklist found for system {SystemId} in building {Id}.",
                    systemId, buildingId);
                return false;
            }

            // Find or create the matching item
            var item = checklist.Items.FirstOrDefault(i =>
                i.Description.Equals(testResult.TestDescription, StringComparison.OrdinalIgnoreCase));

            if (item == null)
            {
                // Add as a new test item
                item = new CxChecklistItem
                {
                    Description = testResult.TestDescription,
                    Criteria = testResult.AcceptanceCriteria
                };
                checklist.Items.Add(item);
            }

            // Update the item with test results
            item.Status = testResult.Passed ? TestStatus.Passed : TestStatus.Failed;
            item.ActualResult = testResult.ActualResult;
            item.TestedBy = testResult.TestedBy;
            item.TestedDate = testResult.TestDate;
            item.Comments = testResult.Comments;

            // Store in test history
            var history = _testHistory.GetOrAdd(systemId, _ => new List<FunctionalTestResult>());
            lock (history) { history.Add(testResult); }

            // Generate punch list item for failures
            if (!testResult.Passed)
            {
                plan.PunchList.Add(new PunchListItem
                {
                    Description = $"FAILED: {testResult.TestDescription} - {testResult.ActualResult}",
                    Location = testResult.Location,
                    SystemId = systemId,
                    Priority = AlertSeverity.Critical,
                    AssignedTo = testResult.ResponsibleParty,
                    ElementId = testResult.ElementId
                });

                Logger.Warn("Functional test FAILED for {SystemId} in {BuildingId}: {Test} - Actual: {Result}",
                    systemId, buildingId, testResult.TestDescription, testResult.ActualResult);
            }
            else
            {
                Logger.Info("Functional test PASSED for {SystemId} in {BuildingId}: {Test}",
                    systemId, buildingId, testResult.TestDescription);
            }

            return true;
        }

        #endregion

        #region Design Intent Verification

        /// <summary>
        /// Verifies that installed system performance matches design intent by comparing
        /// live sensor data against design parameters (setpoints, capacities, efficiencies).
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <param name="systemId">System to verify.</param>
        /// <param name="designParameters">Design intent parameters to verify against.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Verification result with pass/fail per parameter.</returns>
        public async Task<DesignVerificationResult> ValidateDesignIntent(
            string buildingId, string systemId,
            Dictionary<string, DesignParameter> designParameters,
            CancellationToken cancellationToken = default)
        {
            var result = new DesignVerificationResult
            {
                BuildingId = buildingId,
                SystemId = systemId,
                VerificationDate = DateTime.UtcNow
            };

            if (designParameters == null || designParameters.Count == 0)
            {
                result.Status = "NoDesignParameters";
                return result;
            }

            await Task.Run(() =>
            {
                foreach (var param in designParameters)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string paramName = param.Key;
                    var designParam = param.Value;
                    var verification = new ParameterVerification
                    {
                        ParameterName = paramName,
                        DesignValue = designParam.DesignValue,
                        Tolerance = designParam.Tolerance,
                        Unit = designParam.Unit
                    };

                    // Try to get actual value from sensor data
                    if (!string.IsNullOrWhiteSpace(designParam.SensorId))
                    {
                        var reading = _sensorEngine.GetLatestReading(designParam.SensorId);
                        if (reading != null)
                        {
                            verification.ActualValue = reading.Value;
                            verification.DataQuality = reading.Quality;

                            double deviation = Math.Abs(reading.Value - designParam.DesignValue);
                            verification.Deviation = deviation;
                            verification.Passed = deviation <= designParam.Tolerance;

                            if (!verification.Passed)
                            {
                                verification.Comments = $"Actual {reading.Value:F2} {designParam.Unit} " +
                                                        $"deviates {deviation:F2} from design {designParam.DesignValue:F2} " +
                                                        $"(tolerance: +/-{designParam.Tolerance:F2})";
                            }
                        }
                        else
                        {
                            verification.Passed = false;
                            verification.Comments = $"No sensor reading available for {designParam.SensorId}";
                        }
                    }
                    else
                    {
                        verification.Passed = false;
                        verification.Comments = "No sensor bound to this design parameter";
                    }

                    result.Verifications.Add(verification);
                }

                result.PassCount = result.Verifications.Count(v => v.Passed);
                result.FailCount = result.Verifications.Count(v => !v.Passed);
                result.OverallPassed = result.FailCount == 0;
                result.Status = "Complete";

            }, cancellationToken).ConfigureAwait(false);

            Logger.Info("Design intent verification for {SystemId} in {BuildingId}: " +
                        "{Pass} passed, {Fail} failed, overall={Overall}",
                systemId, buildingId, result.PassCount, result.FailCount,
                result.OverallPassed ? "PASS" : "FAIL");

            return result;
        }

        #endregion

        #region Punch List Management

        /// <summary>
        /// Retrieves the current punch list for a building, optionally filtered by system or status.
        /// </summary>
        public IReadOnlyList<PunchListItem> TrackPunchList(
            string buildingId,
            string systemId = null,
            bool unresolvedOnly = true)
        {
            CommissioningPlan plan;
            lock (_lockObject)
            {
                if (!_plans.TryGetValue(buildingId, out plan))
                    return Array.Empty<PunchListItem>();
            }

            IEnumerable<PunchListItem> items = plan.PunchList;

            if (!string.IsNullOrWhiteSpace(systemId))
                items = items.Where(i => i.SystemId.Equals(systemId, StringComparison.OrdinalIgnoreCase));

            if (unresolvedOnly)
                items = items.Where(i => !i.IsResolved);

            return items.OrderByDescending(i => i.Priority)
                        .ThenBy(i => i.CreatedDate)
                        .ToList()
                        .AsReadOnly();
        }

        /// <summary>
        /// Adds a deficiency to the punch list.
        /// </summary>
        public string AddPunchListItem(string buildingId, PunchListItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            CommissioningPlan plan;
            lock (_lockObject)
            {
                if (!_plans.TryGetValue(buildingId, out plan))
                {
                    Logger.Warn("No Cx plan for building {Id}. Cannot add punch list item.", buildingId);
                    return null;
                }
            }

            plan.PunchList.Add(item);
            Logger.Info("Added punch list item {ItemId} for {BuildingId}: {Description}",
                item.ItemId, buildingId, item.Description);

            return item.ItemId;
        }

        /// <summary>
        /// Resolves a punch list item.
        /// </summary>
        public bool ResolvePunchListItem(string buildingId, string itemId)
        {
            CommissioningPlan plan;
            lock (_lockObject)
            {
                if (!_plans.TryGetValue(buildingId, out plan))
                    return false;
            }

            var item = plan.PunchList.FirstOrDefault(i =>
                i.ItemId.Equals(itemId, StringComparison.OrdinalIgnoreCase));

            if (item == null) return false;

            item.IsResolved = true;
            item.ResolvedDate = DateTime.UtcNow;

            Logger.Info("Resolved punch list item {ItemId} for {BuildingId}", itemId, buildingId);
            return true;
        }

        #endregion

        #region TAB Verification

        /// <summary>
        /// Verifies a Testing, Adjusting, and Balancing (TAB) report by comparing
        /// reported airflow/waterflow values against design values and live sensor data.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <param name="systemId">System being verified.</param>
        /// <param name="tabData">TAB report data (terminal/valve ID -> measured flow).</param>
        /// <param name="designFlows">Design flow values (terminal/valve ID -> design flow).</param>
        /// <returns>TAB verification result with per-terminal pass/fail.</returns>
        public TabVerificationResult VerifyBalancingReport(
            string buildingId, string systemId,
            Dictionary<string, double> tabData,
            Dictionary<string, double> designFlows)
        {
            var result = new TabVerificationResult
            {
                BuildingId = buildingId,
                SystemId = systemId,
                VerificationDate = DateTime.UtcNow
            };

            if (tabData == null || designFlows == null)
            {
                result.Status = "MissingData";
                return result;
            }

            // ASHRAE Standard 111: TAB tolerance is typically +/-10% of design
            const double tabTolerance = 0.10;

            foreach (var terminal in tabData)
            {
                string terminalId = terminal.Key;
                double measuredFlow = terminal.Value;

                var check = new TabTerminalCheck
                {
                    TerminalId = terminalId,
                    MeasuredFlow = measuredFlow
                };

                if (designFlows.TryGetValue(terminalId, out double designFlow))
                {
                    check.DesignFlow = designFlow;
                    check.DeviationPercent = designFlow > 0
                        ? Math.Round((measuredFlow - designFlow) / designFlow * 100, 1) : 0;
                    check.Passed = Math.Abs(check.DeviationPercent) <= tabTolerance * 100;

                    if (!check.Passed)
                    {
                        check.Comments = $"Flow deviation of {check.DeviationPercent:F1}% " +
                                         $"exceeds +/-{tabTolerance * 100:F0}% tolerance. " +
                                         "Rebalance required.";
                    }
                }
                else
                {
                    check.Passed = false;
                    check.Comments = "No design flow value available for comparison.";
                }

                result.TerminalChecks.Add(check);
            }

            result.TotalTerminals = result.TerminalChecks.Count;
            result.PassedTerminals = result.TerminalChecks.Count(t => t.Passed);
            result.FailedTerminals = result.TerminalChecks.Count(t => !t.Passed);
            result.OverallPassed = result.FailedTerminals == 0;

            // Calculate system-level total flow balance
            double totalMeasured = tabData.Values.Sum();
            double totalDesign = 0;
            foreach (var design in designFlows)
            {
                if (tabData.ContainsKey(design.Key))
                    totalDesign += design.Value;
            }

            result.TotalMeasuredFlow = Math.Round(totalMeasured, 1);
            result.TotalDesignFlow = Math.Round(totalDesign, 1);
            result.SystemBalancePercent = totalDesign > 0
                ? Math.Round((totalMeasured - totalDesign) / totalDesign * 100, 1) : 0;

            result.Status = "Complete";

            Logger.Info("TAB verification for {SystemId} in {BuildingId}: " +
                        "{Pass}/{Total} terminals passed, system balance={Balance:F1}%",
                systemId, buildingId, result.PassedTerminals,
                result.TotalTerminals, result.SystemBalancePercent);

            return result;
        }

        #endregion

        #region Seasonal Commissioning

        /// <summary>
        /// Generates a seasonal commissioning checklist based on the current month.
        /// Spring/Fall are transition seasons, summer requires cooling verification,
        /// winter requires heating verification.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <param name="month">Month number (1-12) for season determination.</param>
        /// <returns>Seasonal commissioning checklist.</returns>
        public CxChecklist SeasonalCommissioningCheck(string buildingId, int month)
        {
            string season = month switch
            {
                >= 6 and <= 8 => "Summer",
                >= 12 or <= 2 => "Winter",
                _ => "Transition"
            };

            if (!_seasonalTemplates.TryGetValue(season, out var templateItems))
            {
                Logger.Warn("No seasonal template for {Season}.", season);
                return new CxChecklist { SystemId = $"Seasonal_{season}", Phase = CommissioningPhase.Seasonal };
            }

            var checklist = new CxChecklist
            {
                SystemId = $"Seasonal_{season}_{month:D2}",
                SystemName = $"Seasonal Commissioning - {season} (Month {month})",
                Phase = CommissioningPhase.Seasonal,
                Items = templateItems.Select(item => new CxChecklistItem
                {
                    Description = item,
                    Criteria = "Per seasonal commissioning requirements"
                }).ToList()
            };

            // Add to the building plan if it exists
            CommissioningPlan plan;
            lock (_lockObject)
            {
                if (_plans.TryGetValue(buildingId, out plan))
                {
                    plan.Checklists.Add(checklist);
                }
            }

            Logger.Info("Generated {Season} seasonal Cx checklist for {BuildingId}: {ItemCount} items",
                season, buildingId, checklist.Items.Count);

            return checklist;
        }

        #endregion

        #region Handover Package

        /// <summary>
        /// Generates an ISO 19650-3 compliant handover documentation package.
        /// Includes all commissioning results, O&M documentation references,
        /// as-built verification, and certificate generation.
        /// </summary>
        /// <param name="buildingId">Building identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Handover package with all required documentation sections.</returns>
        public async Task<HandoverPackage> GenerateHandoverPackage(
            string buildingId,
            CancellationToken cancellationToken = default)
        {
            var package = new HandoverPackage
            {
                BuildingId = buildingId,
                GeneratedDate = DateTime.UtcNow,
                Standard = "ISO 19650-3:2020"
            };

            CommissioningPlan plan;
            lock (_lockObject)
            {
                if (!_plans.TryGetValue(buildingId, out plan))
                {
                    package.Status = "NoPlanFound";
                    return package;
                }
            }

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                package.BuildingName = plan.BuildingName;

                // Section 1: Commissioning summary
                package.CxCompletionPercentage = plan.CompletionPercentage;
                package.TotalChecklistItems = plan.Checklists.Sum(c => c.Items.Count);
                package.PassedItems = plan.Checklists.Sum(c =>
                    c.Items.Count(i => i.Status == TestStatus.Passed));
                package.FailedItems = plan.Checklists.Sum(c =>
                    c.Items.Count(i => i.Status == TestStatus.Failed));
                package.DeferredItems = plan.Checklists.Sum(c =>
                    c.Items.Count(i => i.Status == TestStatus.Deferred));

                // Section 2: System-by-system results
                var systemGroups = plan.Checklists.GroupBy(c => c.SystemId);
                foreach (var group in systemGroups)
                {
                    int totalItems = group.Sum(c => c.Items.Count);
                    int passedItems = group.Sum(c => c.Items.Count(i => i.Status == TestStatus.Passed));
                    package.SystemResults[group.Key] = new SystemCxSummary
                    {
                        SystemId = group.Key,
                        TotalTests = totalItems,
                        Passed = passedItems,
                        Failed = group.Sum(c => c.Items.Count(i => i.Status == TestStatus.Failed)),
                        CompletionPercent = totalItems > 0 ? Math.Round((double)passedItems / totalItems * 100, 1) : 0
                    };
                }

                // Section 3: Outstanding deficiencies
                package.OutstandingDeficiencies = plan.PunchList
                    .Where(p => !p.IsResolved)
                    .OrderByDescending(p => p.Priority)
                    .ToList();

                // Section 4: Certificate eligibility
                bool allCriticalPassed = plan.Checklists
                    .Where(c => c.Phase == CommissioningPhase.Functional)
                    .All(c => c.Items.All(i => i.Status == TestStatus.Passed ||
                                               i.Status == TestStatus.NotApplicable ||
                                               i.Status == TestStatus.Deferred));

                bool noCriticalPunchList = !plan.PunchList.Any(p =>
                    !p.IsResolved && p.Priority == AlertSeverity.Emergency);

                package.CertificateEligible = allCriticalPassed && noCriticalPunchList;
                package.CertificateNotes = package.CertificateEligible
                    ? "Building meets commissioning requirements for certificate of completion."
                    : "Outstanding items must be resolved before certificate issuance.";

                // Section 5: O&M documentation checklist
                package.OmDocumentation = new List<string>
                {
                    "Equipment submittals and shop drawings",
                    "Manufacturer O&M manuals",
                    "As-built drawings (AutoCAD/Revit)",
                    "Control system sequences of operation",
                    "BMS point list and graphics",
                    "TAB reports (all systems)",
                    "Fire protection system test reports",
                    "Electrical testing reports (Megger, thermography)",
                    "Warranty information and contacts",
                    "Preventive maintenance schedules",
                    "Emergency procedures and contacts",
                    "Training records for facility staff",
                    "Spare parts inventory",
                    "Building energy model (if applicable)"
                };

                package.Status = "Complete";

            }, cancellationToken).ConfigureAwait(false);

            Logger.Info("Generated ISO 19650-3 handover package for {BuildingId}: " +
                        "{Completion:F1}% complete, certificate eligible={Eligible}",
                buildingId, package.CxCompletionPercentage, package.CertificateEligible);

            return package;
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Returns commissioning progress statistics for a building.
        /// </summary>
        public Dictionary<string, object> GetStatistics(string buildingId)
        {
            var stats = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            CommissioningPlan plan;
            lock (_lockObject)
            {
                if (!_plans.TryGetValue(buildingId, out plan))
                    return stats;
            }

            stats["CompletionPercentage"] = Math.Round(plan.CompletionPercentage, 1);
            stats["TotalChecklists"] = plan.Checklists.Count;
            stats["TotalItems"] = plan.Checklists.Sum(c => c.Items.Count);
            stats["PassedItems"] = plan.Checklists.Sum(c => c.Items.Count(i => i.Status == TestStatus.Passed));
            stats["FailedItems"] = plan.Checklists.Sum(c => c.Items.Count(i => i.Status == TestStatus.Failed));
            stats["PendingItems"] = plan.Checklists.Sum(c => c.Items.Count(i => i.Status == TestStatus.NotStarted));
            stats["PunchListOpen"] = plan.PunchList.Count(p => !p.IsResolved);
            stats["PunchListResolved"] = plan.PunchList.Count(p => p.IsResolved);
            stats["CurrentPhase"] = plan.CurrentPhase.ToString();

            return stats;
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Functional test result data for a specific test point.
    /// </summary>
    public class FunctionalTestResult
    {
        public string TestDescription { get; set; } = string.Empty;
        public string AcceptanceCriteria { get; set; } = string.Empty;
        public string ActualResult { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public string TestedBy { get; set; } = string.Empty;
        public DateTime TestDate { get; set; } = DateTime.UtcNow;
        public string Location { get; set; } = string.Empty;
        public string ElementId { get; set; } = string.Empty;
        public string ResponsibleParty { get; set; } = string.Empty;
        public string Comments { get; set; } = string.Empty;
    }

    /// <summary>
    /// Design parameter for intent verification.
    /// </summary>
    public class DesignParameter
    {
        public double DesignValue { get; set; }
        public double Tolerance { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string SensorId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of design intent verification for a system.
    /// </summary>
    public class DesignVerificationResult
    {
        public string BuildingId { get; set; } = string.Empty;
        public string SystemId { get; set; } = string.Empty;
        public DateTime VerificationDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool OverallPassed { get; set; }
        public int PassCount { get; set; }
        public int FailCount { get; set; }
        public List<ParameterVerification> Verifications { get; set; } = new List<ParameterVerification>();
    }

    /// <summary>
    /// Individual parameter verification result.
    /// </summary>
    public class ParameterVerification
    {
        public string ParameterName { get; set; } = string.Empty;
        public double DesignValue { get; set; }
        public double? ActualValue { get; set; }
        public double Tolerance { get; set; }
        public double Deviation { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public SensorDataQuality DataQuality { get; set; }
        public string Comments { get; set; } = string.Empty;
    }

    /// <summary>
    /// TAB verification result.
    /// </summary>
    public class TabVerificationResult
    {
        public string BuildingId { get; set; } = string.Empty;
        public string SystemId { get; set; } = string.Empty;
        public DateTime VerificationDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool OverallPassed { get; set; }
        public int TotalTerminals { get; set; }
        public int PassedTerminals { get; set; }
        public int FailedTerminals { get; set; }
        public double TotalMeasuredFlow { get; set; }
        public double TotalDesignFlow { get; set; }
        public double SystemBalancePercent { get; set; }
        public List<TabTerminalCheck> TerminalChecks { get; set; } = new List<TabTerminalCheck>();
    }

    /// <summary>
    /// Individual TAB terminal check result.
    /// </summary>
    public class TabTerminalCheck
    {
        public string TerminalId { get; set; } = string.Empty;
        public double MeasuredFlow { get; set; }
        public double DesignFlow { get; set; }
        public double DeviationPercent { get; set; }
        public bool Passed { get; set; }
        public string Comments { get; set; } = string.Empty;
    }

    /// <summary>
    /// ISO 19650-3 handover documentation package.
    /// </summary>
    public class HandoverPackage
    {
        public string BuildingId { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public DateTime GeneratedDate { get; set; }
        public string Standard { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double CxCompletionPercentage { get; set; }
        public int TotalChecklistItems { get; set; }
        public int PassedItems { get; set; }
        public int FailedItems { get; set; }
        public int DeferredItems { get; set; }
        public bool CertificateEligible { get; set; }
        public string CertificateNotes { get; set; } = string.Empty;
        public Dictionary<string, SystemCxSummary> SystemResults { get; set; } =
            new Dictionary<string, SystemCxSummary>(StringComparer.OrdinalIgnoreCase);
        public List<PunchListItem> OutstandingDeficiencies { get; set; } = new List<PunchListItem>();
        public List<string> OmDocumentation { get; set; } = new List<string>();
    }

    /// <summary>
    /// Summary of commissioning results for a single system.
    /// </summary>
    public class SystemCxSummary
    {
        public string SystemId { get; set; } = string.Empty;
        public int TotalTests { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public double CompletionPercent { get; set; }
    }

    #endregion
}
