// ============================================================================
// StingBIM AI - FM Helpdesk / Service Request Portal
// AI-powered service request classification and routing
// Uses NLP for automatic request categorization and priority assignment
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.FacilityManagement.AssetManagement;
using StingBIM.AI.FacilityManagement.WorkOrders;

namespace StingBIM.AI.FacilityManagement.Helpdesk
{
    #region Service Request Models

    /// <summary>
    /// Service request from building occupants
    /// </summary>
    public class ServiceRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public DateTime RequestedDate { get; set; } = DateTime.UtcNow;
        public string RequestedBy { get; set; } = string.Empty;
        public string RequesterEmail { get; set; } = string.Empty;
        public string RequesterPhone { get; set; } = string.Empty;
        public string RequesterDepartment { get; set; } = string.Empty;

        // Request Details
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public string LocationDescription { get; set; } = string.Empty;
        public string FloorId { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;

        // AI Classification Results
        public RequestCategory Category { get; set; }
        public RequestSubCategory SubCategory { get; set; }
        public RequestPriority Priority { get; set; }
        public double ConfidenceScore { get; set; }
        public string? InferredAssetId { get; set; }
        public string? InferredAssetType { get; set; }
        public List<string> ExtractedKeywords { get; set; } = new();
        public List<string> ExtractedEntities { get; set; } = new();

        // SLA
        public string SLAId { get; set; } = string.Empty;
        public DateTime? SLAResponseTarget { get; set; }
        public DateTime? SLAResolutionTarget { get; set; }

        // Status Tracking
        public RequestStatus Status { get; set; } = RequestStatus.New;
        public string? AssignedTo { get; set; }
        public string? WorkOrderId { get; set; }
        public DateTime? AcknowledgedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public string? ResolutionNotes { get; set; }
        public int? SatisfactionRating { get; set; }
        public string? SatisfactionComments { get; set; }

        // Audit
        public List<RequestHistoryEntry> History { get; set; } = new();

        // Calculated Properties
        public bool IsSLABreached => SLAResolutionTarget.HasValue &&
                                     DateTime.UtcNow > SLAResolutionTarget &&
                                     Status != RequestStatus.Closed;
        public TimeSpan? ResponseTime => AcknowledgedDate.HasValue
            ? AcknowledgedDate.Value - RequestedDate
            : null;
        public TimeSpan? ResolutionTime => ResolvedDate.HasValue
            ? ResolvedDate.Value - RequestedDate
            : null;
    }

    public class RequestHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public RequestStatus? OldStatus { get; set; }
        public RequestStatus? NewStatus { get; set; }
    }

    public enum RequestStatus
    {
        New,
        Acknowledged,
        InProgress,
        OnHold,
        Resolved,
        Closed,
        Cancelled,
        Escalated
    }

    public enum RequestPriority
    {
        Critical,   // Immediate response required
        High,       // Urgent - respond within hours
        Medium,     // Standard - respond within day
        Low         // Non-urgent - can be scheduled
    }

    public enum RequestCategory
    {
        HVAC,
        Electrical,
        Plumbing,
        Fire,
        Security,
        Lift,
        Cleaning,
        General,
        IT,
        Furniture,
        Access,
        Parking,
        Other
    }

    public enum RequestSubCategory
    {
        // HVAC
        Temperature,
        Airflow,
        Noise,
        Odor,
        LeakCondensate,

        // Electrical
        PowerOutage,
        LightingFailure,
        SocketIssue,
        SwitchIssue,

        // Plumbing
        WaterLeak,
        DrainBlocked,
        NoHotWater,
        ToiletIssue,
        TapIssue,

        // Fire
        AlarmFalse,
        AlarmReal,
        ExtinguisherIssue,
        SignageIssue,

        // Security
        AccessCard,
        LockIssue,
        CCTVIssue,
        Intrusion,

        // Lift
        Entrapment,
        Breakdown,
        NoisyOperation,
        DoorIssue,

        // Cleaning
        SpillCleanup,
        GeneralCleaning,
        WasteRemoval,
        WindowCleaning,

        // General
        BuildingFabric,
        Signage,
        Furniture,
        Move,
        Other
    }

    #endregion

    #region NLP-Powered Classification

    /// <summary>
    /// AI-powered request classifier using pattern matching and NLP
    /// </summary>
    public class RequestClassifier
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<RequestCategory, List<string>> _categoryKeywords;
        private readonly Dictionary<RequestSubCategory, List<string>> _subCategoryKeywords;
        private readonly Dictionary<RequestPriority, List<string>> _urgencyKeywords;
        private readonly List<string> _emergencyTerms;
        private readonly AssetRegistry? _assetRegistry;

        public RequestClassifier(AssetRegistry? assetRegistry = null)
        {
            _assetRegistry = assetRegistry;
            _categoryKeywords = InitializeCategoryKeywords();
            _subCategoryKeywords = InitializeSubCategoryKeywords();
            _urgencyKeywords = InitializeUrgencyKeywords();
            _emergencyTerms = InitializeEmergencyTerms();
        }

        /// <summary>
        /// Classify a service request using NLP techniques
        /// </summary>
        public ClassificationResult Classify(string subject, string description, string? location = null)
        {
            var result = new ClassificationResult();
            var text = $"{subject} {description}".ToLower();

            // Extract keywords and entities
            result.ExtractedKeywords = ExtractKeywords(text);
            result.ExtractedEntities = ExtractEntities(text, location);

            // Classify category
            (result.Category, result.CategoryConfidence) = ClassifyCategory(text);

            // Classify sub-category
            (result.SubCategory, result.SubCategoryConfidence) = ClassifySubCategory(text, result.Category);

            // Determine priority
            (result.Priority, result.PriorityConfidence) = ClassifyPriority(text, result.Category);

            // Check for emergency
            result.IsEmergency = CheckForEmergency(text);
            if (result.IsEmergency && result.Priority != RequestPriority.Critical)
            {
                result.Priority = RequestPriority.Critical;
                result.PriorityConfidence = 0.95;
            }

            // Try to identify related asset
            result.InferredAssetType = InferAssetType(text, result.Category);
            if (_assetRegistry != null && location != null)
            {
                result.InferredAssetId = TryMatchAsset(result.InferredAssetType, location);
            }

            // Calculate overall confidence
            result.OverallConfidence = (result.CategoryConfidence + result.SubCategoryConfidence + result.PriorityConfidence) / 3;

            Logger.Debug($"Classified request: {result.Category}/{result.SubCategory}, Priority: {result.Priority}, Confidence: {result.OverallConfidence:P0}");

            return result;
        }

        #region Classification Methods

        private (RequestCategory category, double confidence) ClassifyCategory(string text)
        {
            var scores = new Dictionary<RequestCategory, double>();

            foreach (var (category, keywords) in _categoryKeywords)
            {
                var score = keywords.Sum(k => CountOccurrences(text, k) * GetKeywordWeight(k));
                scores[category] = score;
            }

            if (scores.Values.Max() == 0)
                return (RequestCategory.Other, 0.5);

            var maxScore = scores.Values.Max();
            var bestCategory = scores.First(s => s.Value == maxScore).Key;
            var confidence = Math.Min(0.95, 0.5 + (maxScore / 10.0));

            return (bestCategory, confidence);
        }

        private (RequestSubCategory subCategory, double confidence) ClassifySubCategory(string text, RequestCategory category)
        {
            var relevantSubCategories = GetSubCategoriesForCategory(category);
            var scores = new Dictionary<RequestSubCategory, double>();

            foreach (var subCat in relevantSubCategories)
            {
                if (_subCategoryKeywords.TryGetValue(subCat, out var keywords))
                {
                    var score = keywords.Sum(k => CountOccurrences(text, k) * GetKeywordWeight(k));
                    scores[subCat] = score;
                }
            }

            if (scores.Count == 0 || scores.Values.Max() == 0)
                return (RequestSubCategory.Other, 0.5);

            var maxScore = scores.Values.Max();
            var bestSubCategory = scores.First(s => s.Value == maxScore).Key;
            var confidence = Math.Min(0.95, 0.5 + (maxScore / 8.0));

            return (bestSubCategory, confidence);
        }

        private (RequestPriority priority, double confidence) ClassifyPriority(string text, RequestCategory category)
        {
            // Check for critical indicators
            if (_urgencyKeywords[RequestPriority.Critical].Any(k => text.Contains(k)))
                return (RequestPriority.Critical, 0.9);

            // Check for high priority indicators
            if (_urgencyKeywords[RequestPriority.High].Any(k => text.Contains(k)))
                return (RequestPriority.High, 0.85);

            // Some categories are inherently higher priority
            if (category == RequestCategory.Fire || category == RequestCategory.Security)
                return (RequestPriority.High, 0.8);

            // Check for low priority indicators
            if (_urgencyKeywords[RequestPriority.Low].Any(k => text.Contains(k)))
                return (RequestPriority.Low, 0.8);

            return (RequestPriority.Medium, 0.7);
        }

        private bool CheckForEmergency(string text)
        {
            return _emergencyTerms.Any(term => text.Contains(term));
        }

        private string? InferAssetType(string text, RequestCategory category)
        {
            var assetTypePatterns = new Dictionary<string, List<string>>
            {
                { "AHU", new List<string> { "ahu", "air handling", "air handler" } },
                { "FCU", new List<string> { "fcu", "fan coil", "fancoil" } },
                { "VAV", new List<string> { "vav", "variable air" } },
                { "Chiller", new List<string> { "chiller", "chilled water" } },
                { "Boiler", new List<string> { "boiler", "heating" } },
                { "Pump", new List<string> { "pump" } },
                { "Lift", new List<string> { "lift", "elevator" } },
                { "Generator", new List<string> { "generator", "genset" } },
                { "UPS", new List<string> { "ups", "uninterruptible" } },
                { "Fire Alarm Panel", new List<string> { "fire alarm", "fire panel" } },
                { "Sprinkler", new List<string> { "sprinkler" } },
                { "Light", new List<string> { "light", "lamp", "bulb", "lighting" } },
                { "Air Conditioner", new List<string> { "ac", "air con", "air conditioner", "aircon" } },
                { "Water Heater", new List<string> { "water heater", "geyser", "hot water" } }
            };

            foreach (var (assetType, patterns) in assetTypePatterns)
            {
                if (patterns.Any(p => text.Contains(p)))
                    return assetType;
            }

            // Fall back to category-based inference
            return category switch
            {
                RequestCategory.HVAC => "HVAC Equipment",
                RequestCategory.Electrical => "Electrical Equipment",
                RequestCategory.Plumbing => "Plumbing Fixture",
                RequestCategory.Lift => "Lift",
                RequestCategory.Fire => "Fire Equipment",
                _ => null
            };
        }

        private string? TryMatchAsset(string? assetType, string location)
        {
            if (assetType == null || _assetRegistry == null)
                return null;

            var assetsAtLocation = _assetRegistry.GetByLocation(location);
            var matchingAsset = assetsAtLocation.FirstOrDefault(a =>
                a.AssetType.Contains(assetType, StringComparison.OrdinalIgnoreCase));

            return matchingAsset?.AssetId;
        }

        #endregion

        #region Entity Extraction

        private List<string> ExtractKeywords(string text)
        {
            var keywords = new List<string>();

            // Extract significant words (remove common words)
            var stopWords = new HashSet<string>
            {
                "the", "is", "at", "in", "on", "a", "an", "and", "or", "but", "to", "of",
                "for", "with", "my", "our", "your", "this", "that", "it", "not", "please",
                "can", "could", "would", "should", "need", "want", "have", "has", "been"
            };

            var words = Regex.Split(text, @"\W+")
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .Distinct()
                .Take(10);

            keywords.AddRange(words);

            return keywords;
        }

        private List<string> ExtractEntities(string text, string? location)
        {
            var entities = new List<string>();

            // Extract room numbers (e.g., "room 201", "R201")
            var roomPattern = @"(?:room|rm|r)\s*(\d+[a-z]?)";
            var roomMatches = Regex.Matches(text, roomPattern, RegexOptions.IgnoreCase);
            foreach (Match match in roomMatches)
            {
                entities.Add($"Room:{match.Groups[1].Value}");
            }

            // Extract floor/level references
            var floorPattern = @"(?:floor|level|l)\s*(\d+|basement|ground|roof)";
            var floorMatches = Regex.Matches(text, floorPattern, RegexOptions.IgnoreCase);
            foreach (Match match in floorMatches)
            {
                entities.Add($"Floor:{match.Groups[1].Value}");
            }

            // Extract equipment references (e.g., "AHU-01", "FCU-L2-001")
            var equipPattern = @"\b([A-Z]{2,4}[-_]?\d{1,2}[-_]?\d{0,3})\b";
            var equipMatches = Regex.Matches(text.ToUpper(), equipPattern);
            foreach (Match match in equipMatches)
            {
                entities.Add($"Equipment:{match.Groups[1].Value}");
            }

            if (!string.IsNullOrEmpty(location))
            {
                entities.Add($"Location:{location}");
            }

            return entities.Distinct().ToList();
        }

        #endregion

        #region Keyword Dictionaries

        private Dictionary<RequestCategory, List<string>> InitializeCategoryKeywords()
        {
            return new Dictionary<RequestCategory, List<string>>
            {
                { RequestCategory.HVAC, new List<string>
                    { "air", "temperature", "hot", "cold", "warm", "cool", "ac", "aircon",
                      "heating", "cooling", "ventilation", "fan", "ahu", "fcu", "vav",
                      "thermostat", "humid", "stuffy", "freezing", "boiling", "draft", "draught" }
                },
                { RequestCategory.Electrical, new List<string>
                    { "power", "electric", "light", "lamp", "bulb", "socket", "outlet",
                      "switch", "circuit", "breaker", "outage", "blackout", "flickering",
                      "sparking", "tripped", "fuse", "volt", "current" }
                },
                { RequestCategory.Plumbing, new List<string>
                    { "water", "leak", "leaking", "drip", "pipe", "drain", "blocked",
                      "clogged", "toilet", "sink", "tap", "faucet", "shower", "flood",
                      "overflow", "plumber", "sewage", "smell" }
                },
                { RequestCategory.Fire, new List<string>
                    { "fire", "smoke", "alarm", "sprinkler", "extinguisher", "detector",
                      "evacuation", "emergency", "burning", "flames" }
                },
                { RequestCategory.Security, new List<string>
                    { "security", "access", "card", "badge", "key", "lock", "cctv",
                      "camera", "theft", "stolen", "intruder", "break", "door", "gate" }
                },
                { RequestCategory.Lift, new List<string>
                    { "lift", "elevator", "escalator", "stuck", "trapped", "buttons",
                      "floor", "indicator", "door" }
                },
                { RequestCategory.Cleaning, new List<string>
                    { "clean", "dirty", "mess", "spill", "trash", "rubbish", "garbage",
                      "bin", "mop", "sweep", "dust", "stain", "odor", "smell" }
                },
                { RequestCategory.General, new List<string>
                    { "repair", "fix", "broken", "damage", "maintenance", "issue",
                      "problem", "fault", "defect" }
                },
                { RequestCategory.IT, new List<string>
                    { "network", "wifi", "internet", "computer", "printer", "phone",
                      "projector", "screen", "av", "audio", "video" }
                },
                { RequestCategory.Furniture, new List<string>
                    { "chair", "desk", "table", "cabinet", "drawer", "furniture",
                      "ergonomic", "broken" }
                },
                { RequestCategory.Access, new List<string>
                    { "key", "card", "badge", "access", "locked", "unlock", "entry" }
                },
                { RequestCategory.Parking, new List<string>
                    { "parking", "car", "vehicle", "garage", "barrier", "space", "bay" }
                }
            };
        }

        private Dictionary<RequestSubCategory, List<string>> InitializeSubCategoryKeywords()
        {
            return new Dictionary<RequestSubCategory, List<string>>
            {
                { RequestSubCategory.Temperature, new List<string>
                    { "temperature", "hot", "cold", "warm", "cool", "freezing", "boiling" }
                },
                { RequestSubCategory.Airflow, new List<string>
                    { "airflow", "stuffy", "no air", "ventilation", "draft", "breeze" }
                },
                { RequestSubCategory.Noise, new List<string>
                    { "noise", "noisy", "loud", "sound", "rattling", "humming", "vibrating" }
                },
                { RequestSubCategory.Odor, new List<string>
                    { "odor", "smell", "odour", "stink", "musty", "foul" }
                },
                { RequestSubCategory.WaterLeak, new List<string>
                    { "leak", "leaking", "drip", "dripping", "water", "wet", "flood" }
                },
                { RequestSubCategory.DrainBlocked, new List<string>
                    { "drain", "blocked", "clogged", "backup", "slow", "overflow" }
                },
                { RequestSubCategory.NoHotWater, new List<string>
                    { "hot water", "no hot", "cold water", "heating" }
                },
                { RequestSubCategory.PowerOutage, new List<string>
                    { "power", "outage", "no power", "blackout", "dead" }
                },
                { RequestSubCategory.LightingFailure, new List<string>
                    { "light", "lamp", "bulb", "dark", "dim", "flickering", "not working" }
                },
                { RequestSubCategory.Entrapment, new List<string>
                    { "stuck", "trapped", "inside", "help", "emergency" }
                },
                { RequestSubCategory.Breakdown, new List<string>
                    { "not working", "broken", "out of service", "stopped" }
                }
            };
        }

        private Dictionary<RequestPriority, List<string>> InitializeUrgencyKeywords()
        {
            return new Dictionary<RequestPriority, List<string>>
            {
                { RequestPriority.Critical, new List<string>
                    { "emergency", "urgent", "immediately", "critical", "danger", "safety",
                      "fire", "flood", "trapped", "stuck", "injury", "hazard", "asap" }
                },
                { RequestPriority.High, new List<string>
                    { "urgent", "important", "asap", "today", "now", "quickly", "priority" }
                },
                { RequestPriority.Low, new List<string>
                    { "when possible", "no rush", "minor", "low priority", "whenever",
                      "convenience", "schedule" }
                }
            };
        }

        private List<string> InitializeEmergencyTerms()
        {
            return new List<string>
            {
                "emergency", "fire", "flood", "trapped", "stuck in lift", "no power",
                "water flooding", "sparking", "smoke", "burning", "collapse", "injury",
                "medical", "security breach", "intruder"
            };
        }

        private IEnumerable<RequestSubCategory> GetSubCategoriesForCategory(RequestCategory category)
        {
            return category switch
            {
                RequestCategory.HVAC => new[] { RequestSubCategory.Temperature, RequestSubCategory.Airflow,
                    RequestSubCategory.Noise, RequestSubCategory.Odor, RequestSubCategory.LeakCondensate },
                RequestCategory.Electrical => new[] { RequestSubCategory.PowerOutage, RequestSubCategory.LightingFailure,
                    RequestSubCategory.SocketIssue, RequestSubCategory.SwitchIssue },
                RequestCategory.Plumbing => new[] { RequestSubCategory.WaterLeak, RequestSubCategory.DrainBlocked,
                    RequestSubCategory.NoHotWater, RequestSubCategory.ToiletIssue, RequestSubCategory.TapIssue },
                RequestCategory.Fire => new[] { RequestSubCategory.AlarmFalse, RequestSubCategory.AlarmReal,
                    RequestSubCategory.ExtinguisherIssue, RequestSubCategory.SignageIssue },
                RequestCategory.Lift => new[] { RequestSubCategory.Entrapment, RequestSubCategory.Breakdown,
                    RequestSubCategory.NoisyOperation, RequestSubCategory.DoorIssue },
                RequestCategory.Cleaning => new[] { RequestSubCategory.SpillCleanup, RequestSubCategory.GeneralCleaning,
                    RequestSubCategory.WasteRemoval, RequestSubCategory.WindowCleaning },
                _ => new[] { RequestSubCategory.Other }
            };
        }

        private int CountOccurrences(string text, string keyword)
        {
            return Regex.Matches(text, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase).Count;
        }

        private double GetKeywordWeight(string keyword)
        {
            // More specific keywords get higher weight
            if (keyword.Length > 6) return 1.5;
            if (keyword.Length > 4) return 1.2;
            return 1.0;
        }

        #endregion
    }

    /// <summary>
    /// Result of NLP classification
    /// </summary>
    public class ClassificationResult
    {
        public RequestCategory Category { get; set; }
        public double CategoryConfidence { get; set; }
        public RequestSubCategory SubCategory { get; set; }
        public double SubCategoryConfidence { get; set; }
        public RequestPriority Priority { get; set; }
        public double PriorityConfidence { get; set; }
        public double OverallConfidence { get; set; }
        public bool IsEmergency { get; set; }
        public string? InferredAssetId { get; set; }
        public string? InferredAssetType { get; set; }
        public List<string> ExtractedKeywords { get; set; } = new();
        public List<string> ExtractedEntities { get; set; } = new();
    }

    #endregion

    #region Service Request Portal

    /// <summary>
    /// Main FM Helpdesk portal for managing service requests
    /// </summary>
    public class ServiceRequestPortal
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, ServiceRequest> _requests;
        private readonly RequestClassifier _classifier;
        private readonly SLAManager _slaManager;
        private readonly object _lock = new();
        private int _requestCounter = 0;

        public ServiceRequestPortal(
            AssetRegistry? assetRegistry = null,
            SLAManager? slaManager = null)
        {
            _requests = new Dictionary<string, ServiceRequest>(StringComparer.OrdinalIgnoreCase);
            _classifier = new RequestClassifier(assetRegistry);
            _slaManager = slaManager ?? new SLAManager();
        }

        /// <summary>
        /// Submit a new service request with automatic AI classification
        /// </summary>
        public ServiceRequest SubmitRequest(
            string subject,
            string description,
            string requestedBy,
            string? email = null,
            string? phone = null,
            string? locationId = null,
            string? floorId = null,
            string? roomNumber = null)
        {
            // Generate request ID
            var requestId = $"SR-{DateTime.Now:yyyyMMdd}-{Interlocked.Increment(ref _requestCounter):D4}";

            // AI Classification
            var classification = _classifier.Classify(subject, description, locationId);

            var request = new ServiceRequest
            {
                RequestId = requestId,
                RequestedDate = DateTime.UtcNow,
                RequestedBy = requestedBy,
                RequesterEmail = email ?? string.Empty,
                RequesterPhone = phone ?? string.Empty,
                Subject = subject,
                Description = description,
                LocationId = locationId ?? string.Empty,
                FloorId = floorId ?? string.Empty,
                RoomNumber = roomNumber ?? string.Empty,

                // AI Classification Results
                Category = classification.Category,
                SubCategory = classification.SubCategory,
                Priority = classification.Priority,
                ConfidenceScore = classification.OverallConfidence,
                InferredAssetId = classification.InferredAssetId,
                InferredAssetType = classification.InferredAssetType,
                ExtractedKeywords = classification.ExtractedKeywords,
                ExtractedEntities = classification.ExtractedEntities,

                Status = RequestStatus.New
            };

            // Apply SLA
            var sla = _slaManager.GetSLA(request.Category, request.Priority);
            if (sla != null)
            {
                request.SLAId = sla.SLAId;
                request.SLAResponseTarget = request.RequestedDate.AddMinutes(sla.ResponseTimeMinutes);
                request.SLAResolutionTarget = request.RequestedDate.AddMinutes(sla.ResolutionTimeMinutes);
            }

            // Add history entry
            request.History.Add(new RequestHistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                Action = "Request Created",
                PerformedBy = "System",
                Notes = $"AI Classification: {request.Category}/{request.SubCategory}, Priority: {request.Priority}, Confidence: {request.ConfidenceScore:P0}",
                NewStatus = RequestStatus.New
            });

            // Store request
            lock (_lock)
            {
                _requests[requestId] = request;
            }

            Logger.Info($"Service request {requestId} created: {subject} - {request.Category}/{request.Priority}");

            // If emergency, trigger immediate notification
            if (classification.IsEmergency)
            {
                Logger.Warn($"EMERGENCY REQUEST: {requestId} - {subject}");
                // In production, this would trigger immediate alerts
            }

            return request;
        }

        /// <summary>
        /// Acknowledge a request
        /// </summary>
        public bool AcknowledgeRequest(string requestId, string acknowledgedBy, string? notes = null)
        {
            lock (_lock)
            {
                if (!_requests.TryGetValue(requestId, out var request))
                    return false;

                request.Status = RequestStatus.Acknowledged;
                request.AcknowledgedDate = DateTime.UtcNow;

                request.History.Add(new RequestHistoryEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Action = "Request Acknowledged",
                    PerformedBy = acknowledgedBy,
                    Notes = notes,
                    OldStatus = RequestStatus.New,
                    NewStatus = RequestStatus.Acknowledged
                });

                return true;
            }
        }

        /// <summary>
        /// Assign request to technician
        /// </summary>
        public bool AssignRequest(string requestId, string assignedTo, string assignedBy)
        {
            lock (_lock)
            {
                if (!_requests.TryGetValue(requestId, out var request))
                    return false;

                var oldStatus = request.Status;
                request.Status = RequestStatus.InProgress;
                request.AssignedTo = assignedTo;

                request.History.Add(new RequestHistoryEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Action = $"Assigned to {assignedTo}",
                    PerformedBy = assignedBy,
                    OldStatus = oldStatus,
                    NewStatus = RequestStatus.InProgress
                });

                return true;
            }
        }

        /// <summary>
        /// Link request to work order
        /// </summary>
        public bool LinkToWorkOrder(string requestId, string workOrderId)
        {
            lock (_lock)
            {
                if (!_requests.TryGetValue(requestId, out var request))
                    return false;

                request.WorkOrderId = workOrderId;

                request.History.Add(new RequestHistoryEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Action = $"Linked to Work Order {workOrderId}",
                    PerformedBy = "System"
                });

                return true;
            }
        }

        /// <summary>
        /// Resolve a request
        /// </summary>
        public bool ResolveRequest(string requestId, string resolvedBy, string resolutionNotes)
        {
            lock (_lock)
            {
                if (!_requests.TryGetValue(requestId, out var request))
                    return false;

                var oldStatus = request.Status;
                request.Status = RequestStatus.Resolved;
                request.ResolvedDate = DateTime.UtcNow;
                request.ResolutionNotes = resolutionNotes;

                request.History.Add(new RequestHistoryEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Action = "Request Resolved",
                    PerformedBy = resolvedBy,
                    Notes = resolutionNotes,
                    OldStatus = oldStatus,
                    NewStatus = RequestStatus.Resolved
                });

                return true;
            }
        }

        /// <summary>
        /// Close a request
        /// </summary>
        public bool CloseRequest(string requestId, string closedBy, int? satisfactionRating = null, string? comments = null)
        {
            lock (_lock)
            {
                if (!_requests.TryGetValue(requestId, out var request))
                    return false;

                var oldStatus = request.Status;
                request.Status = RequestStatus.Closed;
                request.ClosedDate = DateTime.UtcNow;
                request.SatisfactionRating = satisfactionRating;
                request.SatisfactionComments = comments;

                request.History.Add(new RequestHistoryEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Action = "Request Closed",
                    PerformedBy = closedBy,
                    Notes = satisfactionRating.HasValue ? $"Satisfaction: {satisfactionRating}/5" : null,
                    OldStatus = oldStatus,
                    NewStatus = RequestStatus.Closed
                });

                return true;
            }
        }

        /// <summary>
        /// Get request by ID
        /// </summary>
        public ServiceRequest? GetRequest(string requestId)
        {
            lock (_lock)
            {
                return _requests.TryGetValue(requestId, out var request) ? request : null;
            }
        }

        /// <summary>
        /// Get all open requests
        /// </summary>
        public IReadOnlyList<ServiceRequest> GetOpenRequests()
        {
            lock (_lock)
            {
                return _requests.Values
                    .Where(r => r.Status != RequestStatus.Closed && r.Status != RequestStatus.Cancelled)
                    .OrderByDescending(r => r.Priority)
                    .ThenBy(r => r.RequestedDate)
                    .ToList();
            }
        }

        /// <summary>
        /// Get requests breaching SLA
        /// </summary>
        public IReadOnlyList<ServiceRequest> GetSLABreachedRequests()
        {
            lock (_lock)
            {
                return _requests.Values
                    .Where(r => r.IsSLABreached)
                    .OrderBy(r => r.SLAResolutionTarget)
                    .ToList();
            }
        }

        /// <summary>
        /// Get helpdesk statistics
        /// </summary>
        public HelpdeskStatistics GetStatistics(DateTime? fromDate = null, DateTime? toDate = null)
        {
            lock (_lock)
            {
                var requests = _requests.Values.AsEnumerable();

                if (fromDate.HasValue)
                    requests = requests.Where(r => r.RequestedDate >= fromDate.Value);
                if (toDate.HasValue)
                    requests = requests.Where(r => r.RequestedDate <= toDate.Value);

                var list = requests.ToList();

                return new HelpdeskStatistics
                {
                    TotalRequests = list.Count,
                    OpenRequests = list.Count(r => r.Status != RequestStatus.Closed && r.Status != RequestStatus.Cancelled),
                    ClosedRequests = list.Count(r => r.Status == RequestStatus.Closed),
                    ByCategory = list.GroupBy(r => r.Category).ToDictionary(g => g.Key, g => g.Count()),
                    ByPriority = list.GroupBy(r => r.Priority).ToDictionary(g => g.Key, g => g.Count()),
                    ByStatus = list.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count()),
                    AverageResponseTime = list.Where(r => r.ResponseTime.HasValue)
                        .Select(r => r.ResponseTime!.Value.TotalMinutes).DefaultIfEmpty(0).Average(),
                    AverageResolutionTime = list.Where(r => r.ResolutionTime.HasValue)
                        .Select(r => r.ResolutionTime!.Value.TotalHours).DefaultIfEmpty(0).Average(),
                    SLACompliancePercent = list.Any(r => r.SLAResolutionTarget.HasValue)
                        ? list.Count(r => !r.IsSLABreached && r.SLAResolutionTarget.HasValue) * 100.0 /
                          list.Count(r => r.SLAResolutionTarget.HasValue)
                        : 100,
                    AverageSatisfaction = list.Where(r => r.SatisfactionRating.HasValue)
                        .Select(r => r.SatisfactionRating!.Value).DefaultIfEmpty(0).Average()
                };
            }
        }
    }

    /// <summary>
    /// Helpdesk statistics summary
    /// </summary>
    public class HelpdeskStatistics
    {
        public int TotalRequests { get; set; }
        public int OpenRequests { get; set; }
        public int ClosedRequests { get; set; }
        public Dictionary<RequestCategory, int> ByCategory { get; set; } = new();
        public Dictionary<RequestPriority, int> ByPriority { get; set; } = new();
        public Dictionary<RequestStatus, int> ByStatus { get; set; } = new();
        public double AverageResponseTime { get; set; } // minutes
        public double AverageResolutionTime { get; set; } // hours
        public double SLACompliancePercent { get; set; }
        public double AverageSatisfaction { get; set; }
    }

    #endregion

    #region SLA Management

    /// <summary>
    /// SLA definition for service requests
    /// </summary>
    public class SLADefinition
    {
        public string SLAId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public RequestPriority Priority { get; set; }
        public RequestCategory? Category { get; set; }
        public int ResponseTimeMinutes { get; set; }
        public int ResolutionTimeMinutes { get; set; }
        public int EscalationLevel1Minutes { get; set; }
        public int EscalationLevel2Minutes { get; set; }
        public bool WorkingHoursOnly { get; set; }
        public double PenaltyPercent { get; set; }
    }

    /// <summary>
    /// Manages SLA definitions and tracking
    /// </summary>
    public class SLAManager
    {
        private readonly Dictionary<string, SLADefinition> _slaDefinitions;

        public SLAManager()
        {
            _slaDefinitions = InitializeDefaultSLAs();
        }

        public SLADefinition? GetSLA(RequestCategory category, RequestPriority priority)
        {
            // Try category-specific SLA first
            var categoryKey = $"{category}-{priority}";
            if (_slaDefinitions.TryGetValue(categoryKey, out var categorySla))
                return categorySla;

            // Fall back to priority-based SLA
            var priorityKey = $"Default-{priority}";
            return _slaDefinitions.TryGetValue(priorityKey, out var prioritySla) ? prioritySla : null;
        }

        public async Task LoadFromCsvAsync(string csvPath)
        {
            if (!File.Exists(csvPath)) return;

            var lines = await File.ReadAllLinesAsync(csvPath);
            var headers = lines[0].Split(',');

            foreach (var line in lines.Skip(1))
            {
                var values = line.Split(',');
                if (values.Length < headers.Length) continue;

                var sla = new SLADefinition
                {
                    SLAId = values[0],
                    Name = values[1],
                    Priority = Enum.Parse<RequestPriority>(values[2]),
                    ResponseTimeMinutes = int.Parse(values[4]),
                    ResolutionTimeMinutes = int.Parse(values[5])
                };

                _slaDefinitions[sla.SLAId] = sla;
            }
        }

        private Dictionary<string, SLADefinition> InitializeDefaultSLAs()
        {
            return new Dictionary<string, SLADefinition>
            {
                { "Default-Critical", new SLADefinition
                    { SLAId = "SLA-CRIT", Name = "Critical", Priority = RequestPriority.Critical,
                      ResponseTimeMinutes = 15, ResolutionTimeMinutes = 60, WorkingHoursOnly = false }
                },
                { "Default-High", new SLADefinition
                    { SLAId = "SLA-HIGH", Name = "High", Priority = RequestPriority.High,
                      ResponseTimeMinutes = 60, ResolutionTimeMinutes = 240, WorkingHoursOnly = false }
                },
                { "Default-Medium", new SLADefinition
                    { SLAId = "SLA-MED", Name = "Medium", Priority = RequestPriority.Medium,
                      ResponseTimeMinutes = 240, ResolutionTimeMinutes = 1440, WorkingHoursOnly = true }
                },
                { "Default-Low", new SLADefinition
                    { SLAId = "SLA-LOW", Name = "Low", Priority = RequestPriority.Low,
                      ResponseTimeMinutes = 480, ResolutionTimeMinutes = 2880, WorkingHoursOnly = true }
                }
            };
        }
    }

    #endregion
}
