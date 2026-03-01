// ===================================================================================
// StingBIM Prompt-to-Model Engine
// Natural language building generation - converts text prompts to full BIM models
// ===================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Creation.Generation
{
    /// <summary>
    /// Converts natural language descriptions into complete 3D BIM models.
    /// Supports prompts like "Create a 3-bedroom house with garage" and generates
    /// full architectural models with walls, rooms, doors, windows, and more.
    /// </summary>
    public class PromptToModelEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly PromptParser _promptParser;
        private readonly BuildingProgramGenerator _programGenerator;
        private readonly SpacePlanner _spacePlanner;
        private readonly ArchitecturalGenerator _architecturalGenerator;
        private readonly DesignRulesEngine _designRules;
        private readonly PromptToModelSettings _settings;

        // Building type templates
        private readonly Dictionary<BuildingType, BuildingTemplate> _templates;

        public PromptToModelEngine(PromptToModelSettings settings = null)
        {
            _settings = settings ?? new PromptToModelSettings();
            _promptParser = new PromptParser();
            _programGenerator = new BuildingProgramGenerator();
            _spacePlanner = new SpacePlanner(_settings);
            _architecturalGenerator = new ArchitecturalGenerator(_settings);
            _designRules = new DesignRulesEngine();
            _templates = LoadBuildingTemplates();

            Logger.Info("PromptToModelEngine initialized");
        }

        #region Main Generation Methods

        /// <summary>
        /// Generate a BIM model from a natural language prompt
        /// </summary>
        public async Task<GenerationResult> GenerateFromPromptAsync(
            string prompt,
            GenerationOptions options = null,
            IProgress<GenerationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new GenerationResult
            {
                OriginalPrompt = prompt,
                GenerationStartTime = DateTime.Now
            };

            try
            {
                Logger.Info("Starting generation from prompt: {0}", prompt);
                options ??= new GenerationOptions();

                // Parse the prompt
                progress?.Report(new GenerationProgress(5, "Analyzing prompt..."));
                var parsedRequest = await _promptParser.ParseAsync(prompt, cancellationToken);
                result.ParsedRequest = parsedRequest;

                if (!parsedRequest.IsValid)
                {
                    result.Success = false;
                    result.Errors.AddRange(parsedRequest.ValidationErrors);
                    return result;
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Generate building program
                progress?.Report(new GenerationProgress(15, "Generating building program..."));
                var buildingProgram = await _programGenerator.GenerateProgramAsync(
                    parsedRequest,
                    _templates,
                    cancellationToken);
                result.BuildingProgram = buildingProgram;

                cancellationToken.ThrowIfCancellationRequested();

                // Generate space layout
                progress?.Report(new GenerationProgress(35, "Planning space layout..."));
                var spaceLayout = await _spacePlanner.GenerateLayoutAsync(
                    buildingProgram,
                    options,
                    cancellationToken);
                result.SpaceLayout = spaceLayout;

                cancellationToken.ThrowIfCancellationRequested();

                // Apply design rules
                progress?.Report(new GenerationProgress(55, "Applying design rules..."));
                var optimizedLayout = await _designRules.OptimizeLayoutAsync(
                    spaceLayout,
                    buildingProgram,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // Generate architectural elements
                progress?.Report(new GenerationProgress(70, "Generating architectural elements..."));
                var architecturalModel = await _architecturalGenerator.GenerateModelAsync(
                    optimizedLayout,
                    buildingProgram,
                    options,
                    cancellationToken);
                result.Model = architecturalModel;

                // Validate and finalize
                progress?.Report(new GenerationProgress(90, "Validating model..."));
                await ValidateModelAsync(result, cancellationToken);

                progress?.Report(new GenerationProgress(100, "Generation complete"));
                result.Success = true;
                result.GenerationEndTime = DateTime.Now;

                Logger.Info("Generation completed: {0} elements created",
                    architecturalModel.Elements.Count);
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Errors.Add("Generation cancelled by user");
                Logger.Warn("Generation cancelled");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Generation failed: {ex.Message}");
                Logger.Error(ex, "Generation failed");
            }

            return result;
        }

        /// <summary>
        /// Generate variations of a model based on additional prompts
        /// </summary>
        public async Task<List<GenerationResult>> GenerateVariationsAsync(
            GenerationResult baseResult,
            IEnumerable<string> variationPrompts,
            GenerationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<GenerationResult>();

            foreach (var variationPrompt in variationPrompts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Combine base prompt with variation
                var combinedPrompt = $"{baseResult.OriginalPrompt}. Also: {variationPrompt}";
                var result = await GenerateFromPromptAsync(combinedPrompt, options, null, cancellationToken);
                result.IsVariation = true;
                result.VariationDescription = variationPrompt;
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Refine an existing model with additional instructions
        /// </summary>
        public async Task<GenerationResult> RefineModelAsync(
            GenerationResult existingResult,
            string refinementPrompt,
            CancellationToken cancellationToken = default)
        {
            var result = new GenerationResult
            {
                OriginalPrompt = $"{existingResult.OriginalPrompt}. Refinement: {refinementPrompt}",
                GenerationStartTime = DateTime.Now,
                IsRefinement = true
            };

            try
            {
                // Parse refinement request
                var refinementRequest = await _promptParser.ParseRefinementAsync(
                    refinementPrompt,
                    existingResult.ParsedRequest,
                    cancellationToken);

                // Apply modifications to existing model
                var refinedModel = await ApplyRefinementsAsync(
                    existingResult.Model,
                    refinementRequest,
                    cancellationToken);

                result.Model = refinedModel;
                result.ParsedRequest = refinementRequest;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Refinement failed: {ex.Message}");
            }

            result.GenerationEndTime = DateTime.Now;
            return result;
        }

        #endregion

        #region Helper Methods

        private Dictionary<BuildingType, BuildingTemplate> LoadBuildingTemplates()
        {
            return new Dictionary<BuildingType, BuildingTemplate>
            {
                [BuildingType.SingleFamilyHouse] = new BuildingTemplate
                {
                    Type = BuildingType.SingleFamilyHouse,
                    DefaultStoreys = 1,
                    MaxStoreys = 3,
                    DefaultRooms = new List<RoomTemplate>
                    {
                        new RoomTemplate { Type = RoomType.LivingRoom, MinArea = 20, MaxArea = 50, Required = true },
                        new RoomTemplate { Type = RoomType.Kitchen, MinArea = 10, MaxArea = 25, Required = true },
                        new RoomTemplate { Type = RoomType.DiningRoom, MinArea = 10, MaxArea = 20, Required = false },
                        new RoomTemplate { Type = RoomType.MasterBedroom, MinArea = 15, MaxArea = 30, Required = true },
                        new RoomTemplate { Type = RoomType.MasterBathroom, MinArea = 5, MaxArea = 12, Required = true },
                        new RoomTemplate { Type = RoomType.Entrance, MinArea = 3, MaxArea = 10, Required = true }
                    },
                    TypicalFootprint = new FootprintRange { Min = 80, Max = 300 },
                    DefaultWallThickness = 200,
                    DefaultCeilingHeight = 2700
                },
                [BuildingType.Apartment] = new BuildingTemplate
                {
                    Type = BuildingType.Apartment,
                    DefaultStoreys = 1,
                    MaxStoreys = 1,
                    DefaultRooms = new List<RoomTemplate>
                    {
                        new RoomTemplate { Type = RoomType.LivingRoom, MinArea = 15, MaxArea = 35, Required = true },
                        new RoomTemplate { Type = RoomType.Kitchen, MinArea = 6, MaxArea = 15, Required = true },
                        new RoomTemplate { Type = RoomType.Bedroom, MinArea = 10, MaxArea = 20, Required = true },
                        new RoomTemplate { Type = RoomType.Bathroom, MinArea = 4, MaxArea = 8, Required = true },
                        new RoomTemplate { Type = RoomType.Entrance, MinArea = 2, MaxArea = 6, Required = true }
                    },
                    TypicalFootprint = new FootprintRange { Min = 40, Max = 150 },
                    DefaultWallThickness = 150,
                    DefaultCeilingHeight = 2600
                },
                [BuildingType.Office] = new BuildingTemplate
                {
                    Type = BuildingType.Office,
                    DefaultStoreys = 1,
                    MaxStoreys = 50,
                    DefaultRooms = new List<RoomTemplate>
                    {
                        new RoomTemplate { Type = RoomType.OpenOffice, MinArea = 100, MaxArea = 2000, Required = true },
                        new RoomTemplate { Type = RoomType.MeetingRoom, MinArea = 15, MaxArea = 50, Required = true },
                        new RoomTemplate { Type = RoomType.Reception, MinArea = 20, MaxArea = 100, Required = true },
                        new RoomTemplate { Type = RoomType.Bathroom, MinArea = 10, MaxArea = 30, Required = true },
                        new RoomTemplate { Type = RoomType.Kitchen, MinArea = 10, MaxArea = 30, Required = false }
                    },
                    TypicalFootprint = new FootprintRange { Min = 200, Max = 5000 },
                    DefaultWallThickness = 150,
                    DefaultCeilingHeight = 2800
                },
                [BuildingType.Retail] = new BuildingTemplate
                {
                    Type = BuildingType.Retail,
                    DefaultStoreys = 1,
                    MaxStoreys = 5,
                    DefaultRooms = new List<RoomTemplate>
                    {
                        new RoomTemplate { Type = RoomType.SalesFloor, MinArea = 50, MaxArea = 5000, Required = true },
                        new RoomTemplate { Type = RoomType.Storage, MinArea = 20, MaxArea = 500, Required = true },
                        new RoomTemplate { Type = RoomType.Office, MinArea = 10, MaxArea = 50, Required = false },
                        new RoomTemplate { Type = RoomType.Bathroom, MinArea = 5, MaxArea = 20, Required = true }
                    },
                    TypicalFootprint = new FootprintRange { Min = 100, Max = 10000 },
                    DefaultWallThickness = 200,
                    DefaultCeilingHeight = 3500
                },
                [BuildingType.School] = new BuildingTemplate
                {
                    Type = BuildingType.School,
                    DefaultStoreys = 2,
                    MaxStoreys = 4,
                    DefaultRooms = new List<RoomTemplate>
                    {
                        new RoomTemplate { Type = RoomType.Classroom, MinArea = 50, MaxArea = 80, Required = true },
                        new RoomTemplate { Type = RoomType.Corridor, MinArea = 20, MaxArea = 200, Required = true },
                        new RoomTemplate { Type = RoomType.Office, MinArea = 15, MaxArea = 40, Required = true },
                        new RoomTemplate { Type = RoomType.Bathroom, MinArea = 15, MaxArea = 40, Required = true },
                        new RoomTemplate { Type = RoomType.Hall, MinArea = 100, MaxArea = 500, Required = false }
                    },
                    TypicalFootprint = new FootprintRange { Min = 500, Max = 5000 },
                    DefaultWallThickness = 200,
                    DefaultCeilingHeight = 3000
                }
            };
        }

        private async Task ValidateModelAsync(GenerationResult result, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                var model = result.Model;

                // Check minimum room sizes
                foreach (var room in model.Elements.Where(e => e.ElementType == ElementType.Room))
                {
                    var area = room.GetArea();
                    if (area < _settings.MinRoomArea)
                    {
                        result.Warnings.Add($"Room '{room.Name}' is smaller than recommended ({area:F1}m²)");
                    }
                }

                // Check door placement
                foreach (var door in model.Elements.Where(e => e.ElementType == ElementType.Door))
                {
                    if (door.HostElement == null)
                    {
                        result.Warnings.Add($"Door at {door.Location} is not hosted in a wall");
                    }
                }

                // Check window placement
                foreach (var window in model.Elements.Where(e => e.ElementType == ElementType.Window))
                {
                    var hostWall = window.HostElement;
                    if (hostWall != null && !IsExteriorWall(hostWall, model))
                    {
                        result.Warnings.Add($"Window at {window.Location} is in an interior wall");
                    }
                }

                // Calculate statistics
                result.Statistics = new GenerationStatistics
                {
                    TotalArea = model.Elements.Where(e => e.ElementType == ElementType.Room).Sum(r => r.GetArea()),
                    TotalWalls = model.Elements.Count(e => e.ElementType == ElementType.Wall),
                    TotalDoors = model.Elements.Count(e => e.ElementType == ElementType.Door),
                    TotalWindows = model.Elements.Count(e => e.ElementType == ElementType.Window),
                    TotalRooms = model.Elements.Count(e => e.ElementType == ElementType.Room),
                    StoreyCount = model.Levels.Count
                };

            }, cancellationToken);
        }

        private bool IsExteriorWall(GeneratedElement wall, GeneratedModel model)
        {
            // Check if wall is on building perimeter
            return wall.Properties.TryGetValue("IsExterior", out var isExt) && (bool)isExt;
        }

        private async Task<GeneratedModel> ApplyRefinementsAsync(
            GeneratedModel model,
            ParsedRequest refinementRequest,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var refinedModel = model.Clone();

                foreach (var modification in refinementRequest.Modifications)
                {
                    switch (modification.Type)
                    {
                        case ModificationType.AddRoom:
                            AddRoomToModel(refinedModel, modification);
                            break;
                        case ModificationType.RemoveRoom:
                            RemoveRoomFromModel(refinedModel, modification);
                            break;
                        case ModificationType.ResizeRoom:
                            ResizeRoomInModel(refinedModel, modification);
                            break;
                        case ModificationType.AddFeature:
                            AddFeatureToModel(refinedModel, modification);
                            break;
                        case ModificationType.ChangeLayout:
                            ChangeLayoutInModel(refinedModel, modification);
                            break;
                    }
                }

                return refinedModel;
            }, cancellationToken);
        }

        private void AddRoomToModel(GeneratedModel model, Modification modification)
        {
            // Add new room to the model
            var room = new GeneratedElement
            {
                ElementType = ElementType.Room,
                Name = modification.TargetName,
                Properties = new Dictionary<string, object>
                {
                    ["RoomType"] = modification.RoomType,
                    ["Area"] = modification.Area
                }
            };
            model.Elements.Add(room);
        }

        private void RemoveRoomFromModel(GeneratedModel model, Modification modification)
        {
            var room = model.Elements.FirstOrDefault(e =>
                e.ElementType == ElementType.Room && e.Name == modification.TargetName);
            if (room != null)
            {
                model.Elements.Remove(room);
            }
        }

        private void ResizeRoomInModel(GeneratedModel model, Modification modification)
        {
            var room = model.Elements.FirstOrDefault(e =>
                e.ElementType == ElementType.Room && e.Name == modification.TargetName);
            if (room != null)
            {
                room.Properties["Area"] = modification.Area;
            }
        }

        private void AddFeatureToModel(GeneratedModel model, Modification modification)
        {
            // Add feature like garage, balcony, etc.
        }

        private void ChangeLayoutInModel(GeneratedModel model, Modification modification)
        {
            // Change layout arrangement
        }

        #endregion
    }

    #region Prompt Parser

    /// <summary>
    /// Parses natural language prompts into structured building requests
    /// </summary>
    internal class PromptParser
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Patterns for extracting building information
        private static readonly Dictionary<string, Regex> Patterns = new()
        {
            // Building types
            ["house"] = new Regex(@"(?i)\b(house|home|residence|villa|bungalow|cottage)\b"),
            ["apartment"] = new Regex(@"(?i)\b(apartment|flat|condo|unit)\b"),
            ["office"] = new Regex(@"(?i)\b(office|workplace|workspace|commercial)\b"),
            ["retail"] = new Regex(@"(?i)\b(shop|store|retail|boutique)\b"),
            ["school"] = new Regex(@"(?i)\b(school|classroom|education)\b"),
            ["hotel"] = new Regex(@"(?i)\b(hotel|motel|inn|lodge)\b"),
            ["warehouse"] = new Regex(@"(?i)\b(warehouse|storage|depot)\b"),

            // Room counts
            ["bedroom_count"] = new Regex(@"(?i)(\d+)[\s-]*(bedroom|bed|br)\b"),
            ["bathroom_count"] = new Regex(@"(?i)(\d+)[\s-]*(bathroom|bath|wc)\b"),
            ["storey_count"] = new Regex(@"(?i)(\d+)[\s-]*(stor(?:e)?y|floor|level)\b"),

            // Features
            ["garage"] = new Regex(@"(?i)\b(garage|carport|car\s*port)\b"),
            ["garage_count"] = new Regex(@"(?i)(\d+)[\s-]*(car\s*)?garage\b"),
            ["basement"] = new Regex(@"(?i)\b(basement|cellar|underground)\b"),
            ["attic"] = new Regex(@"(?i)\b(attic|loft)\b"),
            ["balcony"] = new Regex(@"(?i)\b(balcon(?:y|ies)|terrace)\b"),
            ["pool"] = new Regex(@"(?i)\b(pool|swimming)\b"),
            ["garden"] = new Regex(@"(?i)\b(garden|yard|lawn)\b"),

            // Areas
            ["total_area"] = new Regex(@"(?i)(\d+(?:\.\d+)?)\s*(?:sq(?:uare)?\s*)?(?:m(?:et(?:er|re))?s?|m²)\b"),
            ["total_area_sqft"] = new Regex(@"(?i)(\d+(?:\.\d+)?)\s*(?:sq(?:uare)?\s*)?(?:f(?:ee)?t|ft)\b"),

            // Room specifications
            ["room_spec"] = new Regex(@"(?i)(large|small|spacious|compact|open[\s-]*plan|en[\s-]*suite)\s+(living|bed|bath|kitchen|dining)", RegexOptions.IgnoreCase),

            // Style
            ["modern"] = new Regex(@"(?i)\b(modern|contemporary|minimalist)\b"),
            ["traditional"] = new Regex(@"(?i)\b(traditional|classic|colonial)\b"),
            ["industrial"] = new Regex(@"(?i)\b(industrial|loft|warehouse[\s-]*style)\b"),

            // Layout preferences
            ["open_plan"] = new Regex(@"(?i)\b(open[\s-]*plan|open[\s-]*concept|open[\s-]*floor)\b"),
            ["separate_rooms"] = new Regex(@"(?i)\b(separate|individual|closed)\s+(room|space)s?\b")
        };

        public async Task<ParsedRequest> ParseAsync(string prompt, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                Logger.Debug("Parsing prompt: {0}", prompt);

                var request = new ParsedRequest
                {
                    OriginalPrompt = prompt,
                    BuildingType = ExtractBuildingType(prompt),
                    BedroomCount = ExtractCount(prompt, "bedroom_count"),
                    BathroomCount = ExtractCount(prompt, "bathroom_count"),
                    StoreyCount = ExtractCount(prompt, "storey_count") ?? 1,
                    Features = ExtractFeatures(prompt),
                    TotalArea = ExtractArea(prompt),
                    Style = ExtractStyle(prompt),
                    LayoutPreferences = ExtractLayoutPreferences(prompt),
                    RoomSpecifications = ExtractRoomSpecifications(prompt)
                };

                // Set defaults based on building type
                ApplyDefaults(request);

                // Validate request
                ValidateRequest(request);

                Logger.Debug("Parsed request: {0}", JsonConvert.SerializeObject(request));
                return request;
            }, cancellationToken);
        }

        public async Task<ParsedRequest> ParseRefinementAsync(
            string refinementPrompt,
            ParsedRequest originalRequest,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var request = originalRequest.Clone();
                request.OriginalPrompt = refinementPrompt;
                request.IsRefinement = true;

                // Parse modifications
                var modifications = ExtractModifications(refinementPrompt);
                request.Modifications.AddRange(modifications);

                return request;
            }, cancellationToken);
        }

        private BuildingType ExtractBuildingType(string prompt)
        {
            if (Patterns["house"].IsMatch(prompt)) return BuildingType.SingleFamilyHouse;
            if (Patterns["apartment"].IsMatch(prompt)) return BuildingType.Apartment;
            if (Patterns["office"].IsMatch(prompt)) return BuildingType.Office;
            if (Patterns["retail"].IsMatch(prompt)) return BuildingType.Retail;
            if (Patterns["school"].IsMatch(prompt)) return BuildingType.School;
            if (Patterns["hotel"].IsMatch(prompt)) return BuildingType.Hotel;
            if (Patterns["warehouse"].IsMatch(prompt)) return BuildingType.Warehouse;

            return BuildingType.SingleFamilyHouse; // Default
        }

        private int? ExtractCount(string prompt, string patternKey)
        {
            var match = Patterns[patternKey].Match(prompt);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
            {
                return count;
            }
            return null;
        }

        private List<BuildingFeature> ExtractFeatures(string prompt)
        {
            var features = new List<BuildingFeature>();

            if (Patterns["garage"].IsMatch(prompt))
            {
                var count = ExtractCount(prompt, "garage_count") ?? 1;
                features.Add(new BuildingFeature
                {
                    Type = FeatureType.Garage,
                    Count = count,
                    Size = count == 1 ? "single" : count == 2 ? "double" : "triple"
                });
            }

            if (Patterns["basement"].IsMatch(prompt))
            {
                features.Add(new BuildingFeature { Type = FeatureType.Basement });
            }

            if (Patterns["attic"].IsMatch(prompt))
            {
                features.Add(new BuildingFeature { Type = FeatureType.Attic });
            }

            if (Patterns["balcony"].IsMatch(prompt))
            {
                features.Add(new BuildingFeature { Type = FeatureType.Balcony });
            }

            if (Patterns["pool"].IsMatch(prompt))
            {
                features.Add(new BuildingFeature { Type = FeatureType.Pool });
            }

            if (Patterns["garden"].IsMatch(prompt))
            {
                features.Add(new BuildingFeature { Type = FeatureType.Garden });
            }

            return features;
        }

        private double? ExtractArea(string prompt)
        {
            var match = Patterns["total_area"].Match(prompt);
            if (match.Success && double.TryParse(match.Groups[1].Value, out double area))
            {
                return area;
            }

            match = Patterns["total_area_sqft"].Match(prompt);
            if (match.Success && double.TryParse(match.Groups[1].Value, out double sqft))
            {
                return sqft * 0.0929; // Convert to m²
            }

            return null;
        }

        private ArchitecturalStyle ExtractStyle(string prompt)
        {
            if (Patterns["modern"].IsMatch(prompt)) return ArchitecturalStyle.Modern;
            if (Patterns["traditional"].IsMatch(prompt)) return ArchitecturalStyle.Traditional;
            if (Patterns["industrial"].IsMatch(prompt)) return ArchitecturalStyle.Industrial;

            return ArchitecturalStyle.Modern; // Default
        }

        private LayoutPreferences ExtractLayoutPreferences(string prompt)
        {
            var prefs = new LayoutPreferences();

            if (Patterns["open_plan"].IsMatch(prompt))
            {
                prefs.OpenPlan = true;
                prefs.OpenPlanAreas.Add(RoomType.LivingRoom);
                prefs.OpenPlanAreas.Add(RoomType.Kitchen);
                prefs.OpenPlanAreas.Add(RoomType.DiningRoom);
            }

            if (Patterns["separate_rooms"].IsMatch(prompt))
            {
                prefs.OpenPlan = false;
            }

            return prefs;
        }

        private List<RoomSpecification> ExtractRoomSpecifications(string prompt)
        {
            var specs = new List<RoomSpecification>();

            foreach (Match match in Patterns["room_spec"].Matches(prompt))
            {
                var modifier = match.Groups[1].Value.ToLower();
                var roomType = ParseRoomType(match.Groups[2].Value);

                var spec = new RoomSpecification
                {
                    RoomType = roomType,
                    SizeModifier = modifier switch
                    {
                        "large" or "spacious" => SizeModifier.Large,
                        "small" or "compact" => SizeModifier.Small,
                        _ => SizeModifier.Standard
                    },
                    Features = new List<string>()
                };

                if (modifier.Contains("en") && modifier.Contains("suite"))
                {
                    spec.Features.Add("ensuite");
                }

                if (modifier.Contains("open"))
                {
                    spec.Features.Add("open_plan");
                }

                specs.Add(spec);
            }

            return specs;
        }

        private RoomType ParseRoomType(string text)
        {
            return text.ToLower() switch
            {
                "living" => RoomType.LivingRoom,
                "bed" or "bedroom" => RoomType.Bedroom,
                "bath" or "bathroom" => RoomType.Bathroom,
                "kitchen" => RoomType.Kitchen,
                "dining" => RoomType.DiningRoom,
                _ => RoomType.Unknown
            };
        }

        private List<Modification> ExtractModifications(string prompt)
        {
            var modifications = new List<Modification>();

            // Parse "add" requests
            var addPattern = new Regex(@"(?i)add\s+(?:a\s+)?(.+?)(?:\s+room|\s*$)");
            foreach (Match match in addPattern.Matches(prompt))
            {
                modifications.Add(new Modification
                {
                    Type = ModificationType.AddRoom,
                    TargetName = match.Groups[1].Value.Trim()
                });
            }

            // Parse "remove" requests
            var removePattern = new Regex(@"(?i)remove\s+(?:the\s+)?(.+?)(?:\s+room|\s*$)");
            foreach (Match match in removePattern.Matches(prompt))
            {
                modifications.Add(new Modification
                {
                    Type = ModificationType.RemoveRoom,
                    TargetName = match.Groups[1].Value.Trim()
                });
            }

            // Parse "make bigger/smaller" requests
            var resizePattern = new Regex(@"(?i)make\s+(?:the\s+)?(.+?)\s+(bigger|larger|smaller)");
            foreach (Match match in resizePattern.Matches(prompt))
            {
                modifications.Add(new Modification
                {
                    Type = ModificationType.ResizeRoom,
                    TargetName = match.Groups[1].Value.Trim(),
                    SizeModifier = match.Groups[2].Value.ToLower() == "smaller" ? SizeModifier.Small : SizeModifier.Large
                });
            }

            return modifications;
        }

        private void ApplyDefaults(ParsedRequest request)
        {
            // Apply bedroom defaults based on building type
            if (!request.BedroomCount.HasValue)
            {
                request.BedroomCount = request.BuildingType switch
                {
                    BuildingType.SingleFamilyHouse => 3,
                    BuildingType.Apartment => 2,
                    _ => null
                };
            }

            // Apply bathroom defaults
            if (!request.BathroomCount.HasValue && request.BedroomCount.HasValue)
            {
                request.BathroomCount = Math.Max(1, request.BedroomCount.Value / 2 + 1);
            }
        }

        private void ValidateRequest(ParsedRequest request)
        {
            if (request.BedroomCount.HasValue && request.BedroomCount.Value > 20)
            {
                request.ValidationErrors.Add("Bedroom count exceeds reasonable maximum (20)");
            }

            if (request.StoreyCount > 100)
            {
                request.ValidationErrors.Add("Storey count exceeds reasonable maximum (100)");
            }

            if (request.TotalArea.HasValue && request.TotalArea.Value > 100000)
            {
                request.ValidationErrors.Add("Total area exceeds reasonable maximum (100,000 m²)");
            }

            request.IsValid = request.ValidationErrors.Count == 0;
        }
    }

    #endregion

    #region Building Program Generator

    /// <summary>
    /// Generates detailed building program from parsed request
    /// </summary>
    internal class BuildingProgramGenerator
    {
        public async Task<BuildingProgram> GenerateProgramAsync(
            ParsedRequest request,
            Dictionary<BuildingType, BuildingTemplate> templates,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var template = templates.GetValueOrDefault(request.BuildingType) ??
                               templates[BuildingType.SingleFamilyHouse];

                var program = new BuildingProgram
                {
                    BuildingType = request.BuildingType,
                    Style = request.Style,
                    StoreyCount = request.StoreyCount,
                    CeilingHeight = template.DefaultCeilingHeight,
                    WallThickness = template.DefaultWallThickness
                };

                // Generate room list
                program.Rooms = GenerateRoomList(request, template);

                // Calculate total area
                program.TotalArea = request.TotalArea ??
                    program.Rooms.Sum(r => r.TargetArea);

                // Add features
                program.Features = request.Features;

                // Generate levels
                program.Levels = GenerateLevels(program, template);

                return program;
            }, cancellationToken);
        }

        private List<ProgrammedRoom> GenerateRoomList(ParsedRequest request, BuildingTemplate template)
        {
            var rooms = new List<ProgrammedRoom>();
            int roomId = 1;

            // Add required rooms from template
            foreach (var roomTemplate in template.DefaultRooms.Where(r => r.Required))
            {
                rooms.Add(new ProgrammedRoom
                {
                    Id = roomId++,
                    Type = roomTemplate.Type,
                    Name = GetRoomName(roomTemplate.Type, rooms),
                    MinArea = roomTemplate.MinArea,
                    MaxArea = roomTemplate.MaxArea,
                    TargetArea = (roomTemplate.MinArea + roomTemplate.MaxArea) / 2,
                    Level = 0
                });
            }

            // Add bedrooms based on count
            if (request.BedroomCount.HasValue)
            {
                var bedroomTemplate = template.DefaultRooms.FirstOrDefault(r => r.Type == RoomType.Bedroom) ??
                    new RoomTemplate { Type = RoomType.Bedroom, MinArea = 10, MaxArea = 20 };

                // Check if we already have a master bedroom
                var hasMaster = rooms.Any(r => r.Type == RoomType.MasterBedroom);

                for (int i = 0; i < request.BedroomCount.Value; i++)
                {
                    var isMaster = !hasMaster && i == 0;
                    var type = isMaster ? RoomType.MasterBedroom : RoomType.Bedroom;
                    var existingMaster = rooms.FirstOrDefault(r => r.Type == RoomType.MasterBedroom);

                    if (isMaster && existingMaster != null)
                    {
                        continue; // Skip, already have master
                    }

                    rooms.Add(new ProgrammedRoom
                    {
                        Id = roomId++,
                        Type = type,
                        Name = isMaster ? "Master Bedroom" : $"Bedroom {rooms.Count(r => r.Type == RoomType.Bedroom) + 1}",
                        MinArea = isMaster ? bedroomTemplate.MinArea * 1.5 : bedroomTemplate.MinArea,
                        MaxArea = isMaster ? bedroomTemplate.MaxArea * 1.5 : bedroomTemplate.MaxArea,
                        TargetArea = isMaster ? (bedroomTemplate.MinArea + bedroomTemplate.MaxArea) / 2 * 1.5 : (bedroomTemplate.MinArea + bedroomTemplate.MaxArea) / 2,
                        Level = request.StoreyCount > 1 ? 1 : 0 // Bedrooms on upper level if multi-storey
                    });
                }
            }

            // Add bathrooms based on count
            if (request.BathroomCount.HasValue)
            {
                var bathroomTemplate = template.DefaultRooms.FirstOrDefault(r => r.Type == RoomType.Bathroom) ??
                    new RoomTemplate { Type = RoomType.Bathroom, MinArea = 4, MaxArea = 8 };

                // Already have master bathroom, add regular bathrooms
                var existingBathrooms = rooms.Count(r => r.Type == RoomType.Bathroom || r.Type == RoomType.MasterBathroom);
                var bathroomsToAdd = request.BathroomCount.Value - existingBathrooms;

                for (int i = 0; i < bathroomsToAdd; i++)
                {
                    rooms.Add(new ProgrammedRoom
                    {
                        Id = roomId++,
                        Type = RoomType.Bathroom,
                        Name = $"Bathroom {rooms.Count(r => r.Type == RoomType.Bathroom) + 1}",
                        MinArea = bathroomTemplate.MinArea,
                        MaxArea = bathroomTemplate.MaxArea,
                        TargetArea = (bathroomTemplate.MinArea + bathroomTemplate.MaxArea) / 2,
                        Level = request.StoreyCount > 1 && i > 0 ? 1 : 0
                    });
                }
            }

            // Add corridor for multi-room layouts
            if (rooms.Count > 4)
            {
                rooms.Add(new ProgrammedRoom
                {
                    Id = roomId++,
                    Type = RoomType.Corridor,
                    Name = "Corridor",
                    MinArea = 5,
                    MaxArea = 20,
                    TargetArea = 10,
                    Level = 0
                });

                if (request.StoreyCount > 1)
                {
                    rooms.Add(new ProgrammedRoom
                    {
                        Id = roomId++,
                        Type = RoomType.Corridor,
                        Name = "Upper Corridor",
                        MinArea = 5,
                        MaxArea = 20,
                        TargetArea = 10,
                        Level = 1
                    });
                }
            }

            // Apply room specifications (large/small modifiers)
            foreach (var spec in request.RoomSpecifications)
            {
                var matchingRoom = rooms.FirstOrDefault(r => r.Type == spec.RoomType);
                if (matchingRoom != null)
                {
                    var modifier = spec.SizeModifier switch
                    {
                        SizeModifier.Large => 1.5,
                        SizeModifier.Small => 0.7,
                        _ => 1.0
                    };
                    matchingRoom.TargetArea *= modifier;
                    matchingRoom.MinArea *= modifier;
                    matchingRoom.MaxArea *= modifier;
                }
            }

            return rooms;
        }

        private string GetRoomName(RoomType type, List<ProgrammedRoom> existingRooms)
        {
            var count = existingRooms.Count(r => r.Type == type);
            var baseName = type.ToString().Replace("Room", " Room");

            // Add space before capital letters
            baseName = Regex.Replace(baseName, "([a-z])([A-Z])", "$1 $2");

            return count > 0 ? $"{baseName} {count + 1}" : baseName;
        }

        private List<ProgramLevel> GenerateLevels(BuildingProgram program, BuildingTemplate template)
        {
            var levels = new List<ProgramLevel>();

            for (int i = 0; i < program.StoreyCount; i++)
            {
                var level = new ProgramLevel
                {
                    Index = i,
                    Name = i == 0 ? "Ground Floor" : $"Level {i}",
                    Elevation = i * program.CeilingHeight,
                    Rooms = program.Rooms.Where(r => r.Level == i).ToList()
                };
                levels.Add(level);
            }

            // Add basement if requested
            if (program.Features.Any(f => f.Type == FeatureType.Basement))
            {
                levels.Insert(0, new ProgramLevel
                {
                    Index = -1,
                    Name = "Basement",
                    Elevation = -program.CeilingHeight,
                    Rooms = new List<ProgrammedRoom>
                    {
                        new ProgrammedRoom
                        {
                            Type = RoomType.Storage,
                            Name = "Basement Storage",
                            TargetArea = program.TotalArea / program.StoreyCount * 0.7,
                            Level = -1
                        }
                    }
                });
            }

            return levels;
        }
    }

    #endregion

    #region Space Planner

    /// <summary>
    /// Generates spatial layouts from building programs
    /// </summary>
    internal class SpacePlanner
    {
        private readonly PromptToModelSettings _settings;

        public SpacePlanner(PromptToModelSettings settings)
        {
            _settings = settings;
        }

        public async Task<SpaceLayout> GenerateLayoutAsync(
            BuildingProgram program,
            GenerationOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var layout = new SpaceLayout
                {
                    BuildingFootprint = CalculateBuildingFootprint(program),
                    LevelLayouts = new List<LevelLayout>()
                };

                foreach (var level in program.Levels)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var levelLayout = GenerateLevelLayout(level, layout.BuildingFootprint, program);
                    layout.LevelLayouts.Add(levelLayout);
                }

                return layout;
            }, cancellationToken);
        }

        private BuildingFootprint CalculateBuildingFootprint(BuildingProgram program)
        {
            // Calculate required footprint based on total area and storeys
            var footprintArea = program.TotalArea / program.StoreyCount;

            // Determine aspect ratio (typically between 1:1 and 2:1)
            var aspectRatio = program.BuildingType switch
            {
                BuildingType.SingleFamilyHouse => 1.4,
                BuildingType.Apartment => 1.8,
                BuildingType.Office => 1.5,
                _ => 1.5
            };

            var width = Math.Sqrt(footprintArea / aspectRatio);
            var depth = footprintArea / width;

            return new BuildingFootprint
            {
                Width = width * 1000, // Convert to mm
                Depth = depth * 1000,
                Area = footprintArea,
                Shape = FootprintShape.Rectangle,
                Origin = new Point(0, 0)
            };
        }

        private LevelLayout GenerateLevelLayout(ProgramLevel level, BuildingFootprint footprint, BuildingProgram program)
        {
            var layout = new LevelLayout
            {
                Level = level,
                Rooms = new List<RoomLayout>()
            };

            // Use bin packing algorithm to place rooms
            var placedRooms = PackRooms(level.Rooms, footprint);
            layout.Rooms = placedRooms;

            return layout;
        }

        private List<RoomLayout> PackRooms(List<ProgrammedRoom> rooms, BuildingFootprint footprint)
        {
            var layouts = new List<RoomLayout>();
            var availableArea = new List<AvailableArea>
            {
                new AvailableArea
                {
                    X = 0,
                    Y = 0,
                    Width = footprint.Width,
                    Height = footprint.Depth
                }
            };

            // Sort rooms by area (largest first)
            var sortedRooms = rooms.OrderByDescending(r => r.TargetArea).ToList();

            foreach (var room in sortedRooms)
            {
                var placement = FindPlacement(room, availableArea, footprint);
                if (placement != null)
                {
                    layouts.Add(placement);
                    UpdateAvailableArea(availableArea, placement);
                }
            }

            return layouts;
        }

        private RoomLayout FindPlacement(ProgrammedRoom room, List<AvailableArea> available, BuildingFootprint footprint)
        {
            // Calculate room dimensions from target area
            var aspectRatio = GetRoomAspectRatio(room.Type);
            var roomWidth = Math.Sqrt(room.TargetArea * 1000000 * aspectRatio); // Convert m² to mm²
            var roomDepth = room.TargetArea * 1000000 / roomWidth;

            // Round to grid
            roomWidth = Math.Round(roomWidth / _settings.GridSize) * _settings.GridSize;
            roomDepth = Math.Round(roomDepth / _settings.GridSize) * _settings.GridSize;

            // Try to find placement in available areas
            foreach (var area in available.OrderByDescending(a => a.Width * a.Height))
            {
                if (area.Width >= roomWidth && area.Height >= roomDepth)
                {
                    return new RoomLayout
                    {
                        Room = room,
                        X = area.X,
                        Y = area.Y,
                        Width = roomWidth,
                        Depth = roomDepth,
                        Area = (roomWidth * roomDepth) / 1000000 // Back to m²
                    };
                }

                // Try rotated
                if (area.Width >= roomDepth && area.Height >= roomWidth)
                {
                    return new RoomLayout
                    {
                        Room = room,
                        X = area.X,
                        Y = area.Y,
                        Width = roomDepth,
                        Depth = roomWidth,
                        Area = (roomWidth * roomDepth) / 1000000
                    };
                }
            }

            // If no fit, force placement with reduced size
            var largestArea = available.OrderByDescending(a => a.Width * a.Height).First();
            var fitWidth = Math.Min(roomWidth, largestArea.Width);
            var fitDepth = Math.Min(roomDepth, largestArea.Height);

            return new RoomLayout
            {
                Room = room,
                X = largestArea.X,
                Y = largestArea.Y,
                Width = fitWidth,
                Depth = fitDepth,
                Area = (fitWidth * fitDepth) / 1000000
            };
        }

        private double GetRoomAspectRatio(RoomType type)
        {
            return type switch
            {
                RoomType.LivingRoom => 1.4,
                RoomType.Bedroom or RoomType.MasterBedroom => 1.3,
                RoomType.Kitchen => 1.2,
                RoomType.Bathroom or RoomType.MasterBathroom => 1.5,
                RoomType.Corridor => 4.0,
                RoomType.Entrance => 1.0,
                _ => 1.3
            };
        }

        private void UpdateAvailableArea(List<AvailableArea> available, RoomLayout placed)
        {
            var toRemove = new List<AvailableArea>();
            var toAdd = new List<AvailableArea>();

            foreach (var area in available)
            {
                if (AreaOverlaps(area, placed))
                {
                    toRemove.Add(area);

                    // Split remaining area
                    // Right of placed room
                    if (area.X + area.Width > placed.X + placed.Width)
                    {
                        toAdd.Add(new AvailableArea
                        {
                            X = placed.X + placed.Width,
                            Y = area.Y,
                            Width = area.X + area.Width - (placed.X + placed.Width),
                            Height = area.Height
                        });
                    }

                    // Below placed room
                    if (area.Y + area.Height > placed.Y + placed.Depth)
                    {
                        toAdd.Add(new AvailableArea
                        {
                            X = area.X,
                            Y = placed.Y + placed.Depth,
                            Width = area.Width,
                            Height = area.Y + area.Height - (placed.Y + placed.Depth)
                        });
                    }
                }
            }

            foreach (var remove in toRemove)
            {
                available.Remove(remove);
            }

            available.AddRange(toAdd);
        }

        private bool AreaOverlaps(AvailableArea area, RoomLayout room)
        {
            return !(area.X >= room.X + room.Width ||
                     area.X + area.Width <= room.X ||
                     area.Y >= room.Y + room.Depth ||
                     area.Y + area.Height <= room.Y);
        }
    }

    #endregion

    #region Architectural Generator

    /// <summary>
    /// Generates architectural elements from space layouts
    /// </summary>
    internal class ArchitecturalGenerator
    {
        private readonly PromptToModelSettings _settings;

        public ArchitecturalGenerator(PromptToModelSettings settings)
        {
            _settings = settings;
        }

        public async Task<GeneratedModel> GenerateModelAsync(
            SpaceLayout layout,
            BuildingProgram program,
            GenerationOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var model = new GeneratedModel
                {
                    Levels = new List<GeneratedLevel>(),
                    Elements = new List<GeneratedElement>()
                };

                // Generate levels
                foreach (var levelLayout in layout.LevelLayouts)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var level = new GeneratedLevel
                    {
                        Name = levelLayout.Level.Name,
                        Elevation = levelLayout.Level.Elevation,
                        Height = program.CeilingHeight
                    };
                    model.Levels.Add(level);

                    // Generate walls
                    var walls = GenerateWalls(levelLayout, program, level);
                    model.Elements.AddRange(walls);

                    // Generate doors
                    var doors = GenerateDoors(levelLayout, walls, level);
                    model.Elements.AddRange(doors);

                    // Generate windows
                    var windows = GenerateWindows(levelLayout, walls, level);
                    model.Elements.AddRange(windows);

                    // Generate rooms
                    var rooms = GenerateRooms(levelLayout, level);
                    model.Elements.AddRange(rooms);

                    // Generate floor
                    var floor = GenerateFloor(levelLayout, level, program);
                    model.Elements.Add(floor);

                    // Generate ceiling
                    var ceiling = GenerateCeiling(levelLayout, level, program);
                    model.Elements.Add(ceiling);
                }

                // Generate stairs if multi-level
                if (model.Levels.Count > 1)
                {
                    var stairs = GenerateStairs(layout, model.Levels, program);
                    model.Elements.AddRange(stairs);
                }

                // Generate roof
                var roof = GenerateRoof(layout, program, model.Levels.Last());
                model.Elements.AddRange(roof);

                // Generate features (garage, etc.)
                foreach (var feature in program.Features)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var featureElements = GenerateFeature(feature, layout, program);
                    model.Elements.AddRange(featureElements);
                }

                return model;
            }, cancellationToken);
        }

        private List<GeneratedElement> GenerateWalls(LevelLayout levelLayout, BuildingProgram program, GeneratedLevel level)
        {
            var walls = new List<GeneratedElement>();
            var wallId = 1;

            // Generate perimeter walls (exterior)
            var perimeterWalls = GeneratePerimeterWalls(levelLayout, program, level, ref wallId);
            walls.AddRange(perimeterWalls);

            // Generate interior walls between rooms
            var interiorWalls = GenerateInteriorWalls(levelLayout, program, level, ref wallId);
            walls.AddRange(interiorWalls);

            return walls;
        }

        private List<GeneratedElement> GeneratePerimeterWalls(
            LevelLayout levelLayout,
            BuildingProgram program,
            GeneratedLevel level,
            ref int wallId)
        {
            var walls = new List<GeneratedElement>();

            // Get bounding box of all rooms
            var minX = levelLayout.Rooms.Min(r => r.X);
            var minY = levelLayout.Rooms.Min(r => r.Y);
            var maxX = levelLayout.Rooms.Max(r => r.X + r.Width);
            var maxY = levelLayout.Rooms.Max(r => r.Y + r.Depth);

            var thickness = program.WallThickness;
            var height = program.CeilingHeight;

            // Bottom wall
            walls.Add(CreateWall($"EW{wallId++}", minX, minY, maxX, minY, thickness, height, level, true));
            // Top wall
            walls.Add(CreateWall($"EW{wallId++}", minX, maxY, maxX, maxY, thickness, height, level, true));
            // Left wall
            walls.Add(CreateWall($"EW{wallId++}", minX, minY, minX, maxY, thickness, height, level, true));
            // Right wall
            walls.Add(CreateWall($"EW{wallId++}", maxX, minY, maxX, maxY, thickness, height, level, true));

            return walls;
        }

        private List<GeneratedElement> GenerateInteriorWalls(
            LevelLayout levelLayout,
            BuildingProgram program,
            GeneratedLevel level,
            ref int wallId)
        {
            var walls = new List<GeneratedElement>();
            var thickness = program.WallThickness * 0.75; // Interior walls thinner
            var height = program.CeilingHeight;

            // Generate walls along room boundaries
            foreach (var room in levelLayout.Rooms)
            {
                // Check each edge of the room
                var edges = new[]
                {
                    (room.X, room.Y, room.X + room.Width, room.Y), // Bottom
                    (room.X, room.Y + room.Depth, room.X + room.Width, room.Y + room.Depth), // Top
                    (room.X, room.Y, room.X, room.Y + room.Depth), // Left
                    (room.X + room.Width, room.Y, room.X + room.Width, room.Y + room.Depth) // Right
                };

                foreach (var edge in edges)
                {
                    // Check if edge is shared with another room
                    var isShared = IsSharedEdge(edge, room, levelLayout.Rooms);
                    var isPerimeter = IsPerimeterEdge(edge, levelLayout);

                    if (isShared && !isPerimeter && !WallExists(walls, edge))
                    {
                        walls.Add(CreateWall(
                            $"IW{wallId++}",
                            edge.Item1, edge.Item2,
                            edge.Item3, edge.Item4,
                            thickness, height, level, false));
                    }
                }
            }

            return walls;
        }

        private GeneratedElement CreateWall(
            string id, double x1, double y1, double x2, double y2,
            double thickness, double height, GeneratedLevel level, bool isExterior)
        {
            return new GeneratedElement
            {
                Id = id,
                ElementType = ElementType.Wall,
                Level = level,
                Location = new Point((x1 + x2) / 2, (y1 + y2) / 2),
                StartPoint = new Point(x1, y1),
                EndPoint = new Point(x2, y2),
                Properties = new Dictionary<string, object>
                {
                    ["Thickness"] = thickness,
                    ["Height"] = height,
                    ["IsExterior"] = isExterior,
                    ["Length"] = Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2))
                }
            };
        }

        private bool IsSharedEdge((double, double, double, double) edge, RoomLayout room, List<RoomLayout> allRooms)
        {
            foreach (var other in allRooms)
            {
                if (other == room) continue;

                // Check if this edge is adjacent to another room
                if (EdgesAdjacent(edge, other))
                    return true;
            }
            return false;
        }

        private bool EdgesAdjacent((double, double, double, double) edge, RoomLayout room)
        {
            var (x1, y1, x2, y2) = edge;

            // Check if edge is along room boundary
            return (Math.Abs(x1 - room.X) < 1 || Math.Abs(x1 - (room.X + room.Width)) < 1 ||
                    Math.Abs(y1 - room.Y) < 1 || Math.Abs(y1 - (room.Y + room.Depth)) < 1);
        }

        private bool IsPerimeterEdge((double, double, double, double) edge, LevelLayout levelLayout)
        {
            var minX = levelLayout.Rooms.Min(r => r.X);
            var minY = levelLayout.Rooms.Min(r => r.Y);
            var maxX = levelLayout.Rooms.Max(r => r.X + r.Width);
            var maxY = levelLayout.Rooms.Max(r => r.Y + r.Depth);

            var (x1, y1, x2, y2) = edge;

            return (Math.Abs(x1 - minX) < 1 && Math.Abs(x2 - minX) < 1) ||
                   (Math.Abs(x1 - maxX) < 1 && Math.Abs(x2 - maxX) < 1) ||
                   (Math.Abs(y1 - minY) < 1 && Math.Abs(y2 - minY) < 1) ||
                   (Math.Abs(y1 - maxY) < 1 && Math.Abs(y2 - maxY) < 1);
        }

        private bool WallExists(List<GeneratedElement> walls, (double, double, double, double) edge)
        {
            var (x1, y1, x2, y2) = edge;

            return walls.Any(w =>
            {
                var ws = w.StartPoint;
                var we = w.EndPoint;
                return (Math.Abs(ws.X - x1) < 1 && Math.Abs(ws.Y - y1) < 1 &&
                        Math.Abs(we.X - x2) < 1 && Math.Abs(we.Y - y2) < 1) ||
                       (Math.Abs(ws.X - x2) < 1 && Math.Abs(ws.Y - y2) < 1 &&
                        Math.Abs(we.X - x1) < 1 && Math.Abs(we.Y - y1) < 1);
            });
        }

        private List<GeneratedElement> GenerateDoors(
            LevelLayout levelLayout,
            List<GeneratedElement> walls,
            GeneratedLevel level)
        {
            var doors = new List<GeneratedElement>();
            var doorId = 1;

            foreach (var room in levelLayout.Rooms)
            {
                // Every room needs at least one door
                var interiorWall = FindInteriorWallForRoom(room, walls);
                if (interiorWall != null)
                {
                    var doorWidth = room.Room.Type == RoomType.Bathroom ? 700 : 900;
                    var door = CreateDoor($"D{doorId++}", interiorWall, level, doorWidth, 2100);
                    doors.Add(door);
                }
            }

            // Add entrance door
            var entranceWall = walls.FirstOrDefault(w => (bool)w.Properties["IsExterior"]);
            if (entranceWall != null)
            {
                var entranceDoor = CreateDoor("D_ENT", entranceWall, level, 1000, 2100);
                entranceDoor.Properties["DoorType"] = "Entrance";
                doors.Add(entranceDoor);
            }

            return doors;
        }

        private GeneratedElement FindInteriorWallForRoom(RoomLayout room, List<GeneratedElement> walls)
        {
            foreach (var wall in walls.Where(w => !(bool)w.Properties["IsExterior"]))
            {
                // Check if wall is adjacent to room
                var wallCenter = wall.Location;
                if (Math.Abs(wallCenter.X - room.X) < room.Width + 100 &&
                    Math.Abs(wallCenter.Y - room.Y) < room.Depth + 100)
                {
                    return wall;
                }
            }

            return walls.FirstOrDefault(w => !(bool)w.Properties["IsExterior"]);
        }

        private GeneratedElement CreateDoor(
            string id,
            GeneratedElement hostWall,
            GeneratedLevel level,
            double width,
            double height)
        {
            var wallMid = new Point(
                (hostWall.StartPoint.X + hostWall.EndPoint.X) / 2,
                (hostWall.StartPoint.Y + hostWall.EndPoint.Y) / 2);

            return new GeneratedElement
            {
                Id = id,
                ElementType = ElementType.Door,
                Level = level,
                Location = wallMid,
                HostElement = hostWall,
                Properties = new Dictionary<string, object>
                {
                    ["Width"] = width,
                    ["Height"] = height,
                    ["DoorType"] = "Single"
                }
            };
        }

        private List<GeneratedElement> GenerateWindows(
            LevelLayout levelLayout,
            List<GeneratedElement> walls,
            GeneratedLevel level)
        {
            var windows = new List<GeneratedElement>();
            var windowId = 1;

            var exteriorWalls = walls.Where(w => (bool)w.Properties["IsExterior"]).ToList();

            foreach (var wall in exteriorWalls)
            {
                var wallLength = (double)wall.Properties["Length"];
                var windowCount = (int)(wallLength / 3000); // One window per 3m

                for (int i = 0; i < windowCount; i++)
                {
                    var t = (i + 0.5) / windowCount;
                    var windowLocation = new Point(
                        wall.StartPoint.X + t * (wall.EndPoint.X - wall.StartPoint.X),
                        wall.StartPoint.Y + t * (wall.EndPoint.Y - wall.StartPoint.Y));

                    windows.Add(new GeneratedElement
                    {
                        Id = $"W{windowId++}",
                        ElementType = ElementType.Window,
                        Level = level,
                        Location = windowLocation,
                        HostElement = wall,
                        Properties = new Dictionary<string, object>
                        {
                            ["Width"] = 1200,
                            ["Height"] = 1400,
                            ["SillHeight"] = 900,
                            ["WindowType"] = "Casement"
                        }
                    });
                }
            }

            return windows;
        }

        private List<GeneratedElement> GenerateRooms(LevelLayout levelLayout, GeneratedLevel level)
        {
            var rooms = new List<GeneratedElement>();

            foreach (var roomLayout in levelLayout.Rooms)
            {
                rooms.Add(new GeneratedElement
                {
                    Id = $"R{roomLayout.Room.Id}",
                    ElementType = ElementType.Room,
                    Name = roomLayout.Room.Name,
                    Level = level,
                    Location = new Point(roomLayout.X + roomLayout.Width / 2, roomLayout.Y + roomLayout.Depth / 2),
                    Properties = new Dictionary<string, object>
                    {
                        ["RoomType"] = roomLayout.Room.Type.ToString(),
                        ["Area"] = roomLayout.Area,
                        ["Width"] = roomLayout.Width,
                        ["Depth"] = roomLayout.Depth,
                        ["X"] = roomLayout.X,
                        ["Y"] = roomLayout.Y
                    }
                });
            }

            return rooms;
        }

        private GeneratedElement GenerateFloor(LevelLayout levelLayout, GeneratedLevel level, BuildingProgram program)
        {
            var minX = levelLayout.Rooms.Min(r => r.X);
            var minY = levelLayout.Rooms.Min(r => r.Y);
            var maxX = levelLayout.Rooms.Max(r => r.X + r.Width);
            var maxY = levelLayout.Rooms.Max(r => r.Y + r.Depth);

            return new GeneratedElement
            {
                Id = $"FL_{level.Name.Replace(" ", "_")}",
                ElementType = ElementType.Floor,
                Level = level,
                Location = new Point((minX + maxX) / 2, (minY + maxY) / 2),
                Properties = new Dictionary<string, object>
                {
                    ["Boundary"] = new List<Point>
                    {
                        new Point(minX, minY),
                        new Point(maxX, minY),
                        new Point(maxX, maxY),
                        new Point(minX, maxY)
                    },
                    ["Thickness"] = 200,
                    ["Area"] = (maxX - minX) * (maxY - minY) / 1000000
                }
            };
        }

        private GeneratedElement GenerateCeiling(LevelLayout levelLayout, GeneratedLevel level, BuildingProgram program)
        {
            var minX = levelLayout.Rooms.Min(r => r.X);
            var minY = levelLayout.Rooms.Min(r => r.Y);
            var maxX = levelLayout.Rooms.Max(r => r.X + r.Width);
            var maxY = levelLayout.Rooms.Max(r => r.Y + r.Depth);

            return new GeneratedElement
            {
                Id = $"CL_{level.Name.Replace(" ", "_")}",
                ElementType = ElementType.Ceiling,
                Level = level,
                Location = new Point((minX + maxX) / 2, (minY + maxY) / 2),
                Properties = new Dictionary<string, object>
                {
                    ["Height"] = program.CeilingHeight - 100,
                    ["Area"] = (maxX - minX) * (maxY - minY) / 1000000
                }
            };
        }

        private List<GeneratedElement> GenerateStairs(SpaceLayout layout, List<GeneratedLevel> levels, BuildingProgram program)
        {
            var stairs = new List<GeneratedElement>();

            for (int i = 0; i < levels.Count - 1; i++)
            {
                var lowerLevel = levels[i];
                var upperLevel = levels[i + 1];

                stairs.Add(new GeneratedElement
                {
                    Id = $"ST_{i}",
                    ElementType = ElementType.Stair,
                    Level = lowerLevel,
                    Location = new Point(layout.BuildingFootprint.Width / 2, layout.BuildingFootprint.Depth / 2),
                    Properties = new Dictionary<string, object>
                    {
                        ["BaseLevel"] = lowerLevel.Name,
                        ["TopLevel"] = upperLevel.Name,
                        ["Height"] = upperLevel.Elevation - lowerLevel.Elevation,
                        ["Width"] = 1000,
                        ["Treads"] = 16,
                        ["RiserHeight"] = (upperLevel.Elevation - lowerLevel.Elevation) / 16
                    }
                });
            }

            return stairs;
        }

        private List<GeneratedElement> GenerateRoof(SpaceLayout layout, BuildingProgram program, GeneratedLevel topLevel)
        {
            var roofElements = new List<GeneratedElement>();

            var roofStyle = program.Style == ArchitecturalStyle.Modern ? "Flat" : "Pitched";

            roofElements.Add(new GeneratedElement
            {
                Id = "ROOF",
                ElementType = ElementType.Roof,
                Level = topLevel,
                Location = new Point(layout.BuildingFootprint.Width / 2, layout.BuildingFootprint.Depth / 2),
                Properties = new Dictionary<string, object>
                {
                    ["RoofType"] = roofStyle,
                    ["Width"] = layout.BuildingFootprint.Width,
                    ["Depth"] = layout.BuildingFootprint.Depth,
                    ["Pitch"] = roofStyle == "Pitched" ? 30.0 : 0.0,
                    ["Overhang"] = 500
                }
            });

            return roofElements;
        }

        private List<GeneratedElement> GenerateFeature(BuildingFeature feature, SpaceLayout layout, BuildingProgram program)
        {
            var elements = new List<GeneratedElement>();

            switch (feature.Type)
            {
                case FeatureType.Garage:
                    elements.AddRange(GenerateGarage(feature, layout, program));
                    break;
                case FeatureType.Balcony:
                    elements.AddRange(GenerateBalcony(feature, layout, program));
                    break;
                    // Add more feature types as needed
            }

            return elements;
        }

        private List<GeneratedElement> GenerateGarage(BuildingFeature feature, SpaceLayout layout, BuildingProgram program)
        {
            var elements = new List<GeneratedElement>();

            var garageWidth = feature.Count * 3000; // 3m per car
            var garageDepth = 6000; // 6m depth

            // Position garage adjacent to main building
            var garageX = layout.BuildingFootprint.Width;
            var garageY = 0;

            var garageLevel = new GeneratedLevel
            {
                Name = "Garage",
                Elevation = 0,
                Height = program.CeilingHeight
            };

            // Garage walls
            elements.Add(CreateWall("GW1", garageX, garageY, garageX + garageWidth, garageY,
                program.WallThickness, program.CeilingHeight, garageLevel, true));
            elements.Add(CreateWall("GW2", garageX + garageWidth, garageY, garageX + garageWidth, garageY + garageDepth,
                program.WallThickness, program.CeilingHeight, garageLevel, true));
            elements.Add(CreateWall("GW3", garageX, garageY + garageDepth, garageX + garageWidth, garageY + garageDepth,
                program.WallThickness, program.CeilingHeight, garageLevel, true));

            // Garage door
            elements.Add(new GeneratedElement
            {
                Id = "GD1",
                ElementType = ElementType.Door,
                Level = garageLevel,
                Location = new Point(garageX + garageWidth / 2, garageY),
                Properties = new Dictionary<string, object>
                {
                    ["DoorType"] = "GarageDoor",
                    ["Width"] = feature.Count * 2400,
                    ["Height"] = 2400
                }
            });

            return elements;
        }

        private List<GeneratedElement> GenerateBalcony(BuildingFeature feature, SpaceLayout layout, BuildingProgram program)
        {
            var elements = new List<GeneratedElement>();

            // Add balcony elements
            // ...

            return elements;
        }
    }

    #endregion

    #region Design Rules Engine

    /// <summary>
    /// Applies architectural design rules to optimize layouts
    /// </summary>
    internal class DesignRulesEngine
    {
        public async Task<SpaceLayout> OptimizeLayoutAsync(
            SpaceLayout layout,
            BuildingProgram program,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var optimized = layout;

                // Apply circulation rules
                optimized = ApplyCirculationRules(optimized);

                // Apply adjacency rules
                optimized = ApplyAdjacencyRules(optimized, program);

                // Apply natural light rules
                optimized = ApplyNaturalLightRules(optimized);

                // Apply privacy rules
                optimized = ApplyPrivacyRules(optimized);

                return optimized;
            }, cancellationToken);
        }

        private SpaceLayout ApplyCirculationRules(SpaceLayout layout)
        {
            // Ensure corridors connect all rooms
            // Ensure minimum corridor widths
            return layout;
        }

        private SpaceLayout ApplyAdjacencyRules(SpaceLayout layout, BuildingProgram program)
        {
            // Kitchen near dining
            // Master bedroom near master bathroom
            // Entrance near living room
            return layout;
        }

        private SpaceLayout ApplyNaturalLightRules(SpaceLayout layout)
        {
            // Living rooms on exterior
            // Bedrooms with windows
            return layout;
        }

        private SpaceLayout ApplyPrivacyRules(SpaceLayout layout)
        {
            // Bedrooms away from entrance
            // Bathrooms not directly off living areas
            return layout;
        }
    }

    #endregion

    #region Data Models

    // Settings
    public class PromptToModelSettings
    {
        public double MinRoomArea { get; set; } = 4.0; // m²
        public double GridSize { get; set; } = 100; // mm
        public double DefaultWallThickness { get; set; } = 200; // mm
        public double DefaultCeilingHeight { get; set; } = 2700; // mm
    }

    public class GenerationOptions
    {
        public double DefaultWallHeight { get; set; } = 2700;
        public bool GenerateInteriorDoors { get; set; } = true;
        public bool GenerateWindows { get; set; } = true;
        public bool GenerateRoof { get; set; } = true;
    }

    // Parsed Request
    public class ParsedRequest
    {
        public string OriginalPrompt { get; set; }
        public bool IsValid { get; set; } = true;
        public bool IsRefinement { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public BuildingType BuildingType { get; set; }
        public int? BedroomCount { get; set; }
        public int? BathroomCount { get; set; }
        public int StoreyCount { get; set; } = 1;
        public List<BuildingFeature> Features { get; set; } = new();
        public double? TotalArea { get; set; }
        public ArchitecturalStyle Style { get; set; }
        public LayoutPreferences LayoutPreferences { get; set; } = new();
        public List<RoomSpecification> RoomSpecifications { get; set; } = new();
        public List<Modification> Modifications { get; set; } = new();

        public ParsedRequest Clone() => JsonConvert.DeserializeObject<ParsedRequest>(JsonConvert.SerializeObject(this));
    }

    public class BuildingFeature
    {
        public FeatureType Type { get; set; }
        public int Count { get; set; } = 1;
        public string Size { get; set; }
    }

    public class LayoutPreferences
    {
        public bool OpenPlan { get; set; }
        public List<RoomType> OpenPlanAreas { get; set; } = new();
    }

    public class RoomSpecification
    {
        public RoomType RoomType { get; set; }
        public SizeModifier SizeModifier { get; set; }
        public List<string> Features { get; set; } = new();
    }

    public class Modification
    {
        public ModificationType Type { get; set; }
        public string TargetName { get; set; }
        public RoomType? RoomType { get; set; }
        public double? Area { get; set; }
        public SizeModifier? SizeModifier { get; set; }
    }

    // Building Program
    public class BuildingProgram
    {
        public BuildingType BuildingType { get; set; }
        public ArchitecturalStyle Style { get; set; }
        public int StoreyCount { get; set; }
        public double CeilingHeight { get; set; }
        public double WallThickness { get; set; }
        public double TotalArea { get; set; }
        public List<ProgrammedRoom> Rooms { get; set; } = new();
        public List<BuildingFeature> Features { get; set; } = new();
        public List<ProgramLevel> Levels { get; set; } = new();
    }

    public class ProgrammedRoom
    {
        public int Id { get; set; }
        public RoomType Type { get; set; }
        public string Name { get; set; }
        public double MinArea { get; set; }
        public double MaxArea { get; set; }
        public double TargetArea { get; set; }
        public int Level { get; set; }
    }

    public class ProgramLevel
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public double Elevation { get; set; }
        public List<ProgrammedRoom> Rooms { get; set; } = new();
    }

    // Templates
    public class BuildingTemplate
    {
        public BuildingType Type { get; set; }
        public int DefaultStoreys { get; set; }
        public int MaxStoreys { get; set; }
        public List<RoomTemplate> DefaultRooms { get; set; } = new();
        public FootprintRange TypicalFootprint { get; set; }
        public double DefaultWallThickness { get; set; }
        public double DefaultCeilingHeight { get; set; }
    }

    public class RoomTemplate
    {
        public RoomType Type { get; set; }
        public double MinArea { get; set; }
        public double MaxArea { get; set; }
        public bool Required { get; set; }
    }

    public class FootprintRange
    {
        public double Min { get; set; }
        public double Max { get; set; }
    }

    // Space Layout
    public class SpaceLayout
    {
        public BuildingFootprint BuildingFootprint { get; set; }
        public List<LevelLayout> LevelLayouts { get; set; } = new();
    }

    public class BuildingFootprint
    {
        public double Width { get; set; }
        public double Depth { get; set; }
        public double Area { get; set; }
        public FootprintShape Shape { get; set; }
        public Point Origin { get; set; }
    }

    public class LevelLayout
    {
        public ProgramLevel Level { get; set; }
        public List<RoomLayout> Rooms { get; set; } = new();
    }

    public class RoomLayout
    {
        public ProgrammedRoom Room { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Depth { get; set; }
        public double Area { get; set; }
    }

    public class AvailableArea
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    // Generated Model
    public class GeneratedModel
    {
        public List<GeneratedLevel> Levels { get; set; } = new();
        public List<GeneratedElement> Elements { get; set; } = new();

        public GeneratedModel Clone() => JsonConvert.DeserializeObject<GeneratedModel>(JsonConvert.SerializeObject(this));
    }

    public class GeneratedLevel
    {
        public string Name { get; set; }
        public double Elevation { get; set; }
        public double Height { get; set; }
    }

    public class GeneratedElement
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ElementType ElementType { get; set; }
        public GeneratedLevel Level { get; set; }
        public Point Location { get; set; }
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }
        public GeneratedElement HostElement { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();

        public double GetArea()
        {
            if (Properties.TryGetValue("Area", out var area))
                return Convert.ToDouble(area);
            return 0;
        }
    }

    public class Point
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point() { }
        public Point(double x, double y) { X = x; Y = y; }

        public override string ToString() => $"({X:F0}, {Y:F0})";
    }

    // Results
    public class GenerationResult
    {
        public bool Success { get; set; }
        public string OriginalPrompt { get; set; }
        public bool IsVariation { get; set; }
        public string VariationDescription { get; set; }
        public bool IsRefinement { get; set; }
        public DateTime GenerationStartTime { get; set; }
        public DateTime GenerationEndTime { get; set; }
        public TimeSpan Duration => GenerationEndTime - GenerationStartTime;
        public ParsedRequest ParsedRequest { get; set; }
        public BuildingProgram BuildingProgram { get; set; }
        public SpaceLayout SpaceLayout { get; set; }
        public GeneratedModel Model { get; set; }
        public GenerationStatistics Statistics { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class GenerationProgress
    {
        public int PercentComplete { get; set; }
        public string Status { get; set; }

        public GenerationProgress(int percent, string status)
        {
            PercentComplete = percent;
            Status = status;
        }
    }

    public class GenerationStatistics
    {
        public double TotalArea { get; set; }
        public int TotalWalls { get; set; }
        public int TotalDoors { get; set; }
        public int TotalWindows { get; set; }
        public int TotalRooms { get; set; }
        public int StoreyCount { get; set; }
    }

    #endregion

    #region Enumerations

    public enum BuildingType
    {
        SingleFamilyHouse,
        Apartment,
        TownHouse,
        Office,
        Retail,
        School,
        Hotel,
        Hospital,
        Warehouse,
        Industrial,
        MixedUse
    }

    public enum RoomType
    {
        Unknown,
        LivingRoom,
        Kitchen,
        DiningRoom,
        Bedroom,
        MasterBedroom,
        Bathroom,
        MasterBathroom,
        Entrance,
        Corridor,
        Storage,
        Garage,
        Laundry,
        Office,
        Study,
        OpenOffice,
        MeetingRoom,
        Reception,
        Classroom,
        Hall,
        SalesFloor
    }

    public enum FeatureType
    {
        Garage,
        Basement,
        Attic,
        Balcony,
        Terrace,
        Pool,
        Garden,
        Deck,
        Porch
    }

    public enum ArchitecturalStyle
    {
        Modern,
        Contemporary,
        Traditional,
        Colonial,
        Victorian,
        Industrial,
        Minimalist,
        Mediterranean,
        Craftsman
    }

    public enum SizeModifier
    {
        Small,
        Standard,
        Large
    }

    public enum ModificationType
    {
        AddRoom,
        RemoveRoom,
        ResizeRoom,
        AddFeature,
        RemoveFeature,
        ChangeLayout
    }

    public enum FootprintShape
    {
        Rectangle,
        LShape,
        TShape,
        UShape,
        Custom
    }

    public enum ElementType
    {
        Wall,
        Door,
        Window,
        Floor,
        Ceiling,
        Roof,
        Room,
        Stair,
        Column,
        Beam,
        Furniture
    }

    #endregion
}
