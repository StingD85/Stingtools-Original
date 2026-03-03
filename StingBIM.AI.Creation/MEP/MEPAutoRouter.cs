using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.MEP
{
    /// <summary>
    /// Automatically routes MEP systems (ducts, pipes, cables) through architectural models
    /// using pathfinding algorithms and sizing calculations.
    /// </summary>
    public class MEPAutoRouter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly RoutingSettings _settings;
        private readonly PathfindingEngine _pathfinder;
        private readonly SizingCalculator _sizingCalculator;
        private readonly ClearanceChecker _clearanceChecker;

        public MEPAutoRouter(RoutingSettings settings = null)
        {
            _settings = settings ?? new RoutingSettings();
            _pathfinder = new PathfindingEngine(_settings);
            _sizingCalculator = new SizingCalculator();
            _clearanceChecker = new ClearanceChecker(_settings);
        }

        /// <summary>
        /// Auto-route all MEP systems for a building model.
        /// </summary>
        public async Task<RoutingResult> RouteAllSystemsAsync(
            BuildingModel building,
            MEPRequirements requirements,
            IProgress<RoutingProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Starting MEP auto-routing for building model");
            var result = new RoutingResult();
            var systemTypes = new[] { MEPSystemType.HVAC, MEPSystemType.Plumbing,
                                       MEPSystemType.Electrical, MEPSystemType.FireProtection };
            int completed = 0;

            foreach (var systemType in systemTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var systemResult = await RouteSystemAsync(building, systemType,
                    requirements, cancellationToken);
                result.SystemResults[systemType] = systemResult;

                completed++;
                progress?.Report(new RoutingProgress
                {
                    Phase = $"Routing {systemType}",
                    PercentComplete = (completed * 100) / systemTypes.Length
                });
            }

            result.Statistics = CalculateStatistics(result);
            Logger.Info($"MEP routing complete: {result.Statistics.TotalRoutes} routes, " +
                       $"{result.Statistics.TotalLength:F1}m total length");
            return result;
        }

        /// <summary>
        /// Route a specific MEP system type.
        /// </summary>
        public async Task<SystemRoutingResult> RouteSystemAsync(
            BuildingModel building,
            MEPSystemType systemType,
            MEPRequirements requirements,
            CancellationToken cancellationToken = default)
        {
            Logger.Debug($"Routing {systemType} system");
            var result = new SystemRoutingResult { SystemType = systemType };

            var endpoints = GetSystemEndpoints(building, systemType, requirements);
            var obstacles = GetObstacles(building, systemType);

            foreach (var connection in endpoints.Connections)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var route = await _pathfinder.FindRouteAsync(
                    connection.Start, connection.End, obstacles, systemType);

                if (route != null)
                {
                    route.Sizing = _sizingCalculator.CalculateSizing(route, systemType, connection.Load);
                    route.Clearances = _clearanceChecker.ValidateClearances(route, building);
                    result.Routes.Add(route);
                }
                else
                {
                    result.FailedConnections.Add(connection);
                    Logger.Warn($"Failed to route connection: {connection.Id}");
                }
            }

            return result;
        }

        /// <summary>
        /// Route HVAC ductwork from AHU to terminal units.
        /// </summary>
        public async Task<DuctRoutingResult> RouteDuctworkAsync(
            BuildingModel building,
            HVACRequirements hvacReq,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Routing HVAC ductwork");
            var result = new DuctRoutingResult();

            foreach (var ahu in hvacReq.AirHandlingUnits)
            {
                var supplyTree = await RouteSupplyDuctsAsync(ahu, hvacReq.TerminalUnits, building, cancellationToken);
                var returnTree = await RouteReturnDuctsAsync(ahu, hvacReq.ReturnGrilles, building, cancellationToken);

                result.SupplyNetworks.Add(supplyTree);
                result.ReturnNetworks.Add(returnTree);
            }

            // Size ducts based on airflow
            foreach (var network in result.SupplyNetworks.Concat(result.ReturnNetworks))
            {
                _sizingCalculator.SizeDuctNetwork(network, hvacReq.MaxVelocity, hvacReq.MaxPressureDrop);
            }

            result.Statistics = CalculateDuctStatistics(result);
            return result;
        }

        /// <summary>
        /// Route piping systems (domestic water, drainage, hydronic).
        /// </summary>
        public async Task<PipeRoutingResult> RoutePipingAsync(
            BuildingModel building,
            PlumbingRequirements plumbingReq,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Routing piping systems");
            var result = new PipeRoutingResult();

            // Route domestic cold water
            if (plumbingReq.ColdWaterFixtures.Any())
            {
                result.ColdWaterNetwork = await RoutePipeNetworkAsync(
                    building, plumbingReq.WaterEntry, plumbingReq.ColdWaterFixtures,
                    PipeSystemType.DomesticColdWater, cancellationToken);
            }

            // Route domestic hot water
            if (plumbingReq.HotWaterFixtures.Any())
            {
                result.HotWaterNetwork = await RoutePipeNetworkAsync(
                    building, plumbingReq.WaterHeater, plumbingReq.HotWaterFixtures,
                    PipeSystemType.DomesticHotWater, cancellationToken);
            }

            // Route drainage with gravity considerations
            if (plumbingReq.DrainageFixtures.Any())
            {
                result.DrainageNetwork = await RouteDrainageAsync(
                    building, plumbingReq.DrainageFixtures, plumbingReq.MainDrain, cancellationToken);
            }

            // Route hydronic heating/cooling
            if (plumbingReq.HydronicTerminals.Any())
            {
                result.HydronicNetwork = await RoutePipeNetworkAsync(
                    building, plumbingReq.HydronicSource, plumbingReq.HydronicTerminals,
                    PipeSystemType.Hydronic, cancellationToken);
            }

            result.Statistics = CalculatePipeStatistics(result);
            return result;
        }

        /// <summary>
        /// Route electrical cable trays and conduits.
        /// </summary>
        public async Task<CableRoutingResult> RouteCablingAsync(
            BuildingModel building,
            ElectricalRequirements elecReq,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Routing electrical cabling");
            var result = new CableRoutingResult();

            // Route main distribution from transformer/MSB
            result.MainDistribution = await RouteMainDistributionAsync(
                building, elecReq.MainSwitchboard, elecReq.DistributionBoards, cancellationToken);

            // Route sub-circuits from distribution boards
            foreach (var db in elecReq.DistributionBoards)
            {
                var circuits = await RouteCircuitsAsync(building, db, elecReq.Outlets, cancellationToken);
                result.SubCircuits.AddRange(circuits);
            }

            // Route data/communication cables
            if (elecReq.DataOutlets.Any())
            {
                result.DataNetwork = await RouteDataCablingAsync(
                    building, elecReq.DataRoom, elecReq.DataOutlets, cancellationToken);
            }

            // Size cables based on load and voltage drop
            foreach (var route in result.AllRoutes)
            {
                _sizingCalculator.SizeCable(route, elecReq.Voltage, elecReq.MaxVoltageDrop);
            }

            result.Statistics = CalculateCableStatistics(result);
            return result;
        }

        /// <summary>
        /// Route fire protection sprinkler system.
        /// </summary>
        public async Task<SprinklerRoutingResult> RouteSprinklersAsync(
            BuildingModel building,
            FireProtectionRequirements fireReq,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Routing fire sprinkler system");
            var result = new SprinklerRoutingResult();

            // Calculate sprinkler head locations based on coverage
            var sprinklerHeads = CalculateSprinklerLayout(building, fireReq);
            result.SprinklerHeads = sprinklerHeads;

            // Route branch lines to heads
            foreach (var zone in fireReq.Zones)
            {
                var zoneHeads = sprinklerHeads.Where(h => h.ZoneId == zone.Id).ToList();
                var branchLines = await RouteSprinklerBranchesAsync(zone, zoneHeads, building, cancellationToken);
                result.BranchLines.AddRange(branchLines);
            }

            // Route cross mains
            var crossMains = await RouteCrossMainsAsync(building, result.BranchLines, cancellationToken);
            result.CrossMains.AddRange(crossMains);

            // Route feed main from riser
            result.FeedMain = await RouteFeedMainAsync(building, fireReq.Riser, result.CrossMains, cancellationToken);

            // Size pipes based on hydraulic calculations
            _sizingCalculator.SizeSprinklerSystem(result, fireReq.DesignDensity, fireReq.RemoteArea);

            result.Statistics = CalculateSprinklerStatistics(result);
            return result;
        }

        #region Private Routing Methods

        private async Task<DuctNetwork> RouteSupplyDuctsAsync(
            AirHandlingUnit ahu, List<TerminalUnit> terminals, BuildingModel building,
            CancellationToken cancellationToken)
        {
            var network = new DuctNetwork { Type = DuctType.Supply, SourceId = ahu.Id };
            var obstacles = GetObstacles(building, MEPSystemType.HVAC);

            // Group terminals by zone for trunk-branch routing
            var zones = terminals.GroupBy(t => t.ZoneId);

            foreach (var zone in zones)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Route trunk to zone
                var zoneCenter = CalculateZoneCenter(zone.ToList());
                var trunkRoute = await _pathfinder.FindRouteAsync(
                    ahu.SupplyConnection, zoneCenter, obstacles, MEPSystemType.HVAC);

                if (trunkRoute != null)
                {
                    network.TrunkRoutes.Add(trunkRoute);

                    // Route branches to each terminal
                    foreach (var terminal in zone)
                    {
                        var branchRoute = await _pathfinder.FindRouteAsync(
                            new MEPConnection { Position = zoneCenter }, terminal.Connection, obstacles, MEPSystemType.HVAC);
                        if (branchRoute != null)
                        {
                            branchRoute.ParentId = trunkRoute.Id;
                            network.BranchRoutes.Add(branchRoute);
                        }
                    }
                }
            }

            return network;
        }

        private async Task<DuctNetwork> RouteReturnDuctsAsync(
            AirHandlingUnit ahu, List<ReturnGrille> grilles, BuildingModel building,
            CancellationToken cancellationToken)
        {
            var network = new DuctNetwork { Type = DuctType.Return, SourceId = ahu.Id };
            var obstacles = GetObstacles(building, MEPSystemType.HVAC);

            foreach (var grille in grilles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var route = await _pathfinder.FindRouteAsync(
                    grille.Connection, ahu.ReturnConnection, obstacles, MEPSystemType.HVAC);

                if (route != null)
                    network.BranchRoutes.Add(route);
            }

            return network;
        }

        private async Task<PipeNetwork> RoutePipeNetworkAsync(
            BuildingModel building, MEPConnection source, List<MEPFixture> fixtures,
            PipeSystemType systemType, CancellationToken cancellationToken)
        {
            var network = new PipeNetwork { SystemType = systemType };
            var obstacles = GetObstacles(building, MEPSystemType.Plumbing);

            // Group fixtures by riser location
            var risers = GroupFixturesByRiser(fixtures, building);

            foreach (var riser in risers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Route main to riser base
                var riserRoute = await _pathfinder.FindRouteAsync(
                    source, riser.BaseConnection, obstacles, MEPSystemType.Plumbing);

                if (riserRoute != null)
                {
                    network.MainRoutes.Add(riserRoute);

                    // Route branches from riser to fixtures on each floor
                    foreach (var fixture in riser.Fixtures)
                    {
                        var branchRoute = await _pathfinder.FindRouteAsync(
                            riser.GetConnectionAtLevel(fixture.Level),
                            fixture.Connection, obstacles, MEPSystemType.Plumbing);

                        if (branchRoute != null)
                            network.BranchRoutes.Add(branchRoute);
                    }
                }
            }

            return network;
        }

        private async Task<DrainageNetwork> RouteDrainageAsync(
            BuildingModel building, List<MEPFixture> fixtures, MEPConnection mainDrain,
            CancellationToken cancellationToken)
        {
            var network = new DrainageNetwork();
            var obstacles = GetObstacles(building, MEPSystemType.Plumbing);

            // Route with slope for gravity drainage
            foreach (var fixture in fixtures.OrderByDescending(f => f.Level))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var route = await _pathfinder.FindDrainageRouteAsync(
                    fixture.DrainConnection, mainDrain, obstacles,
                    _settings.DrainageSlope);

                if (route != null)
                {
                    route.Slope = _settings.DrainageSlope;
                    network.DrainRoutes.Add(route);
                }
            }

            // Add vents
            network.VentRoutes = await RouteVentsAsync(building, network.DrainRoutes, cancellationToken);

            return network;
        }

        private async Task<List<Route>> RouteVentsAsync(
            BuildingModel building, List<Route> drainRoutes, CancellationToken cancellationToken)
        {
            var vents = new List<Route>();
            var obstacles = GetObstacles(building, MEPSystemType.Plumbing);
            var roofPenetrations = building.GetRoofPenetrationPoints();

            foreach (var drain in drainRoutes.Where(d => d.RequiresVent))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var nearestRoof = roofPenetrations.OrderBy(p =>
                    Distance(p, drain.VentConnectionPoint.Position)).First();

                var ventRoute = await _pathfinder.FindRouteAsync(
                    drain.VentConnectionPoint, nearestRoof, obstacles, MEPSystemType.Plumbing);

                if (ventRoute != null)
                {
                    ventRoute.IsVent = true;
                    vents.Add(ventRoute);
                }
            }

            return vents;
        }

        private async Task<CableNetwork> RouteMainDistributionAsync(
            BuildingModel building, Switchboard msb, List<DistributionBoard> dbs,
            CancellationToken cancellationToken)
        {
            var network = new CableNetwork { Type = CableNetworkType.MainDistribution };
            var obstacles = GetObstacles(building, MEPSystemType.Electrical);

            foreach (var db in dbs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var route = await _pathfinder.FindRouteAsync(
                    msb.Connection, db.Connection, obstacles, MEPSystemType.Electrical);

                if (route != null)
                {
                    route.CircuitType = CircuitType.Feeder;
                    network.Routes.Add(route);
                }
            }

            return network;
        }

        private async Task<List<CableRoute>> RouteCircuitsAsync(
            BuildingModel building, DistributionBoard db, List<ElectricalOutlet> outlets,
            CancellationToken cancellationToken)
        {
            var routes = new List<CableRoute>();
            var obstacles = GetObstacles(building, MEPSystemType.Electrical);

            // Group outlets into circuits based on load and location
            var circuits = GroupOutletsIntoCircuits(outlets, db);

            foreach (var circuit in circuits)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Route in daisy chain for outlets on same circuit
                MEPConnection lastPoint = db.Connection;
                foreach (var outlet in circuit.Outlets)
                {
                    var route = await _pathfinder.FindRouteAsync(
                        lastPoint, outlet.Connection, obstacles, MEPSystemType.Electrical);

                    if (route != null)
                    {
                        var cableRoute = new CableRoute(route)
                        {
                            CircuitId = circuit.Id,
                            CircuitType = circuit.Type
                        };
                        routes.Add(cableRoute);
                        lastPoint = outlet.Connection;
                    }
                }
            }

            return routes;
        }

        private async Task<CableNetwork> RouteDataCablingAsync(
            BuildingModel building, DataRoom dataRoom, List<DataOutlet> outlets,
            CancellationToken cancellationToken)
        {
            var network = new CableNetwork { Type = CableNetworkType.Data };
            var obstacles = GetObstacles(building, MEPSystemType.Electrical);

            // Star topology from data room to each outlet
            foreach (var outlet in outlets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var route = await _pathfinder.FindRouteAsync(
                    dataRoom.PatchPanel, outlet.Connection, obstacles, MEPSystemType.Electrical);

                if (route != null)
                {
                    route.CableType = CableType.Cat6A;
                    network.Routes.Add(route);
                }
            }

            return network;
        }

        private List<SprinklerHead> CalculateSprinklerLayout(
            BuildingModel building, FireProtectionRequirements fireReq)
        {
            var heads = new List<SprinklerHead>();
            var maxSpacing = fireReq.HazardClass switch
            {
                HazardClass.Light => 4.6,      // 15 ft
                HazardClass.OrdinaryI => 4.6,
                HazardClass.OrdinaryII => 4.0, // 13 ft
                HazardClass.ExtraI => 3.7,     // 12 ft
                HazardClass.ExtraII => 3.0,    // 10 ft
                _ => 4.6
            };

            foreach (var space in building.Spaces.Where(s => s.RequiresSprinklers))
            {
                var gridHeads = GenerateSprinklerGrid(space, maxSpacing, fireReq.CeilingHeight);
                heads.AddRange(gridHeads);
            }

            return heads;
        }

        private List<SprinklerHead> GenerateSprinklerGrid(Space space, double maxSpacing, double ceilingHeight)
        {
            var heads = new List<SprinklerHead>();
            var bounds = space.Bounds;

            int countX = (int)Math.Ceiling(bounds.Width / maxSpacing);
            int countY = (int)Math.Ceiling(bounds.Length / maxSpacing);

            double spacingX = bounds.Width / countX;
            double spacingY = bounds.Length / countY;

            for (int i = 0; i < countX; i++)
            {
                for (int j = 0; j < countY; j++)
                {
                    heads.Add(new SprinklerHead
                    {
                        Id = Guid.NewGuid().ToString(),
                        ZoneId = space.ZoneId,
                        Position = new Point3D(
                            bounds.MinX + spacingX * (i + 0.5),
                            bounds.MinY + spacingY * (j + 0.5),
                            ceilingHeight - 0.1),
                        Type = SprinklerHeadType.Pendant
                    });
                }
            }

            return heads;
        }

        private async Task<List<Route>> RouteSprinklerBranchesAsync(
            FireZone zone, List<SprinklerHead> heads, BuildingModel building,
            CancellationToken cancellationToken)
        {
            var branches = new List<Route>();
            var obstacles = GetObstacles(building, MEPSystemType.FireProtection);

            // Group heads into branch lines
            var branchGroups = heads.GroupBy(h => Math.Round(h.Position.Y, 1));

            foreach (var group in branchGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sortedHeads = group.OrderBy(h => h.Position.X).ToList();
                var branchStart = new MEPConnection
                {
                    Position = new Point3D(sortedHeads.First().Position.X - 0.3,
                                          sortedHeads.First().Position.Y,
                                          sortedHeads.First().Position.Z)
                };

                MEPConnection lastPoint = branchStart;
                foreach (var head in sortedHeads)
                {
                    var headConnection = new MEPConnection { Position = head.Position };
                    var route = await _pathfinder.FindRouteAsync(
                        lastPoint, headConnection, obstacles, MEPSystemType.FireProtection);

                    if (route != null)
                    {
                        route.SprinklerHeadId = head.Id;
                        branches.Add(route);
                        lastPoint = headConnection;
                    }
                }
            }

            return branches;
        }

        private async Task<List<Route>> RouteCrossMainsAsync(
            BuildingModel building, List<Route> branchLines, CancellationToken cancellationToken)
        {
            var crossMains = new List<Route>();
            var obstacles = GetObstacles(building, MEPSystemType.FireProtection);

            // Connect branch line starts with cross main
            var branchStarts = branchLines
                .Select(b => b.StartPoint)
                .Distinct()
                .OrderBy(p => p.Y)
                .ToList();

            for (int i = 0; i < branchStarts.Count - 1; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var route = await _pathfinder.FindRouteAsync(
                    new MEPConnection { Position = branchStarts[i] },
                    new MEPConnection { Position = branchStarts[i + 1] },
                    obstacles, MEPSystemType.FireProtection);

                if (route != null)
                    crossMains.Add(route);
            }

            return crossMains;
        }

        private async Task<Route> RouteFeedMainAsync(
            BuildingModel building, SprinklerRiser riser, List<Route> crossMains,
            CancellationToken cancellationToken)
        {
            if (!crossMains.Any()) return null;

            var obstacles = GetObstacles(building, MEPSystemType.FireProtection);
            var crossMainStart = crossMains.First().StartPoint;

            return await _pathfinder.FindRouteAsync(
                riser.Connection,
                new MEPConnection { Position = crossMainStart },
                obstacles, MEPSystemType.FireProtection);
        }

        #endregion

        #region Helper Methods

        private SystemEndpoints GetSystemEndpoints(BuildingModel building,
            MEPSystemType systemType, MEPRequirements requirements)
        {
            return new SystemEndpoints
            {
                Connections = requirements.GetConnections(systemType)
            };
        }

        private List<Obstacle> GetObstacles(BuildingModel building, MEPSystemType systemType)
        {
            var obstacles = new List<Obstacle>();

            // Add structural elements
            obstacles.AddRange(building.Columns.Select(c => new Obstacle(c.Bounds, ObstacleType.Structural)));
            obstacles.AddRange(building.Beams.Select(b => new Obstacle(b.Bounds, ObstacleType.Structural)));
            obstacles.AddRange(building.Walls.Where(w => w.IsStructural).Select(w => new Obstacle(w.Bounds, ObstacleType.Structural)));

            // Add existing MEP based on priority
            if (systemType != MEPSystemType.HVAC)
                obstacles.AddRange(building.ExistingDucts.Select(d => new Obstacle(d.Bounds, ObstacleType.MEP)));
            if (systemType != MEPSystemType.Plumbing)
                obstacles.AddRange(building.ExistingPipes.Select(p => new Obstacle(p.Bounds, ObstacleType.MEP)));

            return obstacles;
        }

        private Point3D CalculateZoneCenter(List<TerminalUnit> terminals)
        {
            return new Point3D(
                terminals.Average(t => t.Connection.Position.X),
                terminals.Average(t => t.Connection.Position.Y),
                terminals.Average(t => t.Connection.Position.Z));
        }

        private List<RiserGroup> GroupFixturesByRiser(List<MEPFixture> fixtures, BuildingModel building)
        {
            var riserLocations = building.GetRiserLocations();
            var groups = new List<RiserGroup>();

            foreach (var riserLoc in riserLocations)
            {
                var nearbyFixtures = fixtures
                    .Where(f => Distance2D(f.Connection.Position, riserLoc) < _settings.MaxBranchLength)
                    .ToList();

                if (nearbyFixtures.Any())
                {
                    groups.Add(new RiserGroup
                    {
                        BaseConnection = new MEPConnection { Position = riserLoc },
                        Fixtures = nearbyFixtures
                    });
                }
            }

            return groups;
        }

        private List<Circuit> GroupOutletsIntoCircuits(List<ElectricalOutlet> outlets, DistributionBoard db)
        {
            var circuits = new List<Circuit>();
            var remaining = outlets.ToList();

            while (remaining.Any())
            {
                var circuit = new Circuit
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = remaining.First().RequiredCircuitType
                };

                var maxLoad = circuit.Type == CircuitType.Lighting ? 1800.0 : 2400.0; // Watts
                double currentLoad = 0;

                var nearby = remaining
                    .Where(o => o.RequiredCircuitType == circuit.Type)
                    .OrderBy(o => Distance(o.Connection.Position, db.Connection.Position))
                    .ToList();

                foreach (var outlet in nearby)
                {
                    if (currentLoad + outlet.LoadWatts <= maxLoad)
                    {
                        circuit.Outlets.Add(outlet);
                        currentLoad += outlet.LoadWatts;
                        remaining.Remove(outlet);
                    }
                }

                if (circuit.Outlets.Any())
                    circuits.Add(circuit);
            }

            return circuits;
        }

        private double Distance(Point3D a, Point3D b) =>
            Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));

        private double Distance2D(Point3D a, Point3D b) =>
            Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

        private RoutingStatistics CalculateStatistics(RoutingResult result)
        {
            return new RoutingStatistics
            {
                TotalRoutes = result.SystemResults.Values.Sum(s => s.Routes.Count),
                TotalLength = result.SystemResults.Values.Sum(s => s.Routes.Sum(r => r.Length)),
                FailedConnections = result.SystemResults.Values.Sum(s => s.FailedConnections.Count)
            };
        }

        private DuctStatistics CalculateDuctStatistics(DuctRoutingResult result) =>
            new DuctStatistics
            {
                TotalSupplyLength = result.SupplyNetworks.Sum(n => n.TotalLength),
                TotalReturnLength = result.ReturnNetworks.Sum(n => n.TotalLength),
                FittingsCount = result.SupplyNetworks.Concat(result.ReturnNetworks).Sum(n => n.FittingsCount)
            };

        private PipeStatistics CalculatePipeStatistics(PipeRoutingResult result) =>
            new PipeStatistics
            {
                ColdWaterLength = result.ColdWaterNetwork?.TotalLength ?? 0,
                HotWaterLength = result.HotWaterNetwork?.TotalLength ?? 0,
                DrainageLength = result.DrainageNetwork?.TotalLength ?? 0,
                VentLength = result.DrainageNetwork?.VentRoutes.Sum(v => v.Length) ?? 0
            };

        private CableStatistics CalculateCableStatistics(CableRoutingResult result) =>
            new CableStatistics
            {
                MainDistributionLength = result.MainDistribution?.TotalLength ?? 0,
                CircuitLength = result.SubCircuits.Sum(c => c.Length),
                DataCableLength = result.DataNetwork?.TotalLength ?? 0
            };

        private SprinklerStatistics CalculateSprinklerStatistics(SprinklerRoutingResult result) =>
            new SprinklerStatistics
            {
                HeadCount = result.SprinklerHeads.Count,
                BranchLineLength = result.BranchLines.Sum(b => b.Length),
                CrossMainLength = result.CrossMains.Sum(c => c.Length),
                FeedMainLength = result.FeedMain?.Length ?? 0
            };

        #endregion
    }

    #region Pathfinding Engine

    internal class PathfindingEngine
    {
        private readonly RoutingSettings _settings;

        public PathfindingEngine(RoutingSettings settings)
        {
            _settings = settings;
        }

        public async Task<Route> FindRouteAsync(
            MEPConnection start, MEPConnection end, List<Obstacle> obstacles, MEPSystemType systemType)
        {
            return await Task.Run(() => FindRoute(start, end, obstacles, systemType));
        }

        public async Task<Route> FindRouteAsync(
            MEPConnection start, Point3D end, List<Obstacle> obstacles, MEPSystemType systemType)
        {
            return await FindRouteAsync(start, new MEPConnection { Position = end }, obstacles, systemType);
        }

        public async Task<Route> FindDrainageRouteAsync(
            MEPConnection start, MEPConnection end, List<Obstacle> obstacles, double slope)
        {
            return await Task.Run(() => FindDrainageRoute(start, end, obstacles, slope));
        }

        private Route FindRoute(MEPConnection start, MEPConnection end, List<Obstacle> obstacles, MEPSystemType systemType)
        {
            // A* pathfinding with MEP-specific heuristics
            var openSet = new SortedSet<PathNode>(new PathNodeComparer());
            var closedSet = new HashSet<string>();
            var cameFrom = new Dictionary<string, PathNode>();

            var startNode = new PathNode(start.Position, 0, Heuristic(start.Position, end.Position));
            openSet.Add(startNode);

            var preferredZ = GetPreferredRoutingHeight(systemType);
            var gridSize = _settings.RoutingGridSize;

            while (openSet.Any())
            {
                var current = openSet.First();
                openSet.Remove(current);

                if (IsNearTarget(current.Position, end.Position, gridSize))
                {
                    return ReconstructRoute(cameFrom, current, start, end);
                }

                closedSet.Add(current.Id);

                foreach (var neighbor in GetNeighbors(current, gridSize, preferredZ))
                {
                    if (closedSet.Contains(neighbor.Id)) continue;
                    if (IsBlocked(current.Position, neighbor.Position, obstacles)) continue;

                    var tentativeG = current.G + Distance(current.Position, neighbor.Position);

                    // Add penalties for direction changes
                    if (cameFrom.ContainsKey(current.Id))
                    {
                        var prev = cameFrom[current.Id];
                        if (HasDirectionChange(prev.Position, current.Position, neighbor.Position))
                            tentativeG += _settings.DirectionChangePenalty;
                    }

                    var existingNode = openSet.FirstOrDefault(n => n.Id == neighbor.Id);
                    if (existingNode != null)
                    {
                        if (tentativeG >= existingNode.G) continue;
                        openSet.Remove(existingNode);
                    }

                    neighbor.G = tentativeG;
                    neighbor.H = Heuristic(neighbor.Position, end.Position);
                    cameFrom[neighbor.Id] = current;
                    openSet.Add(neighbor);
                }
            }

            return null; // No path found
        }

        private Route FindDrainageRoute(MEPConnection start, MEPConnection end, List<Obstacle> obstacles, double slope)
        {
            // Modified A* that ensures downward slope
            var route = FindRoute(start, end, obstacles, MEPSystemType.Plumbing);
            if (route == null) return null;

            // Adjust heights to ensure proper slope
            route.RequiresVent = true;
            return route;
        }

        private double GetPreferredRoutingHeight(MEPSystemType systemType)
        {
            return systemType switch
            {
                MEPSystemType.HVAC => _settings.DuctRoutingHeight,
                MEPSystemType.Plumbing => _settings.PipeRoutingHeight,
                MEPSystemType.Electrical => _settings.CableRoutingHeight,
                MEPSystemType.FireProtection => _settings.SprinklerRoutingHeight,
                _ => _settings.DefaultRoutingHeight
            };
        }

        private List<PathNode> GetNeighbors(PathNode node, double gridSize, double preferredZ)
        {
            var neighbors = new List<PathNode>();
            var directions = new[]
            {
                (1, 0, 0), (-1, 0, 0),
                (0, 1, 0), (0, -1, 0),
                (0, 0, 1), (0, 0, -1)
            };

            foreach (var (dx, dy, dz) in directions)
            {
                var newPos = new Point3D(
                    node.Position.X + dx * gridSize,
                    node.Position.Y + dy * gridSize,
                    node.Position.Z + dz * gridSize);

                neighbors.Add(new PathNode(newPos, 0, 0));
            }

            return neighbors;
        }

        private bool IsNearTarget(Point3D current, Point3D target, double tolerance)
        {
            return Math.Abs(current.X - target.X) < tolerance &&
                   Math.Abs(current.Y - target.Y) < tolerance &&
                   Math.Abs(current.Z - target.Z) < tolerance;
        }

        private bool IsBlocked(Point3D from, Point3D to, List<Obstacle> obstacles)
        {
            foreach (var obstacle in obstacles)
            {
                if (LineIntersectsBox(from, to, obstacle.Bounds))
                    return true;
            }
            return false;
        }

        private bool LineIntersectsBox(Point3D from, Point3D to, BoundingBox box)
        {
            // Simplified AABB line intersection test
            var dir = new Point3D(to.X - from.X, to.Y - from.Y, to.Z - from.Z);
            var tMin = 0.0;
            var tMax = 1.0;

            for (int i = 0; i < 3; i++)
            {
                var origin = i == 0 ? from.X : (i == 1 ? from.Y : from.Z);
                var direction = i == 0 ? dir.X : (i == 1 ? dir.Y : dir.Z);
                var min = i == 0 ? box.MinX : (i == 1 ? box.MinY : box.MinZ);
                var max = i == 0 ? box.MaxX : (i == 1 ? box.MaxY : box.MaxZ);

                if (Math.Abs(direction) < 1e-10)
                {
                    if (origin < min || origin > max) return false;
                }
                else
                {
                    var t1 = (min - origin) / direction;
                    var t2 = (max - origin) / direction;
                    if (t1 > t2) (t1, t2) = (t2, t1);
                    tMin = Math.Max(tMin, t1);
                    tMax = Math.Min(tMax, t2);
                    if (tMin > tMax) return false;
                }
            }

            return true;
        }

        private bool HasDirectionChange(Point3D prev, Point3D current, Point3D next)
        {
            var dir1 = Normalize(new Point3D(current.X - prev.X, current.Y - prev.Y, current.Z - prev.Z));
            var dir2 = Normalize(new Point3D(next.X - current.X, next.Y - current.Y, next.Z - current.Z));
            var dot = dir1.X * dir2.X + dir1.Y * dir2.Y + dir1.Z * dir2.Z;
            return dot < 0.99; // Not parallel
        }

        private Point3D Normalize(Point3D p)
        {
            var len = Math.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
            return len > 0 ? new Point3D(p.X / len, p.Y / len, p.Z / len) : p;
        }

        private double Heuristic(Point3D a, Point3D b) =>
            Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z); // Manhattan

        private double Distance(Point3D a, Point3D b) =>
            Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));

        private Route ReconstructRoute(Dictionary<string, PathNode> cameFrom, PathNode current,
            MEPConnection start, MEPConnection end)
        {
            var points = new List<Point3D> { current.Position };
            while (cameFrom.ContainsKey(current.Id))
            {
                current = cameFrom[current.Id];
                points.Add(current.Position);
            }
            points.Reverse();

            return new Route
            {
                Id = Guid.NewGuid().ToString(),
                Points = points,
                StartPoint = start.Position,
                EndPoint = end.Position,
                Length = CalculateLength(points)
            };
        }

        private double CalculateLength(List<Point3D> points)
        {
            double length = 0;
            for (int i = 1; i < points.Count; i++)
                length += Distance(points[i - 1], points[i]);
            return length;
        }
    }

    internal class PathNode
    {
        public Point3D Position { get; }
        public double G { get; set; }
        public double H { get; set; }
        public double F => G + H;
        public string Id => $"{Position.X:F2},{Position.Y:F2},{Position.Z:F2}";

        public PathNode(Point3D position, double g, double h)
        {
            Position = position;
            G = g;
            H = h;
        }
    }

    internal class PathNodeComparer : IComparer<PathNode>
    {
        public int Compare(PathNode x, PathNode y)
        {
            var fCompare = x.F.CompareTo(y.F);
            return fCompare != 0 ? fCompare : string.Compare(x.Id, y.Id, StringComparison.Ordinal);
        }
    }

    #endregion

    #region Sizing Calculator

    internal class SizingCalculator
    {
        public RouteSizing CalculateSizing(Route route, MEPSystemType systemType, double load)
        {
            return systemType switch
            {
                MEPSystemType.HVAC => CalculateDuctSizing(load),
                MEPSystemType.Plumbing => CalculatePipeSizing(load),
                MEPSystemType.Electrical => CalculateCableSizing(load),
                MEPSystemType.FireProtection => CalculateSprinklerPipeSizing(load),
                _ => new RouteSizing()
            };
        }

        public void SizeDuctNetwork(DuctNetwork network, double maxVelocity, double maxPressureDrop)
        {
            // Size from terminals back to source
            foreach (var branch in network.BranchRoutes)
            {
                var airflow = branch.DesignAirflow;
                branch.Sizing = CalculateDuctSizeForAirflow(airflow, maxVelocity);
            }

            foreach (var trunk in network.TrunkRoutes)
            {
                var totalAirflow = network.BranchRoutes
                    .Where(b => b.ParentId == trunk.Id)
                    .Sum(b => b.DesignAirflow);
                trunk.Sizing = CalculateDuctSizeForAirflow(totalAirflow, maxVelocity);
            }
        }

        public void SizeCable(Route route, double voltage, double maxVoltageDrop)
        {
            var current = route.DesignLoad / voltage;
            var length = route.Length;

            // Calculate minimum size for voltage drop
            var minArea = (2 * current * length * 0.0172) / (maxVoltageDrop * voltage / 100);

            route.Sizing = new RouteSizing
            {
                Width = GetCableSizeFromArea(minArea),
                Type = "Cable"
            };
        }

        public void SizeSprinklerSystem(SprinklerRoutingResult result, double density, double remoteArea)
        {
            // Hydraulic calculation (simplified)
            var requiredFlow = density * remoteArea; // GPM

            foreach (var branch in result.BranchLines)
            {
                branch.Sizing = new RouteSizing { Width = 25, Height = 25, Type = "Sprinkler Pipe" }; // 1"
            }

            foreach (var crossMain in result.CrossMains)
            {
                crossMain.Sizing = new RouteSizing { Width = 50, Height = 50, Type = "Sprinkler Pipe" }; // 2"
            }

            if (result.FeedMain != null)
            {
                result.FeedMain.Sizing = new RouteSizing { Width = 80, Height = 80, Type = "Sprinkler Pipe" }; // 3"
            }
        }

        private RouteSizing CalculateDuctSizing(double airflowCFM)
        {
            // Simplified duct sizing
            var velocity = 1500; // fpm typical
            var area = airflowCFM / velocity; // sq ft
            var diameter = Math.Sqrt(4 * area / Math.PI) * 12; // inches

            return new RouteSizing
            {
                Width = Math.Ceiling(diameter / 2) * 2 * 25.4, // Round to even inches, convert to mm
                Height = Math.Ceiling(diameter / 2) * 2 * 25.4,
                Type = "Round Duct"
            };
        }

        private RouteSizing CalculateDuctSizeForAirflow(double airflowCFM, double maxVelocity)
        {
            var area = airflowCFM / maxVelocity;
            var diameter = Math.Sqrt(4 * area / Math.PI) * 12;
            var roundedDiameter = Math.Max(6, Math.Ceiling(diameter / 2) * 2);

            return new RouteSizing
            {
                Width = roundedDiameter * 25.4,
                Height = roundedDiameter * 25.4,
                Type = diameter > 24 ? "Rectangular Duct" : "Round Duct"
            };
        }

        private RouteSizing CalculatePipeSizing(double flowGPM)
        {
            // Based on fixture units and flow
            double diameter = flowGPM switch
            {
                < 5 => 15,    // 1/2"
                < 10 => 20,   // 3/4"
                < 20 => 25,   // 1"
                < 40 => 32,   // 1-1/4"
                < 75 => 40,   // 1-1/2"
                < 150 => 50,  // 2"
                < 300 => 65,  // 2-1/2"
                _ => 80       // 3"
            };

            return new RouteSizing { Width = diameter, Height = diameter, Type = "Pipe" };
        }

        private RouteSizing CalculateCableSizing(double loadWatts)
        {
            var current = loadWatts / 240; // Assume 240V
            double cableSize = current switch
            {
                < 15 => 2.5,   // 2.5mm²
                < 20 => 4,     // 4mm²
                < 25 => 6,     // 6mm²
                < 32 => 10,    // 10mm²
                < 40 => 16,    // 16mm²
                < 50 => 25,    // 25mm²
                _ => 35        // 35mm²
            };

            return new RouteSizing { Width = cableSize, Type = "Cable" };
        }

        private RouteSizing CalculateSprinklerPipeSizing(double flowGPM)
        {
            double diameter = flowGPM switch
            {
                < 20 => 25,   // 1"
                < 50 => 32,   // 1-1/4"
                < 100 => 40,  // 1-1/2"
                < 200 => 50,  // 2"
                < 400 => 65,  // 2-1/2"
                _ => 80       // 3"
            };

            return new RouteSizing { Width = diameter, Height = diameter, Type = "Sprinkler Pipe" };
        }

        private double GetCableSizeFromArea(double areaMM2)
        {
            double[] standardSizes = { 1.5, 2.5, 4, 6, 10, 16, 25, 35, 50, 70, 95, 120 };
            return standardSizes.FirstOrDefault(s => s >= areaMM2) > 0
                ? standardSizes.First(s => s >= areaMM2)
                : 120;
        }
    }

    #endregion

    #region Clearance Checker

    internal class ClearanceChecker
    {
        private readonly RoutingSettings _settings;

        public ClearanceChecker(RoutingSettings settings)
        {
            _settings = settings;
        }

        public ClearanceValidation ValidateClearances(Route route, BuildingModel building)
        {
            var validation = new ClearanceValidation { IsValid = true };

            // Check structural clearances
            foreach (var beam in building.Beams)
            {
                var clearance = CalculateClearance(route, beam.Bounds);
                if (clearance < _settings.MinStructuralClearance)
                {
                    validation.IsValid = false;
                    validation.Violations.Add(new ClearanceViolation
                    {
                        Type = "Structural",
                        ElementId = beam.Id,
                        ActualClearance = clearance,
                        RequiredClearance = _settings.MinStructuralClearance
                    });
                }
            }

            // Check access clearances
            foreach (var point in route.Points)
            {
                if (!HasAccessClearance(point, building))
                {
                    validation.Violations.Add(new ClearanceViolation
                    {
                        Type = "Access",
                        Position = point,
                        RequiredClearance = _settings.MinAccessClearance
                    });
                }
            }

            return validation;
        }

        private double CalculateClearance(Route route, BoundingBox obstacle)
        {
            double minClearance = double.MaxValue;

            foreach (var point in route.Points)
            {
                var clearance = DistanceToBox(point, obstacle);
                minClearance = Math.Min(minClearance, clearance);
            }

            return minClearance;
        }

        private double DistanceToBox(Point3D point, BoundingBox box)
        {
            var dx = Math.Max(0, Math.Max(box.MinX - point.X, point.X - box.MaxX));
            var dy = Math.Max(0, Math.Max(box.MinY - point.Y, point.Y - box.MaxY));
            var dz = Math.Max(0, Math.Max(box.MinZ - point.Z, point.Z - box.MaxZ));
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private bool HasAccessClearance(Point3D point, BuildingModel building)
        {
            // Check if there's enough space around the point for maintenance access
            return true; // Simplified
        }
    }

    #endregion

    #region Data Models

    public class RoutingSettings
    {
        public double RoutingGridSize { get; set; } = 0.1; // meters
        public double DirectionChangePenalty { get; set; } = 0.5;
        public double DuctRoutingHeight { get; set; } = 2.8; // meters
        public double PipeRoutingHeight { get; set; } = 2.6;
        public double CableRoutingHeight { get; set; } = 2.9;
        public double SprinklerRoutingHeight { get; set; } = 2.85;
        public double DefaultRoutingHeight { get; set; } = 2.7;
        public double MinStructuralClearance { get; set; } = 0.05; // 50mm
        public double MinAccessClearance { get; set; } = 0.6; // 600mm
        public double DrainageSlope { get; set; } = 0.02; // 2%
        public double MaxBranchLength { get; set; } = 10; // meters
    }

    public class RoutingProgress
    {
        public string Phase { get; set; }
        public int PercentComplete { get; set; }
        public string CurrentOperation { get; set; }
    }

    public class RoutingResult
    {
        public Dictionary<MEPSystemType, SystemRoutingResult> SystemResults { get; } = new();
        public RoutingStatistics Statistics { get; set; }
    }

    public class SystemRoutingResult
    {
        public MEPSystemType SystemType { get; set; }
        public List<Route> Routes { get; } = new();
        public List<MEPConnectionPair> FailedConnections { get; } = new();
    }

    public class RoutingStatistics
    {
        public int TotalRoutes { get; set; }
        public double TotalLength { get; set; }
        public int FailedConnections { get; set; }
    }

    public class Route
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ParentId { get; set; }
        public List<Point3D> Points { get; set; } = new();
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public double Length { get; set; }
        public double DesignAirflow { get; set; }
        public double DesignLoad { get; set; }
        public double Slope { get; set; }
        public bool RequiresVent { get; set; }
        public bool IsVent { get; set; }
        public string SprinklerHeadId { get; set; }
        public CableType CableType { get; set; }
        public CircuitType CircuitType { get; set; }
        public RouteSizing Sizing { get; set; }
        public ClearanceValidation Clearances { get; set; }
        public MEPConnection VentConnectionPoint => new() { Position = Points.LastOrDefault() };
    }

    public class RouteSizing
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public string Type { get; set; }
    }

    public class ClearanceValidation
    {
        public bool IsValid { get; set; }
        public List<ClearanceViolation> Violations { get; } = new();
    }

    public class ClearanceViolation
    {
        public string Type { get; set; }
        public string ElementId { get; set; }
        public Point3D Position { get; set; }
        public double ActualClearance { get; set; }
        public double RequiredClearance { get; set; }
    }

    public class DuctRoutingResult
    {
        public List<DuctNetwork> SupplyNetworks { get; } = new();
        public List<DuctNetwork> ReturnNetworks { get; } = new();
        public DuctStatistics Statistics { get; set; }
    }

    public class DuctNetwork
    {
        public DuctType Type { get; set; }
        public string SourceId { get; set; }
        public List<Route> TrunkRoutes { get; } = new();
        public List<Route> BranchRoutes { get; } = new();
        public double TotalLength => TrunkRoutes.Sum(r => r.Length) + BranchRoutes.Sum(r => r.Length);
        public int FittingsCount => TrunkRoutes.Count + BranchRoutes.Count;
    }

    public class DuctStatistics
    {
        public double TotalSupplyLength { get; set; }
        public double TotalReturnLength { get; set; }
        public int FittingsCount { get; set; }
    }

    public class PipeRoutingResult
    {
        public PipeNetwork ColdWaterNetwork { get; set; }
        public PipeNetwork HotWaterNetwork { get; set; }
        public DrainageNetwork DrainageNetwork { get; set; }
        public PipeNetwork HydronicNetwork { get; set; }
        public PipeStatistics Statistics { get; set; }
    }

    public class PipeNetwork
    {
        public PipeSystemType SystemType { get; set; }
        public List<Route> MainRoutes { get; } = new();
        public List<Route> BranchRoutes { get; } = new();
        public double TotalLength => MainRoutes.Sum(r => r.Length) + BranchRoutes.Sum(r => r.Length);
    }

    public class DrainageNetwork
    {
        public List<Route> DrainRoutes { get; } = new();
        public List<Route> VentRoutes { get; set; } = new();
        public double TotalLength => DrainRoutes.Sum(r => r.Length);
    }

    public class PipeStatistics
    {
        public double ColdWaterLength { get; set; }
        public double HotWaterLength { get; set; }
        public double DrainageLength { get; set; }
        public double VentLength { get; set; }
    }

    public class CableRoutingResult
    {
        public CableNetwork MainDistribution { get; set; }
        public List<CableRoute> SubCircuits { get; } = new();
        public CableNetwork DataNetwork { get; set; }
        public CableStatistics Statistics { get; set; }
        public IEnumerable<Route> AllRoutes =>
            (MainDistribution?.Routes ?? Enumerable.Empty<Route>())
            .Concat(SubCircuits)
            .Concat(DataNetwork?.Routes ?? Enumerable.Empty<Route>());
    }

    public class CableNetwork
    {
        public CableNetworkType Type { get; set; }
        public List<Route> Routes { get; } = new();
        public double TotalLength => Routes.Sum(r => r.Length);
    }

    public class CableRoute : Route
    {
        public string CircuitId { get; set; }
        public CableRoute(Route route)
        {
            Id = route.Id;
            Points = route.Points;
            StartPoint = route.StartPoint;
            EndPoint = route.EndPoint;
            Length = route.Length;
        }
    }

    public class CableStatistics
    {
        public double MainDistributionLength { get; set; }
        public double CircuitLength { get; set; }
        public double DataCableLength { get; set; }
    }

    public class SprinklerRoutingResult
    {
        public List<SprinklerHead> SprinklerHeads { get; set; } = new();
        public List<Route> BranchLines { get; } = new();
        public List<Route> CrossMains { get; } = new();
        public Route FeedMain { get; set; }
        public SprinklerStatistics Statistics { get; set; }
    }

    public class SprinklerHead
    {
        public string Id { get; set; }
        public string ZoneId { get; set; }
        public Point3D Position { get; set; }
        public SprinklerHeadType Type { get; set; }
    }

    public class SprinklerStatistics
    {
        public int HeadCount { get; set; }
        public double BranchLineLength { get; set; }
        public double CrossMainLength { get; set; }
        public double FeedMainLength { get; set; }
    }

    // Building/MEP Models
    public class BuildingModel
    {
        public List<Space> Spaces { get; set; } = new();
        public List<Column> Columns { get; set; } = new();
        public List<Beam> Beams { get; set; } = new();
        public List<Wall> Walls { get; set; } = new();
        public List<ExistingDuct> ExistingDucts { get; set; } = new();
        public List<ExistingPipe> ExistingPipes { get; set; } = new();

        public List<Point3D> GetRoofPenetrationPoints() => new();
        public List<Point3D> GetRiserLocations() => new();
    }

    public class Space
    {
        public string Id { get; set; }
        public string ZoneId { get; set; }
        public BoundingBox Bounds { get; set; }
        public bool RequiresSprinklers { get; set; } = true;
    }

    public class Column { public string Id { get; set; } public BoundingBox Bounds { get; set; } }
    public class Beam { public string Id { get; set; } public BoundingBox Bounds { get; set; } }
    public class Wall { public BoundingBox Bounds { get; set; } public bool IsStructural { get; set; } }
    public class ExistingDuct { public BoundingBox Bounds { get; set; } }
    public class ExistingPipe { public BoundingBox Bounds { get; set; } }

    public class MEPRequirements
    {
        public HVACRequirements HVAC { get; set; }
        public PlumbingRequirements Plumbing { get; set; }
        public ElectricalRequirements Electrical { get; set; }
        public FireProtectionRequirements FireProtection { get; set; }

        public List<MEPConnectionPair> GetConnections(MEPSystemType type) => new();
    }

    public class HVACRequirements
    {
        public List<AirHandlingUnit> AirHandlingUnits { get; set; } = new();
        public List<TerminalUnit> TerminalUnits { get; set; } = new();
        public List<ReturnGrille> ReturnGrilles { get; set; } = new();
        public double MaxVelocity { get; set; } = 1500; // fpm
        public double MaxPressureDrop { get; set; } = 0.1; // in.wg per 100ft
    }

    public class PlumbingRequirements
    {
        public MEPConnection WaterEntry { get; set; }
        public MEPConnection WaterHeater { get; set; }
        public MEPConnection MainDrain { get; set; }
        public MEPConnection HydronicSource { get; set; }
        public List<MEPFixture> ColdWaterFixtures { get; set; } = new();
        public List<MEPFixture> HotWaterFixtures { get; set; } = new();
        public List<MEPFixture> DrainageFixtures { get; set; } = new();
        public List<MEPFixture> HydronicTerminals { get; set; } = new();
    }

    public class ElectricalRequirements
    {
        public Switchboard MainSwitchboard { get; set; }
        public List<DistributionBoard> DistributionBoards { get; set; } = new();
        public List<ElectricalOutlet> Outlets { get; set; } = new();
        public DataRoom DataRoom { get; set; }
        public List<DataOutlet> DataOutlets { get; set; } = new();
        public double Voltage { get; set; } = 240;
        public double MaxVoltageDrop { get; set; } = 3; // percent
    }

    public class FireProtectionRequirements
    {
        public SprinklerRiser Riser { get; set; }
        public List<FireZone> Zones { get; set; } = new();
        public HazardClass HazardClass { get; set; } = HazardClass.OrdinaryI;
        public double CeilingHeight { get; set; } = 3.0;
        public double DesignDensity { get; set; } = 0.1; // GPM/sq.ft
        public double RemoteArea { get; set; } = 1500; // sq.ft
    }

    public class MEPConnection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Point3D Position { get; set; }
    }

    public class MEPConnectionPair
    {
        public string Id { get; set; }
        public MEPConnection Start { get; set; }
        public MEPConnection End { get; set; }
        public double Load { get; set; }
    }

    public class AirHandlingUnit
    {
        public string Id { get; set; }
        public MEPConnection SupplyConnection { get; set; }
        public MEPConnection ReturnConnection { get; set; }
    }

    public class TerminalUnit
    {
        public string Id { get; set; }
        public string ZoneId { get; set; }
        public MEPConnection Connection { get; set; }
    }

    public class ReturnGrille
    {
        public string Id { get; set; }
        public MEPConnection Connection { get; set; }
    }

    public class MEPFixture
    {
        public string Id { get; set; }
        public int Level { get; set; }
        public MEPConnection Connection { get; set; }
        public MEPConnection DrainConnection { get; set; }
    }

    public class Switchboard { public MEPConnection Connection { get; set; } }
    public class DistributionBoard { public MEPConnection Connection { get; set; } }
    public class ElectricalOutlet
    {
        public MEPConnection Connection { get; set; }
        public double LoadWatts { get; set; }
        public CircuitType RequiredCircuitType { get; set; }
    }
    public class DataRoom { public MEPConnection PatchPanel { get; set; } }
    public class DataOutlet { public MEPConnection Connection { get; set; } }
    public class SprinklerRiser { public MEPConnection Connection { get; set; } }
    public class FireZone { public string Id { get; set; } }

    public class RiserGroup
    {
        public MEPConnection BaseConnection { get; set; }
        public List<MEPFixture> Fixtures { get; set; } = new();
        public MEPConnection GetConnectionAtLevel(int level) => BaseConnection;
    }

    public class Circuit
    {
        public string Id { get; set; }
        public CircuitType Type { get; set; }
        public List<ElectricalOutlet> Outlets { get; } = new();
    }

    public class Obstacle
    {
        public BoundingBox Bounds { get; }
        public ObstacleType Type { get; }
        public Obstacle(BoundingBox bounds, ObstacleType type) { Bounds = bounds; Type = type; }
    }

    public class SystemEndpoints
    {
        public List<MEPConnectionPair> Connections { get; set; } = new();
    }

    public class BoundingBox
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
        public double Width => MaxX - MinX;
        public double Length => MaxY - MinY;
        public double Height => MaxZ - MinZ;
    }

    // Enums
    public enum MEPSystemType { HVAC, Plumbing, Electrical, FireProtection }
    public enum DuctType { Supply, Return, Exhaust }
    public enum PipeSystemType { DomesticColdWater, DomesticHotWater, Drainage, Hydronic }
    public enum CableNetworkType { MainDistribution, SubCircuit, Data }
    public enum CircuitType { Power, Lighting, Appliance, Feeder }
    public enum CableType { Power, Cat6A, Cat6, Coax, Fiber }
    public enum SprinklerHeadType { Pendant, Upright, Sidewall, Concealed }
    public enum HazardClass { Light, OrdinaryI, OrdinaryII, ExtraI, ExtraII }
    public enum ObstacleType { Structural, MEP, Architectural }

    #endregion
}
