// ===================================================================
// StingBIM Structural Element Creators
// ColumnCreator and BeamCreator with grid coordination, load analysis
// Aligned with MR_PARAMETERS.txt structural parameters
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Creation.Elements
{
    #region Column Creator

    /// <summary>
    /// Creates structural columns with grid coordination and load-based sizing.
    /// </summary>
    public class ColumnCreator : IElementCreator
    {
        // ISO 19650 Parameter alignment
        private const string PARAM_COLUMN_WIDTH = "MR_COLUMN_WIDTH";
        private const string PARAM_COLUMN_DEPTH = "MR_COLUMN_DEPTH";
        private const string PARAM_COLUMN_HEIGHT = "MR_COLUMN_HEIGHT";
        private const string PARAM_COLUMN_MATERIAL = "MR_COLUMN_MATERIAL";
        private const string PARAM_COLUMN_LOAD_CAPACITY = "MR_COLUMN_LOAD_CAPACITY";
        private const string PARAM_COLUMN_FIRE_RATING = "MR_COLUMN_FIRE_RATING";
        private const string PARAM_COLUMN_GRID_LOCATION = "MR_COLUMN_GRID_LOCATION";

        // Standard column sizes (mm)
        private static readonly Dictionary<string, ColumnTypeDefault> StandardColumns = new()
        {
            ["Concrete200"] = new ColumnTypeDefault { Width = 200, Depth = 200, Material = ColumnMaterial.Concrete, LoadCapacity = 800 },
            ["Concrete300"] = new ColumnTypeDefault { Width = 300, Depth = 300, Material = ColumnMaterial.Concrete, LoadCapacity = 1800 },
            ["Concrete400"] = new ColumnTypeDefault { Width = 400, Depth = 400, Material = ColumnMaterial.Concrete, LoadCapacity = 3200 },
            ["Concrete450"] = new ColumnTypeDefault { Width = 450, Depth = 450, Material = ColumnMaterial.Concrete, LoadCapacity = 4000 },
            ["Concrete500"] = new ColumnTypeDefault { Width = 500, Depth = 500, Material = ColumnMaterial.Concrete, LoadCapacity = 5000 },
            ["Concrete600"] = new ColumnTypeDefault { Width = 600, Depth = 600, Material = ColumnMaterial.Concrete, LoadCapacity = 7200 },
            ["Concrete750"] = new ColumnTypeDefault { Width = 750, Depth = 750, Material = ColumnMaterial.Concrete, LoadCapacity = 11250 },
            ["SteelHSS150"] = new ColumnTypeDefault { Width = 150, Depth = 150, Material = ColumnMaterial.Steel, LoadCapacity = 600 },
            ["SteelHSS200"] = new ColumnTypeDefault { Width = 200, Depth = 200, Material = ColumnMaterial.Steel, LoadCapacity = 1200 },
            ["SteelHSS250"] = new ColumnTypeDefault { Width = 250, Depth = 250, Material = ColumnMaterial.Steel, LoadCapacity = 1900 },
            ["SteelW10x49"] = new ColumnTypeDefault { Width = 254, Depth = 253, Material = ColumnMaterial.Steel, LoadCapacity = 2200 },
            ["SteelW12x65"] = new ColumnTypeDefault { Width = 305, Depth = 304, Material = ColumnMaterial.Steel, LoadCapacity = 3000 },
            ["SteelW14x90"] = new ColumnTypeDefault { Width = 356, Depth = 368, Material = ColumnMaterial.Steel, LoadCapacity = 4200 },
            ["TimberGlulam200"] = new ColumnTypeDefault { Width = 200, Depth = 200, Material = ColumnMaterial.Timber, LoadCapacity = 400 },
            ["TimberGlulam300"] = new ColumnTypeDefault { Width = 300, Depth = 300, Material = ColumnMaterial.Timber, LoadCapacity = 900 },
            ["ConcreteRound400"] = new ColumnTypeDefault { Width = 400, Depth = 400, Material = ColumnMaterial.Concrete, LoadCapacity = 2500, IsRound = true },
            ["ConcreteRound500"] = new ColumnTypeDefault { Width = 500, Depth = 500, Material = ColumnMaterial.Concrete, LoadCapacity = 4000, IsRound = true }
        };

        public async Task<CreationResult> CreateAsync(
            ElementCreationParams parameters,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var result = new CreationResult
                {
                    ElementType = "Column",
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

                    var columnParams = ExtractColumnParameters(parameters);

                    // Size column based on load if specified
                    if (parameters.Parameters.TryGetValue("DesignLoad", out var loadObj))
                    {
                        double designLoad = Convert.ToDouble(loadObj);
                        AutoSizeColumn(columnParams, designLoad);
                    }

                    result.Success = true;
                    result.CreatedElementId = GenerateElementId();
                    result.Parameters = columnParams;
                    result.Message = $"Created {columnParams.ColumnType} column at {columnParams.GridLocation}";

                    // ISO 19650 parameter mapping
                    result.Metadata[PARAM_COLUMN_WIDTH] = columnParams.Width;
                    result.Metadata[PARAM_COLUMN_DEPTH] = columnParams.Depth;
                    result.Metadata[PARAM_COLUMN_HEIGHT] = columnParams.Height;
                    result.Metadata[PARAM_COLUMN_MATERIAL] = columnParams.Material.ToString();
                    result.Metadata[PARAM_COLUMN_LOAD_CAPACITY] = columnParams.LoadCapacity;
                    result.Metadata[PARAM_COLUMN_FIRE_RATING] = columnParams.FireRating;
                    result.Metadata[PARAM_COLUMN_GRID_LOCATION] = columnParams.GridLocation;
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
        /// Creates a column at a grid intersection.
        /// </summary>
        public async Task<CreationResult> CreateAtGridAsync(
            string gridLine1,
            string gridLine2,
            int baseLevelId,
            int topLevelId,
            ColumnPlacementOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new ColumnPlacementOptions();

            var parameters = new ElementCreationParams
            {
                ElementType = "Column",
                Parameters = new Dictionary<string, object>
                {
                    { "GridLine1", gridLine1 },
                    { "GridLine2", gridLine2 },
                    { "GridLocation", $"{gridLine1}-{gridLine2}" },
                    { "BaseLevel", baseLevelId },
                    { "TopLevel", topLevelId },
                    { "ColumnType", options.ColumnType ?? "Concrete300" },
                    { "Width", options.Width },
                    { "Depth", options.Depth },
                    { "Material", options.Material },
                    { "DesignLoad", options.DesignLoad },
                    { "Rotation", options.Rotation }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Auto-generates columns on a structural grid.
        /// </summary>
        public async Task<BatchCreationResult> GenerateGridColumnsAsync(
            StructuralGrid grid,
            int baseLevelId,
            int topLevelId,
            ColumnAutoPlacementRules rules = null,
            CancellationToken cancellationToken = default)
        {
            rules ??= ColumnAutoPlacementRules.Default;
            var results = new BatchCreationResult { StartTime = DateTime.Now };

            // Generate at all grid intersections
            foreach (var xLine in grid.XLines)
            {
                foreach (var yLine in grid.YLines)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Calculate tributary area for load
                    double tributaryArea = CalculateTributaryArea(xLine, yLine, grid);
                    double designLoad = tributaryArea * rules.FloorLoadPerM2 * grid.NumberOfFloors;

                    // Select appropriate column size
                    var columnType = SelectColumnType(designLoad, rules.PreferredMaterial);

                    var options = new ColumnPlacementOptions
                    {
                        ColumnType = columnType,
                        DesignLoad = designLoad,
                        Material = rules.PreferredMaterial
                    };

                    var result = await CreateAtGridAsync(xLine.Name, yLine.Name, baseLevelId, topLevelId, options, cancellationToken);
                    results.Results.Add(result);
                }
            }

            results.EndTime = DateTime.Now;
            results.TotalCreated = results.Results.Count(r => r.Success);
            results.TotalFailed = results.Results.Count(r => !r.Success);

            return results;
        }

        /// <summary>
        /// Recommends column size based on tributary area and load.
        /// </summary>
        public string RecommendColumnSize(double tributaryArea, int numberOfFloors, double liveLoad = 2.5, double deadLoad = 5.0)
        {
            double totalLoad = tributaryArea * (liveLoad + deadLoad) * numberOfFloors * 1.5; // Safety factor

            return SelectColumnType(totalLoad, ColumnMaterial.Concrete);
        }

        public IEnumerable<ColumnTypeInfo> GetAvailableColumnTypes()
        {
            return StandardColumns.Select(kvp => new ColumnTypeInfo
            {
                Name = kvp.Key,
                Width = kvp.Value.Width,
                Depth = kvp.Value.Depth,
                Material = kvp.Value.Material,
                LoadCapacity = kvp.Value.LoadCapacity,
                IsRound = kvp.Value.IsRound
            });
        }

        #region Private Methods

        private ValidationResult ValidateParameters(ElementCreationParams parameters)
        {
            if (parameters.Parameters.TryGetValue("Width", out var widthObj))
            {
                var width = Convert.ToDouble(widthObj);
                if (width < 100 || width > 2000)
                    return ValidationResult.Invalid($"Column width {width}mm outside valid range");
            }

            return ValidationResult.Valid();
        }

        private ColumnParameters ExtractColumnParameters(ElementCreationParams parameters)
        {
            var columnType = GetString(parameters.Parameters, "ColumnType", "Concrete300");
            var defaults = StandardColumns.GetValueOrDefault(columnType, StandardColumns["Concrete300"]);

            return new ColumnParameters
            {
                ColumnType = columnType,
                Width = GetDouble(parameters.Parameters, "Width", defaults.Width),
                Depth = GetDouble(parameters.Parameters, "Depth", defaults.Depth),
                Height = GetDouble(parameters.Parameters, "Height", 3000),
                Material = defaults.Material,
                LoadCapacity = defaults.LoadCapacity,
                FireRating = GetString(parameters.Parameters, "FireRating", "2HR"),
                GridLocation = GetString(parameters.Parameters, "GridLocation", ""),
                Rotation = GetDouble(parameters.Parameters, "Rotation", 0),
                IsRound = defaults.IsRound
            };
        }

        private void AutoSizeColumn(ColumnParameters columnParams, double designLoad)
        {
            // Find smallest column that can handle the load
            var suitable = StandardColumns
                .Where(c => c.Value.Material == columnParams.Material && c.Value.LoadCapacity >= designLoad)
                .OrderBy(c => c.Value.LoadCapacity)
                .FirstOrDefault();

            if (suitable.Value != null)
            {
                columnParams.ColumnType = suitable.Key;
                columnParams.Width = suitable.Value.Width;
                columnParams.Depth = suitable.Value.Depth;
                columnParams.LoadCapacity = suitable.Value.LoadCapacity;
            }
        }

        private double CalculateTributaryArea(GridLine xLine, GridLine yLine, StructuralGrid grid)
        {
            // Find adjacent grid lines
            var xIndex = grid.XLines.IndexOf(xLine);
            var yIndex = grid.YLines.IndexOf(yLine);

            double xSpan = 0;
            if (xIndex > 0)
                xSpan += (xLine.Position - grid.XLines[xIndex - 1].Position) / 2;
            if (xIndex < grid.XLines.Count - 1)
                xSpan += (grid.XLines[xIndex + 1].Position - xLine.Position) / 2;
            if (xIndex == 0 || xIndex == grid.XLines.Count - 1)
                xSpan *= 2;

            double ySpan = 0;
            if (yIndex > 0)
                ySpan += (yLine.Position - grid.YLines[yIndex - 1].Position) / 2;
            if (yIndex < grid.YLines.Count - 1)
                ySpan += (grid.YLines[yIndex + 1].Position - yLine.Position) / 2;
            if (yIndex == 0 || yIndex == grid.YLines.Count - 1)
                ySpan *= 2;

            return xSpan * ySpan / 1000000; // Convert mm² to m²
        }

        private string SelectColumnType(double load, ColumnMaterial material)
        {
            var suitable = StandardColumns
                .Where(c => c.Value.Material == material && c.Value.LoadCapacity >= load)
                .OrderBy(c => c.Value.LoadCapacity)
                .FirstOrDefault();

            return suitable.Key ?? (material == ColumnMaterial.Concrete ? "Concrete600" : "SteelW14x90");
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

    #region Beam Creator

    /// <summary>
    /// Creates structural beams with span optimization and load calculations.
    /// </summary>
    public class BeamCreator : IElementCreator
    {
        // ISO 19650 Parameter alignment
        private const string PARAM_BEAM_WIDTH = "MR_BEAM_WIDTH";
        private const string PARAM_BEAM_DEPTH = "MR_BEAM_DEPTH";
        private const string PARAM_BEAM_LENGTH = "MR_BEAM_LENGTH";
        private const string PARAM_BEAM_MATERIAL = "MR_BEAM_MATERIAL";
        private const string PARAM_BEAM_MOMENT_CAPACITY = "MR_BEAM_MOMENT_CAPACITY";
        private const string PARAM_BEAM_FIRE_RATING = "MR_BEAM_FIRE_RATING";

        // Standard beam sizes (mm)
        private static readonly Dictionary<string, BeamTypeDefault> StandardBeams = new()
        {
            // Concrete beams (width x depth)
            ["Concrete200x400"] = new BeamTypeDefault { Width = 200, Depth = 400, Material = BeamMaterial.Concrete, MaxSpan = 6000, MomentCapacity = 80 },
            ["Concrete200x500"] = new BeamTypeDefault { Width = 200, Depth = 500, Material = BeamMaterial.Concrete, MaxSpan = 7500, MomentCapacity = 125 },
            ["Concrete250x500"] = new BeamTypeDefault { Width = 250, Depth = 500, Material = BeamMaterial.Concrete, MaxSpan = 8000, MomentCapacity = 156 },
            ["Concrete300x600"] = new BeamTypeDefault { Width = 300, Depth = 600, Material = BeamMaterial.Concrete, MaxSpan = 9000, MomentCapacity = 270 },
            ["Concrete300x700"] = new BeamTypeDefault { Width = 300, Depth = 700, Material = BeamMaterial.Concrete, MaxSpan = 10000, MomentCapacity = 367 },
            ["Concrete350x750"] = new BeamTypeDefault { Width = 350, Depth = 750, Material = BeamMaterial.Concrete, MaxSpan = 12000, MomentCapacity = 492 },
            ["Concrete400x800"] = new BeamTypeDefault { Width = 400, Depth = 800, Material = BeamMaterial.Concrete, MaxSpan = 14000, MomentCapacity = 640 },

            // Steel beams (W sections)
            ["SteelW8x31"] = new BeamTypeDefault { Width = 203, Depth = 203, Material = BeamMaterial.Steel, MaxSpan = 5000, MomentCapacity = 110 },
            ["SteelW10x49"] = new BeamTypeDefault { Width = 254, Depth = 253, Material = BeamMaterial.Steel, MaxSpan = 6500, MomentCapacity = 199 },
            ["SteelW12x65"] = new BeamTypeDefault { Width = 305, Depth = 307, Material = BeamMaterial.Steel, MaxSpan = 8000, MomentCapacity = 321 },
            ["SteelW14x90"] = new BeamTypeDefault { Width = 356, Depth = 356, Material = BeamMaterial.Steel, MaxSpan = 10000, MomentCapacity = 506 },
            ["SteelW16x100"] = new BeamTypeDefault { Width = 406, Depth = 406, Material = BeamMaterial.Steel, MaxSpan = 12000, MomentCapacity = 675 },
            ["SteelW18x119"] = new BeamTypeDefault { Width = 457, Depth = 457, Material = BeamMaterial.Steel, MaxSpan = 14000, MomentCapacity = 898 },
            ["SteelW21x147"] = new BeamTypeDefault { Width = 533, Depth = 533, Material = BeamMaterial.Steel, MaxSpan = 16000, MomentCapacity = 1242 },
            ["SteelW24x176"] = new BeamTypeDefault { Width = 610, Depth = 610, Material = BeamMaterial.Steel, MaxSpan = 18000, MomentCapacity = 1627 },

            // Timber beams
            ["TimberGlulam140x300"] = new BeamTypeDefault { Width = 140, Depth = 300, Material = BeamMaterial.Timber, MaxSpan = 5000, MomentCapacity = 42 },
            ["TimberGlulam180x400"] = new BeamTypeDefault { Width = 180, Depth = 400, Material = BeamMaterial.Timber, MaxSpan = 7000, MomentCapacity = 72 },
            ["TimberGlulam200x500"] = new BeamTypeDefault { Width = 200, Depth = 500, Material = BeamMaterial.Timber, MaxSpan = 9000, MomentCapacity = 125 },
            ["TimberGlulam240x600"] = new BeamTypeDefault { Width = 240, Depth = 600, Material = BeamMaterial.Timber, MaxSpan = 12000, MomentCapacity = 216 }
        };

        public async Task<CreationResult> CreateAsync(
            ElementCreationParams parameters,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var result = new CreationResult
                {
                    ElementType = "Beam",
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

                    var beamParams = ExtractBeamParameters(parameters);

                    // Auto-size based on span and load if specified
                    if (parameters.Parameters.TryGetValue("Span", out var spanObj))
                    {
                        double span = Convert.ToDouble(spanObj);
                        double load = GetDouble(parameters.Parameters, "DesignLoad", 10); // kN/m default
                        AutoSizeBeam(beamParams, span, load);
                    }

                    // Calculate deflection
                    var deflection = CalculateDeflection(beamParams);

                    result.Success = true;
                    result.CreatedElementId = GenerateElementId();
                    result.Parameters = beamParams;
                    result.Message = $"Created {beamParams.BeamType} beam ({beamParams.Length}mm span)";

                    // ISO 19650 parameter mapping
                    result.Metadata[PARAM_BEAM_WIDTH] = beamParams.Width;
                    result.Metadata[PARAM_BEAM_DEPTH] = beamParams.Depth;
                    result.Metadata[PARAM_BEAM_LENGTH] = beamParams.Length;
                    result.Metadata[PARAM_BEAM_MATERIAL] = beamParams.Material.ToString();
                    result.Metadata[PARAM_BEAM_MOMENT_CAPACITY] = beamParams.MomentCapacity;
                    result.Metadata[PARAM_BEAM_FIRE_RATING] = beamParams.FireRating;
                    result.Metadata["Deflection"] = deflection.MaxDeflection;
                    result.Metadata["DeflectionRatio"] = deflection.SpanRatio;
                    result.Metadata["DeflectionOK"] = deflection.IsAcceptable;
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
        /// Creates a beam between two columns or grid intersections.
        /// </summary>
        public async Task<CreationResult> CreateBetweenColumnsAsync(
            int column1Id,
            int column2Id,
            int levelId,
            BeamPlacementOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new BeamPlacementOptions();

            var parameters = new ElementCreationParams
            {
                ElementType = "Beam",
                Parameters = new Dictionary<string, object>
                {
                    { "Column1", column1Id },
                    { "Column2", column2Id },
                    { "Level", levelId },
                    { "BeamType", options.BeamType ?? "Concrete300x600" },
                    { "Span", options.Span },
                    { "DesignLoad", options.DesignLoad },
                    { "Material", options.Material },
                    { "JustificationY", options.JustificationY },
                    { "JustificationZ", options.JustificationZ }
                }
            };

            return await CreateAsync(parameters, cancellationToken);
        }

        /// <summary>
        /// Auto-generates beams on a structural grid.
        /// </summary>
        public async Task<BatchCreationResult> GenerateGridBeamsAsync(
            StructuralGrid grid,
            int levelId,
            BeamAutoPlacementRules rules = null,
            CancellationToken cancellationToken = default)
        {
            rules ??= BeamAutoPlacementRules.Default;
            var results = new BatchCreationResult { StartTime = DateTime.Now };

            // Create beams along X grid lines
            foreach (var xLine in grid.XLines)
            {
                for (int i = 0; i < grid.YLines.Count - 1; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    double span = grid.YLines[i + 1].Position - grid.YLines[i].Position;
                    double tributaryWidth = CalculateTributaryWidth(xLine, grid);
                    double designLoad = tributaryWidth * rules.FloorLoadPerM2 / 1000; // kN/m

                    var beamType = SelectBeamType(span, designLoad, rules.PreferredMaterial);

                    var options = new BeamPlacementOptions
                    {
                        BeamType = beamType,
                        Span = span,
                        DesignLoad = designLoad,
                        Material = rules.PreferredMaterial
                    };

                    // Simulate column IDs
                    var result = await CreateBetweenColumnsAsync(
                        i * 100 + grid.XLines.IndexOf(xLine),
                        (i + 1) * 100 + grid.XLines.IndexOf(xLine),
                        levelId, options, cancellationToken);
                    results.Results.Add(result);
                }
            }

            // Create beams along Y grid lines
            foreach (var yLine in grid.YLines)
            {
                for (int i = 0; i < grid.XLines.Count - 1; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    double span = grid.XLines[i + 1].Position - grid.XLines[i].Position;
                    double tributaryWidth = CalculateTributaryWidth(yLine, grid);
                    double designLoad = tributaryWidth * rules.FloorLoadPerM2 / 1000;

                    var beamType = SelectBeamType(span, designLoad, rules.PreferredMaterial);

                    var options = new BeamPlacementOptions
                    {
                        BeamType = beamType,
                        Span = span,
                        DesignLoad = designLoad,
                        Material = rules.PreferredMaterial
                    };

                    var result = await CreateBetweenColumnsAsync(
                        grid.YLines.IndexOf(yLine) * 100 + i,
                        grid.YLines.IndexOf(yLine) * 100 + i + 1,
                        levelId, options, cancellationToken);
                    results.Results.Add(result);
                }
            }

            results.EndTime = DateTime.Now;
            results.TotalCreated = results.Results.Count(r => r.Success);
            results.TotalFailed = results.Results.Count(r => !r.Success);

            return results;
        }

        /// <summary>
        /// Recommends beam size based on span and loading.
        /// </summary>
        public string RecommendBeamSize(double span, double loadPerMeter, BeamMaterial material = BeamMaterial.Concrete)
        {
            // Calculate required moment capacity (simplified)
            double moment = loadPerMeter * span * span / 8 / 1000; // kN·m

            return SelectBeamType(span, loadPerMeter, material);
        }

        public IEnumerable<BeamTypeInfo> GetAvailableBeamTypes()
        {
            return StandardBeams.Select(kvp => new BeamTypeInfo
            {
                Name = kvp.Key,
                Width = kvp.Value.Width,
                Depth = kvp.Value.Depth,
                Material = kvp.Value.Material,
                MaxSpan = kvp.Value.MaxSpan,
                MomentCapacity = kvp.Value.MomentCapacity
            });
        }

        #region Private Methods

        private ValidationResult ValidateParameters(ElementCreationParams parameters)
        {
            if (parameters.Parameters.TryGetValue("Span", out var spanObj))
            {
                var span = Convert.ToDouble(spanObj);
                if (span < 1000 || span > 20000)
                    return ValidationResult.Invalid($"Beam span {span}mm outside practical range");
            }

            return ValidationResult.Valid();
        }

        private BeamParameters ExtractBeamParameters(ElementCreationParams parameters)
        {
            var beamType = GetString(parameters.Parameters, "BeamType", "Concrete300x600");
            var defaults = StandardBeams.GetValueOrDefault(beamType, StandardBeams["Concrete300x600"]);

            return new BeamParameters
            {
                BeamType = beamType,
                Width = defaults.Width,
                Depth = defaults.Depth,
                Length = GetDouble(parameters.Parameters, "Span", 6000),
                Material = defaults.Material,
                MomentCapacity = defaults.MomentCapacity,
                FireRating = GetString(parameters.Parameters, "FireRating", "2HR"),
                JustificationY = GetString(parameters.Parameters, "JustificationY", "Center"),
                JustificationZ = GetString(parameters.Parameters, "JustificationZ", "Top")
            };
        }

        private void AutoSizeBeam(BeamParameters beamParams, double span, double load)
        {
            // Calculate required moment
            double requiredMoment = load * span * span / 8 / 1000000; // kN·m

            // Find smallest beam that works
            var suitable = StandardBeams
                .Where(b => b.Value.Material == beamParams.Material &&
                           b.Value.MaxSpan >= span &&
                           b.Value.MomentCapacity >= requiredMoment)
                .OrderBy(b => b.Value.Depth)
                .FirstOrDefault();

            if (suitable.Value != null)
            {
                beamParams.BeamType = suitable.Key;
                beamParams.Width = suitable.Value.Width;
                beamParams.Depth = suitable.Value.Depth;
                beamParams.MomentCapacity = suitable.Value.MomentCapacity;
            }
        }

        private DeflectionResult CalculateDeflection(BeamParameters beamParams)
        {
            // Simplified deflection calculation
            // δ = 5wL⁴/(384EI)
            double load = 10; // kN/m assumed
            double L = beamParams.Length / 1000; // m
            double E = beamParams.Material == BeamMaterial.Steel ? 200000 : 25000; // MPa
            double I = beamParams.Width * Math.Pow(beamParams.Depth, 3) / 12 / 1e12; // m⁴

            double deflection = 5 * load * Math.Pow(L, 4) / (384 * E * 1e6 * I) * 1000; // mm
            double ratio = beamParams.Length / deflection;

            return new DeflectionResult
            {
                MaxDeflection = deflection,
                SpanRatio = ratio,
                IsAcceptable = ratio >= 250 // L/250 typical limit
            };
        }

        private double CalculateTributaryWidth(GridLine line, StructuralGrid grid)
        {
            var lines = line.Name.StartsWith("A") || line.Name.StartsWith("B") ? grid.XLines : grid.YLines;
            var index = lines.IndexOf(line);

            double width = 0;
            if (index > 0)
                width += (line.Position - lines[index - 1].Position) / 2;
            if (index < lines.Count - 1)
                width += (lines[index + 1].Position - line.Position) / 2;

            return width;
        }

        private string SelectBeamType(double span, double load, BeamMaterial material)
        {
            double moment = load * span * span / 8 / 1000000;

            var suitable = StandardBeams
                .Where(b => b.Value.Material == material &&
                           b.Value.MaxSpan >= span &&
                           b.Value.MomentCapacity >= moment)
                .OrderBy(b => b.Value.Depth)
                .FirstOrDefault();

            return suitable.Key ?? (material == BeamMaterial.Concrete ? "Concrete400x800" : "SteelW24x176");
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

    #region Shared Supporting Classes

    public class ColumnTypeDefault
    {
        public double Width { get; set; }
        public double Depth { get; set; }
        public ColumnMaterial Material { get; set; }
        public double LoadCapacity { get; set; } // kN
        public bool IsRound { get; set; }
    }

    public class ColumnParameters
    {
        public string ColumnType { get; set; }
        public double Width { get; set; }
        public double Depth { get; set; }
        public double Height { get; set; }
        public ColumnMaterial Material { get; set; }
        public double LoadCapacity { get; set; }
        public string FireRating { get; set; }
        public string GridLocation { get; set; }
        public double Rotation { get; set; }
        public bool IsRound { get; set; }
    }

    public class ColumnTypeInfo
    {
        public string Name { get; set; }
        public double Width { get; set; }
        public double Depth { get; set; }
        public ColumnMaterial Material { get; set; }
        public double LoadCapacity { get; set; }
        public bool IsRound { get; set; }
    }

    public class ColumnPlacementOptions
    {
        public string ColumnType { get; set; }
        public double Width { get; set; }
        public double Depth { get; set; }
        public ColumnMaterial Material { get; set; }
        public double DesignLoad { get; set; }
        public double Rotation { get; set; }
    }

    public class ColumnAutoPlacementRules
    {
        public ColumnMaterial PreferredMaterial { get; set; } = ColumnMaterial.Concrete;
        public double FloorLoadPerM2 { get; set; } = 7.5; // kN/m² (DL + LL)
        public bool AlignToGrid { get; set; } = true;

        public static ColumnAutoPlacementRules Default => new();
    }

    public class BeamTypeDefault
    {
        public double Width { get; set; }
        public double Depth { get; set; }
        public BeamMaterial Material { get; set; }
        public double MaxSpan { get; set; }
        public double MomentCapacity { get; set; } // kN·m
    }

    public class BeamParameters
    {
        public string BeamType { get; set; }
        public double Width { get; set; }
        public double Depth { get; set; }
        public double Length { get; set; }
        public BeamMaterial Material { get; set; }
        public double MomentCapacity { get; set; }
        public string FireRating { get; set; }
        public string JustificationY { get; set; }
        public string JustificationZ { get; set; }
    }

    public class BeamTypeInfo
    {
        public string Name { get; set; }
        public double Width { get; set; }
        public double Depth { get; set; }
        public BeamMaterial Material { get; set; }
        public double MaxSpan { get; set; }
        public double MomentCapacity { get; set; }
    }

    public class BeamPlacementOptions
    {
        public string BeamType { get; set; }
        public double Span { get; set; }
        public double DesignLoad { get; set; }
        public BeamMaterial Material { get; set; }
        public string JustificationY { get; set; } = "Center";
        public string JustificationZ { get; set; } = "Top";
    }

    public class BeamAutoPlacementRules
    {
        public BeamMaterial PreferredMaterial { get; set; } = BeamMaterial.Concrete;
        public double FloorLoadPerM2 { get; set; } = 7.5;
        public double MaxDeflectionRatio { get; set; } = 250;

        public static BeamAutoPlacementRules Default => new();
    }

    public class DeflectionResult
    {
        public double MaxDeflection { get; set; }
        public double SpanRatio { get; set; }
        public bool IsAcceptable { get; set; }
    }

    public class StructuralGrid
    {
        public List<GridLine> XLines { get; set; } = new();
        public List<GridLine> YLines { get; set; } = new();
        public int NumberOfFloors { get; set; } = 1;
    }

    public class GridLine
    {
        public string Name { get; set; }
        public double Position { get; set; }
    }

    public enum ColumnMaterial { Concrete, Steel, Timber, Masonry }
    public enum BeamMaterial { Concrete, Steel, Timber }

    #endregion
}
