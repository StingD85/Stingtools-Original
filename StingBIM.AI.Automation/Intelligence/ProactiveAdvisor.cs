// StingBIM.AI.Automation.Intelligence.ProactiveAdvisor
// Proactive design advisor: budget alerts, compliance warnings, design suggestions
// v4 Prompt Reference: Section A.8 Phase 8 — Proactive Intelligence

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;

namespace StingBIM.AI.Automation.Intelligence
{
    /// <summary>
    /// Proactive intelligence engine that monitors the design model and
    /// provides unsolicited but valuable feedback:
    ///   - Budget threshold alerts (80% / 100% / overrun)
    ///   - Compliance warnings when elements violate codes
    ///   - Design pattern suggestions based on room context
    ///   - Material substitution recommendations
    ///   - Energy efficiency tips
    ///
    /// Uganda/East Africa Context:
    ///   - Budget alerts in UGX with USD equivalent
    ///   - UNBS compliance warnings
    ///   - Passive cooling suggestions for tropical climate
    ///   - Water storage recommendations for unreliable supply
    ///   - Generator sizing alerts for power reliability
    /// </summary>
    public class ProactiveAdvisor
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Document _document;
        private readonly object _lock = new object();

        private double _projectBudgetUGX;
        private double _cumulativeCostUGX;
        private readonly List<ProactiveAlert> _pendingAlerts = new List<ProactiveAlert>();
        private readonly Dictionary<string, DateTime> _suppressedAlerts = new Dictionary<string, DateTime>();
        private readonly TimeSpan _alertCooldown = TimeSpan.FromMinutes(30);

        // Budget thresholds
        private const double BUDGET_WARNING_THRESHOLD = 0.80;   // 80%
        private const double BUDGET_CRITICAL_THRESHOLD = 1.00;  // 100%
        private const double BUDGET_OVERRUN_THRESHOLD = 1.10;   // 110%

        // Compliance rule categories
        private static readonly Dictionary<string, List<ComplianceRule>> ComplianceRules =
            new Dictionary<string, List<ComplianceRule>>
            {
                ["FIRE_SAFETY"] = new List<ComplianceRule>
                {
                    new ComplianceRule
                    {
                        Code = "FS-001",
                        Description = "Travel distance to fire exit exceeds 18m (single direction)",
                        Category = "Fire Safety",
                        Standard = "UNBS US 319 / BS 9999",
                        Severity = AlertSeverity.Critical
                    },
                    new ComplianceRule
                    {
                        Code = "FS-002",
                        Description = "Fire door width less than 900mm",
                        Category = "Fire Safety",
                        Standard = "UNBS US 319",
                        Severity = AlertSeverity.Critical
                    },
                    new ComplianceRule
                    {
                        Code = "FS-003",
                        Description = "No fire detection in habitable room",
                        Category = "Fire Safety",
                        Standard = "BS 5839-6",
                        Severity = AlertSeverity.Warning
                    },
                },
                ["ACCESSIBILITY"] = new List<ComplianceRule>
                {
                    new ComplianceRule
                    {
                        Code = "AC-001",
                        Description = "Door width less than 900mm on accessible route",
                        Category = "Accessibility",
                        Standard = "Uganda PWD Act 2020 / BS 8300",
                        Severity = AlertSeverity.Warning
                    },
                    new ComplianceRule
                    {
                        Code = "AC-002",
                        Description = "No accessible toilet on ground floor",
                        Category = "Accessibility",
                        Standard = "Uganda Building Control Regs",
                        Severity = AlertSeverity.Warning
                    },
                    new ComplianceRule
                    {
                        Code = "AC-003",
                        Description = "Ramp gradient exceeds 1:12",
                        Category = "Accessibility",
                        Standard = "BS 8300-2:2018",
                        Severity = AlertSeverity.Warning
                    },
                    new ComplianceRule
                    {
                        Code = "AC-004",
                        Description = "Level change >300mm without ramp or lift",
                        Category = "Accessibility",
                        Standard = "Uganda PWD Act 2020",
                        Severity = AlertSeverity.Warning
                    },
                },
                ["STRUCTURAL"] = new List<ComplianceRule>
                {
                    new ComplianceRule
                    {
                        Code = "ST-001",
                        Description = "Unsupported wall span exceeds 6m without lintel/beam",
                        Category = "Structural",
                        Standard = "BS EN 1996 / UNBS",
                        Severity = AlertSeverity.Critical
                    },
                    new ComplianceRule
                    {
                        Code = "ST-002",
                        Description = "Floor-to-floor height exceeds 4m for masonry wall without piers",
                        Category = "Structural",
                        Standard = "BS EN 1996",
                        Severity = AlertSeverity.Warning
                    },
                },
                ["VENTILATION"] = new List<ComplianceRule>
                {
                    new ComplianceRule
                    {
                        Code = "VN-001",
                        Description = "Room has no opening window (natural ventilation required)",
                        Category = "Ventilation",
                        Standard = "Uganda Building Regs / CIBSE Guide A",
                        Severity = AlertSeverity.Warning
                    },
                    new ComplianceRule
                    {
                        Code = "VN-002",
                        Description = "Kitchen extract ventilation not specified",
                        Category = "Ventilation",
                        Standard = "BS EN 16798-1",
                        Severity = AlertSeverity.Info
                    },
                },
                ["ELECTRICAL"] = new List<ComplianceRule>
                {
                    new ComplianceRule
                    {
                        Code = "EL-001",
                        Description = "No RCD protection on socket circuits",
                        Category = "Electrical",
                        Standard = "BS 7671 Part 4",
                        Severity = AlertSeverity.Critical
                    },
                    new ComplianceRule
                    {
                        Code = "EL-002",
                        Description = "Generator not specified (power reliability concern)",
                        Category = "Electrical",
                        Standard = "Uganda context",
                        Severity = AlertSeverity.Info
                    },
                },
            };

        // Design suggestions by context
        private static readonly List<DesignSuggestion> DesignSuggestions = new List<DesignSuggestion>
        {
            new DesignSuggestion
            {
                Trigger = "tropical_climate",
                Title = "Passive Cooling Strategy",
                Description = "Consider cross-ventilation with opposing windows, " +
                              "deep overhangs (600mm+), and light-coloured roofing " +
                              "to reduce cooling load by up to 40%.",
                Category = "Energy Efficiency",
                SavingsPercent = 15
            },
            new DesignSuggestion
            {
                Trigger = "water_storage",
                Title = "Water Storage Provision",
                Description = "Provide 3–7 day water storage capacity. " +
                              "Underground tank + roof tank with pump set. " +
                              "Size: 150 litres/person/day.",
                Category = "Services",
                SavingsPercent = 0
            },
            new DesignSuggestion
            {
                Trigger = "solar_ready",
                Title = "Solar-Ready Design",
                Description = "Orient roof surfaces north (southern hemisphere) " +
                              "with minimal shading. Pre-install conduit from roof to DB. " +
                              "East Africa gets 4.5–5.5 peak sun hours/day.",
                Category = "Renewables",
                SavingsPercent = 30
            },
            new DesignSuggestion
            {
                Trigger = "rainwater_harvesting",
                Title = "Rainwater Harvesting",
                Description = "Uganda receives 1,200–1,500 mm rainfall/year. " +
                              "Capture from roof area to supplement water supply. " +
                              "100 m² roof = ~100,000 litres/year.",
                Category = "Sustainability",
                SavingsPercent = 20
            },
            new DesignSuggestion
            {
                Trigger = "natural_lighting",
                Title = "Natural Lighting Optimisation",
                Description = "Maximise daylight with window-to-floor ratio of 15–25%. " +
                              "Use light shelves and splayed reveals. " +
                              "Reduces lighting energy by 50%+ in tropical climate.",
                Category = "Energy Efficiency",
                SavingsPercent = 10
            },
            new DesignSuggestion
            {
                Trigger = "material_local",
                Title = "Local Material Preference",
                Description = "Use locally sourced materials to reduce cost and carbon: " +
                              "clay bricks, natural stone, cypress timber. " +
                              "Reduces material cost by 20–35% vs imported.",
                Category = "Cost Saving",
                SavingsPercent = 25
            },
        };

        /// <summary>
        /// Event raised when a new proactive alert is generated.
        /// </summary>
        public event EventHandler<ProactiveAlertEventArgs> AlertGenerated;

        public ProactiveAdvisor(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        #region Budget Monitoring

        /// <summary>
        /// Sets the project budget for monitoring.
        /// </summary>
        public void SetBudget(double budgetUGX)
        {
            lock (_lock)
            {
                _projectBudgetUGX = budgetUGX;
                Logger.Info($"Project budget set: UGX {budgetUGX:N0} (${budgetUGX / 3750:N0})");
            }
        }

        /// <summary>
        /// Updates cumulative cost and checks budget thresholds.
        /// </summary>
        public ProactiveAlert CheckBudget(double newCostUGX)
        {
            lock (_lock)
            {
                _cumulativeCostUGX += newCostUGX;

                if (_projectBudgetUGX <= 0) return null;

                double ratio = _cumulativeCostUGX / _projectBudgetUGX;

                if (ratio >= BUDGET_OVERRUN_THRESHOLD)
                {
                    return CreateBudgetAlert(AlertSeverity.Critical,
                        $"BUDGET OVERRUN: UGX {_cumulativeCostUGX:N0} " +
                        $"({ratio:P0} of UGX {_projectBudgetUGX:N0} budget). " +
                        $"Over by UGX {_cumulativeCostUGX - _projectBudgetUGX:N0}. " +
                        "Consider value engineering or scope reduction.");
                }
                else if (ratio >= BUDGET_CRITICAL_THRESHOLD)
                {
                    return CreateBudgetAlert(AlertSeverity.Critical,
                        $"Budget limit reached: UGX {_cumulativeCostUGX:N0} " +
                        $"({ratio:P0}). Remaining: UGX 0.");
                }
                else if (ratio >= BUDGET_WARNING_THRESHOLD)
                {
                    return CreateBudgetAlert(AlertSeverity.Warning,
                        $"Budget 80% used: UGX {_cumulativeCostUGX:N0} of " +
                        $"UGX {_projectBudgetUGX:N0}. " +
                        $"Remaining: UGX {_projectBudgetUGX - _cumulativeCostUGX:N0}.");
                }

                return null;
            }
        }

        /// <summary>
        /// Gets current budget status summary.
        /// </summary>
        public string GetBudgetStatus()
        {
            lock (_lock)
            {
                if (_projectBudgetUGX <= 0)
                    return "No project budget set. Use 'set budget [amount]' to enable budget monitoring.";

                double ratio = _cumulativeCostUGX / _projectBudgetUGX;
                double remainingUGX = _projectBudgetUGX - _cumulativeCostUGX;
                string status = ratio >= 1.0 ? "OVERRUN" : ratio >= 0.8 ? "WARNING" : "OK";

                return $"Budget Status: {status}\n" +
                       $"  Budget: UGX {_projectBudgetUGX:N0} (${_projectBudgetUGX / 3750:N0})\n" +
                       $"  Spent: UGX {_cumulativeCostUGX:N0} ({ratio:P0})\n" +
                       $"  Remaining: UGX {remainingUGX:N0} (${remainingUGX / 3750:N0})";
            }
        }

        #endregion

        #region Compliance Checking

        /// <summary>
        /// Runs compliance checks on the current model and returns violations.
        /// </summary>
        public List<ProactiveAlert> CheckCompliance(string category = null)
        {
            var alerts = new List<ProactiveAlert>();

            try
            {
                if (category == null || category.Equals("FIRE_SAFETY", StringComparison.OrdinalIgnoreCase))
                    alerts.AddRange(CheckFireSafety());

                if (category == null || category.Equals("ACCESSIBILITY", StringComparison.OrdinalIgnoreCase))
                    alerts.AddRange(CheckAccessibility());

                if (category == null || category.Equals("VENTILATION", StringComparison.OrdinalIgnoreCase))
                    alerts.AddRange(CheckVentilation());

                if (category == null || category.Equals("ELECTRICAL", StringComparison.OrdinalIgnoreCase))
                    alerts.AddRange(CheckElectrical());

                // Emit alerts
                foreach (var alert in alerts)
                {
                    EmitAlert(alert);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Compliance check failed");
            }

            return alerts;
        }

        private List<ProactiveAlert> CheckFireSafety()
        {
            var alerts = new List<ProactiveAlert>();

            // Check door widths for fire exits
            var doors = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            foreach (var door in doors)
            {
                var typeName = (door.Symbol?.Name ?? "").ToLowerInvariant();
                if (typeName.Contains("fire") || typeName.Contains("exit") || typeName.Contains("escape"))
                {
                    var widthParam = door.Symbol?.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                    if (widthParam != null)
                    {
                        double widthMm = widthParam.AsDouble() * 304.8;
                        if (widthMm < 900)
                        {
                            alerts.Add(new ProactiveAlert
                            {
                                Severity = AlertSeverity.Critical,
                                Category = "Fire Safety",
                                Code = "FS-002",
                                Message = $"Fire door '{door.Symbol.Name}' width {widthMm:F0}mm " +
                                         $"< 900mm minimum (UNBS US 319).",
                                ElementId = door.Id,
                                Recommendation = "Increase fire door width to minimum 900mm."
                            });
                        }
                    }
                }
            }

            return alerts;
        }

        private List<ProactiveAlert> CheckAccessibility()
        {
            var alerts = new List<ProactiveAlert>();

            // Check for accessible doors on ground floor
            var doors = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            var groundFloorDoors = doors.Where(d =>
            {
                var level = d.Level;
                return level != null && (level.Elevation < 1.0); // ground floor
            }).ToList();

            foreach (var door in groundFloorDoors)
            {
                var widthParam = door.Symbol?.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                if (widthParam != null)
                {
                    double widthMm = widthParam.AsDouble() * 304.8;
                    if (widthMm < 900)
                    {
                        alerts.Add(new ProactiveAlert
                        {
                            Severity = AlertSeverity.Warning,
                            Category = "Accessibility",
                            Code = "AC-001",
                            Message = $"Ground floor door width {widthMm:F0}mm < 900mm. " +
                                     "May not comply with PWD Act accessible route requirements.",
                            ElementId = door.Id,
                            Recommendation = "Widen door to 900mm for wheelchair access."
                        });
                    }
                }
            }

            return alerts;
        }

        private List<ProactiveAlert> CheckVentilation()
        {
            var alerts = new List<ProactiveAlert>();

            // Check rooms have windows
            var rooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .Where(r => r.Area > 0)
                .ToList();

            var windows = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Windows)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            foreach (var room in rooms)
            {
                var roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";
                var lower = roomName.ToLowerInvariant();

                // Skip rooms that don't need windows
                if (lower.Contains("store") || lower.Contains("server") ||
                    lower.Contains("plant") || lower.Contains("corridor") ||
                    lower.Contains("lift") || lower.Contains("shaft"))
                    continue;

                // Check if room has windows (simplified — check by level)
                var roomLevel = room.Level;
                var roomWindows = windows.Where(w => w.Level?.Id == roomLevel?.Id).ToList();

                if (roomWindows.Count == 0 && rooms.Count < 20) // only alert for small projects
                {
                    alerts.Add(new ProactiveAlert
                    {
                        Severity = AlertSeverity.Info,
                        Category = "Ventilation",
                        Code = "VN-001",
                        Message = $"Room '{roomName}' may lack natural ventilation. " +
                                 "Consider adding openable windows.",
                        Recommendation = "Add openable windows for cross-ventilation (tropical climate)."
                    });
                }
            }

            return alerts;
        }

        private List<ProactiveAlert> CheckElectrical()
        {
            var alerts = new List<ProactiveAlert>();

            // Check for generator provision
            var generators = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(e => (e.Symbol?.Name ?? "").ToLowerInvariant().Contains("generator"))
                .ToList();

            if (generators.Count == 0)
            {
                alerts.Add(new ProactiveAlert
                {
                    Severity = AlertSeverity.Info,
                    Category = "Electrical",
                    Code = "EL-002",
                    Message = "No generator detected in model. Uganda has power reliability " +
                             "challenges — backup generation is strongly recommended.",
                    Recommendation = "Add standby generator sized to critical loads."
                });
            }

            return alerts;
        }

        #endregion

        #region Design Suggestions

        /// <summary>
        /// Gets design suggestions relevant to the current model context.
        /// </summary>
        public List<DesignSuggestion> GetDesignSuggestions()
        {
            var suggestions = new List<DesignSuggestion>();

            // Always suggest tropical climate strategies
            suggestions.Add(DesignSuggestions.First(s => s.Trigger == "tropical_climate"));
            suggestions.Add(DesignSuggestions.First(s => s.Trigger == "water_storage"));
            suggestions.Add(DesignSuggestions.First(s => s.Trigger == "solar_ready"));
            suggestions.Add(DesignSuggestions.First(s => s.Trigger == "rainwater_harvesting"));

            // Check for natural lighting opportunities
            var rooms = new FilteredElementCollector(_document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .Where(r => r.Area > 0)
                .ToList();

            if (rooms.Count > 0)
            {
                suggestions.Add(DesignSuggestions.First(s => s.Trigger == "natural_lighting"));
            }

            suggestions.Add(DesignSuggestions.First(s => s.Trigger == "material_local"));

            return suggestions;
        }

        /// <summary>
        /// Formats design suggestions for chat display.
        /// </summary>
        public string FormatSuggestions()
        {
            var suggestions = GetDesignSuggestions();
            var lines = new List<string> { "Design Suggestions for East Africa:\n" };

            foreach (var s in suggestions)
            {
                string savingsNote = s.SavingsPercent > 0
                    ? $" (potential {s.SavingsPercent}% savings)"
                    : "";
                lines.Add($"[{s.Category}] {s.Title}{savingsNote}");
                lines.Add($"  {s.Description}\n");
            }

            return string.Join("\n", lines);
        }

        #endregion

        #region Full Model Audit

        /// <summary>
        /// Runs a comprehensive model audit combining budget, compliance, and design checks.
        /// </summary>
        public ModelAuditResult RunFullAudit()
        {
            var auditResult = new ModelAuditResult
            {
                AuditTime = DateTime.Now,
                BudgetStatus = GetBudgetStatus(),
                ComplianceAlerts = CheckCompliance(),
                DesignSuggestions = GetDesignSuggestions()
            };

            // Summary
            int critical = auditResult.ComplianceAlerts.Count(a => a.Severity == AlertSeverity.Critical);
            int warnings = auditResult.ComplianceAlerts.Count(a => a.Severity == AlertSeverity.Warning);
            int info = auditResult.ComplianceAlerts.Count(a => a.Severity == AlertSeverity.Info);

            auditResult.Summary = $"Model Audit Summary:\n" +
                                  $"  Critical issues: {critical}\n" +
                                  $"  Warnings: {warnings}\n" +
                                  $"  Informational: {info}\n" +
                                  $"  Design suggestions: {auditResult.DesignSuggestions.Count}\n" +
                                  $"\n{auditResult.BudgetStatus}";

            return auditResult;
        }

        #endregion

        #region Helpers

        private ProactiveAlert CreateBudgetAlert(AlertSeverity severity, string message)
        {
            var alert = new ProactiveAlert
            {
                Severity = severity,
                Category = "Budget",
                Code = severity == AlertSeverity.Critical ? "BG-002" : "BG-001",
                Message = message,
                Recommendation = severity == AlertSeverity.Critical
                    ? "Review scope and apply value engineering to reduce costs."
                    : "Monitor spending closely on remaining items."
            };

            EmitAlert(alert);
            return alert;
        }

        private void EmitAlert(ProactiveAlert alert)
        {
            lock (_lock)
            {
                // Check cooldown to avoid alert spam
                var key = $"{alert.Code}_{alert.ElementId?.IntegerValue ?? 0}";
                if (_suppressedAlerts.ContainsKey(key) &&
                    DateTime.Now - _suppressedAlerts[key] < _alertCooldown)
                {
                    return;
                }

                _suppressedAlerts[key] = DateTime.Now;
                _pendingAlerts.Add(alert);
                AlertGenerated?.Invoke(this, new ProactiveAlertEventArgs { Alert = alert });
            }
        }

        #endregion
    }

    #region Data Types

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    public class ProactiveAlert
    {
        public AlertSeverity Severity { get; set; }
        public string Category { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public string Recommendation { get; set; }
        public ElementId ElementId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string FormatForChat()
        {
            var icon = Severity == AlertSeverity.Critical ? "CRITICAL"
                     : Severity == AlertSeverity.Warning ? "WARNING"
                     : "INFO";
            return $"[{icon}] [{Category}] {Code}: {Message}\n  Recommendation: {Recommendation}";
        }
    }

    public class ProactiveAlertEventArgs : EventArgs
    {
        public ProactiveAlert Alert { get; set; }
    }

    public class ComplianceRule
    {
        public string Code { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Standard { get; set; }
        public AlertSeverity Severity { get; set; }
    }

    public class DesignSuggestion
    {
        public string Trigger { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public int SavingsPercent { get; set; }
    }

    public class ModelAuditResult
    {
        public DateTime AuditTime { get; set; }
        public string Summary { get; set; }
        public string BudgetStatus { get; set; }
        public List<ProactiveAlert> ComplianceAlerts { get; set; } = new List<ProactiveAlert>();
        public List<DesignSuggestion> DesignSuggestions { get; set; } = new List<DesignSuggestion>();
    }

    #endregion
}
