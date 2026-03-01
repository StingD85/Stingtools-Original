// StingBIM.AI.Creation.Elements.WindowCreator
// Creates windows in Revit walls from AI commands
// Master Proposal Reference: Part 4.2 Phase 1 Month 2 - Basic Elements

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Elements
{
    /// <summary>
    /// Creates and modifies windows in Revit based on natural language commands.
    /// Handles fixed windows, operable windows, curtain walls, and skylights.
    /// </summary>
    public class WindowCreator : IElementCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Standard window dimensions (mm)
        private const double DefaultWindowWidth = 1200;
        private const double DefaultWindowHeight = 1500;
        private const double DefaultSillHeight = 900;
        private const double MinWindowDimension = 300;
        private const double MaxWindowDimension = 6000;

        // Daylighting analysis thresholds
        private const double MinDaylightFactor = 2.0; // 2% minimum for habitable rooms
        private const double TargetDaylightFactor = 5.0; // 5% target for good daylighting

        /// <summary>
        /// Creates a window from parameters.
        /// </summary>
        public async Task<CreationResult> CreateAsync(
            ElementCreationParams parameters,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                Logger.Info($"Creating window with params: {parameters}");

                var result = new CreationResult
                {
                    ElementType = "Window",
                    StartTime = DateTime.Now
                };

                try
                {
                    // Validate parameters
                    var validation = ValidateParameters(parameters);
                    if (!validation.IsValid)
                    {
                        result.Success = false;
                        result.Error = validation.Error;
                        return result;
                    }

                    // Extract window parameters
                    var windowParams = ExtractWindowParameters(parameters);

                    // Calculate glazing performance metrics
                    var performance = CalculatePerformanceMetrics(windowParams);

                    // In real implementation, this would call Revit API:
                    // doc.Create.NewFamilyInstance(location, windowType, wall, level, StructuralType.NonStructural)

                    result.Success = true;
                    result.CreatedElementId = GenerateElementId();
                    result.Parameters = windowParams;
                    result.Message = $"Created {windowParams.Width:F0}x{windowParams.Height:F0}mm {windowParams.WindowTypeName} window";

                    // Add metadata
                    result.Metadata["WindowType"] = windowParams.WindowTypeName;
                    result.Metadata["Width"] = windowParams.Width;
                    result.Metadata["Height"] = windowParams.Height;
                    result.Metadata["SillHeight"] = windowParams.SillHeight;
                    result.Metadata["HostWallId"] = windowParams.HostWallId;
                    result.Metadata["UValue"] = performance.UValue;
                    result.Metadata["SHGC"] = performance.SHGC;
                    result.Metadata["VLT"] = performance.VLT;
                    result.Metadata["GlazingArea"] = performance.GlazingArea;

                    Logger.Info($"Window created: {result.CreatedElementId}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Window creation failed");
                    result.Success = false;
                    result.Error = ex.Message;
                }

                result.EndTime = DateTime.Now;
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Creates a window in a wall at a specified location.
        /// </summary>
        public async Task<CreationResult> CreateInWallAsync(
            int wallElementId,
            double positionAlongWall,
            WindowCreationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new WindowCreationOptions();

            var parameters = new ElementCreationParams
            {
                ElementType = "Window",
                Parameters = new Dictionary<string, object>
                {
                    { "HostWallId", wallElementId },
                    { "PositionAlongWall", positionAlongWall },
                    { "Width", options.Width ?? DefaultWindowWidth },
                    { "Height", options.Height ?? DefaultWindowHeight },
                    { "SillHeight", options.SillHeight ?? DefaultSillHeight },
                    { "WindowType", options.WindowTypeName ?? "Fixed" },
                    { "GlazingType", options.GlazingType },
                    { "FrameMaterial", options.FrameMaterial },
                    { "Operable", options.IsOperable }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Creates a window ribbon (continuous windows along a wall).
        /// </summary>
        public async Task<BatchCreationResult> CreateWindowRibbonAsync(
            int wallElementId,
            double wallLength,
            double windowWidth,
            double mullionWidth,
            WindowCreationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Creating window ribbon in wall {wallElementId}");

            var results = new BatchCreationResult { StartTime = DateTime.Now };
            options ??= new WindowCreationOptions { Width = windowWidth };

            // Calculate number of windows
            var effectiveWidth = windowWidth + mullionWidth;
            var numWindows = (int)Math.Floor((wallLength - mullionWidth) / effectiveWidth);

            // Center the ribbon
            var totalRibbonWidth = numWindows * windowWidth + (numWindows - 1) * mullionWidth;
            var startOffset = (wallLength - totalRibbonWidth) / 2 + windowWidth / 2;

            for (int i = 0; i < numWindows; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var position = startOffset + (i * effectiveWidth);
                var result = await CreateInWallAsync(wallElementId, position, options, cancellationToken);
                results.Results.Add(result);

                if (!result.Success)
                {
                    Logger.Warn($"Failed to create ribbon window {i + 1}: {result.Error}");
                }
            }

            results.EndTime = DateTime.Now;
            results.TotalCreated = results.Results.Count(r => r.Success);
            results.TotalFailed = results.Results.Count(r => !r.Success);

            return results;
        }

        /// <summary>
        /// Creates windows to meet a target daylight factor for a room.
        /// </summary>
        public async Task<BatchCreationResult> CreateForDaylightTargetAsync(
            RoomDaylightRequirements requirements,
            List<WallInfo> availableWalls,
            WindowCreationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Creating windows for {requirements.TargetDaylightFactor}% daylight factor");

            var results = new BatchCreationResult { StartTime = DateTime.Now };
            options ??= new WindowCreationOptions();

            // Calculate required glazing area (simplified daylight factor formula)
            // DF = (Ag * T * theta) / (A * (1 - R^2))
            // Simplified: Ag = (DF * FloorArea) / (T * 0.5)
            var transmittance = options.GlazingType switch
            {
                GlazingType.SingleClear => 0.90,
                GlazingType.DoubleClear => 0.81,
                GlazingType.DoubleLowE => 0.70,
                GlazingType.TripleLowE => 0.60,
                _ => 0.75
            };

            var requiredGlazingArea = (requirements.TargetDaylightFactor / 100.0 * requirements.FloorArea) / (transmittance * 0.5);
            Logger.Info($"Required glazing area: {requiredGlazingArea:F2} m²");

            var currentGlazingArea = 0.0;

            // Sort walls by orientation preference (south-facing first for northern hemisphere)
            var sortedWalls = availableWalls
                .OrderBy(w => Math.Abs(w.Orientation - requirements.PreferredOrientation))
                .ToList();

            foreach (var wall in sortedWalls)
            {
                if (currentGlazingArea >= requiredGlazingArea)
                    break;

                cancellationToken.ThrowIfCancellationRequested();

                // Calculate how much glazing to add to this wall
                var maxWallGlazing = wall.Length * options.Height.GetValueOrDefault(DefaultWindowHeight) * 0.7 / 1000000; // 70% max
                var neededGlazing = Math.Min(maxWallGlazing, requiredGlazingArea - currentGlazingArea);

                // Calculate window dimensions
                var windowHeight = options.Height ?? DefaultWindowHeight;
                var windowWidth = (neededGlazing * 1000000) / windowHeight;

                if (windowWidth >= MinWindowDimension)
                {
                    var windowOptions = new WindowCreationOptions
                    {
                        Width = windowWidth,
                        Height = windowHeight,
                        GlazingType = options.GlazingType,
                        WindowTypeName = options.WindowTypeName
                    };

                    var result = await CreateInWallAsync(
                        wall.ElementId,
                        wall.Length / 2, // Center on wall
                        windowOptions,
                        cancellationToken);

                    results.Results.Add(result);

                    if (result.Success)
                    {
                        currentGlazingArea += (windowWidth * windowHeight) / 1000000;
                    }
                }
            }

            results.EndTime = DateTime.Now;
            results.TotalCreated = results.Results.Count(r => r.Success);
            results.TotalFailed = results.Results.Count(r => !r.Success);

            Logger.Info($"Achieved glazing area: {currentGlazingArea:F2} m² ({currentGlazingArea / requiredGlazingArea * 100:F1}% of target)");

            return results;
        }

        /// <summary>
        /// Creates a skylight in a roof.
        /// </summary>
        public async Task<CreationResult> CreateSkylightAsync(
            int roofElementId,
            Point3D location,
            SkylightOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new SkylightOptions();

            Logger.Info($"Creating skylight in roof {roofElementId} at {location}");

            var parameters = new ElementCreationParams
            {
                ElementType = "Skylight",
                Location = location,
                Parameters = new Dictionary<string, object>
                {
                    { "HostRoofId", roofElementId },
                    { "Width", options.Width },
                    { "Length", options.Length },
                    { "SkylightType", options.SkylightType },
                    { "GlazingType", options.GlazingType },
                    { "HasShade", options.HasShade },
                    { "IsOperable", options.IsOperable }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Modifies an existing window's properties.
        /// </summary>
        public async Task<ModificationResult> ModifyAsync(
            int elementId,
            Dictionary<string, object> modifications,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                Logger.Info($"Modifying window {elementId}");

                var result = new ModificationResult
                {
                    ElementId = elementId,
                    StartTime = DateTime.Now
                };

                try
                {
                    foreach (var (key, value) in modifications)
                    {
                        result.ModifiedProperties[key] = value;

                        switch (key.ToUpperInvariant())
                        {
                            case "WIDTH":
                                Logger.Debug($"Setting window width to {value}");
                                break;
                            case "HEIGHT":
                                Logger.Debug($"Setting window height to {value}");
                                break;
                            case "SILLHEIGHT":
                                Logger.Debug($"Setting sill height to {value}");
                                break;
                            case "WINDOWTYPE":
                                Logger.Debug($"Changing window type to {value}");
                                break;
                            case "GLAZINGTYPE":
                                Logger.Debug($"Changing glazing to {value}");
                                break;
                        }
                    }

                    result.Success = true;
                    result.Message = $"Modified {modifications.Count} properties";
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to modify window {elementId}");
                    result.Success = false;
                    result.Error = ex.Message;
                }

                result.EndTime = DateTime.Now;
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Gets available window types in the current document.
        /// </summary>
        public IEnumerable<WindowTypeInfo> GetAvailableWindowTypes()
        {
            return new List<WindowTypeInfo>
            {
                new WindowTypeInfo { Id = 1, Name = "Fixed", Width = 1200, Height = 1500, IsOperable = false },
                new WindowTypeInfo { Id = 2, Name = "Casement-Single", Width = 600, Height = 1200, IsOperable = true, OperationType = "Casement" },
                new WindowTypeInfo { Id = 3, Name = "Casement-Double", Width = 1200, Height = 1200, IsOperable = true, OperationType = "Casement" },
                new WindowTypeInfo { Id = 4, Name = "Awning", Width = 900, Height = 600, IsOperable = true, OperationType = "Awning" },
                new WindowTypeInfo { Id = 5, Name = "Hopper", Width = 900, Height = 600, IsOperable = true, OperationType = "Hopper" },
                new WindowTypeInfo { Id = 6, Name = "Double-Hung", Width = 900, Height = 1500, IsOperable = true, OperationType = "Hung" },
                new WindowTypeInfo { Id = 7, Name = "Single-Hung", Width = 900, Height = 1500, IsOperable = true, OperationType = "Hung" },
                new WindowTypeInfo { Id = 8, Name = "Sliding-Horizontal", Width = 1800, Height = 1200, IsOperable = true, OperationType = "Sliding" },
                new WindowTypeInfo { Id = 9, Name = "Sliding-Vertical", Width = 900, Height = 1800, IsOperable = true, OperationType = "Sliding" },
                new WindowTypeInfo { Id = 10, Name = "Picture", Width = 2400, Height = 1800, IsOperable = false },
                new WindowTypeInfo { Id = 11, Name = "Bay", Width = 2400, Height = 1500, IsOperable = true, IsBay = true },
                new WindowTypeInfo { Id = 12, Name = "Bow", Width = 3000, Height = 1500, IsOperable = true, IsBow = true },
                new WindowTypeInfo { Id = 13, Name = "Skylight-Fixed", Width = 900, Height = 1200, IsOperable = false, IsSkylight = true },
                new WindowTypeInfo { Id = 14, Name = "Skylight-Venting", Width = 900, Height = 1200, IsOperable = true, IsSkylight = true }
            };
        }

        #region Private Methods

        private ValidationResult ValidateParameters(ElementCreationParams parameters)
        {
            // Check dimensions
            foreach (var dim in new[] { "Width", "Height" })
            {
                if (parameters.Parameters.TryGetValue(dim, out var valueObj))
                {
                    var value = Convert.ToDouble(valueObj);

                    if (value < MinWindowDimension)
                    {
                        return ValidationResult.Invalid($"Window {dim.ToLower()} ({value}mm) is below minimum ({MinWindowDimension}mm)");
                    }

                    if (value > MaxWindowDimension)
                    {
                        return ValidationResult.Invalid($"Window {dim.ToLower()} ({value}mm) exceeds maximum ({MaxWindowDimension}mm)");
                    }
                }
            }

            // Skylights don't need a wall host
            var elementType = parameters.ElementType;
            if (elementType != "Skylight")
            {
                if (!parameters.Parameters.ContainsKey("HostWallId") &&
                    !parameters.Parameters.ContainsKey("HostWall") &&
                    parameters.AdditionalData == null)
                {
                    return ValidationResult.Invalid("Window requires a host wall");
                }
            }

            return ValidationResult.Valid();
        }

        private WindowParameters ExtractWindowParameters(ElementCreationParams parameters)
        {
            return new WindowParameters
            {
                Width = GetDouble(parameters.Parameters, "Width", DefaultWindowWidth),
                Height = GetDouble(parameters.Parameters, "Height", DefaultWindowHeight),
                SillHeight = GetDouble(parameters.Parameters, "SillHeight", DefaultSillHeight),
                WindowTypeName = GetString(parameters.Parameters, "WindowType", "Fixed"),
                HostWallId = GetInt(parameters.Parameters, "HostWallId", 0),
                PositionAlongWall = GetDouble(parameters.Parameters, "PositionAlongWall", 0),
                GlazingType = GetEnum(parameters.Parameters, "GlazingType", GlazingType.DoubleLowE),
                FrameMaterial = GetEnum(parameters.Parameters, "FrameMaterial", FrameMaterial.Aluminum),
                IsOperable = GetBool(parameters.Parameters, "Operable", false)
            };
        }

        private WindowPerformanceMetrics CalculatePerformanceMetrics(WindowParameters windowParams)
        {
            // Glazing performance based on type
            var (uValue, shgc, vlt) = windowParams.GlazingType switch
            {
                GlazingType.SingleClear => (5.8, 0.86, 0.90),
                GlazingType.DoubleClear => (2.8, 0.76, 0.81),
                GlazingType.DoubleLowE => (1.6, 0.40, 0.70),
                GlazingType.TripleLowE => (0.8, 0.27, 0.60),
                GlazingType.DoubleArgon => (1.4, 0.38, 0.72),
                GlazingType.TripleArgon => (0.6, 0.25, 0.58),
                _ => (2.0, 0.50, 0.70)
            };

            // Frame adjustment
            var frameMultiplier = windowParams.FrameMaterial switch
            {
                FrameMaterial.Aluminum => 1.2,
                FrameMaterial.AluminumThermalBreak => 1.0,
                FrameMaterial.Wood => 0.9,
                FrameMaterial.Vinyl => 0.85,
                FrameMaterial.Fiberglass => 0.82,
                _ => 1.0
            };

            var glazingArea = (windowParams.Width * windowParams.Height) / 1000000.0; // Convert to m²

            return new WindowPerformanceMetrics
            {
                UValue = uValue * frameMultiplier,
                SHGC = shgc,
                VLT = vlt,
                GlazingArea = glazingArea
            };
        }

        private double GetDouble(Dictionary<string, object> dict, string key, double defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is double d) return d;
                if (double.TryParse(value?.ToString()?.Replace("mm", "").Replace("m", "000"), out d)) return d;
            }
            return defaultValue;
        }

        private string GetString(Dictionary<string, object> dict, string key, string defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
                return value?.ToString() ?? defaultValue;
            return defaultValue;
        }

        private bool GetBool(Dictionary<string, object> dict, string key, bool defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is bool b) return b;
                if (bool.TryParse(value?.ToString(), out b)) return b;
            }
            return defaultValue;
        }

        private int GetInt(Dictionary<string, object> dict, string key, int defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is int i) return i;
                if (int.TryParse(value?.ToString(), out i)) return i;
            }
            return defaultValue;
        }

        private T GetEnum<T>(Dictionary<string, object> dict, string key, T defaultValue) where T : struct
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is T t) return t;
                if (Enum.TryParse<T>(value?.ToString(), out var e)) return e;
            }
            return defaultValue;
        }

        private int GenerateElementId()
        {
            return new Random().Next(100000, 999999);
        }

        #endregion
    }

    #region Supporting Classes

    public class WindowCreationOptions
    {
        public double? Width { get; set; }
        public double? Height { get; set; }
        public double? SillHeight { get; set; }
        public string WindowTypeName { get; set; }
        public GlazingType GlazingType { get; set; } = GlazingType.DoubleLowE;
        public FrameMaterial FrameMaterial { get; set; } = FrameMaterial.AluminumThermalBreak;
        public bool IsOperable { get; set; }
    }

    public class WindowParameters
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double SillHeight { get; set; }
        public string WindowTypeName { get; set; }
        public int HostWallId { get; set; }
        public double PositionAlongWall { get; set; }
        public GlazingType GlazingType { get; set; }
        public FrameMaterial FrameMaterial { get; set; }
        public bool IsOperable { get; set; }
    }

    public class WindowPerformanceMetrics
    {
        public double UValue { get; set; } // W/m²K
        public double SHGC { get; set; } // Solar Heat Gain Coefficient
        public double VLT { get; set; } // Visible Light Transmittance
        public double GlazingArea { get; set; } // m²
    }

    public class WindowTypeInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsOperable { get; set; }
        public string OperationType { get; set; }
        public bool IsBay { get; set; }
        public bool IsBow { get; set; }
        public bool IsSkylight { get; set; }
    }

    public class RoomDaylightRequirements
    {
        public double FloorArea { get; set; } // m²
        public double TargetDaylightFactor { get; set; } = 5.0; // %
        public double PreferredOrientation { get; set; } = 180; // degrees, 180 = south
    }

    public class WallInfo
    {
        public int ElementId { get; set; }
        public double Length { get; set; } // mm
        public double Orientation { get; set; } // degrees from north
        public bool IsExterior { get; set; }
    }

    public class SkylightOptions
    {
        public double Width { get; set; } = 900;
        public double Length { get; set; } = 1200;
        public string SkylightType { get; set; } = "Fixed";
        public GlazingType GlazingType { get; set; } = GlazingType.DoubleLowE;
        public bool HasShade { get; set; }
        public bool IsOperable { get; set; }
    }

    public enum GlazingType
    {
        SingleClear,
        DoubleClear,
        DoubleLowE,
        TripleLowE,
        DoubleArgon,
        TripleArgon,
        Tinted,
        Reflective
    }

    public enum FrameMaterial
    {
        Aluminum,
        AluminumThermalBreak,
        Wood,
        Vinyl,
        Fiberglass,
        Steel,
        Composite
    }

    #endregion
}
