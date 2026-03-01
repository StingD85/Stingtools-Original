// ============================================================================
// StingBIM AI Tests - VDC Coordination Center Tests
// Unit tests for clash grouping, issue tracking, meetings, and coordination
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using StingBIM.AI.Intelligence.VDC;

namespace StingBIM.AI.Tests.Intelligence
{
    [TestFixture]
    public class VDCCoordinationCenterTests
    {
        private VDCCoordinationCenter _center;

        [SetUp]
        public void Setup()
        {
            _center = VDCCoordinationCenter.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            // Arrange & Act
            var instance1 = VDCCoordinationCenter.Instance;
            var instance2 = VDCCoordinationCenter.Instance;

            // Assert
            Assert.That(instance2, Is.SameAs(instance1));
        }

        #region Clash Import Tests

        [Test]
        public void ImportClashes_ShouldImportClashList()
        {
            // Arrange
            var clashes = new List<ClashData>
            {
                new ClashData
                {
                    ClashId = "CLASH-001",
                    Name = "Duct vs Beam",
                    Element1Id = "DUCT-100",
                    Element2Id = "BEAM-200",
                    Trade1 = "Mechanical",
                    Trade2 = "Structural",
                    Location = "Level 2, Grid B-3",
                    ClashPoint = new ClashPoint { X = 10.5, Y = 20.3, Z = 5.0 },
                    Distance = -0.05,
                    Severity = ClashSeverity.Hard
                },
                new ClashData
                {
                    ClashId = "CLASH-002",
                    Name = "Pipe vs Conduit",
                    Element1Id = "PIPE-100",
                    Element2Id = "CONDUIT-200",
                    Trade1 = "Plumbing",
                    Trade2 = "Electrical",
                    Location = "Level 2, Grid C-4",
                    ClashPoint = new ClashPoint { X = 15.0, Y = 25.0, Z = 5.0 },
                    Distance = 0.02,
                    Severity = ClashSeverity.Clearance
                }
            };

            // Act
            var result = _center.ImportClashes(clashes, "Navisworks", "Model v1.0");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ImportedCount, Is.EqualTo(2));
        }

        [Test]
        public void GetClashGroupingStrategies_ShouldReturnAllStrategies()
        {
            // Arrange & Act
            var strategies = _center.GetClashGroupingStrategies();

            // Assert
            Assert.That(strategies, Is.Not.Null);
            Assert.That(strategies.Count >= 7, Is.True);
            Assert.That(strategies.Exists(s => s.Strategy == ClashGroupingStrategy.ByLocation), Is.True);
            Assert.That(strategies.Exists(s => s.Strategy == ClashGroupingStrategy.ByTradePair), Is.True);
            Assert.That(strategies.Exists(s => s.Strategy == ClashGroupingStrategy.Smart), Is.True);
        }

        [Test]
        public void GroupClashes_ByLocation_ShouldGroupCorrectly()
        {
            // Arrange
            var clashes = new List<ClashData>
            {
                new ClashData { ClashId = "C1", Location = "Level 1, Grid A-1", Trade1 = "Mech", Trade2 = "Struct" },
                new ClashData { ClashId = "C2", Location = "Level 1, Grid A-1", Trade1 = "Elec", Trade2 = "Plumb" },
                new ClashData { ClashId = "C3", Location = "Level 2, Grid B-2", Trade1 = "Mech", Trade2 = "Elec" }
            };
            _center.ImportClashes(clashes, "Test", "v1.0");

            // Act
            var groups = _center.GroupClashes(ClashGroupingStrategy.ByLocation);

            // Assert
            Assert.That(groups, Is.Not.Null);
            Assert.That(groups.Count > 0, Is.True);
        }

        [Test]
        public void GroupClashes_ByTradePair_ShouldGroupCorrectly()
        {
            // Arrange
            var clashes = new List<ClashData>
            {
                new ClashData { ClashId = "T1", Trade1 = "Mechanical", Trade2 = "Structural", Location = "L1" },
                new ClashData { ClashId = "T2", Trade1 = "Mechanical", Trade2 = "Structural", Location = "L2" },
                new ClashData { ClashId = "T3", Trade1 = "Electrical", Trade2 = "Plumbing", Location = "L3" }
            };
            _center.ImportClashes(clashes, "Test", "v1.0");

            // Act
            var groups = _center.GroupClashes(ClashGroupingStrategy.ByTradePair);

            // Assert
            Assert.That(groups, Is.Not.Null);
        }

        #endregion

        #region Coordination Issue Tests

        [Test]
        public void CreateCoordinationIssue_ShouldCreateIssue()
        {
            // Arrange
            var request = new CoordinationIssueRequest
            {
                Title = "HVAC Duct Routing Conflict",
                Description = "Main supply duct conflicts with structural beam at Grid B-3",
                Category = IssueCategory.Clash,
                Priority = IssuePriority.High,
                Location = "Level 2, Grid B-3",
                AffectedTrades = new List<string> { "Mechanical", "Structural" },
                ClashIds = new List<string> { "CLASH-001" },
                CreatedBy = "John Smith"
            };

            // Act
            var issue = _center.CreateCoordinationIssue(request);

            // Assert
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.IssueId, Is.Not.Null);
            Assert.That(issue.Title, Is.EqualTo("HVAC Duct Routing Conflict"));
            Assert.That(issue.Category, Is.EqualTo(IssueCategory.Clash));
            Assert.That(issue.Status, Is.EqualTo(IssueStatus.Open));
        }

        [Test]
        public void AssignIssue_ShouldUpdateAssignment()
        {
            // Arrange
            var issue = _center.CreateCoordinationIssue(new CoordinationIssueRequest
            {
                Title = "Assignment Test Issue",
                Description = "Test",
                Category = IssueCategory.Design,
                CreatedBy = "Creator"
            });

            // Act
            _center.AssignIssue(issue.IssueId, "Jane Doe", "HVAC Contractor");

            // Assert
            Assert.That(issue.AssignedTo, Is.EqualTo("Jane Doe"));
            Assert.That(issue.AssignedCompany, Is.EqualTo("HVAC Contractor"));
            Assert.That(issue.Status, Is.EqualTo(IssueStatus.Assigned));
        }

        [Test]
        public void UpdateIssueStatus_ShouldUpdateAndAddComment()
        {
            // Arrange
            var issue = _center.CreateCoordinationIssue(new CoordinationIssueRequest
            {
                Title = "Status Update Test",
                Description = "Test",
                Category = IssueCategory.RFI,
                CreatedBy = "Creator"
            });

            // Act
            _center.UpdateIssueStatus(issue.IssueId, IssueStatus.InProgress, "Mike", "Started working on this");

            // Assert
            Assert.That(issue.Status, Is.EqualTo(IssueStatus.InProgress));
            Assert.That(issue.Comments.Count > 0, Is.True);
            Assert.That(issue.Comments[0].Author, Is.EqualTo("Mike"));
        }

        [Test]
        public void ResolveIssue_ShouldCloseIssue()
        {
            // Arrange
            var issue = _center.CreateCoordinationIssue(new CoordinationIssueRequest
            {
                Title = "Resolution Test",
                Description = "Test",
                Category = IssueCategory.Clash,
                CreatedBy = "Creator"
            });

            // Act
            _center.ResolveIssue(issue.IssueId, "Resolver", "Rerouted duct above beam");

            // Assert
            Assert.That(issue.Status, Is.EqualTo(IssueStatus.Resolved));
            Assert.That(issue.Resolution, Is.EqualTo("Rerouted duct above beam"));
            Assert.That(issue.ResolvedDate, Is.Not.Null);
        }

        #endregion

        #region Meeting Tests

        [Test]
        public void ScheduleMeeting_ShouldCreateMeeting()
        {
            // Arrange
            var request = new MeetingRequest
            {
                Title = "Weekly Coordination Meeting",
                Type = MeetingType.Coordination,
                ScheduledDate = DateTime.UtcNow.AddDays(1),
                Duration = TimeSpan.FromHours(2),
                Location = "Conference Room A",
                Attendees = new List<string> { "John", "Jane", "Mike" },
                IssueIds = new List<string>(),
                CreatedBy = "Coordinator"
            };

            // Act
            var meeting = _center.ScheduleMeeting(request);

            // Assert
            Assert.That(meeting, Is.Not.Null);
            Assert.That(meeting.MeetingId, Is.Not.Null);
            Assert.That(meeting.Title, Is.EqualTo("Weekly Coordination Meeting"));
            Assert.That(meeting.Type, Is.EqualTo(MeetingType.Coordination));
            Assert.That(meeting.Status, Is.EqualTo(MeetingStatus.Scheduled));
        }

        [Test]
        public void GenerateMeetingAgenda_ShouldCreateAgenda()
        {
            // Arrange
            var meeting = _center.ScheduleMeeting(new MeetingRequest
            {
                Title = "Agenda Test Meeting",
                Type = MeetingType.Coordination,
                ScheduledDate = DateTime.UtcNow.AddDays(1),
                Duration = TimeSpan.FromHours(1),
                CreatedBy = "Test"
            });

            // Act
            var agenda = _center.GenerateMeetingAgenda(meeting.MeetingId);

            // Assert
            Assert.That(agenda, Is.Not.Null);
            Assert.That(agenda.Length > 0, Is.True);
            Assert.That(agenda.Contains("MEETING AGENDA"), Is.True);
        }

        [Test]
        public void RecordMeetingMinutes_ShouldSaveMinutes()
        {
            // Arrange
            var meeting = _center.ScheduleMeeting(new MeetingRequest
            {
                Title = "Minutes Test Meeting",
                Type = MeetingType.Design,
                ScheduledDate = DateTime.UtcNow.AddDays(1),
                Duration = TimeSpan.FromHours(1),
                CreatedBy = "Test"
            });

            var minutes = new MeetingMinutes
            {
                Summary = "Discussed clash resolution strategies",
                Decisions = new List<string> { "Mechanical team to reroute duct" },
                ActionItems = new List<ActionItem>
                {
                    new ActionItem
                    {
                        Description = "Update duct routing",
                        AssignedTo = "Mech Team",
                        DueDate = DateTime.UtcNow.AddDays(7)
                    }
                },
                RecordedBy = "Note Taker"
            };

            // Act
            _center.RecordMeetingMinutes(meeting.MeetingId, minutes);

            // Assert
            Assert.That(meeting.Status, Is.EqualTo(MeetingStatus.Completed));
            Assert.That(meeting.Minutes, Is.Not.Null);
            Assert.That(meeting.Minutes.Summary, Is.EqualTo("Discussed clash resolution strategies"));
        }

        #endregion

        #region Action Item Tests

        [Test]
        public void CreateActionItem_ShouldCreateItem()
        {
            // Arrange
            var request = new ActionItemRequest
            {
                Description = "Review structural drawings",
                AssignedTo = "Structural Engineer",
                AssignedCompany = "Structure Co.",
                DueDate = DateTime.UtcNow.AddDays(5),
                Priority = ActionPriority.High,
                CreatedBy = "Project Manager"
            };

            // Act
            var action = _center.CreateActionItem(request);

            // Assert
            Assert.That(action, Is.Not.Null);
            Assert.That(action.ActionId, Is.Not.Null);
            Assert.That(action.Description, Is.EqualTo("Review structural drawings"));
            Assert.That(action.Status, Is.EqualTo(ActionStatus.Open));
        }

        [Test]
        public void CompleteActionItem_ShouldUpdateStatus()
        {
            // Arrange
            var action = _center.CreateActionItem(new ActionItemRequest
            {
                Description = "Complete test action",
                AssignedTo = "Tester",
                DueDate = DateTime.UtcNow.AddDays(3),
                CreatedBy = "Creator"
            });

            // Act
            _center.CompleteActionItem(action.ActionId, "Tester", "Completed review");

            // Assert
            Assert.That(action.Status, Is.EqualTo(ActionStatus.Completed));
            Assert.That(action.CompletedDate, Is.Not.Null);
        }

        [Test]
        public void GetOverdueActionItems_ShouldReturnOverdueItems()
        {
            // Arrange
            _center.CreateActionItem(new ActionItemRequest
            {
                Description = "Overdue test action",
                AssignedTo = "Tester",
                DueDate = DateTime.UtcNow.AddDays(-5), // Past due
                CreatedBy = "Creator"
            });

            // Act
            var overdueItems = _center.GetOverdueActionItems();

            // Assert
            Assert.That(overdueItems, Is.Not.Null);
            Assert.That(overdueItems.Count > 0, Is.True);
        }

        #endregion

        #region Dashboard Tests

        [Test]
        public void GetCoordinationDashboard_ShouldReturnDashboardData()
        {
            // Arrange - Create some issues
            _center.CreateCoordinationIssue(new CoordinationIssueRequest
            {
                Title = "Dashboard Test Issue",
                Description = "Test",
                Category = IssueCategory.Clash,
                Priority = IssuePriority.High,
                CreatedBy = "Test"
            });

            // Act
            var dashboard = _center.GetCoordinationDashboard();

            // Assert
            Assert.That(dashboard, Is.Not.Null);
            Assert.That(dashboard.TotalClashes >= 0, Is.True);
            Assert.That(dashboard.IssuesByStatus, Is.Not.Null);
            Assert.That(dashboard.IssuesByCategory, Is.Not.Null);
        }

        [Test]
        public void GetWeeklyCoordinationReport_ShouldReturnReport()
        {
            // Act
            var report = _center.GetWeeklyCoordinationReport();

            // Assert
            Assert.That(report, Is.Not.Null);
            Assert.That(report.Length > 0, Is.True);
            Assert.That(report.Contains("VDC COORDINATION WEEKLY REPORT"), Is.True);
        }

        #endregion
    }
}
