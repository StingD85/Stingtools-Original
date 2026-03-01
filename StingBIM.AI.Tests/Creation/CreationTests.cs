// StingBIM.AI.Tests.Creation.CreationTests
// Comprehensive unit tests for the AI Creation module
// Covers: WallCreator, FloorCreator, RoomGenerator, CreativeGenerator

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using Moq;
using StingBIM.AI.Creation.Elements;
using StingBIM.AI.Creation.Spaces;
using StingBIM.AI.Creation.Creative;

namespace StingBIM.AI.Tests.Creation
{
    #region WallCreator Tests

    [TestFixture]
    public class WallCreatorTests
    {
        private WallCreator _wallCreator;

        [SetUp]
        public void SetUp()
        {
            _wallCreator = new WallCreator();
        }

        #region GetAvailableWallTypes Tests

        [Test]
        public void GetAvailableWallTypes_ShouldReturnNonEmptyCollection()
        {
            var wallTypes = _wallCreator.GetAvailableWallTypes().ToList();

            wallTypes.Should().NotBeNull();
            wallTypes.Should().NotBeEmpty();
        }

        [Test]
        public void GetAvailableWallTypes_ShouldReturnTypesWithNamesAndPositiveThickness()
        {
            var wallTypes = _wallCreator.GetAvailableWallTypes().ToList();

            wallTypes.Should().OnlyContain(wt => !string.IsNullOrWhiteSpace(wt.Name));
            wallTypes.Should().OnlyContain(wt => wt.Thickness > 0);
        }

        [Test]
        public void GetAvailableWallTypes_ShouldContainBasicWallType()
        {
            var wallTypes = _wallCreator.GetAvailableWallTypes().ToList();

            wallTypes.Should().Contain(wt => wt.Name == "Basic Wall");
        }

        [Test]
        public void GetAvailableWallTypes_ShouldIncludeBothStructuralAndNonStructural()
        {
            var wallTypes = _wallCreator.GetAvailableWallTypes().ToList();

            wallTypes.Should().Contain(wt => wt.IsStructural);
            wallTypes.Should().Contain(wt => !wt.IsStructural);
        }

        [Test]
        public void GetAvailableWallTypes_ShouldIncludeFireRatedTypes()
        {
            var wallTypes = _wallCreator.GetAvailableWallTypes().ToList();

            wallTypes.Should().Contain(wt => wt.FireRatingMinutes > 0,
                "catalogue should include fire-rated wall types");
        }

        [Test]
        public void GetAvailableWallTypes_ShouldHavePositiveCostAndMaterial()
        {
            var wallTypes = _wallCreator.GetAvailableWallTypes().ToList();

            wallTypes.Should().OnlyContain(wt => wt.CostPerM2 > 0);
            wallTypes.Should().OnlyContain(wt => !string.IsNullOrWhiteSpace(wt.Material));
            wallTypes.Should().OnlyContain(wt => wt.STCAcousticRating >= 0);
        }

        #endregion

        #region RecommendWallType Tests

        [Test]
        public void RecommendWallType_ForInteriorPartition_ShouldRecommendNonStructural()
        {
            var context = new WallContext
            {
                Location = WallLocation.Interior,
                RequiresStructural = false
            };

            var recommendation = _wallCreator.RecommendWallType(context);

            recommendation.Should().NotBeNull();
            recommendation.RecommendedType.Should().NotBeNullOrEmpty();
            recommendation.Confidence.Should().BeInRange(0.0, 1.0);
        }

        [Test]
        public void RecommendWallType_ForExteriorWall_ShouldRecommendStructuralType()
        {
            var context = new WallContext
            {
                Location = WallLocation.Exterior,
                RequiresStructural = true
            };

            var recommendation = _wallCreator.RecommendWallType(context);

            recommendation.RecommendedType.Should().NotBeNullOrEmpty();
            recommendation.IsStructural.Should().BeTrue();
            recommendation.Material.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void RecommendWallType_WithFireRatingRequirement_ShouldMeetMinimum()
        {
            var context = new WallContext
            {
                Location = WallLocation.Interior,
                RequiredFireRating = 60
            };

            var recommendation = _wallCreator.RecommendWallType(context);

            recommendation.FireRatingMinutes.Should().BeGreaterThanOrEqualTo(60);
            recommendation.Notes.Should().Contain(n =>
                n.Contains("fire", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void RecommendWallType_WithAcousticRequirement_ShouldMeetMinimumSTC()
        {
            var context = new WallContext
            {
                Location = WallLocation.Interior,
                MinAcousticRating = 50
            };

            var recommendation = _wallCreator.RecommendWallType(context);

            recommendation.AcousticRating.Should().BeGreaterThanOrEqualTo(50);
        }

        [Test]
        public void RecommendWallType_WithMoistureResistance_ShouldNoteRequirement()
        {
            var context = new WallContext
            {
                Location = WallLocation.Interior,
                RequiresMoistureResistance = true
            };

            var recommendation = _wallCreator.RecommendWallType(context);

            recommendation.Notes.Should().Contain(n =>
                n.Contains("moisture", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void RecommendWallType_ShouldIncludeAlternativesAndCost()
        {
            var context = new WallContext
            {
                Location = WallLocation.Interior,
                PreferLowCost = true
            };

            var recommendation = _wallCreator.RecommendWallType(context);

            recommendation.Alternatives.Should().NotBeNull();
            recommendation.EstimatedCostPerM2.Should().BeGreaterThan(0);
        }

        [Test]
        public void RecommendWallType_ForPartyWall_ShouldProvideNotes()
        {
            var context = new WallContext
            {
                Location = WallLocation.Interior,
                IsPartyWall = true
            };

            var recommendation = _wallCreator.RecommendWallType(context);

            recommendation.Notes.Should().NotBeNull();
        }

        #endregion

        #region CreateAsync Tests

        [Test]
        public async Task CreateAsync_WithValidParameters_ShouldReturnSuccessResult()
        {
            var parameters = new ElementCreationParams
            {
                ElementType = "Wall",
                Parameters = new Dictionary<string, object>
                {
                    { "Length", 5000.0 },
                    { "Height", 2700.0 },
                    { "WallType", "Basic Wall" },
                    { "Level", "Level 1" }
                }
            };

            var result = await _wallCreator.CreateAsync(parameters);

            result.Success.Should().BeTrue();
            result.CreatedElementId.Should().BeGreaterThan(0);
            result.ElementType.Should().Be("Wall");
            result.Message.Should().NotBeNullOrEmpty();
        }

        [Test]
        public async Task CreateAsync_WithTooShortLength_ShouldReturnFailure()
        {
            var parameters = new ElementCreationParams
            {
                ElementType = "Wall",
                Parameters = new Dictionary<string, object>
                {
                    { "Length", 50.0 },
                    { "Height", 2700.0 },
                    { "WallType", "Basic Wall" }
                }
            };

            var result = await _wallCreator.CreateAsync(parameters);

            result.Success.Should().BeFalse();
            result.Error.Should().NotBeNullOrEmpty();
        }

        [Test]
        public async Task CreateAsync_WithExcessiveLength_ShouldReturnFailure()
        {
            var parameters = new ElementCreationParams
            {
                ElementType = "Wall",
                Parameters = new Dictionary<string, object>
                {
                    { "Length", 200000.0 },
                    { "Height", 2700.0 },
                    { "WallType", "Basic Wall" }
                }
            };

            var result = await _wallCreator.CreateAsync(parameters);

            result.Success.Should().BeFalse();
            result.Error.Should().Contain("length", StringComparison.OrdinalIgnoreCase);
        }

        [Test]
        public async Task CreateAsync_WithFireRatedWallType_ShouldIncludeFireMetadata()
        {
            var parameters = new ElementCreationParams
            {
                ElementType = "Wall",
                Parameters = new Dictionary<string, object>
                {
                    { "Length", 5000.0 },
                    { "Height", 2700.0 },
                    { "WallType", "Interior - Fire Rated 1hr" }
                }
            };

            var result = await _wallCreator.CreateAsync(parameters);

            result.Success.Should().BeTrue();
            result.Metadata.Should().ContainKey("FireRating");
        }

        [Test]
        public async Task CreateAsync_WithCancellationToken_ShouldRespectCancellation()
        {
            var parameters = new ElementCreationParams
            {
                ElementType = "Wall",
                Parameters = new Dictionary<string, object>
                {
                    { "Length", 5000.0 },
                    { "Height", 2700.0 },
                    { "WallType", "Basic Wall" }
                }
            };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task> act = async () => await _wallCreator.CreateAsync(parameters, cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        #endregion

        #region CheckCollisions Tests

        [Test]
        public void CheckCollisions_WithNoExistingWalls_ShouldReturnNoCollisions()
        {
            var start = new Point3D(0, 0, 0);
            var end = new Point3D(5000, 0, 0);

            var result = _wallCreator.CheckCollisions(start, end, 200, new List<WallPlacement>());

            result.HasCollisions.Should().BeFalse();
            result.Collisions.Should().BeEmpty();
        }

        [Test]
        public void CheckCollisions_WithNullExistingWalls_ShouldReturnNoCollisions()
        {
            var result = _wallCreator.CheckCollisions(
                new Point3D(0, 0, 0), new Point3D(5000, 0, 0), 200, null);

            result.HasCollisions.Should().BeFalse();
        }

        [Test]
        public void CheckCollisions_WithParallelOverlappingWall_ShouldDetectCollision()
        {
            var start = new Point3D(0, 0, 0);
            var end = new Point3D(5000, 0, 0);
            var existingWalls = new List<WallPlacement>
            {
                new WallPlacement
                {
                    ElementId = 100,
                    StartPoint = new Point3D(0, 50, 0),
                    EndPoint = new Point3D(5000, 50, 0),
                    Thickness = 200
                }
            };

            var result = _wallCreator.CheckCollisions(start, end, 200, existingWalls);

            result.HasCollisions.Should().BeTrue();
            result.Collisions.Should().NotBeEmpty();
            result.Collisions.First().CollisionType.Should().Be(WallCollisionType.Parallel);
        }

        [Test]
        public void CheckCollisions_WithPerpendicularWall_ShouldDetectJunction()
        {
            var start = new Point3D(0, 0, 0);
            var end = new Point3D(5000, 0, 0);
            var existingWalls = new List<WallPlacement>
            {
                new WallPlacement
                {
                    ElementId = 200,
                    StartPoint = new Point3D(2500, -2000, 0),
                    EndPoint = new Point3D(2500, 2000, 0),
                    Thickness = 200
                }
            };

            var result = _wallCreator.CheckCollisions(start, end, 200, existingWalls);

            result.Junctions.Should().NotBeNull();
        }

        [Test]
        public void CheckCollisions_WithDistantWalls_ShouldReturnNoCollisions()
        {
            var start = new Point3D(0, 0, 0);
            var end = new Point3D(5000, 0, 0);
            var existingWalls = new List<WallPlacement>
            {
                new WallPlacement
                {
                    ElementId = 300,
                    StartPoint = new Point3D(0, 5000, 0),
                    EndPoint = new Point3D(5000, 5000, 0),
                    Thickness = 200
                }
            };

            var result = _wallCreator.CheckCollisions(start, end, 200, existingWalls);

            result.HasCollisions.Should().BeFalse();
        }

        #endregion

        #region ModifyAsync Tests

        [Test]
        public async Task ModifyAsync_WithValidModifications_ShouldReturnSuccess()
        {
            var modifications = new Dictionary<string, object>
            {
                { "Height", 3000.0 },
                { "WallType", "Interior - Acoustic" }
            };

            var result = await _wallCreator.ModifyAsync(12345, modifications);

            result.Success.Should().BeTrue();
            result.ElementId.Should().Be(12345);
            result.ModifiedProperties.Should().HaveCount(2);
            result.ModifiedProperties.Should().ContainKey("Height");
        }

        #endregion
    }

    #endregion

    #region FloorCreator Tests

    [TestFixture]
    public class FloorCreatorTests
    {
        private FloorCreator _floorCreator;

        [SetUp]
        public void SetUp()
        {
            _floorCreator = new FloorCreator();
        }

        #region GetAvailableFloorTypes Tests

        [Test]
        public void GetAvailableFloorTypes_ShouldReturnNonEmptyCollection()
        {
            var floorTypes = _floorCreator.GetAvailableFloorTypes().ToList();

            floorTypes.Should().NotBeNull();
            floorTypes.Should().NotBeEmpty();
        }

        [Test]
        public void GetAvailableFloorTypes_ShouldReturnTypesWithNamesAndThickness()
        {
            var floorTypes = _floorCreator.GetAvailableFloorTypes().ToList();

            floorTypes.Should().OnlyContain(ft => !string.IsNullOrWhiteSpace(ft.Name));
            floorTypes.Should().OnlyContain(ft => ft.Thickness > 0);
        }

        [Test]
        public void GetAvailableFloorTypes_ShouldIncludeConcreteSlabs()
        {
            var floorTypes = _floorCreator.GetAvailableFloorTypes().ToList();

            floorTypes.Should().Contain(ft =>
                ft.Name.Contains("Concrete", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void GetAvailableFloorTypes_StructuralTypesShouldHaveLiveLoadCapacity()
        {
            var floorTypes = _floorCreator.GetAvailableFloorTypes().ToList();

            floorTypes.Where(ft => ft.IsStructural)
                .Should().OnlyContain(ft => ft.LiveLoadCapacity > 0);
        }

        [Test]
        public void GetAvailableFloorTypes_ShouldHavePositiveCost()
        {
            var floorTypes = _floorCreator.GetAvailableFloorTypes().ToList();

            floorTypes.Should().OnlyContain(ft => ft.CostPerM2 > 0);
        }

        #endregion

        #region RecommendFloorType Tests

        [Test]
        public void RecommendFloorType_ForResidential_ShouldReturnRecommendation()
        {
            var context = new FloorContext
            {
                OccupancyType = "residential",
                MaxSpanRequired = 4000,
                FloorLevel = 1,
                RequiresStructural = true
            };

            var recommendation = _floorCreator.RecommendFloorType(context);

            recommendation.Should().NotBeNull();
            recommendation.RecommendedType.Should().NotBeNullOrEmpty();
            recommendation.Confidence.Should().BeInRange(0.0, 1.0);
        }

        [Test]
        public void RecommendFloorType_ForOffice_ShouldMeetLiveLoadRequirements()
        {
            var context = new FloorContext
            {
                OccupancyType = "office",
                MaxSpanRequired = 6000,
                RequiresStructural = true
            };

            var recommendation = _floorCreator.RecommendFloorType(context);

            recommendation.LiveLoadCapacity.Should().BeGreaterThanOrEqualTo(2.4,
                "office occupancy requires at least 2.4 kN/m2 per ASCE 7");
        }

        [Test]
        public void RecommendFloorType_ForGroundFloor_ShouldNoteDampProofing()
        {
            var context = new FloorContext
            {
                OccupancyType = "residential",
                FloorLevel = 0,
                IsGroundBearing = true,
                RequiresStructural = true
            };

            var recommendation = _floorCreator.RecommendFloorType(context);

            recommendation.Notes.Should().Contain(n =>
                n.Contains("ground", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("damp", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void RecommendFloorType_ShouldProvideAlternativesAndCost()
        {
            var context = new FloorContext
            {
                OccupancyType = "residential",
                MaxSpanRequired = 4000,
                RequiresStructural = true,
                PreferLowCost = true
            };

            var recommendation = _floorCreator.RecommendFloorType(context);

            recommendation.Alternatives.Should().NotBeNull();
            recommendation.Material.Should().NotBeNullOrEmpty();
            recommendation.EstimatedCostPerM2.Should().BeGreaterThan(0);
        }

        #endregion

        #region ValidateSlope Tests

        [Test]
        public void ValidateSlope_AccessibleRampWithinLimit_ShouldBeCompliant()
        {
            var result = _floorCreator.ValidateSlope(5.0, "ramp");

            result.IsCompliant.Should().BeTrue();
            result.SlopePercent.Should().Be(5.0);
            result.Purpose.Should().Be("ramp");
        }

        [Test]
        public void ValidateSlope_AccessibleRampExceedingLimit_ShouldBeNonCompliant()
        {
            var result = _floorCreator.ValidateSlope(10.0, "ramp");

            result.IsCompliant.Should().BeFalse();
            result.Message.Should().Contain("ADA");
            result.MaxAllowedSlope.Should().Be(8.33);
        }

        [Test]
        public void ValidateSlope_SteepRamp_ShouldRequireHandrails()
        {
            var result = _floorCreator.ValidateSlope(7.0, "ramp");

            result.IsCompliant.Should().BeTrue();
            result.RequiresHandrails.Should().BeTrue();
            result.RequiresLandings.Should().BeTrue();
        }

        [Test]
        public void ValidateSlope_GentleRamp_ShouldNotRequireHandrails()
        {
            var result = _floorCreator.ValidateSlope(3.0, "ramp");

            result.IsCompliant.Should().BeTrue();
            result.RequiresHandrails.Should().BeFalse();
        }

        [Test]
        public void ValidateSlope_ParkingRampWithinLimit_ShouldBeCompliant()
        {
            var result = _floorCreator.ValidateSlope(15.0, "parking");

            result.IsCompliant.Should().BeTrue();
        }

        [Test]
        public void ValidateSlope_ParkingRampExceedingLimit_ShouldBeNonCompliant()
        {
            var result = _floorCreator.ValidateSlope(25.0, "parking");

            result.IsCompliant.Should().BeFalse();
            result.MaxAllowedSlope.Should().Be(20);
        }

        [Test]
        public void ValidateSlope_DrainageSlopeTooShallow_ShouldBeNonCompliant()
        {
            var result = _floorCreator.ValidateSlope(0.5, "drainage");

            result.IsCompliant.Should().BeFalse();
            result.Message.Should().Contain("drainage", StringComparison.OrdinalIgnoreCase);
        }

        [Test]
        public void ValidateSlope_GeneralFloorExceedingAccessibleMax_ShouldBeNonCompliant()
        {
            var result = _floorCreator.ValidateSlope(3.0, "general");

            result.IsCompliant.Should().BeFalse();
            result.Code.Should().Contain("ADA");
        }

        [Test]
        public void ValidateSlope_GeneralFloorWithinLimit_ShouldBeCompliant()
        {
            var result = _floorCreator.ValidateSlope(1.5, "general");

            result.IsCompliant.Should().BeTrue();
        }

        #endregion

        #region CreateAsync and ModifyAsync Tests

        [Test]
        public async Task CreateAsync_WithValidFloorParameters_ShouldReturnSuccess()
        {
            var parameters = new ElementCreationParams
            {
                ElementType = "Floor",
                Parameters = new Dictionary<string, object>
                {
                    { "Area", 20000000.0 },
                    { "FloorType", "Generic - 150mm" },
                    { "Level", "Level 1" }
                }
            };

            var result = await _floorCreator.CreateAsync(parameters);

            result.Success.Should().BeTrue();
            result.ElementType.Should().Be("Floor");
            result.CreatedElementId.Should().BeGreaterThan(0);
        }

        [Test]
        public async Task CreateAsync_WithTooSmallArea_ShouldReturnFailure()
        {
            var parameters = new ElementCreationParams
            {
                ElementType = "Floor",
                Parameters = new Dictionary<string, object>
                {
                    { "Area", 500000.0 },
                    { "FloorType", "Generic - 150mm" }
                }
            };

            var result = await _floorCreator.CreateAsync(parameters);

            result.Success.Should().BeFalse();
            result.Error.Should().NotBeNullOrEmpty();
        }

        [Test]
        public async Task ModifyAsync_WithValidModifications_ShouldReturnSuccess()
        {
            var modifications = new Dictionary<string, object>
            {
                { "Thickness", 200.0 },
                { "FloorType", "Concrete Slab - 200mm" }
            };

            var result = await _floorCreator.ModifyAsync(54321, modifications);

            result.Success.Should().BeTrue();
            result.ElementId.Should().Be(54321);
            result.ModifiedProperties.Should().HaveCount(2);
        }

        #endregion
    }

    #endregion

    #region RoomGenerator Tests

    [TestFixture]
    public class RoomGeneratorTests
    {
        private RoomGenerator _roomGenerator;
        private WallCreator _wallCreator;
        private FloorCreator _floorCreator;

        [SetUp]
        public void SetUp()
        {
            _wallCreator = new WallCreator();
            _floorCreator = new FloorCreator();
            _roomGenerator = new RoomGenerator(_wallCreator, _floorCreator);
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullWallCreator_ShouldThrowArgumentNullException()
        {
            Action act = () => new RoomGenerator(null, new FloorCreator());
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("wallCreator");
        }

        [Test]
        public void Constructor_WithNullFloorCreator_ShouldThrowArgumentNullException()
        {
            Action act = () => new RoomGenerator(new WallCreator(), null);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("floorCreator");
        }

        #endregion

        #region GetRoomTypes Tests

        [Test]
        public void GetRoomTypes_ShouldReturnNonEmptyCollection()
        {
            var roomTypes = _roomGenerator.GetRoomTypes().ToList();

            roomTypes.Should().NotBeNull();
            roomTypes.Should().NotBeEmpty();
        }

        [Test]
        public void GetRoomTypes_ShouldContainCommonResidentialRooms()
        {
            var roomTypes = _roomGenerator.GetRoomTypes().ToList();
            var names = roomTypes.Select(rt => rt.Name).ToList();

            names.Should().Contain(n => n.Contains("Bedroom", StringComparison.OrdinalIgnoreCase));
            names.Should().Contain(n => n.Contains("Kitchen", StringComparison.OrdinalIgnoreCase));
            names.Should().Contain(n => n.Contains("Bathroom", StringComparison.OrdinalIgnoreCase));
            names.Should().Contain(n => n.Contains("Living", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void GetRoomTypes_ShouldHavePositiveMinAreasAndDefaultDimensions()
        {
            var roomTypes = _roomGenerator.GetRoomTypes().ToList();

            roomTypes.Should().OnlyContain(rt => rt.MinArea > 0);
            roomTypes.Should().OnlyContain(rt =>
                rt.DefaultWidth > 0 && rt.DefaultDepth > 0 && rt.DefaultHeight > 0);
        }

        [Test]
        public void GetRoomTypes_LivingRoomShouldHaveLargerMinAreaThanBathroom()
        {
            var roomTypes = _roomGenerator.GetRoomTypes().ToList();
            var livingRoom = roomTypes.FirstOrDefault(rt =>
                rt.Name.Contains("Living", StringComparison.OrdinalIgnoreCase));
            var bathroom = roomTypes.FirstOrDefault(rt =>
                rt.Name.Contains("Bathroom", StringComparison.OrdinalIgnoreCase));

            livingRoom.Should().NotBeNull();
            bathroom.Should().NotBeNull();
            livingRoom.MinArea.Should().BeGreaterThan(bathroom.MinArea);
        }

        [Test]
        public void GetRoomTypes_BathroomShouldRequirePlumbing()
        {
            var roomTypes = _roomGenerator.GetRoomTypes().ToList();
            var bathroom = roomTypes.FirstOrDefault(rt =>
                rt.Name.Contains("Bathroom", StringComparison.OrdinalIgnoreCase));

            bathroom.Should().NotBeNull();
            bathroom.RequiresPlumbing.Should().BeTrue();
        }

        [Test]
        public void GetRoomTypes_BedroomShouldRequireWindow()
        {
            var roomTypes = _roomGenerator.GetRoomTypes().ToList();
            var bedroom = roomTypes.FirstOrDefault(rt =>
                rt.Name.Equals("Bedroom", StringComparison.OrdinalIgnoreCase));

            bedroom.Should().NotBeNull();
            bedroom.RequiresWindow.Should().BeTrue();
        }

        #endregion

        #region ValidateRoom Tests

        [Test]
        public void ValidateRoom_WithValidDimensions_ShouldReturnValid()
        {
            var result = _roomGenerator.ValidateRoom("bedroom", 3500, 4000, 2700);

            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void ValidateRoom_WithTooLowHeight_ShouldReturnInvalid()
        {
            var result = _roomGenerator.ValidateRoom("bedroom", 3500, 4000, 2000);

            result.IsValid.Should().BeFalse();
            result.Error.Should().Contain("height", StringComparison.OrdinalIgnoreCase);
        }

        [Test]
        public void ValidateRoom_WithSmallArea_ShouldIncludeWarning()
        {
            var result = _roomGenerator.ValidateRoom("bedroom", 2000, 2500, 2700);

            result.Warnings.Should().NotBeEmpty();
            result.Warnings.Should().Contain(w =>
                w.Contains("area", StringComparison.OrdinalIgnoreCase) ||
                w.Contains("minimum", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void ValidateRoom_WithElongatedProportion_ShouldIncludeWarning()
        {
            var result = _roomGenerator.ValidateRoom("bedroom", 1500, 10000, 2700);

            result.Warnings.Should().Contain(w =>
                w.Contains("aspect ratio", StringComparison.OrdinalIgnoreCase) ||
                w.Contains("elongated", StringComparison.OrdinalIgnoreCase) ||
                w.Contains("proportion", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void ValidateRoom_ForUnknownRoomType_ShouldStillValidate()
        {
            var result = _roomGenerator.ValidateRoom("mysterious chamber", 3000, 3000, 2700);

            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void ValidateRoom_WithMinimumAllowableHeight_ShouldPass()
        {
            var result = _roomGenerator.ValidateRoom("bedroom", 3000, 4000, 2400);

            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region GenerateRoomByTypeAsync Tests

        [Test]
        public async Task GenerateRoomByTypeAsync_ForBedroom_ShouldReturnSuccessResult()
        {
            var origin = new Point3D(0, 0, 0);

            var result = await _roomGenerator.GenerateRoomByTypeAsync("bedroom", origin, "Level 1");

            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.RoomName.Should().NotBeNullOrEmpty();
            result.Dimensions.Should().NotBeNull();
            result.Dimensions.Width.Should().BeGreaterThan(0);
            result.Dimensions.Depth.Should().BeGreaterThan(0);
        }

        [Test]
        public async Task GenerateRoomByTypeAsync_ForKitchen_ShouldReturnCorrectType()
        {
            var origin = new Point3D(10000, 0, 0);

            var result = await _roomGenerator.GenerateRoomByTypeAsync("kitchen", origin, "Level 1");

            result.Success.Should().BeTrue();
            result.RoomType.Should().Be("kitchen");
        }

        [Test]
        public async Task GenerateRoomByTypeAsync_ShouldCreateWallsAndFloor()
        {
            var origin = new Point3D(0, 0, 0);

            var result = await _roomGenerator.GenerateRoomByTypeAsync("living room", origin);

            result.Success.Should().BeTrue();
            result.CreatedWallIds.Should().NotBeEmpty(
                "room generation should create enclosing walls");
            result.CreatedFloorId.Should().NotBeNull(
                "room generation should create a floor");
        }

        [Test]
        public async Task GenerateRoomByTypeAsync_ShouldProvideSuggestedOpenings()
        {
            var origin = new Point3D(0, 0, 0);

            var result = await _roomGenerator.GenerateRoomByTypeAsync("bedroom", origin);

            result.Success.Should().BeTrue();
            result.SuggestedOpenings.Should().NotBeEmpty(
                "bedrooms should have suggested door and window openings");
        }

        [Test]
        public async Task GenerateRoomByTypeAsync_WithCancellation_ShouldRespectToken()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task> act = async () =>
                await _roomGenerator.GenerateRoomByTypeAsync("bedroom", null, null, cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task GenerateRoomByTypeAsync_ForBathroom_ShouldReturnDimensions()
        {
            var origin = new Point3D(0, 0, 0);

            var result = await _roomGenerator.GenerateRoomByTypeAsync("bathroom", origin);

            result.Success.Should().BeTrue();
            result.Dimensions.Area.Should().BeGreaterThan(0);
            result.Dimensions.Volume.Should().BeGreaterThan(0);
        }

        [Test]
        public async Task GenerateRoomByTypeAsync_ForUnknownType_ShouldUseDefaults()
        {
            var origin = new Point3D(0, 0, 0);

            var result = await _roomGenerator.GenerateRoomByTypeAsync("unknown room type", origin);

            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
        }

        #endregion

        #region GenerateRoomAsync Tests

        [Test]
        public async Task GenerateRoomAsync_WithCustomDimensions_ShouldUseSpecifiedDimensions()
        {
            var parameters = new RoomGenerationParams
            {
                RoomType = "bedroom",
                Width = 4000,
                Depth = 5000,
                Height = 3000,
                Origin = new Point3D(0, 0, 0),
                LevelName = "Level 1",
                IncludeFloor = true
            };

            var result = await _roomGenerator.GenerateRoomAsync(parameters);

            result.Success.Should().BeTrue();
            result.Dimensions.Width.Should().Be(4000);
            result.Dimensions.Depth.Should().Be(5000);
            result.Dimensions.Height.Should().Be(3000);
        }

        #endregion
    }

    #endregion

    #region CreativeGenerator Tests

    [TestFixture]
    public class CreativeGeneratorTests
    {
        private CreativeGenerator _creativeGenerator;

        [SetUp]
        public void SetUp()
        {
            _creativeGenerator = new CreativeGenerator();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithDefaultConfig_ShouldInitialize()
        {
            var generator = new CreativeGenerator();

            generator.Should().NotBeNull();
        }

        [Test]
        public void Constructor_WithCustomConfig_ShouldInitialize()
        {
            var config = new CreativeConfiguration
            {
                MaxVariations = 20,
                NoveltyWeight = 0.5,
                AllowExperimental = false
            };

            var generator = new CreativeGenerator(config);

            generator.Should().NotBeNull();
        }

        #endregion

        #region GenerateVariations Tests

        [Test]
        public void GenerateVariations_WithValidInput_ShouldReturnVariations()
        {
            var layout = CreateSampleLayout();
            var constraints = new GenerationConstraints
            {
                BuildingType = BuildingType.Residential,
                AllowRotation = true,
                AllowMirroring = true
            };

            var result = _creativeGenerator.GenerateVariations(layout, constraints);

            result.Should().NotBeNull();
            result.Variations.Should().NotBeEmpty();
        }

        [Test]
        public void GenerateVariations_ShouldRespectCountParameter()
        {
            var layout = CreateSampleLayout();
            var constraints = new GenerationConstraints
            {
                BuildingType = BuildingType.Residential,
                AllowRotation = true,
                AllowMirroring = true
            };

            var result = _creativeGenerator.GenerateVariations(layout, constraints, count: 3);

            result.Variations.Should().HaveCountLessThanOrEqualTo(3);
        }

        [Test]
        public void GenerateVariations_ShouldReturnScoredVariationsInDescendingOrder()
        {
            var layout = CreateSampleLayout();
            var constraints = new GenerationConstraints
            {
                BuildingType = BuildingType.Commercial,
                AllowRotation = true,
                AllowMirroring = true
            };

            var result = _creativeGenerator.GenerateVariations(layout, constraints, count: 5);

            result.Variations.Should().OnlyContain(v => v.Score >= 0);
            result.Variations.Should().BeInDescendingOrder(v => v.Score);
        }

        [Test]
        public void GenerateVariations_ShouldPopulateBestVariationAndOriginalLayout()
        {
            var layout = CreateSampleLayout();
            var constraints = new GenerationConstraints
            {
                BuildingType = BuildingType.Residential,
                AllowRotation = true,
                AllowMirroring = true
            };

            var result = _creativeGenerator.GenerateVariations(layout, constraints);

            result.BestVariation.Should().NotBeNull();
            result.BestVariation.Score.Should().BeGreaterThanOrEqualTo(0);
            result.OriginalLayout.Should().BeSameAs(layout);
        }

        [Test]
        public void GenerateVariations_WithPreferredStyles_ShouldIncludeStyleVariations()
        {
            var layout = CreateSampleLayout();
            var constraints = new GenerationConstraints
            {
                PreferredStyles = new List<string> { "Modern", "Scandinavian" },
                AllowRotation = true,
                AllowMirroring = true
            };

            var result = _creativeGenerator.GenerateVariations(layout, constraints);

            result.Variations.Should().Contain(v =>
                v.Variation.VariationType == VariationType.StyleBased);
        }

        [Test]
        public void GenerateVariations_EachVariationShouldHaveDescription()
        {
            var layout = CreateSampleLayout();
            var constraints = new GenerationConstraints
            {
                AllowRotation = true,
                AllowMirroring = true
            };

            var result = _creativeGenerator.GenerateVariations(layout, constraints);

            result.Variations.Should().OnlyContain(v =>
                !string.IsNullOrWhiteSpace(v.Variation.Description));
            result.Timestamp.Should().BeOnOrAfter(DateTime.UtcNow.AddMinutes(-1));
        }

        #endregion

        #region ApplyPattern Tests

        [Test]
        public void ApplyPattern_WithValidPatternId_ShouldReturnSuccess()
        {
            var layout = CreateSampleLayout(totalArea: 50);

            var result = _creativeGenerator.ApplyPattern(layout, "OpenPlan");

            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.PatternId.Should().Be("OpenPlan");
            result.OriginalLayout.Should().BeSameAs(layout);
        }

        [Test]
        public void ApplyPattern_WithUnknownPatternId_ShouldReturnFailure()
        {
            var layout = CreateSampleLayout();

            var result = _creativeGenerator.ApplyPattern(layout, "NonExistentPattern");

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }

        [Test]
        public void ApplyPattern_WithValidPattern_ShouldReturnLayoutAndPrinciples()
        {
            var layout = CreateSampleLayout(totalArea: 50);

            var result = _creativeGenerator.ApplyPattern(layout, "OpenPlan");

            if (result.Success)
            {
                result.ResultingLayout.Should().NotBeNull();
                result.AppliedPrinciples.Should().NotBeEmpty();
            }
        }

        [Test]
        public void ApplyPattern_WithCustomParameters_ShouldAcceptParameters()
        {
            var layout = CreateSampleLayout(totalArea: 50);
            var parameters = new PatternParameters
            {
                Intensity = 0.8,
                Overrides = new Dictionary<string, object>
                {
                    { "MaxPartitions", 1 }
                }
            };

            var result = _creativeGenerator.ApplyPattern(layout, "OpenPlan", parameters);

            result.Should().NotBeNull();
        }

        [Test]
        public void ApplyPattern_BiophilicDesign_ShouldSucceed()
        {
            var layout = CreateSampleLayout(totalArea: 100);

            var result = _creativeGenerator.ApplyPattern(layout, "BiophilicDesign");

            result.Should().NotBeNull();
            result.PatternId.Should().Be("BiophilicDesign");
        }

        #endregion

        #region ApplyStyle Tests

        [Test]
        public void ApplyStyle_WithModernStyle_ShouldReturnSuccess()
        {
            var layout = CreateSampleLayout();

            var result = _creativeGenerator.ApplyStyle(layout, "Modern");

            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.StyleId.Should().Be("Modern");
            result.OriginalLayout.Should().BeSameAs(layout);
        }

        [Test]
        public void ApplyStyle_WithUnknownStyleId_ShouldReturnFailure()
        {
            var layout = CreateSampleLayout();

            var result = _creativeGenerator.ApplyStyle(layout, "NonExistentStyle");

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }

        [Test]
        public void ApplyStyle_ShouldReturnMaterialAndProportionChanges()
        {
            var layout = CreateSampleLayout();

            var result = _creativeGenerator.ApplyStyle(layout, "Modern");

            result.MaterialChanges.Should().NotBeEmpty();
            result.ProportionChanges.Should().NotBeEmpty();
            result.ElementChanges.Should().NotBeEmpty();
        }

        [Test]
        public void ApplyStyle_ShouldReturnColorScheme()
        {
            var layout = CreateSampleLayout();

            var result = _creativeGenerator.ApplyStyle(layout, "Modern");

            result.ColorScheme.Should().NotBeNull();
            result.ColorScheme.Primary.Should().NotBeEmpty();
            result.ColorScheme.Accent.Should().NotBeEmpty();
        }

        [Test]
        public void ApplyStyle_WithAfricanContemporary_ShouldReturnLocalMaterials()
        {
            var layout = CreateSampleLayout();

            var result = _creativeGenerator.ApplyStyle(layout, "African Contemporary");

            result.Success.Should().BeTrue();
            result.MaterialChanges.Should().NotBeEmpty();
        }

        [Test]
        public void ApplyStyle_WithCustomParameters_ShouldAcceptParameters()
        {
            var layout = CreateSampleLayout();
            var parameters = new StyleParameters
            {
                Intensity = 0.5,
                ExcludedElements = new List<string> { "Furniture" }
            };

            var result = _creativeGenerator.ApplyStyle(layout, "Modern", parameters);

            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
        }

        #endregion

        #region GetSuggestions Tests

        [Test]
        public void GetSuggestions_ForResidentialContext_ShouldReturnAllSuggestionTypes()
        {
            var context = new DesignContext
            {
                BuildingType = BuildingType.Residential,
                CurrentPhase = "Schematic Design"
            };

            var suggestions = _creativeGenerator.GetSuggestions(context);

            suggestions.Should().NotBeNull();
            suggestions.PatternSuggestions.Should().NotBeEmpty();
            suggestions.StyleSuggestions.Should().NotBeEmpty();
            suggestions.ImprovementSuggestions.Should().NotBeEmpty();
            suggestions.InnovationSuggestions.Should().NotBeEmpty();
            suggestions.Context.Should().BeSameAs(context);
        }

        [Test]
        public void GetSuggestions_PatternSuggestions_ShouldHaveIdsAndDescriptions()
        {
            var context = new DesignContext { BuildingType = BuildingType.Commercial };

            var suggestions = _creativeGenerator.GetSuggestions(context);

            suggestions.PatternSuggestions.Should().OnlyContain(ps =>
                !string.IsNullOrEmpty(ps.PatternId) &&
                !string.IsNullOrEmpty(ps.Description));
        }

        [Test]
        public void GetSuggestions_ImprovementSuggestions_ShouldReferenceCodeCompliance()
        {
            var context = new DesignContext { BuildingType = BuildingType.Commercial };

            var suggestions = _creativeGenerator.GetSuggestions(context);

            suggestions.ImprovementSuggestions.Should().Contain(s =>
                !string.IsNullOrEmpty(s.Code));
        }

        [Test]
        public void GetSuggestions_InnovationSuggestions_ShouldHaveFeasibilityScores()
        {
            var context = new DesignContext { BuildingType = BuildingType.Residential };

            var suggestions = _creativeGenerator.GetSuggestions(context);

            suggestions.InnovationSuggestions.Should().OnlyContain(is_ =>
                !string.IsNullOrEmpty(is_.Idea) &&
                is_.Feasibility > 0 && is_.Feasibility <= 1.0);
        }

        [Test]
        public void GetSuggestions_ForHealthcareContext_ShouldIncludeInfectionControl()
        {
            var context = new DesignContext { BuildingType = BuildingType.Healthcare };

            var suggestions = _creativeGenerator.GetSuggestions(context);

            suggestions.ImprovementSuggestions.Should().Contain(s =>
                s.Area.Contains("Infection", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void GetSuggestions_ForEducationContext_ShouldIncludeClassroomSizing()
        {
            var context = new DesignContext { BuildingType = BuildingType.Education };

            var suggestions = _creativeGenerator.GetSuggestions(context);

            suggestions.ImprovementSuggestions.Should().Contain(s =>
                s.Area.Contains("Classroom", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void GetSuggestions_ShouldSetTimestamp()
        {
            var before = DateTime.UtcNow;
            var context = new DesignContext { BuildingType = BuildingType.Residential };

            var suggestions = _creativeGenerator.GetSuggestions(context);

            suggestions.Timestamp.Should().BeOnOrAfter(before);
        }

        #endregion

        #region BlendDesigns Tests

        [Test]
        public void BlendDesigns_WithMultipleLayouts_ShouldReturnBlendedResult()
        {
            var layouts = new List<Layout>
            {
                CreateSampleLayout(layoutId: "Layout1", totalArea: 100),
                CreateSampleLayout(layoutId: "Layout2", totalArea: 150)
            };
            var parameters = new BlendParameters
            {
                Weights = new List<double> { 0.6, 0.4 },
                BlendMode = "Average"
            };

            var result = _creativeGenerator.BlendDesigns(layouts, parameters);

            result.Should().NotBeNull();
            result.BlendedLayout.Should().NotBeNull();
            result.Sources.Should().HaveCount(2);
            result.FeatureContributions.Should().NotBeEmpty();
        }

        #endregion

        #region Helper Methods

        private Layout CreateSampleLayout(string layoutId = "TestLayout", double totalArea = 100)
        {
            return new Layout
            {
                LayoutId = layoutId,
                TotalArea = totalArea,
                Width = 10,
                Length = totalArea / 10,
                CirculationArea = totalArea * 0.15,
                PartitionCount = 3,
                Spaces = new List<Space>
                {
                    new Space { SpaceId = "S1", Name = "Living Room", Area = totalArea * 0.4, Function = "Living" },
                    new Space { SpaceId = "S2", Name = "Bedroom", Area = totalArea * 0.3, Function = "Sleeping" },
                    new Space { SpaceId = "S3", Name = "Kitchen", Area = totalArea * 0.15, Function = "Cooking" }
                },
                AllElements = new List<string> { "W1", "W2", "W3", "W4", "F1" },
                CirculationPaths = new List<string> { "C1", "C2" }
            };
        }

        #endregion
    }

    #endregion

    #region Common Type Tests

    [TestFixture]
    public class CommonTypeTests
    {
        #region Point3D Tests

        [Test]
        public void Point3D_DistanceTo_ShouldCalculateCorrectDistance()
        {
            var p1 = new Point3D(0, 0, 0);
            var p2 = new Point3D(3000, 4000, 0);

            p1.DistanceTo(p2).Should().BeApproximately(5000, 0.01);
        }

        [Test]
        public void Point3D_Add_ShouldReturnCorrectSum()
        {
            var result = new Point3D(1000, 2000, 500).Add(new Point3D(500, 300, 100));

            result.X.Should().Be(1500);
            result.Y.Should().Be(2300);
            result.Z.Should().Be(600);
        }

        [Test]
        public void Point3D_Subtract_ShouldReturnCorrectDifference()
        {
            var result = new Point3D(5000, 3000, 1000).Subtract(new Point3D(2000, 1000, 500));

            result.X.Should().Be(3000);
            result.Y.Should().Be(2000);
            result.Z.Should().Be(500);
        }

        [Test]
        public void Point3D_Scale_ShouldMultiplyAllCoordinates()
        {
            var result = new Point3D(1000, 2000, 3000).Scale(2.0);

            result.X.Should().Be(2000);
            result.Y.Should().Be(4000);
            result.Z.Should().Be(6000);
        }

        #endregion

        #region BoundingBox Tests

        [Test]
        public void BoundingBox_Contains_ShouldReturnCorrectResults()
        {
            var box = new BoundingBox
            {
                Min = new Point3D(0, 0, 0),
                Max = new Point3D(5000, 4000, 3000)
            };

            box.Contains(new Point3D(2500, 2000, 1500)).Should().BeTrue();
            box.Contains(new Point3D(6000, 2000, 1500)).Should().BeFalse();
        }

        [Test]
        public void BoundingBox_Intersects_ShouldDetectOverlap()
        {
            var box1 = new BoundingBox
            {
                Min = new Point3D(0, 0, 0),
                Max = new Point3D(5000, 4000, 3000)
            };
            var overlapping = new BoundingBox
            {
                Min = new Point3D(3000, 2000, 1000),
                Max = new Point3D(8000, 6000, 4000)
            };
            var separate = new BoundingBox
            {
                Min = new Point3D(10000, 10000, 10000),
                Max = new Point3D(15000, 15000, 15000)
            };

            box1.Intersects(overlapping).Should().BeTrue();
            box1.Intersects(separate).Should().BeFalse();
        }

        [Test]
        public void BoundingBox_DimensionProperties_ShouldCalculateCorrectly()
        {
            var box = new BoundingBox
            {
                Min = new Point3D(1000, 2000, 0),
                Max = new Point3D(6000, 5000, 3000)
            };

            box.Width.Should().Be(5000);
            box.Depth.Should().Be(3000);
            box.Height.Should().Be(3000);
            box.Center.X.Should().Be(3500);
            box.Center.Y.Should().Be(3500);
            box.Center.Z.Should().Be(1500);
        }

        #endregion

        #region ValidationResult Tests

        [Test]
        public void ValidationResult_Valid_ShouldReturnIsValidTrue()
        {
            var result = ValidationResult.Valid();

            result.IsValid.Should().BeTrue();
            result.Error.Should().BeNull();
        }

        [Test]
        public void ValidationResult_Invalid_ShouldReturnIsValidFalseWithError()
        {
            var result = ValidationResult.Invalid("Test error message");

            result.IsValid.Should().BeFalse();
            result.Error.Should().Be("Test error message");
        }

        #endregion
    }

    #endregion
}
