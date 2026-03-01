// ===================================================================
// StingBIM Procurement Intelligence Engine
// Supplier management, bid analysis, and procurement optimization
// Copyright (c) 2026 StingBIM. All rights reserved.
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.ProcurementIntelligence
{
    /// <summary>
    /// Comprehensive procurement intelligence for supplier management,
    /// bid analysis, contract management, and procurement planning
    /// </summary>
    public sealed class ProcurementIntelligenceEngine
    {
        private static readonly Lazy<ProcurementIntelligenceEngine> _instance =
            new Lazy<ProcurementIntelligenceEngine>(() => new ProcurementIntelligenceEngine());
        public static ProcurementIntelligenceEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, ProcurementProject> _projects;
        private readonly ConcurrentDictionary<string, Supplier> _suppliers;
        private readonly ConcurrentDictionary<string, BidPackage> _bidPackages;
        private readonly ConcurrentDictionary<string, Contract> _contracts;
        private readonly object _lockObject = new object();

        public event EventHandler<ProcurementAlertEventArgs> ProcurementAlertRaised;

        private ProcurementIntelligenceEngine()
        {
            _projects = new ConcurrentDictionary<string, ProcurementProject>();
            _suppliers = new ConcurrentDictionary<string, Supplier>();
            _bidPackages = new ConcurrentDictionary<string, BidPackage>();
            _contracts = new ConcurrentDictionary<string, Contract>();

            InitializeSampleSuppliers();
        }

        #region Initialization

        private void InitializeSampleSuppliers()
        {
            var suppliers = new List<Supplier>
            {
                new Supplier
                {
                    Id = "SUP-001",
                    Name = "ABC Concrete Supply",
                    Category = "Concrete",
                    Specialties = new List<string> { "Ready-mix concrete", "Precast elements", "Concrete pumping" },
                    Location = "Regional",
                    Rating = 4.5m,
                    QualityScore = 92,
                    DeliveryScore = 88,
                    SafetyScore = 95,
                    FinancialRating = "A",
                    YearsInBusiness = 25,
                    InsuranceVerified = true,
                    PrequalificationStatus = PrequalificationStatus.Approved,
                    Certifications = new List<string> { "ISO 9001", "LEED Supplier" }
                },
                new Supplier
                {
                    Id = "SUP-002",
                    Name = "Steel Structures Inc",
                    Category = "Structural Steel",
                    Specialties = new List<string> { "Structural fabrication", "Erection", "Miscellaneous metals" },
                    Location = "National",
                    Rating = 4.2m,
                    QualityScore = 90,
                    DeliveryScore = 85,
                    SafetyScore = 92,
                    FinancialRating = "A",
                    YearsInBusiness = 35,
                    InsuranceVerified = true,
                    PrequalificationStatus = PrequalificationStatus.Approved,
                    Certifications = new List<string> { "AISC Certified", "ISO 9001" }
                },
                new Supplier
                {
                    Id = "SUP-003",
                    Name = "MEP Systems Co",
                    Category = "MEP",
                    Specialties = new List<string> { "HVAC", "Plumbing", "Fire protection" },
                    Location = "Regional",
                    Rating = 4.0m,
                    QualityScore = 88,
                    DeliveryScore = 82,
                    SafetyScore = 90,
                    FinancialRating = "B+",
                    YearsInBusiness = 15,
                    InsuranceVerified = true,
                    PrequalificationStatus = PrequalificationStatus.Approved,
                    Certifications = new List<string> { "NFPA Certified", "EPA Certified" }
                },
                new Supplier
                {
                    Id = "SUP-004",
                    Name = "Elite Electrical",
                    Category = "Electrical",
                    Specialties = new List<string> { "Power distribution", "Lighting", "Low voltage" },
                    Location = "Regional",
                    Rating = 4.3m,
                    QualityScore = 91,
                    DeliveryScore = 87,
                    SafetyScore = 94,
                    FinancialRating = "A-",
                    YearsInBusiness = 20,
                    InsuranceVerified = true,
                    PrequalificationStatus = PrequalificationStatus.Approved,
                    Certifications = new List<string> { "NECA Member", "OSHA VPP" }
                },
                new Supplier
                {
                    Id = "SUP-005",
                    Name = "Glazing Solutions",
                    Category = "Glazing",
                    Specialties = new List<string> { "Curtain wall", "Storefronts", "Skylights" },
                    Location = "National",
                    Rating = 4.1m,
                    QualityScore = 89,
                    DeliveryScore = 80,
                    SafetyScore = 88,
                    FinancialRating = "B+",
                    YearsInBusiness = 18,
                    InsuranceVerified = true,
                    PrequalificationStatus = PrequalificationStatus.Approved,
                    Certifications = new List<string> { "GANA Member" }
                }
            };

            foreach (var supplier in suppliers)
            {
                _suppliers.TryAdd(supplier.Id, supplier);
            }
        }

        #endregion

        #region Procurement Planning

        public ProcurementProject CreateProject(ProcurementProjectRequest request)
        {
            var project = new ProcurementProject
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                ProjectValue = request.ProjectValue,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                CreatedDate = DateTime.UtcNow,
                BidPackages = new List<string>(),
                Contracts = new List<string>()
            };

            _projects.TryAdd(project.Id, project);
            return project;
        }

        public ProcurementPlan GenerateProcurementPlan(ProcurementPlanRequest request)
        {
            var plan = new ProcurementPlan
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                CreatedDate = DateTime.UtcNow,
                PackageStrategies = new List<PackageStrategy>(),
                Timeline = new List<ProcurementMilestone>(),
                Recommendations = new List<string>()
            };

            // Define standard bid packages based on project type
            var packages = GenerateStandardPackages(request.ProjectType, request.ProjectValue);

            foreach (var package in packages)
            {
                var strategy = new PackageStrategy
                {
                    PackageName = package.Name,
                    Scope = package.Scope,
                    EstimatedValue = request.ProjectValue * package.ValuePercent,
                    ProcurementMethod = DetermineProcurementMethod(request.ProjectValue * package.ValuePercent),
                    ContractType = DetermineContractType(package.Category),
                    BiddersRequired = DetermineBiddersRequired(request.ProjectValue * package.ValuePercent),
                    LeadTime = package.LeadTime,
                    CriticalPath = package.CriticalPath,
                    PrequalificationRequired = request.ProjectValue * package.ValuePercent > 500000
                };

                // Calculate procurement dates
                strategy.BidDueDate = request.RequiredOnSiteDate.AddDays(-package.LeadTime - 30);
                strategy.AwardDate = strategy.BidDueDate.AddDays(14);
                strategy.ContractDate = strategy.AwardDate.AddDays(7);
                strategy.SubmittalDate = strategy.ContractDate.AddDays(21);
                strategy.DeliveryDate = request.RequiredOnSiteDate;

                plan.PackageStrategies.Add(strategy);
            }

            // Generate timeline milestones
            plan.Timeline = GenerateProcurementTimeline(plan.PackageStrategies, request.StartDate);

            // Recommendations
            GeneratePlanRecommendations(plan, request);

            return plan;
        }

        private List<StandardPackage> GenerateStandardPackages(string projectType, decimal projectValue)
        {
            var packages = new List<StandardPackage>
            {
                new StandardPackage { Name = "Sitework & Earthwork", Category = "Sitework", Scope = "Site preparation, excavation, grading", ValuePercent = 0.05m, LeadTime = 30, CriticalPath = true },
                new StandardPackage { Name = "Concrete", Category = "Concrete", Scope = "Foundations, structural concrete, flatwork", ValuePercent = 0.12m, LeadTime = 45, CriticalPath = true },
                new StandardPackage { Name = "Structural Steel", Category = "Steel", Scope = "Structural steel fabrication and erection", ValuePercent = 0.10m, LeadTime = 90, CriticalPath = true },
                new StandardPackage { Name = "Masonry", Category = "Masonry", Scope = "CMU walls, brick veneer", ValuePercent = 0.04m, LeadTime = 45, CriticalPath = false },
                new StandardPackage { Name = "Roofing", Category = "Roofing", Scope = "Roofing systems, insulation, flashing", ValuePercent = 0.03m, LeadTime = 60, CriticalPath = false },
                new StandardPackage { Name = "Curtain Wall & Glazing", Category = "Glazing", Scope = "Curtain wall, windows, storefronts", ValuePercent = 0.08m, LeadTime = 120, CriticalPath = true },
                new StandardPackage { Name = "Drywall & Ceilings", Category = "Finishes", Scope = "Metal framing, drywall, ACT ceilings", ValuePercent = 0.06m, LeadTime = 30, CriticalPath = false },
                new StandardPackage { Name = "Flooring", Category = "Finishes", Scope = "Carpet, VCT, tile, polished concrete", ValuePercent = 0.03m, LeadTime = 45, CriticalPath = false },
                new StandardPackage { Name = "Painting", Category = "Finishes", Scope = "Interior and exterior painting", ValuePercent = 0.02m, LeadTime = 14, CriticalPath = false },
                new StandardPackage { Name = "HVAC", Category = "Mechanical", Scope = "HVAC equipment, ductwork, controls", ValuePercent = 0.12m, LeadTime = 90, CriticalPath = true },
                new StandardPackage { Name = "Plumbing", Category = "Plumbing", Scope = "Plumbing systems, fixtures", ValuePercent = 0.05m, LeadTime = 60, CriticalPath = false },
                new StandardPackage { Name = "Fire Protection", Category = "Fire", Scope = "Sprinkler systems, fire alarm", ValuePercent = 0.03m, LeadTime = 60, CriticalPath = false },
                new StandardPackage { Name = "Electrical", Category = "Electrical", Scope = "Power, lighting, low voltage", ValuePercent = 0.10m, LeadTime = 60, CriticalPath = true },
                new StandardPackage { Name = "Elevators", Category = "Conveying", Scope = "Elevators, escalators", ValuePercent = 0.04m, LeadTime = 180, CriticalPath = true },
                new StandardPackage { Name = "Specialties", Category = "Specialties", Scope = "Toilet accessories, signage, etc.", ValuePercent = 0.02m, LeadTime = 45, CriticalPath = false }
            };

            return packages;
        }

        private ProcurementMethod DetermineProcurementMethod(decimal value)
        {
            if (value > 1000000) return ProcurementMethod.CompetitiveBid;
            if (value > 100000) return ProcurementMethod.InvitedBid;
            return ProcurementMethod.NegotiatedContract;
        }

        private ContractType DetermineContractType(string category)
        {
            return category switch
            {
                "Steel" or "Glazing" or "Conveying" => ContractType.LumpSum,
                "Sitework" => ContractType.UnitPrice,
                _ => ContractType.LumpSum
            };
        }

        private int DetermineBiddersRequired(decimal value)
        {
            if (value > 500000) return 5;
            if (value > 100000) return 3;
            return 2;
        }

        private List<ProcurementMilestone> GenerateProcurementTimeline(List<PackageStrategy> strategies, DateTime projectStart)
        {
            var milestones = new List<ProcurementMilestone>();

            foreach (var strategy in strategies.OrderBy(s => s.BidDueDate))
            {
                milestones.Add(new ProcurementMilestone
                {
                    PackageName = strategy.PackageName,
                    Milestone = "Issue Bid",
                    Date = strategy.BidDueDate.AddDays(-21),
                    IsCritical = strategy.CriticalPath
                });

                milestones.Add(new ProcurementMilestone
                {
                    PackageName = strategy.PackageName,
                    Milestone = "Bid Due",
                    Date = strategy.BidDueDate,
                    IsCritical = strategy.CriticalPath
                });

                milestones.Add(new ProcurementMilestone
                {
                    PackageName = strategy.PackageName,
                    Milestone = "Award",
                    Date = strategy.AwardDate,
                    IsCritical = strategy.CriticalPath
                });
            }

            return milestones.OrderBy(m => m.Date).ToList();
        }

        private void GeneratePlanRecommendations(ProcurementPlan plan, ProcurementPlanRequest request)
        {
            var criticalPackages = plan.PackageStrategies.Where(p => p.CriticalPath).ToList();

            if (criticalPackages.Any(p => p.LeadTime > 90))
            {
                plan.Recommendations.Add("Long-lead items identified - consider early release packages for structural steel, glazing, and elevators");
            }

            if (request.ProjectValue > 10000000)
            {
                plan.Recommendations.Add("Large project value - implement formal prequalification process for major trades");
            }

            if (plan.PackageStrategies.Count > 15)
            {
                plan.Recommendations.Add("Consider combining smaller packages to reduce procurement overhead");
            }

            plan.Recommendations.Add("Establish clear evaluation criteria before issuing bids");
            plan.Recommendations.Add("Include escalation provisions in contracts for materials with volatile pricing");
        }

        #endregion

        #region Bid Package Management

        public BidPackage CreateBidPackage(BidPackageRequest request)
        {
            var package = new BidPackage
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                Name = request.Name,
                Number = request.Number,
                Scope = request.Scope,
                EstimatedValue = request.EstimatedValue,
                BidDate = request.BidDate,
                Status = BidPackageStatus.Draft,
                CreatedDate = DateTime.UtcNow,
                InvitedBidders = new List<string>(),
                Bids = new List<Bid>(),
                Questions = new List<BidQuestion>(),
                Addenda = new List<Addendum>()
            };

            _bidPackages.TryAdd(package.Id, package);

            if (_projects.TryGetValue(request.ProjectId, out var project))
            {
                project.BidPackages.Add(package.Id);
            }

            return package;
        }

        public BidPackage IssueBidPackage(string packageId, List<string> invitedSupplierIds)
        {
            if (!_bidPackages.TryGetValue(packageId, out var package))
                return null;

            package.InvitedBidders = invitedSupplierIds;
            package.Status = BidPackageStatus.Issued;
            package.IssueDate = DateTime.UtcNow;

            return package;
        }

        public Bid SubmitBid(string packageId, BidSubmission submission)
        {
            if (!_bidPackages.TryGetValue(packageId, out var package))
                return null;

            var bid = new Bid
            {
                Id = Guid.NewGuid().ToString(),
                SupplierId = submission.SupplierId,
                SupplierName = _suppliers.TryGetValue(submission.SupplierId, out var supplier) ? supplier.Name : "Unknown",
                TotalPrice = submission.TotalPrice,
                AlternatePrice = submission.AlternatePrice,
                SubmittedDate = DateTime.UtcNow,
                Exclusions = submission.Exclusions,
                Clarifications = submission.Clarifications,
                ProposedSchedule = submission.ProposedSchedule,
                Status = BidStatus.Received,
                LineItems = submission.LineItems
            };

            lock (_lockObject)
            {
                package.Bids.Add(bid);

                // Update package status if all bids received
                if (package.Bids.Count >= package.InvitedBidders.Count && DateTime.UtcNow >= package.BidDate)
                {
                    package.Status = BidPackageStatus.UnderEvaluation;
                }
            }

            return bid;
        }

        public BidAnalysis AnalyzeBids(string packageId)
        {
            if (!_bidPackages.TryGetValue(packageId, out var package))
                return null;

            if (!package.Bids.Any())
                return null;

            var analysis = new BidAnalysis
            {
                PackageId = packageId,
                PackageName = package.Name,
                AnalysisDate = DateTime.UtcNow,
                BidCount = package.Bids.Count,
                EstimatedValue = package.EstimatedValue,
                BidComparisons = new List<BidComparison>(),
                Recommendations = new List<string>()
            };

            // Calculate statistics
            var prices = package.Bids.Select(b => b.TotalPrice).OrderBy(p => p).ToList();
            analysis.LowBid = prices.First();
            analysis.HighBid = prices.Last();
            analysis.AverageBid = prices.Average();
            analysis.MedianBid = prices[prices.Count / 2];
            analysis.Spread = (analysis.HighBid - analysis.LowBid) / analysis.LowBid * 100;

            // Compare to estimate
            analysis.LowVsEstimate = (analysis.LowBid - package.EstimatedValue) / package.EstimatedValue * 100;

            // Analyze each bid
            foreach (var bid in package.Bids.OrderBy(b => b.TotalPrice))
            {
                var supplierScore = 0m;
                if (_suppliers.TryGetValue(bid.SupplierId, out var supplier))
                {
                    supplierScore = (supplier.QualityScore + supplier.DeliveryScore + supplier.SafetyScore) / 3.0m;
                }

                var comparison = new BidComparison
                {
                    SupplierId = bid.SupplierId,
                    SupplierName = bid.SupplierName,
                    TotalPrice = bid.TotalPrice,
                    Rank = package.Bids.OrderBy(b => b.TotalPrice).ToList().IndexOf(bid) + 1,
                    VarianceFromLow = (bid.TotalPrice - analysis.LowBid) / analysis.LowBid * 100,
                    VarianceFromEstimate = (bid.TotalPrice - package.EstimatedValue) / package.EstimatedValue * 100,
                    SupplierScore = supplierScore,
                    ExclusionCount = bid.Exclusions?.Count ?? 0,
                    Responsive = bid.Exclusions == null || bid.Exclusions.Count < 3
                };

                // Calculate value score (price + quality)
                var priceFactor = 1 - (comparison.VarianceFromLow / 100);
                var qualityFactor = supplierScore / 100;
                comparison.ValueScore = priceFactor * 0.6m + qualityFactor * 0.4m;

                analysis.BidComparisons.Add(comparison);
            }

            // Identify apparent low bidder
            analysis.ApparentLowBidder = analysis.BidComparisons
                .Where(b => b.Responsive)
                .OrderBy(b => b.TotalPrice)
                .FirstOrDefault()?.SupplierId;

            // Best value bidder
            analysis.BestValueBidder = analysis.BidComparisons
                .Where(b => b.Responsive)
                .OrderByDescending(b => b.ValueScore)
                .FirstOrDefault()?.SupplierId;

            // Generate recommendations
            GenerateBidRecommendations(analysis, package);

            return analysis;
        }

        private void GenerateBidRecommendations(BidAnalysis analysis, BidPackage package)
        {
            if (analysis.Spread > 30)
            {
                analysis.Recommendations.Add("Large spread between bids (>30%) - review scope understanding with bidders");
            }

            if (analysis.LowVsEstimate < -15)
            {
                analysis.Recommendations.Add("Low bid is significantly below estimate - verify scope completeness and bid qualifications");
            }

            if (analysis.LowVsEstimate > 15)
            {
                analysis.Recommendations.Add("All bids exceed estimate by >15% - consider value engineering or scope reduction");
            }

            var nonResponsive = analysis.BidComparisons.Count(b => !b.Responsive);
            if (nonResponsive > 0)
            {
                analysis.Recommendations.Add($"{nonResponsive} bid(s) have significant exclusions - review for responsiveness");
            }

            if (analysis.ApparentLowBidder != analysis.BestValueBidder)
            {
                analysis.Recommendations.Add("Best value bidder differs from low bidder - consider total value approach");
            }

            if (analysis.BidCount < 3)
            {
                analysis.Recommendations.Add("Less than 3 bids received - consider re-bidding for more competitive pricing");
            }
        }

        public Bid AwardBid(string packageId, string bidId, AwardRequest request)
        {
            if (!_bidPackages.TryGetValue(packageId, out var package))
                return null;

            var bid = package.Bids.FirstOrDefault(b => b.Id == bidId);
            if (bid == null) return null;

            bid.Status = BidStatus.Awarded;
            bid.AwardDate = DateTime.UtcNow;
            bid.AwardedBy = request.AwardedBy;
            bid.AwardNotes = request.Notes;

            // Update other bids
            foreach (var otherBid in package.Bids.Where(b => b.Id != bidId))
            {
                otherBid.Status = BidStatus.NotAwarded;
            }

            package.Status = BidPackageStatus.Awarded;
            package.AwardedSupplierId = bid.SupplierId;
            package.AwardedAmount = bid.TotalPrice;

            return bid;
        }

        #endregion

        #region Supplier Management

        public Supplier RegisterSupplier(SupplierRegistrationRequest request)
        {
            var supplier = new Supplier
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Category = request.Category,
                Specialties = request.Specialties,
                Location = request.Location,
                ContactName = request.ContactName,
                ContactEmail = request.ContactEmail,
                ContactPhone = request.ContactPhone,
                Address = request.Address,
                YearsInBusiness = request.YearsInBusiness,
                AnnualRevenue = request.AnnualRevenue,
                EmployeeCount = request.EmployeeCount,
                PrequalificationStatus = PrequalificationStatus.Pending,
                RegisteredDate = DateTime.UtcNow,
                Certifications = request.Certifications ?? new List<string>()
            };

            _suppliers.TryAdd(supplier.Id, supplier);
            return supplier;
        }

        public SupplierEvaluation EvaluateSupplier(string supplierId, SupplierEvaluationRequest request)
        {
            if (!_suppliers.TryGetValue(supplierId, out var supplier))
                return null;

            var evaluation = new SupplierEvaluation
            {
                Id = Guid.NewGuid().ToString(),
                SupplierId = supplierId,
                EvaluationDate = DateTime.UtcNow,
                EvaluatedBy = request.EvaluatedBy,
                ProjectId = request.ProjectId,
                Criteria = new List<EvaluationCriterion>()
            };

            // Add evaluation criteria
            evaluation.Criteria.AddRange(new List<EvaluationCriterion>
            {
                new EvaluationCriterion { Name = "Quality of Work", Weight = 0.25m, Score = request.QualityScore, MaxScore = 100 },
                new EvaluationCriterion { Name = "Schedule Performance", Weight = 0.20m, Score = request.ScheduleScore, MaxScore = 100 },
                new EvaluationCriterion { Name = "Safety Performance", Weight = 0.20m, Score = request.SafetyScore, MaxScore = 100 },
                new EvaluationCriterion { Name = "Communication", Weight = 0.15m, Score = request.CommunicationScore, MaxScore = 100 },
                new EvaluationCriterion { Name = "Cost Management", Weight = 0.10m, Score = request.CostScore, MaxScore = 100 },
                new EvaluationCriterion { Name = "Responsiveness", Weight = 0.10m, Score = request.ResponsivenessScore, MaxScore = 100 }
            });

            // Calculate overall score
            evaluation.OverallScore = evaluation.Criteria.Sum(c => c.Score * c.Weight);
            evaluation.Comments = request.Comments;
            evaluation.WouldRecommend = request.WouldRecommend;

            // Update supplier scores
            lock (_lockObject)
            {
                supplier.QualityScore = (supplier.QualityScore + request.QualityScore) / 2;
                supplier.DeliveryScore = (supplier.DeliveryScore + request.ScheduleScore) / 2;
                supplier.SafetyScore = (supplier.SafetyScore + request.SafetyScore) / 2;
                supplier.Rating = evaluation.OverallScore / 20; // Convert to 5-star scale
                supplier.LastEvaluationDate = DateTime.UtcNow;
            }

            return evaluation;
        }

        public PrequalificationResult PrequalifySupplier(string supplierId, PrequalificationRequest request)
        {
            if (!_suppliers.TryGetValue(supplierId, out var supplier))
                return null;

            var result = new PrequalificationResult
            {
                Id = Guid.NewGuid().ToString(),
                SupplierId = supplierId,
                EvaluationDate = DateTime.UtcNow,
                Criteria = new List<PrequalificationCriterion>(),
                Documents = new List<DocumentVerification>()
            };

            // Financial evaluation
            result.Criteria.Add(new PrequalificationCriterion
            {
                Category = "Financial",
                Criterion = "Annual Revenue",
                Requirement = $"Minimum ${request.MinimumRevenue:N0}",
                SupplierValue = $"${supplier.AnnualRevenue:N0}",
                Pass = supplier.AnnualRevenue >= request.MinimumRevenue
            });

            result.Criteria.Add(new PrequalificationCriterion
            {
                Category = "Financial",
                Criterion = "Years in Business",
                Requirement = $"Minimum {request.MinimumYearsInBusiness} years",
                SupplierValue = $"{supplier.YearsInBusiness} years",
                Pass = supplier.YearsInBusiness >= request.MinimumYearsInBusiness
            });

            // Safety evaluation
            result.Criteria.Add(new PrequalificationCriterion
            {
                Category = "Safety",
                Criterion = "Safety Score",
                Requirement = "Minimum 80",
                SupplierValue = supplier.SafetyScore.ToString(),
                Pass = supplier.SafetyScore >= 80
            });

            // Insurance verification
            result.Documents.Add(new DocumentVerification
            {
                DocumentType = "Certificate of Insurance",
                Required = true,
                Verified = supplier.InsuranceVerified,
                ExpirationDate = DateTime.UtcNow.AddYears(1)
            });

            // Certifications
            foreach (var requiredCert in request.RequiredCertifications ?? new List<string>())
            {
                result.Documents.Add(new DocumentVerification
                {
                    DocumentType = requiredCert,
                    Required = true,
                    Verified = supplier.Certifications?.Contains(requiredCert) ?? false
                });
            }

            // Calculate result
            result.CriteriaPass = result.Criteria.All(c => c.Pass);
            result.DocumentsComplete = result.Documents.Where(d => d.Required).All(d => d.Verified);
            result.Approved = result.CriteriaPass && result.DocumentsComplete;

            // Update supplier status
            supplier.PrequalificationStatus = result.Approved
                ? PrequalificationStatus.Approved
                : PrequalificationStatus.Rejected;
            supplier.PrequalificationDate = DateTime.UtcNow;

            return result;
        }

        public List<Supplier> SearchSuppliers(SupplierSearchCriteria criteria)
        {
            var query = _suppliers.Values.AsQueryable();

            if (!string.IsNullOrEmpty(criteria.Category))
            {
                query = query.Where(s => s.Category.Equals(criteria.Category, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(criteria.Location))
            {
                query = query.Where(s => s.Location.Contains(criteria.Location, StringComparison.OrdinalIgnoreCase));
            }

            if (criteria.MinimumRating.HasValue)
            {
                query = query.Where(s => s.Rating >= criteria.MinimumRating.Value);
            }

            if (criteria.PrequalifiedOnly)
            {
                query = query.Where(s => s.PrequalificationStatus == PrequalificationStatus.Approved);
            }

            if (criteria.Specialties != null && criteria.Specialties.Any())
            {
                query = query.Where(s => s.Specialties.Any(sp => criteria.Specialties.Contains(sp, StringComparer.OrdinalIgnoreCase)));
            }

            return query
                .OrderByDescending(s => s.Rating)
                .ThenByDescending(s => s.QualityScore)
                .ToList();
        }

        #endregion

        #region Contract Management

        public Contract CreateContract(ContractRequest request)
        {
            var contract = new Contract
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                BidPackageId = request.BidPackageId,
                SupplierId = request.SupplierId,
                ContractNumber = request.ContractNumber,
                ContractType = request.ContractType,
                OriginalValue = request.ContractValue,
                CurrentValue = request.ContractValue,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Status = ContractStatus.Draft,
                CreatedDate = DateTime.UtcNow,
                ChangeOrders = new List<ChangeOrder>(),
                Payments = new List<Payment>(),
                Milestones = new List<ContractMilestone>()
            };

            _contracts.TryAdd(contract.Id, contract);

            if (_projects.TryGetValue(request.ProjectId, out var project))
            {
                project.Contracts.Add(contract.Id);
            }

            return contract;
        }

        public Contract ExecuteContract(string contractId, string executedBy)
        {
            if (!_contracts.TryGetValue(contractId, out var contract))
                return null;

            contract.Status = ContractStatus.Active;
            contract.ExecutionDate = DateTime.UtcNow;
            contract.ExecutedBy = executedBy;

            return contract;
        }

        public ChangeOrder CreateChangeOrder(string contractId, ChangeOrderRequest request)
        {
            if (!_contracts.TryGetValue(contractId, out var contract))
                return null;

            var changeOrder = new ChangeOrder
            {
                Id = Guid.NewGuid().ToString(),
                Number = $"CO-{contract.ChangeOrders.Count + 1:D3}",
                Description = request.Description,
                Reason = request.Reason,
                Amount = request.Amount,
                DaysExtension = request.DaysExtension,
                Status = ChangeOrderStatus.Pending,
                CreatedDate = DateTime.UtcNow,
                RequestedBy = request.RequestedBy
            };

            lock (_lockObject)
            {
                contract.ChangeOrders.Add(changeOrder);
            }

            return changeOrder;
        }

        public ChangeOrder ApproveChangeOrder(string contractId, string changeOrderId, ChangeOrderApproval approval)
        {
            if (!_contracts.TryGetValue(contractId, out var contract))
                return null;

            var changeOrder = contract.ChangeOrders.FirstOrDefault(co => co.Id == changeOrderId);
            if (changeOrder == null) return null;

            changeOrder.Status = approval.Approved ? ChangeOrderStatus.Approved : ChangeOrderStatus.Rejected;
            changeOrder.ApprovedDate = DateTime.UtcNow;
            changeOrder.ApprovedBy = approval.ApprovedBy;
            changeOrder.ApprovalNotes = approval.Notes;

            if (approval.Approved)
            {
                contract.CurrentValue += changeOrder.Amount;
                if (changeOrder.DaysExtension > 0)
                {
                    contract.EndDate = contract.EndDate.AddDays(changeOrder.DaysExtension);
                }
            }

            return changeOrder;
        }

        public Payment RecordPayment(string contractId, PaymentRequest request)
        {
            if (!_contracts.TryGetValue(contractId, out var contract))
                return null;

            var payment = new Payment
            {
                Id = Guid.NewGuid().ToString(),
                PaymentNumber = contract.Payments.Count + 1,
                InvoiceNumber = request.InvoiceNumber,
                InvoiceDate = request.InvoiceDate,
                Amount = request.Amount,
                RetainageHeld = request.Amount * (contract.RetainagePercent / 100),
                NetPayment = request.Amount * (1 - contract.RetainagePercent / 100),
                Status = PaymentStatus.Pending,
                CreatedDate = DateTime.UtcNow
            };

            lock (_lockObject)
            {
                contract.Payments.Add(payment);
                contract.AmountPaid += payment.NetPayment;
                contract.RetainageHeld += payment.RetainageHeld;
            }

            return payment;
        }

        public ContractSummary GetContractSummary(string contractId)
        {
            if (!_contracts.TryGetValue(contractId, out var contract))
                return null;

            return new ContractSummary
            {
                ContractId = contractId,
                ContractNumber = contract.ContractNumber,
                OriginalValue = contract.OriginalValue,
                ApprovedChanges = contract.ChangeOrders.Where(co => co.Status == ChangeOrderStatus.Approved).Sum(co => co.Amount),
                PendingChanges = contract.ChangeOrders.Where(co => co.Status == ChangeOrderStatus.Pending).Sum(co => co.Amount),
                CurrentValue = contract.CurrentValue,
                AmountBilled = contract.Payments.Sum(p => p.Amount),
                AmountPaid = contract.AmountPaid,
                RetainageHeld = contract.RetainageHeld,
                RemainingValue = contract.CurrentValue - contract.AmountPaid,
                PercentComplete = contract.CurrentValue > 0 ? contract.AmountPaid / contract.CurrentValue * 100 : 0,
                DaysRemaining = (contract.EndDate - DateTime.UtcNow).Days,
                Status = contract.Status
            };
        }

        #endregion

        #region Analytics

        public ProcurementDashboard GetProjectDashboard(string projectId)
        {
            if (!_projects.TryGetValue(projectId, out var project))
                return null;

            var packages = project.BidPackages
                .Select(id => _bidPackages.TryGetValue(id, out var pkg) ? pkg : null)
                .Where(p => p != null)
                .ToList();

            var contracts = project.Contracts
                .Select(id => _contracts.TryGetValue(id, out var c) ? c : null)
                .Where(c => c != null)
                .ToList();

            return new ProcurementDashboard
            {
                ProjectId = projectId,
                GeneratedDate = DateTime.UtcNow,
                TotalPackages = packages.Count,
                PackagesAwarded = packages.Count(p => p.Status == BidPackageStatus.Awarded),
                PackagesInProgress = packages.Count(p => p.Status == BidPackageStatus.Issued || p.Status == BidPackageStatus.UnderEvaluation),
                TotalEstimatedValue = packages.Sum(p => p.EstimatedValue),
                TotalAwardedValue = packages.Where(p => p.AwardedAmount.HasValue).Sum(p => p.AwardedAmount.Value),
                SavingsVsEstimate = packages.Sum(p => p.EstimatedValue) - packages.Where(p => p.AwardedAmount.HasValue).Sum(p => p.AwardedAmount.Value),
                ActiveContracts = contracts.Count(c => c.Status == ContractStatus.Active),
                TotalContractValue = contracts.Sum(c => c.CurrentValue),
                TotalPaid = contracts.Sum(c => c.AmountPaid),
                TotalRetainage = contracts.Sum(c => c.RetainageHeld),
                PendingChangeOrders = contracts.Sum(c => c.ChangeOrders.Count(co => co.Status == ChangeOrderStatus.Pending)),
                UpcomingBidDates = packages
                    .Where(p => p.BidDate > DateTime.UtcNow && p.Status == BidPackageStatus.Issued)
                    .OrderBy(p => p.BidDate)
                    .Take(5)
                    .Select(p => new UpcomingBid { PackageName = p.Name, BidDate = p.BidDate })
                    .ToList()
            };
        }

        #endregion

        #region Helper Methods

        public Supplier GetSupplier(string supplierId)
        {
            _suppliers.TryGetValue(supplierId, out var supplier);
            return supplier;
        }

        public BidPackage GetBidPackage(string packageId)
        {
            _bidPackages.TryGetValue(packageId, out var package);
            return package;
        }

        public Contract GetContract(string contractId)
        {
            _contracts.TryGetValue(contractId, out var contract);
            return contract;
        }

        public List<Supplier> GetAllSuppliers()
        {
            return _suppliers.Values.ToList();
        }

        #endregion
    }

    #region Data Models

    public class ProcurementProject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public decimal ProjectValue { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<string> BidPackages { get; set; }
        public List<string> Contracts { get; set; }
    }

    public class ProcurementProjectRequest
    {
        public string Name { get; set; }
        public decimal ProjectValue { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class ProcurementPlanRequest
    {
        public string ProjectId { get; set; }
        public string ProjectType { get; set; }
        public decimal ProjectValue { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime RequiredOnSiteDate { get; set; }
    }

    public class ProcurementPlan
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<PackageStrategy> PackageStrategies { get; set; }
        public List<ProcurementMilestone> Timeline { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class PackageStrategy
    {
        public string PackageName { get; set; }
        public string Scope { get; set; }
        public decimal EstimatedValue { get; set; }
        public ProcurementMethod ProcurementMethod { get; set; }
        public ContractType ContractType { get; set; }
        public int BiddersRequired { get; set; }
        public int LeadTime { get; set; }
        public bool CriticalPath { get; set; }
        public bool PrequalificationRequired { get; set; }
        public DateTime BidDueDate { get; set; }
        public DateTime AwardDate { get; set; }
        public DateTime ContractDate { get; set; }
        public DateTime SubmittalDate { get; set; }
        public DateTime DeliveryDate { get; set; }
    }

    public class StandardPackage
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string Scope { get; set; }
        public decimal ValuePercent { get; set; }
        public int LeadTime { get; set; }
        public bool CriticalPath { get; set; }
    }

    public class ProcurementMilestone
    {
        public string PackageName { get; set; }
        public string Milestone { get; set; }
        public DateTime Date { get; set; }
        public bool IsCritical { get; set; }
    }

    public class Supplier
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public List<string> Specialties { get; set; }
        public string Location { get; set; }
        public string ContactName { get; set; }
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }
        public string Address { get; set; }
        public decimal Rating { get; set; }
        public decimal QualityScore { get; set; }
        public decimal DeliveryScore { get; set; }
        public decimal SafetyScore { get; set; }
        public string FinancialRating { get; set; }
        public int YearsInBusiness { get; set; }
        public decimal AnnualRevenue { get; set; }
        public int EmployeeCount { get; set; }
        public bool InsuranceVerified { get; set; }
        public PrequalificationStatus PrequalificationStatus { get; set; }
        public DateTime? PrequalificationDate { get; set; }
        public DateTime RegisteredDate { get; set; }
        public DateTime? LastEvaluationDate { get; set; }
        public List<string> Certifications { get; set; }
    }

    public class SupplierRegistrationRequest
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public List<string> Specialties { get; set; }
        public string Location { get; set; }
        public string ContactName { get; set; }
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }
        public string Address { get; set; }
        public int YearsInBusiness { get; set; }
        public decimal AnnualRevenue { get; set; }
        public int EmployeeCount { get; set; }
        public List<string> Certifications { get; set; }
    }

    public class SupplierSearchCriteria
    {
        public string Category { get; set; }
        public string Location { get; set; }
        public decimal? MinimumRating { get; set; }
        public bool PrequalifiedOnly { get; set; }
        public List<string> Specialties { get; set; }
    }

    public class SupplierEvaluationRequest
    {
        public string EvaluatedBy { get; set; }
        public string ProjectId { get; set; }
        public int QualityScore { get; set; }
        public int ScheduleScore { get; set; }
        public int SafetyScore { get; set; }
        public int CommunicationScore { get; set; }
        public int CostScore { get; set; }
        public int ResponsivenessScore { get; set; }
        public string Comments { get; set; }
        public bool WouldRecommend { get; set; }
    }

    public class SupplierEvaluation
    {
        public string Id { get; set; }
        public string SupplierId { get; set; }
        public DateTime EvaluationDate { get; set; }
        public string EvaluatedBy { get; set; }
        public string ProjectId { get; set; }
        public List<EvaluationCriterion> Criteria { get; set; }
        public decimal OverallScore { get; set; }
        public string Comments { get; set; }
        public bool WouldRecommend { get; set; }
    }

    public class EvaluationCriterion
    {
        public string Name { get; set; }
        public decimal Weight { get; set; }
        public int Score { get; set; }
        public int MaxScore { get; set; }
    }

    public class PrequalificationRequest
    {
        public decimal MinimumRevenue { get; set; }
        public int MinimumYearsInBusiness { get; set; }
        public List<string> RequiredCertifications { get; set; }
    }

    public class PrequalificationResult
    {
        public string Id { get; set; }
        public string SupplierId { get; set; }
        public DateTime EvaluationDate { get; set; }
        public List<PrequalificationCriterion> Criteria { get; set; }
        public List<DocumentVerification> Documents { get; set; }
        public bool CriteriaPass { get; set; }
        public bool DocumentsComplete { get; set; }
        public bool Approved { get; set; }
    }

    public class PrequalificationCriterion
    {
        public string Category { get; set; }
        public string Criterion { get; set; }
        public string Requirement { get; set; }
        public string SupplierValue { get; set; }
        public bool Pass { get; set; }
    }

    public class DocumentVerification
    {
        public string DocumentType { get; set; }
        public bool Required { get; set; }
        public bool Verified { get; set; }
        public DateTime? ExpirationDate { get; set; }
    }

    public class BidPackage
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string Number { get; set; }
        public string Scope { get; set; }
        public decimal EstimatedValue { get; set; }
        public DateTime BidDate { get; set; }
        public DateTime? IssueDate { get; set; }
        public BidPackageStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<string> InvitedBidders { get; set; }
        public List<Bid> Bids { get; set; }
        public List<BidQuestion> Questions { get; set; }
        public List<Addendum> Addenda { get; set; }
        public string AwardedSupplierId { get; set; }
        public decimal? AwardedAmount { get; set; }
    }

    public class BidPackageRequest
    {
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string Number { get; set; }
        public string Scope { get; set; }
        public decimal EstimatedValue { get; set; }
        public DateTime BidDate { get; set; }
    }

    public class Bid
    {
        public string Id { get; set; }
        public string SupplierId { get; set; }
        public string SupplierName { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal? AlternatePrice { get; set; }
        public DateTime SubmittedDate { get; set; }
        public List<string> Exclusions { get; set; }
        public List<string> Clarifications { get; set; }
        public int ProposedSchedule { get; set; }
        public BidStatus Status { get; set; }
        public DateTime? AwardDate { get; set; }
        public string AwardedBy { get; set; }
        public string AwardNotes { get; set; }
        public List<BidLineItem> LineItems { get; set; }
    }

    public class BidSubmission
    {
        public string SupplierId { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal? AlternatePrice { get; set; }
        public List<string> Exclusions { get; set; }
        public List<string> Clarifications { get; set; }
        public int ProposedSchedule { get; set; }
        public List<BidLineItem> LineItems { get; set; }
    }

    public class BidLineItem
    {
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal ExtendedPrice { get; set; }
    }

    public class BidQuestion
    {
        public string Id { get; set; }
        public string Question { get; set; }
        public string Answer { get; set; }
        public DateTime AskedDate { get; set; }
        public DateTime? AnsweredDate { get; set; }
    }

    public class Addendum
    {
        public int Number { get; set; }
        public DateTime IssueDate { get; set; }
        public string Description { get; set; }
    }

    public class AwardRequest
    {
        public string AwardedBy { get; set; }
        public string Notes { get; set; }
    }

    public class BidAnalysis
    {
        public string PackageId { get; set; }
        public string PackageName { get; set; }
        public DateTime AnalysisDate { get; set; }
        public int BidCount { get; set; }
        public decimal EstimatedValue { get; set; }
        public decimal LowBid { get; set; }
        public decimal HighBid { get; set; }
        public decimal AverageBid { get; set; }
        public decimal MedianBid { get; set; }
        public decimal Spread { get; set; }
        public decimal LowVsEstimate { get; set; }
        public List<BidComparison> BidComparisons { get; set; }
        public string ApparentLowBidder { get; set; }
        public string BestValueBidder { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class BidComparison
    {
        public string SupplierId { get; set; }
        public string SupplierName { get; set; }
        public decimal TotalPrice { get; set; }
        public int Rank { get; set; }
        public decimal VarianceFromLow { get; set; }
        public decimal VarianceFromEstimate { get; set; }
        public decimal SupplierScore { get; set; }
        public int ExclusionCount { get; set; }
        public bool Responsive { get; set; }
        public decimal ValueScore { get; set; }
    }

    public class Contract
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string BidPackageId { get; set; }
        public string SupplierId { get; set; }
        public string ContractNumber { get; set; }
        public ContractType ContractType { get; set; }
        public decimal OriginalValue { get; set; }
        public decimal CurrentValue { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public ContractStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ExecutionDate { get; set; }
        public string ExecutedBy { get; set; }
        public decimal RetainagePercent { get; set; } = 10;
        public decimal AmountPaid { get; set; }
        public decimal RetainageHeld { get; set; }
        public List<ChangeOrder> ChangeOrders { get; set; }
        public List<Payment> Payments { get; set; }
        public List<ContractMilestone> Milestones { get; set; }
    }

    public class ContractRequest
    {
        public string ProjectId { get; set; }
        public string BidPackageId { get; set; }
        public string SupplierId { get; set; }
        public string ContractNumber { get; set; }
        public ContractType ContractType { get; set; }
        public decimal ContractValue { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class ChangeOrder
    {
        public string Id { get; set; }
        public string Number { get; set; }
        public string Description { get; set; }
        public ChangeOrderReason Reason { get; set; }
        public decimal Amount { get; set; }
        public int DaysExtension { get; set; }
        public ChangeOrderStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public string RequestedBy { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string ApprovedBy { get; set; }
        public string ApprovalNotes { get; set; }
    }

    public class ChangeOrderRequest
    {
        public string Description { get; set; }
        public ChangeOrderReason Reason { get; set; }
        public decimal Amount { get; set; }
        public int DaysExtension { get; set; }
        public string RequestedBy { get; set; }
    }

    public class ChangeOrderApproval
    {
        public bool Approved { get; set; }
        public string ApprovedBy { get; set; }
        public string Notes { get; set; }
    }

    public class Payment
    {
        public string Id { get; set; }
        public int PaymentNumber { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal Amount { get; set; }
        public decimal RetainageHeld { get; set; }
        public decimal NetPayment { get; set; }
        public PaymentStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? PaidDate { get; set; }
    }

    public class PaymentRequest
    {
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal Amount { get; set; }
    }

    public class ContractMilestone
    {
        public string Name { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public decimal PaymentPercent { get; set; }
    }

    public class ContractSummary
    {
        public string ContractId { get; set; }
        public string ContractNumber { get; set; }
        public decimal OriginalValue { get; set; }
        public decimal ApprovedChanges { get; set; }
        public decimal PendingChanges { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal AmountBilled { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal RetainageHeld { get; set; }
        public decimal RemainingValue { get; set; }
        public decimal PercentComplete { get; set; }
        public int DaysRemaining { get; set; }
        public ContractStatus Status { get; set; }
    }

    public class ProcurementDashboard
    {
        public string ProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalPackages { get; set; }
        public int PackagesAwarded { get; set; }
        public int PackagesInProgress { get; set; }
        public decimal TotalEstimatedValue { get; set; }
        public decimal TotalAwardedValue { get; set; }
        public decimal SavingsVsEstimate { get; set; }
        public int ActiveContracts { get; set; }
        public decimal TotalContractValue { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalRetainage { get; set; }
        public int PendingChangeOrders { get; set; }
        public List<UpcomingBid> UpcomingBidDates { get; set; }
    }

    public class UpcomingBid
    {
        public string PackageName { get; set; }
        public DateTime BidDate { get; set; }
    }

    public class ProcurementAlertEventArgs : EventArgs
    {
        public string ProjectId { get; set; }
        public string AlertType { get; set; }
        public string Message { get; set; }
    }

    public enum ProcurementMethod { CompetitiveBid, InvitedBid, NegotiatedContract, SoleSource }
    public enum ContractType { LumpSum, UnitPrice, CostPlus, GMP, TimeAndMaterial }
    public enum PrequalificationStatus { Pending, Approved, Rejected, Expired }
    public enum BidPackageStatus { Draft, Issued, UnderEvaluation, Awarded, Cancelled }
    public enum BidStatus { Received, UnderReview, Awarded, NotAwarded, Withdrawn }
    public enum ContractStatus { Draft, Active, Suspended, Complete, Terminated }
    public enum ChangeOrderStatus { Pending, Approved, Rejected }
    public enum ChangeOrderReason { OwnerChange, DesignError, UnforeseenCondition, Acceleration, Other }
    public enum PaymentStatus { Pending, Approved, Paid }

    #endregion
}
