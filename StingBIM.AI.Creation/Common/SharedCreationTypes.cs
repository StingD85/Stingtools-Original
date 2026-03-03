// ============================================================================
// SharedCreationTypes.cs - Shared type definitions for StingBIM.AI.Creation
// Consolidates common types to avoid duplicate definitions across files
// ============================================================================

using System;
using System.Collections.Generic;

namespace StingBIM.AI.Creation.Common
{
    #region Geometry Types

    /// <summary>
    /// 3D point representation
    /// </summary>
    public class Point3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3D() { }
        public Point3D(double x, double y, double z) { X = x; Y = y; Z = z; }

        public double DistanceTo(Point3D other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            var dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static Point3D operator +(Point3D a, Point3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Point3D operator -(Point3D a, Point3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Point3D operator +(Point3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Point3D operator *(Point3D a, double scalar) => new(a.X * scalar, a.Y * scalar, a.Z * scalar);
        public Vector3D ToVector() => new(X, Y, Z);
        public Vector3D Normalize()
        {
            var length = Math.Sqrt(X * X + Y * Y + Z * Z);
            return length > 0 ? new Vector3D(X / length, Y / length, Z / length) : new Vector3D(0, 0, 0);
        }
        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }

    /// <summary>
    /// 3D vector representation
    /// </summary>
    public class Vector3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Vector3D() { }
        public Vector3D(double x, double y, double z) { X = x; Y = y; Z = z; }

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public Vector3D Normalize()
        {
            var len = Length;
            return len > 0 ? new Vector3D(X / len, Y / len, Z / len) : new Vector3D(0, 0, 0);
        }

        public static double Dot(Vector3D a, Vector3D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Vector3D operator *(Vector3D a, double scalar) => new(a.X * scalar, a.Y * scalar, a.Z * scalar);
        public static Vector3D operator +(Vector3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public override string ToString() => $"<{X:F2}, {Y:F2}, {Z:F2}>";
    }

    /// <summary>
    /// 2D point representation
    /// </summary>
    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point2D() { }
        public Point2D(double x, double y) { X = x; Y = y; }

        public double DistanceTo(Point2D other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString() => $"({X:F2}, {Y:F2})";
    }

    /// <summary>
    /// 3D bounding box
    /// </summary>
    public class BoundingBox3D
    {
        public Point3D Min { get; set; } = new();
        public Point3D Max { get; set; } = new();

        public double Width => Max.X - Min.X;
        public double Height => Max.Y - Min.Y;
        public double Depth => Max.Z - Min.Z;
        public double Volume => Width * Height * Depth;

        public Point3D Center => new(
            (Min.X + Max.X) / 2,
            (Min.Y + Max.Y) / 2,
            (Min.Z + Max.Z) / 2);

        public bool Intersects(BoundingBox3D other)
        {
            return Min.X <= other.Max.X && Max.X >= other.Min.X &&
                   Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
                   Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
        }
    }

    #endregion

    #region Import/Recognition Types

    /// <summary>
    /// Recognized element types from CAD/PDF imports
    /// </summary>
    public enum RecognizedElementType
    {
        Unknown,
        Wall,
        Door,
        Window,
        Column,
        Beam,
        Slab,
        Stair,
        Room,
        Furniture,
        Fixture,
        Equipment,
        Annotation,
        Dimension,
        Grid,
        Level,
        Section,
        Elevation,
        Detail,
        Symbol,
        Text,
        Hatch,
        Line,
        Arc,
        Circle,
        Polyline,
        Spline
    }

    /// <summary>
    /// Text types found in drawings
    /// </summary>
    public enum TextType
    {
        Unknown,
        RoomName,
        RoomNumber,
        DoorTag,
        WindowTag,
        Dimension,
        Annotation,
        Title,
        Label,
        Note,
        Legend,
        Schedule,
        Specification
    }

    /// <summary>
    /// Drawing view types
    /// </summary>
    public enum DrawingViewType
    {
        Unknown,
        FloorPlan,
        CeilingPlan,
        Elevation,
        Section,
        Detail,
        ThreeD,
        Schedule,
        Legend,
        TitleBlock,
        SitePlan,
        AreaPlan,
        RoofPlan,
        ReflectedCeilingPlan
    }

    /// <summary>
    /// Measurement units
    /// </summary>
    public enum MeasurementUnit
    {
        Unknown,
        Millimeters,
        Centimeters,
        Meters,
        Inches,
        Feet,
        FeetAndInches
    }

    /// <summary>
    /// Import progress tracking
    /// </summary>
    public class ImportProgress
    {
        public int TotalItems { get; set; }
        public int ProcessedItems { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public string CurrentOperation { get; set; } = string.Empty;
        public double ProgressPercent => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;
    }

    #endregion

    #region Detection Types

    /// <summary>
    /// Detected line from image/CAD processing
    /// </summary>
    public class DetectedLine
    {
        public Point2D Start { get; set; } = new();
        public Point2D End { get; set; } = new();
        public double Thickness { get; set; }
        public string LayerName { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public double Length => Start.DistanceTo(End);
    }

    /// <summary>
    /// Detected room from image/CAD processing
    /// </summary>
    public class DetectedRoom
    {
        public string RoomId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public List<Point2D> BoundaryPoints { get; set; } = new();
        public double Area { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Scale information for drawings
    /// </summary>
    public class ScaleInfo
    {
        public double Scale { get; set; } = 1.0;
        public MeasurementUnit Unit { get; set; } = MeasurementUnit.Millimeters;
        public double PixelsPerUnit { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Dimension annotation from drawings
    /// </summary>
    public class DimensionAnnotation
    {
        public Point2D Start { get; set; } = new();
        public Point2D End { get; set; } = new();
        public double Value { get; set; }
        public string Text { get; set; } = string.Empty;
        public MeasurementUnit Unit { get; set; }
        public double Confidence { get; set; }
    }

    #endregion

    #region CAD Import Types

    /// <summary>
    /// CAD layer information
    /// </summary>
    public class CADLayer
    {
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public bool IsFrozen { get; set; }
        public bool IsLocked { get; set; }
        public int EntityCount { get; set; }
        public RecognizedElementType? MappedType { get; set; }
    }

    /// <summary>
    /// CAD entity base class
    /// </summary>
    public class CADEntity
    {
        public string EntityId { get; set; } = string.Empty;
        public string LayerName { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public BoundingBox3D Bounds { get; set; } = new();
        public Dictionary<string, string> Properties { get; set; } = new();
    }

    /// <summary>
    /// CAD import result
    /// </summary>
    public class CADImportResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public List<CADLayer> Layers { get; set; } = new();
        public List<CADEntity> Entities { get; set; } = new();
        public int TotalEntities { get; set; }
        public int ImportedEntities { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public TimeSpan ProcessingTime { get; set; }
    }

    #endregion

    #region Clash Detection Types

    /// <summary>
    /// Detected clash between elements
    /// </summary>
    public class DetectedClash
    {
        public string ClashId { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
        public string ElementA_Id { get; set; } = string.Empty;
        public string ElementA_Name { get; set; } = string.Empty;
        public string ElementA_Category { get; set; } = string.Empty;
        public string ElementB_Id { get; set; } = string.Empty;
        public string ElementB_Name { get; set; } = string.Empty;
        public string ElementB_Category { get; set; } = string.Empty;
        public Point3D ClashPoint { get; set; } = new();
        public double ClashDistance { get; set; }
        public ClashSeverity Severity { get; set; }
        public ClashStatus Status { get; set; } = ClashStatus.New;
        public string Resolution { get; set; } = string.Empty;
    }

    /// <summary>
    /// Clash severity levels
    /// </summary>
    public enum ClashSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Clash status
    /// </summary>
    public enum ClashStatus
    {
        New,
        Active,
        Reviewed,
        Resolved,
        Ignored
    }

    /// <summary>
    /// Clash detection statistics
    /// </summary>
    public class ClashStatistics
    {
        public int TotalClashes { get; set; }
        public int CriticalCount { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
        public int ResolvedCount { get; set; }
        public Dictionary<string, int> ByCategory { get; set; } = new();
    }

    #endregion

    #region Element Creation Types

    /// <summary>
    /// Wall creation options
    /// </summary>
    public class WallCreationOptions
    {
        public string WallTypeName { get; set; } = string.Empty;
        public double Height { get; set; }
        public double BaseOffset { get; set; }
        public double TopOffset { get; set; }
        public bool IsStructural { get; set; }
        public string LevelId { get; set; } = string.Empty;
        public bool FlipDirection { get; set; }
    }

    /// <summary>
    /// Family definition for loading
    /// </summary>
    public class FamilyDefinition
    {
        public string FamilyName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string FamilyPath { get; set; } = string.Empty;
        public List<string> TypeNames { get; set; } = new();
        public Dictionary<string, object> DefaultParameters { get; set; } = new();
    }

    /// <summary>
    /// Text extraction result
    /// </summary>
    public class TextExtractor
    {
        public string Text { get; set; } = string.Empty;
        public Point2D Location { get; set; } = new();
        public double Height { get; set; }
        public double Rotation { get; set; }
        public TextType Type { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Element converter for CAD to BIM
    /// </summary>
    public class ElementConverter
    {
        public string SourceType { get; set; } = string.Empty;
        public string TargetCategory { get; set; } = string.Empty;
        public string TargetFamily { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public Dictionary<string, string> ParameterMappings { get; set; } = new();
    }

    #endregion
}
