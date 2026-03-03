// ===================================================================
// StingBIM Resource Intelligence Engine
// Team management, skill matching, and workload optimization
// Copyright (c) 2026 StingBIM. All rights reserved.
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.ResourceIntelligence
{
    /// <summary>
    /// Comprehensive resource intelligence for team allocation,
    /// skill matching, workload balancing, and training management
    /// </summary>
    public sealed class ResourceIntelligenceEngine
    {
        private static readonly Lazy<ResourceIntelligenceEngine> _instance =
            new Lazy<ResourceIntelligenceEngine>(() => new ResourceIntelligenceEngine());
        public static ResourceIntelligenceEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, ResourcePool> _resourcePools;
        private readonly ConcurrentDictionary<string, TeamMember> _teamMembers;
        private readonly ConcurrentDictionary<string, Project> _projects;
        private readonly ConcurrentDictionary<string, Skill> _skills;
        private readonly object _lockObject = new object();

        public event EventHandler<ResourceAlertEventArgs> ResourceAlertRaised;

        private ResourceIntelligenceEngine()
        {
            _resourcePools = new ConcurrentDictionary<string, ResourcePool>();
            _teamMembers = new ConcurrentDictionary<string, TeamMember>();
            _projects = new ConcurrentDictionary<string, Project>();
            _skills = new ConcurrentDictionary<string, Skill>();

            InitializeSkillsLibrary();
        }

        #region Skills Library

        private void InitializeSkillsLibrary()
        {
            var skills = new List<Skill>
            {
                // BIM Skills
                new Skill { Id = "BIM-001", Name = "Revit Architecture", Category = "BIM", Level = SkillLevel.Advanced, Description = "Autodesk Revit for architectural modeling" },
                new Skill { Id = "BIM-002", Name = "Revit Structure", Category = "BIM", Level = SkillLevel.Advanced, Description = "Autodesk Revit for structural modeling" },
                new Skill { Id = "BIM-003", Name = "Revit MEP", Category = "BIM", Level = SkillLevel.Advanced, Description = "Autodesk Revit for MEP systems" },
                new Skill { Id = "BIM-004", Name = "Navisworks", Category = "BIM", Level = SkillLevel.Intermediate, Description = "Clash detection and coordination" },
                new Skill { Id = "BIM-005", Name = "BIM 360", Category = "BIM", Level = SkillLevel.Intermediate, Description = "Cloud collaboration platform" },
                new Skill { Id = "BIM-006", Name = "Dynamo", Category = "BIM", Level = SkillLevel.Advanced, Description = "Visual programming for Revit" },
                new Skill { Id = "BIM-007", Name = "IFC/OpenBIM", Category = "BIM", Level = SkillLevel.Intermediate, Description = "Open BIM standards and interoperability" },

                // Project Management
                new Skill { Id = "PM-001", Name = "Project Planning", Category = "Management", Level = SkillLevel.Advanced, Description = "Project scheduling and planning" },
                new Skill { Id = "PM-002", Name = "Cost Management", Category = "Management", Level = SkillLevel.Advanced, Description = "Budget and cost control" },
                new Skill { Id = "PM-003", Name = "Risk Management", Category = "Management", Level = SkillLevel.Intermediate, Description = "Risk identification and mitigation" },
                new Skill { Id = "PM-004", Name = "Contract Administration", Category = "Management", Level = SkillLevel.Intermediate, Description = "Contract management" },
                new Skill { Id = "PM-005", Name = "Stakeholder Management", Category = "Management", Level = SkillLevel.Advanced, Description = "Client and stakeholder relations" },

                // Technical Skills
                new Skill { Id = "TECH-001", Name = "Structural Analysis", Category = "Technical", Level = SkillLevel.Expert, Description = "Structural engineering analysis" },
                new Skill { Id = "TECH-002", Name = "HVAC Design", Category = "Technical", Level = SkillLevel.Expert, Description = "Mechanical systems design" },
                new Skill { Id = "TECH-003", Name = "Electrical Design", Category = "Technical", Level = SkillLevel.Expert, Description = "Electrical systems design" },
                new Skill { Id = "TECH-004", Name = "Plumbing Design", Category = "Technical", Level = SkillLevel.Advanced, Description = "Plumbing systems design" },
                new Skill { Id = "TECH-005", Name = "Fire Protection", Category = "Technical", Level = SkillLevel.Advanced, Description = "Fire protection engineering" },
                new Skill { Id = "TECH-006", Name = "Sustainability/LEED", Category = "Technical", Level = SkillLevel.Advanced, Description = "Green building certification" },
                new Skill { Id = "TECH-007", Name = "Energy Modeling", Category = "Technical", Level = SkillLevel.Advanced, Description = "Building energy analysis" },

                // Construction
                new Skill { Id = "CON-001", Name = "Site Supervision", Category = "Construction", Level = SkillLevel.Advanced, Description = "Construction site management" },
                new Skill { Id = "CON-002", Name = "Quality Control", Category = "Construction", Level = SkillLevel.Advanced, Description = "QA/QC processes" },
                new Skill { Id = "CON-003", Name = "Safety Management", Category = "Construction", Level = SkillLevel.Advanced, Description = "Construction safety" },
                new Skill { Id = "CON-004", Name = "Scheduling", Category = "Construction", Level = SkillLevel.Advanced, Description = "Construction scheduling" },
                new Skill { Id = "CON-005", Name = "Estimating", Category = "Construction", Level = SkillLevel.Advanced, Description = "Construction cost estimating" },

                // Software
                new Skill { Id = "SW-001", Name = "AutoCAD", Category = "Software", Level = SkillLevel.Intermediate, Description = "2D drafting" },
                new Skill { Id = "SW-002", Name = "SketchUp", Category = "Software", Level = SkillLevel.Intermediate, Description = "3D modeling" },
                new Skill { Id = "SW-003", Name = "Primavera P6", Category = "Software", Level = SkillLevel.Advanced, Description = "Project scheduling" },
                new Skill { Id = "SW-004", Name = "MS Project", Category = "Software", Level = SkillLevel.Intermediate, Description = "Project scheduling" },
                new Skill { Id = "SW-005", Name = "Bluebeam", Category = "Software", Level = SkillLevel.Intermediate, Description = "PDF markup and review" },
                new Skill { Id = "SW-006", Name = "Solibri", Category = "Software", Level = SkillLevel.Advanced, Description = "Model checking" }
            };

            foreach (var skill in skills)
            {
                _skills.TryAdd(skill.Id, skill);
            }
        }

        #endregion

        #region Team Member Management

        public TeamMember AddTeamMember(TeamMemberRequest request)
        {
            var member = new TeamMember
            {
                Id = Guid.NewGuid().ToString(),
                EmployeeId = request.EmployeeId,
                Name = request.Name,
                Email = request.Email,
                Phone = request.Phone,
                Title = request.Title,
                Department = request.Department,
                Location = request.Location,
                HireDate = request.HireDate,
                HourlyRate = request.HourlyRate,
                Status = MemberStatus.Active,
                Skills = new List<MemberSkill>(),
                Certifications = new List<Certification>(),
                Assignments = new List<Assignment>(),
                Availability = new Availability
                {
                    StandardHoursPerWeek = 40,
                    AvailableFrom = DateTime.UtcNow,
                    Exceptions = new List<AvailabilityException>()
                },
                TrainingRecords = new List<TrainingRecord>()
            };

            // Add initial skills
            if (request.Skills != null)
            {
                foreach (var skill in request.Skills)
                {
                    member.Skills.Add(new MemberSkill
                    {
                        SkillId = skill.SkillId,
                        SkillName = _skills.TryGetValue(skill.SkillId, out var s) ? s.Name : skill.SkillId,
                        Level = skill.Level,
                        YearsExperience = skill.YearsExperience,
                        LastUsed = skill.LastUsed,
                        Verified = skill.Verified
                    });
                }
            }

            _teamMembers.TryAdd(member.Id, member);
            return member;
        }

        public TeamMember UpdateSkills(string memberId, List<MemberSkillInput> skills)
        {
            if (!_teamMembers.TryGetValue(memberId, out var member))
                return null;

            foreach (var skill in skills)
            {
                var existing = member.Skills.FirstOrDefault(s => s.SkillId == skill.SkillId);
                if (existing != null)
                {
                    existing.Level = skill.Level;
                    existing.YearsExperience = skill.YearsExperience;
                    existing.LastUsed = skill.LastUsed;
                    existing.Verified = skill.Verified;
                }
                else
                {
                    member.Skills.Add(new MemberSkill
                    {
                        SkillId = skill.SkillId,
                        SkillName = _skills.TryGetValue(skill.SkillId, out var s) ? s.Name : skill.SkillId,
                        Level = skill.Level,
                        YearsExperience = skill.YearsExperience,
                        LastUsed = skill.LastUsed,
                        Verified = skill.Verified
                    });
                }
            }

            return member;
        }

        public Certification AddCertification(string memberId, CertificationRequest request)
        {
            if (!_teamMembers.TryGetValue(memberId, out var member))
                return null;

            var cert = new Certification
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                IssuingOrganization = request.IssuingOrganization,
                CertificationNumber = request.CertificationNumber,
                IssueDate = request.IssueDate,
                ExpirationDate = request.ExpirationDate,
                Status = request.ExpirationDate > DateTime.UtcNow ? CertStatus.Active : CertStatus.Expired
            };

            lock (_lockObject)
            {
                member.Certifications.Add(cert);
            }

            // Alert if certification expiring soon
            if (cert.ExpirationDate.HasValue && cert.ExpirationDate.Value < DateTime.UtcNow.AddDays(60))
            {
                ResourceAlertRaised?.Invoke(this, new ResourceAlertEventArgs
                {
                    MemberId = memberId,
                    AlertType = "Certification Expiring",
                    Message = $"{cert.Name} expires on {cert.ExpirationDate:d}"
                });
            }

            return cert;
        }

        #endregion

        #region Resource Allocation

        public Assignment AssignToProject(AssignmentRequest request)
        {
            if (!_teamMembers.TryGetValue(request.MemberId, out var member))
                return null;

            // Check availability
            var utilization = CalculateUtilization(member, request.StartDate, request.EndDate);
            if (utilization + request.AllocationPercent > 100)
            {
                ResourceAlertRaised?.Invoke(this, new ResourceAlertEventArgs
                {
                    MemberId = request.MemberId,
                    AlertType = "Over-allocation",
                    Message = $"{member.Name} would be {utilization + request.AllocationPercent}% allocated"
                });
            }

            var assignment = new Assignment
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                ProjectName = request.ProjectName,
                Role = request.Role,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                AllocationPercent = request.AllocationPercent,
                HoursPerWeek = (request.AllocationPercent / 100) * member.Availability.StandardHoursPerWeek,
                Status = AssignmentStatus.Active,
                AssignedDate = DateTime.UtcNow,
                AssignedBy = request.AssignedBy
            };

            lock (_lockObject)
            {
                member.Assignments.Add(assignment);
            }

            return assignment;
        }

        public decimal CalculateUtilization(TeamMember member, DateTime startDate, DateTime endDate)
        {
            var activeAssignments = member.Assignments
                .Where(a => a.Status == AssignmentStatus.Active &&
                           a.StartDate <= endDate && a.EndDate >= startDate)
                .ToList();

            return activeAssignments.Sum(a => a.AllocationPercent);
        }

        public List<ResourceMatch> FindResources(ResourceSearchCriteria criteria)
        {
            var matches = new List<ResourceMatch>();

            foreach (var member in _teamMembers.Values.Where(m => m.Status == MemberStatus.Active))
            {
                var match = new ResourceMatch
                {
                    MemberId = member.Id,
                    MemberName = member.Name,
                    Title = member.Title,
                    Department = member.Department,
                    Location = member.Location,
                    MatchScore = 0,
                    SkillMatches = new List<SkillMatch>(),
                    CurrentUtilization = CalculateUtilization(member, criteria.StartDate, criteria.EndDate)
                };

                // Check availability
                if (match.CurrentUtilization >= 100)
                {
                    match.AvailabilityStatus = "Fully Allocated";
                    continue;
                }
                else if (match.CurrentUtilization >= 80)
                {
                    match.AvailabilityStatus = "Limited Availability";
                }
                else
                {
                    match.AvailabilityStatus = "Available";
                }

                // Match skills
                decimal skillScore = 0;
                foreach (var requiredSkill in criteria.RequiredSkills ?? new List<SkillRequirement>())
                {
                    var memberSkill = member.Skills.FirstOrDefault(s => s.SkillId == requiredSkill.SkillId);
                    if (memberSkill != null)
                    {
                        var levelScore = (int)memberSkill.Level >= (int)requiredSkill.MinimumLevel ? 1.0m : 0.5m;
                        skillScore += levelScore * (requiredSkill.Weight ?? 1.0m);

                        match.SkillMatches.Add(new SkillMatch
                        {
                            SkillId = requiredSkill.SkillId,
                            SkillName = memberSkill.SkillName,
                            RequiredLevel = requiredSkill.MinimumLevel,
                            ActualLevel = memberSkill.Level,
                            Match = levelScore >= 1.0m
                        });
                    }
                    else if (requiredSkill.Required)
                    {
                        match.SkillMatches.Add(new SkillMatch
                        {
                            SkillId = requiredSkill.SkillId,
                            SkillName = _skills.TryGetValue(requiredSkill.SkillId, out var s) ? s.Name : requiredSkill.SkillId,
                            RequiredLevel = requiredSkill.MinimumLevel,
                            ActualLevel = SkillLevel.None,
                            Match = false
                        });
                    }
                }

                // Check certifications
                if (criteria.RequiredCertifications != null)
                {
                    foreach (var reqCert in criteria.RequiredCertifications)
                    {
                        var hasCert = member.Certifications.Any(c =>
                            c.Name.Contains(reqCert, StringComparison.OrdinalIgnoreCase) &&
                            c.Status == CertStatus.Active);

                        if (hasCert) skillScore += 0.5m;
                    }
                }

                // Location preference
                if (!string.IsNullOrEmpty(criteria.PreferredLocation) &&
                    member.Location == criteria.PreferredLocation)
                {
                    skillScore += 0.25m;
                }

                // Calculate final score
                var maxScore = (criteria.RequiredSkills?.Count ?? 0) + (criteria.RequiredCertifications?.Count ?? 0) * 0.5m + 0.25m;
                match.MatchScore = maxScore > 0 ? (skillScore / maxScore) * 100 : 100;
                match.AvailableHours = member.Availability.StandardHoursPerWeek * (100 - match.CurrentUtilization) / 100;

                if (match.MatchScore >= (criteria.MinimumMatchScore ?? 50))
                {
                    matches.Add(match);
                }
            }

            return matches
                .OrderByDescending(m => m.MatchScore)
                .ThenBy(m => m.CurrentUtilization)
                .ToList();
        }

        #endregion

        #region Workload Analysis

        public WorkloadAnalysis AnalyzeWorkload(string memberId)
        {
            if (!_teamMembers.TryGetValue(memberId, out var member))
                return null;

            var analysis = new WorkloadAnalysis
            {
                MemberId = memberId,
                MemberName = member.Name,
                AnalysisDate = DateTime.UtcNow,
                StandardHoursPerWeek = member.Availability.StandardHoursPerWeek,
                WeeklyBreakdown = new List<WeeklyWorkload>()
            };

            // Analyze next 12 weeks
            for (int week = 0; week < 12; week++)
            {
                var weekStart = DateTime.UtcNow.AddDays(7 * week).Date;
                var weekEnd = weekStart.AddDays(6);

                var weekAssignments = member.Assignments
                    .Where(a => a.Status == AssignmentStatus.Active &&
                               a.StartDate <= weekEnd && a.EndDate >= weekStart)
                    .ToList();

                var totalHours = weekAssignments.Sum(a => a.HoursPerWeek);
                var utilization = (totalHours / member.Availability.StandardHoursPerWeek) * 100;

                analysis.WeeklyBreakdown.Add(new WeeklyWorkload
                {
                    WeekStarting = weekStart,
                    TotalHours = totalHours,
                    UtilizationPercent = utilization,
                    ProjectBreakdown = weekAssignments.Select(a => new ProjectHours
                    {
                        ProjectId = a.ProjectId,
                        ProjectName = a.ProjectName,
                        Hours = a.HoursPerWeek,
                        Role = a.Role
                    }).ToList(),
                    Status = utilization > 100 ? "Over-allocated" :
                            utilization > 80 ? "High" :
                            utilization > 50 ? "Optimal" : "Under-utilized"
                });
            }

            analysis.AverageUtilization = analysis.WeeklyBreakdown.Average(w => w.UtilizationPercent);
            analysis.PeakUtilization = analysis.WeeklyBreakdown.Max(w => w.UtilizationPercent);
            analysis.OverAllocatedWeeks = analysis.WeeklyBreakdown.Count(w => w.UtilizationPercent > 100);
            analysis.UnderUtilizedWeeks = analysis.WeeklyBreakdown.Count(w => w.UtilizationPercent < 50);

            // Recommendations
            analysis.Recommendations = GenerateWorkloadRecommendations(analysis);

            return analysis;
        }

        private List<string> GenerateWorkloadRecommendations(WorkloadAnalysis analysis)
        {
            var recommendations = new List<string>();

            if (analysis.OverAllocatedWeeks > 0)
            {
                recommendations.Add($"Resource is over-allocated for {analysis.OverAllocatedWeeks} weeks - consider reassigning tasks");
            }

            if (analysis.UnderUtilizedWeeks > 4)
            {
                recommendations.Add($"Resource is under-utilized for {analysis.UnderUtilizedWeeks} weeks - consider additional assignments");
            }

            if (analysis.PeakUtilization > 120)
            {
                recommendations.Add("Peak utilization exceeds 120% - immediate workload balancing required");
            }

            if (analysis.AverageUtilization < 60)
            {
                recommendations.Add("Average utilization below 60% - resource may be available for additional projects");
            }

            return recommendations;
        }

        public TeamWorkloadSummary GetTeamWorkload(List<string> memberIds, DateTime startDate, DateTime endDate)
        {
            var summary = new TeamWorkloadSummary
            {
                StartDate = startDate,
                EndDate = endDate,
                MemberWorkloads = new List<MemberWorkloadSummary>(),
                GeneratedDate = DateTime.UtcNow
            };

            foreach (var memberId in memberIds)
            {
                if (!_teamMembers.TryGetValue(memberId, out var member))
                    continue;

                var utilization = CalculateUtilization(member, startDate, endDate);
                var assignments = member.Assignments
                    .Where(a => a.Status == AssignmentStatus.Active &&
                               a.StartDate <= endDate && a.EndDate >= startDate)
                    .ToList();

                summary.MemberWorkloads.Add(new MemberWorkloadSummary
                {
                    MemberId = memberId,
                    MemberName = member.Name,
                    Title = member.Title,
                    Utilization = utilization,
                    AssignmentCount = assignments.Count,
                    TotalHoursPerWeek = assignments.Sum(a => a.HoursPerWeek),
                    Status = utilization > 100 ? "Over-allocated" :
                            utilization > 80 ? "High" :
                            utilization > 50 ? "Optimal" : "Available"
                });
            }

            summary.TotalMembers = summary.MemberWorkloads.Count;
            summary.AverageUtilization = summary.MemberWorkloads.Any()
                ? summary.MemberWorkloads.Average(m => m.Utilization)
                : 0;
            summary.OverAllocatedCount = summary.MemberWorkloads.Count(m => m.Utilization > 100);
            summary.AvailableCount = summary.MemberWorkloads.Count(m => m.Utilization < 80);

            return summary;
        }

        #endregion

        #region Training Management

        public TrainingPlan CreateTrainingPlan(TrainingPlanRequest request)
        {
            var plan = new TrainingPlan
            {
                Id = Guid.NewGuid().ToString(),
                MemberId = request.MemberId,
                Name = request.Name,
                CreatedDate = DateTime.UtcNow,
                TargetCompletionDate = request.TargetCompletionDate,
                Status = TrainingPlanStatus.Draft,
                Courses = new List<TrainingCourse>(),
                Goals = new List<TrainingGoal>()
            };

            // Identify skill gaps
            if (_teamMembers.TryGetValue(request.MemberId, out var member))
            {
                foreach (var targetSkill in request.TargetSkills ?? new List<SkillTarget>())
                {
                    var currentSkill = member.Skills.FirstOrDefault(s => s.SkillId == targetSkill.SkillId);
                    var currentLevel = currentSkill?.Level ?? SkillLevel.None;

                    if ((int)currentLevel < (int)targetSkill.TargetLevel)
                    {
                        plan.Goals.Add(new TrainingGoal
                        {
                            SkillId = targetSkill.SkillId,
                            SkillName = _skills.TryGetValue(targetSkill.SkillId, out var s) ? s.Name : targetSkill.SkillId,
                            CurrentLevel = currentLevel,
                            TargetLevel = targetSkill.TargetLevel,
                            Priority = targetSkill.Priority
                        });

                        // Recommend courses
                        var courses = RecommendCourses(targetSkill.SkillId, currentLevel, targetSkill.TargetLevel);
                        plan.Courses.AddRange(courses);
                    }
                }
            }

            return plan;
        }

        private List<TrainingCourse> RecommendCourses(string skillId, SkillLevel currentLevel, SkillLevel targetLevel)
        {
            var courses = new List<TrainingCourse>();

            // Course recommendations based on skill and level gap
            var skillName = _skills.TryGetValue(skillId, out var skill) ? skill.Name : skillId;

            if (skillId.StartsWith("BIM"))
            {
                if (currentLevel < SkillLevel.Intermediate)
                {
                    courses.Add(new TrainingCourse
                    {
                        Name = $"{skillName} Fundamentals",
                        Provider = "Autodesk Learning",
                        Duration = 16,
                        Type = CourseType.Online,
                        Cost = 299,
                        Status = CourseStatus.Recommended
                    });
                }

                if (targetLevel >= SkillLevel.Advanced)
                {
                    courses.Add(new TrainingCourse
                    {
                        Name = $"{skillName} Advanced Techniques",
                        Provider = "Autodesk Learning",
                        Duration = 24,
                        Type = CourseType.Online,
                        Cost = 499,
                        Status = CourseStatus.Recommended
                    });
                }

                if (targetLevel == SkillLevel.Expert)
                {
                    courses.Add(new TrainingCourse
                    {
                        Name = $"{skillName} Expert Certification Prep",
                        Provider = "Autodesk",
                        Duration = 40,
                        Type = CourseType.InPerson,
                        Cost = 1500,
                        Status = CourseStatus.Recommended
                    });
                }
            }
            else if (skillId.StartsWith("PM"))
            {
                courses.Add(new TrainingCourse
                {
                    Name = "Project Management Professional (PMP) Prep",
                    Provider = "PMI",
                    Duration = 35,
                    Type = CourseType.Online,
                    Cost = 1200,
                    Status = CourseStatus.Recommended
                });
            }

            return courses;
        }

        public TrainingRecord RecordTraining(string memberId, TrainingRecordRequest request)
        {
            if (!_teamMembers.TryGetValue(memberId, out var member))
                return null;

            var record = new TrainingRecord
            {
                Id = Guid.NewGuid().ToString(),
                CourseName = request.CourseName,
                Provider = request.Provider,
                CompletionDate = request.CompletionDate,
                Duration = request.Duration,
                Score = request.Score,
                CertificateNumber = request.CertificateNumber,
                SkillsGained = request.SkillsGained
            };

            lock (_lockObject)
            {
                member.TrainingRecords.Add(record);

                // Update skill levels if applicable
                if (request.SkillsGained != null)
                {
                    foreach (var skillGained in request.SkillsGained)
                    {
                        var existingSkill = member.Skills.FirstOrDefault(s => s.SkillId == skillGained.SkillId);
                        if (existingSkill != null && (int)skillGained.NewLevel > (int)existingSkill.Level)
                        {
                            existingSkill.Level = skillGained.NewLevel;
                            existingSkill.LastUsed = DateTime.UtcNow;
                        }
                        else if (existingSkill == null)
                        {
                            member.Skills.Add(new MemberSkill
                            {
                                SkillId = skillGained.SkillId,
                                SkillName = _skills.TryGetValue(skillGained.SkillId, out var s) ? s.Name : skillGained.SkillId,
                                Level = skillGained.NewLevel,
                                LastUsed = DateTime.UtcNow
                            });
                        }
                    }
                }
            }

            return record;
        }

        #endregion

        #region Resource Pool Management

        public ResourcePool CreateResourcePool(ResourcePoolRequest request)
        {
            var pool = new ResourcePool
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                Manager = request.Manager,
                CreatedDate = DateTime.UtcNow,
                Members = new List<string>()
            };

            _resourcePools.TryAdd(pool.Id, pool);
            return pool;
        }

        public ResourcePool AddToPool(string poolId, string memberId)
        {
            if (!_resourcePools.TryGetValue(poolId, out var pool))
                return null;

            if (!pool.Members.Contains(memberId))
            {
                pool.Members.Add(memberId);
            }

            return pool;
        }

        public ResourcePoolSummary GetPoolSummary(string poolId)
        {
            if (!_resourcePools.TryGetValue(poolId, out var pool))
                return null;

            var members = pool.Members
                .Select(id => _teamMembers.TryGetValue(id, out var m) ? m : null)
                .Where(m => m != null)
                .ToList();

            var summary = new ResourcePoolSummary
            {
                PoolId = poolId,
                PoolName = pool.Name,
                TotalMembers = members.Count,
                ActiveMembers = members.Count(m => m.Status == MemberStatus.Active),
                TotalCapacityHours = members.Sum(m => m.Availability.StandardHoursPerWeek),
                AllocatedHours = members.Sum(m => m.Assignments.Where(a => a.Status == AssignmentStatus.Active).Sum(a => a.HoursPerWeek)),
                SkillsSummary = new Dictionary<string, int>()
            };

            // Aggregate skills
            foreach (var member in members)
            {
                foreach (var skill in member.Skills)
                {
                    if (summary.SkillsSummary.ContainsKey(skill.SkillName))
                        summary.SkillsSummary[skill.SkillName]++;
                    else
                        summary.SkillsSummary[skill.SkillName] = 1;
                }
            }

            summary.UtilizationPercent = summary.TotalCapacityHours > 0
                ? (summary.AllocatedHours / summary.TotalCapacityHours) * 100
                : 0;

            return summary;
        }

        #endregion

        #region Capacity Planning

        public CapacityForecast ForecastCapacity(CapacityForecastRequest request)
        {
            var forecast = new CapacityForecast
            {
                Id = Guid.NewGuid().ToString(),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                GeneratedDate = DateTime.UtcNow,
                MonthlyCapacity = new List<MonthlyCapacity>()
            };

            var members = _teamMembers.Values
                .Where(m => m.Status == MemberStatus.Active)
                .ToList();

            if (request.PoolId != null && _resourcePools.TryGetValue(request.PoolId, out var pool))
            {
                members = members.Where(m => pool.Members.Contains(m.Id)).ToList();
            }

            // Calculate monthly capacity
            var currentDate = new DateTime(request.StartDate.Year, request.StartDate.Month, 1);
            while (currentDate <= request.EndDate)
            {
                var monthEnd = currentDate.AddMonths(1).AddDays(-1);
                var weeksInMonth = 4.33m; // Average weeks per month

                var monthCapacity = new MonthlyCapacity
                {
                    Month = currentDate,
                    MonthName = currentDate.ToString("MMMM yyyy"),
                    TotalCapacity = members.Sum(m => m.Availability.StandardHoursPerWeek * weeksInMonth),
                    AllocatedHours = 0,
                    AvailableHours = 0,
                    SkillCapacity = new Dictionary<string, decimal>()
                };

                // Calculate allocated hours
                foreach (var member in members)
                {
                    var assignments = member.Assignments
                        .Where(a => a.Status == AssignmentStatus.Active &&
                                   a.StartDate <= monthEnd && a.EndDate >= currentDate)
                        .ToList();

                    monthCapacity.AllocatedHours += assignments.Sum(a => a.HoursPerWeek * weeksInMonth);
                }

                monthCapacity.AvailableHours = monthCapacity.TotalCapacity - monthCapacity.AllocatedHours;
                monthCapacity.UtilizationPercent = monthCapacity.TotalCapacity > 0
                    ? (monthCapacity.AllocatedHours / monthCapacity.TotalCapacity) * 100
                    : 0;

                // Skill capacity
                var skillGroups = members.SelectMany(m => m.Skills)
                    .GroupBy(s => s.SkillName);

                foreach (var group in skillGroups)
                {
                    var skillMembers = members.Where(m => m.Skills.Any(s => s.SkillName == group.Key)).ToList();
                    var skillCapacity = skillMembers.Sum(m => m.Availability.StandardHoursPerWeek * weeksInMonth);
                    var skillAllocated = skillMembers.Sum(m =>
                        m.Assignments.Where(a => a.Status == AssignmentStatus.Active &&
                                                a.StartDate <= monthEnd && a.EndDate >= currentDate)
                            .Sum(a => a.HoursPerWeek * weeksInMonth));

                    monthCapacity.SkillCapacity[group.Key] = skillCapacity - skillAllocated;
                }

                forecast.MonthlyCapacity.Add(monthCapacity);
                currentDate = currentDate.AddMonths(1);
            }

            forecast.TotalCapacity = forecast.MonthlyCapacity.Sum(m => m.TotalCapacity);
            forecast.TotalAllocated = forecast.MonthlyCapacity.Sum(m => m.AllocatedHours);
            forecast.TotalAvailable = forecast.MonthlyCapacity.Sum(m => m.AvailableHours);
            forecast.AverageUtilization = forecast.MonthlyCapacity.Average(m => m.UtilizationPercent);

            return forecast;
        }

        #endregion

        #region Helper Methods

        public TeamMember GetTeamMember(string memberId)
        {
            _teamMembers.TryGetValue(memberId, out var member);
            return member;
        }

        public List<TeamMember> GetAllTeamMembers()
        {
            return _teamMembers.Values.ToList();
        }

        public List<Skill> GetAllSkills()
        {
            return _skills.Values.ToList();
        }

        public List<Skill> GetSkillsByCategory(string category)
        {
            return _skills.Values.Where(s => s.Category == category).ToList();
        }

        #endregion
    }

    #region Data Models

    public class ResourcePool
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Manager { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<string> Members { get; set; }
    }

    public class ResourcePoolRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Manager { get; set; }
    }

    public class TeamMember
    {
        public string Id { get; set; }
        public string EmployeeId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Title { get; set; }
        public string Department { get; set; }
        public string Location { get; set; }
        public DateTime HireDate { get; set; }
        public decimal HourlyRate { get; set; }
        public MemberStatus Status { get; set; }
        public List<MemberSkill> Skills { get; set; }
        public List<Certification> Certifications { get; set; }
        public List<Assignment> Assignments { get; set; }
        public Availability Availability { get; set; }
        public List<TrainingRecord> TrainingRecords { get; set; }
    }

    public class TeamMemberRequest
    {
        public string EmployeeId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Title { get; set; }
        public string Department { get; set; }
        public string Location { get; set; }
        public DateTime HireDate { get; set; }
        public decimal HourlyRate { get; set; }
        public List<MemberSkillInput> Skills { get; set; }
    }

    public class Skill
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public SkillLevel Level { get; set; }
        public string Description { get; set; }
    }

    public class MemberSkill
    {
        public string SkillId { get; set; }
        public string SkillName { get; set; }
        public SkillLevel Level { get; set; }
        public int YearsExperience { get; set; }
        public DateTime? LastUsed { get; set; }
        public bool Verified { get; set; }
    }

    public class MemberSkillInput
    {
        public string SkillId { get; set; }
        public SkillLevel Level { get; set; }
        public int YearsExperience { get; set; }
        public DateTime? LastUsed { get; set; }
        public bool Verified { get; set; }
    }

    public class Certification
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string IssuingOrganization { get; set; }
        public string CertificationNumber { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public CertStatus Status { get; set; }
    }

    public class CertificationRequest
    {
        public string Name { get; set; }
        public string IssuingOrganization { get; set; }
        public string CertificationNumber { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
    }

    public class Assignment
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string Role { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal AllocationPercent { get; set; }
        public decimal HoursPerWeek { get; set; }
        public AssignmentStatus Status { get; set; }
        public DateTime AssignedDate { get; set; }
        public string AssignedBy { get; set; }
    }

    public class AssignmentRequest
    {
        public string MemberId { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string Role { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal AllocationPercent { get; set; }
        public string AssignedBy { get; set; }
    }

    public class Availability
    {
        public decimal StandardHoursPerWeek { get; set; }
        public DateTime AvailableFrom { get; set; }
        public DateTime? AvailableUntil { get; set; }
        public List<AvailabilityException> Exceptions { get; set; }
    }

    public class AvailabilityException
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; }
        public decimal HoursAvailable { get; set; }
    }

    public class Project
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class ResourceSearchCriteria
    {
        public List<SkillRequirement> RequiredSkills { get; set; }
        public List<string> RequiredCertifications { get; set; }
        public string PreferredLocation { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal? MinimumMatchScore { get; set; }
    }

    public class SkillRequirement
    {
        public string SkillId { get; set; }
        public SkillLevel MinimumLevel { get; set; }
        public bool Required { get; set; }
        public decimal? Weight { get; set; }
    }

    public class ResourceMatch
    {
        public string MemberId { get; set; }
        public string MemberName { get; set; }
        public string Title { get; set; }
        public string Department { get; set; }
        public string Location { get; set; }
        public decimal MatchScore { get; set; }
        public List<SkillMatch> SkillMatches { get; set; }
        public decimal CurrentUtilization { get; set; }
        public string AvailabilityStatus { get; set; }
        public decimal AvailableHours { get; set; }
    }

    public class SkillMatch
    {
        public string SkillId { get; set; }
        public string SkillName { get; set; }
        public SkillLevel RequiredLevel { get; set; }
        public SkillLevel ActualLevel { get; set; }
        public bool Match { get; set; }
    }

    public class WorkloadAnalysis
    {
        public string MemberId { get; set; }
        public string MemberName { get; set; }
        public DateTime AnalysisDate { get; set; }
        public decimal StandardHoursPerWeek { get; set; }
        public decimal AverageUtilization { get; set; }
        public decimal PeakUtilization { get; set; }
        public int OverAllocatedWeeks { get; set; }
        public int UnderUtilizedWeeks { get; set; }
        public List<WeeklyWorkload> WeeklyBreakdown { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class WeeklyWorkload
    {
        public DateTime WeekStarting { get; set; }
        public decimal TotalHours { get; set; }
        public decimal UtilizationPercent { get; set; }
        public List<ProjectHours> ProjectBreakdown { get; set; }
        public string Status { get; set; }
    }

    public class ProjectHours
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public decimal Hours { get; set; }
        public string Role { get; set; }
    }

    public class TeamWorkloadSummary
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalMembers { get; set; }
        public decimal AverageUtilization { get; set; }
        public int OverAllocatedCount { get; set; }
        public int AvailableCount { get; set; }
        public List<MemberWorkloadSummary> MemberWorkloads { get; set; }
    }

    public class MemberWorkloadSummary
    {
        public string MemberId { get; set; }
        public string MemberName { get; set; }
        public string Title { get; set; }
        public decimal Utilization { get; set; }
        public int AssignmentCount { get; set; }
        public decimal TotalHoursPerWeek { get; set; }
        public string Status { get; set; }
    }

    public class TrainingPlan
    {
        public string Id { get; set; }
        public string MemberId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime TargetCompletionDate { get; set; }
        public TrainingPlanStatus Status { get; set; }
        public List<TrainingGoal> Goals { get; set; }
        public List<TrainingCourse> Courses { get; set; }
    }

    public class TrainingPlanRequest
    {
        public string MemberId { get; set; }
        public string Name { get; set; }
        public DateTime TargetCompletionDate { get; set; }
        public List<SkillTarget> TargetSkills { get; set; }
    }

    public class SkillTarget
    {
        public string SkillId { get; set; }
        public SkillLevel TargetLevel { get; set; }
        public int Priority { get; set; }
    }

    public class TrainingGoal
    {
        public string SkillId { get; set; }
        public string SkillName { get; set; }
        public SkillLevel CurrentLevel { get; set; }
        public SkillLevel TargetLevel { get; set; }
        public int Priority { get; set; }
    }

    public class TrainingCourse
    {
        public string Name { get; set; }
        public string Provider { get; set; }
        public int Duration { get; set; }
        public CourseType Type { get; set; }
        public decimal Cost { get; set; }
        public CourseStatus Status { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
    }

    public class TrainingRecord
    {
        public string Id { get; set; }
        public string CourseName { get; set; }
        public string Provider { get; set; }
        public DateTime CompletionDate { get; set; }
        public int Duration { get; set; }
        public decimal? Score { get; set; }
        public string CertificateNumber { get; set; }
        public List<SkillGained> SkillsGained { get; set; }
    }

    public class TrainingRecordRequest
    {
        public string CourseName { get; set; }
        public string Provider { get; set; }
        public DateTime CompletionDate { get; set; }
        public int Duration { get; set; }
        public decimal? Score { get; set; }
        public string CertificateNumber { get; set; }
        public List<SkillGained> SkillsGained { get; set; }
    }

    public class SkillGained
    {
        public string SkillId { get; set; }
        public SkillLevel NewLevel { get; set; }
    }

    public class ResourcePoolSummary
    {
        public string PoolId { get; set; }
        public string PoolName { get; set; }
        public int TotalMembers { get; set; }
        public int ActiveMembers { get; set; }
        public decimal TotalCapacityHours { get; set; }
        public decimal AllocatedHours { get; set; }
        public decimal UtilizationPercent { get; set; }
        public Dictionary<string, int> SkillsSummary { get; set; }
    }

    public class CapacityForecast
    {
        public string Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime GeneratedDate { get; set; }
        public decimal TotalCapacity { get; set; }
        public decimal TotalAllocated { get; set; }
        public decimal TotalAvailable { get; set; }
        public decimal AverageUtilization { get; set; }
        public List<MonthlyCapacity> MonthlyCapacity { get; set; }
    }

    public class CapacityForecastRequest
    {
        public string PoolId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class MonthlyCapacity
    {
        public DateTime Month { get; set; }
        public string MonthName { get; set; }
        public decimal TotalCapacity { get; set; }
        public decimal AllocatedHours { get; set; }
        public decimal AvailableHours { get; set; }
        public decimal UtilizationPercent { get; set; }
        public Dictionary<string, decimal> SkillCapacity { get; set; }
    }

    public class ResourceAlertEventArgs : EventArgs
    {
        public string MemberId { get; set; }
        public string AlertType { get; set; }
        public string Message { get; set; }
    }

    public enum MemberStatus { Active, OnLeave, Inactive, Terminated }
    public enum SkillLevel { None, Beginner, Intermediate, Advanced, Expert }
    public enum CertStatus { Active, Expired, Pending }
    public enum AssignmentStatus { Active, Completed, Cancelled }
    public enum TrainingPlanStatus { Draft, Active, Completed, Cancelled }
    public enum CourseType { Online, InPerson, Hybrid, SelfPaced }
    public enum CourseStatus { Recommended, Scheduled, InProgress, Completed, Cancelled }

    #endregion
}
