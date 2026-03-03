// ============================================================================
// StingBIM AI - Lease Management Engine
// Full lease lifecycle management: creation, renewal, termination, escalation,
// rent roll generation, WALE calculation, cash flow projection, vacancy analysis,
// and multi-currency support with alert system for upcoming lease events.
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
    /// Enterprise lease management engine providing comprehensive lifecycle management,
    /// financial analysis, cash flow projections, and proactive alert systems for
    /// commercial property lease portfolios.
    /// </summary>
    public class LeaseManagementEngine
    {
        #region Fields

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        private readonly Dictionary<string, Tenant> _tenants;
        private readonly Dictionary<string, Lease> _leases;
        private readonly Dictionary<string, Space> _spaces;
        private readonly List<LeaseEvent> _events;
        private readonly Dictionary<string, decimal> _currencyRates;

        // Alert configuration thresholds (in days)
        private readonly int[] _alertThresholds = { 30, 60, 90, 180, 365 };

        // Default vacancy assumption for projections
        private const double DefaultRenewalProbability = 0.65;
        private const double DefaultVoidPeriodMonths = 6.0;

        #endregion

        #region Constructor

        public LeaseManagementEngine()
        {
            _tenants = new Dictionary<string, Tenant>(StringComparer.OrdinalIgnoreCase);
            _leases = new Dictionary<string, Lease>(StringComparer.OrdinalIgnoreCase);
            _spaces = new Dictionary<string, Space>(StringComparer.OrdinalIgnoreCase);
            _events = new List<LeaseEvent>();
            _currencyRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            InitializeCurrencyRates();
            Logger.Info("LeaseManagementEngine initialized successfully.");
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes default currency exchange rates relative to USD.
        /// </summary>
        private void InitializeCurrencyRates()
        {
            _currencyRates["USD"] = 1.0m;
            _currencyRates["GBP"] = 0.79m;
            _currencyRates["EUR"] = 0.92m;
            _currencyRates["UGX"] = 3750.0m;
            _currencyRates["KES"] = 153.0m;
            _currencyRates["ZAR"] = 18.5m;
            _currencyRates["TZS"] = 2510.0m;
            _currencyRates["RWF"] = 1280.0m;
            _currencyRates["NGN"] = 1550.0m;

            Logger.Debug("Currency exchange rates initialized with {Count} currencies.", _currencyRates.Count);
        }

        /// <summary>
        /// Updates the exchange rate for a given currency relative to USD.
        /// </summary>
        public void UpdateCurrencyRate(string currencyCode, decimal rateToUsd)
        {
            lock (_lockObject)
            {
                _currencyRates[currencyCode] = rateToUsd;
            }
            Logger.Info("Currency rate updated: {Currency} = {Rate} per USD.", currencyCode, rateToUsd);
        }

        #endregion

        #region Data Registration

        /// <summary>
        /// Registers a tenant in the management system.
        /// </summary>
        public void RegisterTenant(Tenant tenant)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            if (string.IsNullOrWhiteSpace(tenant.TenantId))
                throw new ArgumentException("Tenant ID is required.", nameof(tenant));

            lock (_lockObject)
            {
                _tenants[tenant.TenantId] = tenant;
            }
            Logger.Info("Tenant registered: {TenantId} - {Name}", tenant.TenantId, tenant.Name);
        }

        /// <summary>
        /// Registers a space in the management system.
        /// </summary>
        public void RegisterSpace(Space space)
        {
            if (space == null) throw new ArgumentNullException(nameof(space));
            if (string.IsNullOrWhiteSpace(space.SpaceId))
                throw new ArgumentException("Space ID is required.", nameof(space));

            lock (_lockObject)
            {
                _spaces[space.SpaceId] = space;
            }
            Logger.Info("Space registered: {SpaceId} on floor {Floor}", space.SpaceId, space.FloorLevel);
        }

        /// <summary>
        /// Retrieves a tenant by ID, or null if not found.
        /// </summary>
        public Tenant? GetTenant(string tenantId)
        {
            lock (_lockObject)
            {
                return _tenants.TryGetValue(tenantId, out var tenant) ? tenant : null;
            }
        }

        /// <summary>
        /// Retrieves a lease by ID, or null if not found.
        /// </summary>
        public Lease? GetLease(string leaseId)
        {
            lock (_lockObject)
            {
                return _leases.TryGetValue(leaseId, out var lease) ? lease : null;
            }
        }

        /// <summary>
        /// Retrieves all active tenants.
        /// </summary>
        public List<Tenant> GetAllTenants()
        {
            lock (_lockObject)
            {
                return _tenants.Values.Where(t => t.IsActive).ToList();
            }
        }

        /// <summary>
        /// Retrieves all leases, optionally filtered by status.
        /// </summary>
        public List<Lease> GetLeases(LeaseStatus? statusFilter = null)
        {
            lock (_lockObject)
            {
                var query = _leases.Values.AsEnumerable();
                if (statusFilter.HasValue)
                    query = query.Where(l => l.Status == statusFilter.Value);
                return query.OrderBy(l => l.EndDate).ToList();
            }
        }

        #endregion

        #region Lease Lifecycle

        /// <summary>
        /// Creates a new lease with validation, binds it to the specified spaces,
        /// and generates initial lifecycle events.
        /// </summary>
        public async Task<Lease> CreateLeaseAsync(
            Lease lease,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            if (lease == null) throw new ArgumentNullException(nameof(lease));

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Validating lease parameters...");

                // Validate lease
                ValidateLease(lease);

                lock (_lockObject)
                {
                    // Assign ID if not set
                    if (string.IsNullOrWhiteSpace(lease.LeaseId))
                        lease.LeaseId = $"LSE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

                    // Set timestamps
                    lease.CreatedDate = DateTime.UtcNow;
                    lease.LastModifiedDate = DateTime.UtcNow;
                    lease.Status = LeaseStatus.Active;

                    // Calculate rent-free end date
                    if (lease.RentFreePeriodMonths > 0)
                        lease.RentFreeEndDate = lease.StartDate.AddMonths(lease.RentFreePeriodMonths);

                    // Register the lease
                    _leases[lease.LeaseId] = lease;

                    // Update tenant
                    if (_tenants.TryGetValue(lease.TenantId, out var tenant))
                    {
                        if (!tenant.LeaseIds.Contains(lease.LeaseId))
                            tenant.LeaseIds.Add(lease.LeaseId);
                    }

                    // Bind spaces
                    foreach (var spaceId in lease.SpaceIds)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (_spaces.TryGetValue(spaceId, out var space))
                        {
                            space.OccupancyStatus = OccupancyStatus.Occupied;
                            space.CurrentTenantId = lease.TenantId;
                            space.CurrentLeaseId = lease.LeaseId;
                        }
                    }

                    // Generate lifecycle events
                    GenerateLeaseEvents(lease);
                }

                progress?.Report("Lease created successfully.");
                Logger.Info("Lease created: {LeaseId} for tenant {TenantId}, term {Start} to {End}",
                    lease.LeaseId, lease.TenantId, lease.StartDate.ToShortDateString(), lease.EndDate.ToShortDateString());

                return lease;
            }, cancellationToken);
        }

        /// <summary>
        /// Validates a lease for required fields and logical consistency.
        /// </summary>
        private void ValidateLease(Lease lease)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(lease.TenantId))
                errors.Add("Tenant ID is required.");
            if (lease.SpaceIds == null || lease.SpaceIds.Count == 0)
                errors.Add("At least one space must be assigned.");
            if (lease.StartDate >= lease.EndDate)
                errors.Add("Start date must be before end date.");
            if (lease.MonthlyRent < 0)
                errors.Add("Monthly rent cannot be negative.");
            if (lease.EscalationRate < 0 || lease.EscalationRate > 100)
                errors.Add("Escalation rate must be between 0 and 100.");

            // Validate tenant exists
            lock (_lockObject)
            {
                if (!_tenants.ContainsKey(lease.TenantId))
                    errors.Add($"Tenant '{lease.TenantId}' not found in registry.");

                // Validate spaces exist and are available
                foreach (var spaceId in lease.SpaceIds ?? new List<string>())
                {
                    if (!_spaces.TryGetValue(spaceId, out var space))
                    {
                        errors.Add($"Space '{spaceId}' not found in registry.");
                    }
                    else if (space.OccupancyStatus == OccupancyStatus.Occupied
                             && space.CurrentLeaseId != lease.LeaseId)
                    {
                        errors.Add($"Space '{spaceId}' is already occupied by tenant '{space.CurrentTenantId}'.");
                    }
                }
            }

            if (errors.Count > 0)
            {
                var message = $"Lease validation failed: {string.Join("; ", errors)}";
                Logger.Error(message);
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Generates standard lifecycle events for a lease (expiry, reviews, breaks).
        /// </summary>
        private void GenerateLeaseEvents(Lease lease)
        {
            // Commencement event
            _events.Add(new LeaseEvent
            {
                LeaseId = lease.LeaseId,
                TenantId = lease.TenantId,
                Type = LeaseEventType.Commencement,
                EventDate = lease.StartDate,
                Description = $"Lease commencement for {lease.LeaseId}",
                Processed = lease.StartDate <= DateTime.UtcNow
            });

            // Expiry event
            _events.Add(new LeaseEvent
            {
                LeaseId = lease.LeaseId,
                TenantId = lease.TenantId,
                Type = LeaseEventType.Expiry,
                EventDate = lease.EndDate,
                Description = $"Lease expiry for {lease.LeaseId}",
                AlertDaysBefore = 365
            });

            // Break clause event
            if (lease.BreakClause != null)
            {
                _events.Add(new LeaseEvent
                {
                    LeaseId = lease.LeaseId,
                    TenantId = lease.TenantId,
                    Type = LeaseEventType.BreakNotice,
                    EventDate = lease.BreakClause.NoticeDeadline,
                    Description = $"Break notice deadline for {lease.LeaseId}",
                    AlertDaysBefore = 180
                });
            }

            // Rent-free end event
            if (lease.RentFreeEndDate.HasValue)
            {
                _events.Add(new LeaseEvent
                {
                    LeaseId = lease.LeaseId,
                    TenantId = lease.TenantId,
                    Type = LeaseEventType.RentFreeEnd,
                    EventDate = lease.RentFreeEndDate.Value,
                    Description = $"Rent-free period ends for {lease.LeaseId}",
                    AlertDaysBefore = 30
                });
            }

            // Annual rent review events
            if (lease.EscalationType != EscalationType.Fixed)
            {
                var reviewDate = lease.StartDate.AddYears(1);
                while (reviewDate < lease.EndDate)
                {
                    _events.Add(new LeaseEvent
                    {
                        LeaseId = lease.LeaseId,
                        TenantId = lease.TenantId,
                        Type = LeaseEventType.RentReview,
                        EventDate = reviewDate,
                        Description = $"Rent review ({lease.EscalationType}) for {lease.LeaseId}",
                        AlertDaysBefore = 90
                    });
                    reviewDate = reviewDate.AddYears(1);
                }
            }

            Logger.Debug("Generated lifecycle events for lease {LeaseId}.", lease.LeaseId);
        }

        /// <summary>
        /// Renews an existing lease with new terms, creating a successor lease.
        /// </summary>
        public async Task<Lease> RenewLeaseAsync(
            string leaseId,
            int newTermMonths,
            decimal newMonthlyRent,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Processing lease renewal...");

                lock (_lockObject)
                {
                    if (!_leases.TryGetValue(leaseId, out var existingLease))
                        throw new InvalidOperationException($"Lease '{leaseId}' not found.");

                    if (existingLease.RenewalOption != null)
                    {
                        if (existingLease.RenewalOption.RenewalsExercised >= existingLease.RenewalOption.MaxRenewals)
                            throw new InvalidOperationException("Maximum renewal count exceeded.");
                        existingLease.RenewalOption.RenewalsExercised++;
                    }

                    // Create renewal lease
                    var renewalLease = new Lease
                    {
                        LeaseId = $"LSE-REN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                        TenantId = existingLease.TenantId,
                        SpaceIds = new List<string>(existingLease.SpaceIds),
                        StartDate = existingLease.EndDate.AddDays(1),
                        EndDate = existingLease.EndDate.AddMonths(newTermMonths),
                        MonthlyRent = newMonthlyRent,
                        Currency = existingLease.Currency,
                        EscalationRate = existingLease.EscalationRate,
                        EscalationType = existingLease.EscalationType,
                        SecurityDeposit = existingLease.SecurityDeposit,
                        Status = LeaseStatus.Active,
                        CreatedDate = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    };

                    renewalLease.CustomTerms["PreviousLeaseId"] = leaseId;
                    renewalLease.CustomTerms["RenewalNumber"] =
                        (existingLease.RenewalOption?.RenewalsExercised ?? 1).ToString();

                    // Expire the old lease
                    existingLease.Status = LeaseStatus.Expired;
                    existingLease.LastModifiedDate = DateTime.UtcNow;

                    // Register renewal
                    _leases[renewalLease.LeaseId] = renewalLease;

                    // Update tenant
                    if (_tenants.TryGetValue(renewalLease.TenantId, out var tenant))
                    {
                        tenant.LeaseIds.Add(renewalLease.LeaseId);
                    }

                    // Update spaces
                    foreach (var spaceId in renewalLease.SpaceIds)
                    {
                        if (_spaces.TryGetValue(spaceId, out var space))
                        {
                            space.CurrentLeaseId = renewalLease.LeaseId;
                        }
                    }

                    // Generate new events
                    GenerateLeaseEvents(renewalLease);

                    // Mark existing expiry event as processed
                    var expiryEvent = _events.FirstOrDefault(e =>
                        e.LeaseId == leaseId && e.Type == LeaseEventType.Expiry && !e.Processed);
                    if (expiryEvent != null)
                    {
                        expiryEvent.Processed = true;
                        expiryEvent.ProcessedDate = DateTime.UtcNow;
                        expiryEvent.Notes = $"Renewed as {renewalLease.LeaseId}";
                    }

                    progress?.Report("Lease renewal completed.");
                    Logger.Info("Lease {OldId} renewed as {NewId} for {Months} months at {Rent}/month.",
                        leaseId, renewalLease.LeaseId, newTermMonths, newMonthlyRent);

                    return renewalLease;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Terminates a lease with notice period validation and space release.
        /// </summary>
        public async Task<bool> TerminateLeaseAsync(
            string leaseId,
            string reason,
            DateTime effectiveDate,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Processing lease termination...");

                lock (_lockObject)
                {
                    if (!_leases.TryGetValue(leaseId, out var lease))
                        throw new InvalidOperationException($"Lease '{leaseId}' not found.");

                    if (lease.Status != LeaseStatus.Active && lease.Status != LeaseStatus.Holdover)
                        throw new InvalidOperationException($"Cannot terminate lease in '{lease.Status}' status.");

                    // Validate notice period if break clause exists
                    if (lease.BreakClause != null && effectiveDate <= lease.BreakClause.BreakDate)
                    {
                        var requiredNotice = lease.BreakClause.NoticePeriodMonths;
                        var actualNoticeDays = (effectiveDate - DateTime.UtcNow).TotalDays;
                        var requiredNoticeDays = requiredNotice * 30.44;

                        if (actualNoticeDays < requiredNoticeDays)
                        {
                            Logger.Warn("Insufficient notice period for lease {LeaseId}: {Actual} days vs {Required} days required.",
                                leaseId, (int)actualNoticeDays, (int)requiredNoticeDays);
                        }
                    }

                    // Terminate the lease
                    lease.Status = LeaseStatus.Terminated;
                    lease.EndDate = effectiveDate;
                    lease.LastModifiedDate = DateTime.UtcNow;
                    lease.Notes += $"\nTerminated on {DateTime.UtcNow:yyyy-MM-dd}: {reason}";

                    // Release spaces
                    foreach (var spaceId in lease.SpaceIds)
                    {
                        if (_spaces.TryGetValue(spaceId, out var space))
                        {
                            space.OccupancyStatus = OccupancyStatus.Vacant;
                            space.CurrentTenantId = string.Empty;
                            space.CurrentLeaseId = string.Empty;
                        }
                    }

                    // Create termination event
                    _events.Add(new LeaseEvent
                    {
                        LeaseId = leaseId,
                        TenantId = lease.TenantId,
                        Type = LeaseEventType.Termination,
                        EventDate = effectiveDate,
                        Processed = true,
                        ProcessedDate = DateTime.UtcNow,
                        Description = $"Lease terminated: {reason}"
                    });

                    // Mark remaining unprocessed events as processed
                    foreach (var evt in _events.Where(e => e.LeaseId == leaseId && !e.Processed))
                    {
                        evt.Processed = true;
                        evt.ProcessedDate = DateTime.UtcNow;
                        evt.Notes = "Cancelled due to lease termination.";
                    }

                    progress?.Report("Lease terminated successfully.");
                    Logger.Info("Lease {LeaseId} terminated effective {Date}. Reason: {Reason}",
                        leaseId, effectiveDate.ToShortDateString(), reason);

                    return true;
                }
            }, cancellationToken);
        }

        #endregion

        #region Rent Calculations

        /// <summary>
        /// Calculates the escalated rent for a lease as of a given date, applying
        /// the appropriate escalation methodology (fixed, CPI, market review).
        /// </summary>
        public decimal CalculateRentEscalation(string leaseId, DateTime asOfDate)
        {
            lock (_lockObject)
            {
                if (!_leases.TryGetValue(leaseId, out var lease))
                    throw new InvalidOperationException($"Lease '{leaseId}' not found.");

                if (asOfDate < lease.StartDate)
                    return 0m;

                // Handle rent-free period
                if (lease.RentFreeEndDate.HasValue && asOfDate < lease.RentFreeEndDate.Value)
                    return 0m;

                var baseRent = lease.MonthlyRent;
                var yearsSinceStart = (asOfDate - lease.StartDate).TotalDays / 365.25;
                var fullYears = (int)yearsSinceStart;

                switch (lease.EscalationType)
                {
                    case EscalationType.Fixed:
                        // Compound annual fixed escalation
                        for (int i = 0; i < fullYears; i++)
                        {
                            baseRent *= (1 + lease.EscalationRate / 100m);
                        }
                        break;

                    case EscalationType.CPI:
                        // CPI-linked escalation (uses escalation rate as proxy for CPI)
                        for (int i = 0; i < fullYears; i++)
                        {
                            var cpiRate = lease.EscalationRate; // Would fetch actual CPI in production
                            baseRent *= (1 + cpiRate / 100m);
                        }
                        break;

                    case EscalationType.Market:
                    case EscalationType.OpenMarketReview:
                        // Market review uses comparable rents from space data
                        if (fullYears > 0 && lease.SpaceIds.Count > 0)
                        {
                            var firstSpaceId = lease.SpaceIds[0];
                            if (_spaces.TryGetValue(firstSpaceId, out var space) && space.MarketRentPerSqm > 0)
                            {
                                var marketMonthlyRent = space.MarketRentPerSqm * (decimal)space.RentableArea_sqm;
                                // Take the higher of current escalated rent or market rent (upward-only review)
                                var escalatedRent = lease.MonthlyRent;
                                for (int i = 0; i < fullYears; i++)
                                {
                                    escalatedRent *= (1 + lease.EscalationRate / 100m);
                                }
                                baseRent = Math.Max(escalatedRent, marketMonthlyRent);
                            }
                            else
                            {
                                // Fallback to fixed escalation
                                for (int i = 0; i < fullYears; i++)
                                {
                                    baseRent *= (1 + lease.EscalationRate / 100m);
                                }
                            }
                        }
                        break;

                    case EscalationType.SteppedFixed:
                        // Stepped: different rate per year (simplified - uses same rate but could be table-driven)
                        for (int i = 0; i < fullYears; i++)
                        {
                            var stepRate = lease.EscalationRate + (i * 0.5m); // Increasing step
                            baseRent *= (1 + stepRate / 100m);
                        }
                        break;

                    case EscalationType.IndexLinked:
                        // Index-linked - compound with base rate
                        for (int i = 0; i < fullYears; i++)
                        {
                            baseRent *= (1 + lease.EscalationRate / 100m);
                        }
                        break;

                    case EscalationType.Hybrid:
                        // Hybrid: fixed for first 3 years, then market review
                        for (int i = 0; i < fullYears; i++)
                        {
                            if (i < 3)
                                baseRent *= (1 + lease.EscalationRate / 100m);
                            else
                                baseRent *= (1 + Math.Max(lease.EscalationRate, 5m) / 100m);
                        }
                        break;
                }

                baseRent = Math.Round(baseRent, 2);
                Logger.Debug("Rent escalation for {LeaseId} as of {Date}: {Rent} {Currency}",
                    leaseId, asOfDate.ToShortDateString(), baseRent, lease.Currency);

                return baseRent;
            }
        }

        /// <summary>
        /// Calculates the total occupancy cost for a tenant including rent and service charges.
        /// </summary>
        public decimal CalculateOccupancyCost(string tenantId)
        {
            lock (_lockObject)
            {
                if (!_tenants.TryGetValue(tenantId, out var tenant))
                    throw new InvalidOperationException($"Tenant '{tenantId}' not found.");

                decimal totalCost = 0m;
                var now = DateTime.UtcNow;

                foreach (var leaseId in tenant.LeaseIds)
                {
                    if (_leases.TryGetValue(leaseId, out var lease) && lease.Status == LeaseStatus.Active)
                    {
                        var currentRent = CalculateRentEscalation(leaseId, now);
                        totalCost += currentRent;

                        // Estimate service charge (proportional to space)
                        foreach (var spaceId in lease.SpaceIds)
                        {
                            if (_spaces.TryGetValue(spaceId, out var space))
                            {
                                // Service charge estimate: 15% of rent as proxy
                                totalCost += currentRent * 0.15m;
                            }
                        }
                    }
                }

                Logger.Debug("Total occupancy cost for tenant {TenantId}: {Cost}", tenantId, totalCost);
                return totalCost;
            }
        }

        #endregion

        #region Rent Roll

        /// <summary>
        /// Generates a complete rent roll for the specified period, including
        /// vacancy analysis and effective gross income calculation.
        /// </summary>
        public async Task<RentRoll> GenerateRentRollAsync(
            string period,
            string? buildingId = null,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Generating rent roll...");

                var rentRoll = new RentRoll
                {
                    Period = period,
                    GeneratedDate = DateTime.UtcNow,
                    BuildingId = buildingId ?? "ALL"
                };

                lock (_lockObject)
                {
                    var activeLeases = _leases.Values
                        .Where(l => l.Status == LeaseStatus.Active || l.Status == LeaseStatus.Holdover)
                        .ToList();

                    foreach (var lease in activeLeases)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Filter by building if specified
                        var leaseSpaces = lease.SpaceIds
                            .Where(sid => _spaces.ContainsKey(sid))
                            .Select(sid => _spaces[sid])
                            .ToList();

                        if (!string.IsNullOrEmpty(buildingId))
                            leaseSpaces = leaseSpaces.Where(s => s.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase)).ToList();

                        if (leaseSpaces.Count == 0) continue;

                        var tenantName = _tenants.TryGetValue(lease.TenantId, out var tenant)
                            ? tenant.Name
                            : lease.TenantId;

                        var totalArea = leaseSpaces.Sum(s => s.RentableArea_sqm);
                        var currentRent = CalculateRentEscalation(lease.LeaseId, DateTime.UtcNow);
                        var annualRent = currentRent * 12;
                        var rentPerSqm = totalArea > 0 ? currentRent / (decimal)totalArea : 0;

                        var entry = new RentRollEntry
                        {
                            TenantName = tenantName,
                            TenantId = lease.TenantId,
                            LeaseId = lease.LeaseId,
                            SpaceDescription = string.Join(", ", leaseSpaces.Select(s => $"{s.Name} (Floor {s.FloorLevel})")),
                            SpaceIds = lease.SpaceIds,
                            FloorLevel = leaseSpaces.FirstOrDefault()?.FloorLevel ?? 0,
                            Area_sqm = totalArea,
                            MonthlyRent = currentRent,
                            AnnualRent = annualRent,
                            RentPerSqm = rentPerSqm,
                            LeaseStart = lease.StartDate,
                            LeaseExpiry = lease.EndDate,
                            RemainingMonths = lease.RemainingMonths,
                            LeaseStatus = lease.Status
                        };

                        rentRoll.Entries.Add(entry);
                        rentRoll.TotalGrossRent += currentRent;
                        rentRoll.TotalLeasedArea_sqm += totalArea;
                    }

                    // Calculate vacancy
                    var allSpaces = string.IsNullOrEmpty(buildingId)
                        ? _spaces.Values.ToList()
                        : _spaces.Values.Where(s => s.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase)).ToList();

                    var vacantSpaces = allSpaces.Where(s =>
                        s.OccupancyStatus == OccupancyStatus.Vacant ||
                        s.OccupancyStatus == OccupancyStatus.UnderRefurbishment).ToList();

                    rentRoll.TotalVacantArea_sqm = vacantSpaces.Sum(s => s.RentableArea_sqm);

                    // Estimate vacancy loss from market rents
                    rentRoll.VacancyLoss = vacantSpaces.Sum(s => s.MarketRentPerSqm * (decimal)s.RentableArea_sqm);

                    var totalBuildingArea = allSpaces.Sum(s => s.RentableArea_sqm);
                    rentRoll.OccupancyRate = totalBuildingArea > 0
                        ? rentRoll.TotalLeasedArea_sqm / totalBuildingArea * 100.0
                        : 0;

                    rentRoll.TotalNetRent = rentRoll.TotalGrossRent;
                    rentRoll.EffectiveGrossIncome = rentRoll.TotalGrossRent - rentRoll.VacancyLoss;

                    // Sort entries by floor and tenant name
                    rentRoll.Entries = rentRoll.Entries
                        .OrderBy(e => e.FloorLevel)
                        .ThenBy(e => e.TenantName)
                        .ToList();
                }

                progress?.Report($"Rent roll generated with {rentRoll.Entries.Count} entries.");
                Logger.Info("Rent roll generated for period {Period}: {Count} entries, gross rent {Rent}",
                    period, rentRoll.Entries.Count, rentRoll.TotalGrossRent);

                return rentRoll;
            }, cancellationToken);
        }

        #endregion

        #region Lease Analytics

        /// <summary>
        /// Tracks upcoming lease events within configurable alert windows
        /// (30, 60, 90, 180, and 365 days).
        /// </summary>
        public List<LeaseEvent> TrackLeaseEvents(int? withinDays = null)
        {
            lock (_lockObject)
            {
                var cutoffDate = DateTime.UtcNow.AddDays(withinDays ?? 365);

                var upcomingEvents = _events
                    .Where(e => !e.Processed
                        && e.EventDate > DateTime.UtcNow
                        && e.EventDate <= cutoffDate)
                    .OrderBy(e => e.EventDate)
                    .ToList();

                // Also find overdue events
                var overdueEvents = _events
                    .Where(e => !e.Processed && e.EventDate <= DateTime.UtcNow)
                    .OrderBy(e => e.EventDate)
                    .ToList();

                var allAlerts = overdueEvents.Concat(upcomingEvents).ToList();

                Logger.Info("Lease event tracking: {Overdue} overdue, {Upcoming} upcoming within {Days} days.",
                    overdueEvents.Count, upcomingEvents.Count, withinDays ?? 365);

                return allAlerts;
            }
        }

        /// <summary>
        /// Calculates Weighted Average Lease Expiry (WALE) in years,
        /// weighted by annual rent. A key metric for portfolio valuation.
        /// </summary>
        public double CalculateWALE()
        {
            lock (_lockObject)
            {
                var activeLeases = _leases.Values
                    .Where(l => l.Status == LeaseStatus.Active && l.RemainingMonths > 0)
                    .ToList();

                if (activeLeases.Count == 0) return 0;

                decimal totalWeightedYears = 0;
                decimal totalAnnualRent = 0;

                foreach (var lease in activeLeases)
                {
                    var currentRent = CalculateRentEscalation(lease.LeaseId, DateTime.UtcNow);
                    var annualRent = currentRent * 12;
                    var remainingYears = lease.RemainingMonths / 12.0m;

                    totalWeightedYears += annualRent * remainingYears;
                    totalAnnualRent += annualRent;
                }

                var wale = totalAnnualRent > 0
                    ? (double)(totalWeightedYears / totalAnnualRent)
                    : 0;

                Logger.Info("WALE calculated: {WALE:F2} years across {Count} active leases.", wale, activeLeases.Count);
                return Math.Round(wale, 2);
            }
        }

        /// <summary>
        /// Projects multi-year cash flow with escalation, vacancy assumptions,
        /// and net present value calculation.
        /// </summary>
        public async Task<CashFlowProjection> ProjectCashFlowAsync(
            int years,
            string? buildingId = null,
            double discountRate = 0.08,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Projecting cash flow...");

                var projection = new CashFlowProjection
                {
                    BuildingId = buildingId ?? "ALL",
                    ProjectionYears = years,
                    ProjectionStartDate = DateTime.UtcNow,
                    DiscountRate = discountRate
                };

                lock (_lockObject)
                {
                    for (int year = 1; year <= years; year++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var projectionDate = DateTime.UtcNow.AddYears(year);

                        var annualFlow = new AnnualCashFlow { Year = year };

                        // Calculate gross income from active and projected leases
                        foreach (var lease in _leases.Values)
                        {
                            if (lease.Status != LeaseStatus.Active && lease.Status != LeaseStatus.Holdover)
                                continue;

                            // Filter by building
                            if (!string.IsNullOrEmpty(buildingId))
                            {
                                var leaseInBuilding = lease.SpaceIds.Any(sid =>
                                    _spaces.TryGetValue(sid, out var sp) &&
                                    sp.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase));
                                if (!leaseInBuilding) continue;
                            }

                            if (projectionDate <= lease.EndDate)
                            {
                                // Lease still active in this year
                                var escalatedRent = CalculateRentEscalation(lease.LeaseId, projectionDate);
                                annualFlow.GrossRentalIncome += escalatedRent * 12;
                            }
                            else
                            {
                                // Lease expired - apply renewal probability
                                var monthsPastExpiry = (projectionDate - lease.EndDate).TotalDays / 30.44;
                                if (monthsPastExpiry <= DefaultVoidPeriodMonths)
                                {
                                    // Within void period - no income
                                    annualFlow.LeaseExpiriesCount++;
                                }
                                else
                                {
                                    // Past void period - assume partial re-let
                                    var escalatedRent = CalculateRentEscalation(lease.LeaseId, projectionDate);
                                    annualFlow.GrossRentalIncome += escalatedRent * 12 * (decimal)DefaultRenewalProbability;
                                }
                            }
                        }

                        // Vacancy allowance (structural vacancy assumption 5%)
                        annualFlow.VacancyAllowance = annualFlow.GrossRentalIncome * 0.05m;
                        annualFlow.EffectiveGrossIncome = annualFlow.GrossRentalIncome - annualFlow.VacancyAllowance;

                        // Service charge income (estimated at 15% of gross rent)
                        annualFlow.ServiceChargeIncome = annualFlow.GrossRentalIncome * 0.15m;

                        // Operating expenses (estimated at 30% of effective gross income)
                        annualFlow.OperatingExpenses = annualFlow.EffectiveGrossIncome * 0.30m;

                        // Net operating income
                        annualFlow.NetOperatingIncome = annualFlow.EffectiveGrossIncome
                            + annualFlow.ServiceChargeIncome
                            - annualFlow.OperatingExpenses;

                        // Occupancy rate projection
                        var totalArea = _spaces.Values.Sum(s => s.RentableArea_sqm);
                        annualFlow.ProjectedOccupancyRate = totalArea > 0
                            ? Math.Max(70, 100 - (year * 2)) // Simplified degradation model
                            : 0;

                        projection.AnnualFlows.Add(annualFlow);
                    }

                    // Calculate totals and NPV
                    projection.TotalProjectedIncome = projection.AnnualFlows.Sum(f => f.NetOperatingIncome);

                    decimal npv = 0;
                    for (int i = 0; i < projection.AnnualFlows.Count; i++)
                    {
                        var discountFactor = (decimal)Math.Pow(1 + discountRate, -(i + 1));
                        npv += projection.AnnualFlows[i].NetOperatingIncome * discountFactor;
                    }
                    projection.NetPresentValue = Math.Round(npv, 2);
                }

                progress?.Report($"Cash flow projected over {years} years.");
                Logger.Info("Cash flow projection: {Years} years, NPV = {NPV}, total income = {Total}",
                    years, projection.NetPresentValue, projection.TotalProjectedIncome);

                return projection;
            }, cancellationToken);
        }

        /// <summary>
        /// Performs comprehensive vacancy analysis with current rates,
        /// floor/type breakdowns, and forward-looking projections.
        /// </summary>
        public VacancyAnalysis PerformVacancyAnalysis(string? buildingId = null)
        {
            lock (_lockObject)
            {
                var allSpaces = string.IsNullOrEmpty(buildingId)
                    ? _spaces.Values.ToList()
                    : _spaces.Values.Where(s => s.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase)).ToList();

                var analysis = new VacancyAnalysis
                {
                    BuildingId = buildingId ?? "ALL",
                    AnalysisDate = DateTime.UtcNow
                };

                var vacantSpaces = allSpaces.Where(s =>
                    s.OccupancyStatus == OccupancyStatus.Vacant ||
                    s.OccupancyStatus == OccupancyStatus.UnderRefurbishment).ToList();

                var totalArea = allSpaces.Sum(s => s.RentableArea_sqm);
                var vacantArea = vacantSpaces.Sum(s => s.RentableArea_sqm);

                analysis.TotalVacantArea_sqm = vacantArea;
                analysis.VacantSpaceCount = vacantSpaces.Count;
                analysis.CurrentVacancyRate = totalArea > 0 ? vacantArea / totalArea * 100.0 : 0;
                analysis.EstimatedVacancyLoss = vacantSpaces.Sum(s => s.MarketRentPerSqm * (decimal)s.RentableArea_sqm * 12);

                // By floor breakdown
                var floorGroups = allSpaces.GroupBy(s => s.FloorLevel).OrderBy(g => g.Key);
                foreach (var floor in floorGroups)
                {
                    var floorVacant = floor.Where(s =>
                        s.OccupancyStatus == OccupancyStatus.Vacant ||
                        s.OccupancyStatus == OccupancyStatus.UnderRefurbishment).ToList();

                    var floorTotalArea = floor.Sum(s => s.RentableArea_sqm);
                    var floorVacantArea = floorVacant.Sum(s => s.RentableArea_sqm);

                    analysis.ByFloor.Add(new VacancyByFloor
                    {
                        FloorLevel = floor.Key,
                        FloorName = floor.First().FloorName,
                        TotalArea_sqm = floorTotalArea,
                        VacantArea_sqm = floorVacantArea,
                        VacancyRate = floorTotalArea > 0 ? floorVacantArea / floorTotalArea * 100.0 : 0,
                        VacantSpaceCount = floorVacant.Count
                    });
                }

                // By type breakdown
                var typeGroups = allSpaces.GroupBy(s => s.SpaceType);
                foreach (var typeGroup in typeGroups)
                {
                    var typeVacant = typeGroup.Where(s =>
                        s.OccupancyStatus == OccupancyStatus.Vacant ||
                        s.OccupancyStatus == OccupancyStatus.UnderRefurbishment).ToList();

                    var typeTotalArea = typeGroup.Sum(s => s.RentableArea_sqm);
                    var typeVacantArea = typeVacant.Sum(s => s.RentableArea_sqm);

                    analysis.ByType.Add(new VacancyByType
                    {
                        SpaceType = typeGroup.Key,
                        TotalArea_sqm = typeTotalArea,
                        VacantArea_sqm = typeVacantArea,
                        VacancyRate = typeTotalArea > 0 ? typeVacantArea / typeTotalArea * 100.0 : 0,
                        VacantSpaceCount = typeVacant.Count
                    });
                }

                // Projections based on lease expiries
                var projectionDates = new[] { 3, 6, 12, 24, 36 }; // months ahead
                foreach (var monthsAhead in projectionDates)
                {
                    var projDate = DateTime.UtcNow.AddMonths(monthsAhead);
                    var expiringLeases = _leases.Values
                        .Where(l => l.Status == LeaseStatus.Active
                            && l.EndDate <= projDate
                            && l.EndDate > DateTime.UtcNow)
                        .ToList();

                    // Filter by building
                    if (!string.IsNullOrEmpty(buildingId))
                    {
                        expiringLeases = expiringLeases
                            .Where(l => l.SpaceIds.Any(sid =>
                                _spaces.TryGetValue(sid, out var sp) &&
                                sp.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase)))
                            .ToList();
                    }

                    var additionalVacantArea = expiringLeases
                        .SelectMany(l => l.SpaceIds)
                        .Where(sid => _spaces.ContainsKey(sid))
                        .Select(sid => _spaces[sid])
                        .Sum(s => s.RentableArea_sqm);

                    var projectedVacantArea = vacantArea + (additionalVacantArea * (1 - DefaultRenewalProbability));
                    var projectedVacancyRate = totalArea > 0 ? projectedVacantArea / totalArea * 100.0 : 0;

                    analysis.Projections.Add(new ProjectedVacancy
                    {
                        ProjectionDate = projDate,
                        ProjectedVacancyRate = Math.Round(projectedVacancyRate, 2),
                        AdditionalVacantArea_sqm = additionalVacantArea * (1 - DefaultRenewalProbability),
                        ExpiringLeaseCount = expiringLeases.Count,
                        AssumedRenewalRate = DefaultRenewalProbability
                    });
                }

                Logger.Info("Vacancy analysis complete: {Rate:F1}% vacancy, {Count} vacant spaces.",
                    analysis.CurrentVacancyRate, analysis.VacantSpaceCount);

                return analysis;
            }
        }

        /// <summary>
        /// Provides comparable rent analysis by looking at similar spaces,
        /// useful for market rent benchmarking during lease negotiations.
        /// </summary>
        public (decimal avgRent, decimal minRent, decimal maxRent, int comparableCount)
            ComparableRentAnalysis(SpaceType spaceType, double targetArea_sqm, double areaTolerance = 0.25)
        {
            lock (_lockObject)
            {
                var minArea = targetArea_sqm * (1 - areaTolerance);
                var maxArea = targetArea_sqm * (1 + areaTolerance);

                var comparableSpaces = _spaces.Values
                    .Where(s => s.SpaceType == spaceType
                        && s.RentableArea_sqm >= minArea
                        && s.RentableArea_sqm <= maxArea
                        && s.OccupancyStatus == OccupancyStatus.Occupied
                        && !string.IsNullOrEmpty(s.CurrentLeaseId))
                    .ToList();

                if (comparableSpaces.Count == 0)
                {
                    Logger.Warn("No comparable spaces found for {Type}, area ~{Area}sqm.", spaceType, targetArea_sqm);
                    return (0, 0, 0, 0);
                }

                var rents = new List<decimal>();
                foreach (var space in comparableSpaces)
                {
                    if (_leases.TryGetValue(space.CurrentLeaseId, out var lease))
                    {
                        var rentPerSqm = space.RentableArea_sqm > 0
                            ? lease.MonthlyRent / (decimal)space.RentableArea_sqm
                            : 0;
                        if (rentPerSqm > 0) rents.Add(rentPerSqm);
                    }
                }

                if (rents.Count == 0) return (0, 0, 0, 0);

                var result = (
                    avgRent: Math.Round(rents.Average(), 2),
                    minRent: rents.Min(),
                    maxRent: rents.Max(),
                    comparableCount: rents.Count
                );

                Logger.Info("Comparable rent analysis for {Type}: avg={Avg}, range={Min}-{Max}, count={Count}",
                    spaceType, result.avgRent, result.minRent, result.maxRent, result.comparableCount);

                return result;
            }
        }

        #endregion

        #region Currency Conversion

        /// <summary>
        /// Converts an amount between supported currencies.
        /// </summary>
        public decimal ConvertCurrency(decimal amount, Currency fromCurrency, Currency toCurrency)
        {
            if (fromCurrency == toCurrency) return amount;

            lock (_lockObject)
            {
                var fromKey = fromCurrency.ToString();
                var toKey = toCurrency.ToString();

                if (!_currencyRates.TryGetValue(fromKey, out var fromRate))
                    throw new InvalidOperationException($"Exchange rate not available for {fromCurrency}.");

                if (!_currencyRates.TryGetValue(toKey, out var toRate))
                    throw new InvalidOperationException($"Exchange rate not available for {toCurrency}.");

                // Convert through USD as base currency
                var usdAmount = fromRate != 0 ? amount / fromRate : 0;
                var converted = usdAmount * toRate;

                return Math.Round(converted, 2);
            }
        }

        #endregion

        #region Alert System

        /// <summary>
        /// Generates lease alerts for events occurring within the specified alert thresholds.
        /// Returns alerts grouped by urgency (30, 60, 90, 180, 365 days).
        /// </summary>
        public Dictionary<int, List<LeaseEvent>> GenerateAlerts()
        {
            var alertGroups = new Dictionary<int, List<LeaseEvent>>();

            lock (_lockObject)
            {
                foreach (var threshold in _alertThresholds)
                {
                    var cutoff = DateTime.UtcNow.AddDays(threshold);
                    var events = _events
                        .Where(e => !e.Processed
                            && e.EventDate > DateTime.UtcNow
                            && e.EventDate <= cutoff)
                        .OrderBy(e => e.EventDate)
                        .ToList();

                    alertGroups[threshold] = events;
                }

                // Add overdue events under a special -1 key
                var overdue = _events
                    .Where(e => !e.Processed && e.EventDate <= DateTime.UtcNow)
                    .OrderBy(e => e.EventDate)
                    .ToList();

                if (overdue.Count > 0)
                    alertGroups[-1] = overdue;
            }

            var totalAlerts = alertGroups.Values.Sum(g => g.Count);
            Logger.Info("Alert generation complete: {Total} total alerts across {Groups} threshold groups.",
                totalAlerts, alertGroups.Count);

            return alertGroups;
        }

        #endregion
    }
}
