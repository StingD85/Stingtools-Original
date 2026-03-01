// StingBIM.AI.NLP.Domain.BuildingDomainKnowledge
// Comprehensive domain knowledge for building design NLP
// Master Proposal Reference: Part 2.1 Pillar 4 - Domain Intelligence

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using StingBIM.AI.NLP.Pipeline;

namespace StingBIM.AI.NLP.Domain
{
    /// <summary>
    /// Comprehensive domain knowledge for building design.
    /// Provides intent patterns, entity definitions, and semantic understanding.
    /// </summary>
    public class BuildingDomainKnowledge
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Intent definitions with examples and required entities
        private static readonly List<DomainIntent> DomainIntents = new()
        {
            // ===== CREATION INTENTS =====
            new DomainIntent
            {
                Name = "CreateWall",
                Category = IntentCategory.Creation,
                Examples = new[]
                {
                    "create a wall", "make a wall", "add wall", "draw wall",
                    "create a 4 meter wall", "make a wall 3m long",
                    "add a wall from here to there", "draw a wall along the north",
                    "i need a wall", "put a wall here", "build a wall"
                },
                RequiredSlots = new[] { SlotType.ElementType },
                OptionalSlots = new[] { SlotType.Dimension, SlotType.Direction, SlotType.Position, SlotType.Material },
                Keywords = new[] { "wall", "partition", "boundary" },
                Patterns = new[]
                {
                    @"\b(create|make|add|draw|build|put)\b.*\bwall\b",
                    @"\bwall\b.*\b(here|there|from|along)\b",
                    @"\b(\d+(?:\.\d+)?)\s*(m|meters?|mm|ft|feet)\b.*\bwall\b"
                }
            },
            new DomainIntent
            {
                Name = "CreateRoom",
                Category = IntentCategory.Creation,
                Examples = new[]
                {
                    "create a room", "make a bedroom", "add a kitchen",
                    "create a 4x5 meter room", "make a living room 20 square meters",
                    "i need a bathroom here", "add an office", "build a dining room"
                },
                RequiredSlots = new[] { SlotType.ElementType },
                OptionalSlots = new[] { SlotType.RoomType, SlotType.Dimension, SlotType.Position },
                Keywords = new[] { "room", "space", "area", "bedroom", "bathroom", "kitchen", "living", "office", "dining" },
                Patterns = new[]
                {
                    @"\b(create|make|add|build)\b.*\b(room|bedroom|bathroom|kitchen|living|office|dining|study)\b",
                    @"\b(\d+)\s*(?:x|by)\s*(\d+)\b.*\b(room|bedroom|bathroom)\b",
                    @"\b(bedroom|bathroom|kitchen|living room|office)\b.*\b(here|there)\b"
                }
            },
            new DomainIntent
            {
                Name = "CreateDoor",
                Category = IntentCategory.Creation,
                Examples = new[]
                {
                    "add a door", "create a door", "put a door in the wall",
                    "add a 0.9m door", "create a double door", "i need a door here"
                },
                RequiredSlots = new[] { SlotType.ElementType },
                OptionalSlots = new[] { SlotType.Dimension, SlotType.DoorType, SlotType.Position },
                Keywords = new[] { "door", "entrance", "entry", "doorway" },
                Patterns = new[]
                {
                    @"\b(add|create|put|make)\b.*\bdoor\b",
                    @"\bdoor\b.*\b(in|on|to)\b.*\bwall\b"
                }
            },
            new DomainIntent
            {
                Name = "CreateWindow",
                Category = IntentCategory.Creation,
                Examples = new[]
                {
                    "add a window", "create a window", "put a window here",
                    "add a 1.2m window", "create windows on the south wall"
                },
                RequiredSlots = new[] { SlotType.ElementType },
                OptionalSlots = new[] { SlotType.Dimension, SlotType.WindowType, SlotType.Position, SlotType.Direction },
                Keywords = new[] { "window", "glazing", "opening" },
                Patterns = new[]
                {
                    @"\b(add|create|put|make)\b.*\bwindow\b",
                    @"\bwindow\b.*\b(on|in)\b.*\bwall\b"
                }
            },
            new DomainIntent
            {
                Name = "CreateFloor",
                Category = IntentCategory.Creation,
                Examples = new[]
                {
                    "create a floor", "add a floor slab", "make a floor",
                    "add flooring", "create the floor for this room"
                },
                RequiredSlots = new[] { SlotType.ElementType },
                OptionalSlots = new[] { SlotType.Material, SlotType.Position },
                Keywords = new[] { "floor", "slab", "flooring" },
                Patterns = new[]
                {
                    @"\b(create|add|make)\b.*\b(floor|slab)\b"
                }
            },
            new DomainIntent
            {
                Name = "CreateColumn",
                Category = IntentCategory.Creation,
                Examples = new[]
                {
                    "add a column", "create a column", "put a column here",
                    "add a 300mm column", "create structural columns"
                },
                RequiredSlots = new[] { SlotType.ElementType },
                OptionalSlots = new[] { SlotType.Dimension, SlotType.Position, SlotType.Material },
                Keywords = new[] { "column", "pillar", "post" },
                Patterns = new[]
                {
                    @"\b(add|create|put|make)\b.*\bcolumn\b"
                }
            },
            new DomainIntent
            {
                Name = "CreateStair",
                Category = IntentCategory.Creation,
                Examples = new[]
                {
                    "add stairs", "create a staircase", "make stairs",
                    "add a stair from level 0 to level 1"
                },
                RequiredSlots = new[] { SlotType.ElementType },
                OptionalSlots = new[] { SlotType.Level, SlotType.Position },
                Keywords = new[] { "stair", "stairs", "staircase", "steps" },
                Patterns = new[]
                {
                    @"\b(add|create|make)\b.*\b(stair|stairs|staircase)\b"
                }
            },

            // ===== MODIFICATION INTENTS =====
            new DomainIntent
            {
                Name = "MoveElement",
                Category = IntentCategory.Modification,
                Examples = new[]
                {
                    "move the wall", "move it 2 meters north", "shift the door left",
                    "relocate the window", "move this element", "drag it here"
                },
                RequiredSlots = new[] { SlotType.Reference },
                OptionalSlots = new[] { SlotType.Direction, SlotType.Dimension, SlotType.Position },
                Keywords = new[] { "move", "shift", "relocate", "drag", "reposition" },
                Patterns = new[]
                {
                    @"\b(move|shift|relocate|drag)\b.*\b(wall|door|window|element|it|this)\b",
                    @"\b(move|shift)\b.*\b(\d+)\b.*\b(north|south|east|west|up|down|left|right)\b"
                }
            },
            new DomainIntent
            {
                Name = "ResizeElement",
                Category = IntentCategory.Modification,
                Examples = new[]
                {
                    "make the wall longer", "resize the room", "extend the wall by 2m",
                    "make it bigger", "shrink the window", "increase the height"
                },
                RequiredSlots = new[] { SlotType.Reference },
                OptionalSlots = new[] { SlotType.Dimension, SlotType.Direction },
                Keywords = new[] { "resize", "extend", "shrink", "enlarge", "longer", "shorter", "bigger", "smaller" },
                Patterns = new[]
                {
                    @"\b(resize|extend|shrink|enlarge)\b.*\b(wall|room|window|door)\b",
                    @"\bmake\b.*\b(bigger|smaller|longer|shorter|wider|narrower)\b"
                }
            },
            new DomainIntent
            {
                Name = "RotateElement",
                Category = IntentCategory.Modification,
                Examples = new[]
                {
                    "rotate the element", "turn it 90 degrees", "rotate the door",
                    "flip it around", "rotate clockwise"
                },
                RequiredSlots = new[] { SlotType.Reference },
                OptionalSlots = new[] { SlotType.Angle },
                Keywords = new[] { "rotate", "turn", "flip", "spin" },
                Patterns = new[]
                {
                    @"\b(rotate|turn|flip)\b.*\b(element|door|window|it|this)\b",
                    @"\b(rotate|turn)\b.*\b(\d+)\b.*\b(degrees?|°)\b"
                }
            },
            new DomainIntent
            {
                Name = "CopyElement",
                Category = IntentCategory.Modification,
                Examples = new[]
                {
                    "copy the wall", "duplicate this", "make a copy",
                    "copy it 3 times", "clone the element"
                },
                RequiredSlots = new[] { SlotType.Reference },
                OptionalSlots = new[] { SlotType.Count, SlotType.Position },
                Keywords = new[] { "copy", "duplicate", "clone", "replicate" },
                Patterns = new[]
                {
                    @"\b(copy|duplicate|clone)\b.*\b(wall|door|window|element|it|this)\b"
                }
            },
            new DomainIntent
            {
                Name = "DeleteElement",
                Category = IntentCategory.Modification,
                Examples = new[]
                {
                    "delete the wall", "remove this", "delete selected",
                    "erase the window", "get rid of this"
                },
                RequiredSlots = new[] { SlotType.Reference },
                Keywords = new[] { "delete", "remove", "erase", "get rid of" },
                Patterns = new[]
                {
                    @"\b(delete|remove|erase)\b.*\b(wall|door|window|element|it|this|selected)\b"
                }
            },
            new DomainIntent
            {
                Name = "ChangeProperty",
                Category = IntentCategory.Modification,
                Examples = new[]
                {
                    "change the height to 3m", "set the width to 4 meters",
                    "change the material to brick", "make it concrete",
                    "set the room type to bedroom"
                },
                RequiredSlots = new[] { SlotType.PropertyName, SlotType.PropertyValue },
                OptionalSlots = new[] { SlotType.Reference },
                Keywords = new[] { "change", "set", "modify", "update" },
                Patterns = new[]
                {
                    @"\b(change|set|modify)\b.*\b(height|width|length|material|type)\b.*\bto\b",
                    @"\bmake\s+it\b.*\b(concrete|brick|steel|wood|glass)\b"
                }
            },

            // ===== QUERY INTENTS =====
            new DomainIntent
            {
                Name = "GetDimension",
                Category = IntentCategory.Query,
                Examples = new[]
                {
                    "what is the height", "how wide is this wall",
                    "what are the dimensions", "how long is the room",
                    "what's the area"
                },
                RequiredSlots = new[] { SlotType.Reference },
                OptionalSlots = new[] { SlotType.PropertyName },
                Keywords = new[] { "what", "how", "dimension", "size", "area", "volume" },
                Patterns = new[]
                {
                    @"\b(what|how)\b.*\b(height|width|length|area|volume|size|dimension)\b",
                    @"\bwhat\s+are\b.*\bdimensions\b"
                }
            },
            new DomainIntent
            {
                Name = "GetElementInfo",
                Category = IntentCategory.Query,
                Examples = new[]
                {
                    "what is this", "tell me about this element",
                    "what type of wall is this", "show element properties"
                },
                RequiredSlots = new[] { SlotType.Reference },
                Keywords = new[] { "what", "tell", "show", "info", "properties" },
                Patterns = new[]
                {
                    @"\bwhat\s+(is|are)\s+(this|these|the)\b",
                    @"\btell\s+me\s+about\b",
                    @"\bshow\b.*\bproperties\b"
                }
            },
            new DomainIntent
            {
                Name = "CountElements",
                Category = IntentCategory.Query,
                Examples = new[]
                {
                    "how many walls", "count the doors", "how many rooms",
                    "total number of windows"
                },
                RequiredSlots = new[] { SlotType.ElementType },
                Keywords = new[] { "how many", "count", "total", "number" },
                Patterns = new[]
                {
                    @"\b(how\s+many|count|total)\b.*\b(walls?|doors?|windows?|rooms?)\b"
                }
            },

            // ===== SELECTION INTENTS =====
            new DomainIntent
            {
                Name = "SelectElement",
                Category = IntentCategory.Selection,
                Examples = new[]
                {
                    "select the wall", "select all doors", "pick this element",
                    "select similar", "select walls on this level"
                },
                OptionalSlots = new[] { SlotType.ElementType, SlotType.Reference, SlotType.Filter },
                Keywords = new[] { "select", "pick", "choose", "highlight" },
                Patterns = new[]
                {
                    @"\bselect\b.*\b(all|the|this|similar)\b",
                    @"\b(select|pick)\b.*\b(wall|door|window|room|element)\b"
                }
            },
            new DomainIntent
            {
                Name = "DeselectAll",
                Category = IntentCategory.Selection,
                Examples = new[]
                {
                    "deselect all", "clear selection", "unselect",
                    "deselect everything"
                },
                Keywords = new[] { "deselect", "clear", "unselect" },
                Patterns = new[]
                {
                    @"\b(deselect|clear|unselect)\b"
                }
            },

            // ===== VIEW INTENTS =====
            new DomainIntent
            {
                Name = "ZoomIn",
                Category = IntentCategory.View,
                Examples = new[]
                {
                    "zoom in", "get closer", "magnify", "zoom to selection"
                },
                Keywords = new[] { "zoom in", "closer", "magnify" },
                Patterns = new[]
                {
                    @"\bzoom\s+(in|to)\b",
                    @"\b(get|go)\s+closer\b"
                }
            },
            new DomainIntent
            {
                Name = "ZoomOut",
                Category = IntentCategory.View,
                Examples = new[]
                {
                    "zoom out", "see more", "show whole floor"
                },
                Keywords = new[] { "zoom out", "see more", "show all" },
                Patterns = new[]
                {
                    @"\bzoom\s+out\b",
                    @"\bshow\s+(whole|entire|all)\b"
                }
            },
            new DomainIntent
            {
                Name = "ChangeView",
                Category = IntentCategory.View,
                Examples = new[]
                {
                    "show 3D view", "switch to plan view", "go to level 1",
                    "show section", "open elevation"
                },
                OptionalSlots = new[] { SlotType.ViewType, SlotType.Level },
                Keywords = new[] { "show", "switch", "go to", "view", "3d", "plan", "section", "elevation" },
                Patterns = new[]
                {
                    @"\b(show|switch|go\s+to|open)\b.*\b(3d|plan|section|elevation|view|level)\b"
                }
            },

            // ===== UTILITY INTENTS =====
            new DomainIntent
            {
                Name = "Undo",
                Category = IntentCategory.Utility,
                Examples = new[] { "undo", "undo last action", "go back", "cancel that" },
                Keywords = new[] { "undo", "go back", "cancel", "revert" },
                Patterns = new[] { @"\bundo\b" }
            },
            new DomainIntent
            {
                Name = "Redo",
                Category = IntentCategory.Utility,
                Examples = new[] { "redo", "redo last action", "do again" },
                Keywords = new[] { "redo", "again" },
                Patterns = new[] { @"\bredo\b" }
            },
            new DomainIntent
            {
                Name = "Help",
                Category = IntentCategory.Utility,
                Examples = new[] { "help", "what can you do", "show commands", "how do i" },
                Keywords = new[] { "help", "what can", "show commands", "how do i" },
                Patterns = new[] { @"\b(help|what\s+can|show\s+commands|how\s+do\s+i)\b" }
            },

            // ===== ANALYSIS INTENTS =====
            new DomainIntent
            {
                Name = "CheckCompliance",
                Category = IntentCategory.Analysis,
                Examples = new[]
                {
                    "check building code", "is this compliant", "validate design",
                    "check fire safety", "verify accessibility"
                },
                OptionalSlots = new[] { SlotType.Standard, SlotType.Reference },
                Keywords = new[] { "check", "validate", "verify", "compliant", "compliance" },
                Patterns = new[]
                {
                    @"\b(check|validate|verify)\b.*\b(code|compliant|compliance|safety|accessibility)\b"
                }
            },
            new DomainIntent
            {
                Name = "CalculateArea",
                Category = IntentCategory.Analysis,
                Examples = new[]
                {
                    "calculate the area", "what is the total floor area",
                    "calculate room areas", "measure the space"
                },
                OptionalSlots = new[] { SlotType.Reference },
                Keywords = new[] { "calculate", "compute", "measure", "area" },
                Patterns = new[]
                {
                    @"\b(calculate|compute|measure)\b.*\b(area|space)\b"
                }
            },

            // ===== MEP INTENTS =====
            new DomainIntent
            {
                Name = "RouteDuct",
                Category = IntentCategory.Creation,
                Examples = new[]
                {
                    "route a duct", "add HVAC duct", "create ductwork",
                    "run a duct from the AHU", "add supply duct"
                },
                RequiredSlots = new[] { SlotType.ElementType },
                OptionalSlots = new[] { SlotType.Dimension, SlotType.Position },
                Keywords = new[] { "duct", "ductwork", "HVAC", "supply", "return", "exhaust" },
                Patterns = new[]
                {
                    @"\b(route|add|create|run)\b.*\bduct\b",
                    @"\bduct\b.*\b(from|to|through)\b"
                }
            },
            new DomainIntent
            {
                Name = "RoutePipe",
                Category = IntentCategory.Creation,
                Examples = new[]
                {
                    "route a pipe", "add plumbing", "create pipe run",
                    "run water supply pipe", "add drainage"
                },
                RequiredSlots = new[] { SlotType.ElementType },
                OptionalSlots = new[] { SlotType.Dimension, SlotType.Material },
                Keywords = new[] { "pipe", "plumbing", "water supply", "drainage", "waste" },
                Patterns = new[]
                {
                    @"\b(route|add|create|run)\b.*\bpipe\b",
                    @"\b(plumbing|drainage)\b"
                }
            },
            new DomainIntent
            {
                Name = "PlaceFixture",
                Category = IntentCategory.Creation,
                Examples = new[]
                {
                    "place a light fixture", "add a sprinkler", "put an outlet here",
                    "add a smoke detector", "place diffuser"
                },
                RequiredSlots = new[] { SlotType.ElementType },
                OptionalSlots = new[] { SlotType.Position },
                Keywords = new[] { "fixture", "light", "sprinkler", "outlet", "detector", "diffuser" },
                Patterns = new[]
                {
                    @"\b(place|add|put|install)\b.*\b(fixture|light|sprinkler|outlet|detector|diffuser)\b"
                }
            },
            new DomainIntent
            {
                Name = "CheckClash",
                Category = IntentCategory.Analysis,
                Examples = new[]
                {
                    "check for clashes", "run clash detection", "find conflicts",
                    "are there any clashes", "detect MEP conflicts"
                },
                Keywords = new[] { "clash", "conflict", "interference", "collision" },
                Patterns = new[]
                {
                    @"\b(check|run|find|detect)\b.*\b(clash|conflict|interference|collision)\b"
                }
            },

            // ===== PARAMETER/SCHEDULE INTENTS =====
            new DomainIntent
            {
                Name = "SetParameter",
                Category = IntentCategory.Modification,
                Examples = new[]
                {
                    "set the fire rating to 2 hours", "change the occupancy type",
                    "update the phase", "set mark to A-101"
                },
                RequiredSlots = new[] { SlotType.PropertyName, SlotType.PropertyValue },
                OptionalSlots = new[] { SlotType.Reference },
                Keywords = new[] { "parameter", "fire rating", "occupancy", "mark", "phase" },
                Patterns = new[]
                {
                    @"\b(set|change|update)\b.*\b(parameter|fire rating|occupancy|mark|phase)\b"
                }
            },
            new DomainIntent
            {
                Name = "GenerateSchedule",
                Category = IntentCategory.Analysis,
                Examples = new[]
                {
                    "generate a door schedule", "create room schedule",
                    "make a window schedule", "show me a schedule of walls"
                },
                RequiredSlots = new[] { SlotType.ElementType },
                Keywords = new[] { "schedule", "table", "list", "report" },
                Patterns = new[]
                {
                    @"\b(generate|create|make|show)\b.*\bschedule\b"
                }
            },

            // ===== EXPORT/DOCUMENTATION INTENTS =====
            new DomainIntent
            {
                Name = "ExportModel",
                Category = IntentCategory.Utility,
                Examples = new[]
                {
                    "export to IFC", "export as PDF", "save as DWG",
                    "export the model", "create an IFC export"
                },
                Keywords = new[] { "export", "IFC", "PDF", "DWG", "save as" },
                Patterns = new[]
                {
                    @"\b(export|save)\b.*\b(IFC|PDF|DWG|DXF|model)\b"
                }
            },
            new DomainIntent
            {
                Name = "AnnotateDrawing",
                Category = IntentCategory.Creation,
                Examples = new[]
                {
                    "add a dimension", "annotate this", "add text note",
                    "place a tag", "add section mark"
                },
                Keywords = new[] { "dimension", "annotate", "text", "note", "tag", "label" },
                Patterns = new[]
                {
                    @"\b(add|place|create)\b.*\b(dimension|annotation|text|note|tag|label)\b"
                }
            }
        };

        // Room type definitions with characteristics
        private static readonly Dictionary<string, RoomTypeInfo> RoomTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["bedroom"] = new RoomTypeInfo
            {
                CanonicalName = "Bedroom",
                Synonyms = new[] { "bed room", "sleeping room", "master bedroom", "guest bedroom" },
                DefaultArea = 12.0,
                MinArea = 9.0,
                RequiresWindow = true,
                RequiresDoor = true,
                AdjacentPreferred = new[] { "bathroom", "corridor" },
                AdjacentAvoided = new[] { "kitchen", "garage" }
            },
            ["bathroom"] = new RoomTypeInfo
            {
                CanonicalName = "Bathroom",
                Synonyms = new[] { "bath room", "toilet", "restroom", "washroom", "wc", "lavatory" },
                DefaultArea = 5.0,
                MinArea = 2.5,
                RequiresWindow = false,
                RequiresDoor = true,
                RequiresPlumbing = true,
                RequiresVentilation = true,
                AdjacentPreferred = new[] { "bedroom", "corridor" }
            },
            ["kitchen"] = new RoomTypeInfo
            {
                CanonicalName = "Kitchen",
                Synonyms = new[] { "kitchenette", "cooking area", "galley" },
                DefaultArea = 10.0,
                MinArea = 6.0,
                RequiresWindow = true,
                RequiresDoor = true,
                RequiresPlumbing = true,
                RequiresVentilation = true,
                AdjacentPreferred = new[] { "dining", "living" },
                AdjacentAvoided = new[] { "bedroom" }
            },
            ["living"] = new RoomTypeInfo
            {
                CanonicalName = "Living Room",
                Synonyms = new[] { "living room", "lounge", "sitting room", "family room", "drawing room" },
                DefaultArea = 20.0,
                MinArea = 12.0,
                RequiresWindow = true,
                RequiresDoor = true,
                AdjacentPreferred = new[] { "dining", "kitchen" }
            },
            ["dining"] = new RoomTypeInfo
            {
                CanonicalName = "Dining Room",
                Synonyms = new[] { "dining room", "dining area", "eat-in" },
                DefaultArea = 12.0,
                MinArea = 8.0,
                RequiresWindow = true,
                AdjacentPreferred = new[] { "kitchen", "living" }
            },
            ["office"] = new RoomTypeInfo
            {
                CanonicalName = "Office",
                Synonyms = new[] { "home office", "study", "workspace", "workroom" },
                DefaultArea = 10.0,
                MinArea = 6.0,
                RequiresWindow = true,
                RequiresDoor = true
            },
            ["corridor"] = new RoomTypeInfo
            {
                CanonicalName = "Corridor",
                Synonyms = new[] { "hallway", "hall", "passage", "passageway" },
                DefaultArea = 0,
                MinArea = 0,
                MinWidth = 1.2,
                RequiresWindow = false
            },
            ["storage"] = new RoomTypeInfo
            {
                CanonicalName = "Storage",
                Synonyms = new[] { "closet", "pantry", "store room", "utility" },
                DefaultArea = 3.0,
                MinArea = 1.0,
                RequiresWindow = false
            },
            ["garage"] = new RoomTypeInfo
            {
                CanonicalName = "Garage",
                Synonyms = new[] { "car port", "parking" },
                DefaultArea = 18.0,
                MinArea = 15.0,
                RequiresWindow = false,
                RequiresDoor = true
            },
            ["laundry"] = new RoomTypeInfo
            {
                CanonicalName = "Laundry",
                Synonyms = new[] { "laundry room", "utility room" },
                DefaultArea = 5.0,
                MinArea = 3.0,
                RequiresPlumbing = true,
                RequiresVentilation = true
            },
            // ===== COMMERCIAL/INSTITUTIONAL ROOM TYPES =====
            ["conference"] = new RoomTypeInfo
            {
                CanonicalName = "Conference Room",
                Synonyms = new[] { "conference room", "meeting room", "boardroom", "meeting space" },
                DefaultArea = 25.0,
                MinArea = 15.0,
                RequiresWindow = true,
                RequiresDoor = true,
                RequiresVentilation = true,
                AdjacentPreferred = new[] { "corridor", "reception" }
            },
            ["lobby"] = new RoomTypeInfo
            {
                CanonicalName = "Lobby",
                Synonyms = new[] { "foyer", "entrance hall", "vestibule", "atrium" },
                DefaultArea = 30.0,
                MinArea = 15.0,
                RequiresWindow = true,
                RequiresDoor = true,
                AdjacentPreferred = new[] { "reception", "corridor", "elevator" }
            },
            ["reception"] = new RoomTypeInfo
            {
                CanonicalName = "Reception",
                Synonyms = new[] { "reception area", "front desk", "welcome area" },
                DefaultArea = 15.0,
                MinArea = 8.0,
                RequiresWindow = true,
                AdjacentPreferred = new[] { "lobby", "corridor" }
            },
            ["server_room"] = new RoomTypeInfo
            {
                CanonicalName = "Server Room",
                Synonyms = new[] { "data center", "IT room", "comms room", "network room" },
                DefaultArea = 15.0,
                MinArea = 8.0,
                RequiresWindow = false,
                RequiresDoor = true,
                RequiresVentilation = true,
                AdjacentAvoided = new[] { "bathroom", "kitchen" }
            },
            ["mechanical_room"] = new RoomTypeInfo
            {
                CanonicalName = "Mechanical Room",
                Synonyms = new[] { "plant room", "boiler room", "HVAC room", "mechanical space" },
                DefaultArea = 15.0,
                MinArea = 8.0,
                RequiresWindow = false,
                RequiresDoor = true,
                RequiresVentilation = true
            },
            ["electrical_room"] = new RoomTypeInfo
            {
                CanonicalName = "Electrical Room",
                Synonyms = new[] { "electrical closet", "switchgear room", "transformer room" },
                DefaultArea = 8.0,
                MinArea = 4.0,
                RequiresWindow = false,
                RequiresDoor = true,
                RequiresVentilation = true
            },
            ["open_office"] = new RoomTypeInfo
            {
                CanonicalName = "Open Office",
                Synonyms = new[] { "open plan", "bullpen", "workspace", "open plan office" },
                DefaultArea = 100.0,
                MinArea = 50.0,
                RequiresWindow = true,
                RequiresVentilation = true,
                AdjacentPreferred = new[] { "corridor", "break_room" }
            },
            ["break_room"] = new RoomTypeInfo
            {
                CanonicalName = "Break Room",
                Synonyms = new[] { "kitchenette", "tea room", "lunch room", "staff room" },
                DefaultArea = 15.0,
                MinArea = 8.0,
                RequiresWindow = true,
                RequiresPlumbing = true,
                RequiresVentilation = true,
                AdjacentPreferred = new[] { "open_office", "corridor" }
            },
            ["restroom"] = new RoomTypeInfo
            {
                CanonicalName = "Public Restroom",
                Synonyms = new[] { "public toilet", "washroom", "facilities" },
                DefaultArea = 10.0,
                MinArea = 4.0,
                RequiresPlumbing = true,
                RequiresVentilation = true,
                AdjacentPreferred = new[] { "corridor" }
            },
            ["classroom"] = new RoomTypeInfo
            {
                CanonicalName = "Classroom",
                Synonyms = new[] { "lecture room", "teaching room", "training room", "seminar room" },
                DefaultArea = 50.0,
                MinArea = 30.0,
                RequiresWindow = true,
                RequiresDoor = true,
                RequiresVentilation = true
            },
            ["laboratory"] = new RoomTypeInfo
            {
                CanonicalName = "Laboratory",
                Synonyms = new[] { "lab", "research lab", "testing lab" },
                DefaultArea = 40.0,
                MinArea = 20.0,
                RequiresWindow = true,
                RequiresPlumbing = true,
                RequiresVentilation = true,
                AdjacentAvoided = new[] { "classroom", "office" }
            },
            ["cafeteria"] = new RoomTypeInfo
            {
                CanonicalName = "Cafeteria",
                Synonyms = new[] { "canteen", "dining hall", "food court" },
                DefaultArea = 80.0,
                MinArea = 40.0,
                RequiresWindow = true,
                RequiresPlumbing = true,
                RequiresVentilation = true,
                AdjacentPreferred = new[] { "kitchen", "corridor" }
            },
            ["auditorium"] = new RoomTypeInfo
            {
                CanonicalName = "Auditorium",
                Synonyms = new[] { "hall", "lecture hall", "assembly hall", "theater" },
                DefaultArea = 200.0,
                MinArea = 80.0,
                RequiresWindow = false,
                RequiresDoor = true,
                RequiresVentilation = true
            },
            ["library"] = new RoomTypeInfo
            {
                CanonicalName = "Library",
                Synonyms = new[] { "reading room", "media center", "resource center" },
                DefaultArea = 80.0,
                MinArea = 30.0,
                RequiresWindow = true,
                RequiresVentilation = true
            },
            ["stairwell"] = new RoomTypeInfo
            {
                CanonicalName = "Stairwell",
                Synonyms = new[] { "stair enclosure", "fire stair", "emergency stair" },
                DefaultArea = 8.0,
                MinArea = 4.0,
                MinWidth = 1.1,
                RequiresWindow = false,
                RequiresDoor = true
            },
            ["elevator"] = new RoomTypeInfo
            {
                CanonicalName = "Elevator",
                Synonyms = new[] { "lift", "elevator shaft", "lift shaft" },
                DefaultArea = 4.0,
                MinArea = 2.5,
                RequiresWindow = false,
                RequiresDoor = true
            },
            ["loading_dock"] = new RoomTypeInfo
            {
                CanonicalName = "Loading Dock",
                Synonyms = new[] { "delivery bay", "loading bay", "service entrance" },
                DefaultArea = 50.0,
                MinArea = 25.0,
                RequiresWindow = false,
                RequiresDoor = true,
                RequiresVentilation = true
            },
            ["janitor"] = new RoomTypeInfo
            {
                CanonicalName = "Janitor Closet",
                Synonyms = new[] { "cleaning closet", "broom closet", "janitorial" },
                DefaultArea = 3.0,
                MinArea = 1.5,
                RequiresPlumbing = true
            },
            ["terrace"] = new RoomTypeInfo
            {
                CanonicalName = "Terrace",
                Synonyms = new[] { "balcony", "patio", "deck", "veranda" },
                DefaultArea = 10.0,
                MinArea = 3.0,
                RequiresWindow = false
            }
        };

        // Material definitions
        private static readonly Dictionary<string, MaterialInfo> Materials = new(StringComparer.OrdinalIgnoreCase)
        {
            ["concrete"] = new MaterialInfo
            {
                CanonicalName = "Concrete",
                Synonyms = new[] { "cement", "reinforced concrete", "rc" },
                Category = MaterialCategory.Structural,
                IsStructural = true
            },
            ["brick"] = new MaterialInfo
            {
                CanonicalName = "Brick",
                Synonyms = new[] { "clay brick", "masonry", "brickwork" },
                Category = MaterialCategory.Structural,
                IsStructural = true
            },
            ["steel"] = new MaterialInfo
            {
                CanonicalName = "Steel",
                Synonyms = new[] { "structural steel", "metal" },
                Category = MaterialCategory.Structural,
                IsStructural = true
            },
            ["wood"] = new MaterialInfo
            {
                CanonicalName = "Wood",
                Synonyms = new[] { "timber", "lumber", "wooden" },
                Category = MaterialCategory.Structural,
                IsStructural = true
            },
            ["drywall"] = new MaterialInfo
            {
                CanonicalName = "Drywall",
                Synonyms = new[] { "gypsum board", "plasterboard", "sheetrock", "gib" },
                Category = MaterialCategory.Finish,
                IsStructural = false
            },
            ["glass"] = new MaterialInfo
            {
                CanonicalName = "Glass",
                Synonyms = new[] { "glazing", "window glass" },
                Category = MaterialCategory.Glazing,
                IsStructural = false
            },
            ["tile"] = new MaterialInfo
            {
                CanonicalName = "Tile",
                Synonyms = new[] { "ceramic tile", "porcelain", "floor tile" },
                Category = MaterialCategory.Finish,
                IsStructural = false
            },
            // ===== EXPANDED MATERIALS =====
            ["aluminum"] = new MaterialInfo
            {
                CanonicalName = "Aluminum",
                Synonyms = new[] { "aluminium", "aluminum cladding", "alu" },
                Category = MaterialCategory.Finish,
                IsStructural = false
            },
            ["stone"] = new MaterialInfo
            {
                CanonicalName = "Stone",
                Synonyms = new[] { "natural stone", "granite", "marble", "limestone", "sandstone", "slate" },
                Category = MaterialCategory.Structural,
                IsStructural = true
            },
            ["plaster"] = new MaterialInfo
            {
                CanonicalName = "Plaster",
                Synonyms = new[] { "render", "stucco", "cement render", "lime plaster" },
                Category = MaterialCategory.Finish,
                IsStructural = false
            },
            ["insulation"] = new MaterialInfo
            {
                CanonicalName = "Insulation",
                Synonyms = new[] { "mineral wool", "fiberglass", "rockwool", "foam board", "polystyrene", "PIR" },
                Category = MaterialCategory.Insulation,
                IsStructural = false
            },
            ["vinyl"] = new MaterialInfo
            {
                CanonicalName = "Vinyl",
                Synonyms = new[] { "vinyl flooring", "LVT", "luxury vinyl tile", "PVC flooring" },
                Category = MaterialCategory.Finish,
                IsStructural = false
            },
            ["carpet"] = new MaterialInfo
            {
                CanonicalName = "Carpet",
                Synonyms = new[] { "carpeting", "carpet tile", "broadloom" },
                Category = MaterialCategory.Finish,
                IsStructural = false
            },
            ["composite"] = new MaterialInfo
            {
                CanonicalName = "Composite",
                Synonyms = new[] { "fiber cement", "hardie board", "cement board", "composite panel" },
                Category = MaterialCategory.Finish,
                IsStructural = false
            },
            ["membrane"] = new MaterialInfo
            {
                CanonicalName = "Membrane",
                Synonyms = new[] { "waterproof membrane", "bitumen", "EPDM", "TPO", "roofing membrane" },
                Category = MaterialCategory.Roofing,
                IsStructural = false
            },
            ["precast"] = new MaterialInfo
            {
                CanonicalName = "Precast Concrete",
                Synonyms = new[] { "precast", "prefab concrete", "precast panel" },
                Category = MaterialCategory.Structural,
                IsStructural = true
            },
            ["block"] = new MaterialInfo
            {
                CanonicalName = "Concrete Block",
                Synonyms = new[] { "cinder block", "CMU", "concrete masonry unit", "hollow block" },
                Category = MaterialCategory.Structural,
                IsStructural = true
            },
            ["laminate"] = new MaterialInfo
            {
                CanonicalName = "Laminate",
                Synonyms = new[] { "laminate flooring", "laminated wood", "engineered wood" },
                Category = MaterialCategory.Finish,
                IsStructural = false
            },
            ["copper"] = new MaterialInfo
            {
                CanonicalName = "Copper",
                Synonyms = new[] { "copper pipe", "copper cladding" },
                Category = MaterialCategory.Finish,
                IsStructural = false
            }
        };

        #region Public API

        /// <summary>
        /// Gets all domain intent definitions.
        /// </summary>
        public static IReadOnlyList<DomainIntent> GetIntents() => DomainIntents;

        /// <summary>
        /// Gets all room type definitions.
        /// </summary>
        public static IReadOnlyDictionary<string, RoomTypeInfo> GetRoomTypes() => RoomTypes;

        /// <summary>
        /// Gets all material definitions.
        /// </summary>
        public static IReadOnlyDictionary<string, MaterialInfo> GetMaterials() => Materials;

        /// <summary>
        /// Resolves a room type from natural language input.
        /// </summary>
        public static RoomTypeInfo ResolveRoomType(string input)
        {
            var normalized = input.Trim().ToLowerInvariant();

            // Direct match
            if (RoomTypes.TryGetValue(normalized, out var roomType))
            {
                return roomType;
            }

            // Synonym match
            foreach (var (key, info) in RoomTypes)
            {
                if (info.Synonyms.Any(s => s.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    return info;
                }

                // Partial match
                if (normalized.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    return info;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves a material from natural language input.
        /// </summary>
        public static MaterialInfo ResolveMaterial(string input)
        {
            var normalized = input.Trim().ToLowerInvariant();

            // Direct match
            if (Materials.TryGetValue(normalized, out var material))
            {
                return material;
            }

            // Synonym match
            foreach (var (key, info) in Materials)
            {
                if (info.Synonyms.Any(s => s.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    return info;
                }
            }

            return null;
        }

        /// <summary>
        /// Classifies intent using domain patterns.
        /// </summary>
        public static DomainIntentMatch ClassifyIntent(string text)
        {
            var normalizedText = text.Trim().ToLowerInvariant();
            var bestMatch = new DomainIntentMatch { Confidence = 0 };

            foreach (var intent in DomainIntents)
            {
                var confidence = CalculateIntentConfidence(normalizedText, intent);

                if (confidence > bestMatch.Confidence)
                {
                    bestMatch = new DomainIntentMatch
                    {
                        Intent = intent,
                        Confidence = confidence,
                        MatchedKeywords = intent.Keywords
                            .Where(k => normalizedText.Contains(k, StringComparison.OrdinalIgnoreCase))
                            .ToList()
                    };
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// Extracts domain-specific entities from text.
        /// </summary>
        public static List<DomainEntity> ExtractDomainEntities(string text)
        {
            var entities = new List<DomainEntity>();
            var normalizedText = text.ToLowerInvariant();

            // Extract room types
            foreach (var (key, roomInfo) in RoomTypes)
            {
                if (normalizedText.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                    roomInfo.Synonyms.Any(s => normalizedText.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    entities.Add(new DomainEntity
                    {
                        Type = DomainEntityType.RoomType,
                        Value = key,
                        NormalizedValue = roomInfo.CanonicalName,
                        Confidence = 0.9f,
                        Metadata = new Dictionary<string, object>
                        {
                            ["DefaultArea"] = roomInfo.DefaultArea,
                            ["MinArea"] = roomInfo.MinArea,
                            ["RequiresWindow"] = roomInfo.RequiresWindow
                        }
                    });
                    break;
                }
            }

            // Extract materials
            foreach (var (key, materialInfo) in Materials)
            {
                if (normalizedText.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                    materialInfo.Synonyms.Any(s => normalizedText.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    entities.Add(new DomainEntity
                    {
                        Type = DomainEntityType.Material,
                        Value = key,
                        NormalizedValue = materialInfo.CanonicalName,
                        Confidence = 0.9f,
                        Metadata = new Dictionary<string, object>
                        {
                            ["IsStructural"] = materialInfo.IsStructural,
                            ["Category"] = materialInfo.Category.ToString()
                        }
                    });
                    break;
                }
            }

            // Extract element types
            var elementTypes = new Dictionary<string, string>
            {
                { "wall", "Wall" }, { "door", "Door" }, { "window", "Window" },
                { "floor", "Floor" }, { "ceiling", "Ceiling" }, { "roof", "Roof" },
                { "column", "Column" }, { "beam", "Beam" }, { "stair", "Stair" },
                { "ramp", "Ramp" }, { "railing", "Railing" },
                { "curtain wall", "CurtainWall" }, { "foundation", "Foundation" },
                { "slab", "Slab" }, { "duct", "Duct" }, { "pipe", "Pipe" },
                { "conduit", "Conduit" }, { "sprinkler", "Sprinkler" },
                { "diffuser", "Diffuser" }, { "panel", "Panel" },
                { "fixture", "Fixture" }, { "elevator", "Elevator" },
                { "skylight", "Skylight" }, { "canopy", "Canopy" },
                { "parapet", "Parapet" }, { "mullion", "Mullion" }
            };

            foreach (var (key, canonical) in elementTypes)
            {
                if (normalizedText.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    entities.Add(new DomainEntity
                    {
                        Type = DomainEntityType.ElementType,
                        Value = key,
                        NormalizedValue = canonical,
                        Confidence = 0.95f
                    });
                    break;
                }
            }

            // Extract directions
            var directions = new[] { "north", "south", "east", "west", "up", "down", "left", "right" };
            foreach (var dir in directions)
            {
                if (Regex.IsMatch(normalizedText, $@"\b{dir}\b", RegexOptions.IgnoreCase))
                {
                    entities.Add(new DomainEntity
                    {
                        Type = DomainEntityType.Direction,
                        Value = dir,
                        NormalizedValue = char.ToUpper(dir[0]) + dir.Substring(1),
                        Confidence = 0.95f
                    });
                }
            }

            return entities;
        }

        #endregion

        #region Private Methods

        private static float CalculateIntentConfidence(string text, DomainIntent intent)
        {
            var confidence = 0f;

            // Check patterns
            foreach (var pattern in intent.Patterns)
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                {
                    confidence = Math.Max(confidence, 0.85f);
                }
            }

            // Check keywords
            var matchedKeywords = intent.Keywords.Count(k =>
                text.Contains(k, StringComparison.OrdinalIgnoreCase));

            if (matchedKeywords > 0)
            {
                var keywordScore = (float)matchedKeywords / intent.Keywords.Length;
                confidence = Math.Max(confidence, 0.5f + keywordScore * 0.3f);
            }

            return confidence;
        }

        #endregion
    }

    #region Supporting Types

    public class DomainIntent
    {
        public string Name { get; set; }
        public IntentCategory Category { get; set; }
        public string[] Examples { get; set; } = Array.Empty<string>();
        public SlotType[] RequiredSlots { get; set; } = Array.Empty<SlotType>();
        public SlotType[] OptionalSlots { get; set; } = Array.Empty<SlotType>();
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public string[] Patterns { get; set; } = Array.Empty<string>();
    }

    public enum IntentCategory
    {
        Creation,
        Modification,
        Query,
        Selection,
        View,
        Utility,
        Analysis
    }

    public enum SlotType
    {
        ElementType,
        RoomType,
        Dimension,
        Direction,
        Position,
        Material,
        DoorType,
        WindowType,
        Reference,
        PropertyName,
        PropertyValue,
        Count,
        Angle,
        Level,
        ViewType,
        Filter,
        Standard
    }

    public class DomainIntentMatch
    {
        public DomainIntent Intent { get; set; }
        public float Confidence { get; set; }
        public List<string> MatchedKeywords { get; set; } = new();
    }

    public class RoomTypeInfo
    {
        public string CanonicalName { get; set; }
        public string[] Synonyms { get; set; } = Array.Empty<string>();
        public double DefaultArea { get; set; }
        public double MinArea { get; set; }
        public double MinWidth { get; set; }
        public bool RequiresWindow { get; set; }
        public bool RequiresDoor { get; set; } = true;
        public bool RequiresPlumbing { get; set; }
        public bool RequiresVentilation { get; set; }
        public string[] AdjacentPreferred { get; set; } = Array.Empty<string>();
        public string[] AdjacentAvoided { get; set; } = Array.Empty<string>();
    }

    public class MaterialInfo
    {
        public string CanonicalName { get; set; }
        public string[] Synonyms { get; set; } = Array.Empty<string>();
        public MaterialCategory Category { get; set; }
        public bool IsStructural { get; set; }
    }

    public enum MaterialCategory
    {
        Structural,
        Finish,
        Glazing,
        Insulation,
        Roofing
    }

    public class DomainEntity
    {
        public DomainEntityType Type { get; set; }
        public string Value { get; set; }
        public string NormalizedValue { get; set; }
        public float Confidence { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public enum DomainEntityType
    {
        RoomType,
        ElementType,
        Material,
        Direction,
        Dimension,
        Position
    }

    #endregion
}
