using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Automation.FacilityManagement
{
    /// <summary>
    /// Comprehensive space management for facility operations.
    /// Handles occupancy tracking, utilization analysis, and space allocation.
    /// </summary>
    public class SpaceManagementEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly SpaceSettings _settings;
        private readonly SpaceRepository _repository;
        private readonly OccupancyTracker _occupancyTracker;
        private readonly UtilizationAnalyzer _utilizationAnalyzer;
        private readonly AllocationOptimizer _allocationOptimizer;

        public SpaceManagementEngine(SpaceSettings settings = null)
        {
            _settings = settings ?? new SpaceSettings();
            _repository = new SpaceRepository();
            _occupancyTracker = new OccupancyTracker(_settings);
            _utilizationAnalyzer = new UtilizationAnalyzer();
            _allocationOptimizer = new AllocationOptimizer(_settings);
        }

        #region Space Registration

        /// <summary>
        /// Register a space from BIM model.
        /// </summary>
        public async Task<SpaceRegistrationResult> RegisterSpaceAsync(
            SpaceRegistration registration,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Registering space: {registration.Name}");

            var space = new Space
            {
                Id = registration.Id ?? Guid.NewGuid().ToString(),
                Name = registration.Name,
                Number = registration.Number,
                Type = registration.Type,
                Building = registration.Building,
                Floor = registration.Floor,
                Department = registration.Department,
                CostCenter = registration.CostCenter,
                Area = registration.Area,
                Capacity = registration.Capacity,
                BIMRoomId = registration.BIMRoomId,
                Boundaries = registration.Boundaries,
                Status = SpaceStatus.Active,
                CreatedDate = DateTime.UtcNow
            };

            // Set space standards based on type
            space.StandardsCompliance = GetSpaceStandards(space.Type);

            await _repository.AddSpaceAsync(space, cancellationToken);

            return new SpaceRegistrationResult
            {
                Success = true,
                Space = space,
                SpaceId = space.Id
            };
        }

        /// <summary>
        /// Bulk register spaces from BIM model.
        /// </summary>
        public async Task<BulkSpaceRegistrationResult> RegisterSpacesFromBIMAsync(
            BIMModel model,
            IProgress<SpaceProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Registering spaces from BIM model: {model.Id}");
            var result = new BulkSpaceRegistrationResult();

            var rooms = model.GetRooms();
            int processed = 0;

            foreach (var room in rooms)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var registration = new SpaceRegistration
                {
                    Name = room.Name,
                    Number = room.Number,
                    Type = MapRoomToSpaceType(room),
                    Building = room.Building,
                    Floor = room.Level,
                    Area = room.Area,
                    BIMRoomId = room.Id,
                    Boundaries = room.Boundaries
                };

                var regResult = await RegisterSpaceAsync(registration, cancellationToken);
                if (regResult.Success)
                    result.RegisteredSpaces.Add(regResult.Space);

                processed++;
                progress?.Report(new SpaceProgress
                {
                    Phase = "Registering spaces",
                    PercentComplete = (processed * 100) / rooms.Count,
                    CurrentSpace = room.Name
                });
            }

            result.TotalProcessed = processed;
            return result;
        }

        #endregion

        #region Occupancy Tracking

        /// <summary>
        /// Record occupancy count for a space.
        /// </summary>
        public async Task<bool> RecordOccupancyAsync(
            string spaceId,
            int occupantCount,
            OccupancySource source = OccupancySource.Manual,
            CancellationToken cancellationToken = default)
        {
            var space = await _repository.GetSpaceAsync(spaceId, cancellationToken);
            if (space == null) return false;

            var record = new OccupancyRecord
            {
                SpaceId = spaceId,
                Timestamp = DateTime.UtcNow,
                OccupantCount = occupantCount,
                Source = source,
                UtilizationPercentage = space.Capacity > 0
                    ? (occupantCount * 100.0) / space.Capacity
                    : 0
            };

            await _occupancyTracker.RecordAsync(record, cancellationToken);

            // Update space current occupancy
            space.CurrentOccupancy = occupantCount;
            space.LastOccupancyUpdate = DateTime.UtcNow;
            await _repository.UpdateSpaceAsync(space, cancellationToken);

            return true;
        }

        /// <summary>
        /// Get current occupancy for a space.
        /// </summary>
        public async Task<SpaceOccupancy> GetCurrentOccupancyAsync(
            string spaceId,
            CancellationToken cancellationToken = default)
        {
            var space = await _repository.GetSpaceAsync(spaceId, cancellationToken);
            if (space == null) return null;

            return new SpaceOccupancy
            {
                SpaceId = spaceId,
                SpaceName = space.Name,
                Capacity = space.Capacity,
                CurrentOccupancy = space.CurrentOccupancy,
                UtilizationPercentage = space.Capacity > 0
                    ? (space.CurrentOccupancy * 100.0) / space.Capacity
                    : 0,
                LastUpdated = space.LastOccupancyUpdate ?? DateTime.MinValue
            };
        }

        /// <summary>
        /// Get building-wide occupancy summary.
        /// </summary>
        public async Task<BuildingOccupancy> GetBuildingOccupancyAsync(
            string building,
            CancellationToken cancellationToken = default)
        {
            var spaces = await _repository.GetSpacesByBuildingAsync(building, cancellationToken);

            var summary = new BuildingOccupancy
            {
                Building = building,
                Timestamp = DateTime.UtcNow,
                TotalSpaces = spaces.Count,
                TotalCapacity = spaces.Sum(s => s.Capacity),
                TotalOccupancy = spaces.Sum(s => s.CurrentOccupancy)
            };

            summary.OverallUtilization = summary.TotalCapacity > 0
                ? (summary.TotalOccupancy * 100.0) / summary.TotalCapacity
                : 0;

            summary.ByFloor = spaces
                .GroupBy(s => s.Floor)
                .Select(g => new FloorOccupancy
                {
                    Floor = g.Key,
                    Capacity = g.Sum(s => s.Capacity),
                    Occupancy = g.Sum(s => s.CurrentOccupancy),
                    SpaceCount = g.Count()
                }).ToList();

            summary.ByType = spaces
                .GroupBy(s => s.Type)
                .Select(g => new SpaceTypeOccupancy
                {
                    Type = g.Key,
                    Capacity = g.Sum(s => s.Capacity),
                    Occupancy = g.Sum(s => s.CurrentOccupancy),
                    SpaceCount = g.Count()
                }).ToList();

            return summary;
        }

        /// <summary>
        /// Get occupancy history for a space.
        /// </summary>
        public async Task<List<OccupancyRecord>> GetOccupancyHistoryAsync(
            string spaceId,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            return await _occupancyTracker.GetHistoryAsync(spaceId, fromDate, toDate, cancellationToken);
        }

        #endregion

        #region Utilization Analysis

        /// <summary>
        /// Analyze space utilization over time period.
        /// </summary>
        public async Task<UtilizationAnalysis> AnalyzeUtilizationAsync(
            string spaceId,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var space = await _repository.GetSpaceAsync(spaceId, cancellationToken);
            if (space == null) return null;

            var history = await _occupancyTracker.GetHistoryAsync(spaceId, fromDate, toDate, cancellationToken);

            return _utilizationAnalyzer.Analyze(space, history, fromDate, toDate);
        }

        /// <summary>
        /// Analyze portfolio-wide utilization.
        /// </summary>
        public async Task<PortfolioUtilization> AnalyzePortfolioUtilizationAsync(
            DateTime fromDate,
            DateTime toDate,
            IProgress<SpaceProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Analyzing portfolio utilization");
            var allSpaces = await _repository.GetAllSpacesAsync(cancellationToken);
            var result = new PortfolioUtilization
            {
                AnalysisPeriod = $"{fromDate:d} - {toDate:d}",
                TotalSpaces = allSpaces.Count,
                TotalArea = allSpaces.Sum(s => s.Area)
            };

            int processed = 0;
            foreach (var space in allSpaces)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var analysis = await AnalyzeUtilizationAsync(space.Id, fromDate, toDate, cancellationToken);
                if (analysis != null)
                {
                    result.SpaceAnalyses.Add(analysis);
                }

                processed++;
                progress?.Report(new SpaceProgress
                {
                    Phase = "Analyzing utilization",
                    PercentComplete = (processed * 100) / allSpaces.Count
                });
            }

            // Calculate summaries
            result.AverageUtilization = result.SpaceAnalyses.Any()
                ? result.SpaceAnalyses.Average(a => a.AverageUtilization)
                : 0;

            result.UnderutilizedSpaces = result.SpaceAnalyses
                .Where(a => a.AverageUtilization < _settings.UnderutilizationThreshold)
                .Select(a => a.SpaceId)
                .ToList();

            result.OverutilizedSpaces = result.SpaceAnalyses
                .Where(a => a.PeakUtilization > _settings.OverutilizationThreshold)
                .Select(a => a.SpaceId)
                .ToList();

            result.ByBuilding = result.SpaceAnalyses
                .GroupBy(a => allSpaces.First(s => s.Id == a.SpaceId).Building)
                .Select(g => new BuildingUtilizationSummary
                {
                    Building = g.Key,
                    AverageUtilization = g.Average(a => a.AverageUtilization),
                    SpaceCount = g.Count()
                }).ToList();

            return result;
        }

        /// <summary>
        /// Identify underutilized spaces.
        /// </summary>
        public async Task<List<UnderutilizedSpace>> GetUnderutilizedSpacesAsync(
            double threshold,
            int minimumDaysAnalyzed = 30,
            CancellationToken cancellationToken = default)
        {
            var fromDate = DateTime.UtcNow.AddDays(-minimumDaysAnalyzed);
            var toDate = DateTime.UtcNow;

            var portfolio = await AnalyzePortfolioUtilizationAsync(fromDate, toDate, null, cancellationToken);
            var allSpaces = await _repository.GetAllSpacesAsync(cancellationToken);

            return portfolio.SpaceAnalyses
                .Where(a => a.AverageUtilization < threshold)
                .Select(a =>
                {
                    var space = allSpaces.First(s => s.Id == a.SpaceId);
                    return new UnderutilizedSpace
                    {
                        Space = space,
                        Analysis = a,
                        PotentialSavings = CalculatePotentialSavings(space, a),
                        Recommendations = GenerateRecommendations(space, a)
                    };
                })
                .OrderBy(u => u.Analysis.AverageUtilization)
                .ToList();
        }

        #endregion

        #region Space Allocation

        /// <summary>
        /// Allocate space to department/team.
        /// </summary>
        public async Task<AllocationResult> AllocateSpaceAsync(
            AllocationRequest request,
            CancellationToken cancellationToken = default)
        {
            var space = await _repository.GetSpaceAsync(request.SpaceId, cancellationToken);
            if (space == null)
                return new AllocationResult { Success = false, Error = "Space not found" };

            if (space.Status != SpaceStatus.Active && space.Status != SpaceStatus.Available)
                return new AllocationResult { Success = false, Error = "Space not available for allocation" };

            var allocation = new SpaceAllocation
            {
                Id = Guid.NewGuid().ToString(),
                SpaceId = request.SpaceId,
                AllocatedTo = request.AllocatedTo,
                AllocationType = request.AllocationType,
                Department = request.Department,
                CostCenter = request.CostCenter,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Purpose = request.Purpose,
                ApprovedBy = request.ApprovedBy,
                CreatedDate = DateTime.UtcNow
            };

            space.CurrentAllocation = allocation;
            space.Department = request.Department;
            space.CostCenter = request.CostCenter;
            space.Status = SpaceStatus.Allocated;

            await _repository.UpdateSpaceAsync(space, cancellationToken);
            await _repository.AddAllocationAsync(allocation, cancellationToken);

            return new AllocationResult
            {
                Success = true,
                Allocation = allocation
            };
        }

        /// <summary>
        /// Release space allocation.
        /// </summary>
        public async Task<bool> ReleaseAllocationAsync(
            string spaceId,
            string reason = null,
            CancellationToken cancellationToken = default)
        {
            var space = await _repository.GetSpaceAsync(spaceId, cancellationToken);
            if (space == null) return false;

            if (space.CurrentAllocation != null)
            {
                space.CurrentAllocation.EndDate = DateTime.UtcNow;
                space.CurrentAllocation.ReleaseReason = reason;
            }

            space.Status = SpaceStatus.Available;
            space.Department = null;
            space.CostCenter = null;

            await _repository.UpdateSpaceAsync(space, cancellationToken);
            return true;
        }

        /// <summary>
        /// Find available spaces matching criteria.
        /// </summary>
        public async Task<List<Space>> FindAvailableSpacesAsync(
            SpaceRequirements requirements,
            CancellationToken cancellationToken = default)
        {
            var allSpaces = await _repository.GetAllSpacesAsync(cancellationToken);

            return allSpaces
                .Where(s => s.Status == SpaceStatus.Available || s.Status == SpaceStatus.Active)
                .Where(s => string.IsNullOrEmpty(requirements.Building) || s.Building == requirements.Building)
                .Where(s => string.IsNullOrEmpty(requirements.Floor) || s.Floor == requirements.Floor)
                .Where(s => !requirements.MinArea.HasValue || s.Area >= requirements.MinArea)
                .Where(s => !requirements.MinCapacity.HasValue || s.Capacity >= requirements.MinCapacity)
                .Where(s => !requirements.SpaceTypes?.Any() == true || requirements.SpaceTypes.Contains(s.Type))
                .Where(s => !requirements.RequiredAmenities?.Any() == true ||
                            requirements.RequiredAmenities.All(a => s.Amenities.Contains(a)))
                .OrderBy(s => s.Area)
                .ToList();
        }

        /// <summary>
        /// Optimize space allocation for a department move.
        /// </summary>
        public async Task<AllocationOptimizationResult> OptimizeAllocationAsync(
            OptimizationRequest request,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Optimizing allocation for {request.Department}");

            var availableSpaces = await FindAvailableSpacesAsync(
                new SpaceRequirements
                {
                    Building = request.PreferredBuilding,
                    MinCapacity = request.TotalHeadcount
                }, cancellationToken);

            return _allocationOptimizer.Optimize(request, availableSpaces);
        }

        #endregion

        #region Move Management

        /// <summary>
        /// Plan a space move.
        /// </summary>
        public async Task<MovePlan> CreateMovePlanAsync(
            MoveRequest request,
            CancellationToken cancellationToken = default)
        {
            var sourceSpace = await _repository.GetSpaceAsync(request.SourceSpaceId, cancellationToken);
            var targetSpace = await _repository.GetSpaceAsync(request.TargetSpaceId, cancellationToken);

            var plan = new MovePlan
            {
                Id = Guid.NewGuid().ToString(),
                SourceSpace = sourceSpace,
                TargetSpace = targetSpace,
                Department = request.Department,
                MoveDate = request.PreferredDate,
                Occupants = request.Occupants,
                Status = MoveStatus.Planned,
                CreatedDate = DateTime.UtcNow
            };

            // Generate move tasks
            plan.Tasks = GenerateMoveTasks(plan);

            // Estimate costs
            plan.EstimatedCost = EstimateMoveCost(plan);

            await _repository.SaveMovePlanAsync(plan, cancellationToken);
            return plan;
        }

        /// <summary>
        /// Execute a move plan.
        /// </summary>
        public async Task<bool> ExecuteMoveAsync(
            string movePlanId,
            CancellationToken cancellationToken = default)
        {
            var plan = await _repository.GetMovePlanAsync(movePlanId, cancellationToken);
            if (plan == null) return false;

            // Release source allocation
            await ReleaseAllocationAsync(plan.SourceSpace.Id, "Move to new space", cancellationToken);

            // Allocate target space
            await AllocateSpaceAsync(new AllocationRequest
            {
                SpaceId = plan.TargetSpace.Id,
                Department = plan.Department,
                AllocatedTo = plan.Department,
                StartDate = DateTime.UtcNow,
                Purpose = "Relocation"
            }, cancellationToken);

            plan.Status = MoveStatus.Completed;
            plan.CompletedDate = DateTime.UtcNow;
            await _repository.UpdateMovePlanAsync(plan, cancellationToken);

            return true;
        }

        #endregion

        #region Reporting

        /// <summary>
        /// Generate space inventory report.
        /// </summary>
        public async Task<SpaceReport> GenerateSpaceReportAsync(
            SpaceReportOptions options,
            CancellationToken cancellationToken = default)
        {
            var allSpaces = await _repository.GetAllSpacesAsync(cancellationToken);

            if (!string.IsNullOrEmpty(options.Building))
                allSpaces = allSpaces.Where(s => s.Building == options.Building).ToList();

            var report = new SpaceReport
            {
                GeneratedDate = DateTime.UtcNow,
                ReportType = options.ReportType
            };

            report.Summary = new SpaceReportSummary
            {
                TotalSpaces = allSpaces.Count,
                TotalArea = allSpaces.Sum(s => s.Area),
                TotalCapacity = allSpaces.Sum(s => s.Capacity),
                AllocatedSpaces = allSpaces.Count(s => s.Status == SpaceStatus.Allocated),
                AvailableSpaces = allSpaces.Count(s => s.Status == SpaceStatus.Available),
                AllocatedArea = allSpaces.Where(s => s.Status == SpaceStatus.Allocated).Sum(s => s.Area)
            };

            report.ByBuilding = allSpaces
                .GroupBy(s => s.Building)
                .Select(g => new BuildingSpaceSummary
                {
                    Building = g.Key,
                    SpaceCount = g.Count(),
                    TotalArea = g.Sum(s => s.Area),
                    AllocatedArea = g.Where(s => s.Status == SpaceStatus.Allocated).Sum(s => s.Area)
                }).ToList();

            report.ByType = allSpaces
                .GroupBy(s => s.Type)
                .Select(g => new SpaceTypeSummary
                {
                    Type = g.Key,
                    Count = g.Count(),
                    TotalArea = g.Sum(s => s.Area)
                }).ToList();

            report.ByDepartment = allSpaces
                .Where(s => !string.IsNullOrEmpty(s.Department))
                .GroupBy(s => s.Department)
                .Select(g => new DepartmentSpaceSummary
                {
                    Department = g.Key,
                    SpaceCount = g.Count(),
                    TotalArea = g.Sum(s => s.Area),
                    Headcount = g.Sum(s => s.CurrentOccupancy)
                }).ToList();

            return report;
        }

        /// <summary>
        /// Generate cost allocation report.
        /// </summary>
        public async Task<CostAllocationReport> GenerateCostAllocationReportAsync(
            string fiscalYear,
            CancellationToken cancellationToken = default)
        {
            var allSpaces = await _repository.GetAllSpacesAsync(cancellationToken);

            var report = new CostAllocationReport
            {
                FiscalYear = fiscalYear,
                GeneratedDate = DateTime.UtcNow,
                TotalArea = allSpaces.Sum(s => s.Area)
            };

            // Calculate cost per square meter (would come from settings/database)
            var costPerSqm = _settings.CostPerSquareMeter;

            report.ByDepartment = allSpaces
                .Where(s => !string.IsNullOrEmpty(s.Department))
                .GroupBy(s => s.Department)
                .Select(g => new DepartmentCostAllocation
                {
                    Department = g.Key,
                    CostCenter = g.First().CostCenter,
                    AllocatedArea = g.Sum(s => s.Area),
                    AllocatedCost = (decimal)g.Sum(s => s.Area) * costPerSqm,
                    PercentageOfTotal = (g.Sum(s => s.Area) / report.TotalArea) * 100
                }).ToList();

            report.TotalAllocatedCost = report.ByDepartment.Sum(d => d.AllocatedCost);

            return report;
        }

        #endregion

        #region Private Methods

        private SpaceStandards GetSpaceStandards(SpaceType type)
        {
            return type switch
            {
                SpaceType.Office => new SpaceStandards { MinAreaPerPerson = 8, MaxOccupancyDensity = 10 },
                SpaceType.OpenOffice => new SpaceStandards { MinAreaPerPerson = 6, MaxOccupancyDensity = 8 },
                SpaceType.MeetingRoom => new SpaceStandards { MinAreaPerPerson = 2, MaxOccupancyDensity = 2 },
                SpaceType.ConferenceRoom => new SpaceStandards { MinAreaPerPerson = 2.5, MaxOccupancyDensity = 2 },
                SpaceType.Lobby => new SpaceStandards { MinAreaPerPerson = 1, MaxOccupancyDensity = 2 },
                SpaceType.Cafeteria => new SpaceStandards { MinAreaPerPerson = 1.5, MaxOccupancyDensity = 1.5 },
                _ => new SpaceStandards { MinAreaPerPerson = 5, MaxOccupancyDensity = 5 }
            };
        }

        private SpaceType MapRoomToSpaceType(BIMRoom room)
        {
            var name = room.Name?.ToLower() ?? "";
            if (name.Contains("office")) return SpaceType.Office;
            if (name.Contains("meeting")) return SpaceType.MeetingRoom;
            if (name.Contains("conference")) return SpaceType.ConferenceRoom;
            if (name.Contains("lobby")) return SpaceType.Lobby;
            if (name.Contains("cafeteria") || name.Contains("break")) return SpaceType.Cafeteria;
            if (name.Contains("rest") || name.Contains("toilet")) return SpaceType.Restroom;
            if (name.Contains("storage")) return SpaceType.Storage;
            if (name.Contains("server") || name.Contains("data")) return SpaceType.DataCenter;
            return SpaceType.Other;
        }

        private decimal CalculatePotentialSavings(Space space, UtilizationAnalysis analysis)
        {
            var unusedCapacity = 1 - (analysis.AverageUtilization / 100);
            return (decimal)space.Area * (decimal)unusedCapacity * _settings.CostPerSquareMeter;
        }

        private List<string> GenerateRecommendations(Space space, UtilizationAnalysis analysis)
        {
            var recommendations = new List<string>();

            if (analysis.AverageUtilization < 20)
                recommendations.Add("Consider consolidating this space with adjacent areas");
            if (analysis.AverageUtilization < 40)
                recommendations.Add("Evaluate for hot-desking or shared space arrangement");
            if (analysis.PeakUtilization < 50)
                recommendations.Add("Space may be suitable for downsizing");

            return recommendations;
        }

        private List<MoveTask> GenerateMoveTasks(MovePlan plan)
        {
            return new List<MoveTask>
            {
                new() { Name = "IT Infrastructure Setup", DaysBeforeMove = 7, Category = "IT" },
                new() { Name = "Furniture Delivery", DaysBeforeMove = 2, Category = "Facilities" },
                new() { Name = "Signage Update", DaysBeforeMove = 1, Category = "Facilities" },
                new() { Name = "Access Card Programming", DaysBeforeMove = 1, Category = "Security" },
                new() { Name = "Physical Move", DaysBeforeMove = 0, Category = "Move" },
                new() { Name = "IT Verification", DaysBeforeMove = 0, Category = "IT" }
            };
        }

        private decimal EstimateMoveCost(MovePlan plan)
        {
            var baseCost = 500m; // Base move cost
            var perPersonCost = 100m;
            var distanceFactor = plan.SourceSpace.Building != plan.TargetSpace.Building ? 1.5m : 1m;

            return (baseCost + plan.Occupants.Count * perPersonCost) * distanceFactor;
        }

        #endregion
    }

    #region Supporting Classes

    internal class SpaceRepository
    {
        private readonly List<Space> _spaces = new();
        private readonly List<SpaceAllocation> _allocations = new();
        private readonly List<MovePlan> _movePlans = new();

        public Task AddSpaceAsync(Space space, CancellationToken ct) { _spaces.Add(space); return Task.CompletedTask; }
        public Task UpdateSpaceAsync(Space space, CancellationToken ct)
        {
            var idx = _spaces.FindIndex(s => s.Id == space.Id);
            if (idx >= 0) _spaces[idx] = space;
            return Task.CompletedTask;
        }
        public Task<Space> GetSpaceAsync(string id, CancellationToken ct) => Task.FromResult(_spaces.FirstOrDefault(s => s.Id == id));
        public Task<List<Space>> GetAllSpacesAsync(CancellationToken ct) => Task.FromResult(_spaces.ToList());
        public Task<List<Space>> GetSpacesByBuildingAsync(string building, CancellationToken ct) =>
            Task.FromResult(_spaces.Where(s => s.Building == building).ToList());
        public Task AddAllocationAsync(SpaceAllocation alloc, CancellationToken ct) { _allocations.Add(alloc); return Task.CompletedTask; }
        public Task SaveMovePlanAsync(MovePlan plan, CancellationToken ct) { _movePlans.Add(plan); return Task.CompletedTask; }
        public Task<MovePlan> GetMovePlanAsync(string id, CancellationToken ct) => Task.FromResult(_movePlans.FirstOrDefault(m => m.Id == id));
        public Task UpdateMovePlanAsync(MovePlan plan, CancellationToken ct)
        {
            var idx = _movePlans.FindIndex(m => m.Id == plan.Id);
            if (idx >= 0) _movePlans[idx] = plan;
            return Task.CompletedTask;
        }
    }

    internal class OccupancyTracker
    {
        private readonly SpaceSettings _settings;
        private readonly List<OccupancyRecord> _records = new();

        public OccupancyTracker(SpaceSettings settings) => _settings = settings;

        public Task RecordAsync(OccupancyRecord record, CancellationToken ct) { _records.Add(record); return Task.CompletedTask; }
        public Task<List<OccupancyRecord>> GetHistoryAsync(string spaceId, DateTime from, DateTime to, CancellationToken ct) =>
            Task.FromResult(_records.Where(r => r.SpaceId == spaceId && r.Timestamp >= from && r.Timestamp <= to).ToList());
    }

    internal class UtilizationAnalyzer
    {
        public UtilizationAnalysis Analyze(Space space, List<OccupancyRecord> history, DateTime from, DateTime to)
        {
            var analysis = new UtilizationAnalysis
            {
                SpaceId = space.Id,
                SpaceName = space.Name,
                Capacity = space.Capacity,
                AnalysisPeriod = $"{from:d} - {to:d}"
            };

            if (history.Any())
            {
                analysis.AverageUtilization = history.Average(r => r.UtilizationPercentage);
                analysis.PeakUtilization = history.Max(r => r.UtilizationPercentage);
                analysis.MinUtilization = history.Min(r => r.UtilizationPercentage);
                analysis.AverageOccupancy = history.Average(r => r.OccupantCount);
            }

            return analysis;
        }
    }

    internal class AllocationOptimizer
    {
        private readonly SpaceSettings _settings;
        public AllocationOptimizer(SpaceSettings settings) => _settings = settings;

        public AllocationOptimizationResult Optimize(OptimizationRequest request, List<Space> availableSpaces)
        {
            var result = new AllocationOptimizationResult();

            // Simple bin-packing algorithm
            var remaining = request.TotalHeadcount;
            foreach (var space in availableSpaces.OrderByDescending(s => s.Capacity))
            {
                if (remaining <= 0) break;
                var allocate = Math.Min(space.Capacity, remaining);
                result.ProposedAllocations.Add(new ProposedAllocation
                {
                    Space = space,
                    HeadcountAllocated = allocate
                });
                remaining -= allocate;
            }

            result.TotalCapacityAllocated = result.ProposedAllocations.Sum(a => a.HeadcountAllocated);
            result.CanAccommodate = remaining <= 0;

            return result;
        }
    }

    #endregion

    #region Data Models

    public class SpaceSettings
    {
        public decimal CostPerSquareMeter { get; set; } = 500;
        public double UnderutilizationThreshold { get; set; } = 30;
        public double OverutilizationThreshold { get; set; } = 90;
    }

    public class Space
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Number { get; set; }
        public SpaceType Type { get; set; }
        public string Building { get; set; }
        public string Floor { get; set; }
        public string Department { get; set; }
        public string CostCenter { get; set; }
        public double Area { get; set; }
        public int Capacity { get; set; }
        public int CurrentOccupancy { get; set; }
        public DateTime? LastOccupancyUpdate { get; set; }
        public string BIMRoomId { get; set; }
        public SpaceBoundaries Boundaries { get; set; }
        public SpaceStatus Status { get; set; }
        public SpaceStandards StandardsCompliance { get; set; }
        public SpaceAllocation CurrentAllocation { get; set; }
        public List<string> Amenities { get; set; } = new();
        public DateTime CreatedDate { get; set; }
    }

    public class SpaceRegistration
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Number { get; set; }
        public SpaceType Type { get; set; }
        public string Building { get; set; }
        public string Floor { get; set; }
        public string Department { get; set; }
        public string CostCenter { get; set; }
        public double Area { get; set; }
        public int Capacity { get; set; }
        public string BIMRoomId { get; set; }
        public SpaceBoundaries Boundaries { get; set; }
    }

    public class SpaceBoundaries
    {
        public List<Point2D> Points { get; set; } = new();
    }

    public class Point2D { public double X { get; set; } public double Y { get; set; } }

    public class SpaceStandards
    {
        public double MinAreaPerPerson { get; set; }
        public double MaxOccupancyDensity { get; set; }
    }

    public class SpaceRegistrationResult { public bool Success { get; set; } public Space Space { get; set; } public string SpaceId { get; set; } }
    public class BulkSpaceRegistrationResult { public int TotalProcessed { get; set; } public List<Space> RegisteredSpaces { get; } = new(); }

    public class SpaceProgress { public string Phase { get; set; } public int PercentComplete { get; set; } public string CurrentSpace { get; set; } }

    public class OccupancyRecord
    {
        public string SpaceId { get; set; }
        public DateTime Timestamp { get; set; }
        public int OccupantCount { get; set; }
        public double UtilizationPercentage { get; set; }
        public OccupancySource Source { get; set; }
    }

    public class SpaceOccupancy { public string SpaceId { get; set; } public string SpaceName { get; set; } public int Capacity { get; set; } public int CurrentOccupancy { get; set; } public double UtilizationPercentage { get; set; } public DateTime LastUpdated { get; set; } }
    public class BuildingOccupancy { public string Building { get; set; } public DateTime Timestamp { get; set; } public int TotalSpaces { get; set; } public int TotalCapacity { get; set; } public int TotalOccupancy { get; set; } public double OverallUtilization { get; set; } public List<FloorOccupancy> ByFloor { get; set; } public List<SpaceTypeOccupancy> ByType { get; set; } }
    public class FloorOccupancy { public string Floor { get; set; } public int Capacity { get; set; } public int Occupancy { get; set; } public int SpaceCount { get; set; } }
    public class SpaceTypeOccupancy { public SpaceType Type { get; set; } public int Capacity { get; set; } public int Occupancy { get; set; } public int SpaceCount { get; set; } }

    public class UtilizationAnalysis { public string SpaceId { get; set; } public string SpaceName { get; set; } public int Capacity { get; set; } public string AnalysisPeriod { get; set; } public double AverageUtilization { get; set; } public double PeakUtilization { get; set; } public double MinUtilization { get; set; } public double AverageOccupancy { get; set; } }
    public class PortfolioUtilization { public string AnalysisPeriod { get; set; } public int TotalSpaces { get; set; } public double TotalArea { get; set; } public double AverageUtilization { get; set; } public List<UtilizationAnalysis> SpaceAnalyses { get; } = new(); public List<string> UnderutilizedSpaces { get; set; } public List<string> OverutilizedSpaces { get; set; } public List<BuildingUtilizationSummary> ByBuilding { get; set; } }
    public class BuildingUtilizationSummary { public string Building { get; set; } public double AverageUtilization { get; set; } public int SpaceCount { get; set; } }
    public class UnderutilizedSpace { public Space Space { get; set; } public UtilizationAnalysis Analysis { get; set; } public decimal PotentialSavings { get; set; } public List<string> Recommendations { get; set; } }

    public class SpaceAllocation { public string Id { get; set; } public string SpaceId { get; set; } public string AllocatedTo { get; set; } public AllocationType AllocationType { get; set; } public string Department { get; set; } public string CostCenter { get; set; } public DateTime StartDate { get; set; } public DateTime? EndDate { get; set; } public string Purpose { get; set; } public string ApprovedBy { get; set; } public string ReleaseReason { get; set; } public DateTime CreatedDate { get; set; } }
    public class AllocationRequest { public string SpaceId { get; set; } public string AllocatedTo { get; set; } public AllocationType AllocationType { get; set; } public string Department { get; set; } public string CostCenter { get; set; } public DateTime StartDate { get; set; } public DateTime? EndDate { get; set; } public string Purpose { get; set; } public string ApprovedBy { get; set; } }
    public class AllocationResult { public bool Success { get; set; } public SpaceAllocation Allocation { get; set; } public string Error { get; set; } }
    public class SpaceRequirements { public string Building { get; set; } public string Floor { get; set; } public double? MinArea { get; set; } public int? MinCapacity { get; set; } public List<SpaceType> SpaceTypes { get; set; } public List<string> RequiredAmenities { get; set; } }
    public class OptimizationRequest { public string Department { get; set; } public int TotalHeadcount { get; set; } public string PreferredBuilding { get; set; } }
    public class AllocationOptimizationResult { public List<ProposedAllocation> ProposedAllocations { get; } = new(); public int TotalCapacityAllocated { get; set; } public bool CanAccommodate { get; set; } }
    public class ProposedAllocation { public Space Space { get; set; } public int HeadcountAllocated { get; set; } }

    public class MoveRequest { public string SourceSpaceId { get; set; } public string TargetSpaceId { get; set; } public string Department { get; set; } public DateTime PreferredDate { get; set; } public List<string> Occupants { get; set; } }
    public class MovePlan { public string Id { get; set; } public Space SourceSpace { get; set; } public Space TargetSpace { get; set; } public string Department { get; set; } public DateTime MoveDate { get; set; } public List<string> Occupants { get; set; } public MoveStatus Status { get; set; } public List<MoveTask> Tasks { get; set; } public decimal EstimatedCost { get; set; } public DateTime CreatedDate { get; set; } public DateTime? CompletedDate { get; set; } }
    public class MoveTask { public string Name { get; set; } public int DaysBeforeMove { get; set; } public string Category { get; set; } }

    public class SpaceReport { public DateTime GeneratedDate { get; set; } public SpaceReportType ReportType { get; set; } public SpaceReportSummary Summary { get; set; } public List<BuildingSpaceSummary> ByBuilding { get; set; } public List<SpaceTypeSummary> ByType { get; set; } public List<DepartmentSpaceSummary> ByDepartment { get; set; } }
    public class SpaceReportSummary { public int TotalSpaces { get; set; } public double TotalArea { get; set; } public int TotalCapacity { get; set; } public int AllocatedSpaces { get; set; } public int AvailableSpaces { get; set; } public double AllocatedArea { get; set; } }
    public class SpaceReportOptions { public SpaceReportType ReportType { get; set; } public string Building { get; set; } }
    public class BuildingSpaceSummary { public string Building { get; set; } public int SpaceCount { get; set; } public double TotalArea { get; set; } public double AllocatedArea { get; set; } }
    public class SpaceTypeSummary { public SpaceType Type { get; set; } public int Count { get; set; } public double TotalArea { get; set; } }
    public class DepartmentSpaceSummary { public string Department { get; set; } public int SpaceCount { get; set; } public double TotalArea { get; set; } public int Headcount { get; set; } }
    public class CostAllocationReport { public string FiscalYear { get; set; } public DateTime GeneratedDate { get; set; } public double TotalArea { get; set; } public decimal TotalAllocatedCost { get; set; } public List<DepartmentCostAllocation> ByDepartment { get; set; } }
    public class DepartmentCostAllocation { public string Department { get; set; } public string CostCenter { get; set; } public double AllocatedArea { get; set; } public decimal AllocatedCost { get; set; } public double PercentageOfTotal { get; set; } }

    public class BIMRoom { public string Id { get; set; } public string Name { get; set; } public string Number { get; set; } public string Building { get; set; } public string Level { get; set; } public double Area { get; set; } public SpaceBoundaries Boundaries { get; set; } }

    public enum SpaceType { Office, OpenOffice, MeetingRoom, ConferenceRoom, Lobby, Cafeteria, Restroom, Storage, DataCenter, Lab, Training, Circulation, Other }
    public enum SpaceStatus { Active, Available, Allocated, UnderRenovation, Decommissioned }
    public enum OccupancySource { Manual, Sensor, Schedule, Badge }
    public enum AllocationType { Permanent, Temporary, HotDesk, SharedDesk }
    public enum MoveStatus { Planned, InProgress, Completed, Cancelled }
    public enum SpaceReportType { Inventory, Utilization, Allocation, Cost }

    #endregion
}
