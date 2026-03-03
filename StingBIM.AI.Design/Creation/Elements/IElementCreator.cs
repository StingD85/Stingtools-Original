// StingBIM.AI.Creation.Elements.IElementCreator
// Common interfaces and base classes for element creation
// Master Proposal Reference: Part 4.2 Phase 1 Month 2 - Element Creation Layer

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Elements
{
    /// <summary>
    /// Interface for all element creators.
    /// </summary>
    public interface IElementCreator
    {
        /// <summary>
        /// Creates an element from parameters.
        /// </summary>
        Task<CreationResult> CreateAsync(
            ElementCreationParams parameters,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Modifies an existing element. Default implementation throws NotImplementedException.
        /// </summary>
        Task<ModificationResult> ModifyAsync(
            int elementId,
            Dictionary<string, object> modifications,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ModificationResult
            {
                Success = false,
                Message = "ModifyAsync not implemented for this creator"
            });
        }
    }

    /// <summary>
    /// Parameters for creating an element.
    /// </summary>
    public class ElementCreationParams
    {
        public string ElementType { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public string LevelName { get; set; }
        public Point3D Location { get; set; }
        public object AdditionalData { get; set; }

        public override string ToString()
        {
            var paramList = string.Join(", ", Parameters.Select(p => $"{p.Key}={p.Value}"));
            return $"{ElementType}: {paramList}";
        }
    }

    /// <summary>
    /// Result of an element creation operation.
    /// </summary>
    public class CreationResult
    {
        public bool Success { get; set; }
        public int CreatedElementId { get; set; }
        public string UniqueId { get; set; }
        public string ElementType { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public object Parameters { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public TimeSpan Duration => EndTime - StartTime;
    }

    /// <summary>
    /// Result of a batch creation operation.
    /// </summary>
    public class BatchCreationResult
    {
        public List<CreationResult> Results { get; set; } = new List<CreationResult>();
        public int TotalCreated { get; set; }
        public int TotalFailed { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public TimeSpan Duration => EndTime - StartTime;
        public bool AllSucceeded => TotalFailed == 0;
    }

    /// <summary>
    /// Result of an element modification operation.
    /// </summary>
    public class ModificationResult
    {
        public bool Success { get; set; }
        public int ElementId { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public Dictionary<string, object> ModifiedProperties { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> PreviousValues { get; set; } = new Dictionary<string, object>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public TimeSpan Duration => EndTime - StartTime;
    }

    /// <summary>
    /// Validation result.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Error { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();

        public static ValidationResult Valid() => new ValidationResult { IsValid = true };
        public static ValidationResult Invalid(string error) => new ValidationResult { IsValid = false, Error = error };
    }

    // Point3D moved to StingBIM.AI.Creation.Common.SharedCreationTypes

    /// <summary>
    /// Bounding box representation.
    /// </summary>
    public class BoundingBox
    {
        public Point3D Min { get; set; }
        public Point3D Max { get; set; }

        public double Width => Max.X - Min.X;
        public double Depth => Max.Y - Min.Y;
        public double Height => Max.Z - Min.Z;

        public Point3D Center => new Point3D(
            (Min.X + Max.X) / 2,
            (Min.Y + Max.Y) / 2,
            (Min.Z + Max.Z) / 2);

        public bool Contains(Point3D point)
        {
            return point.X >= Min.X && point.X <= Max.X &&
                   point.Y >= Min.Y && point.Y <= Max.Y &&
                   point.Z >= Min.Z && point.Z <= Max.Z;
        }

        public bool Intersects(BoundingBox other)
        {
            return Min.X <= other.Max.X && Max.X >= other.Min.X &&
                   Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
                   Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
        }
    }

    /// <summary>
    /// Element geometry representation.
    /// </summary>
    public class ElementGeometry
    {
        public double Width { get; set; }
        public double Length { get; set; }
        public double Height { get; set; }
        public double Area => Width * Length;
        public double Volume => Width * Length * Height;
        public Point3D Origin { get; set; }
        public double Rotation { get; set; } // degrees
        public BoundingBox BoundingBox { get; set; }
    }
}
