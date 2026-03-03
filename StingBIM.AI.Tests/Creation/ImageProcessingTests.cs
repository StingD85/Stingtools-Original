// StingBIM.AI.Tests - ImageProcessingTests.cs
// Unit tests for Image to BIM processing components
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;

namespace StingBIM.AI.Tests.Creation
{
    /// <summary>
    /// Unit tests for Image to BIM processing including floor plan interpretation,
    /// sketch recognition, and geometry extraction.
    /// </summary>
    [TestFixture]
    public class ImageProcessingTests
    {
        #region Floor Plan Processing Tests

        [Test]
        public void AnalyzeBoundary_RectangularBoundary_CalculatesCorrectArea()
        {
            // Arrange
            var analyzer = new BoundaryAnalyzerTest();
            var boundary = new List<(double X, double Y)>
            {
                (0, 0), (10000, 0), (10000, 5000), (0, 5000)
            };

            // Act
            var result = analyzer.AnalyzeBoundary(boundary);

            // Assert
            result.Area.Should().BeApproximately(50000000, 1); // 50 sqm in mm²
            result.Perimeter.Should().BeApproximately(30000, 1); // 30m perimeter
        }

        [Test]
        public void AnalyzeBoundary_LShapedBoundary_CalculatesCorrectAspectRatio()
        {
            // Arrange
            var analyzer = new BoundaryAnalyzerTest();
            var boundary = new List<(double X, double Y)>
            {
                (0, 0), (6000, 0), (6000, 3000), (3000, 3000),
                (3000, 6000), (0, 6000)
            };

            // Act
            var result = analyzer.AnalyzeBoundary(boundary);

            // Assert
            result.AspectRatio.Should().BeApproximately(1.0, 0.1); // Roughly square bounding box
        }

        [Test]
        public void DetectWallFromLines_ParallelLines_GroupsIntoWall()
        {
            // Arrange
            var detector = new WallDetectorTest();
            var lines = new List<LineSegment>
            {
                new LineSegment { Start = (0, 0), End = (5000, 0), Thickness = 200 },
                new LineSegment { Start = (0, 200), End = (5000, 200), Thickness = 200 }
            };

            // Act
            var walls = detector.DetectWalls(lines);

            // Assert
            walls.Should().HaveCount(1);
            walls[0].Thickness.Should().BeApproximately(200, 10);
        }

        [Test]
        public void DetectWallFromLines_PerpendicularLines_CreatesMultipleWalls()
        {
            // Arrange
            var detector = new WallDetectorTest();
            var lines = new List<LineSegment>
            {
                new LineSegment { Start = (0, 0), End = (5000, 0), Thickness = 200 },
                new LineSegment { Start = (5000, 0), End = (5000, 3000), Thickness = 200 }
            };

            // Act
            var walls = detector.DetectWalls(lines);

            // Assert
            walls.Should().HaveCountGreaterOrEqualTo(2);
        }

        [Test]
        public void ClassifyWallType_ThickLine_ReturnsExterior()
        {
            // Arrange
            var classifier = new WallClassifierTest();

            // Act
            var wallType = classifier.ClassifyWallType(300);

            // Assert
            wallType.Should().Be("Exterior");
        }

        [Test]
        public void ClassifyWallType_ThinLine_ReturnsPartition()
        {
            // Arrange
            var classifier = new WallClassifierTest();

            // Act
            var wallType = classifier.ClassifyWallType(100);

            // Assert
            wallType.Should().Be("Partition");
        }

        #endregion

        #region Door and Window Detection Tests

        [Test]
        public void DetectDoorSymbol_ArcPattern_IdentifiesAsDoor()
        {
            // Arrange
            var detector = new OpeningDetectorTest();
            var symbol = new SymbolPattern { Type = "Arc", Size = 900, HasLine = true };

            // Act
            var result = detector.ClassifyOpening(symbol);

            // Assert
            result.Type.Should().Be("Door");
            result.Width.Should().Be(900);
        }

        [Test]
        public void DetectWindowSymbol_DoubleLinePattern_IdentifiesAsWindow()
        {
            // Arrange
            var detector = new OpeningDetectorTest();
            var symbol = new SymbolPattern { Type = "DoubleLine", Size = 1200, HasLine = false };

            // Act
            var result = detector.ClassifyOpening(symbol);

            // Assert
            result.Type.Should().Be("Window");
            result.Width.Should().Be(1200);
        }

        [Test]
        public void DetermineSwingDirection_ArcFacingLeft_ReturnsLeftSwing()
        {
            // Arrange
            var detector = new OpeningDetectorTest();
            var arcCurve = new ArcData { StartAngle = 90, EndAngle = 180, Radius = 900 };

            // Act
            var direction = detector.DetermineSwingDirection(arcCurve);

            // Assert
            direction.Should().Be("Left");
        }

        #endregion

        #region Room Detection Tests

        [Test]
        public void DetectRoom_ClosedPolygon_CalculatesArea()
        {
            // Arrange
            var detector = new RoomDetectorTest();
            var boundary = new List<(double X, double Y)>
            {
                (0, 0), (4000, 0), (4000, 3000), (0, 3000)
            };

            // Act
            var room = detector.DetectRoom(boundary);

            // Assert
            room.Area.Should().BeApproximately(12, 0.1); // 12 sqm
        }

        [Test]
        public void InferRoomType_ContainsKitchenLabel_ReturnsKitchen()
        {
            // Arrange
            var inferrer = new RoomTypeInferrerTest();

            // Act
            var roomType = inferrer.InferRoomType("Kitchen");

            // Assert
            roomType.Should().Be("Kitchen");
        }

        [Test]
        [TestCase("BEDROOM", "Bedroom")]
        [TestCase("Living Room", "Living Room")]
        [TestCase("WC", "Bathroom")]
        [TestCase("Bath", "Bathroom")]
        [TestCase("Study", "Office")]
        [TestCase("Office", "Office")]
        public void InferRoomType_VariousLabels_ReturnsCorrectType(string label, string expectedType)
        {
            // Arrange
            var inferrer = new RoomTypeInferrerTest();

            // Act
            var roomType = inferrer.InferRoomType(label);

            // Assert
            roomType.Should().Be(expectedType);
        }

        #endregion

        #region Scale Calibration Tests

        [Test]
        public void CalibrateScale_KnownDimension_CalculatesPixelRatio()
        {
            // Arrange
            var calibrator = new ScaleCalibratorTest();
            var pixelLength = 500; // 500 pixels
            var realLength = 5000; // 5000mm = 5m

            // Act
            var scale = calibrator.CalibrateScale(pixelLength, realLength);

            // Assert
            scale.PixelsToMm.Should().BeApproximately(10, 0.1); // 10 mm per pixel
        }

        [Test]
        public void CalibrateScale_ScaleBar1to100_CalculatesCorrectRatio()
        {
            // Arrange
            var calibrator = new ScaleCalibratorTest();

            // Act
            var scale = calibrator.CalibrateFromScaleBar("1:100", 10); // 10 pixels per mm at 1:100

            // Assert
            scale.DrawingScale.Should().Be(100);
            scale.PixelsToMm.Should().BeApproximately(10, 0.1);
        }

        #endregion

        #region Geometry Extraction Tests

        [Test]
        public void ExtractLines_HoughTransform_DetectsHorizontalLine()
        {
            // Arrange
            var extractor = new GeometryExtractorTest();
            var points = new List<(int X, int Y)>();
            for (int x = 0; x < 100; x++)
            {
                points.Add((x, 50));
            }

            // Act
            var lines = extractor.DetectLines(points);

            // Assert
            lines.Should().HaveCountGreaterOrEqualTo(1);
            lines[0].Angle.Should().BeApproximately(0, 5); // Horizontal
        }

        [Test]
        public void ExtractLines_HoughTransform_DetectsVerticalLine()
        {
            // Arrange
            var extractor = new GeometryExtractorTest();
            var points = new List<(int X, int Y)>();
            for (int y = 0; y < 100; y++)
            {
                points.Add((50, y));
            }

            // Act
            var lines = extractor.DetectLines(points);

            // Assert
            lines.Should().HaveCountGreaterOrEqualTo(1);
            lines[0].Angle.Should().BeApproximately(90, 5); // Vertical
        }

        [Test]
        public void FindEnclosedRegions_FourWalls_FindsOneRoom()
        {
            // Arrange
            var finder = new RegionFinderTest();
            var walls = new List<WallSegment>
            {
                new WallSegment { Start = (0, 0), End = (4000, 0) },
                new WallSegment { Start = (4000, 0), End = (4000, 3000) },
                new WallSegment { Start = (4000, 3000), End = (0, 3000) },
                new WallSegment { Start = (0, 3000), End = (0, 0) }
            };

            // Act
            var regions = finder.FindEnclosedRegions(walls);

            // Assert
            regions.Should().HaveCount(1);
        }

        #endregion

        #region Sketch Processing Tests

        [Test]
        public void RecognizeShape_NearlyClosedLoop_RecognizesAsRectangle()
        {
            // Arrange
            var recognizer = new ShapeRecognizerTest();
            var points = new List<(double X, double Y)>
            {
                (0, 0), (100, 2), (102, 100), (1, 99), (0, 0)
            };

            // Act
            var shape = recognizer.RecognizeShape(points);

            // Assert
            shape.Type.Should().Be("Rectangle");
            shape.Confidence.Should().BeGreaterThan(0.7);
        }

        [Test]
        public void RecognizeShape_CircularPoints_RecognizesAsCircle()
        {
            // Arrange
            var recognizer = new ShapeRecognizerTest();
            var points = new List<(double X, double Y)>();
            for (int i = 0; i < 360; i += 10)
            {
                var rad = i * Math.PI / 180;
                points.Add((50 + 30 * Math.Cos(rad), 50 + 30 * Math.Sin(rad)));
            }

            // Act
            var shape = recognizer.RecognizeShape(points);

            // Assert
            shape.Type.Should().Be("Circle");
            shape.Confidence.Should().BeGreaterThan(0.8);
        }

        [Test]
        public void SnapToGrid_NearGridPoint_SnapsCorrectly()
        {
            // Arrange
            var snapper = new GridSnapperTest(100); // 100mm grid
            var point = (103.5, 198.2);

            // Act
            var snapped = snapper.Snap(point);

            // Assert
            snapped.X.Should().Be(100);
            snapped.Y.Should().Be(200);
        }

        [Test]
        public void AlignToOrthogonal_NearlyHorizontalLine_AlignsHorizontal()
        {
            // Arrange
            var aligner = new OrthogonalAlignerTest(5); // 5 degree tolerance
            var line = new LineSegment { Start = (0, 0), End = (100, 3) };

            // Act
            var aligned = aligner.Align(line);

            // Assert
            aligned.End.Y.Should().Be(0); // Aligned to horizontal
        }

        #endregion
    }

    #region Test Helper Classes

    public class BoundaryAnalyzerTest
    {
        public BoundaryAnalysisResult AnalyzeBoundary(List<(double X, double Y)> points)
        {
            var minX = double.MaxValue;
            var maxX = double.MinValue;
            var minY = double.MaxValue;
            var maxY = double.MinValue;

            foreach (var p in points)
            {
                minX = Math.Min(minX, p.X);
                maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y);
                maxY = Math.Max(maxY, p.Y);
            }

            // Calculate polygon area using shoelace formula
            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                area += points[i].X * points[j].Y;
                area -= points[j].X * points[i].Y;
            }
            area = Math.Abs(area) / 2;

            // Calculate perimeter
            double perimeter = 0;
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                perimeter += Math.Sqrt(Math.Pow(points[j].X - points[i].X, 2) +
                                      Math.Pow(points[j].Y - points[i].Y, 2));
            }

            return new BoundaryAnalysisResult
            {
                Area = area,
                Perimeter = perimeter,
                AspectRatio = (maxX - minX) / (maxY - minY)
            };
        }
    }

    public class BoundaryAnalysisResult
    {
        public double Area { get; set; }
        public double Perimeter { get; set; }
        public double AspectRatio { get; set; }
    }

    public class LineSegment
    {
        public (double X, double Y) Start { get; set; }
        public (double X, double Y) End { get; set; }
        public double Thickness { get; set; }
        public double Angle => Math.Atan2(End.Y - Start.Y, End.X - Start.X) * 180 / Math.PI;
    }

    public class WallDetectorTest
    {
        public List<DetectedWall> DetectWalls(List<LineSegment> lines)
        {
            var walls = new List<DetectedWall>();
            var grouped = new HashSet<int>();

            for (int i = 0; i < lines.Count; i++)
            {
                if (grouped.Contains(i)) continue;

                var wall = new DetectedWall
                {
                    Start = lines[i].Start,
                    End = lines[i].End,
                    Thickness = lines[i].Thickness
                };

                // Check for parallel lines that form a wall
                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (AreParallel(lines[i], lines[j]) && AreClose(lines[i], lines[j]))
                    {
                        grouped.Add(j);
                        wall.Thickness = Math.Max(wall.Thickness, lines[j].Thickness);
                    }
                }

                walls.Add(wall);
            }

            return walls;
        }

        private bool AreParallel(LineSegment a, LineSegment b)
        {
            return Math.Abs(a.Angle - b.Angle) < 10;
        }

        private bool AreClose(LineSegment a, LineSegment b)
        {
            var dist = Math.Min(
                Distance(a.Start, b.Start),
                Distance(a.End, b.End));
            return dist < 300;
        }

        private double Distance((double X, double Y) p1, (double X, double Y) p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }
    }

    public class DetectedWall
    {
        public (double X, double Y) Start { get; set; }
        public (double X, double Y) End { get; set; }
        public double Thickness { get; set; }
    }

    public class WallClassifierTest
    {
        public string ClassifyWallType(double thickness)
        {
            if (thickness >= 250) return "Exterior";
            if (thickness >= 150) return "Interior";
            return "Partition";
        }
    }

    public class SymbolPattern
    {
        public string Type { get; set; }
        public double Size { get; set; }
        public bool HasLine { get; set; }
    }

    public class OpeningResult
    {
        public string Type { get; set; }
        public double Width { get; set; }
    }

    public class ArcData
    {
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
        public double Radius { get; set; }
    }

    public class OpeningDetectorTest
    {
        public OpeningResult ClassifyOpening(SymbolPattern symbol)
        {
            if (symbol.Type == "Arc" && symbol.HasLine)
            {
                return new OpeningResult { Type = "Door", Width = symbol.Size };
            }
            if (symbol.Type == "DoubleLine")
            {
                return new OpeningResult { Type = "Window", Width = symbol.Size };
            }
            return new OpeningResult { Type = "Unknown", Width = symbol.Size };
        }

        public string DetermineSwingDirection(ArcData arc)
        {
            var midAngle = (arc.StartAngle + arc.EndAngle) / 2;
            if (midAngle > 90 && midAngle <= 180) return "Left";
            if (midAngle > 180 && midAngle <= 270) return "Down";
            if (midAngle > 270) return "Right";
            return "Up";
        }
    }

    public class DetectedRoom
    {
        public double Area { get; set; }
        public string Type { get; set; }
    }

    public class RoomDetectorTest
    {
        public DetectedRoom DetectRoom(List<(double X, double Y)> boundary)
        {
            double area = 0;
            for (int i = 0; i < boundary.Count; i++)
            {
                int j = (i + 1) % boundary.Count;
                area += boundary[i].X * boundary[j].Y;
                area -= boundary[j].X * boundary[i].Y;
            }
            area = Math.Abs(area) / 2;

            return new DetectedRoom { Area = area / 1000000 }; // Convert mm² to m²
        }
    }

    public class RoomTypeInferrerTest
    {
        private readonly Dictionary<string, string> _roomTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            { "bedroom", "Bedroom" },
            { "bed", "Bedroom" },
            { "living room", "Living Room" },
            { "living", "Living Room" },
            { "kitchen", "Kitchen" },
            { "bathroom", "Bathroom" },
            { "bath", "Bathroom" },
            { "wc", "Bathroom" },
            { "toilet", "Bathroom" },
            { "office", "Office" },
            { "study", "Office" },
            { "dining", "Dining Room" },
            { "garage", "Garage" }
        };

        public string InferRoomType(string label)
        {
            foreach (var kvp in _roomTypes)
            {
                if (label.ToLowerInvariant().Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }
            return "Room";
        }
    }

    public class ScaleInfo
    {
        public double PixelsToMm { get; set; }
        public int DrawingScale { get; set; }
    }

    public class ScaleCalibratorTest
    {
        public ScaleInfo CalibrateScale(double pixelLength, double realLength)
        {
            return new ScaleInfo
            {
                PixelsToMm = realLength / pixelLength
            };
        }

        public ScaleInfo CalibrateFromScaleBar(string scale, double pixelsPerMm)
        {
            var parts = scale.Split(':');
            var drawingScale = int.Parse(parts[1]);

            return new ScaleInfo
            {
                DrawingScale = drawingScale,
                PixelsToMm = pixelsPerMm
            };
        }
    }

    public class DetectedLine
    {
        public (double X, double Y) Start { get; set; }
        public (double X, double Y) End { get; set; }
        public double Angle { get; set; }
    }

    public class GeometryExtractorTest
    {
        public List<DetectedLine> DetectLines(List<(int X, int Y)> points)
        {
            var lines = new List<DetectedLine>();

            if (points.Count < 2) return lines;

            // Simple line detection - find dominant direction
            var dx = points[points.Count - 1].X - points[0].X;
            var dy = points[points.Count - 1].Y - points[0].Y;
            var angle = Math.Atan2(dy, dx) * 180 / Math.PI;

            lines.Add(new DetectedLine
            {
                Start = (points[0].X, points[0].Y),
                End = (points[points.Count - 1].X, points[points.Count - 1].Y),
                Angle = angle
            });

            return lines;
        }
    }

    public class WallSegment
    {
        public (double X, double Y) Start { get; set; }
        public (double X, double Y) End { get; set; }
    }

    public class EnclosedRegion
    {
        public List<(double X, double Y)> Boundary { get; set; }
        public double Area { get; set; }
    }

    public class RegionFinderTest
    {
        public List<EnclosedRegion> FindEnclosedRegions(List<WallSegment> walls)
        {
            var regions = new List<EnclosedRegion>();

            // Simplified: assume walls form a closed polygon
            if (walls.Count >= 3)
            {
                var boundary = new List<(double X, double Y)>();
                foreach (var wall in walls)
                {
                    boundary.Add(wall.Start);
                }

                // Check if closed
                var firstWall = walls[0];
                var lastWall = walls[walls.Count - 1];
                var distance = Math.Sqrt(Math.Pow(lastWall.End.X - firstWall.Start.X, 2) +
                                        Math.Pow(lastWall.End.Y - firstWall.Start.Y, 2));

                if (distance < 100) // Closed within 100mm
                {
                    regions.Add(new EnclosedRegion { Boundary = boundary });
                }
            }

            return regions;
        }
    }

    public class RecognizedShape
    {
        public string Type { get; set; }
        public double Confidence { get; set; }
    }

    public class ShapeRecognizerTest
    {
        public RecognizedShape RecognizeShape(List<(double X, double Y)> points)
        {
            if (points.Count < 3)
                return new RecognizedShape { Type = "Unknown", Confidence = 0 };

            // Check if closed
            var isClosed = Math.Sqrt(
                Math.Pow(points[points.Count - 1].X - points[0].X, 2) +
                Math.Pow(points[points.Count - 1].Y - points[0].Y, 2)) < 20;

            if (!isClosed)
                return new RecognizedShape { Type = "Line", Confidence = 0.8 };

            // Calculate circularity
            var center = (
                X: points.Average(p => p.X),
                Y: points.Average(p => p.Y)
            );

            var distances = points.Select(p =>
                Math.Sqrt(Math.Pow(p.X - center.X, 2) + Math.Pow(p.Y - center.Y, 2))).ToList();

            var avgRadius = distances.Average();
            var variance = distances.Average(d => Math.Pow(d - avgRadius, 2));
            var circularity = 1 - (Math.Sqrt(variance) / avgRadius);

            if (circularity > 0.9)
                return new RecognizedShape { Type = "Circle", Confidence = circularity };

            // Check for rectangle
            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);

            var rectangularity = 0.0;
            foreach (var p in points)
            {
                var nearEdge =
                    Math.Abs(p.X - minX) < 10 ||
                    Math.Abs(p.X - maxX) < 10 ||
                    Math.Abs(p.Y - minY) < 10 ||
                    Math.Abs(p.Y - maxY) < 10;
                if (nearEdge) rectangularity += 1.0 / points.Count;
            }

            if (rectangularity > 0.7)
                return new RecognizedShape { Type = "Rectangle", Confidence = rectangularity };

            return new RecognizedShape { Type = "Polygon", Confidence = 0.6 };
        }
    }

    public class GridSnapperTest
    {
        private readonly double _gridSize;

        public GridSnapperTest(double gridSize)
        {
            _gridSize = gridSize;
        }

        public (double X, double Y) Snap((double X, double Y) point)
        {
            return (
                Math.Round(point.X / _gridSize) * _gridSize,
                Math.Round(point.Y / _gridSize) * _gridSize
            );
        }
    }

    public class OrthogonalAlignerTest
    {
        private readonly double _toleranceDegrees;

        public OrthogonalAlignerTest(double toleranceDegrees)
        {
            _toleranceDegrees = toleranceDegrees;
        }

        public LineSegment Align(LineSegment line)
        {
            var angle = Math.Atan2(
                line.End.Y - line.Start.Y,
                line.End.X - line.Start.X) * 180 / Math.PI;

            var length = Math.Sqrt(
                Math.Pow(line.End.X - line.Start.X, 2) +
                Math.Pow(line.End.Y - line.Start.Y, 2));

            // Snap to nearest 90 degree
            double alignedAngle;
            if (Math.Abs(angle) < _toleranceDegrees || Math.Abs(angle - 180) < _toleranceDegrees)
                alignedAngle = 0;
            else if (Math.Abs(angle - 90) < _toleranceDegrees)
                alignedAngle = 90;
            else if (Math.Abs(angle + 90) < _toleranceDegrees)
                alignedAngle = -90;
            else
                alignedAngle = angle;

            var radians = alignedAngle * Math.PI / 180;
            return new LineSegment
            {
                Start = line.Start,
                End = (
                    line.Start.X + length * Math.Cos(radians),
                    line.Start.Y + length * Math.Sin(radians)
                ),
                Thickness = line.Thickness
            };
        }
    }

    #endregion
}
