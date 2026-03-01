// ============================================================================
// StingBIM AI - Space Management
// Tracks spaces/rooms from BIM model for FM operations
// Integrates with Revit room data and occupancy tracking
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.FacilityManagement.SpaceManagement
{
    /// <summary>
    /// Space/Room definition for facility management
    /// </summary>
    public class Space
    {
        public string SpaceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // BIM Link
        public Guid? RevitRoomId { get; set; }
        public string? IFCSpaceGuid { get; set; }

        // Location
        public string BuildingId { get; set; } = string.Empty;
        public string FloorId { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public string ParentSpaceId { get; set; } = string.Empty;

        // Classification
        public SpaceType Type { get; set; }
        public string UsageType { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string CostCenter { get; set; } = string.Empty;

        // Dimensions
        public double GrossArea { get; set; }
        public double NetArea { get; set; }
        public double Height { get; set; }
        public double Volume { get; set; }
        public double Perimeter { get; set; }

        // Occupancy
        public int DesignCapacity { get; set; }
        public int CurrentOccupancy { get; set; }
        public double OccupancyPercent => DesignCapacity > 0 ? (CurrentOccupancy * 100.0 / DesignCapacity) : 0;

        // Contact
        public string ContactPerson { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;

        // Access
        public AccessLevel AccessLevel { get; set; }
        public string OperatingHours { get; set; } = string.Empty;

        // Status
        public SpaceStatus Status { get; set; } = SpaceStatus.Active;

        // Attributes
        public Dictionary<string, object> Attributes { get; set; } = new();
    }

    public enum SpaceType
    {
        Office,
        MeetingRoom,
        Reception,
        Corridor,
        Lobby,
        Washroom,
        Kitchen,
        PlantRoom,
        Storage,
        Parking,
        Retail,
        DataCenter,
        Laboratory,
        Workshop,
        Other
    }

    public enum SpaceStatus
    {
        Active,
        Vacant,
        UnderRenovation,
        Reserved,
        Decommissioned
    }

    public enum AccessLevel
    {
        Public,
        Tenant,
        Staff,
        Restricted,
        VIP,
        SecurityOnly
    }

    /// <summary>
    /// Space registry for managing facility spaces
    /// </summary>
    public class SpaceRegistry
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, Space> _spacesById;
        private readonly Dictionary<Guid, Space> _spacesByRevitId;
        private readonly Dictionary<string, List<Space>> _spacesByFloor;
        private readonly object _lock = new();

        public SpaceRegistry()
        {
            _spacesById = new Dictionary<string, Space>(StringComparer.OrdinalIgnoreCase);
            _spacesByRevitId = new Dictionary<Guid, Space>();
            _spacesByFloor = new Dictionary<string, List<Space>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Load spaces from CSV
        /// </summary>
        public async Task LoadFromCsvAsync(string csvPath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(csvPath)) return;

            var lines = await File.ReadAllLinesAsync(csvPath, cancellationToken);
            var headers = lines[0].Split(',');

            foreach (var line in lines.Skip(1))
            {
                var values = line.Split(',');
                if (values.Length < headers.Length) continue;

                var space = new Space
                {
                    SpaceId = GetValue(values, headers, "LocationId"),
                    Name = GetValue(values, headers, "Name"),
                    FloorId = GetValue(values, headers, "FloorId"),
                    Zone = GetValue(values, headers, "Zone"),
                    Department = GetValue(values, headers, "Department"),
                    UsageType = GetValue(values, headers, "UsageType"),
                    CostCenter = GetValue(values, headers, "CostCenter"),
                    ContactPerson = GetValue(values, headers, "ContactPerson"),
                    ContactPhone = GetValue(values, headers, "ContactPhone"),
                    OperatingHours = GetValue(values, headers, "OperatingHours")
                };

                if (double.TryParse(GetValue(values, headers, "Area_SQM"), out var area))
                    space.GrossArea = area;
                if (int.TryParse(GetValue(values, headers, "Capacity"), out var cap))
                    space.DesignCapacity = cap;

                RegisterSpace(space);
            }

            Logger.Info($"Loaded {_spacesById.Count} spaces");
        }

        public void RegisterSpace(Space space)
        {
            lock (_lock)
            {
                _spacesById[space.SpaceId] = space;

                if (space.RevitRoomId.HasValue)
                    _spacesByRevitId[space.RevitRoomId.Value] = space;

                if (!string.IsNullOrEmpty(space.FloorId))
                {
                    if (!_spacesByFloor.ContainsKey(space.FloorId))
                        _spacesByFloor[space.FloorId] = new List<Space>();
                    _spacesByFloor[space.FloorId].Add(space);
                }
            }
        }

        public Space? GetById(string spaceId)
        {
            lock (_lock)
            {
                return _spacesById.TryGetValue(spaceId, out var space) ? space : null;
            }
        }

        public Space? GetByRevitId(Guid revitId)
        {
            lock (_lock)
            {
                return _spacesByRevitId.TryGetValue(revitId, out var space) ? space : null;
            }
        }

        public IReadOnlyList<Space> GetByFloor(string floorId)
        {
            lock (_lock)
            {
                return _spacesByFloor.TryGetValue(floorId, out var spaces)
                    ? spaces.ToList()
                    : new List<Space>();
            }
        }

        public IReadOnlyList<Space> GetAll()
        {
            lock (_lock)
            {
                return _spacesById.Values.ToList();
            }
        }

        /// <summary>
        /// Calculate space utilization metrics
        /// </summary>
        public SpaceUtilizationReport GetUtilizationReport()
        {
            lock (_lock)
            {
                var spaces = _spacesById.Values.ToList();
                var occupiable = spaces.Where(s => s.DesignCapacity > 0).ToList();

                return new SpaceUtilizationReport
                {
                    TotalSpaces = spaces.Count,
                    TotalArea = spaces.Sum(s => s.GrossArea),
                    TotalCapacity = occupiable.Sum(s => s.DesignCapacity),
                    CurrentOccupancy = occupiable.Sum(s => s.CurrentOccupancy),
                    OccupancyRate = occupiable.Sum(s => s.DesignCapacity) > 0
                        ? occupiable.Sum(s => s.CurrentOccupancy) * 100.0 / occupiable.Sum(s => s.DesignCapacity)
                        : 0,
                    ByFloor = _spacesByFloor.ToDictionary(
                        f => f.Key,
                        f => new FloorUtilization
                        {
                            FloorId = f.Key,
                            SpaceCount = f.Value.Count,
                            TotalArea = f.Value.Sum(s => s.GrossArea),
                            Capacity = f.Value.Sum(s => s.DesignCapacity),
                            Occupancy = f.Value.Sum(s => s.CurrentOccupancy)
                        }),
                    ByType = spaces.GroupBy(s => s.UsageType)
                        .ToDictionary(g => g.Key, g => g.Sum(s => s.GrossArea)),
                    VacantSpaces = spaces.Count(s => s.Status == SpaceStatus.Vacant),
                    UnderRenovation = spaces.Count(s => s.Status == SpaceStatus.UnderRenovation)
                };
            }
        }

        private string GetValue(string[] values, string[] headers, string column)
        {
            var idx = Array.IndexOf(headers, column);
            return idx >= 0 && idx < values.Length ? values[idx].Trim() : string.Empty;
        }
    }

    public class SpaceUtilizationReport
    {
        public int TotalSpaces { get; set; }
        public double TotalArea { get; set; }
        public int TotalCapacity { get; set; }
        public int CurrentOccupancy { get; set; }
        public double OccupancyRate { get; set; }
        public Dictionary<string, FloorUtilization> ByFloor { get; set; } = new();
        public Dictionary<string, double> ByType { get; set; } = new();
        public int VacantSpaces { get; set; }
        public int UnderRenovation { get; set; }
    }

    public class FloorUtilization
    {
        public string FloorId { get; set; } = string.Empty;
        public int SpaceCount { get; set; }
        public double TotalArea { get; set; }
        public int Capacity { get; set; }
        public int Occupancy { get; set; }
        public double OccupancyRate => Capacity > 0 ? Occupancy * 100.0 / Capacity : 0;
    }
}
