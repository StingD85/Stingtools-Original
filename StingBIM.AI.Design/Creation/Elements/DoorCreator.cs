// StingBIM.AI.Creation.Elements.DoorCreator
// Creates doors in Revit walls from AI commands
// Master Proposal Reference: Part 4.2 Phase 1 Month 2 - Basic Elements

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Creation.Elements
{
    /// <summary>
    /// Creates and modifies doors in Revit based on natural language commands.
    /// Handles single doors, double doors, sliding doors, and accessibility-compliant doors.
    /// </summary>
    public class DoorCreator : IElementCreator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Standard door dimensions (mm)
        private const double DefaultSingleDoorWidth = 900;
        private const double DefaultDoubleDoorWidth = 1800;
        private const double DefaultDoorHeight = 2100;
        private const double DefaultAccessibleDoorWidth = 1000;
        private const double MinDoorWidth = 600;
        private const double MaxDoorWidth = 3600;

        // Fire door requirements
        private const double FireDoorMinRating = 20; // minutes
        private const double FireDoorMaxLeakage = 25; // L/s per meter

        /// <summary>
        /// Creates a door from parameters.
        /// </summary>
        public async Task<CreationResult> CreateAsync(
            ElementCreationParams parameters,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                Logger.Info($"Creating door with params: {parameters}");

                var result = new CreationResult
                {
                    ElementType = "Door",
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

                    // Extract door parameters
                    var doorParams = ExtractDoorParameters(parameters);

                    // Check accessibility compliance if required
                    if (doorParams.RequiresAccessibility)
                    {
                        var accessibilityCheck = ValidateAccessibility(doorParams);
                        if (!accessibilityCheck.IsValid)
                        {
                            Logger.Warn($"Accessibility issue: {accessibilityCheck.Error}");
                            foreach (var warning in accessibilityCheck.Warnings)
                            {
                                result.Metadata["Warning_" + Guid.NewGuid().ToString().Substring(0, 8)] = warning;
                            }
                        }
                    }

                    // In real implementation, this would call Revit API:
                    // doc.Create.NewFamilyInstance(location, doorType, wall, level, StructuralType.NonStructural)

                    result.Success = true;
                    result.CreatedElementId = GenerateElementId();
                    result.Parameters = doorParams;
                    result.Message = $"Created {doorParams.Width:F0}x{doorParams.Height:F0}mm {doorParams.DoorTypeName} door";

                    // Add metadata
                    result.Metadata["DoorType"] = doorParams.DoorTypeName;
                    result.Metadata["Width"] = doorParams.Width;
                    result.Metadata["Height"] = doorParams.Height;
                    result.Metadata["HostWallId"] = doorParams.HostWallId;
                    result.Metadata["HandSwing"] = doorParams.HandSwing.ToString();
                    result.Metadata["IsAccessible"] = doorParams.RequiresAccessibility;

                    Logger.Info($"Door created: {result.CreatedElementId}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Door creation failed");
                    result.Success = false;
                    result.Error = ex.Message;
                }

                result.EndTime = DateTime.Now;
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Creates a door in a wall at a specified location.
        /// </summary>
        public async Task<CreationResult> CreateInWallAsync(
            int wallElementId,
            double positionAlongWall,
            DoorCreationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new DoorCreationOptions();

            var parameters = new ElementCreationParams
            {
                ElementType = "Door",
                Parameters = new Dictionary<string, object>
                {
                    { "HostWallId", wallElementId },
                    { "PositionAlongWall", positionAlongWall },
                    { "Width", options.Width ?? DefaultSingleDoorWidth },
                    { "Height", options.Height ?? DefaultDoorHeight },
                    { "DoorType", options.DoorTypeName ?? "Single-Flush" },
                    { "HandSwing", options.HandSwing },
                    { "RequiresAccessibility", options.RequiresAccessibility },
                    { "FireRating", options.FireRating }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Creates an accessible door meeting ADA requirements.
        /// </summary>
        public async Task<CreationResult> CreateAccessibleDoorAsync(
            int wallElementId,
            double positionAlongWall,
            AccessibleDoorOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new AccessibleDoorOptions();

            Logger.Info($"Creating ADA-compliant door in wall {wallElementId}");

            var doorOptions = new DoorCreationOptions
            {
                Width = Math.Max(options.ClearWidth + 100, DefaultAccessibleDoorWidth), // Account for door frame
                Height = DefaultDoorHeight,
                DoorTypeName = options.AutomaticOpener ? "Automatic-Accessible" : "Single-Accessible",
                HandSwing = options.PreferredSwing,
                RequiresAccessibility = true,
                ThresholdHeight = options.MaxThresholdHeight,
                CloserPressure = options.MaxCloserPressure,
                HasLever = true // Required for accessibility
            };

            return await CreateInWallAsync(wallElementId, positionAlongWall, doorOptions, cancellationToken);
        }

        /// <summary>
        /// Creates a fire-rated door.
        /// </summary>
        public async Task<CreationResult> CreateFireDoorAsync(
            int wallElementId,
            double positionAlongWall,
            FireDoorOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new FireDoorOptions();

            Logger.Info($"Creating {options.FireRating}-minute fire-rated door in wall {wallElementId}");

            var doorOptions = new DoorCreationOptions
            {
                Width = options.Width ?? DefaultSingleDoorWidth,
                Height = options.Height ?? DefaultDoorHeight,
                DoorTypeName = $"Fire-Rated-{options.FireRating}min",
                FireRating = options.FireRating,
                SmokeSeals = true,
                AutomaticCloser = true,
                HasVisionPanel = options.HasVisionPanel
            };

            return await CreateInWallAsync(wallElementId, positionAlongWall, doorOptions, cancellationToken);
        }

        /// <summary>
        /// Creates multiple doors along a corridor.
        /// </summary>
        public async Task<BatchCreationResult> CreateCorridorDoorsAsync(
            int wallElementId,
            double wallLength,
            double spacing,
            DoorCreationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Creating corridor doors in wall {wallElementId}, length {wallLength}mm, spacing {spacing}mm");

            var results = new BatchCreationResult { StartTime = DateTime.Now };
            options ??= new DoorCreationOptions();

            // Calculate door positions (centered between spacing)
            var doorWidth = options.Width ?? DefaultSingleDoorWidth;
            var effectiveSpacing = spacing + doorWidth;
            var numDoors = (int)Math.Floor(wallLength / effectiveSpacing);

            // Start offset to center the door pattern
            var startOffset = (wallLength - (numDoors * effectiveSpacing - spacing)) / 2 + doorWidth / 2;

            for (int i = 0; i < numDoors; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var position = startOffset + (i * effectiveSpacing);
                var result = await CreateInWallAsync(wallElementId, position, options, cancellationToken);
                results.Results.Add(result);

                if (!result.Success)
                {
                    Logger.Warn($"Failed to create door {i + 1}: {result.Error}");
                }
            }

            results.EndTime = DateTime.Now;
            results.TotalCreated = results.Results.Count(r => r.Success);
            results.TotalFailed = results.Results.Count(r => !r.Success);

            return results;
        }

        /// <summary>
        /// Modifies an existing door's properties.
        /// </summary>
        public async Task<ModificationResult> ModifyAsync(
            int elementId,
            Dictionary<string, object> modifications,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                Logger.Info($"Modifying door {elementId}");

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
                                Logger.Debug($"Setting door width to {value}");
                                break;
                            case "HEIGHT":
                                Logger.Debug($"Setting door height to {value}");
                                break;
                            case "DOORTYPE":
                                Logger.Debug($"Changing door type to {value}");
                                break;
                            case "HANDSWING":
                                Logger.Debug($"Changing door swing to {value}");
                                break;
                            case "POSITION":
                                Logger.Debug($"Moving door to {value}");
                                break;
                        }
                    }

                    result.Success = true;
                    result.Message = $"Modified {modifications.Count} properties";
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to modify door {elementId}");
                    result.Success = false;
                    result.Error = ex.Message;
                }

                result.EndTime = DateTime.Now;
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Flips the door hand/swing direction.
        /// </summary>
        public async Task<ModificationResult> FlipHandAsync(
            int doorElementId,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Flipping door hand for element {doorElementId}");

            return await ModifyAsync(doorElementId, new Dictionary<string, object>
            {
                { "FlipHand", true }
            }, cancellationToken);
        }

        /// <summary>
        /// Flips the door facing direction.
        /// </summary>
        public async Task<ModificationResult> FlipFacingAsync(
            int doorElementId,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Flipping door facing for element {doorElementId}");

            return await ModifyAsync(doorElementId, new Dictionary<string, object>
            {
                { "FlipFacing", true }
            }, cancellationToken);
        }

        /// <summary>
        /// Gets available door types in the current document.
        /// </summary>
        public IEnumerable<DoorTypeInfo> GetAvailableDoorTypes()
        {
            // In real implementation, would query Revit document
            return new List<DoorTypeInfo>
            {
                new DoorTypeInfo { Id = 1, Name = "Single-Flush", Width = 900, Height = 2100, Leaves = 1 },
                new DoorTypeInfo { Id = 2, Name = "Single-Panel", Width = 900, Height = 2100, Leaves = 1 },
                new DoorTypeInfo { Id = 3, Name = "Double-Flush", Width = 1800, Height = 2100, Leaves = 2 },
                new DoorTypeInfo { Id = 4, Name = "Double-Panel", Width = 1800, Height = 2100, Leaves = 2 },
                new DoorTypeInfo { Id = 5, Name = "Single-Glass", Width = 900, Height = 2100, Leaves = 1 },
                new DoorTypeInfo { Id = 6, Name = "Sliding-Single", Width = 1200, Height = 2100, Leaves = 1, IsSliding = true },
                new DoorTypeInfo { Id = 7, Name = "Sliding-Double", Width = 2400, Height = 2100, Leaves = 2, IsSliding = true },
                new DoorTypeInfo { Id = 8, Name = "Single-Accessible", Width = 1000, Height = 2100, Leaves = 1, IsAccessible = true },
                new DoorTypeInfo { Id = 9, Name = "Fire-Rated-20min", Width = 900, Height = 2100, Leaves = 1, FireRating = 20 },
                new DoorTypeInfo { Id = 10, Name = "Fire-Rated-60min", Width = 900, Height = 2100, Leaves = 1, FireRating = 60 },
                new DoorTypeInfo { Id = 11, Name = "Fire-Rated-90min", Width = 900, Height = 2100, Leaves = 1, FireRating = 90 },
                new DoorTypeInfo { Id = 12, Name = "Automatic-Accessible", Width = 1200, Height = 2100, Leaves = 2, IsAccessible = true, IsAutomatic = true },
                new DoorTypeInfo { Id = 13, Name = "Revolving-3Wing", Width = 2400, Height = 2100, Leaves = 3, IsRevolving = true },
                new DoorTypeInfo { Id = 14, Name = "Pocket-Single", Width = 900, Height = 2100, Leaves = 1, IsPocket = true }
            };
        }

        #region Private Methods

        private ValidationResult ValidateParameters(ElementCreationParams parameters)
        {
            // Check for width
            if (parameters.Parameters.TryGetValue("Width", out var widthObj))
            {
                var width = Convert.ToDouble(widthObj);

                if (width < MinDoorWidth)
                {
                    return ValidationResult.Invalid($"Door width ({width}mm) is below minimum ({MinDoorWidth}mm)");
                }

                if (width > MaxDoorWidth)
                {
                    return ValidationResult.Invalid($"Door width ({width}mm) exceeds maximum ({MaxDoorWidth}mm)");
                }
            }

            // Check for host wall
            if (!parameters.Parameters.ContainsKey("HostWallId") &&
                !parameters.Parameters.ContainsKey("HostWall") &&
                parameters.AdditionalData == null)
            {
                return ValidationResult.Invalid("Door requires a host wall");
            }

            return ValidationResult.Valid();
        }

        private ValidationResult ValidateAccessibility(DoorParameters doorParams)
        {
            var result = new ValidationResult { IsValid = true };
            var warnings = new List<string>();

            // ADA minimum clear width: 32 inches (813mm)
            const double minAccessibleClearWidth = 813;
            var clearWidth = doorParams.Width - 100; // Approximate frame deduction

            if (clearWidth < minAccessibleClearWidth)
            {
                result.IsValid = false;
                result.Error = $"Door clear width ({clearWidth:F0}mm) is below ADA minimum ({minAccessibleClearWidth}mm)";
                return result;
            }

            // Check threshold height (max 13mm for ADA)
            if (doorParams.ThresholdHeight > 13)
            {
                warnings.Add($"Threshold height ({doorParams.ThresholdHeight}mm) exceeds ADA maximum (13mm)");
            }

            // Check closer pressure (max 5 lbf / 22N for interior doors)
            if (doorParams.CloserPressure > 22)
            {
                warnings.Add($"Door closer pressure ({doorParams.CloserPressure}N) exceeds ADA maximum (22N)");
            }

            // Check for lever handle
            if (!doorParams.HasLever)
            {
                warnings.Add("ADA requires lever or push-type hardware, not knobs");
            }

            result.Warnings = warnings;
            return result;
        }

        private DoorParameters ExtractDoorParameters(ElementCreationParams parameters)
        {
            return new DoorParameters
            {
                Width = GetDouble(parameters.Parameters, "Width", DefaultSingleDoorWidth),
                Height = GetDouble(parameters.Parameters, "Height", DefaultDoorHeight),
                DoorTypeName = GetString(parameters.Parameters, "DoorType", "Single-Flush"),
                HostWallId = GetInt(parameters.Parameters, "HostWallId", 0),
                PositionAlongWall = GetDouble(parameters.Parameters, "PositionAlongWall", 0),
                HandSwing = GetEnum(parameters.Parameters, "HandSwing", DoorHandSwing.RightHand),
                FireRating = GetDouble(parameters.Parameters, "FireRating", 0),
                RequiresAccessibility = GetBool(parameters.Parameters, "RequiresAccessibility", false),
                ThresholdHeight = GetDouble(parameters.Parameters, "ThresholdHeight", 10),
                CloserPressure = GetDouble(parameters.Parameters, "CloserPressure", 20),
                HasLever = GetBool(parameters.Parameters, "HasLever", true)
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

    public class DoorCreationOptions
    {
        public double? Width { get; set; }
        public double? Height { get; set; }
        public string DoorTypeName { get; set; }
        public DoorHandSwing HandSwing { get; set; } = DoorHandSwing.RightHand;
        public bool RequiresAccessibility { get; set; }
        public double FireRating { get; set; } // minutes
        public double? ThresholdHeight { get; set; }
        public double? CloserPressure { get; set; }
        public bool HasLever { get; set; } = true;
        public bool SmokeSeals { get; set; }
        public bool AutomaticCloser { get; set; }
        public bool HasVisionPanel { get; set; }
    }

    public class DoorParameters
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public string DoorTypeName { get; set; }
        public int HostWallId { get; set; }
        public double PositionAlongWall { get; set; }
        public DoorHandSwing HandSwing { get; set; }
        public double FireRating { get; set; }
        public bool RequiresAccessibility { get; set; }
        public double ThresholdHeight { get; set; }
        public double CloserPressure { get; set; }
        public bool HasLever { get; set; }
    }

    public class DoorTypeInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int Leaves { get; set; }
        public double FireRating { get; set; }
        public bool IsSliding { get; set; }
        public bool IsAccessible { get; set; }
        public bool IsAutomatic { get; set; }
        public bool IsRevolving { get; set; }
        public bool IsPocket { get; set; }
    }

    public class AccessibleDoorOptions
    {
        public double ClearWidth { get; set; } = 813; // ADA minimum
        public double MaxThresholdHeight { get; set; } = 13;
        public double MaxCloserPressure { get; set; } = 22; // Newtons
        public bool AutomaticOpener { get; set; }
        public DoorHandSwing PreferredSwing { get; set; } = DoorHandSwing.RightHand;
    }

    public class FireDoorOptions
    {
        public double? Width { get; set; }
        public double? Height { get; set; }
        public int FireRating { get; set; } = 60; // minutes
        public bool HasVisionPanel { get; set; }
        public bool IsEmergencyExit { get; set; }
    }

    public enum DoorHandSwing
    {
        LeftHand,
        RightHand,
        LeftHandReverse,
        RightHandReverse
    }

    #endregion
}
