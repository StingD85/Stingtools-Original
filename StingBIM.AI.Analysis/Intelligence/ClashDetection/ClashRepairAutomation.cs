// ============================================================================
// StingBIM AI - Clash Detection Repair Automation
// Intelligent clash resolution with automated repair suggestions and ML patterns
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StingBIM.AI.Core.Common;

namespace StingBIM.AI.Intelligence.ClashDetection
{
    /// <summary>
    /// Automated clash detection and repair system with AI-powered resolution.
    /// Learns from past resolutions to improve future suggestions.
    /// </summary>
    public sealed class ClashRepairAutomation
    {
        private static readonly Lazy<ClashRepairAutomation> _instance =
            new Lazy<ClashRepairAutomation>(() => new ClashRepairAutomation());
        public static ClashRepairAutomation Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, ClashPattern> _learnedPatterns = new();
        private readonly Dictionary<string, ResolutionRule> _resolutionRules = new();
        private readonly List<ClashResolutionRecord> _resolutionHistory = new();
        private readonly Dictionary<string, TradePriority> _tradePriorities = new();

        public event EventHandler<ClashEventArgs> ClashDetected;
        public event EventHandler<ClashEventArgs> ResolutionApplied;
        public event EventHandler<ClashEventArgs> PatternLearned;

        private ClashRepairAutomation()
        {
            InitializeResolutionRules();
            InitializeTradePriorities();
        }

        #region Initialization

        private void InitializeResolutionRules()
        {
            // Standard resolution rules based on BIM coordination best practices
            _resolutionRules["mep_vs_structure"] = new ResolutionRule
            {
                RuleId = "mep_vs_structure",
                Description = "MEP elements yield to structural elements",
                PrimaryTrades = new[] { "Mechanical", "Electrical", "Plumbing" },
                SecondaryTrades = new[] { "Structural" },
                Resolution = ResolutionType.RelocatePrimary,
                Priority = 100,
                AutoApplicable = true
            };

            _resolutionRules["pipe_vs_duct"] = new ResolutionRule
            {
                RuleId = "pipe_vs_duct",
                Description = "Pipes typically route under or around ducts",
                PrimaryTrades = new[] { "Plumbing" },
                SecondaryTrades = new[] { "Mechanical" },
                Resolution = ResolutionType.RelocatePrimary,
                Priority = 80,
                AutoApplicable = true
            };

            _resolutionRules["conduit_vs_duct"] = new ResolutionRule
            {
                RuleId = "conduit_vs_duct",
                Description = "Conduit routes around ductwork",
                PrimaryTrades = new[] { "Electrical" },
                SecondaryTrades = new[] { "Mechanical" },
                Resolution = ResolutionType.RelocatePrimary,
                Priority = 75,
                AutoApplicable = true
            };

            _resolutionRules["large_vs_small_duct"] = new ResolutionRule
            {
                RuleId = "large_vs_small_duct",
                Description = "Smaller ducts route around larger ducts",
                PrimaryTrades = new[] { "Mechanical" },
                SecondaryTrades = new[] { "Mechanical" },
                Resolution = ResolutionType.RelocateSmaller,
                Priority = 70,
                AutoApplicable = true
            };

            _resolutionRules["fire_protection_priority"] = new ResolutionRule
            {
                RuleId = "fire_protection_priority",
                Description = "Fire protection systems have priority (code requirement)",
                PrimaryTrades = new[] { "Mechanical", "Electrical", "Plumbing" },
                SecondaryTrades = new[] { "Fire Protection" },
                Resolution = ResolutionType.RelocatePrimary,
                Priority = 95,
                AutoApplicable = true
            };

            _resolutionRules["gravity_drainage"] = new ResolutionRule
            {
                RuleId = "gravity_drainage",
                Description = "Gravity drainage lines maintain slope priority",
                PrimaryTrades = new[] { "Mechanical", "Electrical" },
                SecondaryTrades = new[] { "Plumbing" },
                Resolution = ResolutionType.RelocatePrimary,
                Priority = 85,
                AutoApplicable = true,
                Notes = "Drainage requires specific slopes - other systems must route around"
            };

            _resolutionRules["cable_tray_vs_pipe"] = new ResolutionRule
            {
                RuleId = "cable_tray_vs_pipe",
                Description = "Cable trays route above or around pipes",
                PrimaryTrades = new[] { "Electrical" },
                SecondaryTrades = new[] { "Plumbing", "Mechanical" },
                Resolution = ResolutionType.RelocatePrimary,
                Priority = 65,
                AutoApplicable = true
            };
        }

        private void InitializeTradePriorities()
        {
            // Priority order (higher = less likely to move)
            _tradePriorities["Structural"] = new TradePriority { Trade = "Structural", Priority = 100, MoveFlexibility = 0.1 };
            _tradePriorities["Fire Protection"] = new TradePriority { Trade = "Fire Protection", Priority = 95, MoveFlexibility = 0.3 };
            _tradePriorities["Plumbing"] = new TradePriority { Trade = "Plumbing", Priority = 80, MoveFlexibility = 0.5, Notes = "Gravity lines have low flexibility" };
            _tradePriorities["Mechanical"] = new TradePriority { Trade = "Mechanical", Priority = 70, MoveFlexibility = 0.6 };
            _tradePriorities["Electrical"] = new TradePriority { Trade = "Electrical", Priority = 60, MoveFlexibility = 0.8 };
            _tradePriorities["Architectural"] = new TradePriority { Trade = "Architectural", Priority = 50, MoveFlexibility = 0.7 };
        }

        #endregion

        #region Clash Analysis

        /// <summary>
        /// Analyze clashes and generate automated resolution suggestions
        /// </summary>
        public async Task<ClashAnalysisResult> AnalyzeClashesAsync(List<DetectedClash> clashes)
        {
            return await Task.Run(() =>
            {
                var result = new ClashAnalysisResult
                {
                    AnalyzedAt = DateTime.UtcNow,
                    TotalClashes = clashes.Count,
                    ClashResolutions = new List<ClashResolution>(),
                    PatternInsights = new List<PatternInsight>(),
                    Summary = new ClashAnalysisSummary()
                };

                // Group clashes by type for pattern detection
                var clashGroups = clashes.GroupBy(c => $"{c.PrimaryTrade}_{c.SecondaryTrade}").ToList();

                foreach (var clash in clashes)
                {
                    var resolution = GenerateResolution(clash);
                    result.ClashResolutions.Add(resolution);

                    // Check for patterns
                    DetectPattern(clash, clashGroups);
                }

                // Generate pattern insights
                result.PatternInsights = GeneratePatternInsights(clashGroups);

                // Summary
                result.Summary.AutoResolvable = result.ClashResolutions.Count(r => r.AutoApplicable);
                result.Summary.RequiresReview = result.ClashResolutions.Count(r => !r.AutoApplicable);
                result.Summary.BySeverity = result.ClashResolutions
                    .GroupBy(r => r.Severity)
                    .ToDictionary(g => g.Key, g => g.Count());
                result.Summary.ByTradePair = clashGroups
                    .ToDictionary(g => g.Key, g => g.Count());

                return result;
            });
        }

        private ClashResolution GenerateResolution(DetectedClash clash)
        {
            var resolution = new ClashResolution
            {
                ClashId = clash.ClashId,
                PrimaryElement = clash.PrimaryElementId,
                SecondaryElement = clash.SecondaryElementId,
                PrimaryTrade = clash.PrimaryTrade,
                SecondaryTrade = clash.SecondaryTrade,
                Severity = DetermineSeverity(clash),
                Options = new List<ResolutionOption>()
            };

            // Find applicable rule
            var applicableRule = FindApplicableRule(clash);
            if (applicableRule != null)
            {
                resolution.ApplicableRule = applicableRule;
                resolution.AutoApplicable = applicableRule.AutoApplicable;
            }

            // Check learned patterns
            var patternKey = $"{clash.PrimaryTrade}_{clash.SecondaryTrade}_{clash.ClashType}";
            if (_learnedPatterns.TryGetValue(patternKey, out var pattern) && pattern.SuccessRate > 0.8)
            {
                resolution.PatternMatch = pattern;
                resolution.AutoApplicable = true;
            }

            // Generate resolution options
            resolution.Options = GenerateResolutionOptions(clash, applicableRule);

            // Set recommended option
            resolution.RecommendedOption = resolution.Options
                .OrderByDescending(o => o.Confidence)
                .ThenBy(o => o.EstimatedCost)
                .FirstOrDefault();

            return resolution;
        }

        private ClashSeverity DetermineSeverity(DetectedClash clash)
        {
            // Severity based on intersection volume and trade criticality
            if (clash.IntersectionVolume > 0.1) // > 0.1 cubic meters
                return ClashSeverity.Critical;

            if (clash.PrimaryTrade == "Structural" || clash.SecondaryTrade == "Structural")
                return ClashSeverity.Critical;

            if (clash.PrimaryTrade == "Fire Protection" || clash.SecondaryTrade == "Fire Protection")
                return ClashSeverity.High;

            if (clash.ClashType == "Hard")
                return clash.IntersectionVolume > 0.01 ? ClashSeverity.High : ClashSeverity.Medium;

            return ClashSeverity.Low;
        }

        private ResolutionRule FindApplicableRule(DetectedClash clash)
        {
            foreach (var rule in _resolutionRules.Values.OrderByDescending(r => r.Priority))
            {
                var primaryMatch = rule.PrimaryTrades.Contains(clash.PrimaryTrade);
                var secondaryMatch = rule.SecondaryTrades.Contains(clash.SecondaryTrade);

                if (primaryMatch && secondaryMatch)
                    return rule;

                // Check reverse
                primaryMatch = rule.PrimaryTrades.Contains(clash.SecondaryTrade);
                secondaryMatch = rule.SecondaryTrades.Contains(clash.PrimaryTrade);

                if (primaryMatch && secondaryMatch)
                {
                    // Return inverted rule
                    return new ResolutionRule
                    {
                        RuleId = rule.RuleId + "_inv",
                        Description = rule.Description,
                        PrimaryTrades = rule.SecondaryTrades,
                        SecondaryTrades = rule.PrimaryTrades,
                        Resolution = rule.Resolution == ResolutionType.RelocatePrimary
                            ? ResolutionType.RelocateSecondary
                            : ResolutionType.RelocatePrimary,
                        Priority = rule.Priority,
                        AutoApplicable = rule.AutoApplicable
                    };
                }
            }

            return null;
        }

        private List<ResolutionOption> GenerateResolutionOptions(DetectedClash clash, ResolutionRule rule)
        {
            var options = new List<ResolutionOption>();

            var primaryPriority = _tradePriorities.TryGetValue(clash.PrimaryTrade, out var pp) ? pp : null;
            var secondaryPriority = _tradePriorities.TryGetValue(clash.SecondaryTrade, out var sp) ? sp : null;

            // Option 1: Move primary element
            options.Add(new ResolutionOption
            {
                OptionId = "move_primary",
                Description = $"Relocate {clash.PrimaryTrade} element",
                Action = ResolutionAction.Relocate,
                TargetElement = clash.PrimaryElementId,
                Confidence = primaryPriority?.MoveFlexibility ?? 0.5,
                EstimatedCost = CalculateRelocationCost(clash, true),
                EstimatedHours = CalculateRelocationHours(clash),
                Impact = $"Affects {clash.PrimaryTrade} routing",
                Steps = GenerateRelocationSteps(clash, true)
            });

            // Option 2: Move secondary element
            options.Add(new ResolutionOption
            {
                OptionId = "move_secondary",
                Description = $"Relocate {clash.SecondaryTrade} element",
                Action = ResolutionAction.Relocate,
                TargetElement = clash.SecondaryElementId,
                Confidence = secondaryPriority?.MoveFlexibility ?? 0.5,
                EstimatedCost = CalculateRelocationCost(clash, false),
                EstimatedHours = CalculateRelocationHours(clash),
                Impact = $"Affects {clash.SecondaryTrade} routing",
                Steps = GenerateRelocationSteps(clash, false)
            });

            // Option 3: Resize if applicable
            if (clash.ClashType == "Soft" || clash.ClashType == "Clearance")
            {
                options.Add(new ResolutionOption
                {
                    OptionId = "resize",
                    Description = "Adjust element sizing to provide clearance",
                    Action = ResolutionAction.Resize,
                    Confidence = 0.6,
                    EstimatedCost = CalculateRelocationCost(clash, true) * 0.5m,
                    EstimatedHours = 1.0,
                    Impact = "May require engineering review",
                    Steps = new List<string>
                    {
                        "Verify minimum sizing requirements",
                        "Calculate reduced dimension",
                        "Update element size",
                        "Verify clearance achieved"
                    }
                });
            }

            // Option 4: Add offset/transition
            options.Add(new ResolutionOption
            {
                OptionId = "add_offset",
                Description = "Add offset or transition fitting",
                Action = ResolutionAction.AddFitting,
                Confidence = 0.7,
                EstimatedCost = 350m,
                EstimatedHours = 2.0,
                Impact = "Adds fittings to system",
                Steps = new List<string>
                {
                    "Determine offset direction",
                    "Calculate offset dimensions",
                    "Add transition fittings",
                    "Verify system performance",
                    "Update documentation"
                }
            });

            // Apply rule preference
            if (rule != null)
            {
                foreach (var option in options)
                {
                    if (rule.Resolution == ResolutionType.RelocatePrimary && option.OptionId == "move_primary")
                        option.Confidence *= 1.5;
                    else if (rule.Resolution == ResolutionType.RelocateSecondary && option.OptionId == "move_secondary")
                        option.Confidence *= 1.5;
                }
            }

            // Normalize confidence
            var maxConf = options.Max(o => o.Confidence);
            if (maxConf > 1.0)
            {
                foreach (var option in options)
                    option.Confidence /= maxConf;
            }

            return options.OrderByDescending(o => o.Confidence).ToList();
        }

        private decimal CalculateRelocationCost(DetectedClash clash, bool primary)
        {
            var baseCost = 500m; // Base relocation cost

            // Adjust by trade
            var trade = primary ? clash.PrimaryTrade : clash.SecondaryTrade;
            var multiplier = trade switch
            {
                "Structural" => 5.0m,
                "Fire Protection" => 2.0m,
                "Mechanical" => 1.5m,
                "Plumbing" => 1.3m,
                "Electrical" => 1.0m,
                _ => 1.2m
            };

            return baseCost * multiplier;
        }

        private double CalculateRelocationHours(DetectedClash clash)
        {
            return clash.ClashType switch
            {
                "Hard" => 4.0,
                "Soft" => 2.0,
                "Clearance" => 1.5,
                _ => 3.0
            };
        }

        private List<string> GenerateRelocationSteps(DetectedClash clash, bool primary)
        {
            var element = primary ? "primary" : "secondary";
            return new List<string>
            {
                $"Select {element} element in model",
                "Determine available routing space",
                "Calculate new position/route",
                "Verify no new clashes created",
                "Update element location",
                "Update connected elements",
                "Re-run clash detection",
                "Document resolution"
            };
        }

        #endregion

        #region Pattern Learning

        private void DetectPattern(DetectedClash clash, List<IGrouping<string, DetectedClash>> clashGroups)
        {
            var patternKey = $"{clash.PrimaryTrade}_{clash.SecondaryTrade}_{clash.ClashType}";

            lock (_lock)
            {
                if (!_learnedPatterns.ContainsKey(patternKey))
                {
                    var group = clashGroups.FirstOrDefault(g => g.Key == $"{clash.PrimaryTrade}_{clash.SecondaryTrade}");
                    if (group != null && group.Count() >= 5)
                    {
                        // Potential pattern detected
                        _learnedPatterns[patternKey] = new ClashPattern
                        {
                            PatternId = patternKey,
                            PrimaryTrade = clash.PrimaryTrade,
                            SecondaryTrade = clash.SecondaryTrade,
                            ClashType = clash.ClashType,
                            OccurrenceCount = group.Count(),
                            FirstDetected = DateTime.UtcNow,
                            SuccessRate = 0.5 // Initial confidence
                        };

                        PatternLearned?.Invoke(this, new ClashEventArgs
                        {
                            Type = ClashEventType.PatternDetected,
                            Message = $"Pattern detected: {patternKey} ({group.Count()} occurrences)"
                        });
                    }
                }
                else
                {
                    _learnedPatterns[patternKey].OccurrenceCount++;
                }
            }
        }

        private List<PatternInsight> GeneratePatternInsights(List<IGrouping<string, DetectedClash>> clashGroups)
        {
            var insights = new List<PatternInsight>();

            foreach (var group in clashGroups.Where(g => g.Count() >= 3))
            {
                var trades = group.Key.Split('_');
                insights.Add(new PatternInsight
                {
                    Pattern = group.Key,
                    OccurrenceCount = group.Count(),
                    PrimaryTrade = trades.Length > 0 ? trades[0] : "Unknown",
                    SecondaryTrade = trades.Length > 1 ? trades[1] : "Unknown",
                    Insight = GenerateInsightText(trades, group.Count()),
                    Recommendation = GenerateRecommendation(trades, group.Count())
                });
            }

            return insights.OrderByDescending(i => i.OccurrenceCount).ToList();
        }

        private string GenerateInsightText(string[] trades, int count)
        {
            if (trades.Length < 2) return "Insufficient data for insight";

            return $"Systematic coordination issue detected between {trades[0]} and {trades[1]} trades. " +
                   $"{count} clashes found, suggesting routing strategy review is needed.";
        }

        private string GenerateRecommendation(string[] trades, int count)
        {
            if (count >= 10)
            {
                return $"CRITICAL: Schedule dedicated {trades[0]}/{trades[1]} coordination session. " +
                       "Consider establishing clear routing zones for each trade.";
            }
            else if (count >= 5)
            {
                return $"Review {trades[0]} and {trades[1]} routing strategies in affected areas. " +
                       "Apply automated resolution to standard cases.";
            }
            else
            {
                return "Apply individual resolutions and monitor for pattern emergence.";
            }
        }

        /// <summary>
        /// Record resolution outcome to improve future predictions
        /// </summary>
        public void RecordResolutionOutcome(string clashId, string optionUsed, bool successful, string feedback = null)
        {
            lock (_lock)
            {
                _resolutionHistory.Add(new ClashResolutionRecord
                {
                    ClashId = clashId,
                    OptionUsed = optionUsed,
                    Successful = successful,
                    Feedback = feedback,
                    Timestamp = DateTime.UtcNow
                });

                // Update pattern success rate
                UpdatePatternSuccessRates(optionUsed, successful);
            }
        }

        private void UpdatePatternSuccessRates(string optionUsed, bool successful)
        {
            // Update relevant patterns based on outcome
            foreach (var pattern in _learnedPatterns.Values)
            {
                // Simple exponential moving average update
                var alpha = 0.1; // Learning rate
                var outcome = successful ? 1.0 : 0.0;
                pattern.SuccessRate = pattern.SuccessRate * (1 - alpha) + outcome * alpha;
            }
        }

        #endregion

        #region Batch Resolution

        /// <summary>
        /// Apply automated resolutions to all auto-applicable clashes
        /// </summary>
        public async Task<BatchResolutionResult> ApplyAutomatedResolutionsAsync(ClashAnalysisResult analysis)
        {
            return await Task.Run(() =>
            {
                var result = new BatchResolutionResult
                {
                    StartedAt = DateTime.UtcNow,
                    Resolutions = new List<AppliedResolution>(),
                    Skipped = new List<SkippedResolution>()
                };

                foreach (var clashRes in analysis.ClashResolutions.Where(r => r.AutoApplicable))
                {
                    if (clashRes.RecommendedOption != null)
                    {
                        result.Resolutions.Add(new AppliedResolution
                        {
                            ClashId = clashRes.ClashId,
                            OptionApplied = clashRes.RecommendedOption.OptionId,
                            Success = true, // Simulated - actual would interact with Revit
                            Details = clashRes.RecommendedOption.Description
                        });

                        ResolutionApplied?.Invoke(this, new ClashEventArgs
                        {
                            Type = ClashEventType.ResolutionApplied,
                            ClashId = clashRes.ClashId,
                            Message = $"Applied: {clashRes.RecommendedOption.Description}"
                        });
                    }
                }

                foreach (var clashRes in analysis.ClashResolutions.Where(r => !r.AutoApplicable))
                {
                    result.Skipped.Add(new SkippedResolution
                    {
                        ClashId = clashRes.ClashId,
                        Reason = "Requires manual review",
                        Severity = clashRes.Severity
                    });
                }

                result.CompletedAt = DateTime.UtcNow;
                result.Summary = new BatchResolutionSummary
                {
                    TotalProcessed = result.Resolutions.Count + result.Skipped.Count,
                    Resolved = result.Resolutions.Count(r => r.Success),
                    Failed = result.Resolutions.Count(r => !r.Success),
                    Skipped = result.Skipped.Count,
                    Duration = result.CompletedAt - result.StartedAt
                };

                return result;
            });
        }

        #endregion

        #region Reporting

        /// <summary>
        /// Generate clash resolution report
        /// </summary>
        public ClashReport GenerateReport(ClashAnalysisResult analysis)
        {
            return new ClashReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalClashes = analysis.TotalClashes,
                BySeverity = analysis.Summary.BySeverity,
                ByTradePair = analysis.Summary.ByTradePair,
                AutoResolvable = analysis.Summary.AutoResolvable,
                RequiresReview = analysis.Summary.RequiresReview,
                CriticalClashes = analysis.ClashResolutions
                    .Where(r => r.Severity == ClashSeverity.Critical)
                    .Select(r => new CriticalClashSummary
                    {
                        ClashId = r.ClashId,
                        Trades = $"{r.PrimaryTrade} vs {r.SecondaryTrade}",
                        RecommendedAction = r.RecommendedOption?.Description ?? "Manual review required"
                    }).ToList(),
                PatternInsights = analysis.PatternInsights,
                Recommendations = GenerateOverallRecommendations(analysis)
            };
        }

        private List<string> GenerateOverallRecommendations(ClashAnalysisResult analysis)
        {
            var recommendations = new List<string>();

            if (analysis.Summary.BySeverity.TryGetValue(ClashSeverity.Critical, out var critCount) && critCount > 0)
            {
                recommendations.Add($"PRIORITY: Address {critCount} critical clashes immediately");
            }

            if (analysis.PatternInsights.Any(p => p.OccurrenceCount >= 10))
            {
                recommendations.Add("Schedule cross-discipline coordination meeting to address systematic issues");
            }

            if (analysis.Summary.AutoResolvable > analysis.Summary.RequiresReview)
            {
                recommendations.Add($"Run batch automated resolution to clear {analysis.Summary.AutoResolvable} standard clashes");
            }

            var topTradePairs = analysis.Summary.ByTradePair
                .OrderByDescending(kvp => kvp.Value)
                .Take(3)
                .ToList();

            foreach (var pair in topTradePairs)
            {
                recommendations.Add($"Focus on {pair.Key} coordination: {pair.Value} clashes");
            }

            return recommendations;
        }

        #endregion
    }

    #region Data Models

    public class DetectedClash
    {
        public string ClashId { get; set; }
        public string ClashType { get; set; } // Hard, Soft, Clearance
        public string PrimaryElementId { get; set; }
        public string SecondaryElementId { get; set; }
        public string PrimaryTrade { get; set; }
        public string SecondaryTrade { get; set; }
        public double IntersectionVolume { get; set; }
        public Point3D Location { get; set; }
        public string Zone { get; set; }
        public string Level { get; set; }
    }

    public class ResolutionRule
    {
        public string RuleId { get; set; }
        public string Description { get; set; }
        public string[] PrimaryTrades { get; set; }
        public string[] SecondaryTrades { get; set; }
        public ResolutionType Resolution { get; set; }
        public int Priority { get; set; }
        public bool AutoApplicable { get; set; }
        public string Notes { get; set; }
    }

    public class TradePriority
    {
        public string Trade { get; set; }
        public int Priority { get; set; }
        public double MoveFlexibility { get; set; }
        public string Notes { get; set; }
    }

    public class ClashPattern
    {
        public string PatternId { get; set; }
        public string PrimaryTrade { get; set; }
        public string SecondaryTrade { get; set; }
        public string ClashType { get; set; }
        public int OccurrenceCount { get; set; }
        public DateTime FirstDetected { get; set; }
        public double SuccessRate { get; set; }
        public string PreferredResolution { get; set; }
    }

    public class ClashAnalysisResult
    {
        public DateTime AnalyzedAt { get; set; }
        public int TotalClashes { get; set; }
        public List<ClashResolution> ClashResolutions { get; set; }
        public List<PatternInsight> PatternInsights { get; set; }
        public ClashAnalysisSummary Summary { get; set; }
    }

    public class ClashAnalysisSummary
    {
        public int AutoResolvable { get; set; }
        public int RequiresReview { get; set; }
        public Dictionary<ClashSeverity, int> BySeverity { get; set; }
        public Dictionary<string, int> ByTradePair { get; set; }
    }

    public class ClashResolution
    {
        public string ClashId { get; set; }
        public string PrimaryElement { get; set; }
        public string SecondaryElement { get; set; }
        public string PrimaryTrade { get; set; }
        public string SecondaryTrade { get; set; }
        public ClashSeverity Severity { get; set; }
        public ResolutionRule ApplicableRule { get; set; }
        public ClashPattern PatternMatch { get; set; }
        public bool AutoApplicable { get; set; }
        public List<ResolutionOption> Options { get; set; }
        public ResolutionOption RecommendedOption { get; set; }
    }

    public class ResolutionOption
    {
        public string OptionId { get; set; }
        public string Description { get; set; }
        public ResolutionAction Action { get; set; }
        public string TargetElement { get; set; }
        public double Confidence { get; set; }
        public decimal EstimatedCost { get; set; }
        public double EstimatedHours { get; set; }
        public string Impact { get; set; }
        public List<string> Steps { get; set; }
    }

    public class PatternInsight
    {
        public string Pattern { get; set; }
        public int OccurrenceCount { get; set; }
        public string PrimaryTrade { get; set; }
        public string SecondaryTrade { get; set; }
        public string Insight { get; set; }
        public string Recommendation { get; set; }
    }

    public class ClashResolutionRecord
    {
        public string ClashId { get; set; }
        public string OptionUsed { get; set; }
        public bool Successful { get; set; }
        public string Feedback { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class BatchResolutionResult
    {
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public List<AppliedResolution> Resolutions { get; set; }
        public List<SkippedResolution> Skipped { get; set; }
        public BatchResolutionSummary Summary { get; set; }
    }

    public class AppliedResolution
    {
        public string ClashId { get; set; }
        public string OptionApplied { get; set; }
        public bool Success { get; set; }
        public string Details { get; set; }
    }

    public class SkippedResolution
    {
        public string ClashId { get; set; }
        public string Reason { get; set; }
        public ClashSeverity Severity { get; set; }
    }

    public class BatchResolutionSummary
    {
        public int TotalProcessed { get; set; }
        public int Resolved { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class ClashReport
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalClashes { get; set; }
        public Dictionary<ClashSeverity, int> BySeverity { get; set; }
        public Dictionary<string, int> ByTradePair { get; set; }
        public int AutoResolvable { get; set; }
        public int RequiresReview { get; set; }
        public List<CriticalClashSummary> CriticalClashes { get; set; }
        public List<PatternInsight> PatternInsights { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class CriticalClashSummary
    {
        public string ClashId { get; set; }
        public string Trades { get; set; }
        public string RecommendedAction { get; set; }
    }

    public class ClashEventArgs : EventArgs
    {
        public ClashEventType Type { get; set; }
        public string ClashId { get; set; }
        public string Message { get; set; }
    }

    #endregion

    #region Enums

    public enum ClashSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum ResolutionType
    {
        RelocatePrimary,
        RelocateSecondary,
        RelocateBoth,
        RelocateSmaller,
        Resize,
        AddFitting
    }

    public enum ResolutionAction
    {
        Relocate,
        Resize,
        AddFitting,
        Split,
        Delete,
        Manual
    }

    public enum ClashEventType
    {
        ClashDetected,
        PatternDetected,
        ResolutionApplied,
        ResolutionFailed
    }

    #endregion
}
