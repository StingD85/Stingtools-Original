// StingBIM.AI.Reasoning.Spatial.SpatialReasoner
// Spatial reasoning engine for architectural space analysis
// Master Proposal Reference: Part 2.1 Pillar 2 - Spatial Intelligence

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using StingBIM.AI.Core.Common;

namespace StingBIM.AI.Reasoning.Spatial
{
    /// <summary>
    /// Spatial reasoning engine that analyzes and reasons about architectural spaces.
    /// Handles proximity analysis, spatial relationships, circulation, and layout optimization.
    /// </summary>
    public class SpatialReasoner
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Spatial constants
        private const double CollisionTolerance = 0.05; // 5cm tolerance
        private const double AdjacencyThreshold = 0.3; // 30cm = considered adjacent
        private const double ProximityNear = 3.0; // meters
        private const double ProximityFar = 10.0; // meters

        private readonly List<SpatialEntity> _entities;
        private readonly SpatialIndex _spatialIndex;
        private readonly Dictionary<string, SpatialRelationship> _relationships;

        public SpatialReasoner()
        {
            _entities = new List<SpatialEntity>();
            _spatialIndex = new SpatialIndex();
            _relationships = new Dictionary<string, SpatialRelationship>();
        }

        #region Entity Management

        /// <summary>
        /// Adds a spatial entity to the reasoner.
        /// </summary>
        public void AddEntity(SpatialEntity entity)
        {
            _entities.Add(entity);
            _spatialIndex.Insert(entity);

            // Update relationships with existing entities
            UpdateRelationships(entity);

            Logger.Debug($"Added entity {entity.Id} at ({entity.BoundingBox.Center})");
        }

        /// <summary>
        /// Removes an entity from the reasoner.
        /// </summary>
        public void RemoveEntity(string entityId)
        {
            var entity = _entities.FirstOrDefault(e => e.Id == entityId);
            if (entity != null)
            {
                _entities.Remove(entity);
                _spatialIndex.Remove(entity);

                // Remove relationships involving this entity
                var keysToRemove = _relationships.Keys
                    .Where(k => k.Contains(entityId))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _relationships.Remove(key);
                }

                Logger.Debug($"Removed entity {entityId}");
            }
        }

        /// <summary>
        /// Gets all entities of a specific type.
        /// </summary>
        public IEnumerable<SpatialEntity> GetEntities(string entityType = null)
        {
            return entityType == null
                ? _entities
                : _entities.Where(e => e.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Spatial Queries

        /// <summary>
        /// Finds entities within a specified distance from a point.
        /// </summary>
        public IEnumerable<SpatialEntity> FindNearby(Point3D point, double radius)
        {
            return _spatialIndex.QueryRadius(point, radius);
        }

        /// <summary>
        /// Finds entities within a bounding box.
        /// </summary>
        public IEnumerable<SpatialEntity> FindInBounds(BoundingBox bounds)
        {
            return _spatialIndex.QueryBounds(bounds);
        }

        /// <summary>
        /// Checks if an entity at the given bounds would collide with existing entities.
        /// </summary>
        public CollisionResult CheckCollisions(BoundingBox proposedBounds, string excludeEntityId = null)
        {
            var result = new CollisionResult { HasCollision = false };

            foreach (var entity in _entities)
            {
                if (entity.Id == excludeEntityId) continue;

                if (BoundsIntersect(proposedBounds, entity.BoundingBox, CollisionTolerance))
                {
                    result.HasCollision = true;
                    result.CollidingEntities.Add(entity);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the spatial relationship between two entities.
        /// </summary>
        public SpatialRelationship GetRelationship(string entityId1, string entityId2)
        {
            var key = GetRelationshipKey(entityId1, entityId2);

            if (_relationships.TryGetValue(key, out var relationship))
            {
                return relationship;
            }

            // Calculate relationship on demand
            var entity1 = _entities.FirstOrDefault(e => e.Id == entityId1);
            var entity2 = _entities.FirstOrDefault(e => e.Id == entityId2);

            if (entity1 != null && entity2 != null)
            {
                return CalculateRelationship(entity1, entity2);
            }

            return null;
        }

        /// <summary>
        /// Finds all entities adjacent to a given entity.
        /// </summary>
        public IEnumerable<SpatialEntity> FindAdjacent(string entityId)
        {
            var entity = _entities.FirstOrDefault(e => e.Id == entityId);
            if (entity == null) yield break;

            foreach (var other in _entities.Where(e => e.Id != entityId))
            {
                var distance = CalculateDistance(entity.BoundingBox, other.BoundingBox);
                if (distance <= AdjacencyThreshold)
                {
                    yield return other;
                }
            }
        }

        /// <summary>
        /// Determines if two rooms are adjacent (share a wall).
        /// </summary>
        public bool AreAdjacent(string entityId1, string entityId2)
        {
            var relationship = GetRelationship(entityId1, entityId2);
            return relationship?.Type == RelationshipType.Adjacent;
        }

        #endregion

        #region Spatial Analysis

        /// <summary>
        /// Analyzes circulation paths between spaces.
        /// </summary>
        public CirculationAnalysis AnalyzeCirculation(string fromEntityId, string toEntityId)
        {
            var analysis = new CirculationAnalysis();

            var from = _entities.FirstOrDefault(e => e.Id == fromEntityId);
            var to = _entities.FirstOrDefault(e => e.Id == toEntityId);

            if (from == null || to == null)
            {
                analysis.IsReachable = false;
                analysis.Reason = "Entity not found";
                return analysis;
            }

            // Calculate direct distance
            analysis.DirectDistance = CalculateDistance(from.BoundingBox.Center, to.BoundingBox.Center);

            // Check for direct line of sight
            analysis.HasDirectPath = !HasObstructions(from.BoundingBox.Center, to.BoundingBox.Center);

            // Find path through connected spaces
            var path = FindPath(from, to);
            analysis.Path = path;
            analysis.IsReachable = path != null && path.Count > 0;
            analysis.PathLength = CalculatePathLength(path);

            // Calculate circulation efficiency
            if (analysis.PathLength > 0)
            {
                analysis.Efficiency = analysis.DirectDistance / analysis.PathLength;
            }

            return analysis;
        }

        /// <summary>
        /// Analyzes spatial quality of a room.
        /// </summary>
        public SpatialQualityAnalysis AnalyzeSpatialQuality(string roomEntityId)
        {
            var analysis = new SpatialQualityAnalysis();

            var room = _entities.FirstOrDefault(e => e.Id == roomEntityId);
            if (room == null) return analysis;

            var bbox = room.BoundingBox;
            var width = bbox.Max.X - bbox.Min.X;
            var length = bbox.Max.Y - bbox.Min.Y;
            var height = bbox.Max.Z - bbox.Min.Z;

            // Calculate metrics
            analysis.Area = width * length;
            analysis.Volume = width * length * height;
            analysis.AspectRatio = Math.Max(width, length) / Math.Min(width, length);
            analysis.CeilingHeight = height;

            // Evaluate proportions (golden ratio is ~1.618)
            var idealRatio = 1.618;
            analysis.ProportionScore = 1.0 - Math.Min(1.0, Math.Abs(analysis.AspectRatio - idealRatio) / idealRatio);

            // Evaluate ceiling height (2.7m ideal for residential)
            var idealHeight = 2.7;
            analysis.HeightScore = Math.Min(1.0, height / idealHeight);

            // Calculate overall quality score
            analysis.OverallScore = (analysis.ProportionScore + analysis.HeightScore) / 2.0;

            // Generate recommendations
            if (analysis.AspectRatio > 2.5)
            {
                analysis.Recommendations.Add("Room is elongated. Consider subdividing or adjusting proportions.");
            }

            if (height < 2.4)
            {
                analysis.Recommendations.Add("Ceiling height is low. Consider raising if possible.");
            }

            return analysis;
        }

        /// <summary>
        /// Analyzes adjacency requirements for a space.
        /// </summary>
        public AdjacencyAnalysis AnalyzeAdjacencies(string roomEntityId, AdjacencyRequirements requirements)
        {
            var analysis = new AdjacencyAnalysis();

            var room = _entities.FirstOrDefault(e => e.Id == roomEntityId);
            if (room == null) return analysis;

            // Check required adjacencies
            foreach (var required in requirements.MustBeAdjacent)
            {
                var targetRoom = _entities.FirstOrDefault(e =>
                    e.EntityType.Equals(required, StringComparison.OrdinalIgnoreCase) ||
                    e.Name?.Contains(required, StringComparison.OrdinalIgnoreCase) == true);

                if (targetRoom != null)
                {
                    var isAdjacent = AreAdjacent(room.Id, targetRoom.Id);
                    if (isAdjacent)
                    {
                        analysis.SatisfiedRequirements.Add(required);
                    }
                    else
                    {
                        analysis.UnmetRequirements.Add(required);
                    }
                }
                else
                {
                    analysis.MissingSpaces.Add(required);
                }
            }

            // Check prohibited adjacencies
            foreach (var prohibited in requirements.MustNotBeAdjacent)
            {
                var targetRoom = _entities.FirstOrDefault(e =>
                    e.EntityType.Equals(prohibited, StringComparison.OrdinalIgnoreCase) ||
                    e.Name?.Contains(prohibited, StringComparison.OrdinalIgnoreCase) == true);

                if (targetRoom != null && AreAdjacent(room.Id, targetRoom.Id))
                {
                    analysis.Violations.Add($"Should not be adjacent to {prohibited}");
                }
            }

            analysis.Score = requirements.MustBeAdjacent.Count > 0
                ? (double)analysis.SatisfiedRequirements.Count / requirements.MustBeAdjacent.Count
                : 1.0;

            return analysis;
        }

        /// <summary>
        /// Suggests optimal placement for a new room.
        /// </summary>
        public PlacementSuggestion SuggestPlacement(
            string roomType,
            double width,
            double length,
            AdjacencyRequirements requirements = null)
        {
            var suggestion = new PlacementSuggestion();
            var bestScore = double.MinValue;
            var candidatePositions = GenerateCandidatePositions(width, length);

            foreach (var position in candidatePositions)
            {
                var score = EvaluatePlacement(position, width, length, roomType, requirements);

                if (score > bestScore)
                {
                    bestScore = score;
                    suggestion.Position = position;
                    suggestion.Score = score;
                }
            }

            suggestion.Rotation = DetermineOptimalRotation(suggestion.Position, width, length, requirements);

            if (bestScore > 0.7)
            {
                suggestion.Confidence = Confidence.High;
            }
            else if (bestScore > 0.4)
            {
                suggestion.Confidence = Confidence.Medium;
            }
            else
            {
                suggestion.Confidence = Confidence.Low;
            }

            return suggestion;
        }

        #endregion

        #region Spatial Optimization

        /// <summary>
        /// Optimizes layout for circulation efficiency.
        /// </summary>
        public LayoutOptimizationResult OptimizeLayout(LayoutOptimizationParams parameters)
        {
            var result = new LayoutOptimizationResult();

            // Calculate current metrics
            result.OriginalScore = CalculateLayoutScore(parameters);

            // Generate optimization suggestions
            var suggestions = new List<LayoutSuggestion>();

            // Check for circulation bottlenecks
            var bottlenecks = FindCirculationBottlenecks();
            foreach (var bottleneck in bottlenecks)
            {
                suggestions.Add(new LayoutSuggestion
                {
                    Type = SuggestionType.CirculationImprovement,
                    Description = $"Widen passage near {bottleneck.Entity.Name}",
                    Impact = 0.1,
                    Priority = Priority.Medium
                });
            }

            // Check for adjacency violations
            var violations = FindAdjacencyViolations(parameters.AdjacencyMatrix);
            foreach (var violation in violations)
            {
                suggestions.Add(new LayoutSuggestion
                {
                    Type = SuggestionType.AdjacencyImprovement,
                    Description = $"Move {violation.Entity1} closer to {violation.Entity2}",
                    Impact = 0.15,
                    Priority = Priority.High
                });
            }

            result.Suggestions = suggestions.OrderByDescending(s => s.Impact).ToList();

            // Estimate optimized score
            result.PotentialScore = result.OriginalScore +
                suggestions.Sum(s => s.Impact) / Math.Max(suggestions.Count, 1);

            return result;
        }

        #endregion

        #region Private Methods

        private void UpdateRelationships(SpatialEntity newEntity)
        {
            foreach (var existing in _entities.Where(e => e.Id != newEntity.Id))
            {
                var relationship = CalculateRelationship(newEntity, existing);
                var key = GetRelationshipKey(newEntity.Id, existing.Id);
                _relationships[key] = relationship;
            }
        }

        private SpatialRelationship CalculateRelationship(SpatialEntity entity1, SpatialEntity entity2)
        {
            var distance = CalculateDistance(entity1.BoundingBox, entity2.BoundingBox);
            var direction = CalculateDirection(entity1.BoundingBox.Center, entity2.BoundingBox.Center);

            var relationship = new SpatialRelationship
            {
                Entity1Id = entity1.Id,
                Entity2Id = entity2.Id,
                Distance = distance,
                Direction = direction
            };

            // Determine relationship type
            if (distance < CollisionTolerance)
            {
                relationship.Type = RelationshipType.Overlapping;
            }
            else if (distance <= AdjacencyThreshold)
            {
                relationship.Type = RelationshipType.Adjacent;
            }
            else if (distance <= ProximityNear)
            {
                relationship.Type = RelationshipType.Near;
            }
            else if (distance <= ProximityFar)
            {
                relationship.Type = RelationshipType.Nearby;
            }
            else
            {
                relationship.Type = RelationshipType.Distant;
            }

            return relationship;
        }

        private string GetRelationshipKey(string id1, string id2)
        {
            return string.Compare(id1, id2, StringComparison.Ordinal) < 0
                ? $"{id1}_{id2}"
                : $"{id2}_{id1}";
        }

        private bool BoundsIntersect(BoundingBox a, BoundingBox b, double tolerance)
        {
            return !(a.Max.X + tolerance < b.Min.X || a.Min.X - tolerance > b.Max.X ||
                     a.Max.Y + tolerance < b.Min.Y || a.Min.Y - tolerance > b.Max.Y ||
                     a.Max.Z + tolerance < b.Min.Z || a.Min.Z - tolerance > b.Max.Z);
        }

        private double CalculateDistance(BoundingBox a, BoundingBox b)
        {
            // Calculate minimum distance between bounding boxes
            var dx = Math.Max(0, Math.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X));
            var dy = Math.Max(0, Math.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y));
            var dz = Math.Max(0, Math.Max(a.Min.Z - b.Max.Z, b.Min.Z - a.Max.Z));

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private double CalculateDistance(Point3D a, Point3D b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private Direction3D CalculateDirection(Point3D from, Point3D to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var dz = to.Z - from.Z;
            var magnitude = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (magnitude < 0.001) return new Direction3D { X = 0, Y = 0, Z = 0 };

            return new Direction3D
            {
                X = dx / magnitude,
                Y = dy / magnitude,
                Z = dz / magnitude
            };
        }

        private bool HasObstructions(Point3D from, Point3D to)
        {
            // Simple ray-cast check for obstructions
            foreach (var entity in _entities)
            {
                if (RayIntersectsBox(from, to, entity.BoundingBox))
                {
                    return true;
                }
            }
            return false;
        }

        private bool RayIntersectsBox(Point3D from, Point3D to, BoundingBox box)
        {
            // Simplified AABB ray intersection
            var direction = new Direction3D
            {
                X = to.X - from.X,
                Y = to.Y - from.Y,
                Z = to.Z - from.Z
            };

            var tMin = 0.0;
            var tMax = 1.0;

            // Check X
            if (Math.Abs(direction.X) > 0.0001)
            {
                var t1 = (box.Min.X - from.X) / direction.X;
                var t2 = (box.Max.X - from.X) / direction.X;
                tMin = Math.Max(tMin, Math.Min(t1, t2));
                tMax = Math.Min(tMax, Math.Max(t1, t2));
            }

            // Check Y
            if (Math.Abs(direction.Y) > 0.0001)
            {
                var t1 = (box.Min.Y - from.Y) / direction.Y;
                var t2 = (box.Max.Y - from.Y) / direction.Y;
                tMin = Math.Max(tMin, Math.Min(t1, t2));
                tMax = Math.Min(tMax, Math.Max(t1, t2));
            }

            return tMin <= tMax;
        }

        private List<SpatialEntity> FindPath(SpatialEntity from, SpatialEntity to)
        {
            // Simple BFS pathfinding through adjacent spaces
            var visited = new HashSet<string>();
            var queue = new Queue<List<SpatialEntity>>();
            queue.Enqueue(new List<SpatialEntity> { from });

            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                var current = path.Last();

                if (current.Id == to.Id)
                {
                    return path;
                }

                if (visited.Contains(current.Id)) continue;
                visited.Add(current.Id);

                foreach (var adjacent in FindAdjacent(current.Id))
                {
                    if (!visited.Contains(adjacent.Id))
                    {
                        var newPath = new List<SpatialEntity>(path) { adjacent };
                        queue.Enqueue(newPath);
                    }
                }
            }

            return null;
        }

        private double CalculatePathLength(List<SpatialEntity> path)
        {
            if (path == null || path.Count < 2) return 0;

            var length = 0.0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                length += CalculateDistance(path[i].BoundingBox.Center, path[i + 1].BoundingBox.Center);
            }

            return length;
        }

        private IEnumerable<Point3D> GenerateCandidatePositions(double width, double length)
        {
            // Generate grid of candidate positions based on existing geometry
            var positions = new List<Point3D>();

            // Add positions adjacent to existing entities
            foreach (var entity in _entities)
            {
                var bbox = entity.BoundingBox;

                // North, South, East, West of existing entities
                positions.Add(new Point3D { X = bbox.Center.X, Y = bbox.Max.Y + length / 2, Z = bbox.Center.Z });
                positions.Add(new Point3D { X = bbox.Center.X, Y = bbox.Min.Y - length / 2, Z = bbox.Center.Z });
                positions.Add(new Point3D { X = bbox.Max.X + width / 2, Y = bbox.Center.Y, Z = bbox.Center.Z });
                positions.Add(new Point3D { X = bbox.Min.X - width / 2, Y = bbox.Center.Y, Z = bbox.Center.Z });
            }

            // Add grid positions if no entities exist
            if (_entities.Count == 0)
            {
                for (double x = 0; x < 20; x += 4)
                {
                    for (double y = 0; y < 20; y += 4)
                    {
                        positions.Add(new Point3D { X = x, Y = y, Z = 0 });
                    }
                }
            }

            return positions;
        }

        private double EvaluatePlacement(Point3D position, double width, double length,
            string roomType, AdjacencyRequirements requirements)
        {
            var score = 1.0;
            var proposedBounds = new BoundingBox
            {
                Min = new Point3D { X = position.X - width / 2, Y = position.Y - length / 2, Z = 0 },
                Max = new Point3D { X = position.X + width / 2, Y = position.Y + length / 2, Z = 2.7 }
            };

            // Penalize collisions
            var collision = CheckCollisions(proposedBounds);
            if (collision.HasCollision)
            {
                score -= 0.5 * collision.CollidingEntities.Count;
            }

            // Reward adjacency to required rooms
            if (requirements != null)
            {
                foreach (var required in requirements.MustBeAdjacent)
                {
                    var target = _entities.FirstOrDefault(e =>
                        e.EntityType.Equals(required, StringComparison.OrdinalIgnoreCase));

                    if (target != null)
                    {
                        var distance = CalculateDistance(proposedBounds, target.BoundingBox);
                        if (distance < AdjacencyThreshold)
                        {
                            score += 0.2;
                        }
                        else
                        {
                            score -= 0.1 * Math.Min(distance / 10, 1);
                        }
                    }
                }

                // Penalize adjacency to prohibited rooms
                foreach (var prohibited in requirements.MustNotBeAdjacent)
                {
                    var target = _entities.FirstOrDefault(e =>
                        e.EntityType.Equals(prohibited, StringComparison.OrdinalIgnoreCase));

                    if (target != null)
                    {
                        var distance = CalculateDistance(proposedBounds, target.BoundingBox);
                        if (distance < AdjacencyThreshold)
                        {
                            score -= 0.3;
                        }
                    }
                }
            }

            return Math.Max(0, Math.Min(1, score));
        }

        private double DetermineOptimalRotation(Point3D position, double width, double length,
            AdjacencyRequirements requirements)
        {
            // Test 0° and 90° rotations
            var score0 = EvaluatePlacement(position, width, length, null, requirements);
            var score90 = EvaluatePlacement(position, length, width, null, requirements);

            return score90 > score0 ? 90.0 : 0.0;
        }

        private double CalculateLayoutScore(LayoutOptimizationParams parameters)
        {
            var score = 1.0;

            // Evaluate circulation efficiency
            foreach (var entity in _entities)
            {
                foreach (var other in _entities.Where(e => e.Id != entity.Id))
                {
                    var circulation = AnalyzeCirculation(entity.Id, other.Id);
                    if (circulation.Efficiency < 0.7)
                    {
                        score -= 0.05;
                    }
                }
            }

            return Math.Max(0, score);
        }

        private IEnumerable<CirculationBottleneck> FindCirculationBottlenecks()
        {
            var bottlenecks = new List<CirculationBottleneck>();

            foreach (var entity in _entities.Where(e =>
                e.EntityType.Equals("corridor", StringComparison.OrdinalIgnoreCase)))
            {
                var width = Math.Min(
                    entity.BoundingBox.Max.X - entity.BoundingBox.Min.X,
                    entity.BoundingBox.Max.Y - entity.BoundingBox.Min.Y);

                if (width < 1.5) // Less than 1.5m
                {
                    bottlenecks.Add(new CirculationBottleneck
                    {
                        Entity = entity,
                        Width = width,
                        Severity = width < 1.0 ? Severity.High : Severity.Medium
                    });
                }
            }

            return bottlenecks;
        }

        private IEnumerable<AdjacencyViolation> FindAdjacencyViolations(Dictionary<string, List<string>> adjacencyMatrix)
        {
            var violations = new List<AdjacencyViolation>();

            if (adjacencyMatrix == null) return violations;

            foreach (var kvp in adjacencyMatrix)
            {
                var entity = _entities.FirstOrDefault(e =>
                    e.EntityType.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));

                if (entity == null) continue;

                foreach (var required in kvp.Value)
                {
                    var target = _entities.FirstOrDefault(e =>
                        e.EntityType.Equals(required, StringComparison.OrdinalIgnoreCase));

                    if (target != null && !AreAdjacent(entity.Id, target.Id))
                    {
                        violations.Add(new AdjacencyViolation
                        {
                            Entity1 = entity.EntityType,
                            Entity2 = target.EntityType
                        });
                    }
                }
            }

            return violations;
        }

        #endregion
    }

    #region Supporting Types

    public class SpatialEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string EntityType { get; set; }
        public BoundingBox BoundingBox { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    public class BoundingBox
    {
        public Point3D Min { get; set; }
        public Point3D Max { get; set; }
        public Point3D Center => new Point3D
        {
            X = (Min.X + Max.X) / 2,
            Y = (Min.Y + Max.Y) / 2,
            Z = (Min.Z + Max.Z) / 2
        };
    }

    public class Direction3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class SpatialRelationship
    {
        public string Entity1Id { get; set; }
        public string Entity2Id { get; set; }
        public RelationshipType Type { get; set; }
        public double Distance { get; set; }
        public Direction3D Direction { get; set; }
    }

    public enum RelationshipType
    {
        Overlapping,
        Adjacent,
        Near,
        Nearby,
        Distant
    }

    public class CollisionResult
    {
        public bool HasCollision { get; set; }
        public List<SpatialEntity> CollidingEntities { get; set; } = new();
    }

    public class CirculationAnalysis
    {
        public bool IsReachable { get; set; }
        public bool HasDirectPath { get; set; }
        public double DirectDistance { get; set; }
        public double PathLength { get; set; }
        public double Efficiency { get; set; }
        public List<SpatialEntity> Path { get; set; }
        public string Reason { get; set; }
    }

    public class SpatialQualityAnalysis
    {
        public double Area { get; set; }
        public double Volume { get; set; }
        public double AspectRatio { get; set; }
        public double CeilingHeight { get; set; }
        public double ProportionScore { get; set; }
        public double HeightScore { get; set; }
        public double OverallScore { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    public class AdjacencyRequirements
    {
        public List<string> MustBeAdjacent { get; set; } = new();
        public List<string> MustNotBeAdjacent { get; set; } = new();
        public List<string> PreferAdjacent { get; set; } = new();
    }

    public class AdjacencyAnalysis
    {
        public double Score { get; set; }
        public List<string> SatisfiedRequirements { get; set; } = new();
        public List<string> UnmetRequirements { get; set; } = new();
        public List<string> MissingSpaces { get; set; } = new();
        public List<string> Violations { get; set; } = new();
    }

    public class PlacementSuggestion
    {
        public Point3D Position { get; set; }
        public double Rotation { get; set; }
        public double Score { get; set; }
        public Confidence Confidence { get; set; }
    }

    public enum Confidence
    {
        Low,
        Medium,
        High
    }

    public class LayoutOptimizationParams
    {
        public Dictionary<string, List<string>> AdjacencyMatrix { get; set; }
        public double TargetEfficiency { get; set; } = 0.85;
    }

    public class LayoutOptimizationResult
    {
        public double OriginalScore { get; set; }
        public double PotentialScore { get; set; }
        public List<LayoutSuggestion> Suggestions { get; set; } = new();
    }

    public class LayoutSuggestion
    {
        public SuggestionType Type { get; set; }
        public string Description { get; set; }
        public double Impact { get; set; }
        public Priority Priority { get; set; }
    }

    public enum SuggestionType
    {
        CirculationImprovement,
        AdjacencyImprovement,
        SpaceOptimization
    }

    public enum Priority
    {
        Low,
        Medium,
        High
    }

    public class CirculationBottleneck
    {
        public SpatialEntity Entity { get; set; }
        public double Width { get; set; }
        public Severity Severity { get; set; }
    }

    public enum Severity
    {
        Low,
        Medium,
        High
    }

    public class AdjacencyViolation
    {
        public string Entity1 { get; set; }
        public string Entity2 { get; set; }
    }

    /// <summary>
    /// Octree-based spatial index for O(log n) spatial queries (T3-12).
    /// Recursively subdivides 3D space into 8 octants.
    /// Falls back to linear scan for entities spanning the root bounds.
    /// </summary>
    internal class SpatialIndex
    {
        private readonly Dictionary<string, SpatialEntity> _entities = new();
        private OctreeNode _root;
        private const int MaxEntitiesPerNode = 8;
        private const int MaxDepth = 10;

        public SpatialIndex()
        {
            // Initialize with a large default world bounds (1km cube centered at origin)
            _root = new OctreeNode(
                new Point3D(-500, -500, -500),
                new Point3D(500, 500, 500),
                0);
        }

        public void Insert(SpatialEntity entity)
        {
            _entities[entity.Id] = entity;

            // Expand root if entity is outside current bounds
            while (!ContainsBounds(_root.Min, _root.Max, entity.BoundingBox))
            {
                ExpandRoot(entity.BoundingBox);
            }

            InsertIntoNode(_root, entity);
        }

        public void Remove(SpatialEntity entity)
        {
            _entities.Remove(entity.Id);
            RemoveFromNode(_root, entity);
        }

        /// <summary>
        /// Finds all entities within radius of center point. O(log n) average case.
        /// </summary>
        public IEnumerable<SpatialEntity> QueryRadius(Point3D center, double radius)
        {
            var results = new List<SpatialEntity>();
            var queryBounds = new BoundingBox
            {
                Min = new Point3D(center.X - radius, center.Y - radius, center.Z - radius),
                Max = new Point3D(center.X + radius, center.Y + radius, center.Z + radius)
            };

            QueryNodeBounds(_root, queryBounds, results);

            // Post-filter by exact radius (bounding box query is approximate)
            return results.Where(e =>
            {
                var dx = e.BoundingBox.Center.X - center.X;
                var dy = e.BoundingBox.Center.Y - center.Y;
                var dz = e.BoundingBox.Center.Z - center.Z;
                return Math.Sqrt(dx * dx + dy * dy + dz * dz) <= radius;
            });
        }

        /// <summary>
        /// Finds all entities intersecting the query bounds. O(log n) average case.
        /// </summary>
        public IEnumerable<SpatialEntity> QueryBounds(BoundingBox bounds)
        {
            var results = new List<SpatialEntity>();
            QueryNodeBounds(_root, bounds, results);
            return results;
        }

        private void InsertIntoNode(OctreeNode node, SpatialEntity entity)
        {
            // If this is a leaf with capacity, add here
            if (node.Children == null && node.Entities.Count < MaxEntitiesPerNode)
            {
                node.Entities.Add(entity);
                return;
            }

            // Subdivide if needed and not at max depth
            if (node.Children == null && node.Depth < MaxDepth)
            {
                Subdivide(node);

                // Re-insert existing entities into children
                var existing = new List<SpatialEntity>(node.Entities);
                node.Entities.Clear();
                foreach (var e in existing)
                {
                    InsertEntityIntoChildren(node, e);
                }
            }

            // Insert into appropriate child(ren)
            if (node.Children != null)
            {
                InsertEntityIntoChildren(node, entity);
            }
            else
            {
                // Max depth reached, store here
                node.Entities.Add(entity);
            }
        }

        private void InsertEntityIntoChildren(OctreeNode node, SpatialEntity entity)
        {
            bool inserted = false;
            foreach (var child in node.Children)
            {
                if (IntersectsBounds(child.Min, child.Max, entity.BoundingBox))
                {
                    InsertIntoNode(child, entity);
                    inserted = true;
                }
            }

            // If entity doesn't fit any child (shouldn't happen), store in parent
            if (!inserted)
            {
                node.Entities.Add(entity);
            }
        }

        private void RemoveFromNode(OctreeNode node, SpatialEntity entity)
        {
            node.Entities.RemoveAll(e => e.Id == entity.Id);

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    RemoveFromNode(child, entity);
                }
            }
        }

        private void QueryNodeBounds(OctreeNode node, BoundingBox queryBounds, List<SpatialEntity> results)
        {
            // Check if this node intersects the query
            if (!IntersectsBounds(node.Min, node.Max, queryBounds))
                return;

            // Add entities stored at this node
            foreach (var entity in node.Entities)
            {
                var bb = entity.BoundingBox;
                if (bb.Max.X >= queryBounds.Min.X && bb.Min.X <= queryBounds.Max.X &&
                    bb.Max.Y >= queryBounds.Min.Y && bb.Min.Y <= queryBounds.Max.Y &&
                    bb.Max.Z >= queryBounds.Min.Z && bb.Min.Z <= queryBounds.Max.Z)
                {
                    results.Add(entity);
                }
            }

            // Recurse into children
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    QueryNodeBounds(child, queryBounds, results);
                }
            }
        }

        private void Subdivide(OctreeNode node)
        {
            var mid = new Point3D(
                (node.Min.X + node.Max.X) / 2,
                (node.Min.Y + node.Max.Y) / 2,
                (node.Min.Z + node.Max.Z) / 2);

            node.Children = new OctreeNode[8];
            int childDepth = node.Depth + 1;

            // 8 octants: all combinations of (min/max X, min/max Y, min/max Z)
            node.Children[0] = new OctreeNode(new Point3D(node.Min.X, node.Min.Y, node.Min.Z), mid, childDepth);
            node.Children[1] = new OctreeNode(new Point3D(mid.X, node.Min.Y, node.Min.Z), new Point3D(node.Max.X, mid.Y, mid.Z), childDepth);
            node.Children[2] = new OctreeNode(new Point3D(node.Min.X, mid.Y, node.Min.Z), new Point3D(mid.X, node.Max.Y, mid.Z), childDepth);
            node.Children[3] = new OctreeNode(new Point3D(mid.X, mid.Y, node.Min.Z), new Point3D(node.Max.X, node.Max.Y, mid.Z), childDepth);
            node.Children[4] = new OctreeNode(new Point3D(node.Min.X, node.Min.Y, mid.Z), new Point3D(mid.X, mid.Y, node.Max.Z), childDepth);
            node.Children[5] = new OctreeNode(new Point3D(mid.X, node.Min.Y, mid.Z), new Point3D(node.Max.X, mid.Y, node.Max.Z), childDepth);
            node.Children[6] = new OctreeNode(new Point3D(node.Min.X, mid.Y, mid.Z), new Point3D(mid.X, node.Max.Y, node.Max.Z), childDepth);
            node.Children[7] = new OctreeNode(mid, node.Max, childDepth);
        }

        private void ExpandRoot(BoundingBox entityBounds)
        {
            var newMin = new Point3D(
                Math.Min(_root.Min.X, entityBounds.Min.X) - 100,
                Math.Min(_root.Min.Y, entityBounds.Min.Y) - 100,
                Math.Min(_root.Min.Z, entityBounds.Min.Z) - 100);
            var newMax = new Point3D(
                Math.Max(_root.Max.X, entityBounds.Max.X) + 100,
                Math.Max(_root.Max.Y, entityBounds.Max.Y) + 100,
                Math.Max(_root.Max.Z, entityBounds.Max.Z) + 100);

            var newRoot = new OctreeNode(newMin, newMax, 0);

            // Re-insert all existing entities
            foreach (var entity in _entities.Values)
            {
                InsertIntoNode(newRoot, entity);
            }

            _root = newRoot;
        }

        private static bool ContainsBounds(Point3D nodeMin, Point3D nodeMax, BoundingBox bb)
        {
            return bb.Min.X >= nodeMin.X && bb.Max.X <= nodeMax.X &&
                   bb.Min.Y >= nodeMin.Y && bb.Max.Y <= nodeMax.Y &&
                   bb.Min.Z >= nodeMin.Z && bb.Max.Z <= nodeMax.Z;
        }

        private static bool IntersectsBounds(Point3D nodeMin, Point3D nodeMax, BoundingBox bb)
        {
            return nodeMax.X >= bb.Min.X && nodeMin.X <= bb.Max.X &&
                   nodeMax.Y >= bb.Min.Y && nodeMin.Y <= bb.Max.Y &&
                   nodeMax.Z >= bb.Min.Z && nodeMin.Z <= bb.Max.Z;
        }

        private class OctreeNode
        {
            public Point3D Min { get; }
            public Point3D Max { get; }
            public int Depth { get; }
            public List<SpatialEntity> Entities { get; } = new List<SpatialEntity>();
            public OctreeNode[] Children { get; set; }

            public OctreeNode(Point3D min, Point3D max, int depth)
            {
                Min = min;
                Max = max;
                Depth = depth;
            }
        }
    }

    #endregion
}
