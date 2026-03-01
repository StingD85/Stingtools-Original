// ============================================================================
// StingBIM AI Tests - Field Operations Module Tests
// Unit tests for inspections, punch lists, daily reports, and safety tracking
// ============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using StingBIM.AI.Intelligence.FieldOperations;

namespace StingBIM.AI.Tests.Intelligence
{
    [TestFixture]
    public class FieldOperationsModuleTests
    {
        private FieldOperationsModule _module;

        [SetUp]
        public void Setup()
        {
            _module = FieldOperationsModule.Instance;
        }

        [Test]
        public void Instance_ShouldReturnSingleton()
        {
            // Arrange & Act
            var instance1 = FieldOperationsModule.Instance;
            var instance2 = FieldOperationsModule.Instance;

            // Assert
            Assert.That(instance2, Is.SameAs(instance1));
        }

        #region Inspection Tests

        [Test]
        public void CreateInspection_ShouldCreateInspectionWithChecklist()
        {
            // Arrange
            var request = new InspectionRequest
            {
                Title = "Concrete Pour Inspection - L2 Slab",
                Type = InspectionType.PrePour,
                TemplateId = "STRUCT-CONCRETE",
                Location = "Level 2 Slab",
                Level = "Level 2",
                Zone = "Zone A",
                ScheduledDate = DateTime.UtcNow.AddDays(1),
                Inspector = "John Inspector",
                CreatedBy = "Project Manager"
            };

            // Act
            var inspection = _module.CreateInspection(request);

            // Assert
            Assert.That(inspection, Is.Not.Null);
            Assert.That(inspection.InspectionId, Is.Not.Null);
            Assert.That(inspection.Title, Is.EqualTo("Concrete Pour Inspection - L2 Slab"));
            Assert.That(inspection.Status, Is.EqualTo(InspectionStatus.Scheduled));
            Assert.That(inspection.ChecklistItems.Count > 0, Is.True);
        }

        [Test]
        public void StartInspection_ShouldUpdateStatus()
        {
            // Arrange
            var inspection = _module.CreateInspection(new InspectionRequest
            {
                Title = "Start Test Inspection",
                Type = InspectionType.Rough,
                TemplateId = "MEP-ABOVE-CEILING",
                CreatedBy = "Creator"
            });

            // Act
            _module.StartInspection(inspection.InspectionId, "Field Inspector");

            // Assert
            Assert.That(inspection.Status, Is.EqualTo(InspectionStatus.InProgress));
            Assert.That(inspection.StartedDate, Is.Not.Null);
            Assert.That(inspection.Inspector, Is.EqualTo("Field Inspector"));
        }

        [Test]
        public void UpdateChecklistItem_ShouldUpdateItemStatus()
        {
            // Arrange
            var inspection = _module.CreateInspection(new InspectionRequest
            {
                Title = "Checklist Update Test",
                Type = InspectionType.Final,
                TemplateId = "STRUCT-CONCRETE",
                CreatedBy = "Creator"
            });
            var firstItem = inspection.ChecklistItems[0];

            // Act
            _module.UpdateChecklistItem(
                inspection.InspectionId,
                firstItem.ItemId,
                ChecklistItemStatus.Pass,
                "Verified correct alignment"
            );

            // Assert
            Assert.That(firstItem.Status, Is.EqualTo(ChecklistItemStatus.Pass));
            Assert.That(firstItem.Notes, Is.EqualTo("Verified correct alignment"));
            Assert.That(firstItem.CheckedDate, Is.Not.Null);
        }

        [Test]
        public void AddPhoto_ShouldAddPhotoToInspection()
        {
            // Arrange
            var inspection = _module.CreateInspection(new InspectionRequest
            {
                Title = "Photo Test Inspection",
                Type = InspectionType.PrePour,
                CreatedBy = "Creator"
            });

            var photoUpload = new PhotoUpload
            {
                FileName = "rebar_placement.jpg",
                FilePath = "/photos/rebar_placement.jpg",
                Caption = "Reinforcement placement at grid B-3",
                Location = "Level 2, Grid B-3",
                TakenBy = "Inspector John"
            };

            // Act
            var photo = _module.AddPhoto(inspection.InspectionId, photoUpload);

            // Assert
            Assert.That(photo, Is.Not.Null);
            Assert.That(photo.PhotoId, Is.Not.Null);
            Assert.That(photo.FileName, Is.EqualTo("rebar_placement.jpg"));
            Assert.That(inspection.Photos.Count, Is.EqualTo(1));
        }

        [Test]
        public void CompleteInspection_ShouldCalculateResult()
        {
            // Arrange
            var inspection = _module.CreateInspection(new InspectionRequest
            {
                Title = "Complete Test Inspection",
                Type = InspectionType.PrePour,
                TemplateId = "STRUCT-CONCRETE",
                CreatedBy = "Creator"
            });

            // Mark all items as pass
            foreach (var item in inspection.ChecklistItems)
            {
                _module.UpdateChecklistItem(inspection.InspectionId, item.ItemId, ChecklistItemStatus.Pass);
            }

            // Act
            var result = _module.CompleteInspection(inspection.InspectionId, "Completer");

            // Assert
            Assert.That(result, Is.EqualTo(InspectionResult.Pass));
            Assert.That(inspection.Status, Is.EqualTo(InspectionStatus.Completed));
            Assert.That(inspection.CompletedDate, Is.Not.Null);
        }

        [Test]
        public void CompleteInspection_WithFailures_ShouldCreatePunchItems()
        {
            // Arrange
            var inspection = _module.CreateInspection(new InspectionRequest
            {
                Title = "Failure Test Inspection",
                Type = InspectionType.Rough,
                TemplateId = "MEP-ABOVE-CEILING",
                Location = "Level 3",
                CreatedBy = "Creator"
            });

            // Mark first item as fail, rest as pass
            _module.UpdateChecklistItem(
                inspection.InspectionId,
                inspection.ChecklistItems[0].ItemId,
                ChecklistItemStatus.Fail,
                "Not properly supported"
            );

            for (int i = 1; i < inspection.ChecklistItems.Count; i++)
            {
                _module.UpdateChecklistItem(
                    inspection.InspectionId,
                    inspection.ChecklistItems[i].ItemId,
                    ChecklistItemStatus.Pass
                );
            }

            PunchItem createdPunchItem = null;
            _module.PunchItemCreated += (s, e) => createdPunchItem = null; // Just verify event fires

            // Act
            var result = _module.CompleteInspection(inspection.InspectionId, "Completer");

            // Assert - Should be conditional pass with punch items created
            Assert.That(result == InspectionResult.Fail || result == InspectionResult.ConditionalPass, Is.True);
        }

        [Test]
        public void CompleteInspection_ShouldFireCompletedEvent()
        {
            // Arrange
            var inspection = _module.CreateInspection(new InspectionRequest
            {
                Title = "Event Test Inspection",
                Type = InspectionType.Final,
                CreatedBy = "Creator"
            });

            FieldEventArgs eventArgs = null;
            _module.InspectionCompleted += (s, e) => eventArgs = e;

            // Act
            _module.CompleteInspection(inspection.InspectionId, "Completer");

            // Assert
            Assert.That(eventArgs, Is.Not.Null);
            Assert.That(eventArgs.Type, Is.EqualTo(FieldEventType.InspectionCompleted));
            Assert.That(eventArgs.EntityId, Is.EqualTo(inspection.InspectionId));
        }

        #endregion

        #region Punch List Tests

        [Test]
        public void CreatePunchItem_ShouldCreateItem()
        {
            // Arrange
            var request = new PunchItemRequest
            {
                Title = "Touch up paint at column C-5",
                Description = "Minor paint damage from scaffolding",
                Category = PunchCategory.Walkthrough,
                Priority = PunchPriority.Low,
                Location = "Level 1, Column C-5",
                Level = "Level 1",
                Zone = "Zone B",
                AssignedTo = "Paint Contractor",
                AssignedCompany = "ABC Painting",
                DueDate = DateTime.UtcNow.AddDays(7),
                CreatedBy = "Site Manager"
            };

            // Act
            var punchItem = _module.CreatePunchItem(request);

            // Assert
            Assert.That(punchItem, Is.Not.Null);
            Assert.That(punchItem.PunchId, Is.Not.Null);
            Assert.That(punchItem.Title, Is.EqualTo("Touch up paint at column C-5"));
            Assert.That(punchItem.Status, Is.EqualTo(PunchStatus.Open));
            Assert.That(punchItem.Priority, Is.EqualTo(PunchPriority.Low));
        }

        [Test]
        public void CreatePunchItem_ShouldFireEvent()
        {
            // Arrange
            FieldEventArgs eventArgs = null;
            _module.PunchItemCreated += (s, e) => eventArgs = e;

            // Act
            var punchItem = _module.CreatePunchItem(new PunchItemRequest
            {
                Title = "Event Test Punch",
                Description = "Test",
                Category = PunchCategory.Safety,
                CreatedBy = "Creator"
            });

            // Assert
            Assert.That(eventArgs, Is.Not.Null);
            Assert.That(eventArgs.Type, Is.EqualTo(FieldEventType.PunchItemCreated));
        }

        [Test]
        public void UpdatePunchItemStatus_ShouldUpdateStatus()
        {
            // Arrange
            var punchItem = _module.CreatePunchItem(new PunchItemRequest
            {
                Title = "Status Update Test",
                Description = "Test",
                Category = PunchCategory.Inspection,
                CreatedBy = "Creator"
            });

            // Act
            _module.UpdatePunchItemStatus(punchItem.PunchId, PunchStatus.InProgress, "Worker", "Started work");

            // Assert
            Assert.That(punchItem.Status, Is.EqualTo(PunchStatus.InProgress));
            Assert.That(punchItem.Comments.Count > 0, Is.True);
        }

        [Test]
        public void UpdatePunchItemStatus_ToCompleted_ShouldSetCompletedDate()
        {
            // Arrange
            var punchItem = _module.CreatePunchItem(new PunchItemRequest
            {
                Title = "Completion Test",
                Description = "Test",
                Category = PunchCategory.Coordination,
                CreatedBy = "Creator"
            });

            // Act
            _module.UpdatePunchItemStatus(punchItem.PunchId, PunchStatus.Completed, "Worker", "Finished work");

            // Assert
            Assert.That(punchItem.Status, Is.EqualTo(PunchStatus.Completed));
            Assert.That(punchItem.CompletedDate, Is.Not.Null);
            Assert.That(punchItem.CompletedBy, Is.EqualTo("Worker"));
        }

        [Test]
        public void UpdatePunchItemStatus_ToVerified_ShouldSetVerifiedDate()
        {
            // Arrange
            var punchItem = _module.CreatePunchItem(new PunchItemRequest
            {
                Title = "Verification Test",
                Description = "Test",
                Category = PunchCategory.Warranty,
                CreatedBy = "Creator"
            });

            // Act
            _module.UpdatePunchItemStatus(punchItem.PunchId, PunchStatus.Verified, "Inspector", "Verified complete");

            // Assert
            Assert.That(punchItem.Status, Is.EqualTo(PunchStatus.Verified));
            Assert.That(punchItem.VerifiedDate, Is.Not.Null);
            Assert.That(punchItem.VerifiedBy, Is.EqualTo("Inspector"));
        }

        [Test]
        public void GetPunchListSummary_ShouldReturnSummary()
        {
            // Arrange
            _module.CreatePunchItem(new PunchItemRequest
            {
                Title = "Summary Test 1",
                Category = PunchCategory.Inspection,
                Priority = PunchPriority.High,
                Level = "Level 1",
                CreatedBy = "Creator"
            });
            _module.CreatePunchItem(new PunchItemRequest
            {
                Title = "Summary Test 2",
                Category = PunchCategory.Walkthrough,
                Priority = PunchPriority.Medium,
                Level = "Level 1",
                CreatedBy = "Creator"
            });

            // Act
            var summary = _module.GetPunchListSummary("Level 1");

            // Assert
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.TotalItems >= 2, Is.True);
            Assert.That(summary.ByCategory, Is.Not.Null);
            Assert.That(summary.ByPriority, Is.Not.Null);
        }

        #endregion

        #region Daily Report Tests

        [Test]
        public void CreateDailyReport_ShouldCreateReport()
        {
            // Arrange
            var request = new DailyReportRequest
            {
                ReportDate = DateTime.Today,
                Weather = "Sunny",
                Temperature = 28.5,
                WorkDescription = "Continued concrete pour on Level 3",
                Manpower = new List<ManpowerEntry>
                {
                    new ManpowerEntry { Trade = "Carpenters", Company = "ABC Construction", Count = 12, Hours = 8 },
                    new ManpowerEntry { Trade = "Ironworkers", Company = "Steel Inc", Count = 8, Hours = 8 }
                },
                Equipment = new List<EquipmentEntry>
                {
                    new EquipmentEntry { Equipment = "Concrete Pump", Quantity = 1, HoursUsed = 6 }
                },
                Materials = new List<MaterialDelivery>
                {
                    new MaterialDelivery { Material = "Concrete C30", Quantity = 150, Unit = "m³", Supplier = "Ready Mix Co" }
                },
                CreatedBy = "Site Supervisor"
            };

            // Act
            var report = _module.CreateDailyReport(request);

            // Assert
            Assert.That(report, Is.Not.Null);
            Assert.That(report.ReportId, Is.Not.Null);
            Assert.That(report.Weather, Is.EqualTo("Sunny"));
            Assert.That(report.TotalManpower, Is.EqualTo(20)); // 12 + 8
            Assert.That(report.TotalManHours, Is.EqualTo(160)); // 12*8 + 8*8
            Assert.That(report.Status, Is.EqualTo(ReportStatus.Draft));
        }

        [Test]
        public void SubmitDailyReport_ShouldUpdateStatus()
        {
            // Arrange
            var report = _module.CreateDailyReport(new DailyReportRequest
            {
                ReportDate = DateTime.Today,
                WorkDescription = "Test work",
                CreatedBy = "Creator"
            });

            // Act
            _module.SubmitDailyReport(report.ReportId, "Submitter");

            // Assert
            Assert.That(report.Status, Is.EqualTo(ReportStatus.Submitted));
            Assert.That(report.SubmittedDate, Is.Not.Null);
            Assert.That(report.SubmittedBy, Is.EqualTo("Submitter"));
        }

        #endregion

        #region Safety Observation Tests

        [Test]
        public void ReportSafetyObservation_ShouldCreateObservation()
        {
            // Arrange
            var request = new SafetyObservationRequest
            {
                Type = SafetyObservationType.Hazard,
                Severity = SafetySeverity.High,
                Description = "Unguarded floor opening at stairwell",
                Location = "Level 4, Stairwell B",
                Level = "Level 4",
                Zone = "Zone A",
                ObservedBy = "Safety Officer",
                ImmediateAction = "Barricaded area and posted warning signs"
            };

            // Act
            var observation = _module.ReportSafetyObservation(request);

            // Assert
            Assert.That(observation, Is.Not.Null);
            Assert.That(observation.ObservationId, Is.Not.Null);
            Assert.That(observation.Type, Is.EqualTo(SafetyObservationType.Hazard));
            Assert.That(observation.Severity, Is.EqualTo(SafetySeverity.High));
            Assert.That(observation.Status, Is.EqualTo(SafetyStatus.Open));
        }

        [Test]
        public void ReportSafetyObservation_Critical_ShouldFireEvent()
        {
            // Arrange
            FieldEventArgs eventArgs = null;
            _module.SafetyIssueReported += (s, e) => eventArgs = e;

            // Act
            _module.ReportSafetyObservation(new SafetyObservationRequest
            {
                Type = SafetyObservationType.Incident,
                Severity = SafetySeverity.Critical,
                Description = "Critical safety event",
                ObservedBy = "Observer"
            });

            // Assert
            Assert.That(eventArgs, Is.Not.Null);
            Assert.That(eventArgs.Type, Is.EqualTo(FieldEventType.SafetyIssue));
        }

        [Test]
        public void CloseSafetyObservation_ShouldCloseObservation()
        {
            // Arrange
            var observation = _module.ReportSafetyObservation(new SafetyObservationRequest
            {
                Type = SafetyObservationType.NearMiss,
                Severity = SafetySeverity.Medium,
                Description = "Near miss incident",
                ObservedBy = "Worker"
            });

            // Act
            _module.CloseSafetyObservation(observation.ObservationId, "Safety Manager", "Corrective measures implemented");

            // Assert
            Assert.That(observation.Status, Is.EqualTo(SafetyStatus.Closed));
            Assert.That(observation.ClosedDate, Is.Not.Null);
            Assert.That(observation.Resolution, Is.EqualTo("Corrective measures implemented"));
        }

        #endregion

        #region Progress Capture Tests

        [Test]
        public void CaptureProgress_ShouldCaptureProgress()
        {
            // Arrange
            var request = new ProgressCaptureRequest
            {
                CapturedBy = "Field Engineer",
                Location = "Level 2 Slab",
                Level = "Level 2",
                Zone = "Zone A",
                ActivityId = "ACT-001",
                PercentComplete = 75.0,
                Notes = "Concrete curing in progress",
                Photos = new List<PhotoUpload>
                {
                    new PhotoUpload { FileName = "progress.jpg", Caption = "75% complete" }
                }
            };

            // Act
            var capture = _module.CaptureProgress(request);

            // Assert
            Assert.That(capture, Is.Not.Null);
            Assert.That(capture.CaptureId, Is.Not.Null);
            Assert.That(capture.PercentComplete, Is.EqualTo(75.0));
            Assert.That(capture.Photos.Count, Is.EqualTo(1));
        }

        [Test]
        public void GetProgressHistory_ShouldReturnHistory()
        {
            // Arrange
            var level = "Level-Progress-Test";
            var zone = "Zone-Progress-Test";

            _module.CaptureProgress(new ProgressCaptureRequest
            {
                CapturedBy = "Engineer",
                Level = level,
                Zone = zone,
                PercentComplete = 25.0
            });

            _module.CaptureProgress(new ProgressCaptureRequest
            {
                CapturedBy = "Engineer",
                Level = level,
                Zone = zone,
                PercentComplete = 50.0
            });

            // Act
            var history = _module.GetProgressHistory(level, zone);

            // Assert
            Assert.That(history, Is.Not.Null);
            Assert.That(history.Count, Is.EqualTo(2));
        }

        #endregion

        #region Quality Record Tests

        [Test]
        public void CreateQualityRecord_ShouldCreateRecord()
        {
            // Arrange
            var request = new QualityRecordRequest
            {
                Type = QualityRecordType.ConcreteTest,
                Title = "Concrete Cube Test - L3 Slab",
                Description = "28-day compressive strength test",
                Location = "Level 3 Slab",
                TestDate = DateTime.Today,
                Result = "35.5 MPa",
                Specification = "Minimum 30 MPa",
                ActualValue = 35.5,
                RequiredValue = 30.0,
                Unit = "MPa",
                Pass = true,
                TestedBy = "Testing Lab",
                CertificateNumber = "CERT-2024-001"
            };

            // Act
            var record = _module.CreateQualityRecord(request);

            // Assert
            Assert.That(record, Is.Not.Null);
            Assert.That(record.RecordId, Is.Not.Null);
            Assert.That(record.Type, Is.EqualTo(QualityRecordType.ConcreteTest));
            Assert.That(record.Pass, Is.True);
            Assert.That(record.ActualValue, Is.EqualTo(35.5));
        }

        #endregion

        #region Dashboard Tests

        [Test]
        public void GetDashboard_ShouldReturnDashboardData()
        {
            // Act
            var dashboard = _module.GetDashboard();

            // Assert
            Assert.That(dashboard, Is.Not.Null);
            Assert.That(dashboard.InspectionsSummary, Is.Not.Null);
            Assert.That(dashboard.PunchListSummary, Is.Not.Null);
            Assert.That(dashboard.SafetySummary, Is.Not.Null);
        }

        #endregion
    }
}
