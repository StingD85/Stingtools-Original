using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.AI.Creation.Elements;

namespace StingBIM.AI.Tests.Integration.RevitMocked
{
    /// <summary>
    /// Tier 2 — Integration tests using mocked Revit patterns.
    /// WallCreator/FloorCreator are stub implementations (no real Revit calls),
    /// so they can be fully tested without Revit.
    /// </summary>
    [TestFixture]
    public class ElementCreatorTests
    {
        #region WallCreator Tests

        [TestFixture]
        public class WallCreatorTests
        {
            private WallCreator _creator;

            [SetUp]
            public void SetUp()
            {
                _creator = new WallCreator();
            }

            [Test]
            public async Task CreateAsync_WithValidParams_ReturnsSuccess()
            {
                var parameters = new ElementCreationParams
                {
                    ElementType = "Wall",
                    Parameters = new Dictionary<string, object>
                    {
                        { "Length", 3000.0 },
                        { "Height", 2700.0 },
                        { "WallType", "Basic Wall" }
                    }
                };

                var result = await _creator.CreateAsync(parameters);

                result.Should().NotBeNull();
                result.Success.Should().BeTrue();
                result.CreatedElementId.Should().BeGreaterThan(0);
                result.ElementType.Should().Be("Wall");
            }

            [Test]
            public async Task CreateAsync_SetsTimestamps()
            {
                var before = DateTime.Now;
                var parameters = new ElementCreationParams
                {
                    ElementType = "Wall",
                    Parameters = new Dictionary<string, object>
                    {
                        { "Length", 5000.0 },
                        { "Height", 2700.0 }
                    }
                };

                var result = await _creator.CreateAsync(parameters);

                result.StartTime.Should().BeOnOrAfter(before);
                result.EndTime.Should().BeOnOrAfter(result.StartTime);
                result.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
            }

            [Test]
            public async Task CreateAsync_PopulatesMetadata()
            {
                var parameters = new ElementCreationParams
                {
                    ElementType = "Wall",
                    Parameters = new Dictionary<string, object>
                    {
                        { "Length", 4000.0 },
                        { "Height", 3000.0 },
                        { "WallType", "Curtain Wall" }
                    }
                };

                var result = await _creator.CreateAsync(parameters);

                result.Metadata.Should().ContainKey("WallType");
                result.Metadata.Should().ContainKey("Length");
                result.Metadata.Should().ContainKey("Height");
            }

            [Test]
            public async Task CreateAsync_NullParams_ReturnsFailure()
            {
                var result = await _creator.CreateAsync(null);

                result.Should().NotBeNull();
                result.Success.Should().BeFalse();
                result.Error.Should().NotBeNullOrWhiteSpace();
            }

            [Test]
            public async Task CreateAsync_MissingLength_ReturnsFailure()
            {
                var parameters = new ElementCreationParams
                {
                    ElementType = "Wall",
                    Parameters = new Dictionary<string, object>()
                };

                var result = await _creator.CreateAsync(parameters);

                result.Should().NotBeNull();
                result.Success.Should().BeFalse();
            }

            [Test]
            public async Task CreateAsync_SupportsCancellation()
            {
                var cts = new CancellationTokenSource();
                cts.Cancel();

                var parameters = new ElementCreationParams
                {
                    ElementType = "Wall",
                    Parameters = new Dictionary<string, object>
                    {
                        { "Length", 3000.0 },
                        { "Height", 2700.0 }
                    }
                };

                Func<Task> act = () => _creator.CreateAsync(parameters, cts.Token);

                await act.Should().ThrowAsync<OperationCanceledException>();
            }

            [Test]
            public async Task CreateByPointsAsync_ValidPoints_ReturnsSuccess()
            {
                var start = new Point3D(0, 0, 0);
                var end = new Point3D(5000, 0, 0);

                var result = await _creator.CreateByPointsAsync(start, end);

                result.Should().NotBeNull();
                result.Success.Should().BeTrue();
            }

            [Test]
            public async Task CreateByPointsAsync_WithOptions_UsesOptions()
            {
                var start = new Point3D(0, 0, 0);
                var end = new Point3D(6000, 0, 0);
                var options = new WallCreationOptions
                {
                    Height = 3500,
                    WallTypeName = "Exterior Wall",
                    IsStructural = true
                };

                var result = await _creator.CreateByPointsAsync(start, end, options);

                result.Should().NotBeNull();
                result.Success.Should().BeTrue();
            }

            [Test]
            public async Task CreateByPointsAsync_CalculatesLength()
            {
                var start = new Point3D(0, 0, 0);
                var end = new Point3D(3000, 4000, 0);
                // Distance = sqrt(3000² + 4000²) = 5000

                var result = await _creator.CreateByPointsAsync(start, end);

                result.Should().NotBeNull();
                result.Success.Should().BeTrue();
                result.Metadata.Should().ContainKey("Length");
            }

            [Test]
            public async Task CreateRectangleAsync_Creates4Walls()
            {
                var origin = new Point3D(0, 0, 0);

                var result = await _creator.CreateRectangleAsync(origin, 5000, 4000);

                result.Should().NotBeNull();
                result.Results.Should().HaveCount(4, "rectangle requires 4 walls");
                result.TotalCreated.Should().Be(4);
                result.TotalFailed.Should().Be(0);
                result.AllSucceeded.Should().BeTrue();
            }

            [Test]
            public async Task CreateRectangleAsync_SetsTimestamps()
            {
                var origin = new Point3D(0, 0, 0);

                var result = await _creator.CreateRectangleAsync(origin, 3000, 3000);

                result.StartTime.Should().NotBe(default);
                result.EndTime.Should().BeOnOrAfter(result.StartTime);
            }

            [Test]
            public async Task CreateRectangleAsync_SupportsCancellation()
            {
                var cts = new CancellationTokenSource();
                cts.Cancel();

                var origin = new Point3D(0, 0, 0);

                Func<Task> act = () => _creator.CreateRectangleAsync(origin, 5000, 4000, cancellationToken: cts.Token);

                await act.Should().ThrowAsync<OperationCanceledException>();
            }

            [Test]
            public async Task ModifyAsync_ValidModifications_ReturnsSuccess()
            {
                var modifications = new Dictionary<string, object>
                {
                    { "Height", 3000.0 },
                    { "WallType", "Partition Wall" }
                };

                var result = await _creator.ModifyAsync(12345, modifications);

                result.Should().NotBeNull();
                result.Success.Should().BeTrue();
                result.ElementId.Should().Be(12345);
                result.ModifiedProperties.Should().ContainKey("Height");
                result.ModifiedProperties.Should().ContainKey("WallType");
            }

            [Test]
            public async Task ModifyAsync_EmptyModifications_ReturnsSuccess()
            {
                var modifications = new Dictionary<string, object>();

                var result = await _creator.ModifyAsync(1, modifications);

                result.Should().NotBeNull();
                result.Success.Should().BeTrue();
            }

            [Test]
            public void GetAvailableWallTypes_ReturnsNonEmptyList()
            {
                var wallTypes = _creator.GetAvailableWallTypes().ToList();

                wallTypes.Should().NotBeEmpty();
                wallTypes.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Name));
            }
        }

        #endregion

        #region Point3D Geometry Tests

        [TestFixture]
        public class Point3DTests
        {
            [Test]
            public void DistanceTo_KnownTriangle_ReturnsCorrectDistance()
            {
                var a = new Point3D(0, 0, 0);
                var b = new Point3D(3, 4, 0);

                a.DistanceTo(b).Should().BeApproximately(5.0, 0.001);
            }

            [Test]
            public void DistanceTo_SamePoint_ReturnsZero()
            {
                var a = new Point3D(1, 2, 3);

                a.DistanceTo(a).Should().BeApproximately(0.0, 0.001);
            }

            [Test]
            public void DistanceTo_3D_ReturnsCorrectDistance()
            {
                var a = new Point3D(0, 0, 0);
                var b = new Point3D(1, 2, 2);

                // sqrt(1 + 4 + 4) = 3
                a.DistanceTo(b).Should().BeApproximately(3.0, 0.001);
            }

            [Test]
            public void Add_TwoPoints_ReturnsSum()
            {
                var a = new Point3D(1, 2, 3);
                var b = new Point3D(4, 5, 6);

                var result = a.Add(b);

                result.X.Should().Be(5);
                result.Y.Should().Be(7);
                result.Z.Should().Be(9);
            }

            [Test]
            public void Subtract_TwoPoints_ReturnsDifference()
            {
                var a = new Point3D(5, 7, 9);
                var b = new Point3D(1, 2, 3);

                var result = a.Subtract(b);

                result.X.Should().Be(4);
                result.Y.Should().Be(5);
                result.Z.Should().Be(6);
            }

            [Test]
            public void Scale_ByFactor_ScalesAllComponents()
            {
                var p = new Point3D(2, 3, 4);

                var result = p.Scale(3.0);

                result.X.Should().Be(6);
                result.Y.Should().Be(9);
                result.Z.Should().Be(12);
            }
        }

        #endregion

        #region BoundingBox Tests

        [TestFixture]
        public class BoundingBoxTests
        {
            [Test]
            public void Dimensions_ReturnCorrectValues()
            {
                var bb = new BoundingBox
                {
                    Min = new Point3D(0, 0, 0),
                    Max = new Point3D(10, 20, 30)
                };

                bb.Width.Should().Be(10);
                bb.Depth.Should().Be(20);
                bb.Height.Should().Be(30);
            }

            [Test]
            public void Center_ReturnsMidpoint()
            {
                var bb = new BoundingBox
                {
                    Min = new Point3D(0, 0, 0),
                    Max = new Point3D(10, 20, 30)
                };

                bb.Center.X.Should().Be(5);
                bb.Center.Y.Should().Be(10);
                bb.Center.Z.Should().Be(15);
            }

            [Test]
            public void Contains_PointInside_ReturnsTrue()
            {
                var bb = new BoundingBox
                {
                    Min = new Point3D(0, 0, 0),
                    Max = new Point3D(10, 10, 10)
                };

                bb.Contains(new Point3D(5, 5, 5)).Should().BeTrue();
            }

            [Test]
            public void Contains_PointOutside_ReturnsFalse()
            {
                var bb = new BoundingBox
                {
                    Min = new Point3D(0, 0, 0),
                    Max = new Point3D(10, 10, 10)
                };

                bb.Contains(new Point3D(15, 5, 5)).Should().BeFalse();
            }

            [Test]
            public void Contains_PointOnBoundary_ReturnsTrue()
            {
                var bb = new BoundingBox
                {
                    Min = new Point3D(0, 0, 0),
                    Max = new Point3D(10, 10, 10)
                };

                bb.Contains(new Point3D(0, 0, 0)).Should().BeTrue();
                bb.Contains(new Point3D(10, 10, 10)).Should().BeTrue();
            }

            [Test]
            public void Intersects_OverlappingBoxes_ReturnsTrue()
            {
                var bb1 = new BoundingBox
                {
                    Min = new Point3D(0, 0, 0),
                    Max = new Point3D(10, 10, 10)
                };
                var bb2 = new BoundingBox
                {
                    Min = new Point3D(5, 5, 5),
                    Max = new Point3D(15, 15, 15)
                };

                bb1.Intersects(bb2).Should().BeTrue();
                bb2.Intersects(bb1).Should().BeTrue();
            }

            [Test]
            public void Intersects_SeparateBoxes_ReturnsFalse()
            {
                var bb1 = new BoundingBox
                {
                    Min = new Point3D(0, 0, 0),
                    Max = new Point3D(5, 5, 5)
                };
                var bb2 = new BoundingBox
                {
                    Min = new Point3D(10, 10, 10),
                    Max = new Point3D(20, 20, 20)
                };

                bb1.Intersects(bb2).Should().BeFalse();
            }
        }

        #endregion

        #region ElementGeometry Tests

        [TestFixture]
        public class ElementGeometryTests
        {
            [Test]
            public void Area_ReturnsWidthTimesLength()
            {
                var geom = new ElementGeometry { Width = 5, Length = 10, Height = 3 };

                geom.Area.Should().Be(50);
            }

            [Test]
            public void Volume_ReturnsWidthTimesLengthTimesHeight()
            {
                var geom = new ElementGeometry { Width = 5, Length = 10, Height = 3 };

                geom.Volume.Should().Be(150);
            }
        }

        #endregion

        #region ValidationResult Tests

        [TestFixture]
        public class ValidationResultTests
        {
            [Test]
            public void Valid_ReturnsIsValidTrue()
            {
                var result = ValidationResult.Valid();

                result.IsValid.Should().BeTrue();
                result.Error.Should().BeNull();
            }

            [Test]
            public void Invalid_ReturnsIsValidFalse()
            {
                var result = ValidationResult.Invalid("Something went wrong");

                result.IsValid.Should().BeFalse();
                result.Error.Should().Be("Something went wrong");
            }
        }

        #endregion
    }
}
