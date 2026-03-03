// StingBIM.AI.Intelligence.Reasoning.AnalogicalReasoner
// Analogical reasoning for finding similar projects and transferring knowledge
// Master Proposal Reference: Part 2.2 - Phase 2 Intelligence Enhancement

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.Intelligence.Reasoning
{
    #region Project Similarity

    /// <summary>
    /// Finds similar projects and enables knowledge transfer through analogical reasoning.
    /// </summary>
    public class AnalogicalReasoner
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<ProjectArchetype> _archetypes;
        private readonly List<ProjectProfile> _projectHistory;
        private readonly SimilarityCalculator _similarityCalculator;

        public AnalogicalReasoner()
        {
            _archetypes = new List<ProjectArchetype>();
            _projectHistory = new List<ProjectProfile>();
            _similarityCalculator = new SimilarityCalculator();

            InitializeArchetypes();
        }

        /// <summary>
        /// Registers a project for future comparison.
        /// </summary>
        public void RegisterProject(ProjectProfile project)
        {
            _projectHistory.Add(project);
            Logger.Info($"Registered project: {project.ProjectId} ({project.BuildingType})");
        }

        /// <summary>
        /// Finds the most similar historical projects.
        /// </summary>
        public List<SimilarProject> FindSimilarProjects(ProjectProfile currentProject, int maxResults = 5)
        {
            var similarities = new List<SimilarProject>();

            foreach (var historical in _projectHistory)
            {
                if (historical.ProjectId == currentProject.ProjectId)
                    continue;

                var similarity = _similarityCalculator.Calculate(currentProject, historical);

                if (similarity.OverallScore > 0.3f)
                {
                    similarities.Add(new SimilarProject
                    {
                        Project = historical,
                        Similarity = similarity,
                        TransferableKnowledge = IdentifyTransferableKnowledge(historical, similarity)
                    });
                }
            }

            return similarities
                .OrderByDescending(s => s.Similarity.OverallScore)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Finds the best matching archetype for a project.
        /// </summary>
        public ArchetypeMatch FindMatchingArchetype(ProjectProfile project)
        {
            var bestMatch = _archetypes
                .Select(a => new
                {
                    Archetype = a,
                    Score = CalculateArchetypeMatch(project, a)
                })
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (bestMatch == null)
            {
                return new ArchetypeMatch { MatchScore = 0 };
            }

            return new ArchetypeMatch
            {
                Archetype = bestMatch.Archetype,
                MatchScore = bestMatch.Score,
                ApplicablePatterns = bestMatch.Archetype.DesignPatterns
                    .Where(p => IsPatternApplicable(p, project))
                    .ToList(),
                Recommendations = GenerateArchetypeRecommendations(bestMatch.Archetype, project)
            };
        }

        /// <summary>
        /// Transfers a solution from a similar project to the current context.
        /// </summary>
        public SolutionTransfer TransferSolution(
            string solutionType,
            ProjectProfile sourceProject,
            ProjectProfile targetProject)
        {
            var transfer = new SolutionTransfer
            {
                SolutionType = solutionType,
                SourceProject = sourceProject,
                TargetProject = targetProject
            };

            // Find the solution in source project
            var sourceSolution = sourceProject.Solutions?.FirstOrDefault(s => s.Type == solutionType);
            if (sourceSolution == null)
            {
                transfer.CanTransfer = false;
                transfer.Reason = $"Solution type '{solutionType}' not found in source project";
                return transfer;
            }

            // Calculate context similarity
            var similarity = _similarityCalculator.Calculate(sourceProject, targetProject);
            transfer.ContextSimilarity = similarity.OverallScore;

            // Check if transfer is appropriate
            var applicability = AssessSolutionApplicability(sourceSolution, targetProject);
            transfer.CanTransfer = applicability.IsApplicable;
            transfer.ConfidenceScore = applicability.Confidence;
            transfer.RequiredAdaptations = applicability.RequiredAdaptations;

            if (transfer.CanTransfer)
            {
                // Create adapted solution
                transfer.AdaptedSolution = AdaptSolution(sourceSolution, targetProject, applicability);
                transfer.Reason = $"Solution can be transferred with {applicability.RequiredAdaptations.Count} adaptations";
            }
            else
            {
                transfer.Reason = applicability.RejectionReason;
            }

            return transfer;
        }

        /// <summary>
        /// Finds analogies between design problems.
        /// </summary>
        public List<DesignAnalogy> FindAnalogies(DesignProblem problem)
        {
            var analogies = new List<DesignAnalogy>();

            // Search for similar problems in project history
            foreach (var project in _projectHistory)
            {
                if (project.SolvedProblems == null) continue;

                foreach (var solved in project.SolvedProblems)
                {
                    var similarity = CalculateProblemSimilarity(problem, solved);

                    if (similarity > 0.5f)
                    {
                        analogies.Add(new DesignAnalogy
                        {
                            SourceProblem = solved,
                            SourceProject = project,
                            Similarity = similarity,
                            SourceSolution = solved.Solution,
                            AdaptationNeeded = DescribeAdaptation(problem, solved)
                        });
                    }
                }
            }

            // Search archetypes for pattern-based analogies
            foreach (var archetype in _archetypes)
            {
                var patternMatch = archetype.DesignPatterns
                    .Where(p => p.SolvesProblemType == problem.ProblemType)
                    .Select(p => new DesignAnalogy
                    {
                        SourcePattern = p,
                        Similarity = 0.7f, // Pattern matches are generally applicable
                        SourceSolution = p.Solution,
                        AdaptationNeeded = "Apply pattern to specific context"
                    })
                    .FirstOrDefault();

                if (patternMatch != null)
                {
                    analogies.Add(patternMatch);
                }
            }

            return analogies.OrderByDescending(a => a.Similarity).ToList();
        }

        /// <summary>
        /// Learns from a successful solution for future analogies.
        /// </summary>
        public void LearnFromSuccess(ProjectProfile project, SolvedProblem solution)
        {
            // Add to project's solved problems
            project.SolvedProblems = project.SolvedProblems ?? new List<SolvedProblem>();
            project.SolvedProblems.Add(solution);

            // Check if this represents a new pattern
            var similarSolutions = _projectHistory
                .SelectMany(p => p.SolvedProblems ?? new List<SolvedProblem>())
                .Where(s => s.ProblemType == solution.ProblemType)
                .Where(s => IsSimilarSolution(s, solution))
                .Count();

            if (similarSolutions >= 3)
            {
                // This might be a pattern worth codifying
                Logger.Info($"Potential new pattern detected for {solution.ProblemType}");
            }
        }

        private List<TransferableKnowledge> IdentifyTransferableKnowledge(
            ProjectProfile source,
            ProjectSimilarity similarity)
        {
            var knowledge = new List<TransferableKnowledge>();

            // Room layouts if similar building type
            if (similarity.BuildingTypeSimilarity > 0.7f && source.RoomLayouts?.Any() == true)
            {
                knowledge.Add(new TransferableKnowledge
                {
                    Type = KnowledgeType.RoomLayout,
                    Description = "Room layout patterns",
                    Confidence = similarity.BuildingTypeSimilarity,
                    SourceData = source.RoomLayouts
                });
            }

            // MEP strategies if similar scale
            if (similarity.ScaleSimilarity > 0.6f && source.MEPStrategies?.Any() == true)
            {
                knowledge.Add(new TransferableKnowledge
                {
                    Type = KnowledgeType.MEPStrategy,
                    Description = "MEP system strategies",
                    Confidence = similarity.ScaleSimilarity,
                    SourceData = source.MEPStrategies
                });
            }

            // Material choices if similar climate
            if (similarity.ClimateSimilarity > 0.7f && source.MaterialChoices?.Any() == true)
            {
                knowledge.Add(new TransferableKnowledge
                {
                    Type = KnowledgeType.MaterialChoice,
                    Description = "Climate-appropriate materials",
                    Confidence = similarity.ClimateSimilarity,
                    SourceData = source.MaterialChoices
                });
            }

            // Solved problems are always transferable knowledge
            if (source.SolvedProblems?.Any() == true)
            {
                knowledge.Add(new TransferableKnowledge
                {
                    Type = KnowledgeType.SolvedProblem,
                    Description = $"{source.SolvedProblems.Count} solved design problems",
                    Confidence = similarity.OverallScore,
                    SourceData = source.SolvedProblems
                });
            }

            return knowledge;
        }

        private float CalculateArchetypeMatch(ProjectProfile project, ProjectArchetype archetype)
        {
            float score = 0f;
            int factors = 0;

            // Building type match
            if (project.BuildingType == archetype.BuildingType)
            {
                score += 1.0f;
                factors++;
            }
            else if (archetype.RelatedBuildingTypes?.Contains(project.BuildingType) == true)
            {
                score += 0.6f;
                factors++;
            }

            // Scale match
            if (project.GrossArea >= archetype.MinArea && project.GrossArea <= archetype.MaxArea)
            {
                score += 0.8f;
                factors++;
            }

            // Program match
            if (archetype.TypicalProgram != null && project.Program != null)
            {
                var programMatch = archetype.TypicalProgram.Keys
                    .Intersect(project.Program.Keys)
                    .Count() / (float)archetype.TypicalProgram.Count;
                score += programMatch;
                factors++;
            }

            return factors > 0 ? score / factors : 0f;
        }

        private bool IsPatternApplicable(DesignPattern pattern, ProjectProfile project)
        {
            // Check constraints
            if (pattern.MinArea.HasValue && project.GrossArea < pattern.MinArea.Value)
                return false;

            if (pattern.MaxArea.HasValue && project.GrossArea > pattern.MaxArea.Value)
                return false;

            if (pattern.RequiredRoomTypes?.Any() == true)
            {
                var projectRoomTypes = project.Program?.Keys.ToList() ?? new List<string>();
                if (!pattern.RequiredRoomTypes.All(r => projectRoomTypes.Contains(r)))
                    return false;
            }

            return true;
        }

        private List<string> GenerateArchetypeRecommendations(ProjectArchetype archetype, ProjectProfile project)
        {
            var recommendations = new List<string>();

            recommendations.Add($"This project matches the '{archetype.Name}' archetype");

            foreach (var pattern in archetype.DesignPatterns.Take(3))
            {
                recommendations.Add($"Consider: {pattern.Name} - {pattern.Description}");
            }

            if (archetype.CommonMistakes?.Any() == true)
            {
                recommendations.Add($"Avoid common mistakes: {string.Join(", ", archetype.CommonMistakes.Take(3))}");
            }

            return recommendations;
        }

        private SolutionApplicability AssessSolutionApplicability(
            DesignSolution solution,
            ProjectProfile target)
        {
            var applicability = new SolutionApplicability
            {
                IsApplicable = true,
                Confidence = 1.0f,
                RequiredAdaptations = new List<Adaptation>()
            };

            // Check scale compatibility
            if (solution.MinScale.HasValue && target.GrossArea < solution.MinScale.Value)
            {
                applicability.IsApplicable = false;
                applicability.RejectionReason = "Target project too small for this solution";
                return applicability;
            }

            // Check required elements
            if (solution.RequiredElements?.Any() == true)
            {
                var missing = solution.RequiredElements
                    .Except(target.AvailableElements ?? new List<string>())
                    .ToList();

                if (missing.Any())
                {
                    applicability.RequiredAdaptations.Add(new Adaptation
                    {
                        Type = AdaptationType.AddElement,
                        Description = $"Add missing elements: {string.Join(", ", missing)}"
                    });
                    applicability.Confidence -= 0.1f * missing.Count;
                }
            }

            // Check climate compatibility
            if (!string.IsNullOrEmpty(solution.ClimateZone) && solution.ClimateZone != target.ClimateZone)
            {
                applicability.RequiredAdaptations.Add(new Adaptation
                {
                    Type = AdaptationType.ClimateAdapt,
                    Description = $"Adapt from {solution.ClimateZone} to {target.ClimateZone} climate"
                });
                applicability.Confidence -= 0.2f;
            }

            applicability.Confidence = Math.Max(0.1f, applicability.Confidence);
            return applicability;
        }

        private DesignSolution AdaptSolution(
            DesignSolution source,
            ProjectProfile target,
            SolutionApplicability applicability)
        {
            var adapted = new DesignSolution
            {
                Type = source.Type,
                Name = $"{source.Name} (Adapted)",
                Description = source.Description,
                Parameters = new Dictionary<string, object>(source.Parameters ?? new Dictionary<string, object>())
            };

            // Apply adaptations
            foreach (var adaptation in applicability.RequiredAdaptations)
            {
                switch (adaptation.Type)
                {
                    case AdaptationType.Scale:
                        var scaleFactor = target.GrossArea / (source.MinScale ?? target.GrossArea);
                        adapted.Parameters["ScaleFactor"] = scaleFactor;
                        break;

                    case AdaptationType.ClimateAdapt:
                        adapted.Parameters["OriginalClimate"] = source.ClimateZone;
                        adapted.Parameters["TargetClimate"] = target.ClimateZone;
                        break;
                }
            }

            return adapted;
        }

        private float CalculateProblemSimilarity(DesignProblem problem1, SolvedProblem problem2)
        {
            float score = 0f;
            int factors = 0;

            // Problem type match
            if (problem1.ProblemType == problem2.ProblemType)
            {
                score += 1.0f;
                factors++;
            }

            // Context similarity
            if (problem1.Context?.RoomType == problem2.Context?.RoomType)
            {
                score += 0.5f;
                factors++;
            }

            // Constraint similarity
            if (problem1.Constraints?.Any() == true && problem2.Constraints?.Any() == true)
            {
                var common = problem1.Constraints.Intersect(problem2.Constraints).Count();
                var total = problem1.Constraints.Union(problem2.Constraints).Count();
                score += (float)common / total;
                factors++;
            }

            return factors > 0 ? score / factors : 0f;
        }

        private string DescribeAdaptation(DesignProblem target, SolvedProblem source)
        {
            var differences = new List<string>();

            if (target.Context?.RoomType != source.Context?.RoomType)
            {
                differences.Add($"Different room type ({source.Context?.RoomType} → {target.Context?.RoomType})");
            }

            if (target.Context?.BuildingType != source.Context?.BuildingType)
            {
                differences.Add($"Different building type");
            }

            return differences.Any()
                ? $"Adapt solution for: {string.Join(", ", differences)}"
                : "Direct application possible";
        }

        private bool IsSimilarSolution(SolvedProblem s1, SolvedProblem s2)
        {
            return s1.ProblemType == s2.ProblemType &&
                   s1.Solution?.Type == s2.Solution?.Type;
        }

        private void InitializeArchetypes()
        {
            // Residential archetypes
            _archetypes.Add(new ProjectArchetype
            {
                ArchetypeId = "ARCH001",
                Name = "Single Family Residence",
                BuildingType = "Residential",
                RelatedBuildingTypes = new List<string> { "Villa", "House" },
                MinArea = 100,
                MaxArea = 500,
                TypicalProgram = new Dictionary<string, float>
                {
                    ["Living"] = 0.25f, ["Bedroom"] = 0.30f, ["Kitchen"] = 0.10f,
                    ["Bathroom"] = 0.10f, ["Circulation"] = 0.15f, ["Storage"] = 0.10f
                },
                DesignPatterns = new List<DesignPattern>
                {
                    new DesignPattern
                    {
                        Name = "Public-Private Gradient",
                        Description = "Arrange spaces from public (living) to private (bedrooms)",
                        SolvesProblemType = "SpatialOrganization"
                    },
                    new DesignPattern
                    {
                        Name = "Service Core",
                        Description = "Group wet rooms to minimize plumbing runs",
                        SolvesProblemType = "PlumbingEfficiency"
                    }
                },
                CommonMistakes = new List<string>
                {
                    "Insufficient storage", "Poor natural light in bathrooms", "Kitchen too far from entry"
                }
            });

            // Commercial office archetype
            _archetypes.Add(new ProjectArchetype
            {
                ArchetypeId = "ARCH002",
                Name = "Small Office Building",
                BuildingType = "Office",
                RelatedBuildingTypes = new List<string> { "Commercial", "Workplace" },
                MinArea = 500,
                MaxArea = 5000,
                TypicalProgram = new Dictionary<string, float>
                {
                    ["OpenOffice"] = 0.50f, ["Meeting"] = 0.15f, ["Reception"] = 0.05f,
                    ["Circulation"] = 0.15f, ["Services"] = 0.10f, ["Amenities"] = 0.05f
                },
                DesignPatterns = new List<DesignPattern>
                {
                    new DesignPattern
                    {
                        Name = "Perimeter Offices",
                        Description = "Position enclosed offices on perimeter for daylight",
                        SolvesProblemType = "DaylightAccess"
                    },
                    new DesignPattern
                    {
                        Name = "Central Core",
                        Description = "Group services, stairs, elevators in central core",
                        SolvesProblemType = "CirculationEfficiency"
                    }
                },
                CommonMistakes = new List<string>
                {
                    "Insufficient meeting rooms", "Poor acoustic separation", "Inadequate power/data"
                }
            });

            // Healthcare archetype
            _archetypes.Add(new ProjectArchetype
            {
                ArchetypeId = "ARCH003",
                Name = "Medical Clinic",
                BuildingType = "Healthcare",
                RelatedBuildingTypes = new List<string> { "Clinic", "Medical" },
                MinArea = 200,
                MaxArea = 2000,
                TypicalProgram = new Dictionary<string, float>
                {
                    ["Waiting"] = 0.15f, ["ExamRoom"] = 0.30f, ["Consultation"] = 0.15f,
                    ["Admin"] = 0.10f, ["Circulation"] = 0.20f, ["Support"] = 0.10f
                },
                DesignPatterns = new List<DesignPattern>
                {
                    new DesignPattern
                    {
                        Name = "Patient Flow",
                        Description = "Separate patient and staff circulation",
                        SolvesProblemType = "CirculationSeparation"
                    },
                    new DesignPattern
                    {
                        Name = "Clean-Dirty Separation",
                        Description = "Separate clean and soiled materials flow",
                        SolvesProblemType = "InfectionControl"
                    }
                },
                CommonMistakes = new List<string>
                {
                    "Patient privacy compromised", "Inadequate waiting space", "Poor wayfinding"
                }
            });
        }
    }

    #endregion

    #region Similarity Calculator

    /// <summary>
    /// Calculates similarity between projects.
    /// </summary>
    public class SimilarityCalculator
    {
        public ProjectSimilarity Calculate(ProjectProfile project1, ProjectProfile project2)
        {
            var similarity = new ProjectSimilarity();

            // Building type similarity
            similarity.BuildingTypeSimilarity = project1.BuildingType == project2.BuildingType ? 1.0f : 0.3f;

            // Scale similarity (area comparison)
            var areaRatio = Math.Min(project1.GrossArea, project2.GrossArea) /
                           Math.Max(project1.GrossArea, project2.GrossArea);
            similarity.ScaleSimilarity = (float)areaRatio;

            // Climate similarity
            similarity.ClimateSimilarity = project1.ClimateZone == project2.ClimateZone ? 1.0f : 0.4f;

            // Program similarity
            if (project1.Program != null && project2.Program != null)
            {
                var commonRooms = project1.Program.Keys.Intersect(project2.Program.Keys).Count();
                var totalRooms = project1.Program.Keys.Union(project2.Program.Keys).Count();
                similarity.ProgramSimilarity = totalRooms > 0 ? (float)commonRooms / totalRooms : 0.5f;
            }
            else
            {
                similarity.ProgramSimilarity = 0.5f;
            }

            // Calculate overall score (weighted average)
            similarity.OverallScore =
                similarity.BuildingTypeSimilarity * 0.35f +
                similarity.ScaleSimilarity * 0.25f +
                similarity.ClimateSimilarity * 0.20f +
                similarity.ProgramSimilarity * 0.20f;

            return similarity;
        }
    }

    #endregion

    #region Types

    /// <summary>
    /// Project profile for comparison.
    /// </summary>
    public class ProjectProfile
    {
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string BuildingType { get; set; }
        public double GrossArea { get; set; }
        public string ClimateZone { get; set; }
        public string Region { get; set; }
        public Dictionary<string, float> Program { get; set; }
        public List<string> AvailableElements { get; set; }
        public List<object> RoomLayouts { get; set; }
        public List<object> MEPStrategies { get; set; }
        public List<object> MaterialChoices { get; set; }
        public List<DesignSolution> Solutions { get; set; }
        public List<SolvedProblem> SolvedProblems { get; set; }
    }

    /// <summary>
    /// Project archetype template.
    /// </summary>
    public class ProjectArchetype
    {
        public string ArchetypeId { get; set; }
        public string Name { get; set; }
        public string BuildingType { get; set; }
        public List<string> RelatedBuildingTypes { get; set; }
        public double MinArea { get; set; }
        public double MaxArea { get; set; }
        public Dictionary<string, float> TypicalProgram { get; set; }
        public List<DesignPattern> DesignPatterns { get; set; }
        public List<string> CommonMistakes { get; set; }
    }

    /// <summary>
    /// Design pattern within an archetype.
    /// </summary>
    public class DesignPattern
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string SolvesProblemType { get; set; }
        public DesignSolution Solution { get; set; }
        public double? MinArea { get; set; }
        public double? MaxArea { get; set; }
        public List<string> RequiredRoomTypes { get; set; }
    }

    /// <summary>
    /// Similarity scores between projects.
    /// </summary>
    public class ProjectSimilarity
    {
        public float BuildingTypeSimilarity { get; set; }
        public float ScaleSimilarity { get; set; }
        public float ClimateSimilarity { get; set; }
        public float ProgramSimilarity { get; set; }
        public float OverallScore { get; set; }
    }

    /// <summary>
    /// A similar project result.
    /// </summary>
    public class SimilarProject
    {
        public ProjectProfile Project { get; set; }
        public ProjectSimilarity Similarity { get; set; }
        public List<TransferableKnowledge> TransferableKnowledge { get; set; }
    }

    /// <summary>
    /// Knowledge that can be transferred.
    /// </summary>
    public class TransferableKnowledge
    {
        public KnowledgeType Type { get; set; }
        public string Description { get; set; }
        public float Confidence { get; set; }
        public object SourceData { get; set; }
    }

    public enum KnowledgeType
    {
        RoomLayout,
        MEPStrategy,
        MaterialChoice,
        SolvedProblem,
        DesignPattern
    }

    /// <summary>
    /// Archetype match result.
    /// </summary>
    public class ArchetypeMatch
    {
        public ProjectArchetype Archetype { get; set; }
        public float MatchScore { get; set; }
        public List<DesignPattern> ApplicablePatterns { get; set; }
        public List<string> Recommendations { get; set; }
    }

    /// <summary>
    /// Solution transfer result.
    /// </summary>
    public class SolutionTransfer
    {
        public string SolutionType { get; set; }
        public ProjectProfile SourceProject { get; set; }
        public ProjectProfile TargetProject { get; set; }
        public bool CanTransfer { get; set; }
        public float ContextSimilarity { get; set; }
        public float ConfidenceScore { get; set; }
        public List<Adaptation> RequiredAdaptations { get; set; }
        public DesignSolution AdaptedSolution { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// A design solution.
    /// </summary>
    public class DesignSolution
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public double? MinScale { get; set; }
        public List<string> RequiredElements { get; set; }
        public string ClimateZone { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    /// <summary>
    /// A solved design problem.
    /// </summary>
    public class SolvedProblem
    {
        public string ProblemType { get; set; }
        public string Description { get; set; }
        public DesignSolution Solution { get; set; }
        public ProblemContext Context { get; set; }
        public List<string> Constraints { get; set; }
        public bool WasSuccessful { get; set; }
    }

    /// <summary>
    /// A design problem to solve.
    /// </summary>
    public class DesignProblem
    {
        public string ProblemType { get; set; }
        public string Description { get; set; }
        public ProblemContext Context { get; set; }
        public List<string> Constraints { get; set; }
    }

    /// <summary>
    /// Context of a problem.
    /// </summary>
    public class ProblemContext
    {
        public string RoomType { get; set; }
        public string BuildingType { get; set; }
        public string ClimateZone { get; set; }
    }

    /// <summary>
    /// An analogy between design problems.
    /// </summary>
    public class DesignAnalogy
    {
        public SolvedProblem SourceProblem { get; set; }
        public ProjectProfile SourceProject { get; set; }
        public DesignPattern SourcePattern { get; set; }
        public float Similarity { get; set; }
        public DesignSolution SourceSolution { get; set; }
        public string AdaptationNeeded { get; set; }
    }

    /// <summary>
    /// Solution applicability assessment.
    /// </summary>
    public class SolutionApplicability
    {
        public bool IsApplicable { get; set; }
        public float Confidence { get; set; }
        public List<Adaptation> RequiredAdaptations { get; set; }
        public string RejectionReason { get; set; }
    }

    /// <summary>
    /// An adaptation needed.
    /// </summary>
    public class Adaptation
    {
        public AdaptationType Type { get; set; }
        public string Description { get; set; }
    }

    public enum AdaptationType
    {
        Scale,
        ClimateAdapt,
        AddElement,
        ModifyParameter
    }

    #endregion
}
