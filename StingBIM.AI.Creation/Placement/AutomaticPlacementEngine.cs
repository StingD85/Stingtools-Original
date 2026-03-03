// ============================================================================
// StingBIM.AI.Creation - Automatic Placement Engine with MEP Intelligence
// Intelligent fixture placement, MEP coordination, and code compliance
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Placement
{
    /// <summary>
    /// Automatic Placement Engine with MEP Intelligence
    /// Handles intelligent placement of fixtures, equipment, and MEP elements
    /// </summary>
    public sealed class AutomaticPlacementEngine
    {
        private static readonly Lazy<AutomaticPlacementEngine> _instance =
            new Lazy<AutomaticPlacementEngine>(() => new AutomaticPlacementEngine());
        public static AutomaticPlacementEngine Instance => _instance.Value;

        // ISO 19650 Parameter Constants
        private const string PARAM_FIXTURE_TYPE = "MR_FIXTURE_TYPE";
        private const string PARAM_CLEARANCE_FRONT = "MR_CLEARANCE_FRONT";
        private const string PARAM_CLEARANCE_SIDE = "MR_CLEARANCE_SIDE";
        private const string PARAM_ELECTRICAL_LOAD = "MR_ELECTRICAL_LOAD";
        private const string PARAM_WATER_DEMAND = "MR_WATER_DEMAND";
        private const string PARAM_DRAINAGE_SIZE = "MR_DRAINAGE_SIZE";
        private const string PARAM_AIRFLOW_CFM = "MR_AIRFLOW_CFM";
        private const string PARAM_HVAC_ZONE = "MR_HVAC_ZONE";

        private readonly Dictionary<string, PlacementRule> _placementRules;
        private readonly Dictionary<string, MEPRequirements> _mepRequirements;
        private readonly List<PlacedElement> _placedElements;
        private readonly object _lockObject = new object();

        public event EventHandler<PlacementEventArgs> ElementPlaced;
        public event EventHandler<ClashEventArgs> ClashDetected;

        private AutomaticPlacementEngine()
        {
            _placementRules = InitializePlacementRules();
            _mepRequirements = InitializeMEPRequirements();
            _placedElements = new List<PlacedElement>();
        }

        #region Placement Rules

        private Dictionary<string, PlacementRule> InitializePlacementRules()
        {
            return new Dictionary<string, PlacementRule>
            {
                // Plumbing Fixtures
                ["TOILET"] = new PlacementRule
                {
                    FixtureType = FixtureType.Plumbing,
                    Category = "Toilet",
                    ClearanceFront = 533,
                    ClearanceSide = 457,
                    RequiresWall = true,
                    WallOffset = 305,
                    DrainageSize = 100,
                    WaterConnection = "Cold",
                    HeightFromFloor = 0,
                    Orientation = PlacementOrientation.AgainstWall
                },
                ["SINK_BATHROOM"] = new PlacementRule
                {
                    FixtureType = FixtureType.Plumbing,
                    Category = "Lavatory",
                    ClearanceFront = 533,
                    ClearanceSide = 100,
                    RequiresWall = true,
                    DrainageSize = 38,
                    WaterConnection = "HotCold",
                    HeightFromFloor = 864,
                    Orientation = PlacementOrientation.AgainstWall
                },
                ["SINK_KITCHEN"] = new PlacementRule
                {
                    FixtureType = FixtureType.Plumbing,
                    Category = "Kitchen Sink",
                    ClearanceFront = 762,
                    ClearanceSide = 610,
                    DrainageSize = 50,
                    WaterConnection = "HotCold",
                    HeightFromFloor = 914,
                    Orientation = PlacementOrientation.InCounter
                },
                ["SHOWER"] = new PlacementRule
                {
                    FixtureType = FixtureType.Plumbing,
                    Category = "Shower",
                    ClearanceFront = 762,
                    RequiresWall = true,
                    DrainageSize = 50,
                    WaterConnection = "HotCold",
                    Orientation = PlacementOrientation.Corner
                },
                ["URINAL"] = new PlacementRule
                {
                    FixtureType = FixtureType.Plumbing,
                    Category = "Urinal",
                    ClearanceFront = 457,
                    ClearanceSide = 381,
                    RequiresWall = true,
                    DrainageSize = 50,
                    WaterConnection = "Cold",
                    HeightFromFloor = 432,
                    Orientation = PlacementOrientation.AgainstWall
                },

                // Electrical Fixtures
                ["RECEPTACLE"] = new PlacementRule
                {
                    FixtureType = FixtureType.Electrical,
                    Category = "Receptacle",
                    ClearanceFront = 914,
                    RequiresWall = true,
                    HeightFromFloor = 305,
                    ElectricalLoad = 180,
                    CircuitType = "20A-120V",
                    Orientation = PlacementOrientation.AgainstWall
                },
                ["RECEPTACLE_GFCI"] = new PlacementRule
                {
                    FixtureType = FixtureType.Electrical,
                    Category = "GFCI Receptacle",
                    RequiresWall = true,
                    HeightFromFloor = 1067,
                    ElectricalLoad = 180,
                    CircuitType = "20A-120V-GFCI",
                    Orientation = PlacementOrientation.AgainstWall
                },
                ["LIGHT_CEILING"] = new PlacementRule
                {
                    FixtureType = FixtureType.Electrical,
                    Category = "Ceiling Light",
                    ElectricalLoad = 100,
                    CircuitType = "15A-120V",
                    Orientation = PlacementOrientation.Ceiling
                },
                ["SWITCH"] = new PlacementRule
                {
                    FixtureType = FixtureType.Electrical,
                    Category = "Switch",
                    RequiresWall = true,
                    HeightFromFloor = 1219,
                    CircuitType = "15A-120V",
                    Orientation = PlacementOrientation.AgainstWall
                },
                ["PANEL"] = new PlacementRule
                {
                    FixtureType = FixtureType.Electrical,
                    Category = "Panel",
                    ClearanceFront = 914,
                    ClearanceSide = 762,
                    RequiresWall = true,
                    HeightFromFloor = 1524,
                    Orientation = PlacementOrientation.AgainstWall
                },

                // HVAC Fixtures
                ["DIFFUSER_SUPPLY"] = new PlacementRule
                {
                    FixtureType = FixtureType.HVAC,
                    Category = "Supply Diffuser",
                    AirflowCFM = 150,
                    DuctSize = 200,
                    Orientation = PlacementOrientation.Ceiling
                },
                ["DIFFUSER_RETURN"] = new PlacementRule
                {
                    FixtureType = FixtureType.HVAC,
                    Category = "Return Grille",
                    AirflowCFM = 200,
                    DuctSize = 250,
                    Orientation = PlacementOrientation.Ceiling
                },
                ["THERMOSTAT"] = new PlacementRule
                {
                    FixtureType = FixtureType.HVAC,
                    Category = "Thermostat",
                    RequiresWall = true,
                    HeightFromFloor = 1372,
                    Orientation = PlacementOrientation.AgainstWall
                },
                ["FCU"] = new PlacementRule
                {
                    FixtureType = FixtureType.HVAC,
                    Category = "Fan Coil Unit",
                    ClearanceFront = 610,
                    AirflowCFM = 400,
                    WaterConnection = "ChilledHot",
                    DrainageSize = 19,
                    Orientation = PlacementOrientation.Ceiling
                },

                // Fire Protection
                ["SPRINKLER"] = new PlacementRule
                {
                    FixtureType = FixtureType.FireProtection,
                    Category = "Sprinkler",
                    MaxCoverage = 18.6,
                    MaxSpacing = 4572,
                    Orientation = PlacementOrientation.Ceiling
                },
                ["SMOKE_DETECTOR"] = new PlacementRule
                {
                    FixtureType = FixtureType.FireProtection,
                    Category = "Smoke Detector",
                    MaxCoverage = 84,
                    Orientation = PlacementOrientation.Ceiling
                },
                ["FIRE_ALARM"] = new PlacementRule
                {
                    FixtureType = FixtureType.FireProtection,
                    Category = "Fire Alarm",
                    RequiresWall = true,
                    HeightFromFloor = 2134,
                    Orientation = PlacementOrientation.AgainstWall
                },
                ["FIRE_EXTINGUISHER"] = new PlacementRule
                {
                    FixtureType = FixtureType.FireProtection,
                    Category = "Fire Extinguisher",
                    ClearanceFront = 914,
                    RequiresWall = true,
                    HeightFromFloor = 1067,
                    MaxTravelDistance = 22860,
                    Orientation = PlacementOrientation.AgainstWall
                }
            };
        }

        private Dictionary<string, MEPRequirements> InitializeMEPRequirements()
        {
            return new Dictionary<string, MEPRequirements>
            {
                ["BATHROOM"] = new MEPRequirements
                {
                    RoomType = "Bathroom",
                    VentilationCFM = 50,
                    ExhaustRequired = true,
                    LightingLux = 300,
                    Circuits = new[] { "20A-120V-GFCI" },
                    RequiredFixtures = new[] { "TOILET", "SINK_BATHROOM" }
                },
                ["KITCHEN"] = new MEPRequirements
                {
                    RoomType = "Kitchen",
                    VentilationCFM = 100,
                    ExhaustRequired = true,
                    LightingLux = 500,
                    Circuits = new[] { "20A-120V-GFCI", "50A-240V" },
                    RequiredFixtures = new[] { "SINK_KITCHEN" },
                    ReceptacleSpacing = 1219
                },
                ["OFFICE"] = new MEPRequirements
                {
                    RoomType = "Office",
                    VentilationCFM = 17,
                    LightingLux = 500,
                    Circuits = new[] { "20A-120V" },
                    ReceptaclesPerArea = 0.01
                },
                ["CORRIDOR"] = new MEPRequirements
                {
                    RoomType = "Corridor",
                    LightingLux = 100,
                    EmergencyLighting = true,
                    ExitSigns = true
                },
                ["MECHANICAL"] = new MEPRequirements
                {
                    RoomType = "Mechanical Room",
                    VentilationCFM = 200,
                    ExhaustRequired = true,
                    LightingLux = 300,
                    FloorDrain = true
                }
            };
        }

        #endregion

        #region Main Placement Methods

        /// <summary>
        /// Auto-place all fixtures in a room based on room type
        /// </summary>
        public async Task<PlacementResult> AutoPlaceRoomFixturesAsync(
            RoomDefinition room,
            PlacementOptions options = null,
            IProgress<PlacementProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new PlacementOptions();
            var result = new PlacementResult { RoomId = room.Id, RoomName = room.Name };
            var placements = new List<PlacedElement>();

            try
            {
                // Get MEP requirements for room type
                _mepRequirements.TryGetValue(room.RoomType, out var requirements);
                requirements ??= GetDefaultRequirements(room);

                int step = 0;
                int totalSteps = CalculateTotalSteps(requirements);

                // 1. Place plumbing fixtures
                if (requirements.RequiredFixtures?.Any() == true)
                {
                    foreach (var fixture in requirements.RequiredFixtures)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        progress?.Report(new PlacementProgress(++step, totalSteps, $"Placing {fixture}"));

                        var placement = PlaceFixture(room, fixture, options);
                        if (placement != null)
                        {
                            placements.Add(placement);
                            OnElementPlaced(placement);
                        }
                    }
                }

                // 2. Place electrical fixtures
                progress?.Report(new PlacementProgress(++step, totalSteps, "Placing electrical"));
                var electrical = PlaceElectrical(room, requirements, options);
                placements.AddRange(electrical);

                // 3. Place HVAC fixtures
                progress?.Report(new PlacementProgress(++step, totalSteps, "Placing HVAC"));
                var hvac = PlaceHVAC(room, requirements, options);
                placements.AddRange(hvac);

                // 4. Place fire protection
                if (options.IncludeFireProtection)
                {
                    progress?.Report(new PlacementProgress(++step, totalSteps, "Placing fire protection"));
                    var fire = PlaceFireProtection(room, options);
                    placements.AddRange(fire);
                }

                // 5. Validate placements
                var clashes = DetectClashes(placements, room);
                if (clashes.Any() && !options.AllowClashes)
                {
                    placements = ResolveClashes(placements, clashes, room);
                }

                result.PlacedElements = placements;
                result.Success = true;
                result.Statistics = CalculateStatistics(placements);

                lock (_lockObject)
                {
                    _placedElements.AddRange(placements);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return await Task.FromResult(result);
        }

        /// <summary>
        /// Place a single fixture at optimal location
        /// </summary>
        public PlacedElement PlaceFixture(RoomDefinition room, string fixtureKey, PlacementOptions options = null)
        {
            if (!_placementRules.TryGetValue(fixtureKey, out var rule))
                return null;

            options ??= new PlacementOptions();

            // Find optimal location
            var location = FindOptimalLocation(room, rule);
            if (location == null) return null;

            return new PlacedElement
            {
                Id = Guid.NewGuid().ToString(),
                RoomId = room.Id,
                FixtureType = fixtureKey,
                Category = rule.Category,
                Location = location,
                Rotation = CalculateRotation(room, location, rule),
                Parameters = GenerateParameters(rule)
            };
        }

        /// <summary>
        /// Auto-place MEP for entire building
        /// </summary>
        public async Task<BuildingPlacementResult> AutoPlaceBuildingMEPAsync(
            BuildingDefinition building,
            PlacementOptions options = null,
            IProgress<PlacementProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new PlacementOptions();
            var result = new BuildingPlacementResult { BuildingId = building.Id };
            var roomResults = new List<PlacementResult>();

            int current = 0;
            int total = building.Rooms.Count;

            foreach (var room in building.Rooms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new PlacementProgress(++current, total, $"Processing {room.Name}"));

                var roomResult = await AutoPlaceRoomFixturesAsync(room, options, null, cancellationToken);
                roomResults.Add(roomResult);
            }

            // Coordinate systems across rooms
            if (options.CoordinateSystems)
            {
                CoordinateSystems(roomResults, building);
            }

            result.RoomResults = roomResults;
            result.TotalElements = roomResults.Sum(r => r.PlacedElements?.Count ?? 0);
            result.Success = roomResults.All(r => r.Success);

            return result;
        }

        #endregion

        #region Electrical Placement

        private List<PlacedElement> PlaceElectrical(RoomDefinition room, MEPRequirements requirements, PlacementOptions options)
        {
            var placements = new List<PlacedElement>();

            // Calculate receptacle count
            int receptacleCount = requirements.MinReceptacles;
            if (requirements.ReceptaclesPerArea > 0)
            {
                receptacleCount = Math.Max(receptacleCount, (int)Math.Ceiling(room.Area * requirements.ReceptaclesPerArea));
            }

            // Place receptacles along walls
            var locations = CalculateReceptacleLocations(room, receptacleCount, requirements.ReceptacleSpacing);
            foreach (var loc in locations)
            {
                var rule = requirements.Circuits?.Contains("20A-120V-GFCI") == true
                    ? _placementRules["RECEPTACLE_GFCI"]
                    : _placementRules["RECEPTACLE"];

                placements.Add(new PlacedElement
                {
                    Id = Guid.NewGuid().ToString(),
                    RoomId = room.Id,
                    FixtureType = rule.Category,
                    Category = "Electrical",
                    Location = loc,
                    Rotation = CalculateWallRotation(room, loc),
                    Parameters = GenerateParameters(rule)
                });
            }

            // Place switches near doors
            foreach (var door in room.Doors)
            {
                var switchLoc = CalculateSwitchLocation(door, room);
                if (switchLoc != null)
                {
                    var rule = _placementRules["SWITCH"];
                    placements.Add(new PlacedElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        RoomId = room.Id,
                        FixtureType = "SWITCH",
                        Category = rule.Category,
                        Location = switchLoc,
                        Rotation = CalculateWallRotation(room, switchLoc),
                        Parameters = GenerateParameters(rule)
                    });
                }
            }

            // Place ceiling lights
            var lightLocations = CalculateLightingLayout(room, requirements.LightingLux);
            foreach (var loc in lightLocations)
            {
                var rule = _placementRules["LIGHT_CEILING"];
                placements.Add(new PlacedElement
                {
                    Id = Guid.NewGuid().ToString(),
                    RoomId = room.Id,
                    FixtureType = "LIGHT_CEILING",
                    Category = rule.Category,
                    Location = loc,
                    Parameters = GenerateParameters(rule)
                });
            }

            return placements;
        }

        private List<Point3D> CalculateReceptacleLocations(RoomDefinition room, int count, double maxSpacing)
        {
            var locations = new List<Point3D>();
            maxSpacing = maxSpacing > 0 ? maxSpacing : 3658; // 12' default per NEC

            foreach (var wall in room.Walls.Where(w => !w.IsExterior))
            {
                double length = wall.Length;
                int onWall = Math.Max(1, (int)Math.Ceiling(length / maxSpacing));

                for (int i = 0; i < onWall && locations.Count < count; i++)
                {
                    double t = (i + 0.5) / onWall;
                    locations.Add(new Point3D
                    {
                        X = wall.Start.X + (wall.End.X - wall.Start.X) * t,
                        Y = wall.Start.Y + (wall.End.Y - wall.Start.Y) * t,
                        Z = _placementRules["RECEPTACLE"].HeightFromFloor
                    });
                }
            }

            return locations.Take(count).ToList();
        }

        private Point3D CalculateSwitchLocation(DoorDefinition door, RoomDefinition room)
        {
            double offset = door.SwingDirection == SwingDirection.Left ? 100 : -100;
            return new Point3D
            {
                X = door.Location.X + offset,
                Y = door.Location.Y,
                Z = _placementRules["SWITCH"].HeightFromFloor
            };
        }

        private List<Point3D> CalculateLightingLayout(RoomDefinition room, double targetLux)
        {
            var locations = new List<Point3D>();

            // Calculate fixture count based on lux requirement
            double lumensNeeded = targetLux * room.Area / 0.56; // CU=0.7, MF=0.8
            int fixtureCount = Math.Max(1, (int)Math.Ceiling(lumensNeeded / 3200)); // 3200 lm typical

            // Grid layout
            double ratio = room.Length / room.Width;
            int cols = Math.Max(1, (int)Math.Round(Math.Sqrt(fixtureCount * ratio)));
            int rows = Math.Max(1, (int)Math.Ceiling((double)fixtureCount / cols));

            double xSpace = room.Length / (cols + 1);
            double ySpace = room.Width / (rows + 1);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols && locations.Count < fixtureCount; c++)
                {
                    locations.Add(new Point3D
                    {
                        X = room.Origin.X + xSpace * (c + 1),
                        Y = room.Origin.Y + ySpace * (r + 1),
                        Z = room.CeilingHeight - 25
                    });
                }
            }

            return locations;
        }

        #endregion

        #region HVAC Placement

        private List<PlacedElement> PlaceHVAC(RoomDefinition room, MEPRequirements requirements, PlacementOptions options)
        {
            var placements = new List<PlacedElement>();

            double cfm = CalculateRequiredCFM(room, requirements);

            if (cfm > 0)
            {
                // Place supply diffusers
                var supplyLocs = CalculateDiffuserLayout(room, cfm, true);
                foreach (var loc in supplyLocs)
                {
                    var rule = _placementRules["DIFFUSER_SUPPLY"];
                    placements.Add(new PlacedElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        RoomId = room.Id,
                        FixtureType = "DIFFUSER_SUPPLY",
                        Category = rule.Category,
                        Location = loc,
                        Parameters = GenerateParameters(rule)
                    });
                }

                // Place return grilles
                var returnLocs = CalculateDiffuserLayout(room, cfm * 0.9, false);
                foreach (var loc in returnLocs)
                {
                    var rule = _placementRules["DIFFUSER_RETURN"];
                    placements.Add(new PlacedElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        RoomId = room.Id,
                        FixtureType = "DIFFUSER_RETURN",
                        Category = rule.Category,
                        Location = loc,
                        Parameters = GenerateParameters(rule)
                    });
                }
            }

            // Place thermostat
            if (room.Area > 9.3)
            {
                var thermoLoc = CalculateThermostatLocation(room);
                if (thermoLoc != null)
                {
                    var rule = _placementRules["THERMOSTAT"];
                    var element = new PlacedElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        RoomId = room.Id,
                        FixtureType = "THERMOSTAT",
                        Category = rule.Category,
                        Location = thermoLoc,
                        Rotation = CalculateWallRotation(room, thermoLoc),
                        Parameters = GenerateParameters(rule)
                    };
                    element.Parameters[PARAM_HVAC_ZONE] = room.Name;
                    placements.Add(element);
                }
            }

            return placements;
        }

        private double CalculateRequiredCFM(RoomDefinition room, MEPRequirements requirements)
        {
            double cfm = requirements.VentilationCFM;
            if (requirements.VentilationPerArea > 0)
            {
                cfm += room.Area * requirements.VentilationPerArea * 10.764;
            }
            return cfm;
        }

        private List<Point3D> CalculateDiffuserLayout(RoomDefinition room, double totalCFM, bool isSupply)
        {
            var locations = new List<Point3D>();

            double cfmPer = isSupply ? 150 : 200;
            int count = Math.Max(1, (int)Math.Ceiling(totalCFM / cfmPer));

            double ratio = room.Length / room.Width;
            int cols = Math.Max(1, (int)Math.Round(Math.Sqrt(count * ratio)));
            int rows = Math.Max(1, (int)Math.Ceiling((double)count / cols));

            double offsetX = isSupply ? 0 : room.Length * 0.1;
            double offsetY = isSupply ? 0 : room.Width * 0.1;

            double xSpace = room.Length / (cols + 1);
            double ySpace = room.Width / (rows + 1);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols && locations.Count < count; c++)
                {
                    locations.Add(new Point3D
                    {
                        X = room.Origin.X + xSpace * (c + 1) + offsetX,
                        Y = room.Origin.Y + ySpace * (r + 1) + offsetY,
                        Z = room.CeilingHeight - 25
                    });
                }
            }

            return locations;
        }

        private Point3D CalculateThermostatLocation(RoomDefinition room)
        {
            var wall = room.Walls.FirstOrDefault(w => !w.IsExterior) ?? room.Walls.First();
            return new Point3D
            {
                X = wall.Start.X + (wall.End.X - wall.Start.X) * 0.5,
                Y = wall.Start.Y + (wall.End.Y - wall.Start.Y) * 0.5,
                Z = _placementRules["THERMOSTAT"].HeightFromFloor
            };
        }

        #endregion

        #region Fire Protection Placement

        private List<PlacedElement> PlaceFireProtection(RoomDefinition room, PlacementOptions options)
        {
            var placements = new List<PlacedElement>();

            // Sprinklers - NFPA 13
            if (room.Area > 4.65)
            {
                var sprinklerLocs = CalculateSprinklerLayout(room);
                foreach (var loc in sprinklerLocs)
                {
                    placements.Add(new PlacedElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        RoomId = room.Id,
                        FixtureType = "SPRINKLER",
                        Category = "Sprinkler",
                        Location = loc,
                        Parameters = GenerateParameters(_placementRules["SPRINKLER"])
                    });
                }
            }

            // Smoke detectors
            placements.Add(new PlacedElement
            {
                Id = Guid.NewGuid().ToString(),
                RoomId = room.Id,
                FixtureType = "SMOKE_DETECTOR",
                Category = "Smoke Detector",
                Location = new Point3D
                {
                    X = room.Origin.X + room.Length / 2,
                    Y = room.Origin.Y + room.Width / 2,
                    Z = room.CeilingHeight - 25
                },
                Parameters = GenerateParameters(_placementRules["SMOKE_DETECTOR"])
            });

            return placements;
        }

        private List<Point3D> CalculateSprinklerLayout(RoomDefinition room)
        {
            var locations = new List<Point3D>();

            double maxSpacing = 4572; // 15' NFPA 13
            double maxCoverage = 18.58; // 200 sq ft

            int cols = Math.Max(1, (int)Math.Ceiling(room.Length / maxSpacing));
            int rows = Math.Max(1, (int)Math.Ceiling(room.Width / maxSpacing));

            double xSpace = room.Length / cols;
            double ySpace = room.Width / rows;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    locations.Add(new Point3D
                    {
                        X = room.Origin.X + xSpace * (c + 0.5),
                        Y = room.Origin.Y + ySpace * (r + 0.5),
                        Z = room.CeilingHeight - 25
                    });
                }
            }

            return locations;
        }

        #endregion

        #region Helper Methods

        private Point3D FindOptimalLocation(RoomDefinition room, PlacementRule rule)
        {
            return rule.Orientation switch
            {
                PlacementOrientation.AgainstWall => FindWallLocation(room, rule),
                PlacementOrientation.Corner => FindCornerLocation(room, rule),
                PlacementOrientation.Ceiling => FindCeilingLocation(room),
                PlacementOrientation.InCounter => FindCounterLocation(room, rule),
                _ => FindCenterLocation(room, rule)
            };
        }

        private Point3D FindWallLocation(RoomDefinition room, PlacementRule rule)
        {
            var wall = room.Walls.OrderByDescending(w => w.Length).First();
            return new Point3D
            {
                X = wall.Start.X + (wall.End.X - wall.Start.X) * 0.5 + (wall.Normal?.X ?? 0) * rule.WallOffset,
                Y = wall.Start.Y + (wall.End.Y - wall.Start.Y) * 0.5 + (wall.Normal?.Y ?? 0) * rule.WallOffset,
                Z = rule.HeightFromFloor
            };
        }

        private Point3D FindCornerLocation(RoomDefinition room, PlacementRule rule)
        {
            return new Point3D
            {
                X = room.Origin.X + rule.WallOffset,
                Y = room.Origin.Y + rule.WallOffset,
                Z = rule.HeightFromFloor
            };
        }

        private Point3D FindCeilingLocation(RoomDefinition room)
        {
            return new Point3D
            {
                X = room.Origin.X + room.Length / 2,
                Y = room.Origin.Y + room.Width / 2,
                Z = room.CeilingHeight - 25
            };
        }

        private Point3D FindCounterLocation(RoomDefinition room, PlacementRule rule)
        {
            var counter = room.Counters.FirstOrDefault();
            if (counter != null)
            {
                return new Point3D
                {
                    X = counter.Location.X + counter.Length / 2,
                    Y = counter.Location.Y + counter.Width / 2,
                    Z = counter.Height
                };
            }
            return FindWallLocation(room, rule);
        }

        private Point3D FindCenterLocation(RoomDefinition room, PlacementRule rule)
        {
            return new Point3D
            {
                X = room.Origin.X + room.Length / 2,
                Y = room.Origin.Y + room.Width / 2,
                Z = rule.HeightFromFloor
            };
        }

        private double CalculateRotation(RoomDefinition room, Point3D location, PlacementRule rule)
        {
            var center = new Point3D
            {
                X = room.Origin.X + room.Length / 2,
                Y = room.Origin.Y + room.Width / 2
            };
            return Math.Atan2(center.Y - location.Y, center.X - location.X) * 180 / Math.PI;
        }

        private double CalculateWallRotation(RoomDefinition room, Point3D location)
        {
            var wall = room.Walls.OrderBy(w => DistanceToWall(location, w)).First();
            double angle = Math.Atan2(wall.End.Y - wall.Start.Y, wall.End.X - wall.Start.X);
            return (angle + Math.PI / 2) * 180 / Math.PI;
        }

        private double DistanceToWall(Point3D point, WallDefinition wall)
        {
            double dx = wall.End.X - wall.Start.X;
            double dy = wall.End.Y - wall.Start.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len == 0) return double.MaxValue;

            double t = Math.Max(0, Math.Min(1,
                ((point.X - wall.Start.X) * dx + (point.Y - wall.Start.Y) * dy) / (len * len)));

            double cx = wall.Start.X + t * dx;
            double cy = wall.Start.Y + t * dy;

            return Math.Sqrt(Math.Pow(point.X - cx, 2) + Math.Pow(point.Y - cy, 2));
        }

        private Dictionary<string, string> GenerateParameters(PlacementRule rule)
        {
            var parameters = new Dictionary<string, string>
            {
                [PARAM_FIXTURE_TYPE] = rule.Category
            };

            if (rule.ClearanceFront > 0)
                parameters[PARAM_CLEARANCE_FRONT] = rule.ClearanceFront.ToString();
            if (rule.ClearanceSide > 0)
                parameters[PARAM_CLEARANCE_SIDE] = rule.ClearanceSide.ToString();
            if (rule.ElectricalLoad > 0)
                parameters[PARAM_ELECTRICAL_LOAD] = rule.ElectricalLoad.ToString();
            if (!string.IsNullOrEmpty(rule.WaterConnection))
                parameters[PARAM_WATER_DEMAND] = rule.WaterConnection;
            if (rule.DrainageSize > 0)
                parameters[PARAM_DRAINAGE_SIZE] = rule.DrainageSize.ToString();
            if (rule.AirflowCFM > 0)
                parameters[PARAM_AIRFLOW_CFM] = rule.AirflowCFM.ToString();

            return parameters;
        }

        private List<ClashInfo> DetectClashes(List<PlacedElement> placements, RoomDefinition room)
        {
            var clashes = new List<ClashInfo>();

            for (int i = 0; i < placements.Count; i++)
            {
                for (int j = i + 1; j < placements.Count; j++)
                {
                    double dist = Distance(placements[i].Location, placements[j].Location);
                    double minDist = GetMinSeparation(placements[i].FixtureType, placements[j].FixtureType);

                    if (dist < minDist)
                    {
                        clashes.Add(new ClashInfo
                        {
                            Element1Id = placements[i].Id,
                            Element2Id = placements[j].Id,
                            Distance = dist,
                            RequiredDistance = minDist
                        });
                    }
                }
            }

            return clashes;
        }

        private List<PlacedElement> ResolveClashes(List<PlacedElement> placements, List<ClashInfo> clashes, RoomDefinition room)
        {
            // Simple resolution: offset clashing elements
            foreach (var clash in clashes)
            {
                var element = placements.FirstOrDefault(p => p.Id == clash.Element2Id);
                if (element != null)
                {
                    element.Location.X += clash.RequiredDistance - clash.Distance + 50;
                }
            }
            return placements;
        }

        private double Distance(Point3D a, Point3D b)
        {
            return Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2) + Math.Pow(b.Z - a.Z, 2));
        }

        private double GetMinSeparation(string type1, string type2)
        {
            if (_placementRules.TryGetValue(type1, out var rule))
                return rule.ClearanceSide > 0 ? rule.ClearanceSide : 300;
            return 300;
        }

        private MEPRequirements GetDefaultRequirements(RoomDefinition room)
        {
            return new MEPRequirements
            {
                RoomType = room.RoomType,
                VentilationCFM = 20,
                LightingLux = 300,
                MinReceptacles = 2
            };
        }

        private int CalculateTotalSteps(MEPRequirements requirements)
        {
            return (requirements.RequiredFixtures?.Length ?? 0) + 4;
        }

        private PlacementStatistics CalculateStatistics(List<PlacedElement> placements)
        {
            return new PlacementStatistics
            {
                TotalElements = placements.Count,
                PlumbingCount = placements.Count(p => _placementRules.TryGetValue(p.FixtureType, out var r) && r.FixtureType == FixtureType.Plumbing),
                ElectricalCount = placements.Count(p => _placementRules.TryGetValue(p.FixtureType, out var r) && r.FixtureType == FixtureType.Electrical),
                HVACCount = placements.Count(p => _placementRules.TryGetValue(p.FixtureType, out var r) && r.FixtureType == FixtureType.HVAC),
                FireCount = placements.Count(p => _placementRules.TryGetValue(p.FixtureType, out var r) && r.FixtureType == FixtureType.FireProtection),
                TotalElectricalLoad = placements.Sum(p => p.Parameters.TryGetValue(PARAM_ELECTRICAL_LOAD, out var v) && double.TryParse(v, out var d) ? d : 0),
                TotalAirflow = placements.Sum(p => p.Parameters.TryGetValue(PARAM_AIRFLOW_CFM, out var v) && double.TryParse(v, out var d) ? d : 0)
            };
        }

        private void CoordinateSystems(List<PlacementResult> roomResults, BuildingDefinition building)
        {
            // Coordinate plumbing stacks, electrical panels, HVAC zones across rooms
        }

        private void OnElementPlaced(PlacedElement element)
        {
            ElementPlaced?.Invoke(this, new PlacementEventArgs { Element = element });
        }

        #endregion
    }

    #region Data Models

    public enum FixtureType { Plumbing, Electrical, HVAC, FireProtection }
    public enum PlacementOrientation { AgainstWall, Corner, Ceiling, Floor, InCounter, FreeStanding }
    public enum SwingDirection { Left, Right, Double, Sliding }

    public class PlacementRule
    {
        public FixtureType FixtureType { get; set; }
        public string Category { get; set; }
        public double ClearanceFront { get; set; }
        public double ClearanceSide { get; set; }
        public bool RequiresWall { get; set; }
        public double WallOffset { get; set; }
        public double HeightFromFloor { get; set; }
        public PlacementOrientation Orientation { get; set; }
        public double DrainageSize { get; set; }
        public string WaterConnection { get; set; }
        public double ElectricalLoad { get; set; }
        public string CircuitType { get; set; }
        public double AirflowCFM { get; set; }
        public double DuctSize { get; set; }
        public double MaxCoverage { get; set; }
        public double MaxSpacing { get; set; }
        public double MaxTravelDistance { get; set; }
    }

    public class MEPRequirements
    {
        public string RoomType { get; set; }
        public double VentilationCFM { get; set; }
        public double VentilationPerArea { get; set; }
        public bool ExhaustRequired { get; set; }
        public double LightingLux { get; set; }
        public string[] Circuits { get; set; }
        public string[] RequiredFixtures { get; set; }
        public int MinReceptacles { get; set; }
        public double ReceptaclesPerArea { get; set; }
        public double ReceptacleSpacing { get; set; }
        public bool EmergencyLighting { get; set; }
        public bool ExitSigns { get; set; }
        public bool FloorDrain { get; set; }
    }

    public class PlacedElement
    {
        public string Id { get; set; }
        public string RoomId { get; set; }
        public string FixtureType { get; set; }
        public string Category { get; set; }
        public Point3D Location { get; set; }
        public double Rotation { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    public class PlacementResult
    {
        public string RoomId { get; set; }
        public string RoomName { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<PlacedElement> PlacedElements { get; set; } = new List<PlacedElement>();
        public PlacementStatistics Statistics { get; set; }
    }

    public class BuildingPlacementResult
    {
        public string BuildingId { get; set; }
        public bool Success { get; set; }
        public List<PlacementResult> RoomResults { get; set; } = new List<PlacementResult>();
        public int TotalElements { get; set; }
    }

    public class PlacementStatistics
    {
        public int TotalElements { get; set; }
        public int PlumbingCount { get; set; }
        public int ElectricalCount { get; set; }
        public int HVACCount { get; set; }
        public int FireCount { get; set; }
        public double TotalElectricalLoad { get; set; }
        public double TotalAirflow { get; set; }
    }

    public class PlacementOptions
    {
        public bool AllowClashes { get; set; } = false;
        public bool IncludeFireProtection { get; set; } = true;
        public bool CoordinateSystems { get; set; } = true;
        public string ComplianceCode { get; set; } = "IBC";
    }

    public class PlacementProgress
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string Operation { get; set; }
        public PlacementProgress(int current, int total, string operation)
        {
            Current = current;
            Total = total;
            Operation = operation;
        }
    }

    public class ClashInfo
    {
        public string Element1Id { get; set; }
        public string Element2Id { get; set; }
        public double Distance { get; set; }
        public double RequiredDistance { get; set; }
    }

    public class PlacementEventArgs : EventArgs
    {
        public PlacedElement Element { get; set; }
    }

    public class ClashEventArgs : EventArgs
    {
        public PlacedElement Element { get; set; }
        public List<ClashInfo> Clashes { get; set; }
    }

    public class RoomDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string RoomType { get; set; }
        public Point3D Origin { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double Area => Length * Width / 1000000;
        public double CeilingHeight { get; set; }
        public List<WallDefinition> Walls { get; set; } = new List<WallDefinition>();
        public List<DoorDefinition> Doors { get; set; } = new List<DoorDefinition>();
        public List<CounterDefinition> Counters { get; set; } = new List<CounterDefinition>();
    }

    public class WallDefinition
    {
        public Point3D Start { get; set; }
        public Point3D End { get; set; }
        public double Thickness { get; set; }
        public bool IsExterior { get; set; }
        public double Length => Math.Sqrt(Math.Pow(End.X - Start.X, 2) + Math.Pow(End.Y - Start.Y, 2));
        public Point3D Normal => new Point3D
        {
            X = -(End.Y - Start.Y) / Length,
            Y = (End.X - Start.X) / Length
        };
    }

    public class DoorDefinition
    {
        public string Id { get; set; }
        public Point3D Location { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public SwingDirection SwingDirection { get; set; }
    }

    public class CounterDefinition
    {
        public Point3D Location { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class BuildingDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<RoomDefinition> Rooms { get; set; } = new List<RoomDefinition>();
    }

    #endregion
}
