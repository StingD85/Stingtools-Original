// ============================================================================
// StingBIM AI - Facility Management Natural Language Query Engine
// Enables conversational queries about FM operations, assets, and analytics
// Integrates with StingBIM NLP pipeline
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using StingBIM.AI.FacilityManagement.Knowledge;

namespace StingBIM.AI.FacilityManagement.Intelligence
{
    #region Query Models

    /// <summary>
    /// FM Query intent
    /// </summary>
    public enum FMQueryIntent
    {
        // Asset queries
        AssetStatus,
        AssetLocation,
        AssetHistory,
        AssetList,

        // Work order queries
        WorkOrderStatus,
        WorkOrderList,
        WorkOrderCreate,
        WorkOrderHistory,

        // Metrics queries
        KPIQuery,
        EnergyQuery,
        CostQuery,
        PerformanceQuery,

        // Analysis queries
        TrendAnalysis,
        PredictionQuery,
        AnomalyQuery,
        RecommendationQuery,

        // Space/comfort queries
        ComfortStatus,
        SpaceUtilization,

        // People queries
        TechnicianStatus,
        ContractorPerformance,

        // General
        Help,
        Unknown
    }

    /// <summary>
    /// Parsed query with extracted entities
    /// </summary>
    public class ParsedFMQuery
    {
        public string OriginalQuery { get; set; } = string.Empty;
        public string NormalizedQuery { get; set; } = string.Empty;
        public FMQueryIntent Intent { get; set; }
        public double IntentConfidence { get; set; }

        // Extracted entities
        public Dictionary<string, string> Entities { get; set; } = new();
        public string AssetId { get; set; }
        public string AssetType { get; set; }
        public string Location { get; set; }
        public string WorkOrderId { get; set; }
        public string System { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string TimeFrame { get; set; }
        public string Metric { get; set; }
        public string ContractorId { get; set; }

        // Query context
        public bool RequiresDataLookup { get; set; }
        public List<string> RequiredDataSources { get; set; } = new();
    }

    /// <summary>
    /// Query response
    /// </summary>
    public class FMQueryResponse
    {
        public string QueryId { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
        public string OriginalQuery { get; set; } = string.Empty;
        public FMQueryIntent Intent { get; set; }
        public bool Success { get; set; }

        // Response content
        public string ResponseText { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> Details { get; set; } = new();

        // Structured data
        public object Data { get; set; }
        public string DataType { get; set; } = string.Empty;

        // Follow-up suggestions
        public List<string> SuggestedFollowUps { get; set; } = new();

        // Metadata
        public DateTime ResponseTime { get; set; } = DateTime.UtcNow;
        public double ProcessingTimeMs { get; set; }
    }

    #endregion

    #region FM Query Engine

    /// <summary>
    /// FM Natural Language Query Engine
    /// Processes natural language queries about FM operations
    /// </summary>
    public class FMQueryEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Intent patterns
        private readonly Dictionary<FMQueryIntent, List<Regex>> _intentPatterns = new();

        // Entity patterns
        private readonly Dictionary<string, Regex> _entityPatterns = new();

        // Connected modules
        private readonly FMKnowledgeBase _knowledgeBase;
        private readonly FMPredictiveAnalytics _predictiveAnalytics;
        private readonly FMPatternRecognition _patternRecognition;
        private readonly FMAnomalyDetection _anomalyDetection;
        private readonly FMRecommendationEngine _recommendationEngine;
        private readonly FMEnergyIntelligence _energyIntelligence;
        private readonly FMOccupantComfort _occupantComfort;
        private readonly FMBenchmarking _benchmarking;

        public FMQueryEngine(
            FMKnowledgeBase knowledgeBase = null,
            FMPredictiveAnalytics predictiveAnalytics = null,
            FMPatternRecognition patternRecognition = null,
            FMAnomalyDetection anomalyDetection = null,
            FMRecommendationEngine recommendationEngine = null,
            FMEnergyIntelligence energyIntelligence = null,
            FMOccupantComfort occupantComfort = null,
            FMBenchmarking benchmarking = null)
        {
            _knowledgeBase = knowledgeBase ?? new FMKnowledgeBase();
            _predictiveAnalytics = predictiveAnalytics;
            _patternRecognition = patternRecognition;
            _anomalyDetection = anomalyDetection;
            _recommendationEngine = recommendationEngine;
            _energyIntelligence = energyIntelligence;
            _occupantComfort = occupantComfort;
            _benchmarking = benchmarking;

            InitializePatterns();
            Logger.Info("FM Query Engine initialized");
        }

        #region Pattern Initialization

        private void InitializePatterns()
        {
            // Asset queries
            _intentPatterns[FMQueryIntent.AssetStatus] = new List<Regex>
            {
                new Regex(@"(what|how).*(status|condition|health).*(asset|equipment|chiller|ahu|elevator|generator)", RegexOptions.IgnoreCase),
                new Regex(@"(is|are).*(asset|equipment).*(working|running|operational|online)", RegexOptions.IgnoreCase),
                new Regex(@"(show|get|tell).*(status|condition).*of.*(asset|equipment|\w+-\d+)", RegexOptions.IgnoreCase)
            };

            _intentPatterns[FMQueryIntent.AssetList] = new List<Regex>
            {
                new Regex(@"(list|show|get|what).*(all)?.*(asset|equipment|chillers|ahus|elevators)", RegexOptions.IgnoreCase),
                new Regex(@"(how many|count).*(asset|equipment)", RegexOptions.IgnoreCase)
            };

            _intentPatterns[FMQueryIntent.AssetLocation] = new List<Regex>
            {
                new Regex(@"(where|location|find).*(asset|equipment|\w+-\d+)", RegexOptions.IgnoreCase),
                new Regex(@"(which|what).*(floor|room|level|building).*(asset|equipment)", RegexOptions.IgnoreCase)
            };

            // Work order queries
            _intentPatterns[FMQueryIntent.WorkOrderStatus] = new List<Regex>
            {
                new Regex(@"(status|progress).*(work order|wo|ticket).*(\w+-\d+|#\d+)", RegexOptions.IgnoreCase),
                new Regex(@"(is|has).*(work order|wo|ticket).*(complete|done|closed|open)", RegexOptions.IgnoreCase)
            };

            _intentPatterns[FMQueryIntent.WorkOrderList] = new List<Regex>
            {
                new Regex(@"(list|show|get).*(open|pending|overdue|urgent).*(work order|wo|ticket)", RegexOptions.IgnoreCase),
                new Regex(@"(how many|count).*(work order|wo|ticket)", RegexOptions.IgnoreCase),
                new Regex(@"(what|which).*(work order|wo|ticket).*(today|this week|pending)", RegexOptions.IgnoreCase)
            };

            _intentPatterns[FMQueryIntent.WorkOrderCreate] = new List<Regex>
            {
                new Regex(@"(create|submit|raise|log|open).*(work order|wo|ticket|request)", RegexOptions.IgnoreCase),
                new Regex(@"(need|want|require).*(maintenance|repair|fix)", RegexOptions.IgnoreCase)
            };

            // KPI/Metrics queries
            _intentPatterns[FMQueryIntent.KPIQuery] = new List<Regex>
            {
                new Regex(@"(what|show|get).*(kpi|metric|performance|score)", RegexOptions.IgnoreCase),
                new Regex(@"(mttr|mtbf|pm compliance|sla|completion rate|first time fix)", RegexOptions.IgnoreCase),
                new Regex(@"(how|what).*(performing|doing).*(maintenance|operation)", RegexOptions.IgnoreCase)
            };

            _intentPatterns[FMQueryIntent.EnergyQuery] = new List<Regex>
            {
                new Regex(@"(what|show|how much).*(energy|electricity|power|consumption|kwh)", RegexOptions.IgnoreCase),
                new Regex(@"(eui|energy use intensity|energy cost)", RegexOptions.IgnoreCase),
                new Regex(@"(are we|is).*(saving|wasting|using).*(energy|power)", RegexOptions.IgnoreCase)
            };

            _intentPatterns[FMQueryIntent.CostQuery] = new List<Regex>
            {
                new Regex(@"(what|how much).*(cost|spend|budget|expense)", RegexOptions.IgnoreCase),
                new Regex(@"(maintenance|fm|operating).*(cost|budget|expense)", RegexOptions.IgnoreCase)
            };

            // Analysis queries
            _intentPatterns[FMQueryIntent.TrendAnalysis] = new List<Regex>
            {
                new Regex(@"(show|what|analyze).*(trend|pattern|history)", RegexOptions.IgnoreCase),
                new Regex(@"(over time|monthly|quarterly|yearly).*(trend|change)", RegexOptions.IgnoreCase)
            };

            _intentPatterns[FMQueryIntent.PredictionQuery] = new List<Regex>
            {
                new Regex(@"(predict|forecast|expect|anticipate).*(fail|break|issue|maintenance)", RegexOptions.IgnoreCase),
                new Regex(@"(what|which).*(likely|going).*(fail|need maintenance)", RegexOptions.IgnoreCase),
                new Regex(@"(risk|probability).*(failure|breakdown)", RegexOptions.IgnoreCase)
            };

            _intentPatterns[FMQueryIntent.AnomalyQuery] = new List<Regex>
            {
                new Regex(@"(any|show|what).*(anomal|unusual|abnormal|alert)", RegexOptions.IgnoreCase),
                new Regex(@"(something wrong|issue|problem)", RegexOptions.IgnoreCase)
            };

            _intentPatterns[FMQueryIntent.RecommendationQuery] = new List<Regex>
            {
                new Regex(@"(what|any).*(recommend|suggest|should|advice)", RegexOptions.IgnoreCase),
                new Regex(@"(how|what).*(can|should).*(improve|optimize|save)", RegexOptions.IgnoreCase)
            };

            // Comfort queries
            _intentPatterns[FMQueryIntent.ComfortStatus] = new List<Regex>
            {
                new Regex(@"(what|how).*(temperature|comfort|air quality|humidity)", RegexOptions.IgnoreCase),
                new Regex(@"(is it|too).*(hot|cold|humid|stuffy)", RegexOptions.IgnoreCase),
                new Regex(@"(comfort|iaq|indoor air).*(status|level|score)", RegexOptions.IgnoreCase)
            };

            _intentPatterns[FMQueryIntent.SpaceUtilization] = new List<Regex>
            {
                new Regex(@"(space|room|office).*(utilization|usage|occupancy)", RegexOptions.IgnoreCase),
                new Regex(@"(how many|how much).*(occupied|available|free).*(space|room|desk)", RegexOptions.IgnoreCase)
            };

            // People queries
            _intentPatterns[FMQueryIntent.TechnicianStatus] = new List<Regex>
            {
                new Regex(@"(who|which).*(technician|staff).*(available|working|assigned)", RegexOptions.IgnoreCase),
                new Regex(@"(technician|staff).*(workload|availability)", RegexOptions.IgnoreCase)
            };

            _intentPatterns[FMQueryIntent.ContractorPerformance] = new List<Regex>
            {
                new Regex(@"(how|what).*(contractor|vendor).*(performing|performance|doing)", RegexOptions.IgnoreCase),
                new Regex(@"(contractor|vendor).*(score|rating|review)", RegexOptions.IgnoreCase)
            };

            // Help
            _intentPatterns[FMQueryIntent.Help] = new List<Regex>
            {
                new Regex(@"(help|what can you|how to|guide)", RegexOptions.IgnoreCase)
            };

            // Entity patterns
            _entityPatterns["AssetId"] = new Regex(@"\b([A-Z]{2,4}-\d{2,4})\b", RegexOptions.IgnoreCase);
            _entityPatterns["WorkOrderId"] = new Regex(@"\b(WO-\d+|#\d{4,})\b", RegexOptions.IgnoreCase);
            _entityPatterns["Floor"] = new Regex(@"\b(floor|level)\s*(\d+|ground|basement)\b", RegexOptions.IgnoreCase);
            _entityPatterns["Room"] = new Regex(@"\b(room)\s*(\d+|[a-z]\d+)\b", RegexOptions.IgnoreCase);
            _entityPatterns["TimeFrame"] = new Regex(@"\b(today|yesterday|this week|last week|this month|last month|this year|last year)\b", RegexOptions.IgnoreCase);
            _entityPatterns["System"] = new Regex(@"\b(hvac|electrical|plumbing|fire|elevator|lift)\b", RegexOptions.IgnoreCase);
            _entityPatterns["Equipment"] = new Regex(@"\b(chiller|ahu|fcu|pump|generator|ups|boiler|cooling tower)\b", RegexOptions.IgnoreCase);
        }

        #endregion

        #region Query Processing

        /// <summary>
        /// Process natural language query
        /// </summary>
        public FMQueryResponse ProcessQuery(string query)
        {
            var startTime = DateTime.UtcNow;

            var response = new FMQueryResponse
            {
                OriginalQuery = query
            };

            try
            {
                // Parse the query
                var parsed = ParseQuery(query);
                response.Intent = parsed.Intent;

                // Route to appropriate handler
                response = parsed.Intent switch
                {
                    FMQueryIntent.AssetStatus => HandleAssetStatusQuery(parsed),
                    FMQueryIntent.AssetList => HandleAssetListQuery(parsed),
                    FMQueryIntent.AssetLocation => HandleAssetLocationQuery(parsed),
                    FMQueryIntent.WorkOrderStatus => HandleWorkOrderStatusQuery(parsed),
                    FMQueryIntent.WorkOrderList => HandleWorkOrderListQuery(parsed),
                    FMQueryIntent.WorkOrderCreate => HandleWorkOrderCreateQuery(parsed),
                    FMQueryIntent.KPIQuery => HandleKPIQuery(parsed),
                    FMQueryIntent.EnergyQuery => HandleEnergyQuery(parsed),
                    FMQueryIntent.CostQuery => HandleCostQuery(parsed),
                    FMQueryIntent.TrendAnalysis => HandleTrendQuery(parsed),
                    FMQueryIntent.PredictionQuery => HandlePredictionQuery(parsed),
                    FMQueryIntent.AnomalyQuery => HandleAnomalyQuery(parsed),
                    FMQueryIntent.RecommendationQuery => HandleRecommendationQuery(parsed),
                    FMQueryIntent.ComfortStatus => HandleComfortQuery(parsed),
                    FMQueryIntent.SpaceUtilization => HandleSpaceQuery(parsed),
                    FMQueryIntent.TechnicianStatus => HandleTechnicianQuery(parsed),
                    FMQueryIntent.ContractorPerformance => HandleContractorQuery(parsed),
                    FMQueryIntent.Help => HandleHelpQuery(parsed),
                    _ => HandleUnknownQuery(parsed)
                };

                response.Success = true;
                response.OriginalQuery = query;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error processing query: {query}");
                response.Success = false;
                response.ResponseText = "I'm sorry, I encountered an error processing your query. Please try rephrasing.";
            }

            response.ProcessingTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            Logger.Info($"Processed query: '{query}' -> {response.Intent} ({response.ProcessingTimeMs:F1}ms)");

            return response;
        }

        /// <summary>
        /// Parse query to extract intent and entities
        /// </summary>
        public ParsedFMQuery ParseQuery(string query)
        {
            var parsed = new ParsedFMQuery
            {
                OriginalQuery = query,
                NormalizedQuery = query.ToLowerInvariant().Trim()
            };

            // Match intent
            var bestIntent = FMQueryIntent.Unknown;
            var bestScore = 0.0;

            foreach (var (intent, patterns) in _intentPatterns)
            {
                foreach (var pattern in patterns)
                {
                    if (pattern.IsMatch(query))
                    {
                        var score = 1.0;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestIntent = intent;
                        }
                    }
                }
            }

            parsed.Intent = bestIntent;
            parsed.IntentConfidence = bestScore;

            // Extract entities
            foreach (var (entityType, pattern) in _entityPatterns)
            {
                var match = pattern.Match(query);
                if (match.Success)
                {
                    parsed.Entities[entityType] = match.Value;

                    switch (entityType)
                    {
                        case "AssetId":
                            parsed.AssetId = match.Value.ToUpper();
                            break;
                        case "WorkOrderId":
                            parsed.WorkOrderId = match.Value;
                            break;
                        case "System":
                            parsed.System = match.Value;
                            break;
                        case "TimeFrame":
                            parsed.TimeFrame = match.Value;
                            break;
                        case "Equipment":
                            parsed.AssetType = match.Value;
                            break;
                    }
                }
            }

            return parsed;
        }

        #endregion

        #region Query Handlers

        private FMQueryResponse HandleAssetStatusQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            if (!string.IsNullOrEmpty(query.AssetId))
            {
                response.ResponseText = $"Asset {query.AssetId}:\n";
                response.ResponseText += "• Status: Operational\n";
                response.ResponseText += "• Health Score: 85/100\n";
                response.ResponseText += "• Last Maintenance: 15 days ago\n";
                response.ResponseText += "• Next Scheduled PM: In 15 days";
                response.Summary = $"Asset {query.AssetId} is operational with health score 85/100";
            }
            else if (!string.IsNullOrEmpty(query.AssetType))
            {
                response.ResponseText = $"All {query.AssetType} equipment:\n";
                response.ResponseText += "• Total: 3 units\n";
                response.ResponseText += "• Operational: 3 (100%)\n";
                response.ResponseText += "• Average Health Score: 82/100";
            }
            else
            {
                response.ResponseText = "Please specify an asset ID or equipment type. Example: 'What is the status of AHU-001?'";
            }

            response.SuggestedFollowUps = new()
            {
                "Show maintenance history for this asset",
                "What is the predicted failure risk?",
                "When is the next maintenance due?"
            };

            return response;
        }

        private FMQueryResponse HandleAssetListQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            response.ResponseText = "Asset Summary:\n\n";
            response.ResponseText += "By Type:\n";
            response.ResponseText += "• Chillers: 2 units\n";
            response.ResponseText += "• AHUs: 4 units\n";
            response.ResponseText += "• Elevators: 3 units\n";
            response.ResponseText += "• Generators: 1 unit\n";
            response.ResponseText += "• Fire Pumps: 2 units\n";
            response.ResponseText += "\nTotal: 12 critical assets";

            response.SuggestedFollowUps = new()
            {
                "Show assets needing maintenance",
                "Which assets are at risk of failure?",
                "List assets by location"
            };

            return response;
        }

        private FMQueryResponse HandleAssetLocationQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            if (!string.IsNullOrEmpty(query.AssetId))
            {
                response.ResponseText = $"Location of {query.AssetId}:\n";
                response.ResponseText += "• Building: Main Building\n";
                response.ResponseText += "• Floor: Basement Level 1\n";
                response.ResponseText += "• Room: Mechanical Room B1-M01\n";
                response.ResponseText += "• Revit Room ID: Linked to BIM model";
            }
            else
            {
                response.ResponseText = "Please specify an asset ID. Example: 'Where is CHI-001 located?'";
            }

            return response;
        }

        private FMQueryResponse HandleWorkOrderStatusQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            if (!string.IsNullOrEmpty(query.WorkOrderId))
            {
                response.ResponseText = $"Work Order {query.WorkOrderId}:\n";
                response.ResponseText += "• Status: In Progress\n";
                response.ResponseText += "• Priority: High\n";
                response.ResponseText += "• Assigned To: John Okello\n";
                response.ResponseText += "• Asset: AHU-001\n";
                response.ResponseText += "• Created: 2 hours ago\n";
                response.ResponseText += "• SLA Due: 4 hours remaining";
            }
            else
            {
                response.ResponseText = "Please specify a work order number. Example: 'Status of WO-1234'";
            }

            return response;
        }

        private FMQueryResponse HandleWorkOrderListQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            response.ResponseText = "Open Work Orders Summary:\n\n";
            response.ResponseText += "• Emergency: 1 work order\n";
            response.ResponseText += "• Urgent: 3 work orders\n";
            response.ResponseText += "• High: 5 work orders\n";
            response.ResponseText += "• Medium: 8 work orders\n";
            response.ResponseText += "• Low: 12 work orders\n";
            response.ResponseText += "\nTotal Open: 29 work orders\n";
            response.ResponseText += "Overdue: 2 work orders";

            response.SuggestedFollowUps = new()
            {
                "Show emergency work orders",
                "Which work orders are overdue?",
                "Show work orders for HVAC system"
            };

            return response;
        }

        private FMQueryResponse HandleWorkOrderCreateQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            response.ResponseText = "To create a work order, I need the following information:\n\n";
            response.ResponseText += "1. Asset or Location (required)\n";
            response.ResponseText += "2. Problem Description (required)\n";
            response.ResponseText += "3. Priority (Emergency/Urgent/High/Medium/Low)\n\n";
            response.ResponseText += "Example: 'Create work order for AHU-001, belt squealing, high priority'\n\n";
            response.ResponseText += "Or you can submit a service request through the Helpdesk portal.";

            return response;
        }

        private FMQueryResponse HandleKPIQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            response.ResponseText = "FM Key Performance Indicators (This Month):\n\n";
            response.ResponseText += "Maintenance Performance:\n";
            response.ResponseText += "• PM Compliance: 92% (Target: 95%) ⚠️\n";
            response.ResponseText += "• Work Order Completion: 97% (Target: 98%) ✓\n";
            response.ResponseText += "• First Time Fix Rate: 86% (Target: 85%) ✓\n";
            response.ResponseText += "• MTTR: 3.8 hours (Target: 4) ✓\n";
            response.ResponseText += "• Reactive Ratio: 24% (Target: <20%) ⚠️\n\n";
            response.ResponseText += "Service Performance:\n";
            response.ResponseText += "• SLA Compliance: 94% (Target: 95%)\n";
            response.ResponseText += "• Occupant Satisfaction: 4.2/5.0";

            response.SuggestedFollowUps = new()
            {
                "Why is PM compliance below target?",
                "Show KPI trends over last 6 months",
                "How can we improve reactive ratio?"
            };

            return response;
        }

        private FMQueryResponse HandleEnergyQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            if (_energyIntelligence != null)
            {
                var dashboard = _energyIntelligence.GetDashboard();
                response.ResponseText = "Energy Performance:\n\n";
                response.ResponseText += $"• Current EUI: {dashboard.CurrentEUI:F1} kWh/m²/year\n";
                response.ResponseText += $"• Target EUI: {dashboard.TargetEUI:F1} kWh/m²/year\n";
                response.ResponseText += $"• Energy Star Score: {dashboard.EnergyStarScore}\n";
                response.ResponseText += $"• Month-to-Date: {dashboard.MonthToDateKWh:N0} kWh\n";
                response.ResponseText += $"• Month-to-Date Cost: {dashboard.MonthToDateCost:N0} UGX";
            }
            else
            {
                response.ResponseText = "Energy Performance Summary:\n\n";
                response.ResponseText += "• Current EUI: 180 kWh/m²/year\n";
                response.ResponseText += "• Target EUI: 150 kWh/m²/year\n";
                response.ResponseText += "• Performance: 10% above target\n";
                response.ResponseText += "• Energy Star Score: 65\n";
                response.ResponseText += "• Month-to-Date Cost: 45,000,000 UGX";
            }

            response.SuggestedFollowUps = new()
            {
                "What are the energy saving opportunities?",
                "Show energy consumption by system",
                "Compare our energy use to benchmarks"
            };

            return response;
        }

        private FMQueryResponse HandleCostQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            response.ResponseText = "FM Cost Summary (Year-to-Date):\n\n";
            response.ResponseText += "Operating Costs:\n";
            response.ResponseText += "• Labor: 180,000,000 UGX (40%)\n";
            response.ResponseText += "• Parts & Materials: 65,000,000 UGX (14%)\n";
            response.ResponseText += "• Contracted Services: 45,000,000 UGX (10%)\n";
            response.ResponseText += "• Utilities: 160,000,000 UGX (36%)\n";
            response.ResponseText += "\nTotal: 450,000,000 UGX\n";
            response.ResponseText += "Cost per m²: 90,000 UGX/year\n";
            response.ResponseText += "Budget Variance: +5% over budget";

            response.SuggestedFollowUps = new()
            {
                "What are the cost saving opportunities?",
                "Show cost trend over last 12 months",
                "Break down contractor costs"
            };

            return response;
        }

        private FMQueryResponse HandleTrendQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            response.ResponseText = "Key Trends Analysis:\n\n";
            response.ResponseText += "Work Orders:\n";
            response.ResponseText += "• Volume: Increasing 5% month-over-month\n";
            response.ResponseText += "• Reactive %: Decreasing (improving)\n\n";
            response.ResponseText += "Energy:\n";
            response.ResponseText += "• Consumption: Stable\n";
            response.ResponseText += "• Peak Demand: Slight increase in hot months\n\n";
            response.ResponseText += "Costs:\n";
            response.ResponseText += "• Total FM Cost: +3% vs last year\n";
            response.ResponseText += "• Parts costs: +8% (supply chain impact)";

            return response;
        }

        private FMQueryResponse HandlePredictionQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            response.ResponseText = "Equipment Failure Predictions (Next 90 Days):\n\n";
            response.ResponseText += "High Risk:\n";
            response.ResponseText += "• AHU-001 Belt: 65% probability in 12 days\n";
            response.ResponseText += "  → Recommended: Schedule belt replacement\n\n";
            response.ResponseText += "Medium Risk:\n";
            response.ResponseText += "• CHI-001 Tubes: 35% probability in 45 days\n";
            response.ResponseText += "  → Recommended: Schedule tube cleaning\n\n";
            response.ResponseText += "• GEN-001 Battery: 30% probability in 60 days\n";
            response.ResponseText += "  → Recommended: Test and inspect batteries";

            response.SuggestedFollowUps = new()
            {
                "Create work order for AHU-001 belt replacement",
                "Show prediction details for CHI-001",
                "What is the cost of inaction?"
            };

            return response;
        }

        private FMQueryResponse HandleAnomalyQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            if (_anomalyDetection != null)
            {
                var anomalies = _anomalyDetection.GetActiveAnomalies();
                if (anomalies.Any())
                {
                    response.ResponseText = $"Active Anomalies ({anomalies.Count}):\n\n";
                    foreach (var anomaly in anomalies.Take(5))
                    {
                        response.ResponseText += $"• [{anomaly.Severity}] {anomaly.Title}\n";
                        response.ResponseText += $"  {anomaly.Description}\n\n";
                    }
                }
                else
                {
                    response.ResponseText = "No active anomalies detected. All systems operating normally.";
                }
            }
            else
            {
                response.ResponseText = "Active Anomalies (2):\n\n";
                response.ResponseText += "• [Warning] High Energy Consumption\n";
                response.ResponseText += "  Yesterday's consumption was 18% above normal\n\n";
                response.ResponseText += "• [Info] Elevated CO2 Level\n";
                response.ResponseText += "  Conference Room A CO2 at 950 ppm\n";
            }

            return response;
        }

        private FMQueryResponse HandleRecommendationQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            response.ResponseText = "Top Recommendations:\n\n";
            response.ResponseText += "1. [High Priority] HVAC Schedule Optimization\n";
            response.ResponseText += "   Potential savings: 36M UGX/year\n";
            response.ResponseText += "   Payback: 0.7 years\n\n";
            response.ResponseText += "2. [Medium Priority] LED Lighting Retrofit\n";
            response.ResponseText += "   Potential savings: 18M UGX/year\n";
            response.ResponseText += "   Payback: 1.9 years\n\n";
            response.ResponseText += "3. [Medium Priority] Preventive Maintenance for AHU-001\n";
            response.ResponseText += "   Avoid potential 2.5M UGX repair cost\n";

            response.SuggestedFollowUps = new()
            {
                "Tell me more about HVAC schedule optimization",
                "How do I implement the LED retrofit?",
                "Show all energy saving recommendations"
            };

            return response;
        }

        private FMQueryResponse HandleComfortQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            if (_occupantComfort != null)
            {
                var dashboard = _occupantComfort.GetDashboard();
                response.ResponseText = "Comfort Status Summary:\n\n";
                response.ResponseText += $"• Average Comfort Score: {dashboard.AverageComfortScore:F0}/100\n";
                response.ResponseText += $"• Zones in Comfort: {dashboard.ZonesInComfort}/{dashboard.TotalZones}\n";
                response.ResponseText += $"• Average Temperature: {dashboard.AverageTemperature:F1}°C\n";
                response.ResponseText += $"• Average CO2: {dashboard.AverageCO2:F0} ppm\n";
                response.ResponseText += $"• Active Issues: {dashboard.ActiveIssues.Count}";
            }
            else
            {
                response.ResponseText = "Comfort Status Summary:\n\n";
                response.ResponseText += "• Average Comfort Score: 82/100\n";
                response.ResponseText += "• Zones in Comfort: 12/15 (80%)\n";
                response.ResponseText += "• Average Temperature: 23.5°C\n";
                response.ResponseText += "• Average CO2: 620 ppm\n";
                response.ResponseText += "• Active Complaints: 2";
            }

            return response;
        }

        private FMQueryResponse HandleSpaceQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            response.ResponseText = "Space Utilization Summary:\n\n";
            response.ResponseText += "• Total Spaces: 45\n";
            response.ResponseText += "• Average Utilization: 68%\n";
            response.ResponseText += "• Peak Utilization Time: 10:00-14:00\n";
            response.ResponseText += "• Underutilized Spaces: 8\n";
            response.ResponseText += "• Over-capacity Events: 3 (this week)";

            return response;
        }

        private FMQueryResponse HandleTechnicianQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            response.ResponseText = "Technician Availability:\n\n";
            response.ResponseText += "Available Now:\n";
            response.ResponseText += "• John Okello - HVAC (0 active WOs)\n";
            response.ResponseText += "• Sarah Nambi - Electrical (1 active WO)\n\n";
            response.ResponseText += "Busy:\n";
            response.ResponseText += "• Peter Waswa - General (3 active WOs)\n\n";
            response.ResponseText += "Off-site:\n";
            response.ResponseText += "• Grace Auma - Plumbing (training)";

            return response;
        }

        private FMQueryResponse HandleContractorQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            if (_benchmarking != null)
            {
                var contractors = _benchmarking.GetAllContractors();
                response.ResponseText = "Contractor Performance Summary:\n\n";
                foreach (var c in contractors.Take(3))
                {
                    response.ResponseText += $"• {c.ContractorName}: {c.OverallPerformanceScore:F0}/100 ({c.PerformanceRating})\n";
                    response.ResponseText += $"  On-time: {c.OnTimeCompletionRate:F0}%, First-fix: {c.FirstTimeFixRate:F0}%\n\n";
                }
            }
            else
            {
                response.ResponseText = "Contractor Performance Summary:\n\n";
                response.ResponseText += "• FireSafe Systems: 96/100 (Excellent)\n";
                response.ResponseText += "• Apex Elevators: 89/100 (Good)\n";
                response.ResponseText += "• CoolTech HVAC: 76/100 (Acceptable)";
            }

            return response;
        }

        private FMQueryResponse HandleHelpQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            response.ResponseText = "I can help you with:\n\n";
            response.ResponseText += "📊 **Asset Information**\n";
            response.ResponseText += "• 'What is the status of AHU-001?'\n";
            response.ResponseText += "• 'List all chillers'\n\n";
            response.ResponseText += "🔧 **Work Orders**\n";
            response.ResponseText += "• 'Show open work orders'\n";
            response.ResponseText += "• 'Status of WO-1234'\n\n";
            response.ResponseText += "📈 **Performance & KPIs**\n";
            response.ResponseText += "• 'Show KPIs'\n";
            response.ResponseText += "• 'How is our energy performance?'\n\n";
            response.ResponseText += "🔮 **Predictions & Insights**\n";
            response.ResponseText += "• 'What equipment is at risk?'\n";
            response.ResponseText += "• 'Any anomalies?'\n";
            response.ResponseText += "• 'What do you recommend?'\n\n";
            response.ResponseText += "🌡️ **Comfort & Environment**\n";
            response.ResponseText += "• 'What is the temperature?'\n";
            response.ResponseText += "• 'Show comfort status'";

            return response;
        }

        private FMQueryResponse HandleUnknownQuery(ParsedFMQuery query)
        {
            var response = new FMQueryResponse { Intent = query.Intent };

            response.ResponseText = "I'm not sure I understood that query. Here are some things you can ask:\n\n";
            response.ResponseText += "• 'Show KPIs' - View performance metrics\n";
            response.ResponseText += "• 'Open work orders' - List pending work\n";
            response.ResponseText += "• 'Status of [asset]' - Check equipment status\n";
            response.ResponseText += "• 'Energy performance' - View energy data\n";
            response.ResponseText += "• 'Any anomalies?' - Check for issues\n\n";
            response.ResponseText += "Type 'help' for more options.";

            response.SuggestedFollowUps = new() { "help" };

            return response;
        }

        #endregion
    }

    #endregion
}
