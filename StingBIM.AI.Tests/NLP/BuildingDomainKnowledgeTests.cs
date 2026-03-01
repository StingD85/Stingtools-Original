using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using StingBIM.AI.NLP.Domain;

namespace StingBIM.AI.Tests.NLP
{
    /// <summary>
    /// Unit tests for BuildingDomainKnowledge class.
    /// Tests intent classification, entity extraction, and domain resolution.
    /// </summary>
    [TestFixture]
    public class BuildingDomainKnowledgeTests
    {
        #region GetIntents Tests

        [Test]
        public void GetIntents_ShouldReturnNonEmptyList()
        {
            // Act
            var intents = BuildingDomainKnowledge.GetIntents();

            // Assert
            intents.Should().NotBeEmpty();
            intents.Count.Should().BeGreaterThan(10);
        }

        [Test]
        public void GetIntents_ShouldIncludeCreationIntents()
        {
            // Act
            var intents = BuildingDomainKnowledge.GetIntents();
            var creationIntents = intents.Where(i => i.Category == IntentCategory.Creation).ToList();

            // Assert
            creationIntents.Should().Contain(i => i.Name == "CreateWall");
            creationIntents.Should().Contain(i => i.Name == "CreateRoom");
            creationIntents.Should().Contain(i => i.Name == "CreateDoor");
            creationIntents.Should().Contain(i => i.Name == "CreateWindow");
        }

        [Test]
        public void GetIntents_ShouldIncludeModificationIntents()
        {
            // Act
            var intents = BuildingDomainKnowledge.GetIntents();
            var modificationIntents = intents.Where(i => i.Category == IntentCategory.Modification).ToList();

            // Assert
            modificationIntents.Should().Contain(i => i.Name == "MoveElement");
            modificationIntents.Should().Contain(i => i.Name == "ResizeElement");
            modificationIntents.Should().Contain(i => i.Name == "DeleteElement");
        }

        [Test]
        public void GetIntents_AllIntentsShouldHaveExamples()
        {
            // Act
            var intents = BuildingDomainKnowledge.GetIntents();

            // Assert
            intents.Should().OnlyContain(i => i.Examples != null && i.Examples.Length > 0);
        }

        [Test]
        public void GetIntents_AllIntentsShouldHaveKeywords()
        {
            // Act
            var intents = BuildingDomainKnowledge.GetIntents();

            // Assert
            intents.Should().OnlyContain(i => i.Keywords != null && i.Keywords.Length > 0);
        }

        #endregion

        #region GetRoomTypes Tests

        [Test]
        public void GetRoomTypes_ShouldReturnNonEmptyDictionary()
        {
            // Act
            var roomTypes = BuildingDomainKnowledge.GetRoomTypes();

            // Assert
            roomTypes.Should().NotBeEmpty();
            roomTypes.Count.Should().BeGreaterOrEqualTo(10);
        }

        [Test]
        public void GetRoomTypes_ShouldIncludeCommonRoomTypes()
        {
            // Act
            var roomTypes = BuildingDomainKnowledge.GetRoomTypes();

            // Assert
            roomTypes.Should().ContainKey("bedroom");
            roomTypes.Should().ContainKey("bathroom");
            roomTypes.Should().ContainKey("kitchen");
            roomTypes.Should().ContainKey("living");
            roomTypes.Should().ContainKey("office");
        }

        [Test]
        public void GetRoomTypes_BedroomShouldHaveCorrectProperties()
        {
            // Act
            var roomTypes = BuildingDomainKnowledge.GetRoomTypes();
            var bedroom = roomTypes["bedroom"];

            // Assert
            bedroom.CanonicalName.Should().Be("Bedroom");
            bedroom.RequiresWindow.Should().BeTrue();
            bedroom.RequiresDoor.Should().BeTrue();
            bedroom.MinArea.Should().BeGreaterThan(0);
            bedroom.DefaultArea.Should().BeGreaterThan(bedroom.MinArea);
        }

        [Test]
        public void GetRoomTypes_BathroomShouldRequirePlumbing()
        {
            // Act
            var roomTypes = BuildingDomainKnowledge.GetRoomTypes();
            var bathroom = roomTypes["bathroom"];

            // Assert
            bathroom.RequiresPlumbing.Should().BeTrue();
            bathroom.RequiresVentilation.Should().BeTrue();
        }

        [Test]
        public void GetRoomTypes_CorridorShouldHaveMinimumWidth()
        {
            // Act
            var roomTypes = BuildingDomainKnowledge.GetRoomTypes();
            var corridor = roomTypes["corridor"];

            // Assert
            corridor.MinWidth.Should().BeGreaterThan(0);
            corridor.RequiresWindow.Should().BeFalse();
        }

        #endregion

        #region GetMaterials Tests

        [Test]
        public void GetMaterials_ShouldReturnNonEmptyDictionary()
        {
            // Act
            var materials = BuildingDomainKnowledge.GetMaterials();

            // Assert
            materials.Should().NotBeEmpty();
            materials.Count.Should().BeGreaterOrEqualTo(5);
        }

        [Test]
        public void GetMaterials_ShouldIncludeStructuralMaterials()
        {
            // Act
            var materials = BuildingDomainKnowledge.GetMaterials();

            // Assert
            materials.Should().ContainKey("concrete");
            materials.Should().ContainKey("steel");
            materials.Should().ContainKey("wood");
            materials.Should().ContainKey("brick");
        }

        [Test]
        public void GetMaterials_ConcreteShouldBeStructural()
        {
            // Act
            var materials = BuildingDomainKnowledge.GetMaterials();
            var concrete = materials["concrete"];

            // Assert
            concrete.IsStructural.Should().BeTrue();
            concrete.Category.Should().Be(MaterialCategory.Structural);
            concrete.Synonyms.Should().Contain("cement");
        }

        [Test]
        public void GetMaterials_DrywallShouldBeFinish()
        {
            // Act
            var materials = BuildingDomainKnowledge.GetMaterials();
            var drywall = materials["drywall"];

            // Assert
            drywall.IsStructural.Should().BeFalse();
            drywall.Category.Should().Be(MaterialCategory.Finish);
        }

        #endregion

        #region ResolveRoomType Tests

        [Test]
        public void ResolveRoomType_DirectMatch_ShouldResolve()
        {
            // Act
            var result = BuildingDomainKnowledge.ResolveRoomType("bedroom");

            // Assert
            result.Should().NotBeNull();
            result.CanonicalName.Should().Be("Bedroom");
        }

        [Test]
        public void ResolveRoomType_CaseInsensitive_ShouldResolve()
        {
            // Act
            var result = BuildingDomainKnowledge.ResolveRoomType("BEDROOM");

            // Assert
            result.Should().NotBeNull();
            result.CanonicalName.Should().Be("Bedroom");
        }

        [Test]
        public void ResolveRoomType_Synonym_ShouldResolve()
        {
            // Act
            var result = BuildingDomainKnowledge.ResolveRoomType("toilet");

            // Assert
            result.Should().NotBeNull();
            result.CanonicalName.Should().Be("Bathroom");
        }

        [Test]
        public void ResolveRoomType_WC_ShouldResolveToBathroom()
        {
            // Act
            var result = BuildingDomainKnowledge.ResolveRoomType("wc");

            // Assert
            result.Should().NotBeNull();
            result.CanonicalName.Should().Be("Bathroom");
        }

        [Test]
        public void ResolveRoomType_Lounge_ShouldResolveToLivingRoom()
        {
            // Act
            var result = BuildingDomainKnowledge.ResolveRoomType("lounge");

            // Assert
            result.Should().NotBeNull();
            result.CanonicalName.Should().Be("Living Room");
        }

        [Test]
        public void ResolveRoomType_Study_ShouldResolveToOffice()
        {
            // Act
            var result = BuildingDomainKnowledge.ResolveRoomType("study");

            // Assert
            result.Should().NotBeNull();
            result.CanonicalName.Should().Be("Office");
        }

        [Test]
        public void ResolveRoomType_Unknown_ShouldReturnNull()
        {
            // Act
            var result = BuildingDomainKnowledge.ResolveRoomType("unknown_room_type");

            // Assert
            result.Should().BeNull();
        }

        [Test]
        public void ResolveRoomType_PartialMatch_ShouldResolve()
        {
            // Act
            var result = BuildingDomainKnowledge.ResolveRoomType("master bedroom");

            // Assert
            result.Should().NotBeNull();
        }

        #endregion

        #region ResolveMaterial Tests

        [Test]
        public void ResolveMaterial_DirectMatch_ShouldResolve()
        {
            // Act
            var result = BuildingDomainKnowledge.ResolveMaterial("concrete");

            // Assert
            result.Should().NotBeNull();
            result.CanonicalName.Should().Be("Concrete");
        }

        [Test]
        public void ResolveMaterial_CaseInsensitive_ShouldResolve()
        {
            // Act
            var result = BuildingDomainKnowledge.ResolveMaterial("STEEL");

            // Assert
            result.Should().NotBeNull();
            result.CanonicalName.Should().Be("Steel");
        }

        [Test]
        public void ResolveMaterial_Synonym_ShouldResolve()
        {
            // Act
            var result = BuildingDomainKnowledge.ResolveMaterial("timber");

            // Assert
            result.Should().NotBeNull();
            result.CanonicalName.Should().Be("Wood");
        }

        [Test]
        public void ResolveMaterial_PlasterBoard_ShouldResolveToDrywall()
        {
            // Act
            var result = BuildingDomainKnowledge.ResolveMaterial("plasterboard");

            // Assert
            result.Should().NotBeNull();
            result.CanonicalName.Should().Be("Drywall");
        }

        [Test]
        public void ResolveMaterial_Unknown_ShouldReturnNull()
        {
            // Act
            var result = BuildingDomainKnowledge.ResolveMaterial("unknown_material");

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region ClassifyIntent Tests

        [Test]
        public void ClassifyIntent_CreateWall_ShouldClassifyCorrectly()
        {
            // Act
            var result = BuildingDomainKnowledge.ClassifyIntent("create a wall");

            // Assert
            result.Should().NotBeNull();
            result.Intent.Should().NotBeNull();
            result.Intent.Name.Should().Be("CreateWall");
            result.Confidence.Should().BeGreaterThan(0.5f);
        }

        [Test]
        public void ClassifyIntent_MakeWall_ShouldClassifyAsCreateWall()
        {
            // Act
            var result = BuildingDomainKnowledge.ClassifyIntent("make a wall 4 meters long");

            // Assert
            result.Intent.Name.Should().Be("CreateWall");
            result.Confidence.Should().BeGreaterThan(0.5f);
        }

        [Test]
        public void ClassifyIntent_AddDoor_ShouldClassifyCorrectly()
        {
            // Act
            var result = BuildingDomainKnowledge.ClassifyIntent("add a door here");

            // Assert
            result.Intent.Name.Should().Be("CreateDoor");
        }

        [Test]
        public void ClassifyIntent_CreateRoom_ShouldClassifyCorrectly()
        {
            // Act
            var result = BuildingDomainKnowledge.ClassifyIntent("create a bedroom");

            // Assert
            result.Intent.Name.Should().Be("CreateRoom");
        }

        [Test]
        public void ClassifyIntent_MoveElement_ShouldClassifyCorrectly()
        {
            // Act
            var result = BuildingDomainKnowledge.ClassifyIntent("move the wall 2 meters north");

            // Assert
            result.Intent.Name.Should().Be("MoveElement");
            result.Intent.Category.Should().Be(IntentCategory.Modification);
        }

        [Test]
        public void ClassifyIntent_DeleteWall_ShouldClassifyCorrectly()
        {
            // Act
            var result = BuildingDomainKnowledge.ClassifyIntent("delete the wall");

            // Assert
            result.Intent.Name.Should().Be("DeleteElement");
        }

        [Test]
        public void ClassifyIntent_SelectAll_ShouldClassifyCorrectly()
        {
            // Act
            var result = BuildingDomainKnowledge.ClassifyIntent("select all doors");

            // Assert
            result.Intent.Name.Should().Be("SelectElement");
            result.Intent.Category.Should().Be(IntentCategory.Selection);
        }

        [Test]
        public void ClassifyIntent_ZoomIn_ShouldClassifyCorrectly()
        {
            // Act
            var result = BuildingDomainKnowledge.ClassifyIntent("zoom in");

            // Assert
            result.Intent.Name.Should().Be("ZoomIn");
            result.Intent.Category.Should().Be(IntentCategory.View);
        }

        [Test]
        public void ClassifyIntent_Undo_ShouldClassifyCorrectly()
        {
            // Act
            var result = BuildingDomainKnowledge.ClassifyIntent("undo");

            // Assert
            result.Intent.Name.Should().Be("Undo");
            result.Intent.Category.Should().Be(IntentCategory.Utility);
        }

        [Test]
        public void ClassifyIntent_Help_ShouldClassifyCorrectly()
        {
            // Act
            var result = BuildingDomainKnowledge.ClassifyIntent("help me");

            // Assert
            result.Intent.Name.Should().Be("Help");
        }

        [Test]
        public void ClassifyIntent_CheckCompliance_ShouldClassifyCorrectly()
        {
            // Act
            var result = BuildingDomainKnowledge.ClassifyIntent("check building code compliance");

            // Assert
            result.Intent.Name.Should().Be("CheckCompliance");
            result.Intent.Category.Should().Be(IntentCategory.Analysis);
        }

        [Test]
        public void ClassifyIntent_ShouldReturnMatchedKeywords()
        {
            // Act
            var result = BuildingDomainKnowledge.ClassifyIntent("create a wall here");

            // Assert
            result.MatchedKeywords.Should().NotBeEmpty();
            result.MatchedKeywords.Should().Contain("wall");
        }

        #endregion

        #region ExtractDomainEntities Tests

        [Test]
        public void ExtractDomainEntities_WallMention_ShouldExtractElementType()
        {
            // Act
            var entities = BuildingDomainKnowledge.ExtractDomainEntities("create a wall here");

            // Assert
            entities.Should().Contain(e => e.Type == DomainEntityType.ElementType && e.Value == "wall");
        }

        [Test]
        public void ExtractDomainEntities_RoomTypeMention_ShouldExtractRoomType()
        {
            // Act
            var entities = BuildingDomainKnowledge.ExtractDomainEntities("create a bedroom here");

            // Assert
            entities.Should().Contain(e => e.Type == DomainEntityType.RoomType && e.Value == "bedroom");
        }

        [Test]
        public void ExtractDomainEntities_MaterialMention_ShouldExtractMaterial()
        {
            // Act
            var entities = BuildingDomainKnowledge.ExtractDomainEntities("make it concrete");

            // Assert
            entities.Should().Contain(e => e.Type == DomainEntityType.Material && e.Value == "concrete");
        }

        [Test]
        public void ExtractDomainEntities_DirectionMention_ShouldExtractDirection()
        {
            // Act
            var entities = BuildingDomainKnowledge.ExtractDomainEntities("move it 2 meters north");

            // Assert
            entities.Should().Contain(e => e.Type == DomainEntityType.Direction && e.Value == "north");
        }

        [Test]
        public void ExtractDomainEntities_MultipleDirections_ShouldExtractAll()
        {
            // Act
            var entities = BuildingDomainKnowledge.ExtractDomainEntities("move north then east");

            // Assert
            var directions = entities.Where(e => e.Type == DomainEntityType.Direction).ToList();
            directions.Should().HaveCount(2);
            directions.Should().Contain(e => e.Value == "north");
            directions.Should().Contain(e => e.Value == "east");
        }

        [Test]
        public void ExtractDomainEntities_RoomTypeSynonym_ShouldExtractWithCanonicalName()
        {
            // Act
            var entities = BuildingDomainKnowledge.ExtractDomainEntities("create a toilet here");

            // Assert
            var roomEntity = entities.FirstOrDefault(e => e.Type == DomainEntityType.RoomType);
            roomEntity.Should().NotBeNull();
            roomEntity.NormalizedValue.Should().Be("Bathroom");
        }

        [Test]
        public void ExtractDomainEntities_ElementTypeNormalization_ShouldCapitalize()
        {
            // Act
            var entities = BuildingDomainKnowledge.ExtractDomainEntities("add a door");

            // Assert
            var doorEntity = entities.FirstOrDefault(e => e.Type == DomainEntityType.ElementType);
            doorEntity.Should().NotBeNull();
            doorEntity.NormalizedValue.Should().Be("Door");
        }

        [Test]
        public void ExtractDomainEntities_RoomTypeMetadata_ShouldIncludeDefaultArea()
        {
            // Act
            var entities = BuildingDomainKnowledge.ExtractDomainEntities("create a bedroom");

            // Assert
            var bedroomEntity = entities.FirstOrDefault(e => e.Type == DomainEntityType.RoomType);
            bedroomEntity.Should().NotBeNull();
            bedroomEntity.Metadata.Should().ContainKey("DefaultArea");
            bedroomEntity.Metadata.Should().ContainKey("MinArea");
        }

        [Test]
        public void ExtractDomainEntities_MaterialMetadata_ShouldIncludeStructuralFlag()
        {
            // Act
            var entities = BuildingDomainKnowledge.ExtractDomainEntities("use steel");

            // Assert
            var steelEntity = entities.FirstOrDefault(e => e.Type == DomainEntityType.Material);
            steelEntity.Should().NotBeNull();
            steelEntity.Metadata.Should().ContainKey("IsStructural");
            steelEntity.Metadata["IsStructural"].Should().Be(true);
        }

        [Test]
        public void ExtractDomainEntities_ConfidenceScores_ShouldBeReasonable()
        {
            // Act
            var entities = BuildingDomainKnowledge.ExtractDomainEntities("create a concrete wall");

            // Assert
            entities.Should().OnlyContain(e => e.Confidence >= 0.8f && e.Confidence <= 1.0f);
        }

        [Test]
        public void ExtractDomainEntities_NoEntities_ShouldReturnEmptyList()
        {
            // Act
            var entities = BuildingDomainKnowledge.ExtractDomainEntities("hello world");

            // Assert
            entities.Should().BeEmpty();
        }

        #endregion

        #region Real-World Command Tests

        [Test]
        public void RealWorldCommand_CreateBedroomWithDimensions()
        {
            // Arrange
            var command = "create a 4x5 meter bedroom with a window on the north wall";

            // Act
            var intent = BuildingDomainKnowledge.ClassifyIntent(command);
            var entities = BuildingDomainKnowledge.ExtractDomainEntities(command);

            // Assert
            intent.Intent.Name.Should().Be("CreateRoom");
            entities.Should().Contain(e => e.Type == DomainEntityType.RoomType);
            entities.Should().Contain(e => e.Type == DomainEntityType.ElementType && e.Value == "window");
            entities.Should().Contain(e => e.Type == DomainEntityType.Direction && e.Value == "north");
        }

        [Test]
        public void RealWorldCommand_ModifyWallMaterial()
        {
            // Arrange
            var command = "change the wall material to concrete";

            // Act
            var intent = BuildingDomainKnowledge.ClassifyIntent(command);
            var entities = BuildingDomainKnowledge.ExtractDomainEntities(command);

            // Assert
            intent.Intent.Name.Should().Be("ChangeProperty");
            entities.Should().Contain(e => e.Type == DomainEntityType.ElementType && e.Value == "wall");
            entities.Should().Contain(e => e.Type == DomainEntityType.Material && e.Value == "concrete");
        }

        [Test]
        public void RealWorldCommand_MoveElementWithDirection()
        {
            // Arrange
            var command = "move the door 500mm to the left";

            // Act
            var intent = BuildingDomainKnowledge.ClassifyIntent(command);
            var entities = BuildingDomainKnowledge.ExtractDomainEntities(command);

            // Assert
            intent.Intent.Name.Should().Be("MoveElement");
            entities.Should().Contain(e => e.Type == DomainEntityType.ElementType && e.Value == "door");
            entities.Should().Contain(e => e.Type == DomainEntityType.Direction && e.Value == "left");
        }

        [Test]
        public void RealWorldCommand_QueryRoomArea()
        {
            // Arrange
            var command = "what is the area of this room";

            // Act
            var intent = BuildingDomainKnowledge.ClassifyIntent(command);

            // Assert
            intent.Intent.Name.Should().Be("GetDimension");
            intent.Intent.Category.Should().Be(IntentCategory.Query);
        }

        [Test]
        public void RealWorldCommand_CreateKitchenWithRequirements()
        {
            // Arrange
            var command = "add a kitchen near the dining room";

            // Act
            var intent = BuildingDomainKnowledge.ClassifyIntent(command);
            var entities = BuildingDomainKnowledge.ExtractDomainEntities(command);

            // Assert
            intent.Intent.Name.Should().Be("CreateRoom");

            var kitchenEntity = entities.FirstOrDefault(e => e.Type == DomainEntityType.RoomType && e.Value == "kitchen");
            kitchenEntity.Should().NotBeNull();

            // Verify kitchen metadata
            var kitchenInfo = BuildingDomainKnowledge.ResolveRoomType("kitchen");
            kitchenInfo.RequiresPlumbing.Should().BeTrue();
            kitchenInfo.RequiresVentilation.Should().BeTrue();
            kitchenInfo.AdjacentPreferred.Should().Contain("dining");
        }

        #endregion
    }
}
