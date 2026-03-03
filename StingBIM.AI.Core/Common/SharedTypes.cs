// StingBIM.AI.Core.Common.SharedTypes
// Consolidated common types used across the StingBIM.AI solution
// This file provides canonical definitions to avoid duplicate type errors

using System;
using System.Collections.Generic;

namespace StingBIM.AI.Core.Common
{
    /// <summary>
    /// 3D point representation used throughout the solution.
    /// </summary>
    public class Point3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3D() { }

        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double DistanceTo(Point3D other)
        {
            if (other == null) return double.MaxValue;
            var dx = other.X - X;
            var dy = other.Y - Y;
            var dz = other.Z - Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public Point3D Add(Point3D other)
        {
            if (other == null) return new Point3D(X, Y, Z);
            return new Point3D(X + other.X, Y + other.Y, Z + other.Z);
        }

        public Point3D Subtract(Point3D other)
        {
            if (other == null) return new Point3D(X, Y, Z);
            return new Point3D(X - other.X, Y - other.Y, Z - other.Z);
        }

        public Point3D Scale(double factor) => new Point3D(X * factor, Y * factor, Z * factor);

        public Point3D Normalize()
        {
            var len = Math.Sqrt(X * X + Y * Y + Z * Z);
            return len > 0 ? new Point3D(X / len, Y / len, Z / len) : new Point3D();
        }

        public Vector3D ToVector() => new Vector3D(X, Y, Z);

        public static Point3D operator +(Point3D a, Point3D b) => a?.Add(b) ?? b;
        public static Point3D operator -(Point3D a, Point3D b) => a?.Subtract(b);
        public static Point3D operator *(Point3D a, double scalar) => a?.Scale(scalar);
        public static Point3D operator *(double scalar, Point3D a) => a?.Scale(scalar);

        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";

        public override bool Equals(object obj)
        {
            if (obj is Point3D other)
            {
                const double tolerance = 1e-10;
                return Math.Abs(X - other.X) < tolerance &&
                       Math.Abs(Y - other.Y) < tolerance &&
                       Math.Abs(Z - other.Z) < tolerance;
            }
            return false;
        }

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    }

    /// <summary>
    /// 2D point representation.
    /// </summary>
    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point2D() { }

        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double DistanceTo(Point2D other)
        {
            if (other == null) return double.MaxValue;
            var dx = other.X - X;
            var dy = other.Y - Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public Point3D ToPoint3D(double z = 0) => new Point3D(X, Y, z);

        public override string ToString() => $"({X:F2}, {Y:F2})";
    }

    /// <summary>
    /// 3D vector representation.
    /// </summary>
    public class Vector3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Vector3D() { }

        public Vector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public Vector3D Normalize()
        {
            var len = Length;
            if (len < 1e-10) return new Vector3D(0, 0, 0);
            return new Vector3D(X / len, Y / len, Z / len);
        }

        public double Dot(Vector3D other)
        {
            if (other == null) return 0;
            return X * other.X + Y * other.Y + Z * other.Z;
        }

        public Vector3D Cross(Vector3D other)
        {
            if (other == null) return new Vector3D(0, 0, 0);
            return new Vector3D(
                Y * other.Z - Z * other.Y,
                Z * other.X - X * other.Z,
                X * other.Y - Y * other.X);
        }

        public static Vector3D operator +(Vector3D a, Vector3D b)
            => new Vector3D((a?.X ?? 0) + (b?.X ?? 0), (a?.Y ?? 0) + (b?.Y ?? 0), (a?.Z ?? 0) + (b?.Z ?? 0));

        public static Vector3D operator -(Vector3D a, Vector3D b)
            => new Vector3D((a?.X ?? 0) - (b?.X ?? 0), (a?.Y ?? 0) - (b?.Y ?? 0), (a?.Z ?? 0) - (b?.Z ?? 0));

        public static Vector3D operator *(Vector3D v, double scalar)
            => new Vector3D((v?.X ?? 0) * scalar, (v?.Y ?? 0) * scalar, (v?.Z ?? 0) * scalar);

        public static Vector3D operator *(double scalar, Vector3D v)
            => new Vector3D((v?.X ?? 0) * scalar, (v?.Y ?? 0) * scalar, (v?.Z ?? 0) * scalar);

        public override string ToString() => $"<{X:F2}, {Y:F2}, {Z:F2}>";
    }

    /// <summary>
    /// 3D bounding box representation.
    /// </summary>
    public class BoundingBox3D
    {
        public Point3D Min { get; set; }
        public Point3D Max { get; set; }

        public BoundingBox3D()
        {
            Min = new Point3D();
            Max = new Point3D();
        }

        public BoundingBox3D(Point3D min, Point3D max)
        {
            Min = min ?? new Point3D();
            Max = max ?? new Point3D();
        }

        public double Width => Max.X - Min.X;
        public double Depth => Max.Y - Min.Y;
        public double Height => Max.Z - Min.Z;
        public double Volume => Width * Depth * Height;

        public Point3D Center => new Point3D(
            (Min.X + Max.X) / 2,
            (Min.Y + Max.Y) / 2,
            (Min.Z + Max.Z) / 2);

        public bool Contains(Point3D point)
        {
            if (point == null) return false;
            return point.X >= Min.X && point.X <= Max.X &&
                   point.Y >= Min.Y && point.Y <= Max.Y &&
                   point.Z >= Min.Z && point.Z <= Max.Z;
        }

        public bool Intersects(BoundingBox3D other)
        {
            if (other == null) return false;
            return Min.X <= other.Max.X && Max.X >= other.Min.X &&
                   Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
                   Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
        }

        public BoundingBox3D Union(BoundingBox3D other)
        {
            if (other == null) return this;
            return new BoundingBox3D(
                new Point3D(
                    Math.Min(Min.X, other.Min.X),
                    Math.Min(Min.Y, other.Min.Y),
                    Math.Min(Min.Z, other.Min.Z)),
                new Point3D(
                    Math.Max(Max.X, other.Max.X),
                    Math.Max(Max.Y, other.Max.Y),
                    Math.Max(Max.Z, other.Max.Z)));
        }
    }

    /// <summary>
    /// 2D bounding box representation.
    /// </summary>
    public class BoundingBox2D
    {
        public Point2D Min { get; set; }
        public Point2D Max { get; set; }

        public BoundingBox2D()
        {
            Min = new Point2D();
            Max = new Point2D();
        }

        public BoundingBox2D(Point2D min, Point2D max)
        {
            Min = min ?? new Point2D();
            Max = max ?? new Point2D();
        }

        public double Width => Max.X - Min.X;
        public double Height => Max.Y - Min.Y;
        public double Area => Width * Height;

        public Point2D Center => new Point2D(
            (Min.X + Max.X) / 2,
            (Min.Y + Max.Y) / 2);

        public bool Contains(Point2D point)
        {
            if (point == null) return false;
            return point.X >= Min.X && point.X <= Max.X &&
                   point.Y >= Min.Y && point.Y <= Max.Y;
        }

        public bool Intersects(BoundingBox2D other)
        {
            if (other == null) return false;
            return Min.X <= other.Max.X && Max.X >= other.Min.X &&
                   Min.Y <= other.Max.Y && Max.Y >= other.Min.Y;
        }
    }

    /// <summary>
    /// Rectangle representation for 2D operations.
    /// </summary>
    public class Rectangle
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public Rectangle() { }

        public Rectangle(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double Area => Width * Height;
        public double CenterX => X + Width / 2;
        public double CenterY => Y + Height / 2;

        public bool Contains(double px, double py)
        {
            return px >= X && px <= X + Width &&
                   py >= Y && py <= Y + Height;
        }

        public bool Intersects(Rectangle other)
        {
            if (other == null) return false;
            return X < other.X + other.Width && X + Width > other.X &&
                   Y < other.Y + other.Height && Y + Height > other.Y;
        }
    }

    /// <summary>
    /// Line segment in 3D space.
    /// </summary>
    public class Line3D
    {
        public Point3D Start { get; set; }
        public Point3D End { get; set; }

        public Line3D()
        {
            Start = new Point3D();
            End = new Point3D();
        }

        public Line3D(Point3D start, Point3D end)
        {
            Start = start ?? new Point3D();
            End = end ?? new Point3D();
        }

        public double Length => Start.DistanceTo(End);

        public Vector3D Direction
        {
            get
            {
                var dx = End.X - Start.X;
                var dy = End.Y - Start.Y;
                var dz = End.Z - Start.Z;
                var len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (len < 1e-10) return new Vector3D(0, 0, 0);
                return new Vector3D(dx / len, dy / len, dz / len);
            }
        }

        public Point3D Midpoint => new Point3D(
            (Start.X + End.X) / 2,
            (Start.Y + End.Y) / 2,
            (Start.Z + End.Z) / 2);
    }

    /// <summary>
    /// Common priority levels used throughout the solution.
    /// </summary>
    public enum Priority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Common risk levels used throughout the solution.
    /// </summary>
    public enum RiskLevel
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>
    /// Common status for operations.
    /// </summary>
    public enum OperationStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Common building element types.
    /// </summary>
    public enum ElementCategory
    {
        Wall,
        Door,
        Window,
        Floor,
        Ceiling,
        Roof,
        Column,
        Beam,
        Stair,
        Ramp,
        Room,
        Space,
        Duct,
        Pipe,
        CableTray,
        Equipment,
        Furniture,
        Generic
    }

    /// <summary>
    /// Base result class for operations.
    /// </summary>
    public class OperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        public static OperationResult Succeeded(string message = null) => new OperationResult { Success = true, Message = message };
        public static OperationResult Failed(string error) => new OperationResult { Success = false, Error = error };
    }

    /// <summary>
    /// Generic result class with data.
    /// </summary>
    /// <typeparam name="T">Type of result data.</typeparam>
    public class OperationResult<T> : OperationResult
    {
        public T Data { get; set; }

        public static new OperationResult<T> Succeeded(string message = null) => new OperationResult<T> { Success = true, Message = message };
        public static OperationResult<T> Succeeded(T data, string message = null) => new OperationResult<T> { Success = true, Data = data, Message = message };
        public static new OperationResult<T> Failed(string error) => new OperationResult<T> { Success = false, Error = error };
    }
}
