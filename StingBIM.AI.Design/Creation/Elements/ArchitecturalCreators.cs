// ===================================================================
// StingBIM Architectural Element Creators
// StairCreator, RoofCreator, CeilingCreator, CurtainWallCreator
// Code-compliant with accessibility and safety requirements
// Aligned with MR_PARAMETERS.txt
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Elements
{
    #region Stair Creator

    /// <summary>
    /// Creates stairs with code-compliant dimensions, landings, and railings.
    /// </summary>
    public class StairCreator : IElementCreator
    {
        // ISO 19650 Parameter alignment
        private const string PARAM_STAIR_WIDTH = "MR_STAIR_WIDTH";
        private const string PARAM_STAIR_RISER_HEIGHT = "MR_STAIR_RISER_HEIGHT";
        private const string PARAM_STAIR_TREAD_DEPTH = "MR_STAIR_TREAD_DEPTH";
        private const string PARAM_STAIR_TOTAL_RISE = "MR_STAIR_TOTAL_RISE";
        private const string PARAM_STAIR_NUMBER_RISERS = "MR_STAIR_NUMBER_RISERS";
        private const string PARAM_STAIR_HEADROOM = "MR_STAIR_HEADROOM";

        // Building code requirements (IBC/BS)
        private static readonly Dictionary<string, StairCodeRequirements> CodeRequirements = new()
        {
            ["IBC_Commercial"] = new StairCodeRequirements { MinWidth = 1118, MinRiser = 102, MaxRiser = 178, MinTread = 279, MaxTread = 330, MinHeadroom = 2032, MinLandingDepth = 1118 },
            ["IBC_Residential"] = new StairCodeRequirements { MinWidth = 914, MinRiser = 102, MaxRiser = 196, MinTread = 254, MaxTread = 330, MinHeadroom = 1981, MinLandingDepth = 914 },
            ["IBC_Assembly"] = new StairCodeRequirements { MinWidth = 1422, MinRiser = 102, MaxRiser = 178, MinTread = 279, MaxTread = 330, MinHeadroom = 2032, MinLandingDepth = 1422 },
            ["BS_Commercial"] = new StairCodeRequirements { MinWidth = 1000, MinRiser = 150, MaxRiser = 170, MinTread = 250, MaxTread = 350, MinHeadroom = 2000, MinLandingDepth = 1200 },
            ["BS_Residential"] = new StairCodeRequirements { MinWidth = 800, MinRiser = 150, MaxRiser = 220, MinTread = 220, MaxTread = 350, MinHeadroom = 2000, MinLandingDepth = 800 },
            ["ADA_Accessible"] = new StairCodeRequirements { MinWidth = 1118, MinRiser = 102, MaxRiser = 178, MinTread = 279, MaxTread = 330, MinHeadroom = 2032, MinLandingDepth = 1524, RequiresHandrails = true }
        };

        // Comfort formula: 2R + T = 600-650mm (Blondel formula)
        private const double BLONDEL_MIN = 600;
        private const double BLONDEL_MAX = 650;

        public async Task<CreationResult> CreateAsync(
            ElementCreationParams parameters,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var result = new CreationResult
                {
                    ElementType = "Stairs",
                    StartTime = DateTime.Now
                };

                try
                {
                    var validation = ValidateParameters(parameters);
                    if (!validation.IsValid)
                    {
                        result.Success = false;
                        result.Error = validation.Error;
                        return result;
                    }

                    var stairParams = ExtractStairParameters(parameters);

                    // Calculate stair geometry
                    CalculateStairGeometry(stairParams);

                    // Validate against building codes
                    var codeCheck = ValidateAgainstCode(stairParams, parameters);
                    if (!codeCheck.IsValid)
                    {
                        result.Success = false;
                        result.Error = codeCheck.Error;
                        return result;
                    }

                    result.Success = true;
                    result.CreatedElementId = GenerateElementId();
                    result.Parameters = stairParams;
                    result.Message = $"Created {stairParams.StairType} stairs ({stairParams.NumberOfRisers} risers)";

                    // ISO 19650 parameter mapping
                    result.Metadata[PARAM_STAIR_WIDTH] = stairParams.Width;
                    result.Metadata[PARAM_STAIR_RISER_HEIGHT] = stairParams.RiserHeight;
                    result.Metadata[PARAM_STAIR_TREAD_DEPTH] = stairParams.TreadDepth;
                    result.Metadata[PARAM_STAIR_TOTAL_RISE] = stairParams.TotalRise;
                    result.Metadata[PARAM_STAIR_NUMBER_RISERS] = stairParams.NumberOfRisers;
                    result.Metadata[PARAM_STAIR_HEADROOM] = stairParams.Headroom;
                    result.Metadata["BlondelFormula"] = stairParams.BlondelValue;
                    result.Metadata["LandingRequired"] = stairParams.RequiresLanding;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                result.EndTime = DateTime.Now;
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Creates a stair by specifying floor-to-floor height.
        /// </summary>
        public async Task<CreationResult> CreateByFloorHeightAsync(
            double floorToFloorHeight,
            Point3D basePoint,
            StairPlacementOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new StairPlacementOptions();

            // Calculate optimal riser/tread for comfort
            var geometry = CalculateOptimalGeometry(floorToFloorHeight, options.BuildingCode);

            var parameters = new ElementCreationParams
            {
                ElementType = "Stairs",
                Parameters = new Dictionary<string, object>
                {
                    { "BasePoint", basePoint },
                    { "TotalRise", floorToFloorHeight },
                    { "Width", options.Width ?? geometry.RecommendedWidth },
                    { "RiserHeight", geometry.RiserHeight },
                    { "TreadDepth", geometry.TreadDepth },
                    { "NumberOfRisers", geometry.NumberOfRisers },
                    { "StairType", options.StairType ?? "Straight" },
                    { "BuildingCode", options.BuildingCode },
                    { "IncludeRailings", options.IncludeRailings },
                    { "IncludeLandings", geometry.RequiresLanding }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Creates a multi-run stair with landings.
        /// </summary>
        public async Task<BatchCreationResult> CreateMultiRunAsync(
            double totalRise,
            int numberOfRuns,
            Point3D basePoint,
            StairMultiRunOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new StairMultiRunOptions();
            var results = new BatchCreationResult { StartTime = DateTime.Now };

            double risePerRun = totalRise / numberOfRuns;
            var currentBase = basePoint;
            double runAngle = 0;

            for (int i = 0; i < numberOfRuns; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var runOptions = new StairPlacementOptions
                {
                    Width = options.Width,
                    BuildingCode = options.BuildingCode,
                    IncludeRailings = options.IncludeRailings,
                    StairType = "Straight"
                };

                var result = await CreateByFloorHeightAsync(risePerRun, currentBase, runOptions, cancellationToken);
                results.Results.Add(result);

                // Calculate next run start point
                if (result.Success && i < numberOfRuns - 1)
                {
                    // Add landing
                    runAngle = options.TurnDirection == TurnDirection.Left ? runAngle - 90 : runAngle + 90;
                    currentBase = CalculateNextRunStart(currentBase, result.Parameters as StairParameters, options.LandingDepth, runAngle);

                    // Create landing
                    var landingResult = await CreateLandingAsync(currentBase, options.Width ?? 1100, options.LandingDepth, cancellationToken);
                    results.Results.Add(landingResult);
                }
            }

            results.EndTime = DateTime.Now;
            results.TotalCreated = results.Results.Count(r => r.Success);
            results.TotalFailed = results.Results.Count(r => !r.Success);

            return results;
        }

        /// <summary>
        /// Creates a spiral stair.
        /// </summary>
        public async Task<CreationResult> CreateSpiralAsync(
            double totalRise,
            double centerRadius,
            Point3D centerPoint,
            SpiralStairOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new SpiralStairOptions();

            var geometry = CalculateOptimalGeometry(totalRise, options.BuildingCode ?? "IBC_Residential");
            double anglePerStep = 360.0 / geometry.NumberOfRisers * (options.Clockwise ? -1 : 1);

            var parameters = new ElementCreationParams
            {
                ElementType = "Stairs",
                Parameters = new Dictionary<string, object>
                {
                    { "CenterPoint", centerPoint },
                    { "TotalRise", totalRise },
                    { "CenterRadius", centerRadius },
                    { "OuterRadius", centerRadius + (options.Width ?? 900) },
                    { "RotationAngle", geometry.NumberOfRisers * Math.Abs(anglePerStep) },
                    { "StairType", "Spiral" },
                    { "NumberOfRisers", geometry.NumberOfRisers },
                    { "RiserHeight", geometry.RiserHeight },
                    { "Clockwise", options.Clockwise },
                    { "BuildingCode", options.BuildingCode }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        private async Task<CreationResult> CreateLandingAsync(
            Point3D location,
            double width,
            double depth,
            CancellationToken cancellationToken)
        {
            var parameters = new ElementCreationParams
            {
                ElementType = "StairLanding",
                Parameters = new Dictionary<string, object>
                {
                    { "Location", location },
                    { "Width", width },
                    { "Depth", depth }
                }
            };

            return await Task.FromResult(new CreationResult
            {
                Success = true,
                CreatedElementId = GenerateElementId(),
                ElementType = "StairLanding",
                Message = $"Created landing {width}x{depth}mm"
            });
        }

        public IEnumerable<StairCodeRequirements> GetCodeRequirements()
        {
            return CodeRequirements.Select(kvp => new StairCodeRequirements
            {
                Code = kvp.Key,
                MinWidth = kvp.Value.MinWidth,
                MinRiser = kvp.Value.MinRiser,
                MaxRiser = kvp.Value.MaxRiser,
                MinTread = kvp.Value.MinTread,
                MaxTread = kvp.Value.MaxTread,
                MinHeadroom = kvp.Value.MinHeadroom,
                MinLandingDepth = kvp.Value.MinLandingDepth
            });
        }

        #region Private Methods

        private ValidationResult ValidateParameters(ElementCreationParams parameters)
        {
            if (parameters.Parameters.TryGetValue("TotalRise", out var riseObj))
            {
                var rise = Convert.ToDouble(riseObj);
                if (rise < 200 || rise > 20000)
                    return ValidationResult.Invalid($"Total rise {rise}mm outside practical range");
            }

            return ValidationResult.Valid();
        }

        private StairParameters ExtractStairParameters(ElementCreationParams parameters)
        {
            return new StairParameters
            {
                StairType = GetString(parameters.Parameters, "StairType", "Straight"),
                Width = GetDouble(parameters.Parameters, "Width", 1100),
                TotalRise = GetDouble(parameters.Parameters, "TotalRise", 3000),
                RiserHeight = GetDouble(parameters.Parameters, "RiserHeight", 0),
                TreadDepth = GetDouble(parameters.Parameters, "TreadDepth", 0),
                NumberOfRisers = GetInt(parameters.Parameters, "NumberOfRisers", 0),
                IncludeRailings = GetBool(parameters.Parameters, "IncludeRailings", true)
            };
        }

        private void CalculateStairGeometry(StairParameters stairParams)
        {
            if (stairParams.RiserHeight == 0)
            {
                // Calculate optimal riser height (target ~170mm)
                int numRisers = (int)Math.Round(stairParams.TotalRise / 170);
                numRisers = Math.Max(2, numRisers);
                stairParams.RiserHeight = stairParams.TotalRise / numRisers;
                stairParams.NumberOfRisers = numRisers;
            }

            if (stairParams.TreadDepth == 0)
            {
                // Use Blondel formula: 2R + T = 630 (optimal)
                stairParams.TreadDepth = 630 - 2 * stairParams.RiserHeight;
                stairParams.TreadDepth = Math.Max(250, Math.Min(350, stairParams.TreadDepth));
            }

            stairParams.BlondelValue = 2 * stairParams.RiserHeight + stairParams.TreadDepth;
            stairParams.TotalRun = (stairParams.NumberOfRisers - 1) * stairParams.TreadDepth;
            stairParams.RequiresLanding = stairParams.TotalRise > 3700; // IBC: landing every 12 feet
            stairParams.Headroom = 2100;
        }

        private StairGeometry CalculateOptimalGeometry(double totalRise, string buildingCode)
        {
            var code = CodeRequirements.GetValueOrDefault(buildingCode ?? "IBC_Commercial", CodeRequirements["IBC_Commercial"]);

            // Find optimal riser in code range
            double targetRiser = (code.MinRiser + code.MaxRiser) / 2;
            int numRisers = (int)Math.Round(totalRise / targetRiser);
            numRisers = Math.Max(2, numRisers);
            double actualRiser = totalRise / numRisers;

            // Ensure within code limits
            if (actualRiser > code.MaxRiser)
            {
                numRisers = (int)Math.Ceiling(totalRise / code.MaxRiser);
                actualRiser = totalRise / numRisers;
            }
            else if (actualRiser < code.MinRiser)
            {
                numRisers = (int)Math.Floor(totalRise / code.MinRiser);
                actualRiser = totalRise / numRisers;
            }

            // Calculate tread using Blondel
            double tread = 630 - 2 * actualRiser;
            tread = Math.Max(code.MinTread, Math.Min(code.MaxTread, tread));

            return new StairGeometry
            {
                RiserHeight = actualRiser,
                TreadDepth = tread,
                NumberOfRisers = numRisers,
                RecommendedWidth = code.MinWidth,
                RequiresLanding = totalRise > 3700
            };
        }

        private ValidationResult ValidateAgainstCode(StairParameters stairParams, ElementCreationParams parameters)
        {
            var codeType = GetString(parameters.Parameters, "BuildingCode", "IBC_Commercial");
            var code = CodeRequirements.GetValueOrDefault(codeType, CodeRequirements["IBC_Commercial"]);

            if (stairParams.RiserHeight < code.MinRiser)
                return ValidationResult.Invalid($"Riser {stairParams.RiserHeight}mm below minimum {code.MinRiser}mm");
            if (stairParams.RiserHeight > code.MaxRiser)
                return ValidationResult.Invalid($"Riser {stairParams.RiserHeight}mm exceeds maximum {code.MaxRiser}mm");
            if (stairParams.TreadDepth < code.MinTread)
                return ValidationResult.Invalid($"Tread {stairParams.TreadDepth}mm below minimum {code.MinTread}mm");
            if (stairParams.Width < code.MinWidth)
                return ValidationResult.Invalid($"Width {stairParams.Width}mm below minimum {code.MinWidth}mm");

            return ValidationResult.Valid();
        }

        private Point3D CalculateNextRunStart(Point3D current, StairParameters stairParams, double landingDepth, double angle)
        {
            double radians = angle * Math.PI / 180;
            return new Point3D(
                current.X + (stairParams.TotalRun + landingDepth) * Math.Cos(radians),
                current.Y + (stairParams.TotalRun + landingDepth) * Math.Sin(radians),
                current.Z + stairParams.TotalRise
            );
        }

        private double GetDouble(Dictionary<string, object> dict, string key, double defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is double d) return d;
                if (double.TryParse(value?.ToString(), out d)) return d;
            }
            return defaultValue;
        }

        private string GetString(Dictionary<string, object> dict, string key, string defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
                return value?.ToString() ?? defaultValue;
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

        private bool GetBool(Dictionary<string, object> dict, string key, bool defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is bool b) return b;
                if (bool.TryParse(value?.ToString(), out b)) return b;
            }
            return defaultValue;
        }

        private int GenerateElementId() => new Random().Next(100000, 999999);

        #endregion
    }

    #endregion

    #region Roof Creator

    /// <summary>
    /// Creates roofs with slope, drainage, and insulation specifications.
    /// </summary>
    public class RoofCreator : IElementCreator
    {
        // ISO 19650 Parameter alignment
        private const string PARAM_ROOF_SLOPE = "MR_ROOF_SLOPE";
        private const string PARAM_ROOF_AREA = "MR_ROOF_AREA";
        private const string PARAM_ROOF_TYPE = "MR_ROOF_TYPE";
        private const string PARAM_ROOF_INSULATION = "MR_ROOF_INSULATION";
        private const string PARAM_ROOF_MATERIAL = "MR_ROOF_MATERIAL";
        private const string PARAM_ROOF_U_VALUE = "MR_ROOF_U_VALUE";

        private static readonly Dictionary<string, RoofTypeDefault> StandardRoofs = new()
        {
            ["Flat"] = new RoofTypeDefault { MinSlope = 1.5, MaxSlope = 3, InsulationR = 6.0 },
            ["LowSlope"] = new RoofTypeDefault { MinSlope = 3, MaxSlope = 15, InsulationR = 6.0 },
            ["PitchedGable"] = new RoofTypeDefault { MinSlope = 15, MaxSlope = 45, InsulationR = 6.5 },
            ["PitchedHip"] = new RoofTypeDefault { MinSlope = 15, MaxSlope = 45, InsulationR = 6.5 },
            ["Mansard"] = new RoofTypeDefault { MinSlope = 30, MaxSlope = 70, InsulationR = 6.5 },
            ["Butterfly"] = new RoofTypeDefault { MinSlope = 5, MaxSlope = 25, InsulationR = 6.0 },
            ["Shed"] = new RoofTypeDefault { MinSlope = 5, MaxSlope = 30, InsulationR = 6.0 },
            ["GreenRoof"] = new RoofTypeDefault { MinSlope = 0, MaxSlope = 35, InsulationR = 4.0 },
            ["MetalStanding"] = new RoofTypeDefault { MinSlope = 3, MaxSlope = 90, InsulationR = 5.5 }
        };

        public async Task<CreationResult> CreateAsync(
            ElementCreationParams parameters,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var result = new CreationResult
                {
                    ElementType = "Roof",
                    StartTime = DateTime.Now
                };

                try
                {
                    var roofParams = ExtractRoofParameters(parameters);

                    // Calculate drainage
                    var drainage = CalculateDrainage(roofParams);

                    // Calculate insulation requirements
                    var insulation = CalculateInsulation(roofParams, parameters);

                    result.Success = true;
                    result.CreatedElementId = GenerateElementId();
                    result.Parameters = roofParams;
                    result.Message = $"Created {roofParams.RoofType} roof ({roofParams.Area}m² at {roofParams.Slope}° slope)";

                    result.Metadata[PARAM_ROOF_SLOPE] = roofParams.Slope;
                    result.Metadata[PARAM_ROOF_AREA] = roofParams.Area;
                    result.Metadata[PARAM_ROOF_TYPE] = roofParams.RoofType;
                    result.Metadata[PARAM_ROOF_INSULATION] = insulation.RValue;
                    result.Metadata[PARAM_ROOF_MATERIAL] = roofParams.Material;
                    result.Metadata[PARAM_ROOF_U_VALUE] = insulation.UValue;
                    result.Metadata["DrainagePoints"] = drainage.NumberOfDrains;
                    result.Metadata["GutterSize"] = drainage.GutterSize;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                result.EndTime = DateTime.Now;
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Creates a roof by footprint with automatic slope.
        /// </summary>
        public async Task<CreationResult> CreateByFootprintAsync(
            List<Point3D> footprintPoints,
            int levelId,
            RoofPlacementOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new RoofPlacementOptions();

            var parameters = new ElementCreationParams
            {
                ElementType = "Roof",
                Parameters = new Dictionary<string, object>
                {
                    { "FootprintPoints", footprintPoints },
                    { "Level", levelId },
                    { "RoofType", options.RoofType ?? "PitchedGable" },
                    { "Slope", options.Slope },
                    { "RidgeDirection", options.RidgeDirection },
                    { "Overhang", options.Overhang ?? 600 },
                    { "Material", options.Material ?? "AsphaltShingle" },
                    { "ClimateZone", options.ClimateZone }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Creates a gable roof with specified ridge line.
        /// </summary>
        public async Task<CreationResult> CreateGableRoofAsync(
            List<Point3D> footprint,
            Point3D ridgeStart,
            Point3D ridgeEnd,
            double slope,
            RoofPlacementOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new RoofPlacementOptions();
            options.RoofType = "PitchedGable";
            options.Slope = slope;

            var parameters = new ElementCreationParams
            {
                ElementType = "Roof",
                Parameters = new Dictionary<string, object>
                {
                    { "FootprintPoints", footprint },
                    { "RidgeStart", ridgeStart },
                    { "RidgeEnd", ridgeEnd },
                    { "Slope", slope },
                    { "RoofType", "PitchedGable" },
                    { "Overhang", options.Overhang ?? 600 },
                    { "Material", options.Material ?? "AsphaltShingle" }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Recommends roof slope based on material and climate.
        /// </summary>
        public double RecommendSlope(string roofMaterial, string climateZone, double annualRainfall)
        {
            double baseSlope = roofMaterial.ToLower() switch
            {
                "metal" or "metalstanding" => 5,
                "tile" or "clay" => 25,
                "asphalt" or "asphaltshingle" => 20,
                "slate" => 30,
                "membrane" or "flat" => 2,
                "greenroof" => 3,
                _ => 15
            };

            // Adjust for rainfall
            if (annualRainfall > 2000) baseSlope += 5;
            else if (annualRainfall > 1000) baseSlope += 2;

            // Adjust for snow loads
            if (climateZone == "Cold" || climateZone == "VeryCold")
                baseSlope = Math.Max(baseSlope, 25);

            return baseSlope;
        }

        #region Private Methods

        private RoofParameters ExtractRoofParameters(ElementCreationParams parameters)
        {
            var roofType = GetString(parameters.Parameters, "RoofType", "PitchedGable");
            var defaults = StandardRoofs.GetValueOrDefault(roofType, StandardRoofs["PitchedGable"]);

            var slope = GetDouble(parameters.Parameters, "Slope", 0);
            if (slope == 0) slope = (defaults.MinSlope + defaults.MaxSlope) / 2;

            return new RoofParameters
            {
                RoofType = roofType,
                Slope = slope,
                Overhang = GetDouble(parameters.Parameters, "Overhang", 600),
                Material = GetString(parameters.Parameters, "Material", "AsphaltShingle"),
                Area = GetDouble(parameters.Parameters, "Area", 0),
                InsulationRValue = defaults.InsulationR
            };
        }

        private DrainageResult CalculateDrainage(RoofParameters roofParams)
        {
            // One drain per 150m² typically
            int drains = Math.Max(2, (int)Math.Ceiling(roofParams.Area / 150));

            // Gutter size based on area
            double gutterSize = roofParams.Area switch
            {
                < 50 => 100,
                < 100 => 125,
                < 200 => 150,
                _ => 200
            };

            return new DrainageResult
            {
                NumberOfDrains = drains,
                GutterSize = gutterSize,
                DownpipeSize = gutterSize * 0.75
            };
        }

        private InsulationResult CalculateInsulation(RoofParameters roofParams, ElementCreationParams parameters)
        {
            var climateZone = GetString(parameters.Parameters, "ClimateZone", "Temperate");

            double requiredR = climateZone switch
            {
                "Tropical" => 4.0,
                "Subtropical" => 4.5,
                "Temperate" => 6.0,
                "Cold" => 8.0,
                "VeryCold" => 10.0,
                _ => 6.0
            };

            return new InsulationResult
            {
                RValue = requiredR,
                UValue = 1 / requiredR,
                Thickness = requiredR * 25 // Approximate mm
            };
        }

        private double GetDouble(Dictionary<string, object> dict, string key, double defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is double d) return d;
                if (double.TryParse(value?.ToString(), out d)) return d;
            }
            return defaultValue;
        }

        private string GetString(Dictionary<string, object> dict, string key, string defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
                return value?.ToString() ?? defaultValue;
            return defaultValue;
        }

        private int GenerateElementId() => new Random().Next(100000, 999999);

        #endregion
    }

    #endregion

    #region Ceiling Creator

    /// <summary>
    /// Creates ceilings with grid systems, acoustics, and plenum considerations.
    /// </summary>
    public class CeilingCreator : IElementCreator
    {
        private const string PARAM_CEILING_HEIGHT = "MR_CEILING_HEIGHT";
        private const string PARAM_CEILING_TYPE = "MR_CEILING_TYPE";
        private const string PARAM_CEILING_ACOUSTIC_RATING = "MR_CEILING_ACOUSTIC_RATING";
        private const string PARAM_CEILING_FIRE_RATING = "MR_CEILING_FIRE_RATING";

        private static readonly Dictionary<string, CeilingTypeDefault> StandardCeilings = new()
        {
            ["ACT600"] = new CeilingTypeDefault { GridSize = 600, TileSize = 600, AcousticNRC = 0.55, FireRating = "Class A" },
            ["ACT1200x600"] = new CeilingTypeDefault { GridSize = 600, TileSize = 1200, AcousticNRC = 0.55, FireRating = "Class A" },
            ["GWB"] = new CeilingTypeDefault { GridSize = 0, TileSize = 0, AcousticNRC = 0.05, FireRating = "1HR" },
            ["GWBAcoustic"] = new CeilingTypeDefault { GridSize = 0, TileSize = 0, AcousticNRC = 0.70, FireRating = "1HR" },
            ["MetalPan"] = new CeilingTypeDefault { GridSize = 600, TileSize = 600, AcousticNRC = 0.45, FireRating = "Class A" },
            ["WoodSlat"] = new CeilingTypeDefault { GridSize = 0, TileSize = 0, AcousticNRC = 0.35, FireRating = "Class B" },
            ["Exposed"] = new CeilingTypeDefault { GridSize = 0, TileSize = 0, AcousticNRC = 0.15, FireRating = "N/A" },
            ["StretchFabric"] = new CeilingTypeDefault { GridSize = 0, TileSize = 0, AcousticNRC = 0.60, FireRating = "Class A" }
        };

        public async Task<CreationResult> CreateAsync(
            ElementCreationParams parameters,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var result = new CreationResult
                {
                    ElementType = "Ceiling",
                    StartTime = DateTime.Now
                };

                try
                {
                    var ceilingParams = ExtractCeilingParameters(parameters);

                    // Calculate plenum height
                    ceilingParams.PlenumHeight = CalculatePlenumHeight(ceilingParams, parameters);

                    // Determine grid layout
                    var gridLayout = CalculateGridLayout(ceilingParams, parameters);

                    result.Success = true;
                    result.CreatedElementId = GenerateElementId();
                    result.Parameters = ceilingParams;
                    result.Message = $"Created {ceilingParams.CeilingType} ceiling at {ceilingParams.Height}mm";

                    result.Metadata[PARAM_CEILING_HEIGHT] = ceilingParams.Height;
                    result.Metadata[PARAM_CEILING_TYPE] = ceilingParams.CeilingType;
                    result.Metadata[PARAM_CEILING_ACOUSTIC_RATING] = ceilingParams.AcousticNRC;
                    result.Metadata[PARAM_CEILING_FIRE_RATING] = ceilingParams.FireRating;
                    result.Metadata["PlenumHeight"] = ceilingParams.PlenumHeight;
                    result.Metadata["GridTiles"] = gridLayout.TotalTiles;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                result.EndTime = DateTime.Now;
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Creates a ceiling in a room with automatic height calculation.
        /// </summary>
        public async Task<CreationResult> CreateInRoomAsync(
            int roomId,
            CeilingPlacementOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new CeilingPlacementOptions();

            var parameters = new ElementCreationParams
            {
                ElementType = "Ceiling",
                Parameters = new Dictionary<string, object>
                {
                    { "RoomId", roomId },
                    { "CeilingType", options.CeilingType ?? "ACT600" },
                    { "Height", options.Height },
                    { "HeightOffset", options.HeightOffset ?? 0 },
                    { "RoomFunction", options.RoomFunction }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Recommends ceiling type based on room function.
        /// </summary>
        public string RecommendCeilingType(string roomFunction, bool requiresAcoustic = false, bool requiresFireRating = false)
        {
            var function = roomFunction?.ToLower() ?? "";

            if (function.Contains("conference") || function.Contains("theater") || function.Contains("auditorium"))
                return requiresFireRating ? "GWBAcoustic" : "ACT600";

            if (function.Contains("office") || function.Contains("classroom"))
                return "ACT600";

            if (function.Contains("corridor") || function.Contains("lobby"))
                return "ACT1200x600";

            if (function.Contains("mechanical") || function.Contains("warehouse"))
                return "Exposed";

            if (function.Contains("restaurant") || function.Contains("retail"))
                return "MetalPan";

            return requiresAcoustic ? "GWBAcoustic" : "ACT600";
        }

        #region Private Methods

        private CeilingParameters ExtractCeilingParameters(ElementCreationParams parameters)
        {
            var ceilingType = GetString(parameters.Parameters, "CeilingType", "ACT600");
            var defaults = StandardCeilings.GetValueOrDefault(ceilingType, StandardCeilings["ACT600"]);

            return new CeilingParameters
            {
                CeilingType = ceilingType,
                Height = GetDouble(parameters.Parameters, "Height", 2700),
                GridSize = defaults.GridSize,
                TileSize = defaults.TileSize,
                AcousticNRC = defaults.AcousticNRC,
                FireRating = defaults.FireRating
            };
        }

        private double CalculatePlenumHeight(CeilingParameters ceilingParams, ElementCreationParams parameters)
        {
            double floorHeight = GetDouble(parameters.Parameters, "FloorToFloorHeight", 3600);
            return floorHeight - ceilingParams.Height - 200; // 200mm for structure
        }

        private CeilingGridLayout CalculateGridLayout(CeilingParameters ceilingParams, ElementCreationParams parameters)
        {
            if (ceilingParams.GridSize == 0)
                return new CeilingGridLayout { TotalTiles = 0 };

            double roomWidth = GetDouble(parameters.Parameters, "RoomWidth", 6000);
            double roomLength = GetDouble(parameters.Parameters, "RoomLength", 8000);

            int tilesX = (int)Math.Ceiling(roomWidth / ceilingParams.GridSize);
            int tilesY = (int)Math.Ceiling(roomLength / ceilingParams.GridSize);

            return new CeilingGridLayout
            {
                TilesX = tilesX,
                TilesY = tilesY,
                TotalTiles = tilesX * tilesY,
                BorderWidth = (roomWidth - tilesX * ceilingParams.GridSize) / 2 + ceilingParams.GridSize,
                BorderLength = (roomLength - tilesY * ceilingParams.GridSize) / 2 + ceilingParams.GridSize
            };
        }

        private double GetDouble(Dictionary<string, object> dict, string key, double defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is double d) return d;
                if (double.TryParse(value?.ToString(), out d)) return d;
            }
            return defaultValue;
        }

        private string GetString(Dictionary<string, object> dict, string key, string defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
                return value?.ToString() ?? defaultValue;
            return defaultValue;
        }

        private int GenerateElementId() => new Random().Next(100000, 999999);

        #endregion
    }

    #endregion

    #region Curtain Wall Creator

    /// <summary>
    /// Creates curtain wall systems with mullions, panels, and thermal analysis.
    /// </summary>
    public class CurtainWallCreator : IElementCreator
    {
        private const string PARAM_CW_HEIGHT = "MR_CURTAINWALL_HEIGHT";
        private const string PARAM_CW_WIDTH = "MR_CURTAINWALL_WIDTH";
        private const string PARAM_CW_U_VALUE = "MR_CURTAINWALL_U_VALUE";
        private const string PARAM_CW_SHGC = "MR_CURTAINWALL_SHGC";
        private const string PARAM_CW_MULLION_SIZE = "MR_CURTAINWALL_MULLION";

        private static readonly Dictionary<string, CurtainWallSystemDefault> StandardSystems = new()
        {
            ["StickBuilt"] = new CurtainWallSystemDefault { MullionWidth = 50, MullionDepth = 150, GridX = 1500, GridY = 3600, UValue = 2.5 },
            ["Unitized"] = new CurtainWallSystemDefault { MullionWidth = 65, MullionDepth = 180, GridX = 1500, GridY = 3600, UValue = 2.0 },
            ["PointSupported"] = new CurtainWallSystemDefault { MullionWidth = 0, MullionDepth = 0, GridX = 1500, GridY = 1500, UValue = 2.8 },
            ["CableNet"] = new CurtainWallSystemDefault { MullionWidth = 20, MullionDepth = 20, GridX = 2000, GridY = 2000, UValue = 2.8 },
            ["DoubleSkidFacade"] = new CurtainWallSystemDefault { MullionWidth = 50, MullionDepth = 600, GridX = 1500, GridY = 3600, UValue = 1.2 },
            ["VentilatedFacade"] = new CurtainWallSystemDefault { MullionWidth = 75, MullionDepth = 200, GridX = 600, GridY = 1200, UValue = 1.5 }
        };

        public async Task<CreationResult> CreateAsync(
            ElementCreationParams parameters,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var result = new CreationResult
                {
                    ElementType = "CurtainWall",
                    StartTime = DateTime.Now
                };

                try
                {
                    var cwParams = ExtractCurtainWallParameters(parameters);

                    // Calculate panel layout
                    var layout = CalculatePanelLayout(cwParams);

                    // Calculate thermal performance
                    var thermal = CalculateThermalPerformance(cwParams, parameters);

                    result.Success = true;
                    result.CreatedElementId = GenerateElementId();
                    result.Parameters = cwParams;
                    result.Message = $"Created {cwParams.SystemType} curtain wall ({layout.TotalPanels} panels)";

                    result.Metadata[PARAM_CW_HEIGHT] = cwParams.Height;
                    result.Metadata[PARAM_CW_WIDTH] = cwParams.Width;
                    result.Metadata[PARAM_CW_U_VALUE] = thermal.OverallUValue;
                    result.Metadata[PARAM_CW_SHGC] = thermal.SHGC;
                    result.Metadata[PARAM_CW_MULLION_SIZE] = $"{cwParams.MullionWidth}x{cwParams.MullionDepth}";
                    result.Metadata["TotalPanels"] = layout.TotalPanels;
                    result.Metadata["GlazingArea"] = layout.GlazingArea;
                    result.Metadata["SpandrelArea"] = layout.SpandrelArea;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                result.EndTime = DateTime.Now;
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Creates a curtain wall by curve.
        /// </summary>
        public async Task<CreationResult> CreateByCurveAsync(
            Point3D startPoint,
            Point3D endPoint,
            double height,
            CurtainWallPlacementOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new CurtainWallPlacementOptions();

            var parameters = new ElementCreationParams
            {
                ElementType = "CurtainWall",
                Parameters = new Dictionary<string, object>
                {
                    { "StartPoint", startPoint },
                    { "EndPoint", endPoint },
                    { "Height", height },
                    { "SystemType", options.SystemType ?? "StickBuilt" },
                    { "GridX", options.GridX },
                    { "GridY", options.GridY },
                    { "GlazingType", options.GlazingType ?? "DoubleIGU" },
                    { "SpandrelType", options.SpandrelType ?? "InsulatedMetal" },
                    { "SpandrelRatio", options.SpandrelRatio ?? 0.3 }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Calculates overall facade performance.
        /// </summary>
        public FacadePerformance CalculateFacadePerformance(
            double totalArea, double glazingRatio, double glazingUValue, double glazingSHGC,
            double spandrelUValue, string orientation)
        {
            double glazingArea = totalArea * glazingRatio;
            double spandrelArea = totalArea * (1 - glazingRatio);

            double overallU = (glazingArea * glazingUValue + spandrelArea * spandrelUValue) / totalArea;
            double overallSHGC = glazingSHGC * glazingRatio;

            // Solar load estimate by orientation
            double solarIntensity = orientation?.ToUpper() switch
            {
                "S" or "SOUTH" => 800,
                "E" or "EAST" or "W" or "WEST" => 600,
                "N" or "NORTH" => 200,
                _ => 500
            };

            return new FacadePerformance
            {
                OverallUValue = overallU,
                OverallSHGC = overallSHGC,
                GlazingRatio = glazingRatio,
                AnnualHeatGain = glazingArea * overallSHGC * solarIntensity * 2000 / 1000, // kWh rough estimate
                AnnualHeatLoss = totalArea * overallU * 3000 / 1000 // kWh rough estimate
            };
        }

        #region Private Methods

        private CurtainWallParameters ExtractCurtainWallParameters(ElementCreationParams parameters)
        {
            var systemType = GetString(parameters.Parameters, "SystemType", "StickBuilt");
            var defaults = StandardSystems.GetValueOrDefault(systemType, StandardSystems["StickBuilt"]);

            return new CurtainWallParameters
            {
                SystemType = systemType,
                Width = GetDouble(parameters.Parameters, "Width", 10000),
                Height = GetDouble(parameters.Parameters, "Height", 3600),
                GridX = GetDouble(parameters.Parameters, "GridX", defaults.GridX),
                GridY = GetDouble(parameters.Parameters, "GridY", defaults.GridY),
                MullionWidth = defaults.MullionWidth,
                MullionDepth = defaults.MullionDepth,
                GlazingType = GetString(parameters.Parameters, "GlazingType", "DoubleIGU"),
                SpandrelRatio = GetDouble(parameters.Parameters, "SpandrelRatio", 0.3)
            };
        }

        private CurtainWallLayout CalculatePanelLayout(CurtainWallParameters cwParams)
        {
            int panelsX = cwParams.GridX > 0 ? (int)Math.Ceiling(cwParams.Width / cwParams.GridX) : 1;
            int panelsY = cwParams.GridY > 0 ? (int)Math.Ceiling(cwParams.Height / cwParams.GridY) : 1;
            double totalArea = cwParams.Width * cwParams.Height / 1000000;

            return new CurtainWallLayout
            {
                PanelsX = panelsX,
                PanelsY = panelsY,
                TotalPanels = panelsX * panelsY,
                GlazingArea = totalArea * (1 - cwParams.SpandrelRatio),
                SpandrelArea = totalArea * cwParams.SpandrelRatio
            };
        }

        private ThermalResult CalculateThermalPerformance(CurtainWallParameters cwParams, ElementCreationParams parameters)
        {
            double glazingU = cwParams.GlazingType switch
            {
                "SingleGlass" => 5.8,
                "DoubleIGU" => 2.8,
                "DoubleLowE" => 1.8,
                "TripleLowE" => 1.0,
                _ => 2.5
            };

            double spandrelU = 0.8; // Typical insulated spandrel
            double glazingRatio = 1 - cwParams.SpandrelRatio;

            return new ThermalResult
            {
                OverallUValue = glazingU * glazingRatio + spandrelU * cwParams.SpandrelRatio,
                SHGC = 0.35 * glazingRatio
            };
        }

        private double GetDouble(Dictionary<string, object> dict, string key, double defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is double d) return d;
                if (double.TryParse(value?.ToString(), out d)) return d;
            }
            return defaultValue;
        }

        private string GetString(Dictionary<string, object> dict, string key, string defaultValue)
        {
            if (dict.TryGetValue(key, out var value))
                return value?.ToString() ?? defaultValue;
            return defaultValue;
        }

        private int GenerateElementId() => new Random().Next(100000, 999999);

        #endregion
    }

    #endregion

    #region Supporting Classes for All Creators

    // Stair classes
    public class StairParameters
    {
        public string StairType { get; set; }
        public double Width { get; set; }
        public double TotalRise { get; set; }
        public double TotalRun { get; set; }
        public double RiserHeight { get; set; }
        public double TreadDepth { get; set; }
        public int NumberOfRisers { get; set; }
        public double BlondelValue { get; set; }
        public double Headroom { get; set; }
        public bool RequiresLanding { get; set; }
        public bool IncludeRailings { get; set; }
    }

    public class StairCodeRequirements
    {
        public string Code { get; set; }
        public double MinWidth { get; set; }
        public double MinRiser { get; set; }
        public double MaxRiser { get; set; }
        public double MinTread { get; set; }
        public double MaxTread { get; set; }
        public double MinHeadroom { get; set; }
        public double MinLandingDepth { get; set; }
        public bool RequiresHandrails { get; set; }
    }

    public class StairGeometry
    {
        public double RiserHeight { get; set; }
        public double TreadDepth { get; set; }
        public int NumberOfRisers { get; set; }
        public double RecommendedWidth { get; set; }
        public bool RequiresLanding { get; set; }
    }

    public class StairPlacementOptions
    {
        public double? Width { get; set; }
        public string StairType { get; set; }
        public string BuildingCode { get; set; } = "IBC_Commercial";
        public bool IncludeRailings { get; set; } = true;
    }

    public class StairMultiRunOptions : StairPlacementOptions
    {
        public double LandingDepth { get; set; } = 1200;
        public TurnDirection TurnDirection { get; set; } = TurnDirection.Right;
    }

    public class SpiralStairOptions
    {
        public double? Width { get; set; }
        public bool Clockwise { get; set; } = true;
        public string BuildingCode { get; set; }
    }

    public enum TurnDirection { Left, Right }

    // Roof classes
    public class RoofParameters
    {
        public string RoofType { get; set; }
        public double Slope { get; set; }
        public double Overhang { get; set; }
        public string Material { get; set; }
        public double Area { get; set; }
        public double InsulationRValue { get; set; }
    }

    public class RoofTypeDefault
    {
        public double MinSlope { get; set; }
        public double MaxSlope { get; set; }
        public double InsulationR { get; set; }
    }

    public class RoofPlacementOptions
    {
        public string RoofType { get; set; }
        public double Slope { get; set; }
        public string RidgeDirection { get; set; }
        public double? Overhang { get; set; }
        public string Material { get; set; }
        public string ClimateZone { get; set; }
    }

    public class DrainageResult
    {
        public int NumberOfDrains { get; set; }
        public double GutterSize { get; set; }
        public double DownpipeSize { get; set; }
    }

    public class InsulationResult
    {
        public double RValue { get; set; }
        public double UValue { get; set; }
        public double Thickness { get; set; }
    }

    // Ceiling classes
    public class CeilingParameters
    {
        public string CeilingType { get; set; }
        public double Height { get; set; }
        public double GridSize { get; set; }
        public double TileSize { get; set; }
        public double AcousticNRC { get; set; }
        public string FireRating { get; set; }
        public double PlenumHeight { get; set; }
    }

    public class CeilingTypeDefault
    {
        public double GridSize { get; set; }
        public double TileSize { get; set; }
        public double AcousticNRC { get; set; }
        public string FireRating { get; set; }
    }

    public class CeilingPlacementOptions
    {
        public string CeilingType { get; set; }
        public double Height { get; set; }
        public double? HeightOffset { get; set; }
        public string RoomFunction { get; set; }
    }

    public class CeilingGridLayout
    {
        public int TilesX { get; set; }
        public int TilesY { get; set; }
        public int TotalTiles { get; set; }
        public double BorderWidth { get; set; }
        public double BorderLength { get; set; }
    }

    // Curtain Wall classes
    public class CurtainWallParameters
    {
        public string SystemType { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double GridX { get; set; }
        public double GridY { get; set; }
        public double MullionWidth { get; set; }
        public double MullionDepth { get; set; }
        public string GlazingType { get; set; }
        public double SpandrelRatio { get; set; }
    }

    public class CurtainWallSystemDefault
    {
        public double MullionWidth { get; set; }
        public double MullionDepth { get; set; }
        public double GridX { get; set; }
        public double GridY { get; set; }
        public double UValue { get; set; }
    }

    public class CurtainWallPlacementOptions
    {
        public string SystemType { get; set; }
        public double GridX { get; set; }
        public double GridY { get; set; }
        public string GlazingType { get; set; }
        public string SpandrelType { get; set; }
        public double? SpandrelRatio { get; set; }
    }

    public class CurtainWallLayout
    {
        public int PanelsX { get; set; }
        public int PanelsY { get; set; }
        public int TotalPanels { get; set; }
        public double GlazingArea { get; set; }
        public double SpandrelArea { get; set; }
    }

    public class ThermalResult
    {
        public double OverallUValue { get; set; }
        public double SHGC { get; set; }
    }

    public class FacadePerformance
    {
        public double OverallUValue { get; set; }
        public double OverallSHGC { get; set; }
        public double GlazingRatio { get; set; }
        public double AnnualHeatGain { get; set; }
        public double AnnualHeatLoss { get; set; }
    }

    #endregion
}
