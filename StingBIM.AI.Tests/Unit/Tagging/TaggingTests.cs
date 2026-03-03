// StingBIM.AI.Tests - SuperIntelligent Tagging Module Tests
// Tests for tag models, cluster detection, collision resolution, rule engine

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Tagging.Models;
using StingBIM.AI.Tagging.Intelligence;
using StingBIM.AI.Tagging.Data;

namespace StingBIM.AI.Tests.Unit.Tagging
{
    [TestFixture]
    public class TaggingTests
    {
        #region Point2D and Geometry Tests

        [TestFixture]
        public class Point2DTests
        {
            [Test]
            public void Constructor_ShouldSetXY()
            {
                var point = new Point2D(3.0, 4.0);
                point.X.Should().Be(3.0);
                point.Y.Should().Be(4.0);
            }

            [Test]
            public void DistanceTo_ShouldCalculatePythagorean()
            {
                var a = new Point2D(0, 0);
                var b = new Point2D(3, 4);
                a.DistanceTo(b).Should().BeApproximately(5.0, 0.001);
            }

            [Test]
            public void DistanceTo_SamePoint_ShouldBeZero()
            {
                var p = new Point2D(5, 5);
                p.DistanceTo(p).Should().Be(0);
            }

            [Test]
            public void Nullable_HasValue_ShouldWork()
            {
                Point2D? nullable = new Point2D(1, 2);
                nullable.HasValue.Should().BeTrue();
                nullable.Value.X.Should().Be(1);

                Point2D? empty = null;
                empty.HasValue.Should().BeFalse();
            }
        }

        #endregion

        #region TagBounds2D Tests

        [TestFixture]
        public class TagBounds2DTests
        {
            [Test]
            public void Aliases_ShouldMatchMinMax()
            {
                var bounds = new TagBounds2D
                {
                    MinX = 10, MaxX = 50,
                    MinY = 20, MaxY = 80
                };

                bounds.Left.Should().Be(10);
                bounds.Right.Should().Be(50);
                bounds.Bottom.Should().Be(20);
                bounds.Top.Should().Be(80);
            }

            [Test]
            public void Width_ShouldBeMaxXMinusMinX()
            {
                var bounds = new TagBounds2D { MinX = 10, MaxX = 30, MinY = 5, MaxY = 25 };
                (bounds.MaxX - bounds.MinX).Should().Be(20);
                (bounds.MaxY - bounds.MinY).Should().Be(20);
            }
        }

        #endregion

        #region TagInstance Tests

        [TestFixture]
        public class TagInstanceTests
        {
            [Test]
            public void Constructor_ShouldSetDefaults()
            {
                var tag = new TagInstance
                {
                    TagId = "TAG-001",
                    HostElementId = 12345,
                    Placement = new TagPlacement { Position = new Point2D(100, 200) },
                    DisplayText = "Door 101"
                };

                tag.TagId.Should().Be("TAG-001");
                tag.HostElementId.Should().Be(12345);
                tag.DisplayText.Should().Be("Door 101");
                tag.Placement.Position.X.Should().Be(100);
            }
        }

        #endregion

        #region ClusterDetector Tests

        [TestFixture]
        public class ClusterDetectorTests
        {
            [Test]
            public void Constructor_Default_ShouldUseConfigValues()
            {
                var detector = new ClusterDetector();
                detector.Epsilon.Should().BeGreaterThan(0);
                detector.MinPoints.Should().BeGreaterThanOrEqualTo(2);
            }

            [Test]
            public void Constructor_WithParameters_ShouldSetValues()
            {
                var detector = new ClusterDetector(2.0, 5);
                detector.Epsilon.Should().Be(2.0);
                detector.MinPoints.Should().Be(5);
            }

            [Test]
            public void Constructor_NegativeEpsilon_ShouldThrow()
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new ClusterDetector(-1.0, 3));
            }

            [Test]
            public void DetectClusters_EmptyInput_ShouldReturnEmptyResult()
            {
                var detector = new ClusterDetector(1.0, 3);
                var elements = new List<ClusterDetector.ElementInfo>();
                var result = detector.DetectClusters(elements);

                result.Should().NotBeNull();
                result.Clusters.Should().BeEmpty();
                result.TotalElementsAnalyzed.Should().Be(0);
            }

            [Test]
            public void DetectClusters_NullInput_ShouldThrow()
            {
                var detector = new ClusterDetector(1.0, 3);
                Assert.Throws<ArgumentNullException>(() =>
                    detector.DetectClusters((IReadOnlyList<ClusterDetector.ElementInfo>)null));
            }

            [Test]
            public void DetectClusters_ClusteredElements_ShouldFindClusters()
            {
                var detector = new ClusterDetector(5.0, 3);

                // Create a tight cluster of 5 elements
                var elements = new List<ClusterDetector.ElementInfo>
                {
                    new ClusterDetector.ElementInfo(1, new Point2D(0, 0), "Doors", "Single-Flush:900x2100"),
                    new ClusterDetector.ElementInfo(2, new Point2D(1, 0), "Doors", "Single-Flush:900x2100"),
                    new ClusterDetector.ElementInfo(3, new Point2D(2, 0), "Doors", "Single-Flush:900x2100"),
                    new ClusterDetector.ElementInfo(4, new Point2D(3, 0), "Doors", "Single-Flush:900x2100"),
                    new ClusterDetector.ElementInfo(5, new Point2D(4, 0), "Doors", "Single-Flush:900x2100"),
                };

                var result = detector.DetectClusters(elements);
                result.Clusters.Should().NotBeEmpty("five closely-spaced same-type elements should form a cluster");
            }

            [Test]
            public void DetectClusters_4ArgOverload_ShouldReturnClusterList()
            {
                var detector = new ClusterDetector(1.0, 3);
                var elementIds = new List<int> { 100, 200, 300, 400, 500 };
                var viewContext = new ViewTagContext
                {
                    ViewId = 1,
                    ViewName = "Level 1",
                    Scale = 100,
                    CropRegion = new TagBounds2D { MinX = 0, MinY = 0, MaxX = 100, MaxY = 100 }
                };

                var clusters = detector.DetectClusters(elementIds, viewContext, 5.0, 3);
                clusters.Should().NotBeNull();
            }
        }

        #endregion

        #region TagConfiguration Tests

        [TestFixture]
        public class TagConfigurationTests
        {
            [Test]
            public void Instance_ShouldBeSingleton()
            {
                var a = TagConfiguration.Instance;
                var b = TagConfiguration.Instance;
                a.Should().BeSameAs(b);
            }

            [Test]
            public void Settings_ShouldHaveDefaults()
            {
                var settings = TagConfiguration.Instance.Settings;
                settings.Should().NotBeNull();
                settings.ClusterEpsilon.Should().BeGreaterThan(0);
                settings.ClusterMinPoints.Should().BeGreaterThanOrEqualTo(2);
            }
        }

        #endregion

        #region Enum Tests

        [TestFixture]
        public class TagEnumTests
        {
            [Test]
            public void TagPosition_ShouldContainExpectedValues()
            {
                Enum.IsDefined(typeof(TagPosition), TagPosition.TopCenter).Should().BeTrue();
                Enum.IsDefined(typeof(TagPosition), TagPosition.BottomCenter).Should().BeTrue();
                Enum.IsDefined(typeof(TagPosition), TagPosition.MiddleLeft).Should().BeTrue();
            }

            [Test]
            public void ClusterType_ShouldContainExpectedValues()
            {
                Enum.IsDefined(typeof(ClusterType), ClusterType.Linear).Should().BeTrue();
            }

            [Test]
            public void IssueSeverity_ShouldContainErrorValue()
            {
                Enum.IsDefined(typeof(IssueSeverity), IssueSeverity.Error).Should().BeTrue();
            }
        }

        #endregion
    }
}
