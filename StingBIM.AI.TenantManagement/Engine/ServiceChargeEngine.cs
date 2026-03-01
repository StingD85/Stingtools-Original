// ============================================================================
// StingBIM AI - Service Charge Engine
// Service charge calculation, cost allocation, budget forecasting,
// reconciliation, statement generation, and multi-building portfolio support.
// Apportionment methods: floor area, weighted area, equal share, metered.
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
    /// Service charge management engine providing pro-rata cost allocation,
    /// budget forecasting, year-end reconciliation, detailed statement generation,
    /// and recoverable expense tracking for commercial property portfolios.
    /// </summary>
    public class ServiceChargeEngine
    {
        #region Fields

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        private readonly Dictionary<string, Tenant> _tenants;
        private readonly Dictionary<string, Lease> _leases;
        private readonly Dictionary<string, Space> _spaces;
        private readonly Dictionary<string, ServiceCharge> _charges;
        private readonly Dictionary<string, ServiceChargeBudget> _budgets;
        private readonly Dictionary<string, List<BuildingExpense>> _expenses;

        // Default cost category weights for budget estimation
        private static readonly Dictionary<string, double> DefaultCostWeights = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ManagementFees"] = 0.12,
            ["Insurance"] = 0.08,
            ["RepairsAndMaintenance"] = 0.18,
            ["Utilities"] = 0.22,
            ["Cleaning"] = 0.12,
            ["Security"] = 0.10,
            ["Landscaping"] = 0.03,
            ["CommonAreaMaintenance"] = 0.06,
            ["WasteManagement"] = 0.03,
            ["FireSafety"] = 0.02,
            ["LiftMaintenance"] = 0.03,
            ["Sinking"] = 0.01
        };

        #endregion

        #region Constructor

        public ServiceChargeEngine()
        {
            _tenants = new Dictionary<string, Tenant>(StringComparer.OrdinalIgnoreCase);
            _leases = new Dictionary<string, Lease>(StringComparer.OrdinalIgnoreCase);
            _spaces = new Dictionary<string, Space>(StringComparer.OrdinalIgnoreCase);
            _charges = new Dictionary<string, ServiceCharge>(StringComparer.OrdinalIgnoreCase);
            _budgets = new Dictionary<string, ServiceChargeBudget>(StringComparer.OrdinalIgnoreCase);
            _expenses = new Dictionary<string, List<BuildingExpense>>(StringComparer.OrdinalIgnoreCase);

            Logger.Info("ServiceChargeEngine initialized.");
        }

        #endregion

        #region Data Registration

        /// <summary>
        /// Registers a tenant for service charge calculations.
        /// </summary>
        public void RegisterTenant(Tenant tenant)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            lock (_lockObject) { _tenants[tenant.TenantId] = tenant; }
        }

        /// <summary>
        /// Registers a lease for service charge allocation.
        /// </summary>
        public void RegisterLease(Lease lease)
        {
            if (lease == null) throw new ArgumentNullException(nameof(lease));
            lock (_lockObject) { _leases[lease.LeaseId] = lease; }
        }

        /// <summary>
        /// Registers a space for area-based apportionment.
        /// </summary>
        public void RegisterSpace(Space space)
        {
            if (space == null) throw new ArgumentNullException(nameof(space));
            lock (_lockObject) { _spaces[space.SpaceId] = space; }
        }

        /// <summary>
        /// Records a building operating expense for a specific period.
        /// </summary>
        public void RecordExpense(BuildingExpense expense)
        {
            if (expense == null) throw new ArgumentNullException(nameof(expense));

            lock (_lockObject)
            {
                var key = $"{expense.BuildingId}_{expense.Period}";
                if (!_expenses.ContainsKey(key))
                    _expenses[key] = new List<BuildingExpense>();

                _expenses[key].Add(expense);
            }

            Logger.Debug("Expense recorded: {Category} = {Amount} for {Building} period {Period}.",
                expense.Category, expense.Amount, expense.BuildingId, expense.Period);
        }

        #endregion

        #region Service Charge Calculation

        /// <summary>
        /// Calculates the service charge for a tenant for a given period,
        /// allocating building costs pro-rata based on the configured apportionment method.
        /// </summary>
        public async Task<ServiceCharge> CalculateServiceChargeAsync(
            string tenantId,
            string period,
            string? buildingId = null,
            ApportionmentMethod? methodOverride = null,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Calculating service charge for tenant {tenantId}...");

                lock (_lockObject)
                {
                    if (!_tenants.TryGetValue(tenantId, out var tenant))
                        throw new InvalidOperationException($"Tenant '{tenantId}' not found.");

                    // Find the tenant's active leases
                    var tenantLeases = _leases.Values
                        .Where(l => l.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
                            && l.Status == LeaseStatus.Active)
                        .ToList();

                    if (tenantLeases.Count == 0)
                        throw new InvalidOperationException($"No active leases found for tenant '{tenantId}'.");

                    // Gather tenant's spaces
                    var tenantSpaces = tenantLeases
                        .SelectMany(l => l.SpaceIds)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Where(sid => _spaces.ContainsKey(sid))
                        .Select(sid => _spaces[sid])
                        .ToList();

                    if (!string.IsNullOrEmpty(buildingId))
                    {
                        tenantSpaces = tenantSpaces
                            .Where(s => s.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }

                    var effectiveBuildingId = buildingId ?? tenantSpaces.FirstOrDefault()?.BuildingId ?? "UNKNOWN";
                    var tenantArea = tenantSpaces.Sum(s => s.RentableArea_sqm);

                    // Get total building area for proportioning
                    var allBuildingSpaces = _spaces.Values
                        .Where(s => s.BuildingId.Equals(effectiveBuildingId, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var totalBuildingLettableArea = allBuildingSpaces
                        .Where(s => s.SpaceType != SpaceType.Common
                            && s.SpaceType != SpaceType.Parking)
                        .Sum(s => s.RentableArea_sqm);

                    // Calculate proportion
                    var method = methodOverride ?? DetermineApportionmentMethod(tenantLeases.First());
                    var proportion = CalculateProportion(method, tenantArea, totalBuildingLettableArea,
                        tenantSpaces.Count, allBuildingSpaces.Count);

                    // Get or estimate building expenses
                    var expenseKey = $"{effectiveBuildingId}_{period}";
                    var totalExpenses = GetBuildingExpenses(expenseKey, effectiveBuildingId);

                    // Apply proportion to each cost category
                    var breakdown = new ServiceChargeBreakdown
                    {
                        ManagementFees = Math.Round(totalExpenses.ManagementFees * (decimal)proportion, 2),
                        Insurance = Math.Round(totalExpenses.Insurance * (decimal)proportion, 2),
                        RepairsAndMaintenance = Math.Round(totalExpenses.RepairsAndMaintenance * (decimal)proportion, 2),
                        Utilities = Math.Round(totalExpenses.Utilities * (decimal)proportion, 2),
                        Cleaning = Math.Round(totalExpenses.Cleaning * (decimal)proportion, 2),
                        Security = Math.Round(totalExpenses.Security * (decimal)proportion, 2),
                        Landscaping = Math.Round(totalExpenses.Landscaping * (decimal)proportion, 2),
                        CommonAreaMaintenance = Math.Round(totalExpenses.CommonAreaMaintenance * (decimal)proportion, 2),
                        WasteManagement = Math.Round(totalExpenses.WasteManagement * (decimal)proportion, 2),
                        FireSafety = Math.Round(totalExpenses.FireSafety * (decimal)proportion, 2),
                        LiftMaintenance = Math.Round(totalExpenses.LiftMaintenance * (decimal)proportion, 2),
                        Sinking = Math.Round(totalExpenses.Sinking * (decimal)proportion, 2)
                    };

                    var charge = new ServiceCharge
                    {
                        TenantId = tenantId,
                        LeaseId = tenantLeases.First().LeaseId,
                        Period = period,
                        TotalAmount = breakdown.Total,
                        Currency = tenantLeases.First().Currency,
                        ApportionmentMethod = method,
                        TenantAreaProportion = proportion,
                        Breakdown = breakdown,
                        IssueDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow.AddDays(30)
                    };

                    _charges[charge.ChargeId] = charge;

                    progress?.Report($"Service charge calculated: {charge.TotalAmount:N2} {charge.Currency}");
                    Logger.Info("Service charge for {Tenant}, period {Period}: {Amount} {Currency} ({Method}, {Pct:F2}% share)",
                        tenantId, period, charge.TotalAmount, charge.Currency, method, proportion * 100);

                    return charge;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Determines the apportionment method based on lease terms.
        /// </summary>
        private ApportionmentMethod DetermineApportionmentMethod(Lease lease)
        {
            if (lease.CustomTerms.TryGetValue("ApportionmentMethod", out var methodStr))
            {
                if (Enum.TryParse<ApportionmentMethod>(methodStr, true, out var parsed))
                    return parsed;
            }
            return ApportionmentMethod.FloorArea; // Default
        }

        /// <summary>
        /// Calculates the tenant's proportion based on the chosen apportionment method.
        /// </summary>
        private double CalculateProportion(
            ApportionmentMethod method,
            double tenantArea,
            double totalBuildingArea,
            int tenantSpaceCount,
            int totalSpaceCount)
        {
            return method switch
            {
                ApportionmentMethod.FloorArea =>
                    totalBuildingArea > 0 ? tenantArea / totalBuildingArea : 0,

                ApportionmentMethod.WeightedArea =>
                    totalBuildingArea > 0 ? (tenantArea * 1.1) / (totalBuildingArea * 1.05) : 0,

                ApportionmentMethod.EqualShare =>
                    totalSpaceCount > 0 ? (double)tenantSpaceCount / totalSpaceCount : 0,

                ApportionmentMethod.Metered =>
                    totalBuildingArea > 0 ? tenantArea / totalBuildingArea : 0,

                ApportionmentMethod.Headcount =>
                    totalBuildingArea > 0 ? tenantArea / totalBuildingArea : 0,

                _ => totalBuildingArea > 0 ? tenantArea / totalBuildingArea : 0
            };
        }

        /// <summary>
        /// Retrieves recorded building expenses or generates estimates if unavailable.
        /// </summary>
        private ServiceChargeBreakdown GetBuildingExpenses(string expenseKey, string buildingId)
        {
            var breakdown = new ServiceChargeBreakdown();

            if (_expenses.TryGetValue(expenseKey, out var expenses) && expenses.Count > 0)
            {
                foreach (var expense in expenses)
                {
                    switch (expense.Category?.ToLowerInvariant())
                    {
                        case "management": case "managementfees": breakdown.ManagementFees += expense.Amount; break;
                        case "insurance": breakdown.Insurance += expense.Amount; break;
                        case "repairs": case "repairsandmaintenance": case "maintenance":
                            breakdown.RepairsAndMaintenance += expense.Amount; break;
                        case "utilities": case "electricity": case "water": case "gas":
                            breakdown.Utilities += expense.Amount; break;
                        case "cleaning": breakdown.Cleaning += expense.Amount; break;
                        case "security": breakdown.Security += expense.Amount; break;
                        case "landscaping": breakdown.Landscaping += expense.Amount; break;
                        case "commonarea": case "cam": case "commonareamaintenance":
                            breakdown.CommonAreaMaintenance += expense.Amount; break;
                        case "waste": case "wastemanagement": breakdown.WasteManagement += expense.Amount; break;
                        case "fire": case "firesafety": breakdown.FireSafety += expense.Amount; break;
                        case "lift": case "elevator": case "liftmaintenance":
                            breakdown.LiftMaintenance += expense.Amount; break;
                        case "sinking": case "reserve": breakdown.Sinking += expense.Amount; break;
                        default:
                            breakdown.OtherCosts[expense.Category ?? "Other"] = expense.Amount;
                            break;
                    }
                }
            }
            else
            {
                // Estimate from building area - typical costs per sqm per month
                var buildingSpaces = _spaces.Values
                    .Where(s => s.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var totalArea = buildingSpaces.Sum(s => s.Area_sqm);
                if (totalArea <= 0) totalArea = 1000; // Default assumption

                // Typical service charge: 3-8 USD per sqm per month for East Africa
                var totalEstimate = (decimal)totalArea * 5.0m; // 5 USD/sqm/month mid-range

                foreach (var kvp in DefaultCostWeights)
                {
                    var categoryAmount = totalEstimate * (decimal)kvp.Value;
                    switch (kvp.Key)
                    {
                        case "ManagementFees": breakdown.ManagementFees = categoryAmount; break;
                        case "Insurance": breakdown.Insurance = categoryAmount; break;
                        case "RepairsAndMaintenance": breakdown.RepairsAndMaintenance = categoryAmount; break;
                        case "Utilities": breakdown.Utilities = categoryAmount; break;
                        case "Cleaning": breakdown.Cleaning = categoryAmount; break;
                        case "Security": breakdown.Security = categoryAmount; break;
                        case "Landscaping": breakdown.Landscaping = categoryAmount; break;
                        case "CommonAreaMaintenance": breakdown.CommonAreaMaintenance = categoryAmount; break;
                        case "WasteManagement": breakdown.WasteManagement = categoryAmount; break;
                        case "FireSafety": breakdown.FireSafety = categoryAmount; break;
                        case "LiftMaintenance": breakdown.LiftMaintenance = categoryAmount; break;
                        case "Sinking": breakdown.Sinking = categoryAmount; break;
                    }
                }

                Logger.Debug("Using estimated building expenses for {Building}: total {Amount}",
                    buildingId, breakdown.Total);
            }

            return breakdown;
        }

        #endregion

        #region Cost Allocation

        /// <summary>
        /// Allocates a specific cost category across all tenants in a building
        /// for a given period, returning individual allocation amounts.
        /// </summary>
        public async Task<List<(string tenantId, string tenantName, double proportion, decimal amount)>>
            AllocateCostsAsync(
                string buildingId,
                string period,
                string costCategory,
                decimal totalCost,
                ApportionmentMethod method = ApportionmentMethod.FloorArea,
                CancellationToken cancellationToken = default,
                IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Allocating {costCategory} costs across tenants...");

                var allocations = new List<(string tenantId, string tenantName, double proportion, decimal amount)>();

                lock (_lockObject)
                {
                    // Get all lettable spaces in the building
                    var buildingSpaces = _spaces.Values
                        .Where(s => s.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase)
                            && s.SpaceType != SpaceType.Common
                            && s.SpaceType != SpaceType.Parking)
                        .ToList();

                    var totalBuildingArea = buildingSpaces.Sum(s => s.RentableArea_sqm);
                    if (totalBuildingArea <= 0)
                    {
                        Logger.Warn("No lettable area found in building {BuildingId}.", buildingId);
                        return allocations;
                    }

                    // Group spaces by tenant
                    var tenantGroups = buildingSpaces
                        .Where(s => s.OccupancyStatus == OccupancyStatus.Occupied
                            && !string.IsNullOrEmpty(s.CurrentTenantId))
                        .GroupBy(s => s.CurrentTenantId, StringComparer.OrdinalIgnoreCase);

                    decimal allocatedTotal = 0;

                    foreach (var group in tenantGroups)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var tenantId = group.Key;
                        var tenantArea = group.Sum(s => s.RentableArea_sqm);
                        var tenantSpaceCount = group.Count();

                        var proportion = CalculateProportion(
                            method, tenantArea, totalBuildingArea,
                            tenantSpaceCount, buildingSpaces.Count);

                        var amount = Math.Round(totalCost * (decimal)proportion, 2);
                        allocatedTotal += amount;

                        var tenantName = _tenants.TryGetValue(tenantId, out var tenant)
                            ? tenant.Name
                            : tenantId;

                        allocations.Add((tenantId, tenantName, Math.Round(proportion, 4), amount));
                    }

                    // Handle vacant space cost (landlord's portion)
                    var unallocated = totalCost - allocatedTotal;
                    if (unallocated > 0.01m)
                    {
                        allocations.Add(("LANDLORD", "Landlord (vacant spaces)",
                            Math.Round((double)(unallocated / totalCost), 4), unallocated));
                    }

                    // Sort by amount descending
                    allocations = allocations.OrderByDescending(a => a.amount).ToList();
                }

                progress?.Report($"Cost allocation complete: {allocations.Count} parties.");
                Logger.Info("Cost allocation for {Category} in {Building}: {Total} across {Count} tenants.",
                    costCategory, buildingId, totalCost, allocations.Count);

                return allocations;
            }, cancellationToken);
        }

        #endregion

        #region Statement Generation

        /// <summary>
        /// Generates a detailed service charge statement for a tenant,
        /// itemizing each cost category with the tenant's proportional share.
        /// </summary>
        public async Task<ServiceChargeStatement> GenerateServiceChargeStatementAsync(
            string tenantId,
            string period,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            return await Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Generating service charge statement...");

                // Ensure charge is calculated
                var charge = _charges.Values.FirstOrDefault(c =>
                    c.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase) && c.Period == period);

                if (charge == null)
                {
                    charge = await CalculateServiceChargeAsync(tenantId, period,
                        cancellationToken: cancellationToken);
                }

                var statement = new ServiceChargeStatement
                {
                    StatementId = $"SCS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                    ChargeId = charge.ChargeId,
                    TenantId = tenantId,
                    Period = period,
                    IssueDate = DateTime.UtcNow,
                    DueDate = charge.DueDate,
                    Currency = charge.Currency
                };

                lock (_lockObject)
                {
                    // Tenant details
                    if (_tenants.TryGetValue(tenantId, out var tenant))
                    {
                        statement.TenantName = tenant.Name;
                        statement.TenantAddress = $"{tenant.ContactInfo.Address}, {tenant.ContactInfo.City}";
                    }

                    // Lease details
                    var activeLease = _leases.Values.FirstOrDefault(l =>
                        l.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
                        && l.Status == LeaseStatus.Active);

                    if (activeLease != null)
                    {
                        statement.LeaseReference = activeLease.LeaseId;
                        statement.LeaseStart = activeLease.StartDate;
                        statement.LeaseEnd = activeLease.EndDate;
                    }

                    // Space details
                    var tenantSpaces = _spaces.Values
                        .Where(s => s.CurrentTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    statement.SpaceDescription = string.Join(", ",
                        tenantSpaces.Select(s => $"{s.Name} (Floor {s.FloorLevel}, {s.RentableArea_sqm:F1} sqm)"));
                    statement.TenantArea_sqm = tenantSpaces.Sum(s => s.RentableArea_sqm);
                    statement.ApportionmentMethod = charge.ApportionmentMethod;
                    statement.TenantProportion = charge.TenantAreaProportion;
                }

                // Line items from breakdown
                statement.LineItems = GenerateLineItems(charge.Breakdown);
                statement.SubTotal = charge.Breakdown.Total;
                statement.Adjustments = charge.ReconciliationAdjustment;
                statement.TotalDue = charge.TotalAmount + charge.ReconciliationAdjustment;
                statement.IsPaid = charge.IsPaid;

                progress?.Report($"Statement generated: {statement.TotalDue:N2} {statement.Currency}");
                Logger.Info("Service charge statement generated: {StatementId} for {Tenant}, total {Amount}",
                    statement.StatementId, tenantId, statement.TotalDue);

                return statement;
            }, cancellationToken);
        }

        /// <summary>
        /// Converts a breakdown into structured line items for statement display.
        /// </summary>
        private List<ServiceChargeLineItem> GenerateLineItems(ServiceChargeBreakdown breakdown)
        {
            var items = new List<ServiceChargeLineItem>();

            void AddItem(string category, string description, decimal amount)
            {
                if (amount != 0)
                {
                    items.Add(new ServiceChargeLineItem
                    {
                        Category = category,
                        Description = description,
                        Amount = amount
                    });
                }
            }

            AddItem("Management", "Property management fees", breakdown.ManagementFees);
            AddItem("Insurance", "Building insurance premium", breakdown.Insurance);
            AddItem("Repairs", "Repairs and maintenance", breakdown.RepairsAndMaintenance);
            AddItem("Utilities", "Electricity, water, and gas", breakdown.Utilities);
            AddItem("Cleaning", "Common area cleaning services", breakdown.Cleaning);
            AddItem("Security", "Security services and systems", breakdown.Security);
            AddItem("Landscaping", "Grounds and landscaping maintenance", breakdown.Landscaping);
            AddItem("CAM", "Common area maintenance", breakdown.CommonAreaMaintenance);
            AddItem("Waste", "Waste management and disposal", breakdown.WasteManagement);
            AddItem("Fire Safety", "Fire safety systems maintenance", breakdown.FireSafety);
            AddItem("Lifts", "Lift/elevator maintenance", breakdown.LiftMaintenance);
            AddItem("Sinking Fund", "Reserve/sinking fund contribution", breakdown.Sinking);

            foreach (var kvp in breakdown.OtherCosts)
            {
                AddItem(kvp.Key, kvp.Key, kvp.Value);
            }

            return items;
        }

        #endregion

        #region Reconciliation

        /// <summary>
        /// Reconciles actual versus budgeted service charges for a period,
        /// calculating the adjustment (over/under-charge) for each tenant.
        /// </summary>
        public async Task<List<ReconciliationResult>> ReconcileActualVsBudgetAsync(
            string buildingId,
            string period,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Reconciling actual vs budget...");

                var results = new List<ReconciliationResult>();

                lock (_lockObject)
                {
                    // Get actual expenses
                    var expenseKey = $"{buildingId}_{period}";
                    var actualBreakdown = GetBuildingExpenses(expenseKey, buildingId);
                    var actualTotal = actualBreakdown.Total;

                    // Get budget
                    var budgetKey = $"{buildingId}_{period}";
                    var budgetTotal = _budgets.TryGetValue(budgetKey, out var budget)
                        ? budget.TotalBudget
                        : actualTotal; // If no budget, no variance

                    var variance = actualTotal - budgetTotal;
                    var variancePct = budgetTotal != 0 ? (double)(variance / budgetTotal) * 100 : 0;

                    // Find all charges issued for this period in this building
                    var periodCharges = _charges.Values
                        .Where(c => c.Period == period)
                        .ToList();

                    foreach (var charge in periodCharges)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Tenant's actual share based on current proportion
                        var actualShare = actualTotal * (decimal)charge.TenantAreaProportion;
                        var budgetedShare = charge.TotalAmount;
                        var adjustment = actualShare - budgetedShare;

                        var tenantName = _tenants.TryGetValue(charge.TenantId, out var tenant)
                            ? tenant.Name
                            : charge.TenantId;

                        results.Add(new ReconciliationResult
                        {
                            TenantId = charge.TenantId,
                            TenantName = tenantName,
                            Period = period,
                            BudgetedAmount = budgetedShare,
                            ActualAmount = Math.Round(actualShare, 2),
                            Adjustment = Math.Round(adjustment, 2),
                            IsOverCharge = adjustment < 0,
                            Currency = charge.Currency
                        });

                        // Update the charge with reconciliation data
                        charge.IsReconciled = true;
                        charge.ReconciliationAdjustment = Math.Round(adjustment, 2);
                    }

                    // Log building-level variance
                    Logger.Info("Reconciliation for {Building} period {Period}: actual={Actual}, budget={Budget}, variance={Var:F1}%",
                        buildingId, period, actualTotal, budgetTotal, variancePct);
                }

                results = results.OrderByDescending(r => Math.Abs(r.Adjustment)).ToList();
                progress?.Report($"Reconciliation complete: {results.Count} tenants processed.");

                return results;
            }, cancellationToken);
        }

        #endregion

        #region Budget Forecasting

        /// <summary>
        /// Creates a service charge budget forecast for the next year based on
        /// historical expenses, inflation assumptions, and planned capital works.
        /// </summary>
        public async Task<ServiceChargeBudget> BudgetServiceChargesAsync(
            string buildingId,
            string nextYearPeriod,
            double inflationRate = 0.05,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("Forecasting service charge budget...");

                lock (_lockObject)
                {
                    // Find the most recent period's expenses
                    var recentExpenseKey = _expenses.Keys
                        .Where(k => k.StartsWith(buildingId, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(k => k)
                        .FirstOrDefault();

                    var baseBreakdown = recentExpenseKey != null
                        ? GetBuildingExpenses(recentExpenseKey, buildingId)
                        : GetBuildingExpenses($"{buildingId}_estimate", buildingId);

                    var inflationFactor = 1.0m + (decimal)inflationRate;

                    var budget = new ServiceChargeBudget
                    {
                        BuildingId = buildingId,
                        Period = nextYearPeriod,
                        CreatedDate = DateTime.UtcNow,
                        InflationAssumption = inflationRate,
                        Breakdown = new ServiceChargeBreakdown
                        {
                            ManagementFees = Math.Round(baseBreakdown.ManagementFees * inflationFactor, 2),
                            Insurance = Math.Round(baseBreakdown.Insurance * (inflationFactor + 0.02m), 2), // Insurance rises faster
                            RepairsAndMaintenance = Math.Round(baseBreakdown.RepairsAndMaintenance * inflationFactor, 2),
                            Utilities = Math.Round(baseBreakdown.Utilities * (inflationFactor + 0.03m), 2), // Utilities rise faster
                            Cleaning = Math.Round(baseBreakdown.Cleaning * inflationFactor, 2),
                            Security = Math.Round(baseBreakdown.Security * inflationFactor, 2),
                            Landscaping = Math.Round(baseBreakdown.Landscaping * inflationFactor, 2),
                            CommonAreaMaintenance = Math.Round(baseBreakdown.CommonAreaMaintenance * inflationFactor, 2),
                            WasteManagement = Math.Round(baseBreakdown.WasteManagement * inflationFactor, 2),
                            FireSafety = Math.Round(baseBreakdown.FireSafety * inflationFactor, 2),
                            LiftMaintenance = Math.Round(baseBreakdown.LiftMaintenance * inflationFactor, 2),
                            Sinking = Math.Round(baseBreakdown.Sinking * inflationFactor, 2)
                        }
                    };

                    budget.TotalBudget = budget.Breakdown.Total;
                    budget.PriorYearActual = baseBreakdown.Total;
                    budget.VarianceFromPrior = budget.TotalBudget - budget.PriorYearActual;
                    budget.VariancePct = budget.PriorYearActual != 0
                        ? (double)(budget.VarianceFromPrior / budget.PriorYearActual) * 100
                        : 0;

                    // Calculate per-sqm rate
                    var buildingArea = _spaces.Values
                        .Where(s => s.BuildingId.Equals(buildingId, StringComparison.OrdinalIgnoreCase))
                        .Sum(s => s.RentableArea_sqm);
                    budget.RatePerSqm_Monthly = buildingArea > 0
                        ? budget.TotalBudget / (decimal)buildingArea
                        : 0;
                    budget.RatePerSqm_Annual = budget.RatePerSqm_Monthly * 12;

                    // Store budget
                    var budgetKey = $"{buildingId}_{nextYearPeriod}";
                    _budgets[budgetKey] = budget;

                    progress?.Report($"Budget forecast: {budget.TotalBudget:N2} ({budget.VariancePct:F1}% vs prior year)");
                    Logger.Info("Service charge budget for {Building} {Period}: {Total} ({Var:F1}% variance)",
                        buildingId, nextYearPeriod, budget.TotalBudget, budget.VariancePct);

                    return budget;
                }
            }, cancellationToken);
        }

        #endregion

        #region Expense Tracking

        /// <summary>
        /// Tracks recoverable expenses for a period, calculating the total
        /// amount that can be passed through to tenants via service charges.
        /// </summary>
        public (decimal totalRecoverable, decimal totalNonRecoverable, List<(string category, decimal amount, bool recoverable)> breakdown)
            TrackRecoverableExpenses(string buildingId, string period)
        {
            lock (_lockObject)
            {
                var expenseKey = $"{buildingId}_{period}";
                var expenses = _expenses.TryGetValue(expenseKey, out var list)
                    ? list
                    : new List<BuildingExpense>();

                var breakdown = new List<(string category, decimal amount, bool recoverable)>();
                decimal totalRecoverable = 0;
                decimal totalNonRecoverable = 0;

                // Default recoverability by category
                var recoverableCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "management", "managementfees", "insurance", "repairs", "repairsandmaintenance",
                    "maintenance", "utilities", "electricity", "water", "gas", "cleaning",
                    "security", "landscaping", "commonarea", "cam", "commonareamaintenance",
                    "waste", "wastemanagement", "fire", "firesafety", "lift", "elevator",
                    "liftmaintenance"
                };

                var nonRecoverableCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "depreciation", "capital", "capitalexpenditure", "void", "voidcosts",
                    "marketing", "letting", "lettingfees", "legal", "legalfees"
                };

                foreach (var expense in expenses)
                {
                    var category = expense.Category ?? "Other";
                    var isRecoverable = expense.IsRecoverable
                        ?? recoverableCategories.Contains(category.Replace(" ", ""));

                    if (isRecoverable)
                        totalRecoverable += expense.Amount;
                    else
                        totalNonRecoverable += expense.Amount;

                    breakdown.Add((category, expense.Amount, isRecoverable));
                }

                // If no actual expenses, estimate from building data
                if (expenses.Count == 0)
                {
                    var estimatedBreakdown = GetBuildingExpenses(expenseKey, buildingId);
                    totalRecoverable = estimatedBreakdown.Total;
                    breakdown.Add(("Estimated Total (no actuals recorded)", totalRecoverable, true));
                }

                Logger.Info("Expense tracking for {Building} {Period}: recoverable={Rec}, non-recoverable={NonRec}",
                    buildingId, period, totalRecoverable, totalNonRecoverable);

                return (totalRecoverable, totalNonRecoverable, breakdown);
            }
        }

        #endregion

        #region Supporting Models

        /// <summary>
        /// A recorded building operating expense.
        /// </summary>
        public class BuildingExpense
        {
            public string ExpenseId { get; set; } = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            public string BuildingId { get; set; } = string.Empty;
            public string Period { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public Currency Currency { get; set; } = Currency.USD;
            public DateTime ExpenseDate { get; set; }
            public string Vendor { get; set; } = string.Empty;
            public string InvoiceReference { get; set; } = string.Empty;
            public bool? IsRecoverable { get; set; }
        }

        /// <summary>
        /// Service charge budget for a building for a specific period.
        /// </summary>
        public class ServiceChargeBudget
        {
            public string BuildingId { get; set; } = string.Empty;
            public string Period { get; set; } = string.Empty;
            public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
            public decimal TotalBudget { get; set; }
            public decimal PriorYearActual { get; set; }
            public decimal VarianceFromPrior { get; set; }
            public double VariancePct { get; set; }
            public double InflationAssumption { get; set; }
            public decimal RatePerSqm_Monthly { get; set; }
            public decimal RatePerSqm_Annual { get; set; }
            public ServiceChargeBreakdown Breakdown { get; set; } = new();
        }

        /// <summary>
        /// Detailed service charge statement for a tenant.
        /// </summary>
        public class ServiceChargeStatement
        {
            public string StatementId { get; set; } = string.Empty;
            public string ChargeId { get; set; } = string.Empty;
            public string TenantId { get; set; } = string.Empty;
            public string TenantName { get; set; } = string.Empty;
            public string TenantAddress { get; set; } = string.Empty;
            public string LeaseReference { get; set; } = string.Empty;
            public DateTime LeaseStart { get; set; }
            public DateTime LeaseEnd { get; set; }
            public string SpaceDescription { get; set; } = string.Empty;
            public double TenantArea_sqm { get; set; }
            public ApportionmentMethod ApportionmentMethod { get; set; }
            public double TenantProportion { get; set; }
            public string Period { get; set; } = string.Empty;
            public DateTime IssueDate { get; set; }
            public DateTime DueDate { get; set; }
            public Currency Currency { get; set; } = Currency.USD;
            public List<ServiceChargeLineItem> LineItems { get; set; } = new();
            public decimal SubTotal { get; set; }
            public decimal Adjustments { get; set; }
            public decimal TotalDue { get; set; }
            public bool IsPaid { get; set; }
        }

        /// <summary>
        /// Individual line item on a service charge statement.
        /// </summary>
        public class ServiceChargeLineItem
        {
            public string Category { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public decimal Amount { get; set; }
        }

        /// <summary>
        /// Result of a budget vs actual reconciliation for a single tenant.
        /// </summary>
        public class ReconciliationResult
        {
            public string TenantId { get; set; } = string.Empty;
            public string TenantName { get; set; } = string.Empty;
            public string Period { get; set; } = string.Empty;
            public decimal BudgetedAmount { get; set; }
            public decimal ActualAmount { get; set; }
            public decimal Adjustment { get; set; }
            public bool IsOverCharge { get; set; }
            public Currency Currency { get; set; } = Currency.USD;
        }

        #endregion
    }
}
