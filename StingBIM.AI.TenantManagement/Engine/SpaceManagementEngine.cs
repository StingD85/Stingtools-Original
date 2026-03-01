// ============================================================================
// StingBIM AI - Space Management Engine
// Space allocation, optimization, stacking plans, utilization analysis,
// BOMA 2017 / RICS area calculations, fit testing, hot-desking analysis,
// tenant move planning, and Revit room data integration.
// Copyright (c) 2026 StingBIM. All rights reserved.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.TenantManagement.Models;

namespace StingBIM.AI.TenantManagement.Engine
{
    /// <summary>
    /// Space management engine providing AI-powered space allocation, utilization tracking,
    /// stacking plan generation, BOMA/RICS area measurement, fit testing, and move planning
    /// with integration to Revit room/area data.
    /// </summary>
    public class SpaceManagementEngine
    {
        #region Fields

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        private readonly Dictionary<string, Space> _spaces;
        private readonly Dictionary<string, Tenant> _tenants;
        private readonly Dictionary<string, Lease> _leases;
        private readonly Dictionary<string, List<SpaceUtilization>> _utilizationHistory;
        private readonly Dictionary<string, MoveRequest> _moveRequests;

        // BOMA 2017 standard constants
        private const double DefaultCommonAreaFactor_Office = 1.15;
        private const double DefaultCommonAreaFactor_Retail = 1.10;
        private const double DefaultCommonAreaFactor_Industrial = 1.05;

        // Space planning standards (sqm per person)
        private const double OfficeAreaPerPerson = 8.0;     // Open plan
        private const double PrivateOfficePerPerson = 14.0;  // Private office
        private const double MeetingRoomPerPerson = 2.5;     // Meeting room
        private const double ReceptionPerPerson = 5.0;       // Reception
        private const double HotDeskAreaPerPerson = 5.5;     // Hot desk

        #endregion

        #region Constructor

        public SpaceManagementEngine()
        {
            _spaces = new Dictionary<string, Space>(StringComparer.OrdinalIgnoreCase);
            _tenants = new Dictionary<string, Tenant>(StringComparer.OrdinalIgnoreCase);
            _leases = new Dictionary<string, Lease>(StringComparer.OrdinalIgnoreCase);
            _utilizationHistory = new Dictionary<string, List<SpaceUtilization>>(StringComparer.OrdinalIgnoreCase);
            _moveRequests = new Dictionary<string, MoveRequest>(StringComparer.OrdinalIgnoreCase);

            Logger.Info("SpaceManagementEngine initialized.");
        }

        #endregion

        #region Data Registration

        /// <summary>
        /// Registers or updates a space in the management system.
        /// </summary>
        public void RegisterSpace(Space space)
        {
            if (space == null) throw new ArgumentNullException(nameof(space));
            if (string.IsNullOrWhiteSpace(space.SpaceId))
                throw new ArgumentException("Space ID is required.", nameof(space));

            lock (_lockObject)
            {
                _spaces[space.SpaceId] = space;
                if (!_utilizationHistory.ContainsKey(space.SpaceId))
                    _utilizationHistory[space.SpaceId] = new List<SpaceUtilization>();
            }
            Logger.Debug("Space registered: {SpaceId} ({Type}, {Area}sqm, Floor {Floor})",
                space.SpaceId, space.SpaceType, space.Area_sqm, space.FloorLevel);
        }

        /// <summary>
        /// Registers a tenant for space matching operations.
        /// </summary>
        public void RegisterTenant(Tenant tenant)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            lock (_lockObject)
            {
                _tenants[tenant.TenantId] = tenant;
            }
        }

        /// <summary>
        /// Registers a lease for space-lease relationship tracking.
        /// </summary>
        public void RegisterLease(Lease lease)
        {
            if (lease == null) throw new ArgumentNullException(nameof(lease));
            lock (_lockObject)
            {
                _leases[lease.LeaseId] = lease;
            }
        }

        /// <summary>
        /// Records a utilization measurement for a space.
        /// </summary>
        public void RecordUtilization(SpaceUtilization utilization)
        {
            if (utilization == null) throw new ArgumentNullException(nameof(utilization));

            lock (_lockObject)
            {
                if (!_utilizationHistory.ContainsKey(utilization.SpaceId))
                    _utilizationHistory[utilization.SpaceId] = new List<SpaceUtilization>();

                _utilizationHistory[utilization.SpaceId].Add(utilization);
            }
            Logger.Debug("Utilization recorded for space {SpaceId}: {Pct}%",
                utilization.SpaceId, utilization.UtilizationPct);
        }

        #endregion

        #region Space Allocation

        /// <summary>
        /// AI-powered space matching that scores and ranks available spaces
        /// against a tenant's requirements including area, type, floor preference,
        /// budget, headcount, and adjacency preferences.
        /// </summary>
        public async Task<List<(Space space, double score, string reasoning)>> AllocateSpaceAsync(
            string tenantId,
            SpaceRequirements requirements,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Analyzing available spaces against requirements...");

                var results = new List<(Space space, double score, string reasoning)>();

                lock (_lockObject)
                {
                    var availableSpaces = _spaces.Values
                        .Where(s => s.OccupancyStatus == OccupancyStatus.Vacant
                            || s.OccupancyStatus == OccupancyStatus.UnderRefurbishment)
                        .ToList();

                    foreach (var space in availableSpaces)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        double score = 0;
                        var reasons = new List<string>();

                        // Area scoring (0-30 points)
                        double areaScore = ScoreArea(space, requirements);
                        score += areaScore;
                        if (areaScore > 20) reasons.Add("Excellent area match");
                        else if (areaScore > 10) reasons.Add("Acceptable area");

                        // Space type scoring (0-20 points)
                        if (space.SpaceType == requirements.PreferredType)
                        {
                            score += 20;
                            reasons.Add($"Matches preferred type ({requirements.PreferredType})");
                        }
                        else if (IsCompatibleType(space.SpaceType, requirements.PreferredType))
                        {
                            score += 10;
                            reasons.Add($"Compatible type ({space.SpaceType})");
                        }

                        // Floor preference scoring (0-15 points)
                        if (requirements.PreferredFloors.Count > 0)
                        {
                            if (requirements.PreferredFloors.Contains(space.FloorLevel))
                            {
                                score += 15;
                                reasons.Add($"Preferred floor ({space.FloorLevel})");
                            }
                            else
                            {
                                var minDistance = requirements.PreferredFloors
                                    .Min(f => Math.Abs(f - space.FloorLevel));
                                score += Math.Max(0, 15 - (minDistance * 3));
                            }
                        }
                        else
                        {
                            score += 10; // No preference, neutral score
                        }

                        // Headcount capacity scoring (0-15 points)
                        if (requirements.RequiredHeadcount > 0)
                        {
                            var capacity = EstimateCapacity(space);
                            if (capacity >= requirements.RequiredHeadcount)
                            {
                                score += 15;
                                reasons.Add($"Fits {requirements.RequiredHeadcount} people (capacity: {capacity})");
                            }
                            else
                            {
                                var ratio = (double)capacity / requirements.RequiredHeadcount;
                                score += ratio * 15;
                                reasons.Add($"Limited capacity: {capacity}/{requirements.RequiredHeadcount}");
                            }
                        }
                        else
                        {
                            score += 10;
                        }

                        // Budget scoring (0-10 points)
                        if (requirements.MaxBudgetPerSqm > 0 && space.MarketRentPerSqm > 0)
                        {
                            if (space.MarketRentPerSqm <= requirements.MaxBudgetPerSqm)
                            {
                                score += 10;
                                reasons.Add("Within budget");
                            }
                            else
                            {
                                var overBudgetPct = (double)((space.MarketRentPerSqm - requirements.MaxBudgetPerSqm)
                                    / requirements.MaxBudgetPerSqm) * 100;
                                score += Math.Max(0, 10 - overBudgetPct);
                                reasons.Add($"Over budget by {overBudgetPct:F0}%");
                            }
                        }
                        else
                        {
                            score += 5;
                        }

                        // Adjacency preference scoring (0-10 points)
                        if (requirements.AdjacentTenantPreferences.Count > 0)
                        {
                            var adjacencyScore = ScoreAdjacency(space, requirements.AdjacentTenantPreferences);
                            score += adjacencyScore;
                            if (adjacencyScore > 5) reasons.Add("Good adjacency match");
                        }
                        else
                        {
                            score += 5;
                        }

                        // Only include spaces meeting minimum threshold
                        if (score >= 30)
                        {
                            results.Add((space, Math.Round(score, 1), string.Join("; ", reasons)));
                        }
                    }
                }

                // Sort by score descending
                results = results.OrderByDescending(r => r.score).ToList();

                progress?.Report($"Found {results.Count} matching spaces.");
                Logger.Info("Space allocation for tenant {TenantId}: {Count} matching spaces found.",
                    tenantId, results.Count);

                return results;
            }, cancellationToken);
        }

        /// <summary>
        /// Scores a space against area requirements.
        /// </summary>
        private double ScoreArea(Space space, SpaceRequirements requirements)
        {
            var area = space.UsableArea_sqm;
            if (area < requirements.MinArea_sqm) return 0;
            if (area > requirements.MaxArea_sqm * 1.5) return 5; // Too large penalty

            if (area >= requirements.MinArea_sqm && area <= requirements.MaxArea_sqm)
            {
                // Ideal range
                var midpoint = (requirements.MinArea_sqm + requirements.MaxArea_sqm) / 2.0;
                var deviation = Math.Abs(area - midpoint) / midpoint;
                return 30 * (1 - deviation * 0.5);
            }

            // Slightly over max
            var overRatio = (area - requirements.MaxArea_sqm) / requirements.MaxArea_sqm;
            return Math.Max(10, 25 - (overRatio * 30));
        }

        /// <summary>
        /// Checks if two space types are compatible for tenant use.
        /// </summary>
        private bool IsCompatibleType(SpaceType actual, SpaceType preferred)
        {
            return (actual, preferred) switch
            {
                (SpaceType.Office, SpaceType.MeetingRoom) => true,
                (SpaceType.MeetingRoom, SpaceType.Office) => true,
                (SpaceType.Retail, SpaceType.FoodCourt) => true,
                (SpaceType.FoodCourt, SpaceType.Retail) => true,
                (SpaceType.Storage, SpaceType.Industrial) => true,
                _ => false
            };
        }

        /// <summary>
        /// Estimates headcount capacity for a space based on type and area.
        /// </summary>
        private int EstimateCapacity(Space space)
        {
            var areaPerPerson = space.SpaceType switch
            {
                SpaceType.Office => OfficeAreaPerPerson,
                SpaceType.MeetingRoom => MeetingRoomPerPerson,
                SpaceType.Reception => ReceptionPerPerson,
                SpaceType.Retail => 12.0,
                SpaceType.Industrial => 20.0,
                _ => 10.0
            };

            return space.MaxOccupancy > 0
                ? space.MaxOccupancy
                : (int)(space.UsableArea_sqm / areaPerPerson);
        }

        /// <summary>
        /// Scores adjacency preferences based on tenant locations on the same floor.
        /// </summary>
        private double ScoreAdjacency(Space candidateSpace, List<string> preferredTenantIds)
        {
            double score = 0;
            int matchCount = 0;

            foreach (var tenantId in preferredTenantIds)
            {
                // Check if any of the preferred tenants are on the same floor
                var onSameFloor = _spaces.Values.Any(s =>
                    s.CurrentTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
                    && s.FloorLevel == candidateSpace.FloorLevel);

                if (onSameFloor)
                {
                    matchCount++;
                }
            }

            if (preferredTenantIds.Count > 0)
                score = ((double)matchCount / preferredTenantIds.Count) * 10;

            return score;
        }

        #endregion

        #region Utilization Analysis

        /// <summary>
        /// Calculates space utilization metrics for a given space over a date range.
        /// </summary>
        public SpaceUtilization CalculateUtilization(string spaceId, DateTime startDate, DateTime endDate)
        {
            lock (_lockObject)
            {
                if (!_spaces.TryGetValue(spaceId, out var space))
                    throw new InvalidOperationException($"Space '{spaceId}' not found.");

                var records = _utilizationHistory.TryGetValue(spaceId, out var history)
                    ? history.Where(u => u.Date >= startDate && u.Date <= endDate).ToList()
                    : new List<SpaceUtilization>();

                var result = new SpaceUtilization
                {
                    SpaceId = spaceId,
                    SpaceName = space.Name,
                    PeriodStart = startDate,
                    PeriodEnd = endDate,
                    Date = DateTime.UtcNow
                };

                if (records.Count == 0)
                {
                    Logger.Warn("No utilization data for space {SpaceId} in range {Start} to {End}.",
                        spaceId, startDate.ToShortDateString(), endDate.ToShortDateString());
                    return result;
                }

                result.OccupancyHours = records.Sum(r => r.OccupancyHours);
                result.AvailableHours = records.Sum(r => r.AvailableHours);
                result.PeakOccupancy = records.Max(r => r.PeakOccupancy);
                result.AvgOccupancy = records.Average(r => r.AvgOccupancy);
                result.UtilizationPct = result.AvailableHours > 0
                    ? result.OccupancyHours / result.AvailableHours * 100.0
                    : 0;
                result.TotalDesks = records.FirstOrDefault()?.TotalDesks ?? 0;
                result.OccupiedDesks = (int)records.Average(r => r.OccupiedDesks);
                result.DeskUtilizationPct = result.TotalDesks > 0
                    ? (double)result.OccupiedDesks / result.TotalDesks * 100.0
                    : 0;

                // Build hourly distribution
                var allHourly = records.SelectMany(r => r.HourlyDistribution);
                var hourlyGroups = allHourly.GroupBy(kv => kv.Key);
                foreach (var group in hourlyGroups)
                {
                    result.HourlyDistribution[group.Key] = group.Average(kv => kv.Value);
                }

                Logger.Debug("Utilization for {SpaceId}: {Pct:F1}% space, {DeskPct:F1}% desk utilization.",
                    spaceId, result.UtilizationPct, result.DeskUtilizationPct);

                return result;
            }
        }

        #endregion

        #region Stacking Plan

        /// <summary>
        /// Generates a visual stacking plan for a building showing floor-by-floor
        /// tenant allocation, vacancy, and key lease metrics.
        /// </summary>
        public async Task<StackingPlan> GenerateStackingPlanAsync(
            string buildingId,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Generating stacking plan...");

                var plan = new StackingPlan
                {
                    BuildingId = buildingId,
                    GeneratedDate = DateTime.UtcNow
                };

                lock (_lockObject)
                {
                    var buildingSpaces = _spaces.Values
                        .Where(s => s.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (buildingSpaces.Count == 0)
                    {
                        Logger.Warn("No spaces found for building {BuildingId}.", buildingId);
                        return plan;
                    }

                    plan.BuildingName = buildingId;

                    // Group by floor
                    var floorGroups = buildingSpaces
                        .GroupBy(s => s.FloorLevel)
                        .OrderByDescending(g => g.Key);

                    var uniqueTenants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var floorGroup in floorGroups)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var floor = new StackingFloor
                        {
                            FloorLevel = floorGroup.Key,
                            FloorName = floorGroup.First().FloorName.Length > 0
                                ? floorGroup.First().FloorName
                                : $"Level {floorGroup.Key}",
                            TotalArea_sqm = floorGroup.Sum(s => s.RentableArea_sqm)
                        };

                        foreach (var space in floorGroup.OrderBy(s => s.Name))
                        {
                            var stackingSpace = new StackingSpace
                            {
                                SpaceId = space.SpaceId,
                                SpaceName = space.Name,
                                SpaceType = space.SpaceType,
                                Area_sqm = space.RentableArea_sqm,
                                OccupancyStatus = space.OccupancyStatus
                            };

                            if (space.OccupancyStatus == OccupancyStatus.Occupied
                                && !string.IsNullOrEmpty(space.CurrentLeaseId))
                            {
                                stackingSpace.TenantId = space.CurrentTenantId;
                                stackingSpace.LeaseId = space.CurrentLeaseId;

                                if (_tenants.TryGetValue(space.CurrentTenantId, out var tenant))
                                {
                                    stackingSpace.TenantName = tenant.Name;
                                    uniqueTenants.Add(tenant.TenantId);
                                }

                                if (_leases.TryGetValue(space.CurrentLeaseId, out var lease))
                                {
                                    stackingSpace.LeaseExpiry = lease.EndDate;
                                    stackingSpace.MonthlyRent = lease.MonthlyRent;
                                    stackingSpace.RentPerSqm = space.RentableArea_sqm > 0
                                        ? lease.MonthlyRent / (decimal)space.RentableArea_sqm
                                        : 0;
                                }

                                floor.LeasedArea_sqm += space.RentableArea_sqm;
                            }
                            else
                            {
                                floor.VacantArea_sqm += space.RentableArea_sqm;
                            }

                            floor.Spaces.Add(stackingSpace);
                        }

                        plan.Floors.Add(floor);
                    }

                    // Aggregate building totals
                    plan.TotalBuildingArea_sqm = plan.Floors.Sum(f => f.TotalArea_sqm);
                    plan.TotalLeasedArea_sqm = plan.Floors.Sum(f => f.LeasedArea_sqm);
                    plan.TotalVacantArea_sqm = plan.Floors.Sum(f => f.VacantArea_sqm);
                    plan.OccupancyRate = plan.TotalBuildingArea_sqm > 0
                        ? plan.TotalLeasedArea_sqm / plan.TotalBuildingArea_sqm * 100.0
                        : 0;
                    plan.TotalTenants = uniqueTenants.Count;

                    // Calculate total annual rent
                    plan.TotalAnnualRent = plan.Floors
                        .SelectMany(f => f.Spaces)
                        .Where(s => s.MonthlyRent > 0)
                        .Sum(s => s.MonthlyRent * 12);

                    plan.AverageRentPerSqm = plan.TotalLeasedArea_sqm > 0
                        ? plan.TotalAnnualRent / (decimal)plan.TotalLeasedArea_sqm / 12
                        : 0;
                }

                progress?.Report($"Stacking plan generated: {plan.Floors.Count} floors, {plan.TotalTenants} tenants.");
                Logger.Info("Stacking plan for {Building}: {Floors} floors, {Occupancy:F1}% occupied, {Tenants} tenants.",
                    buildingId, plan.Floors.Count, plan.OccupancyRate, plan.TotalTenants);

                return plan;
            }, cancellationToken);
        }

        #endregion

        #region Space Optimization

        /// <summary>
        /// Optimizes space allocation across a building to minimize vacancy,
        /// maximize adjacency satisfaction, and improve floor utilization balance.
        /// </summary>
        public async Task<List<(string tenantId, string fromSpaceId, string toSpaceId, string reason)>>
            OptimizeSpaceAllocationAsync(
                string buildingId,
                CancellationToken cancellationToken = default,
                IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Analyzing space allocation for optimization...");

                var recommendations = new List<(string tenantId, string fromSpaceId, string toSpaceId, string reason)>();

                lock (_lockObject)
                {
                    var buildingSpaces = _spaces.Values
                        .Where(s => s.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // Strategy 1: Consolidate partially vacant floors
                    var floorGroups = buildingSpaces.GroupBy(s => s.FloorLevel);
                    var partiallyVacantFloors = floorGroups
                        .Where(g =>
                        {
                            var total = g.Sum(s => s.RentableArea_sqm);
                            var vacant = g.Where(s => s.OccupancyStatus == OccupancyStatus.Vacant)
                                .Sum(s => s.RentableArea_sqm);
                            var vacancyPct = total > 0 ? vacant / total : 0;
                            return vacancyPct > 0.3 && vacancyPct < 0.8;
                        })
                        .OrderByDescending(g => g.Where(s => s.OccupancyStatus == OccupancyStatus.Vacant)
                            .Sum(s => s.RentableArea_sqm))
                        .ToList();

                    foreach (var floor in partiallyVacantFloors)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var occupiedSpaces = floor
                            .Where(s => s.OccupancyStatus == OccupancyStatus.Occupied)
                            .ToList();

                        // Try to find spaces on more utilized floors that could absorb these tenants
                        foreach (var occupiedSpace in occupiedSpaces)
                        {
                            var betterFloors = floorGroups
                                .Where(g => g.Key != floor.Key)
                                .Where(g =>
                                {
                                    var floorOccupied = g.Where(s => s.OccupancyStatus == OccupancyStatus.Occupied)
                                        .Sum(s => s.RentableArea_sqm);
                                    var floorTotal = g.Sum(s => s.RentableArea_sqm);
                                    return floorTotal > 0 && floorOccupied / floorTotal > 0.6;
                                })
                                .SelectMany(g => g)
                                .Where(s => s.OccupancyStatus == OccupancyStatus.Vacant
                                    && s.SpaceType == occupiedSpace.SpaceType
                                    && Math.Abs(s.UsableArea_sqm - occupiedSpace.UsableArea_sqm) / occupiedSpace.UsableArea_sqm < 0.2)
                                .ToList();

                            if (betterFloors.Count > 0)
                            {
                                var target = betterFloors.First();
                                recommendations.Add((
                                    occupiedSpace.CurrentTenantId,
                                    occupiedSpace.SpaceId,
                                    target.SpaceId,
                                    $"Consolidation: Free up Floor {floor.Key} by moving to more utilized Floor {target.FloorLevel}"
                                ));
                            }
                        }
                    }

                    // Strategy 2: Right-size under/over-utilized spaces
                    foreach (var space in buildingSpaces.Where(s => s.OccupancyStatus == OccupancyStatus.Occupied))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!_utilizationHistory.TryGetValue(space.SpaceId, out var history) || history.Count == 0)
                            continue;

                        var recentUtil = history
                            .Where(u => u.Date >= DateTime.UtcNow.AddMonths(-3))
                            .ToList();

                        if (recentUtil.Count == 0) continue;

                        var avgUtil = recentUtil.Average(u => u.UtilizationPct);

                        // Under-utilized: move to smaller space
                        if (avgUtil < 40)
                        {
                            var smallerSpaces = buildingSpaces
                                .Where(s => s.OccupancyStatus == OccupancyStatus.Vacant
                                    && s.SpaceType == space.SpaceType
                                    && s.UsableArea_sqm < space.UsableArea_sqm * 0.7
                                    && s.UsableArea_sqm >= space.UsableArea_sqm * 0.4)
                                .OrderByDescending(s => s.UsableArea_sqm)
                                .ToList();

                            if (smallerSpaces.Count > 0)
                            {
                                recommendations.Add((
                                    space.CurrentTenantId,
                                    space.SpaceId,
                                    smallerSpaces.First().SpaceId,
                                    $"Right-sizing: Current space {avgUtil:F0}% utilized, recommend smaller space"
                                ));
                            }
                        }

                        // Over-utilized: move to larger space
                        if (avgUtil > 90)
                        {
                            var largerSpaces = buildingSpaces
                                .Where(s => s.OccupancyStatus == OccupancyStatus.Vacant
                                    && s.SpaceType == space.SpaceType
                                    && s.UsableArea_sqm > space.UsableArea_sqm * 1.2
                                    && s.UsableArea_sqm <= space.UsableArea_sqm * 1.8)
                                .OrderBy(s => s.UsableArea_sqm)
                                .ToList();

                            if (largerSpaces.Count > 0)
                            {
                                recommendations.Add((
                                    space.CurrentTenantId,
                                    space.SpaceId,
                                    largerSpaces.First().SpaceId,
                                    $"Expansion: Current space {avgUtil:F0}% utilized, recommend larger space"
                                ));
                            }
                        }
                    }
                }

                progress?.Report($"Optimization complete: {recommendations.Count} recommendations.");
                Logger.Info("Space optimization for {Building}: {Count} recommendations generated.",
                    buildingId, recommendations.Count);

                return recommendations;
            }, cancellationToken);
        }

        #endregion

        #region Move Planning

        /// <summary>
        /// Plans a tenant move with phased execution, minimal disruption scheduling,
        /// cost estimation, and prerequisite tracking.
        /// </summary>
        public async Task<MoveRequest> PlanTenantMoveAsync(
            MoveRequest request,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Planning tenant move...");

                lock (_lockObject)
                {
                    // Validate request
                    if (!_tenants.ContainsKey(request.TenantId))
                        throw new InvalidOperationException($"Tenant '{request.TenantId}' not found.");

                    foreach (var spaceId in request.ToSpaceIds)
                    {
                        if (!_spaces.TryGetValue(spaceId, out var space))
                            throw new InvalidOperationException($"Target space '{spaceId}' not found.");
                        if (space.OccupancyStatus == OccupancyStatus.Occupied)
                            throw new InvalidOperationException($"Target space '{spaceId}' is already occupied.");
                    }

                    // Calculate estimated cost
                    var totalArea = request.FromSpaceIds
                        .Where(sid => _spaces.ContainsKey(sid))
                        .Sum(sid => _spaces[sid].Area_sqm);

                    // Cost model: base cost per sqm + per-space overhead
                    var baseCostPerSqm = 25.0m; // USD per sqm move cost
                    var perSpaceOverhead = 500.0m; // USD per space
                    request.EstimatedCost = (baseCostPerSqm * (decimal)totalArea)
                        + (perSpaceOverhead * (request.FromSpaceIds.Count + request.ToSpaceIds.Count));

                    // Estimate duration
                    request.EstimatedDurationDays = Math.Max(1, (int)(totalArea / 100) + request.FromSpaceIds.Count);

                    // Generate move phases
                    request.Phases.Clear();
                    var phaseStart = request.RequestedDate;

                    // Phase 1: Preparation
                    request.Phases.Add(new MovePhase
                    {
                        PhaseNumber = 1,
                        Description = "Pre-move preparation: IT infrastructure, furniture planning, access cards",
                        PlannedStart = phaseStart,
                        PlannedEnd = phaseStart.AddDays(Math.Max(2, request.EstimatedDurationDays / 3)),
                        AffectedSpaceIds = new List<string>(request.ToSpaceIds)
                    });

                    // Phase 2: Fit-out of destination (if needed)
                    var phase2Start = request.Phases.Last().PlannedEnd.AddDays(1);
                    var toSpacesNeedRefurb = request.ToSpaceIds
                        .Where(sid => _spaces.TryGetValue(sid, out var s)
                            && s.OccupancyStatus == OccupancyStatus.UnderRefurbishment)
                        .ToList();

                    if (toSpacesNeedRefurb.Count > 0)
                    {
                        request.Phases.Add(new MovePhase
                        {
                            PhaseNumber = 2,
                            Description = "Destination fit-out and refurbishment",
                            PlannedStart = phase2Start,
                            PlannedEnd = phase2Start.AddDays(14),
                            AffectedSpaceIds = toSpacesNeedRefurb
                        });
                        phase2Start = request.Phases.Last().PlannedEnd.AddDays(1);
                    }

                    // Phase 3: Physical move (schedule for weekend to minimize disruption)
                    var moveStart = phase2Start;
                    while (moveStart.DayOfWeek != DayOfWeek.Friday)
                        moveStart = moveStart.AddDays(1);

                    request.Phases.Add(new MovePhase
                    {
                        PhaseNumber = request.Phases.Count + 1,
                        Description = "Physical move execution (scheduled over weekend)",
                        PlannedStart = moveStart,
                        PlannedEnd = moveStart.AddDays(2),
                        AffectedSpaceIds = request.FromSpaceIds.Concat(request.ToSpaceIds).ToList()
                    });

                    // Phase 4: Post-move settling
                    var settleStart = request.Phases.Last().PlannedEnd.AddDays(1);
                    request.Phases.Add(new MovePhase
                    {
                        PhaseNumber = request.Phases.Count + 1,
                        Description = "Post-move: IT verification, snagging, old space decommission",
                        PlannedStart = settleStart,
                        PlannedEnd = settleStart.AddDays(3),
                        AffectedSpaceIds = new List<string>(request.FromSpaceIds)
                    });

                    request.ScheduledDate = request.Phases.First().PlannedStart;
                    request.Status = MoveRequestStatus.Approved;

                    // Add prerequisites
                    request.Prerequisites.Clear();
                    request.Prerequisites.Add("IT infrastructure readiness at destination");
                    request.Prerequisites.Add("Tenant notification (minimum 14 days prior)");
                    request.Prerequisites.Add("Building management approval");
                    request.Prerequisites.Add("Insurance notification for move period");
                    request.Prerequisites.Add("Key/access card provisioning");

                    // Store the request
                    _moveRequests[request.RequestId] = request;
                }

                progress?.Report($"Move plan ready: {request.Phases.Count} phases, est. cost {request.EstimatedCost:C}");
                Logger.Info("Move plan created: {RequestId} for tenant {TenantId}, {Phases} phases, cost {Cost}.",
                    request.RequestId, request.TenantId, request.Phases.Count, request.EstimatedCost);

                return request;
            }, cancellationToken);
        }

        #endregion

        #region Fit Test

        /// <summary>
        /// Tests whether a space can accommodate a given headcount and requirements,
        /// providing a detailed analysis of capacity, layout feasibility, and amenities.
        /// </summary>
        public async Task<(bool canFit, int capacity, string analysis)> FitTestAsync(
            string spaceId,
            int headcount,
            SpaceRequirements? requirements = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_lockObject)
                {
                    if (!_spaces.TryGetValue(spaceId, out var space))
                        throw new InvalidOperationException($"Space '{spaceId}' not found.");

                    var analysis = new List<string>();
                    var capacity = EstimateCapacity(space);
                    var canFit = capacity >= headcount;

                    analysis.Add($"Space: {space.Name} ({space.SpaceType})");
                    analysis.Add($"Usable area: {space.UsableArea_sqm:F1} sqm");
                    analysis.Add($"Estimated capacity: {capacity} persons");
                    analysis.Add($"Requested headcount: {headcount}");

                    if (canFit)
                    {
                        var utilization = (double)headcount / capacity * 100;
                        analysis.Add($"FIT: Space can accommodate {headcount} people ({utilization:F0}% of capacity)");

                        // Desk layout suggestions
                        var deskArea = space.UsableArea_sqm * 0.6; // 60% for desks
                        var openPlanDesks = (int)(deskArea / 5.0);
                        var privateOffices = (int)(deskArea / 12.0);

                        analysis.Add($"Suggested layout options:");
                        analysis.Add($"  Open plan: {openPlanDesks} workstations");
                        analysis.Add($"  Private offices: {privateOffices} offices");
                        analysis.Add($"  Hybrid (70/30): {(int)(openPlanDesks * 0.7)} open + {(int)(privateOffices * 0.3)} private");

                        // Amenity space
                        var meetingRoomArea = space.UsableArea_sqm * 0.15;
                        var breakoutArea = space.UsableArea_sqm * 0.10;
                        analysis.Add($"Amenity allocation:");
                        analysis.Add($"  Meeting rooms: {meetingRoomArea:F0} sqm ({(int)(meetingRoomArea / MeetingRoomPerPerson)} capacity)");
                        analysis.Add($"  Breakout space: {breakoutArea:F0} sqm");
                    }
                    else
                    {
                        var deficit = headcount - capacity;
                        var additionalArea = deficit * OfficeAreaPerPerson;
                        analysis.Add($"NO FIT: {deficit} persons over capacity");
                        analysis.Add($"Additional area needed: ~{additionalArea:F0} sqm");

                        // Check if subdividing adjacent vacant spaces could help
                        if (space.IsSubdivisible)
                        {
                            analysis.Add("Note: Space is subdivisible - consider combining with adjacent vacant space");
                        }
                    }

                    // Check specific requirements
                    if (requirements != null)
                    {
                        if (requirements.NeedsNaturalLight)
                        {
                            var hasWindows = space.Amenities.Contains("window", StringComparison.OrdinalIgnoreCase)
                                || space.Amenities.Contains("natural light", StringComparison.OrdinalIgnoreCase);
                            analysis.Add($"Natural light: {(hasWindows ? "Available" : "NOT confirmed - verify on site")}");
                        }
                        if (requirements.NeedsReception)
                        {
                            analysis.Add("Reception requirement: Allocate ~15 sqm near entrance");
                        }
                    }

                    Logger.Debug("Fit test for {SpaceId}: {CanFit}, capacity={Capacity}, requested={Headcount}",
                        spaceId, canFit, capacity, headcount);

                    return (canFit, capacity, string.Join("\n", analysis));
                }
            }, cancellationToken);
        }

        #endregion

        #region BOMA / RICS Area Calculations

        /// <summary>
        /// Calculates area breakdown per BOMA 2017 standard including GIA, NIA, NLA,
        /// common area, service area, and building efficiency ratio.
        /// </summary>
        public AreaBreakdown CalculateAreaBreakdown(string buildingId)
        {
            lock (_lockObject)
            {
                var buildingSpaces = _spaces.Values
                    .Where(s => s.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var breakdown = new AreaBreakdown
                {
                    BuildingId = buildingId,
                    Standard = "BOMA 2017 / RICS"
                };

                if (buildingSpaces.Count == 0)
                {
                    Logger.Warn("No spaces found for building {BuildingId}.", buildingId);
                    return breakdown;
                }

                // Calculate totals by space category
                foreach (var space in buildingSpaces)
                {
                    breakdown.GrossInternalArea_sqm += space.Area_sqm;

                    switch (space.SpaceType)
                    {
                        case SpaceType.Common:
                        case SpaceType.Reception:
                            breakdown.CommonArea_sqm += space.Area_sqm;
                            break;
                        case SpaceType.Parking:
                            breakdown.ParkingArea_sqm += space.Area_sqm;
                            break;
                        case SpaceType.ServerRoom:
                            breakdown.ServiceArea_sqm += space.Area_sqm;
                            break;
                        default:
                            // Leasable spaces
                            breakdown.NetInternalArea_sqm += space.UsableArea_sqm;
                            breakdown.NetLettableArea_sqm += space.RentableArea_sqm;
                            break;
                    }
                }

                // Estimate circulation (typically 15-20% of GIA)
                breakdown.CirculationArea_sqm = breakdown.GrossInternalArea_sqm * 0.15;

                // Floor-level breakdowns
                var floorGroups = buildingSpaces.GroupBy(s => s.FloorLevel).OrderBy(g => g.Key);
                foreach (var floor in floorGroups)
                {
                    var floorLettable = floor
                        .Where(s => s.SpaceType != SpaceType.Common
                            && s.SpaceType != SpaceType.Parking
                            && s.SpaceType != SpaceType.ServerRoom
                            && s.SpaceType != SpaceType.Reception)
                        .ToList();

                    var floorCommon = floor
                        .Where(s => s.SpaceType == SpaceType.Common || s.SpaceType == SpaceType.Reception)
                        .Sum(s => s.Area_sqm);

                    var floorGross = floor.Sum(s => s.Area_sqm);
                    var floorNet = floorLettable.Sum(s => s.UsableArea_sqm);
                    var floorLettableArea = floorLettable.Sum(s => s.RentableArea_sqm);

                    breakdown.FloorBreakdowns.Add(new FloorAreaBreakdown
                    {
                        FloorLevel = floor.Key,
                        FloorName = floor.First().FloorName.Length > 0
                            ? floor.First().FloorName
                            : $"Level {floor.Key}",
                        GrossArea_sqm = floorGross,
                        NetArea_sqm = floorNet,
                        LettableArea_sqm = floorLettableArea,
                        CommonArea_sqm = floorCommon,
                        CommonAreaFactor = floorNet > 0 ? floorLettableArea / floorNet : 1.0
                    });
                }

                Logger.Info("Area breakdown for {Building}: GIA={GIA:F0}sqm, NIA={NIA:F0}sqm, NLA={NLA:F0}sqm, Efficiency={Eff:F1}%",
                    buildingId, breakdown.GrossInternalArea_sqm, breakdown.NetInternalArea_sqm,
                    breakdown.NetLettableArea_sqm, breakdown.BuildingEfficiency);

                return breakdown;
            }
        }

        /// <summary>
        /// Calculates common area factor per BOMA 2017 for a given building,
        /// applied to usable area to derive rentable area.
        /// </summary>
        public double CalculateCommonAreaFactor(string buildingId)
        {
            lock (_lockObject)
            {
                var buildingSpaces = _spaces.Values
                    .Where(s => s.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (buildingSpaces.Count == 0) return 1.0;

                var totalGIA = buildingSpaces.Sum(s => s.Area_sqm);
                var commonArea = buildingSpaces
                    .Where(s => s.SpaceType == SpaceType.Common || s.SpaceType == SpaceType.Reception)
                    .Sum(s => s.Area_sqm);
                var usableArea = buildingSpaces
                    .Where(s => s.SpaceType != SpaceType.Common
                        && s.SpaceType != SpaceType.Parking
                        && s.SpaceType != SpaceType.ServerRoom
                        && s.SpaceType != SpaceType.Reception)
                    .Sum(s => s.UsableArea_sqm);

                var caf = usableArea > 0 ? (usableArea + commonArea) / usableArea : 1.0;

                Logger.Debug("Common area factor for {Building}: {CAF:F3} (common={Common:F0}sqm, usable={Usable:F0}sqm)",
                    buildingId, caf, commonArea, usableArea);

                return Math.Round(caf, 4);
            }
        }

        #endregion

        #region Hot-Desking Analysis

        /// <summary>
        /// Analyzes hot-desking (desk sharing) potential for a floor based on
        /// utilization patterns, recommending optimal desk-to-person ratios.
        /// </summary>
        public async Task<(double optimalRatio, int recommendedDesks, int currentDesks, string analysis)>
            HotDeskingAnalysisAsync(
                int floorLevel,
                string buildingId,
                CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_lockObject)
                {
                    var floorSpaces = _spaces.Values
                        .Where(s => s.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase)
                            && s.FloorLevel == floorLevel
                            && s.SpaceType == SpaceType.Office)
                        .ToList();

                    if (floorSpaces.Count == 0)
                    {
                        return (1.0, 0, 0, "No office spaces found on this floor.");
                    }

                    var totalDesks = 0;
                    var totalHeadcount = 0;
                    var avgUtilization = 0.0;
                    var utilizationCount = 0;

                    foreach (var space in floorSpaces)
                    {
                        if (_utilizationHistory.TryGetValue(space.SpaceId, out var history))
                        {
                            var recent = history.Where(u => u.Date >= DateTime.UtcNow.AddMonths(-3)).ToList();
                            if (recent.Count > 0)
                            {
                                avgUtilization += recent.Average(u => u.DeskUtilizationPct);
                                utilizationCount++;
                                totalDesks += recent.First().TotalDesks;
                            }
                        }

                        totalHeadcount += space.MaxOccupancy > 0
                            ? space.MaxOccupancy
                            : EstimateCapacity(space);
                    }

                    if (utilizationCount > 0)
                        avgUtilization /= utilizationCount;
                    else
                        avgUtilization = 70; // Default assumption

                    // Calculate optimal ratio
                    // Peak utilization rarely exceeds 80%, adding 10% buffer
                    var peakUtilization = Math.Min(avgUtilization * 1.2, 100);
                    var optimalRatio = peakUtilization > 0 ? peakUtilization / 100.0 : 0.8;
                    optimalRatio = Math.Max(0.5, Math.Min(1.0, optimalRatio)); // Clamp 0.5 to 1.0

                    var recommendedDesks = (int)Math.Ceiling(totalHeadcount * optimalRatio);

                    var analysisLines = new List<string>
                    {
                        $"Floor {floorLevel} Hot-Desking Analysis",
                        $"  Total headcount assigned: {totalHeadcount}",
                        $"  Current desks: {totalDesks}",
                        $"  Average desk utilization: {avgUtilization:F1}%",
                        $"  Optimal desk-to-person ratio: {optimalRatio:F2}:1",
                        $"  Recommended desks: {recommendedDesks}",
                        $"  Potential desk reduction: {Math.Max(0, totalDesks - recommendedDesks)} desks",
                        $"  Space savings: ~{Math.Max(0, (totalDesks - recommendedDesks) * HotDeskAreaPerPerson):F0} sqm"
                    };

                    if (optimalRatio < 0.7)
                        analysisLines.Add("  Recommendation: Aggressive hot-desking viable - high mobility workforce");
                    else if (optimalRatio < 0.85)
                        analysisLines.Add("  Recommendation: Moderate hot-desking recommended");
                    else
                        analysisLines.Add("  Recommendation: Limited hot-desking - consider dedicated desks");

                    Logger.Info("Hot-desking analysis for Floor {Floor}: ratio={Ratio:F2}, recommended={Desks}",
                        floorLevel, optimalRatio, recommendedDesks);

                    return (
                        Math.Round(optimalRatio, 2),
                        recommendedDesks,
                        totalDesks,
                        string.Join("\n", analysisLines)
                    );
                }
            }, cancellationToken);
        }

        #endregion

        #region Space Report

        /// <summary>
        /// Generates a comprehensive space inventory report for a building
        /// with floor-by-floor breakdown, vacancy, utilization, and market rent data.
        /// </summary>
        public async Task<Dictionary<string, object>> GenerateSpaceReportAsync(
            string buildingId,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Generating space report...");

                var report = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                lock (_lockObject)
                {
                    var buildingSpaces = _spaces.Values
                        .Where(s => s.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // Summary
                    report["BuildingId"] = buildingId;
                    report["ReportDate"] = DateTime.UtcNow;
                    report["TotalSpaces"] = buildingSpaces.Count;
                    report["TotalArea_sqm"] = buildingSpaces.Sum(s => s.Area_sqm);
                    report["TotalUsableArea_sqm"] = buildingSpaces.Sum(s => s.UsableArea_sqm);
                    report["TotalRentableArea_sqm"] = buildingSpaces.Sum(s => s.RentableArea_sqm);

                    // Occupancy summary
                    var occupied = buildingSpaces.Count(s => s.OccupancyStatus == OccupancyStatus.Occupied);
                    var vacant = buildingSpaces.Count(s => s.OccupancyStatus == OccupancyStatus.Vacant);
                    var underRefurb = buildingSpaces.Count(s => s.OccupancyStatus == OccupancyStatus.UnderRefurbishment);

                    report["OccupiedSpaces"] = occupied;
                    report["VacantSpaces"] = vacant;
                    report["UnderRefurbishmentSpaces"] = underRefurb;
                    report["OccupancyRate"] = buildingSpaces.Count > 0
                        ? (double)occupied / buildingSpaces.Count * 100.0
                        : 0;

                    // Area-weighted occupancy
                    var occupiedArea = buildingSpaces
                        .Where(s => s.OccupancyStatus == OccupancyStatus.Occupied)
                        .Sum(s => s.RentableArea_sqm);
                    var totalRentableArea = buildingSpaces.Sum(s => s.RentableArea_sqm);
                    report["AreaWeightedOccupancy"] = totalRentableArea > 0
                        ? occupiedArea / totalRentableArea * 100.0
                        : 0;

                    // By type
                    var typeBreakdown = buildingSpaces
                        .GroupBy(s => s.SpaceType)
                        .Select(g => new
                        {
                            Type = g.Key.ToString(),
                            Count = g.Count(),
                            TotalArea = g.Sum(s => s.Area_sqm),
                            OccupiedCount = g.Count(s => s.OccupancyStatus == OccupancyStatus.Occupied),
                            VacantCount = g.Count(s => s.OccupancyStatus == OccupancyStatus.Vacant)
                        })
                        .ToList();
                    report["SpacesByType"] = typeBreakdown;

                    // By floor
                    var floorBreakdown = buildingSpaces
                        .GroupBy(s => s.FloorLevel)
                        .OrderBy(g => g.Key)
                        .Select(g => new
                        {
                            Floor = g.Key,
                            FloorName = g.First().FloorName,
                            SpaceCount = g.Count(),
                            TotalArea = g.Sum(s => s.Area_sqm),
                            OccupiedArea = g.Where(s => s.OccupancyStatus == OccupancyStatus.Occupied).Sum(s => s.RentableArea_sqm),
                            VacantArea = g.Where(s => s.OccupancyStatus == OccupancyStatus.Vacant).Sum(s => s.RentableArea_sqm)
                        })
                        .ToList();
                    report["SpacesByFloor"] = floorBreakdown;

                    // Market rent summary
                    var spacesWithRent = buildingSpaces.Where(s => s.MarketRentPerSqm > 0).ToList();
                    if (spacesWithRent.Count > 0)
                    {
                        report["AvgMarketRentPerSqm"] = spacesWithRent.Average(s => s.MarketRentPerSqm);
                        report["MinMarketRentPerSqm"] = spacesWithRent.Min(s => s.MarketRentPerSqm);
                        report["MaxMarketRentPerSqm"] = spacesWithRent.Max(s => s.MarketRentPerSqm);
                        report["TotalPotentialAnnualRent"] = spacesWithRent.Sum(s => s.EstimatedAnnualRent);
                    }

                    // BOMA area breakdown
                    var areaBreakdown = CalculateAreaBreakdown(buildingId);
                    report["BuildingEfficiency"] = areaBreakdown.BuildingEfficiency;
                    report["CommonAreaFactor"] = CalculateCommonAreaFactor(buildingId);
                }

                progress?.Report("Space report generated.");
                Logger.Info("Space report generated for building {BuildingId}.", buildingId);

                return report;
            }, cancellationToken);
        }

        #endregion
    }
}
