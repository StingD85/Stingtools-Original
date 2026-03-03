using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Automation.FacilityManagement
{
    /// <summary>
    /// Comprehensive asset management for facility operations.
    /// Handles asset tracking, lifecycle management, inventory, and depreciation.
    /// </summary>
    public class AssetManagementEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly AssetSettings _settings;
        private readonly AssetRepository _repository;
        private readonly LifecycleAnalyzer _lifecycleAnalyzer;
        private readonly DepreciationCalculator _depreciationCalc;
        private readonly InventoryManager _inventoryManager;

        public AssetManagementEngine(AssetSettings settings = null)
        {
            _settings = settings ?? new AssetSettings();
            _repository = new AssetRepository();
            _lifecycleAnalyzer = new LifecycleAnalyzer(_settings);
            _depreciationCalc = new DepreciationCalculator();
            _inventoryManager = new InventoryManager();
        }

        #region Asset Registration & Tracking

        /// <summary>
        /// Register a new asset in the system.
        /// </summary>
        public async Task<AssetRegistrationResult> RegisterAssetAsync(
            AssetRegistration registration,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Registering asset: {registration.Name}");

            var asset = new Asset
            {
                Id = GenerateAssetId(registration),
                Name = registration.Name,
                Description = registration.Description,
                Category = registration.Category,
                SubCategory = registration.SubCategory,
                Manufacturer = registration.Manufacturer,
                Model = registration.Model,
                SerialNumber = registration.SerialNumber,
                BarCode = registration.BarCode ?? GenerateBarCode(),
                QRCode = GenerateQRCode(registration),
                PurchaseDate = registration.PurchaseDate,
                InstallationDate = registration.InstallationDate,
                WarrantyExpiry = registration.WarrantyExpiry,
                PurchaseCost = registration.PurchaseCost,
                CurrentValue = registration.PurchaseCost,
                ExpectedLifeYears = registration.ExpectedLifeYears,
                Location = registration.Location,
                Status = AssetStatus.Active,
                Condition = AssetCondition.Excellent,
                CreatedDate = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            };

            // Set depreciation method
            asset.DepreciationMethod = registration.DepreciationMethod ?? DepreciationMethod.StraightLine;
            asset.SalvageValue = registration.SalvageValue ?? registration.PurchaseCost * 0.1m;

            // Link to BIM element if provided
            if (!string.IsNullOrEmpty(registration.BIMElementId))
            {
                asset.BIMElementId = registration.BIMElementId;
                asset.BIMParameters = await ExtractBIMParametersAsync(registration.BIMElementId, cancellationToken);
            }

            // Add specifications
            asset.Specifications = registration.Specifications ?? new Dictionary<string, string>();

            // Calculate initial depreciation
            asset.CurrentValue = _depreciationCalc.CalculateCurrentValue(asset);

            await _repository.AddAssetAsync(asset, cancellationToken);

            return new AssetRegistrationResult
            {
                Success = true,
                Asset = asset,
                AssetId = asset.Id,
                BarCode = asset.BarCode,
                QRCode = asset.QRCode
            };
        }

        /// <summary>
        /// Bulk register assets from BIM model.
        /// </summary>
        public async Task<BulkRegistrationResult> RegisterAssetsFromBIMAsync(
            BIMModel model,
            AssetRegistrationOptions options,
            IProgress<AssetProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Bulk registering assets from BIM model: {model.Id}");
            var result = new BulkRegistrationResult();

            var elements = model.GetEquipmentElements();
            int processed = 0;

            foreach (var element in elements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var registration = CreateRegistrationFromBIM(element, options);
                    var regResult = await RegisterAssetAsync(registration, cancellationToken);

                    if (regResult.Success)
                        result.RegisteredAssets.Add(regResult.Asset);
                    else
                        result.FailedElements.Add(element.Id);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to register asset from element {element.Id}");
                    result.FailedElements.Add(element.Id);
                }

                processed++;
                progress?.Report(new AssetProgress
                {
                    Phase = "Registering assets",
                    PercentComplete = (processed * 100) / elements.Count,
                    CurrentAsset = element.Name
                });
            }

            result.TotalProcessed = processed;
            return result;
        }

        /// <summary>
        /// Update asset location.
        /// </summary>
        public async Task<bool> UpdateAssetLocationAsync(
            string assetId,
            AssetLocation newLocation,
            string reason = null,
            CancellationToken cancellationToken = default)
        {
            var asset = await _repository.GetAssetAsync(assetId, cancellationToken);
            if (asset == null) return false;

            var movement = new AssetMovement
            {
                AssetId = assetId,
                FromLocation = asset.Location,
                ToLocation = newLocation,
                MovementDate = DateTime.UtcNow,
                Reason = reason,
                MovedBy = _settings.CurrentUserId
            };

            asset.Location = newLocation;
            asset.LastUpdated = DateTime.UtcNow;
            asset.MovementHistory.Add(movement);

            await _repository.UpdateAssetAsync(asset, cancellationToken);
            Logger.Info($"Asset {assetId} moved to {newLocation.Building}/{newLocation.Floor}/{newLocation.Room}");
            return true;
        }

        /// <summary>
        /// Update asset status.
        /// </summary>
        public async Task<bool> UpdateAssetStatusAsync(
            string assetId,
            AssetStatus newStatus,
            string notes = null,
            CancellationToken cancellationToken = default)
        {
            var asset = await _repository.GetAssetAsync(assetId, cancellationToken);
            if (asset == null) return false;

            var statusChange = new StatusChange
            {
                AssetId = assetId,
                FromStatus = asset.Status,
                ToStatus = newStatus,
                ChangeDate = DateTime.UtcNow,
                Notes = notes,
                ChangedBy = _settings.CurrentUserId
            };

            asset.Status = newStatus;
            asset.LastUpdated = DateTime.UtcNow;
            asset.StatusHistory.Add(statusChange);

            if (newStatus == AssetStatus.Disposed || newStatus == AssetStatus.Retired)
            {
                asset.DisposalDate = DateTime.UtcNow;
            }

            await _repository.UpdateAssetAsync(asset, cancellationToken);
            return true;
        }

        /// <summary>
        /// Update asset condition after inspection.
        /// </summary>
        public async Task<bool> UpdateAssetConditionAsync(
            string assetId,
            AssetCondition condition,
            string inspectionNotes = null,
            CancellationToken cancellationToken = default)
        {
            var asset = await _repository.GetAssetAsync(assetId, cancellationToken);
            if (asset == null) return false;

            asset.Condition = condition;
            asset.LastInspectionDate = DateTime.UtcNow;
            asset.LastUpdated = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(inspectionNotes))
            {
                asset.Notes.Add(new AssetNote
                {
                    Date = DateTime.UtcNow,
                    Type = NoteType.Inspection,
                    Content = inspectionNotes,
                    Author = _settings.CurrentUserId
                });
            }

            await _repository.UpdateAssetAsync(asset, cancellationToken);
            return true;
        }

        #endregion

        #region Asset Queries

        /// <summary>
        /// Get asset by ID.
        /// </summary>
        public async Task<Asset> GetAssetAsync(string assetId, CancellationToken cancellationToken = default)
        {
            return await _repository.GetAssetAsync(assetId, cancellationToken);
        }

        /// <summary>
        /// Search assets with filters.
        /// </summary>
        public async Task<AssetSearchResult> SearchAssetsAsync(
            AssetSearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            return await _repository.SearchAssetsAsync(criteria, cancellationToken);
        }

        /// <summary>
        /// Get assets by location.
        /// </summary>
        public async Task<List<Asset>> GetAssetsByLocationAsync(
            AssetLocation location,
            bool includeSubLocations = true,
            CancellationToken cancellationToken = default)
        {
            var criteria = new AssetSearchCriteria
            {
                Building = location.Building,
                Floor = location.Floor,
                Room = includeSubLocations ? null : location.Room
            };

            var result = await _repository.SearchAssetsAsync(criteria, cancellationToken);
            return result.Assets;
        }

        /// <summary>
        /// Get assets by category.
        /// </summary>
        public async Task<List<Asset>> GetAssetsByCategoryAsync(
            AssetCategory category,
            string subCategory = null,
            CancellationToken cancellationToken = default)
        {
            var criteria = new AssetSearchCriteria
            {
                Category = category,
                SubCategory = subCategory
            };

            var result = await _repository.SearchAssetsAsync(criteria, cancellationToken);
            return result.Assets;
        }

        /// <summary>
        /// Get assets requiring attention (warranty expiring, poor condition, etc.).
        /// </summary>
        public async Task<AttentionRequiredResult> GetAssetsRequiringAttentionAsync(
            int warrantyDaysThreshold = 30,
            CancellationToken cancellationToken = default)
        {
            var allAssets = await _repository.GetAllAssetsAsync(cancellationToken);
            var result = new AttentionRequiredResult();

            foreach (var asset in allAssets.Where(a => a.Status == AssetStatus.Active))
            {
                // Warranty expiring soon
                if (asset.WarrantyExpiry.HasValue)
                {
                    var daysUntilExpiry = (asset.WarrantyExpiry.Value - DateTime.UtcNow).Days;
                    if (daysUntilExpiry <= warrantyDaysThreshold && daysUntilExpiry > 0)
                    {
                        result.WarrantyExpiringSoon.Add(new WarrantyAlert
                        {
                            Asset = asset,
                            DaysUntilExpiry = daysUntilExpiry
                        });
                    }
                }

                // Poor condition
                if (asset.Condition == AssetCondition.Poor || asset.Condition == AssetCondition.Critical)
                {
                    result.PoorCondition.Add(asset);
                }

                // End of life approaching
                var remainingLife = _lifecycleAnalyzer.GetRemainingLifePercentage(asset);
                if (remainingLife <= 10)
                {
                    result.EndOfLifeApproaching.Add(new LifecycleAlert
                    {
                        Asset = asset,
                        RemainingLifePercentage = remainingLife
                    });
                }

                // Overdue for inspection
                if (asset.LastInspectionDate.HasValue)
                {
                    var daysSinceInspection = (DateTime.UtcNow - asset.LastInspectionDate.Value).Days;
                    if (daysSinceInspection > _settings.InspectionIntervalDays)
                    {
                        result.OverdueInspection.Add(asset);
                    }
                }
            }

            return result;
        }

        #endregion

        #region Lifecycle Management

        /// <summary>
        /// Perform comprehensive lifecycle analysis.
        /// </summary>
        public async Task<LifecycleAnalysisResult> AnalyzeAssetLifecycleAsync(
            string assetId,
            CancellationToken cancellationToken = default)
        {
            var asset = await _repository.GetAssetAsync(assetId, cancellationToken);
            if (asset == null) return null;

            return await _lifecycleAnalyzer.AnalyzeAsync(asset, cancellationToken);
        }

        /// <summary>
        /// Get replacement recommendations.
        /// </summary>
        public async Task<List<ReplacementRecommendation>> GetReplacementRecommendationsAsync(
            ReplacementCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            var allAssets = await _repository.GetAllAssetsAsync(cancellationToken);
            var recommendations = new List<ReplacementRecommendation>();

            foreach (var asset in allAssets.Where(a => a.Status == AssetStatus.Active))
            {
                var analysis = await _lifecycleAnalyzer.AnalyzeAsync(asset, cancellationToken);

                if (ShouldRecommendReplacement(asset, analysis, criteria))
                {
                    recommendations.Add(new ReplacementRecommendation
                    {
                        Asset = asset,
                        LifecycleAnalysis = analysis,
                        RecommendedAction = DetermineReplacementAction(asset, analysis),
                        Priority = DetermineReplacementPriority(asset, analysis),
                        EstimatedReplacementCost = await EstimateReplacementCostAsync(asset, cancellationToken),
                        RecommendedReplacementDate = CalculateReplacementDate(asset, analysis)
                    });
                }
            }

            return recommendations.OrderByDescending(r => r.Priority).ToList();
        }

        /// <summary>
        /// Generate capital planning forecast.
        /// </summary>
        public async Task<CapitalPlanningForecast> GenerateCapitalForecastAsync(
            int yearsAhead,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating {yearsAhead}-year capital forecast");
            var forecast = new CapitalPlanningForecast { YearsAhead = yearsAhead };

            var allAssets = await _repository.GetAllAssetsAsync(cancellationToken);

            for (int year = 1; year <= yearsAhead; year++)
            {
                var yearForecast = new YearlyForecast { Year = DateTime.UtcNow.Year + year };

                foreach (var asset in allAssets.Where(a => a.Status == AssetStatus.Active))
                {
                    var analysis = await _lifecycleAnalyzer.AnalyzeAsync(asset, cancellationToken);
                    var expectedEndOfLife = asset.InstallationDate.AddYears(asset.ExpectedLifeYears);

                    if (expectedEndOfLife.Year == yearForecast.Year)
                    {
                        var replacementCost = await EstimateReplacementCostAsync(asset, cancellationToken);
                        yearForecast.PlannedReplacements.Add(new PlannedReplacement
                        {
                            Asset = asset,
                            EstimatedCost = replacementCost
                        });
                        yearForecast.TotalReplacementCost += replacementCost;
                    }

                    // Add maintenance costs
                    yearForecast.TotalMaintenanceCost += analysis.AnnualMaintenanceCost;
                }

                forecast.YearlyForecasts.Add(yearForecast);
            }

            forecast.TotalForecastedCost = forecast.YearlyForecasts.Sum(y =>
                y.TotalReplacementCost + y.TotalMaintenanceCost);

            return forecast;
        }

        #endregion

        #region Depreciation & Valuation

        /// <summary>
        /// Calculate current asset value.
        /// </summary>
        public async Task<AssetValuation> CalculateAssetValueAsync(
            string assetId,
            CancellationToken cancellationToken = default)
        {
            var asset = await _repository.GetAssetAsync(assetId, cancellationToken);
            if (asset == null) return null;

            return _depreciationCalc.CalculateValuation(asset);
        }

        /// <summary>
        /// Generate depreciation schedule.
        /// </summary>
        public async Task<DepreciationSchedule> GenerateDepreciationScheduleAsync(
            string assetId,
            CancellationToken cancellationToken = default)
        {
            var asset = await _repository.GetAssetAsync(assetId, cancellationToken);
            if (asset == null) return null;

            return _depreciationCalc.GenerateSchedule(asset);
        }

        /// <summary>
        /// Calculate total portfolio value.
        /// </summary>
        public async Task<PortfolioValuation> CalculatePortfolioValueAsync(
            AssetSearchCriteria filter = null,
            CancellationToken cancellationToken = default)
        {
            var assets = filter != null
                ? (await _repository.SearchAssetsAsync(filter, cancellationToken)).Assets
                : await _repository.GetAllAssetsAsync(cancellationToken);

            var valuation = new PortfolioValuation
            {
                ValuationDate = DateTime.UtcNow,
                TotalAssets = assets.Count
            };

            foreach (var asset in assets.Where(a => a.Status == AssetStatus.Active))
            {
                var assetVal = _depreciationCalc.CalculateValuation(asset);
                valuation.TotalOriginalCost += asset.PurchaseCost;
                valuation.TotalCurrentValue += assetVal.CurrentValue;
                valuation.TotalAccumulatedDepreciation += assetVal.AccumulatedDepreciation;

                // By category
                if (!valuation.ByCategory.ContainsKey(asset.Category))
                    valuation.ByCategory[asset.Category] = new CategoryValuation();

                valuation.ByCategory[asset.Category].AssetCount++;
                valuation.ByCategory[asset.Category].OriginalCost += asset.PurchaseCost;
                valuation.ByCategory[asset.Category].CurrentValue += assetVal.CurrentValue;
            }

            return valuation;
        }

        #endregion

        #region Inventory Management

        /// <summary>
        /// Perform inventory audit.
        /// </summary>
        public async Task<InventoryAuditResult> PerformInventoryAuditAsync(
            AssetLocation location,
            List<ScannedAsset> scannedAssets,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Performing inventory audit at {location.Building}/{location.Floor}");

            var expectedAssets = await GetAssetsByLocationAsync(location, false, cancellationToken);
            var result = new InventoryAuditResult
            {
                AuditDate = DateTime.UtcNow,
                Location = location,
                AuditedBy = _settings.CurrentUserId
            };

            var expectedIds = expectedAssets.Select(a => a.Id).ToHashSet();
            var scannedIds = scannedAssets.Select(s => s.AssetId).ToHashSet();

            // Found assets
            foreach (var scanned in scannedAssets)
            {
                if (expectedIds.Contains(scanned.AssetId))
                {
                    result.VerifiedAssets.Add(scanned.AssetId);
                }
                else
                {
                    // Found but not expected at this location
                    result.UnexpectedAssets.Add(scanned.AssetId);
                }
            }

            // Missing assets
            foreach (var expected in expectedAssets)
            {
                if (!scannedIds.Contains(expected.Id))
                {
                    result.MissingAssets.Add(expected.Id);
                }
            }

            result.AccuracyPercentage = expectedAssets.Count > 0
                ? (result.VerifiedAssets.Count * 100.0) / expectedAssets.Count
                : 100;

            // Save audit record
            await _repository.SaveAuditResultAsync(result, cancellationToken);

            return result;
        }

        /// <summary>
        /// Get inventory summary by location.
        /// </summary>
        public async Task<InventorySummary> GetInventorySummaryAsync(
            string building = null,
            CancellationToken cancellationToken = default)
        {
            return await _inventoryManager.GetSummaryAsync(building, cancellationToken);
        }

        #endregion

        #region Reporting

        /// <summary>
        /// Generate comprehensive asset report.
        /// </summary>
        public async Task<AssetReport> GenerateAssetReportAsync(
            AssetReportOptions options,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating asset report: {options.ReportType}");

            var report = new AssetReport
            {
                GeneratedDate = DateTime.UtcNow,
                ReportType = options.ReportType,
                GeneratedBy = _settings.CurrentUserId
            };

            var assets = options.Filter != null
                ? (await _repository.SearchAssetsAsync(options.Filter, cancellationToken)).Assets
                : await _repository.GetAllAssetsAsync(cancellationToken);

            switch (options.ReportType)
            {
                case AssetReportType.FullInventory:
                    report.Sections.Add(await GenerateFullInventorySectionAsync(assets, cancellationToken));
                    break;
                case AssetReportType.Valuation:
                    report.Sections.Add(await GenerateValuationSectionAsync(assets, cancellationToken));
                    break;
                case AssetReportType.Lifecycle:
                    report.Sections.Add(await GenerateLifecycleSectionAsync(assets, cancellationToken));
                    break;
                case AssetReportType.Maintenance:
                    report.Sections.Add(await GenerateMaintenanceSectionAsync(assets, cancellationToken));
                    break;
                case AssetReportType.Executive:
                    report.Sections.Add(await GenerateExecutiveSummaryAsync(assets, cancellationToken));
                    break;
            }

            return report;
        }

        #endregion

        #region Private Methods

        private string GenerateAssetId(AssetRegistration registration)
        {
            var prefix = registration.Category.ToString().Substring(0, 3).ToUpper();
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
            var random = new Random().Next(1000, 9999);
            return $"{prefix}-{timestamp}-{random}";
        }

        private string GenerateBarCode()
        {
            return $"STG{DateTime.UtcNow.Ticks.ToString().Substring(10)}";
        }

        private string GenerateQRCode(AssetRegistration registration)
        {
            return $"QR-{registration.Category}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
        }

        private async Task<Dictionary<string, object>> ExtractBIMParametersAsync(
            string elementId, CancellationToken cancellationToken)
        {
            // Would integrate with Revit API
            return await Task.FromResult(new Dictionary<string, object>());
        }

        private AssetRegistration CreateRegistrationFromBIM(BIMElement element, AssetRegistrationOptions options)
        {
            return new AssetRegistration
            {
                Name = element.Name,
                Description = element.Description,
                Category = MapElementToCategory(element),
                Manufacturer = element.GetParameterValue("Manufacturer")?.ToString(),
                Model = element.GetParameterValue("Model")?.ToString(),
                BIMElementId = element.Id,
                Location = new AssetLocation
                {
                    Building = element.Building,
                    Floor = element.Level,
                    Room = element.Room
                },
                InstallationDate = options.DefaultInstallationDate ?? DateTime.UtcNow,
                ExpectedLifeYears = options.DefaultLifeYears
            };
        }

        private AssetCategory MapElementToCategory(BIMElement element)
        {
            return element.Category switch
            {
                "Mechanical Equipment" => AssetCategory.HVAC,
                "Electrical Equipment" => AssetCategory.Electrical,
                "Plumbing Fixtures" => AssetCategory.Plumbing,
                "Furniture" => AssetCategory.Furniture,
                "Fire Protection" => AssetCategory.FireSafety,
                _ => AssetCategory.Equipment
            };
        }

        private bool ShouldRecommendReplacement(Asset asset, LifecycleAnalysisResult analysis, ReplacementCriteria criteria)
        {
            if (analysis.RemainingLifePercentage <= criteria.LifeRemainingThreshold) return true;
            if (asset.Condition == AssetCondition.Critical) return true;
            if (analysis.AnnualMaintenanceCost > asset.PurchaseCost * (decimal)criteria.MaintenanceCostRatio) return true;
            return false;
        }

        private ReplacementAction DetermineReplacementAction(Asset asset, LifecycleAnalysisResult analysis)
        {
            if (asset.Condition == AssetCondition.Critical)
                return ReplacementAction.ImmediateReplacement;
            if (analysis.RemainingLifePercentage <= 5)
                return ReplacementAction.PlannedReplacement;
            if (analysis.RemainingLifePercentage <= 20)
                return ReplacementAction.BudgetForReplacement;
            return ReplacementAction.MonitorAndMaintain;
        }

        private ReplacementPriority DetermineReplacementPriority(Asset asset, LifecycleAnalysisResult analysis)
        {
            if (asset.Condition == AssetCondition.Critical) return ReplacementPriority.Critical;
            if (analysis.RemainingLifePercentage <= 5) return ReplacementPriority.High;
            if (analysis.RemainingLifePercentage <= 20) return ReplacementPriority.Medium;
            return ReplacementPriority.Low;
        }

        private async Task<decimal> EstimateReplacementCostAsync(Asset asset, CancellationToken cancellationToken)
        {
            // Estimate based on original cost with inflation
            var yearsOld = (DateTime.UtcNow - asset.PurchaseDate).Days / 365.0;
            var inflationRate = 0.03; // 3% annual
            var inflatedCost = asset.PurchaseCost * (decimal)Math.Pow(1 + inflationRate, yearsOld);
            return await Task.FromResult(Math.Round(inflatedCost, 2));
        }

        private DateTime CalculateReplacementDate(Asset asset, LifecycleAnalysisResult analysis)
        {
            var remainingYears = (asset.ExpectedLifeYears * analysis.RemainingLifePercentage) / 100.0;
            return DateTime.UtcNow.AddYears((int)Math.Ceiling(remainingYears));
        }

        private async Task<ReportSection> GenerateFullInventorySectionAsync(List<Asset> assets, CancellationToken ct)
        {
            return await Task.FromResult(new ReportSection
            {
                Title = "Full Asset Inventory",
                Data = assets.Select(a => new Dictionary<string, object>
                {
                    ["ID"] = a.Id,
                    ["Name"] = a.Name,
                    ["Category"] = a.Category.ToString(),
                    ["Location"] = $"{a.Location.Building}/{a.Location.Floor}/{a.Location.Room}",
                    ["Status"] = a.Status.ToString(),
                    ["Condition"] = a.Condition.ToString(),
                    ["Value"] = a.CurrentValue
                }).ToList()
            });
        }

        private async Task<ReportSection> GenerateValuationSectionAsync(List<Asset> assets, CancellationToken ct)
        {
            var portfolio = await CalculatePortfolioValueAsync(null, ct);
            return new ReportSection
            {
                Title = "Asset Valuation Summary",
                Summary = new Dictionary<string, object>
                {
                    ["TotalAssets"] = portfolio.TotalAssets,
                    ["TotalOriginalCost"] = portfolio.TotalOriginalCost,
                    ["TotalCurrentValue"] = portfolio.TotalCurrentValue,
                    ["TotalDepreciation"] = portfolio.TotalAccumulatedDepreciation
                }
            };
        }

        private async Task<ReportSection> GenerateLifecycleSectionAsync(List<Asset> assets, CancellationToken ct)
        {
            var recommendations = await GetReplacementRecommendationsAsync(new ReplacementCriteria(), ct);
            return new ReportSection
            {
                Title = "Lifecycle Analysis",
                Data = recommendations.Select(r => new Dictionary<string, object>
                {
                    ["Asset"] = r.Asset.Name,
                    ["RemainingLife"] = $"{r.LifecycleAnalysis.RemainingLifePercentage:F0}%",
                    ["Action"] = r.RecommendedAction.ToString(),
                    ["Priority"] = r.Priority.ToString()
                }).ToList()
            };
        }

        private async Task<ReportSection> GenerateMaintenanceSectionAsync(List<Asset> assets, CancellationToken ct)
        {
            var attention = await GetAssetsRequiringAttentionAsync(30, ct);
            return new ReportSection
            {
                Title = "Maintenance Overview",
                Summary = new Dictionary<string, object>
                {
                    ["OverdueInspections"] = attention.OverdueInspection.Count,
                    ["PoorCondition"] = attention.PoorCondition.Count,
                    ["WarrantyExpiring"] = attention.WarrantyExpiringSoon.Count
                }
            };
        }

        private async Task<ReportSection> GenerateExecutiveSummaryAsync(List<Asset> assets, CancellationToken ct)
        {
            var portfolio = await CalculatePortfolioValueAsync(null, ct);
            var attention = await GetAssetsRequiringAttentionAsync(30, ct);

            return new ReportSection
            {
                Title = "Executive Summary",
                Summary = new Dictionary<string, object>
                {
                    ["TotalAssets"] = assets.Count,
                    ["ActiveAssets"] = assets.Count(a => a.Status == AssetStatus.Active),
                    ["TotalValue"] = portfolio.TotalCurrentValue,
                    ["RequiringAttention"] = attention.TotalCount
                }
            };
        }

        #endregion
    }

    #region Supporting Classes

    internal class AssetRepository
    {
        private readonly List<Asset> _assets = new();
        private readonly List<InventoryAuditResult> _audits = new();

        public Task AddAssetAsync(Asset asset, CancellationToken ct)
        {
            _assets.Add(asset);
            return Task.CompletedTask;
        }

        public Task UpdateAssetAsync(Asset asset, CancellationToken ct)
        {
            var index = _assets.FindIndex(a => a.Id == asset.Id);
            if (index >= 0) _assets[index] = asset;
            return Task.CompletedTask;
        }

        public Task<Asset> GetAssetAsync(string id, CancellationToken ct)
        {
            return Task.FromResult(_assets.FirstOrDefault(a => a.Id == id));
        }

        public Task<List<Asset>> GetAllAssetsAsync(CancellationToken ct)
        {
            return Task.FromResult(_assets.ToList());
        }

        public Task<AssetSearchResult> SearchAssetsAsync(AssetSearchCriteria criteria, CancellationToken ct)
        {
            var query = _assets.AsQueryable();

            if (criteria.Category.HasValue)
                query = query.Where(a => a.Category == criteria.Category.Value);
            if (!string.IsNullOrEmpty(criteria.SubCategory))
                query = query.Where(a => a.SubCategory == criteria.SubCategory);
            if (!string.IsNullOrEmpty(criteria.Building))
                query = query.Where(a => a.Location.Building == criteria.Building);
            if (!string.IsNullOrEmpty(criteria.Floor))
                query = query.Where(a => a.Location.Floor == criteria.Floor);
            if (!string.IsNullOrEmpty(criteria.Room))
                query = query.Where(a => a.Location.Room == criteria.Room);
            if (criteria.Status.HasValue)
                query = query.Where(a => a.Status == criteria.Status.Value);

            return Task.FromResult(new AssetSearchResult { Assets = query.ToList() });
        }

        public Task SaveAuditResultAsync(InventoryAuditResult result, CancellationToken ct)
        {
            _audits.Add(result);
            return Task.CompletedTask;
        }
    }

    internal class LifecycleAnalyzer
    {
        private readonly AssetSettings _settings;

        public LifecycleAnalyzer(AssetSettings settings) => _settings = settings;

        public async Task<LifecycleAnalysisResult> AnalyzeAsync(Asset asset, CancellationToken ct)
        {
            var ageYears = (DateTime.UtcNow - asset.InstallationDate).Days / 365.0;
            var remainingLife = Math.Max(0, 100 - (ageYears / asset.ExpectedLifeYears * 100));

            return await Task.FromResult(new LifecycleAnalysisResult
            {
                AssetId = asset.Id,
                AgeYears = ageYears,
                ExpectedLifeYears = asset.ExpectedLifeYears,
                RemainingLifePercentage = remainingLife,
                AnnualMaintenanceCost = asset.PurchaseCost * 0.05m, // 5% estimate
                ConditionScore = GetConditionScore(asset.Condition),
                LifecycleStage = DetermineStage(remainingLife)
            });
        }

        public double GetRemainingLifePercentage(Asset asset)
        {
            var ageYears = (DateTime.UtcNow - asset.InstallationDate).Days / 365.0;
            return Math.Max(0, 100 - (ageYears / asset.ExpectedLifeYears * 100));
        }

        private int GetConditionScore(AssetCondition condition) => condition switch
        {
            AssetCondition.Excellent => 100,
            AssetCondition.Good => 80,
            AssetCondition.Fair => 60,
            AssetCondition.Poor => 40,
            AssetCondition.Critical => 20,
            _ => 50
        };

        private LifecycleStage DetermineStage(double remainingLife) => remainingLife switch
        {
            > 75 => LifecycleStage.New,
            > 50 => LifecycleStage.Mature,
            > 25 => LifecycleStage.Aging,
            > 10 => LifecycleStage.EndOfLife,
            _ => LifecycleStage.Overdue
        };
    }

    internal class DepreciationCalculator
    {
        public decimal CalculateCurrentValue(Asset asset)
        {
            var valuation = CalculateValuation(asset);
            return valuation.CurrentValue;
        }

        public AssetValuation CalculateValuation(Asset asset)
        {
            var ageYears = (DateTime.UtcNow - asset.PurchaseDate).Days / 365.0;
            var depreciableAmount = asset.PurchaseCost - asset.SalvageValue;
            decimal accumulatedDepreciation;

            switch (asset.DepreciationMethod)
            {
                case DepreciationMethod.StraightLine:
                    var annualDepreciation = depreciableAmount / asset.ExpectedLifeYears;
                    accumulatedDepreciation = Math.Min(annualDepreciation * (decimal)ageYears, depreciableAmount);
                    break;
                case DepreciationMethod.DecliningBalance:
                    var rate = 2.0 / asset.ExpectedLifeYears;
                    accumulatedDepreciation = asset.PurchaseCost * (1 - (decimal)Math.Pow(1 - rate, ageYears));
                    accumulatedDepreciation = Math.Min(accumulatedDepreciation, depreciableAmount);
                    break;
                default:
                    accumulatedDepreciation = 0;
                    break;
            }

            return new AssetValuation
            {
                AssetId = asset.Id,
                ValuationDate = DateTime.UtcNow,
                OriginalCost = asset.PurchaseCost,
                SalvageValue = asset.SalvageValue,
                AccumulatedDepreciation = accumulatedDepreciation,
                CurrentValue = asset.PurchaseCost - accumulatedDepreciation,
                Method = asset.DepreciationMethod
            };
        }

        public DepreciationSchedule GenerateSchedule(Asset asset)
        {
            var schedule = new DepreciationSchedule { AssetId = asset.Id };
            var depreciableAmount = asset.PurchaseCost - asset.SalvageValue;
            var annualDepreciation = depreciableAmount / asset.ExpectedLifeYears;
            var bookValue = asset.PurchaseCost;

            for (int year = 1; year <= asset.ExpectedLifeYears; year++)
            {
                var entry = new DepreciationEntry
                {
                    Year = year,
                    YearDate = asset.PurchaseDate.AddYears(year),
                    OpeningValue = bookValue,
                    Depreciation = annualDepreciation,
                    ClosingValue = bookValue - annualDepreciation,
                    AccumulatedDepreciation = annualDepreciation * year
                };
                schedule.Entries.Add(entry);
                bookValue = entry.ClosingValue;
            }

            return schedule;
        }
    }

    internal class InventoryManager
    {
        public async Task<InventorySummary> GetSummaryAsync(string building, CancellationToken ct)
        {
            return await Task.FromResult(new InventorySummary { Building = building });
        }
    }

    #endregion

    #region Data Models

    public class AssetSettings
    {
        public string CurrentUserId { get; set; } = "System";
        public int InspectionIntervalDays { get; set; } = 365;
    }

    public class Asset
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public AssetCategory Category { get; set; }
        public string SubCategory { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string BarCode { get; set; }
        public string QRCode { get; set; }
        public string BIMElementId { get; set; }
        public Dictionary<string, object> BIMParameters { get; set; } = new();
        public Dictionary<string, string> Specifications { get; set; } = new();
        public DateTime PurchaseDate { get; set; }
        public DateTime InstallationDate { get; set; }
        public DateTime? WarrantyExpiry { get; set; }
        public DateTime? DisposalDate { get; set; }
        public DateTime? LastInspectionDate { get; set; }
        public decimal PurchaseCost { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal SalvageValue { get; set; }
        public int ExpectedLifeYears { get; set; }
        public DepreciationMethod DepreciationMethod { get; set; }
        public AssetLocation Location { get; set; }
        public AssetStatus Status { get; set; }
        public AssetCondition Condition { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<AssetMovement> MovementHistory { get; } = new();
        public List<StatusChange> StatusHistory { get; } = new();
        public List<AssetNote> Notes { get; } = new();
    }

    public class AssetRegistration
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public AssetCategory Category { get; set; }
        public string SubCategory { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string BarCode { get; set; }
        public string BIMElementId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime InstallationDate { get; set; }
        public DateTime? WarrantyExpiry { get; set; }
        public decimal PurchaseCost { get; set; }
        public decimal? SalvageValue { get; set; }
        public int ExpectedLifeYears { get; set; } = 15;
        public DepreciationMethod? DepreciationMethod { get; set; }
        public AssetLocation Location { get; set; }
        public Dictionary<string, string> Specifications { get; set; }
    }

    public class AssetLocation
    {
        public string Building { get; set; }
        public string Floor { get; set; }
        public string Room { get; set; }
        public string Zone { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Z { get; set; }
    }

    public class AssetMovement
    {
        public string AssetId { get; set; }
        public AssetLocation FromLocation { get; set; }
        public AssetLocation ToLocation { get; set; }
        public DateTime MovementDate { get; set; }
        public string Reason { get; set; }
        public string MovedBy { get; set; }
    }

    public class StatusChange
    {
        public string AssetId { get; set; }
        public AssetStatus FromStatus { get; set; }
        public AssetStatus ToStatus { get; set; }
        public DateTime ChangeDate { get; set; }
        public string Notes { get; set; }
        public string ChangedBy { get; set; }
    }

    public class AssetNote
    {
        public DateTime Date { get; set; }
        public NoteType Type { get; set; }
        public string Content { get; set; }
        public string Author { get; set; }
    }

    public class AssetRegistrationResult
    {
        public bool Success { get; set; }
        public Asset Asset { get; set; }
        public string AssetId { get; set; }
        public string BarCode { get; set; }
        public string QRCode { get; set; }
        public string Error { get; set; }
    }

    public class BulkRegistrationResult
    {
        public int TotalProcessed { get; set; }
        public List<Asset> RegisteredAssets { get; } = new();
        public List<string> FailedElements { get; } = new();
    }

    public class AssetSearchCriteria
    {
        public AssetCategory? Category { get; set; }
        public string SubCategory { get; set; }
        public string Building { get; set; }
        public string Floor { get; set; }
        public string Room { get; set; }
        public AssetStatus? Status { get; set; }
        public string SearchText { get; set; }
    }

    public class AssetSearchResult
    {
        public List<Asset> Assets { get; set; } = new();
        public int TotalCount => Assets.Count;
    }

    public class AssetProgress
    {
        public string Phase { get; set; }
        public int PercentComplete { get; set; }
        public string CurrentAsset { get; set; }
    }

    public class AttentionRequiredResult
    {
        public List<WarrantyAlert> WarrantyExpiringSoon { get; } = new();
        public List<Asset> PoorCondition { get; } = new();
        public List<LifecycleAlert> EndOfLifeApproaching { get; } = new();
        public List<Asset> OverdueInspection { get; } = new();
        public int TotalCount => WarrantyExpiringSoon.Count + PoorCondition.Count +
                                  EndOfLifeApproaching.Count + OverdueInspection.Count;
    }

    public class WarrantyAlert { public Asset Asset { get; set; } public int DaysUntilExpiry { get; set; } }
    public class LifecycleAlert { public Asset Asset { get; set; } public double RemainingLifePercentage { get; set; } }

    public class LifecycleAnalysisResult
    {
        public string AssetId { get; set; }
        public double AgeYears { get; set; }
        public int ExpectedLifeYears { get; set; }
        public double RemainingLifePercentage { get; set; }
        public decimal AnnualMaintenanceCost { get; set; }
        public int ConditionScore { get; set; }
        public LifecycleStage LifecycleStage { get; set; }
    }

    public class ReplacementRecommendation
    {
        public Asset Asset { get; set; }
        public LifecycleAnalysisResult LifecycleAnalysis { get; set; }
        public ReplacementAction RecommendedAction { get; set; }
        public ReplacementPriority Priority { get; set; }
        public decimal EstimatedReplacementCost { get; set; }
        public DateTime RecommendedReplacementDate { get; set; }
    }

    public class ReplacementCriteria
    {
        public double LifeRemainingThreshold { get; set; } = 20;
        public double MaintenanceCostRatio { get; set; } = 0.5;
    }

    public class CapitalPlanningForecast
    {
        public int YearsAhead { get; set; }
        public List<YearlyForecast> YearlyForecasts { get; } = new();
        public decimal TotalForecastedCost { get; set; }
    }

    public class YearlyForecast
    {
        public int Year { get; set; }
        public List<PlannedReplacement> PlannedReplacements { get; } = new();
        public decimal TotalReplacementCost { get; set; }
        public decimal TotalMaintenanceCost { get; set; }
    }

    public class PlannedReplacement
    {
        public Asset Asset { get; set; }
        public decimal EstimatedCost { get; set; }
    }

    public class AssetValuation
    {
        public string AssetId { get; set; }
        public DateTime ValuationDate { get; set; }
        public decimal OriginalCost { get; set; }
        public decimal SalvageValue { get; set; }
        public decimal AccumulatedDepreciation { get; set; }
        public decimal CurrentValue { get; set; }
        public DepreciationMethod Method { get; set; }
    }

    public class DepreciationSchedule
    {
        public string AssetId { get; set; }
        public List<DepreciationEntry> Entries { get; } = new();
    }

    public class DepreciationEntry
    {
        public int Year { get; set; }
        public DateTime YearDate { get; set; }
        public decimal OpeningValue { get; set; }
        public decimal Depreciation { get; set; }
        public decimal ClosingValue { get; set; }
        public decimal AccumulatedDepreciation { get; set; }
    }

    public class PortfolioValuation
    {
        public DateTime ValuationDate { get; set; }
        public int TotalAssets { get; set; }
        public decimal TotalOriginalCost { get; set; }
        public decimal TotalCurrentValue { get; set; }
        public decimal TotalAccumulatedDepreciation { get; set; }
        public Dictionary<AssetCategory, CategoryValuation> ByCategory { get; } = new();
    }

    public class CategoryValuation
    {
        public int AssetCount { get; set; }
        public decimal OriginalCost { get; set; }
        public decimal CurrentValue { get; set; }
    }

    public class InventoryAuditResult
    {
        public DateTime AuditDate { get; set; }
        public AssetLocation Location { get; set; }
        public string AuditedBy { get; set; }
        public List<string> VerifiedAssets { get; } = new();
        public List<string> MissingAssets { get; } = new();
        public List<string> UnexpectedAssets { get; } = new();
        public double AccuracyPercentage { get; set; }
    }

    public class ScannedAsset
    {
        public string AssetId { get; set; }
        public string BarCode { get; set; }
        public DateTime ScannedAt { get; set; }
    }

    public class InventorySummary
    {
        public string Building { get; set; }
        public int TotalAssets { get; set; }
        public Dictionary<string, int> ByFloor { get; } = new();
        public Dictionary<AssetCategory, int> ByCategory { get; } = new();
    }

    public class AssetReport
    {
        public DateTime GeneratedDate { get; set; }
        public AssetReportType ReportType { get; set; }
        public string GeneratedBy { get; set; }
        public List<ReportSection> Sections { get; } = new();
    }

    public class ReportSection
    {
        public string Title { get; set; }
        public string SectionName { get; set; }
        public Dictionary<string, object> Summary { get; set; }
        public List<Dictionary<string, object>> Data { get; set; }
        public List<ReportItem> Items { get; set; }
    }

    public class ReportItem
    {
        public string Question { get; set; }
        public string Response { get; set; }
        public bool? Pass { get; set; }
        public string Notes { get; set; }
        public List<string> Photos { get; set; }
    }

    public class AssetReportOptions
    {
        public AssetReportType ReportType { get; set; }
        public AssetSearchCriteria Filter { get; set; }
    }

    public class AssetRegistrationOptions
    {
        public DateTime? DefaultInstallationDate { get; set; }
        public int DefaultLifeYears { get; set; } = 15;
    }

    public class BIMModel
    {
        public string Id { get; set; }
        public List<BIMElement> GetEquipmentElements() => new();
        public List<BIMRoom> GetRooms() => new();
    }

    public class BIMElement
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Building { get; set; }
        public string Level { get; set; }
        public string Room { get; set; }
        public object GetParameterValue(string name) => null;
    }

    // Enums
    public enum AssetCategory { HVAC, Electrical, Plumbing, FireSafety, Elevator, Furniture, IT, Security, Equipment, Other }
    public enum AssetStatus { Active, InMaintenance, Disposed, Retired, Lost, Reserved }
    public enum AssetCondition { Excellent, Good, Fair, Poor, Critical }
    public enum DepreciationMethod { StraightLine, DecliningBalance, SumOfYears, UnitsOfProduction }
    public enum NoteType { General, Inspection, Maintenance, Repair, Warranty }
    public enum LifecycleStage { New, Mature, Aging, EndOfLife, Overdue }
    public enum ReplacementAction { MonitorAndMaintain, BudgetForReplacement, PlannedReplacement, ImmediateReplacement }
    public enum ReplacementPriority { Low, Medium, High, Critical }
    public enum AssetReportType { FullInventory, Valuation, Lifecycle, Maintenance, Executive }

    #endregion
}
