// ===================================================================
// StingBIM Specification Intelligence Engine
// CSI MasterFormat expertise, spec writing, product substitution
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.SpecificationIntelligence
{
    #region Enums

    public enum SpecFormat { MasterFormat2020, MasterFormat2016, UniFormat, OmniClass }
    public enum SpecSectionType { General, Products, Execution }
    public enum SubstitutionStatus { Requested, UnderReview, Approved, Rejected, Conditional }
    public enum ProductStatus { Specified, Acceptable, NotAcceptable, Substitution }

    #endregion

    #region Data Models

    public class ProjectSpecification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public SpecFormat Format { get; set; } = SpecFormat.MasterFormat2020;
        public List<SpecDivision> Divisions { get; set; } = new();
        public List<SubstitutionRequest> Substitutions { get; set; } = new();
        public List<ProductData> Products { get; set; } = new();
        public SpecificationMetrics Metrics { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class SpecDivision
    {
        public string Number { get; set; }
        public string Title { get; set; }
        public List<SpecSection> Sections { get; set; } = new();
    }

    public class SpecSection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Number { get; set; }
        public string Title { get; set; }
        public List<SpecArticle> Part1General { get; set; } = new();
        public List<SpecArticle> Part2Products { get; set; } = new();
        public List<SpecArticle> Part3Execution { get; set; } = new();
        public List<string> RelatedSections { get; set; } = new();
        public List<string> References { get; set; } = new();
        public string Status { get; set; } = "Draft";
    }

    public class SpecArticle
    {
        public string Number { get; set; }
        public string Title { get; set; }
        public List<SpecParagraph> Paragraphs { get; set; } = new();
    }

    public class SpecParagraph
    {
        public string Letter { get; set; }
        public string Text { get; set; }
        public List<string> SubItems { get; set; } = new();
    }

    public class SubstitutionRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RequestNumber { get; set; }
        public string SectionNumber { get; set; }
        public string SpecifiedProduct { get; set; }
        public string ProposedProduct { get; set; }
        public string Manufacturer { get; set; }
        public string Requestor { get; set; }
        public SubstitutionStatus Status { get; set; } = SubstitutionStatus.Requested;
        public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;
        public DateTime? DecisionDate { get; set; }
        public string Justification { get; set; }
        public List<ComparisonItem> Comparison { get; set; } = new();
        public decimal CostImpact { get; set; }
        public int ScheduleImpact { get; set; }
        public List<string> Conditions { get; set; } = new();
        public string ReviewerComments { get; set; }
    }

    public class ComparisonItem
    {
        public string Attribute { get; set; }
        public string SpecifiedValue { get; set; }
        public string ProposedValue { get; set; }
        public bool Compliant { get; set; }
        public string Notes { get; set; }
    }

    public class ProductData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SectionNumber { get; set; }
        public string ProductName { get; set; }
        public string Manufacturer { get; set; }
        public string ModelNumber { get; set; }
        public ProductStatus Status { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new();
        public List<string> Certifications { get; set; } = new();
        public string SubmittalStatus { get; set; }
        public decimal UnitCost { get; set; }
        public int LeadTimeWeeks { get; set; }
    }

    public class SpecificationMetrics
    {
        public int TotalSections { get; set; }
        public int CompleteSections { get; set; }
        public int DraftSections { get; set; }
        public int TotalSubstitutions { get; set; }
        public int ApprovedSubstitutions { get; set; }
        public int PendingSubstitutions { get; set; }
        public double CompletionRate => TotalSections > 0 ? CompleteSections * 100.0 / TotalSections : 0;
    }

    public class SpecTemplate
    {
        public string SectionNumber { get; set; }
        public string Title { get; set; }
        public string BaseContent { get; set; }
        public List<string> RequiredReferences { get; set; } = new();
        public List<string> CommonProducts { get; set; } = new();
    }

    public class SpecAnalysis
    {
        public string SectionNumber { get; set; }
        public List<string> MissingElements { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<string> ConflictsWithOtherSections { get; set; } = new();
        public double QualityScore { get; set; }
    }

    #endregion

    public sealed class SpecificationIntelligenceEngine
    {
        private static readonly Lazy<SpecificationIntelligenceEngine> _instance =
            new Lazy<SpecificationIntelligenceEngine>(() => new SpecificationIntelligenceEngine());
        public static SpecificationIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, ProjectSpecification> _projects = new();
        private readonly Dictionary<string, SpecDivision> _masterFormat = new();
        private readonly object _lock = new object();

        private SpecificationIntelligenceEngine()
        {
            InitializeMasterFormat();
        }

        private void InitializeMasterFormat()
        {
            var divisions = new[]
            {
                ("00", "Procurement and Contracting Requirements"),
                ("01", "General Requirements"),
                ("02", "Existing Conditions"),
                ("03", "Concrete"),
                ("04", "Masonry"),
                ("05", "Metals"),
                ("06", "Wood, Plastics, and Composites"),
                ("07", "Thermal and Moisture Protection"),
                ("08", "Openings"),
                ("09", "Finishes"),
                ("10", "Specialties"),
                ("11", "Equipment"),
                ("12", "Furnishings"),
                ("13", "Special Construction"),
                ("14", "Conveying Equipment"),
                ("21", "Fire Suppression"),
                ("22", "Plumbing"),
                ("23", "Heating, Ventilating, and Air Conditioning"),
                ("25", "Integrated Automation"),
                ("26", "Electrical"),
                ("27", "Communications"),
                ("28", "Electronic Safety and Security"),
                ("31", "Earthwork"),
                ("32", "Exterior Improvements"),
                ("33", "Utilities"),
                ("34", "Transportation"),
                ("35", "Waterway and Marine Construction"),
                ("40", "Process Interconnections"),
                ("41", "Material Processing and Handling Equipment"),
                ("42", "Process Heating, Cooling, and Drying Equipment"),
                ("43", "Process Gas and Liquid Handling"),
                ("44", "Pollution and Waste Control Equipment"),
                ("45", "Industry-Specific Manufacturing Equipment"),
                ("46", "Water and Wastewater Equipment"),
                ("48", "Electrical Power Generation")
            };

            foreach (var (number, title) in divisions)
            {
                _masterFormat[number] = new SpecDivision { Number = number, Title = title };
            }
        }

        public ProjectSpecification CreateProjectSpecification(string projectId, string projectName)
        {
            var spec = new ProjectSpecification
            {
                ProjectId = projectId,
                ProjectName = projectName,
                Divisions = _masterFormat.Values.Select(d => new SpecDivision
                {
                    Number = d.Number,
                    Title = d.Title
                }).ToList()
            };

            lock (_lock) { _projects[spec.Id] = spec; }
            return spec;
        }

        public SpecSection CreateSection(string projectId, string divisionNumber, string sectionNumber, string title)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var division = project.Divisions.FirstOrDefault(d => d.Number == divisionNumber);
                if (division == null) return null;

                var section = new SpecSection
                {
                    Number = sectionNumber,
                    Title = title,
                    Part1General = GenerateDefaultPart1(),
                    References = GetStandardReferences(sectionNumber)
                };

                division.Sections.Add(section);
                UpdateMetrics(project);
                return section;
            }
        }

        private List<SpecArticle> GenerateDefaultPart1()
        {
            return new List<SpecArticle>
            {
                new SpecArticle
                {
                    Number = "1.1",
                    Title = "RELATED DOCUMENTS",
                    Paragraphs = new List<SpecParagraph>
                    {
                        new SpecParagraph { Letter = "A", Text = "Drawings and general provisions of the Contract apply to this Section." }
                    }
                },
                new SpecArticle
                {
                    Number = "1.2",
                    Title = "SUMMARY",
                    Paragraphs = new List<SpecParagraph>
                    {
                        new SpecParagraph { Letter = "A", Text = "Section Includes:" }
                    }
                },
                new SpecArticle
                {
                    Number = "1.3",
                    Title = "SUBMITTALS",
                    Paragraphs = new List<SpecParagraph>
                    {
                        new SpecParagraph { Letter = "A", Text = "Product Data: Submit manufacturer's technical product data." },
                        new SpecParagraph { Letter = "B", Text = "Shop Drawings: Submit shop drawings showing layout, dimensions, and details." }
                    }
                },
                new SpecArticle
                {
                    Number = "1.4",
                    Title = "QUALITY ASSURANCE",
                    Paragraphs = new List<SpecParagraph>
                    {
                        new SpecParagraph { Letter = "A", Text = "Installer Qualifications: Engage an experienced installer." }
                    }
                }
            };
        }

        private List<string> GetStandardReferences(string sectionNumber)
        {
            var prefix = sectionNumber.Substring(0, 2);
            var references = new List<string>();

            switch (prefix)
            {
                case "03":
                    references.AddRange(new[] { "ACI 301", "ACI 318", "ASTM C94", "ASTM C150" });
                    break;
                case "05":
                    references.AddRange(new[] { "AISC 360", "AWS D1.1", "ASTM A36", "ASTM A992" });
                    break;
                case "07":
                    references.AddRange(new[] { "ASTM D4586", "ASTM E96", "NRCA Guidelines" });
                    break;
                case "08":
                    references.AddRange(new[] { "AAMA/WDMA/CSA 101", "ASTM E283", "ASTM E330" });
                    break;
                case "09":
                    references.AddRange(new[] { "ASTM C840", "GA-216", "ASTM E84" });
                    break;
                case "23":
                    references.AddRange(new[] { "ASHRAE 90.1", "SMACNA", "NFPA 90A" });
                    break;
                case "26":
                    references.AddRange(new[] { "NFPA 70 (NEC)", "IEEE C2", "UL Standards" });
                    break;
            }

            return references;
        }

        public SubstitutionRequest RequestSubstitution(string projectId, string sectionNumber,
            string specifiedProduct, string proposedProduct, string manufacturer, string requestor, string justification)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var request = new SubstitutionRequest
                {
                    RequestNumber = $"SUB-{project.Substitutions.Count + 1:D3}",
                    SectionNumber = sectionNumber,
                    SpecifiedProduct = specifiedProduct,
                    ProposedProduct = proposedProduct,
                    Manufacturer = manufacturer,
                    Requestor = requestor,
                    Justification = justification
                };

                project.Substitutions.Add(request);
                UpdateMetrics(project);
                return request;
            }
        }

        public SubstitutionRequest ReviewSubstitution(string projectId, string requestId,
            SubstitutionStatus decision, string comments, List<string> conditions = null)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var request = project.Substitutions.FirstOrDefault(s => s.Id == requestId);
                if (request == null) return null;

                request.Status = decision;
                request.DecisionDate = DateTime.UtcNow;
                request.ReviewerComments = comments;
                if (conditions != null) request.Conditions = conditions;

                UpdateMetrics(project);
                return request;
            }
        }

        public async Task<List<ComparisonItem>> CompareProducts(string specifiedProduct, string proposedProduct,
            Dictionary<string, string> specifiedAttrs, Dictionary<string, string> proposedAttrs)
        {
            return await Task.Run(() =>
            {
                var comparison = new List<ComparisonItem>();

                var allKeys = specifiedAttrs.Keys.Union(proposedAttrs.Keys).Distinct();

                foreach (var key in allKeys)
                {
                    var specValue = specifiedAttrs.GetValueOrDefault(key, "Not specified");
                    var propValue = proposedAttrs.GetValueOrDefault(key, "Not provided");

                    comparison.Add(new ComparisonItem
                    {
                        Attribute = key,
                        SpecifiedValue = specValue,
                        ProposedValue = propValue,
                        Compliant = specValue == propValue || propValue.Contains("exceed") || propValue.Contains("better"),
                        Notes = specValue == propValue ? "Meets specification" : "Review required"
                    });
                }

                return comparison;
            });
        }

        public async Task<SpecAnalysis> AnalyzeSection(string projectId, string sectionId)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_projects.TryGetValue(projectId, out var project))
                        return null;

                    var section = project.Divisions
                        .SelectMany(d => d.Sections)
                        .FirstOrDefault(s => s.Id == sectionId);

                    if (section == null) return null;

                    var analysis = new SpecAnalysis { SectionNumber = section.Number };

                    // Check for missing elements
                    if (!section.Part2Products.Any())
                        analysis.MissingElements.Add("Part 2 - Products section is empty");
                    if (!section.Part3Execution.Any())
                        analysis.MissingElements.Add("Part 3 - Execution section is empty");
                    if (!section.References.Any())
                        analysis.MissingElements.Add("No reference standards specified");

                    // Generate recommendations
                    if (section.Part1General.Count < 4)
                        analysis.Recommendations.Add("Add more detail to Part 1 General requirements");

                    analysis.QualityScore = 100 - (analysis.MissingElements.Count * 15);

                    return analysis;
                }
            });
        }

        public ProductData AddProduct(string projectId, string sectionNumber, string productName,
            string manufacturer, string modelNumber, ProductStatus status)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return null;

                var product = new ProductData
                {
                    SectionNumber = sectionNumber,
                    ProductName = productName,
                    Manufacturer = manufacturer,
                    ModelNumber = modelNumber,
                    Status = status
                };

                project.Products.Add(product);
                return product;
            }
        }

        public List<SpecSection> SearchSections(string projectId, string searchTerm)
        {
            lock (_lock)
            {
                if (!_projects.TryGetValue(projectId, out var project))
                    return new List<SpecSection>();

                return project.Divisions
                    .SelectMany(d => d.Sections)
                    .Where(s => s.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                               s.Number.Contains(searchTerm))
                    .ToList();
            }
        }

        public List<string> GetRelatedSections(string sectionNumber)
        {
            var related = new List<string>();
            var prefix = sectionNumber.Substring(0, 2);

            var relationships = new Dictionary<string, string[]>
            {
                ["03"] = new[] { "01 45 00", "03 30 00", "03 35 00", "31 20 00" },
                ["05"] = new[] { "01 45 00", "03 30 00", "05 12 00", "05 50 00" },
                ["07"] = new[] { "06 10 00", "07 92 00", "08 44 00" },
                ["08"] = new[] { "07 92 00", "08 71 00", "08 80 00" },
                ["09"] = new[] { "06 41 00", "09 29 00", "09 91 00" },
                ["23"] = new[] { "23 05 00", "23 21 00", "25 00 00", "26 00 00" },
                ["26"] = new[] { "26 05 00", "26 24 00", "27 00 00", "28 00 00" }
            };

            if (relationships.TryGetValue(prefix, out var sections))
                related.AddRange(sections);

            return related;
        }

        private void UpdateMetrics(ProjectSpecification project)
        {
            var allSections = project.Divisions.SelectMany(d => d.Sections).ToList();

            project.Metrics = new SpecificationMetrics
            {
                TotalSections = allSections.Count,
                CompleteSections = allSections.Count(s => s.Status == "Complete"),
                DraftSections = allSections.Count(s => s.Status == "Draft"),
                TotalSubstitutions = project.Substitutions.Count,
                ApprovedSubstitutions = project.Substitutions.Count(s => s.Status == SubstitutionStatus.Approved),
                PendingSubstitutions = project.Substitutions.Count(s => s.Status == SubstitutionStatus.Requested ||
                                                                        s.Status == SubstitutionStatus.UnderReview)
            };
        }
    }
}
