using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Creation.Fabrication
{
    /// <summary>
    /// Generates fabrication-ready models, shop drawings, and CNC output.
    /// Supports steel, precast, MEP, and curtain wall fabrication.
    /// </summary>
    public class FabricationModelGenerator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly FabricationSettings _settings;
        private readonly ShopDrawingGenerator _drawingGenerator;
        private readonly CNCExporter _cncExporter;
        private readonly NestingOptimizer _nestingOptimizer;

        public FabricationModelGenerator(FabricationSettings settings = null)
        {
            _settings = settings ?? new FabricationSettings();
            _drawingGenerator = new ShopDrawingGenerator(_settings);
            _cncExporter = new CNCExporter(_settings);
            _nestingOptimizer = new NestingOptimizer();
        }

        /// <summary>
        /// Generate complete fabrication package for structural steel.
        /// </summary>
        public async Task<SteelFabricationPackage> GenerateSteelFabricationAsync(
            SteelModel model,
            IProgress<FabricationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating steel fabrication for {model.Members.Count} members");
            var package = new SteelFabricationPackage { ProjectId = model.ProjectId };

            // Generate piece marks
            progress?.Report(new FabricationProgress { Phase = "Assigning piece marks", Percent = 5 });
            AssignPieceMarks(model);

            // Generate shop drawings
            progress?.Report(new FabricationProgress { Phase = "Generating shop drawings", Percent = 20 });
            foreach (var member in model.Members)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var drawing = await _drawingGenerator.GenerateSteelShopDrawingAsync(member, cancellationToken);
                package.ShopDrawings.Add(drawing);
            }

            // Generate assembly drawings
            progress?.Report(new FabricationProgress { Phase = "Generating assemblies", Percent = 50 });
            var assemblies = GroupIntoAssemblies(model);
            foreach (var assembly in assemblies)
            {
                var assemblyDrawing = await _drawingGenerator.GenerateAssemblyDrawingAsync(assembly, cancellationToken);
                package.AssemblyDrawings.Add(assemblyDrawing);
            }

            // Generate CNC files
            progress?.Report(new FabricationProgress { Phase = "Generating CNC files", Percent = 70 });
            foreach (var member in model.Members)
            {
                var cncFile = await _cncExporter.ExportSteelMemberAsync(member, cancellationToken);
                package.CNCFiles.Add(cncFile);
            }

            // Generate cut lists and BOMs
            progress?.Report(new FabricationProgress { Phase = "Generating BOMs", Percent = 90 });
            package.CutList = GenerateSteelCutList(model);
            package.BillOfMaterials = GenerateSteelBOM(model);
            package.NestingSheets = await _nestingOptimizer.OptimizePlateNestingAsync(model, cancellationToken);

            package.Statistics = CalculateSteelStatistics(package);
            Logger.Info($"Steel fabrication complete: {package.ShopDrawings.Count} drawings, {package.CNCFiles.Count} CNC files");
            return package;
        }

        /// <summary>
        /// Generate fabrication package for precast concrete.
        /// </summary>
        public async Task<PrecastFabricationPackage> GeneratePrecastFabricationAsync(
            PrecastModel model,
            IProgress<FabricationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating precast fabrication for {model.Elements.Count} elements");
            var package = new PrecastFabricationPackage { ProjectId = model.ProjectId };

            // Generate piece drawings with rebar
            progress?.Report(new FabricationProgress { Phase = "Generating piece drawings", Percent = 20 });
            foreach (var element in model.Elements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var drawing = await _drawingGenerator.GeneratePrecastDrawingAsync(element, cancellationToken);
                package.PieceDrawings.Add(drawing);

                var rebarDrawing = await _drawingGenerator.GenerateRebarDrawingAsync(element, cancellationToken);
                package.RebarDrawings.Add(rebarDrawing);
            }

            // Generate form drawings
            progress?.Report(new FabricationProgress { Phase = "Generating form drawings", Percent = 50 });
            var formGroups = GroupByFormType(model);
            foreach (var formGroup in formGroups)
            {
                var formDrawing = await _drawingGenerator.GenerateFormDrawingAsync(formGroup, cancellationToken);
                package.FormDrawings.Add(formDrawing);
            }

            // Generate embed schedules
            progress?.Report(new FabricationProgress { Phase = "Generating schedules", Percent = 70 });
            package.EmbedSchedule = GenerateEmbedSchedule(model);
            package.RebarSchedule = GenerateRebarSchedule(model);
            package.LiftingSchedule = GenerateLiftingSchedule(model);

            // Generate erection drawings
            progress?.Report(new FabricationProgress { Phase = "Generating erection plans", Percent = 85 });
            package.ErectionDrawings = await GenerateErectionDrawingsAsync(model, cancellationToken);

            package.Statistics = CalculatePrecastStatistics(package);
            return package;
        }

        /// <summary>
        /// Generate MEP fabrication/spool drawings.
        /// </summary>
        public async Task<MEPFabricationPackage> GenerateMEPFabricationAsync(
            MEPModel model,
            IProgress<FabricationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating MEP fabrication for {model.Systems.Count} systems");
            var package = new MEPFabricationPackage { ProjectId = model.ProjectId };

            foreach (var system in model.Systems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Generate spool drawings
                var spools = DivideIntoSpools(system);
                progress?.Report(new FabricationProgress
                {
                    Phase = $"Processing {system.Type} system",
                    Percent = 30
                });

                foreach (var spool in spools)
                {
                    var spoolDrawing = await _drawingGenerator.GenerateSpoolDrawingAsync(spool, cancellationToken);
                    package.SpoolDrawings.Add(spoolDrawing);
                }

                // Generate isometric drawings
                var isoDrawing = await _drawingGenerator.GenerateIsometricAsync(system, cancellationToken);
                package.IsometricDrawings.Add(isoDrawing);
            }

            // Generate cut lists and BOMs
            package.PipeCutList = GeneratePipeCutList(model);
            package.DuctCutList = GenerateDuctCutList(model);
            package.BillOfMaterials = GenerateMEPBOM(model);
            package.HangerSchedule = GenerateHangerSchedule(model);

            return package;
        }

        /// <summary>
        /// Generate curtain wall fabrication package.
        /// </summary>
        public async Task<CurtainWallFabricationPackage> GenerateCurtainWallFabricationAsync(
            CurtainWallModel model,
            IProgress<FabricationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating curtain wall fabrication for {model.Panels.Count} panels");
            var package = new CurtainWallFabricationPackage { ProjectId = model.ProjectId };

            // Generate panel drawings
            progress?.Report(new FabricationProgress { Phase = "Generating panel drawings", Percent = 20 });
            foreach (var panel in model.Panels)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var drawing = await _drawingGenerator.GeneratePanelDrawingAsync(panel, cancellationToken);
                package.PanelDrawings.Add(drawing);
            }

            // Generate mullion drawings with CNC data
            progress?.Report(new FabricationProgress { Phase = "Generating mullion drawings", Percent = 50 });
            foreach (var mullion in model.Mullions)
            {
                var drawing = await _drawingGenerator.GenerateMullionDrawingAsync(mullion, cancellationToken);
                package.MullionDrawings.Add(drawing);

                var cncFile = await _cncExporter.ExportMullionAsync(mullion, cancellationToken);
                package.CNCFiles.Add(cncFile);
            }

            // Generate glass schedules
            progress?.Report(new FabricationProgress { Phase = "Generating schedules", Percent = 80 });
            package.GlassSchedule = GenerateGlassSchedule(model);
            package.MullionSchedule = GenerateMullionSchedule(model);
            package.HardwareSchedule = GenerateHardwareSchedule(model);

            // Generate installation sequence
            package.InstallationSequence = GenerateInstallationSequence(model);

            return package;
        }

        /// <summary>
        /// Export to DXF/DWG format for CAD integration.
        /// </summary>
        public async Task<byte[]> ExportToDXFAsync(
            ShopDrawing drawing,
            DXFExportOptions options,
            CancellationToken cancellationToken = default)
        {
            var exporter = new DXFExporter(options);
            return await exporter.ExportAsync(drawing, cancellationToken);
        }

        /// <summary>
        /// Export CNC-ready G-code for plasma/laser cutting.
        /// </summary>
        public async Task<CNCFile> ExportGCodeAsync(
            PlateNestingResult nesting,
            CNCMachineType machineType,
            CancellationToken cancellationToken = default)
        {
            return await _cncExporter.ExportGCodeAsync(nesting, machineType, cancellationToken);
        }

        #region Private Methods

        private void AssignPieceMarks(SteelModel model)
        {
            var markCounter = new Dictionary<string, int>();
            foreach (var member in model.Members.OrderBy(m => m.Section).ThenBy(m => m.Length))
            {
                var prefix = GetMarkPrefix(member);
                if (!markCounter.ContainsKey(prefix))
                    markCounter[prefix] = 0;

                markCounter[prefix]++;
                member.PieceMark = $"{prefix}{markCounter[prefix]:D3}";
            }
        }

        private string GetMarkPrefix(SteelMember member)
        {
            return member.Type switch
            {
                SteelMemberType.Column => "C",
                SteelMemberType.Beam => "B",
                SteelMemberType.Brace => "BR",
                SteelMemberType.Girt => "G",
                SteelMemberType.Purlin => "P",
                SteelMemberType.Plate => "PL",
                SteelMemberType.Misc => "M",
                _ => "X"
            };
        }

        private List<SteelAssembly> GroupIntoAssemblies(SteelModel model)
        {
            var assemblies = new List<SteelAssembly>();

            // Group connected members into assemblies
            var processed = new HashSet<string>();
            foreach (var member in model.Members.Where(m => !processed.Contains(m.Id)))
            {
                var assembly = new SteelAssembly { Id = Guid.NewGuid().ToString() };
                CollectConnectedMembers(member, model, processed, assembly);
                if (assembly.Members.Count > 1)
                    assemblies.Add(assembly);
            }

            return assemblies;
        }

        private void CollectConnectedMembers(SteelMember member, SteelModel model,
            HashSet<string> processed, SteelAssembly assembly)
        {
            if (processed.Contains(member.Id)) return;
            processed.Add(member.Id);
            assembly.Members.Add(member);

            foreach (var connectedId in member.ConnectedMemberIds)
            {
                var connected = model.Members.FirstOrDefault(m => m.Id == connectedId);
                if (connected != null && !processed.Contains(connectedId))
                {
                    // Only include in assembly if it's a sub-member (like stiffeners)
                    if (connected.IsSubMember)
                        CollectConnectedMembers(connected, model, processed, assembly);
                }
            }
        }

        private CutList GenerateSteelCutList(SteelModel model)
        {
            var cutList = new CutList();

            var grouped = model.Members
                .GroupBy(m => new { m.Section, m.Grade })
                .OrderBy(g => g.Key.Section);

            foreach (var group in grouped)
            {
                foreach (var member in group.OrderByDescending(m => m.Length))
                {
                    cutList.Items.Add(new CutListItem
                    {
                        PieceMark = member.PieceMark,
                        Section = group.Key.Section,
                        Grade = group.Key.Grade,
                        Length = member.Length,
                        Quantity = 1,
                        Weight = member.Weight
                    });
                }
            }

            cutList.TotalWeight = cutList.Items.Sum(i => i.Weight * i.Quantity);
            return cutList;
        }

        private BillOfMaterials GenerateSteelBOM(SteelModel model)
        {
            var bom = new BillOfMaterials();

            // Main members
            var sectionGroups = model.Members
                .GroupBy(m => new { m.Section, m.Grade, m.Length })
                .Select(g => new BOMItem
                {
                    Description = $"{g.Key.Section} {g.Key.Grade}",
                    Size = g.Key.Section,
                    Length = g.Key.Length,
                    Quantity = g.Count(),
                    Unit = "EA",
                    Weight = g.Sum(m => m.Weight)
                });

            bom.Items.AddRange(sectionGroups);

            // Bolts and connections
            foreach (var conn in model.Connections)
            {
                foreach (var bolt in conn.Bolts)
                {
                    var existing = bom.Items.FirstOrDefault(i =>
                        i.Description == $"Bolt {bolt.Size} x {bolt.Length}");
                    if (existing != null)
                        existing.Quantity += bolt.Quantity;
                    else
                        bom.Items.Add(new BOMItem
                        {
                            Description = $"Bolt {bolt.Size} x {bolt.Length}",
                            Size = bolt.Size,
                            Quantity = bolt.Quantity,
                            Unit = "EA"
                        });
                }
            }

            bom.TotalWeight = bom.Items.Sum(i => i.Weight);
            return bom;
        }

        private List<PrecastFormGroup> GroupByFormType(PrecastModel model)
        {
            return model.Elements
                .GroupBy(e => e.FormType)
                .Select(g => new PrecastFormGroup
                {
                    FormType = g.Key,
                    Elements = g.ToList()
                })
                .ToList();
        }

        private Schedule GenerateEmbedSchedule(PrecastModel model)
        {
            var schedule = new Schedule { Name = "Embed Schedule" };
            schedule.Columns = new[] { "Piece Mark", "Embed Type", "Size", "Qty", "Location" };

            foreach (var element in model.Elements)
            {
                foreach (var embed in element.Embeds)
                {
                    schedule.Rows.Add(new[]
                    {
                        element.PieceMark,
                        embed.Type,
                        embed.Size,
                        embed.Quantity.ToString(),
                        embed.Location
                    });
                }
            }

            return schedule;
        }

        private Schedule GenerateRebarSchedule(PrecastModel model)
        {
            var schedule = new Schedule { Name = "Rebar Schedule" };
            schedule.Columns = new[] { "Piece Mark", "Bar Mark", "Size", "Shape", "Length", "Qty" };

            foreach (var element in model.Elements)
            {
                foreach (var bar in element.Rebar)
                {
                    schedule.Rows.Add(new[]
                    {
                        element.PieceMark,
                        bar.Mark,
                        bar.Size,
                        bar.Shape,
                        bar.Length.ToString("F0"),
                        bar.Quantity.ToString()
                    });
                }
            }

            return schedule;
        }

        private Schedule GenerateLiftingSchedule(PrecastModel model)
        {
            var schedule = new Schedule { Name = "Lifting Schedule" };
            schedule.Columns = new[] { "Piece Mark", "Weight", "Lift Points", "Lift Insert", "Rigging" };

            foreach (var element in model.Elements)
            {
                schedule.Rows.Add(new[]
                {
                    element.PieceMark,
                    $"{element.Weight:F0} kg",
                    element.LiftPoints.Count.ToString(),
                    element.LiftInsertType,
                    element.RecommendedRigging
                });
            }

            return schedule;
        }

        private async Task<List<ErectionDrawing>> GenerateErectionDrawingsAsync(
            PrecastModel model, CancellationToken cancellationToken)
        {
            var drawings = new List<ErectionDrawing>();

            // Group by level/sequence
            var sequences = model.Elements
                .GroupBy(e => e.ErectionSequence)
                .OrderBy(g => g.Key);

            foreach (var seq in sequences)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var drawing = new ErectionDrawing
                {
                    SequenceNumber = seq.Key,
                    Elements = seq.Select(e => e.PieceMark).ToList()
                };
                drawings.Add(drawing);
            }

            return await Task.FromResult(drawings);
        }

        private List<MEPSpool> DivideIntoSpools(MEPSystem system)
        {
            var spools = new List<MEPSpool>();
            var maxSpoolLength = _settings.MaxSpoolLength;

            // Divide system runs into manageable spools
            foreach (var run in system.Runs)
            {
                var currentSpool = new MEPSpool { SystemId = system.Id };
                double currentLength = 0;

                foreach (var segment in run.Segments)
                {
                    if (currentLength + segment.Length > maxSpoolLength && currentSpool.Segments.Any())
                    {
                        spools.Add(currentSpool);
                        currentSpool = new MEPSpool { SystemId = system.Id };
                        currentLength = 0;
                    }

                    currentSpool.Segments.Add(segment);
                    currentLength += segment.Length;
                }

                if (currentSpool.Segments.Any())
                    spools.Add(currentSpool);
            }

            // Assign spool marks
            for (int i = 0; i < spools.Count; i++)
            {
                spools[i].SpoolMark = $"{system.Type.ToString().Substring(0, 1)}-{i + 1:D3}";
            }

            return spools;
        }

        private CutList GeneratePipeCutList(MEPModel model)
        {
            var cutList = new CutList { Name = "Pipe Cut List" };

            foreach (var system in model.Systems.Where(s => s.Type == MEPSystemType.Piping))
            {
                foreach (var run in system.Runs)
                {
                    foreach (var segment in run.Segments)
                    {
                        cutList.Items.Add(new CutListItem
                        {
                            PieceMark = segment.Mark,
                            Section = $"{segment.Size}\" {segment.Material}",
                            Length = segment.Length,
                            Quantity = 1
                        });
                    }
                }
            }

            return cutList;
        }

        private CutList GenerateDuctCutList(MEPModel model)
        {
            var cutList = new CutList { Name = "Duct Cut List" };

            foreach (var system in model.Systems.Where(s => s.Type == MEPSystemType.Ductwork))
            {
                foreach (var run in system.Runs)
                {
                    foreach (var segment in run.Segments)
                    {
                        cutList.Items.Add(new CutListItem
                        {
                            PieceMark = segment.Mark,
                            Section = segment.DuctSize,
                            Length = segment.Length,
                            Quantity = 1
                        });
                    }
                }
            }

            return cutList;
        }

        private BillOfMaterials GenerateMEPBOM(MEPModel model)
        {
            var bom = new BillOfMaterials();

            foreach (var system in model.Systems)
            {
                // Pipe/duct lengths
                var segmentGroups = system.Runs
                    .SelectMany(r => r.Segments)
                    .GroupBy(s => new { s.Size, s.Material })
                    .Select(g => new BOMItem
                    {
                        Description = $"{g.Key.Size}\" {g.Key.Material}",
                        Quantity = g.Sum(s => s.Length),
                        Unit = "LF"
                    });
                bom.Items.AddRange(segmentGroups);

                // Fittings
                var fittingGroups = system.Runs
                    .SelectMany(r => r.Fittings)
                    .GroupBy(f => new { f.Type, f.Size })
                    .Select(g => new BOMItem
                    {
                        Description = $"{g.Key.Type} {g.Key.Size}\"",
                        Quantity = g.Count(),
                        Unit = "EA"
                    });
                bom.Items.AddRange(fittingGroups);
            }

            return bom;
        }

        private Schedule GenerateHangerSchedule(MEPModel model)
        {
            var schedule = new Schedule { Name = "Hanger Schedule" };
            schedule.Columns = new[] { "Mark", "Type", "Size", "Rod Size", "Location", "Qty" };

            foreach (var system in model.Systems)
            {
                foreach (var hanger in system.Hangers)
                {
                    schedule.Rows.Add(new[]
                    {
                        hanger.Mark,
                        hanger.Type,
                        hanger.Size,
                        hanger.RodSize,
                        hanger.Location,
                        "1"
                    });
                }
            }

            return schedule;
        }

        private Schedule GenerateGlassSchedule(CurtainWallModel model)
        {
            var schedule = new Schedule { Name = "Glass Schedule" };
            schedule.Columns = new[] { "Panel ID", "Width", "Height", "Glass Type", "Coating", "Qty" };

            var groups = model.Panels
                .GroupBy(p => new { p.Width, p.Height, p.GlassType, p.Coating });

            foreach (var group in groups)
            {
                schedule.Rows.Add(new[]
                {
                    string.Join(",", group.Select(p => p.Id)),
                    group.Key.Width.ToString("F0"),
                    group.Key.Height.ToString("F0"),
                    group.Key.GlassType,
                    group.Key.Coating,
                    group.Count().ToString()
                });
            }

            return schedule;
        }

        private Schedule GenerateMullionSchedule(CurtainWallModel model)
        {
            var schedule = new Schedule { Name = "Mullion Schedule" };
            schedule.Columns = new[] { "Mark", "Profile", "Length", "Finish", "Qty" };

            var groups = model.Mullions
                .GroupBy(m => new { m.Profile, m.Length, m.Finish });

            foreach (var group in groups)
            {
                schedule.Rows.Add(new[]
                {
                    group.First().Mark,
                    group.Key.Profile,
                    group.Key.Length.ToString("F0"),
                    group.Key.Finish,
                    group.Count().ToString()
                });
            }

            return schedule;
        }

        private Schedule GenerateHardwareSchedule(CurtainWallModel model)
        {
            var schedule = new Schedule { Name = "Hardware Schedule" };
            schedule.Columns = new[] { "Type", "Description", "Finish", "Qty" };

            var allHardware = model.Panels.SelectMany(p => p.Hardware)
                .Concat(model.Mullions.SelectMany(m => m.Hardware));

            var groups = allHardware.GroupBy(h => new { h.Type, h.Description, h.Finish });

            foreach (var group in groups)
            {
                schedule.Rows.Add(new[]
                {
                    group.Key.Type,
                    group.Key.Description,
                    group.Key.Finish,
                    group.Count().ToString()
                });
            }

            return schedule;
        }

        private InstallationSequence GenerateInstallationSequence(CurtainWallModel model)
        {
            var sequence = new InstallationSequence();

            // Sort panels by floor then by grid position
            var sortedPanels = model.Panels
                .OrderBy(p => p.Level)
                .ThenBy(p => p.GridX)
                .ThenBy(p => p.GridY)
                .ToList();

            int step = 1;
            foreach (var panel in sortedPanels)
            {
                sequence.Steps.Add(new InstallationStep
                {
                    StepNumber = step++,
                    ElementId = panel.Id,
                    Description = $"Install panel {panel.Id} at Level {panel.Level}, Grid {panel.GridX}-{panel.GridY}"
                });
            }

            return sequence;
        }

        private FabricationStatistics CalculateSteelStatistics(SteelFabricationPackage package)
        {
            return new FabricationStatistics
            {
                TotalDrawings = package.ShopDrawings.Count + package.AssemblyDrawings.Count,
                TotalCNCFiles = package.CNCFiles.Count,
                TotalWeight = package.BillOfMaterials?.TotalWeight ?? 0,
                TotalPieces = package.CutList?.Items.Count ?? 0
            };
        }

        private FabricationStatistics CalculatePrecastStatistics(PrecastFabricationPackage package)
        {
            return new FabricationStatistics
            {
                TotalDrawings = package.PieceDrawings.Count + package.RebarDrawings.Count,
                TotalPieces = package.PieceDrawings.Count
            };
        }

        #endregion
    }

    #region Support Classes

    internal class ShopDrawingGenerator
    {
        private readonly FabricationSettings _settings;

        public ShopDrawingGenerator(FabricationSettings settings) => _settings = settings;

        public Task<ShopDrawing> GenerateSteelShopDrawingAsync(SteelMember member, CancellationToken ct)
        {
            var drawing = new ShopDrawing
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"Shop Drawing - {member.PieceMark}",
                PieceMark = member.PieceMark,
                Scale = _settings.DefaultScale,
                Views = GenerateSteelViews(member)
            };
            return Task.FromResult(drawing);
        }

        public Task<ShopDrawing> GenerateAssemblyDrawingAsync(SteelAssembly assembly, CancellationToken ct)
        {
            var drawing = new ShopDrawing
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"Assembly Drawing - {assembly.Id}",
                Scale = _settings.AssemblyScale,
                Views = new List<DrawingView> { new() { Name = "Assembly", Type = ViewType.Isometric } }
            };
            return Task.FromResult(drawing);
        }

        public Task<ShopDrawing> GeneratePrecastDrawingAsync(PrecastElement element, CancellationToken ct)
        {
            var drawing = new ShopDrawing
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"Precast - {element.PieceMark}",
                PieceMark = element.PieceMark,
                Views = GeneratePrecastViews(element)
            };
            return Task.FromResult(drawing);
        }

        public Task<ShopDrawing> GenerateRebarDrawingAsync(PrecastElement element, CancellationToken ct)
        {
            var drawing = new ShopDrawing
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"Rebar - {element.PieceMark}",
                PieceMark = element.PieceMark
            };
            return Task.FromResult(drawing);
        }

        public Task<ShopDrawing> GenerateFormDrawingAsync(PrecastFormGroup formGroup, CancellationToken ct)
        {
            var drawing = new ShopDrawing
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"Form Drawing - {formGroup.FormType}"
            };
            return Task.FromResult(drawing);
        }

        public Task<ShopDrawing> GenerateSpoolDrawingAsync(MEPSpool spool, CancellationToken ct)
        {
            var drawing = new ShopDrawing
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"Spool - {spool.SpoolMark}",
                PieceMark = spool.SpoolMark
            };
            return Task.FromResult(drawing);
        }

        public Task<ShopDrawing> GenerateIsometricAsync(MEPSystem system, CancellationToken ct)
        {
            var drawing = new ShopDrawing
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"Isometric - {system.Name}",
                Views = new List<DrawingView> { new() { Type = ViewType.Isometric } }
            };
            return Task.FromResult(drawing);
        }

        public Task<ShopDrawing> GeneratePanelDrawingAsync(CurtainWallPanel panel, CancellationToken ct)
        {
            var drawing = new ShopDrawing
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"Panel - {panel.Id}",
                PieceMark = panel.Id
            };
            return Task.FromResult(drawing);
        }

        public Task<ShopDrawing> GenerateMullionDrawingAsync(CurtainWallMullion mullion, CancellationToken ct)
        {
            var drawing = new ShopDrawing
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"Mullion - {mullion.Mark}",
                PieceMark = mullion.Mark
            };
            return Task.FromResult(drawing);
        }

        private List<DrawingView> GenerateSteelViews(SteelMember member)
        {
            return new List<DrawingView>
            {
                new() { Name = "Front", Type = ViewType.Elevation },
                new() { Name = "Top", Type = ViewType.Plan },
                new() { Name = "End", Type = ViewType.Section }
            };
        }

        private List<DrawingView> GeneratePrecastViews(PrecastElement element)
        {
            return new List<DrawingView>
            {
                new() { Name = "Plan", Type = ViewType.Plan },
                new() { Name = "Elevation", Type = ViewType.Elevation },
                new() { Name = "Section A", Type = ViewType.Section },
                new() { Name = "Section B", Type = ViewType.Section }
            };
        }
    }

    internal class CNCExporter
    {
        private readonly FabricationSettings _settings;

        public CNCExporter(FabricationSettings settings) => _settings = settings;

        public Task<CNCFile> ExportSteelMemberAsync(SteelMember member, CancellationToken ct)
        {
            var file = new CNCFile
            {
                FileName = $"{member.PieceMark}.nc1",
                Format = CNCFormat.NC1,
                Content = GenerateNC1Content(member)
            };
            return Task.FromResult(file);
        }

        public Task<CNCFile> ExportMullionAsync(CurtainWallMullion mullion, CancellationToken ct)
        {
            var file = new CNCFile
            {
                FileName = $"{mullion.Mark}.dxf",
                Format = CNCFormat.DXF,
                Content = GenerateMullionDXF(mullion)
            };
            return Task.FromResult(file);
        }

        public Task<CNCFile> ExportGCodeAsync(PlateNestingResult nesting, CNCMachineType machineType, CancellationToken ct)
        {
            var file = new CNCFile
            {
                FileName = $"Nest_{nesting.SheetId}.nc",
                Format = CNCFormat.GCode,
                Content = GenerateGCode(nesting, machineType)
            };
            return Task.FromResult(file);
        }

        private byte[] GenerateNC1Content(SteelMember member)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ST{member.PieceMark}");
            sb.AppendLine($"SI{member.Section}");
            sb.AppendLine($"LN{member.Length:F1}");
            // Add hole and cut data...
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private byte[] GenerateMullionDXF(CurtainWallMullion mullion)
        {
            // Generate DXF content for mullion profile
            return Array.Empty<byte>();
        }

        private byte[] GenerateGCode(PlateNestingResult nesting, CNCMachineType machineType)
        {
            var sb = new StringBuilder();
            sb.AppendLine("%");
            sb.AppendLine($"O{nesting.SheetId}");
            sb.AppendLine("G90 G54");

            foreach (var part in nesting.PlacedParts)
            {
                sb.AppendLine($"(Part: {part.PartId})");
                sb.AppendLine($"G0 X{part.X:F3} Y{part.Y:F3}");
                // Add cutting paths...
            }

            sb.AppendLine("M30");
            sb.AppendLine("%");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }

    internal class NestingOptimizer
    {
        public async Task<List<PlateNestingResult>> OptimizePlateNestingAsync(SteelModel model, CancellationToken ct)
        {
            var results = new List<PlateNestingResult>();

            // Group plates by thickness and grade
            var plateGroups = model.Members
                .Where(m => m.Type == SteelMemberType.Plate)
                .GroupBy(m => new { m.Thickness, m.Grade });

            foreach (var group in plateGroups)
            {
                ct.ThrowIfCancellationRequested();

                var nesting = new PlateNestingResult
                {
                    SheetId = Guid.NewGuid().ToString(),
                    SheetSize = new SheetSize { Width = 2400, Length = 6000 },
                    Thickness = group.Key.Thickness,
                    Grade = group.Key.Grade
                };

                // Simple first-fit decreasing bin packing
                var parts = group.OrderByDescending(p => p.PlateWidth * p.PlateLength).ToList();
                foreach (var part in parts)
                {
                    nesting.PlacedParts.Add(new PlacedPart
                    {
                        PartId = part.PieceMark,
                        X = 0, Y = 0, // Simplified placement
                        Width = part.PlateWidth,
                        Length = part.PlateLength,
                        Rotation = 0
                    });
                }

                nesting.Utilization = CalculateUtilization(nesting);
                results.Add(nesting);
            }

            return await Task.FromResult(results);
        }

        private double CalculateUtilization(PlateNestingResult nesting)
        {
            var usedArea = nesting.PlacedParts.Sum(p => p.Width * p.Length);
            var sheetArea = nesting.SheetSize.Width * nesting.SheetSize.Length;
            return usedArea / sheetArea * 100;
        }
    }

    internal class DXFExporter
    {
        private readonly DXFExportOptions _options;

        public DXFExporter(DXFExportOptions options) => _options = options;

        public Task<byte[]> ExportAsync(ShopDrawing drawing, CancellationToken ct)
        {
            var sb = new StringBuilder();
            sb.AppendLine("0\nSECTION\n2\nHEADER");
            sb.AppendLine("0\nENDSEC");
            sb.AppendLine("0\nSECTION\n2\nENTITIES");
            // Add drawing entities...
            sb.AppendLine("0\nENDSEC\n0\nEOF");
            return Task.FromResult(Encoding.UTF8.GetBytes(sb.ToString()));
        }
    }

    #endregion

    #region Data Models

    public class FabricationSettings
    {
        public string DefaultScale { get; set; } = "1:10";
        public string AssemblyScale { get; set; } = "1:25";
        public double MaxSpoolLength { get; set; } = 6.0; // meters
    }

    public class FabricationProgress
    {
        public string Phase { get; set; }
        public int Percent { get; set; }
    }

    // Steel
    public class SteelFabricationPackage
    {
        public string ProjectId { get; set; }
        public List<ShopDrawing> ShopDrawings { get; } = new();
        public List<ShopDrawing> AssemblyDrawings { get; } = new();
        public List<CNCFile> CNCFiles { get; } = new();
        public CutList CutList { get; set; }
        public BillOfMaterials BillOfMaterials { get; set; }
        public List<PlateNestingResult> NestingSheets { get; set; } = new();
        public FabricationStatistics Statistics { get; set; }
    }

    public class SteelModel
    {
        public string ProjectId { get; set; }
        public List<SteelMember> Members { get; set; } = new();
        public List<SteelConnection> Connections { get; set; } = new();
    }

    public class SteelMember
    {
        public string Id { get; set; }
        public string PieceMark { get; set; }
        public SteelMemberType Type { get; set; }
        public string Section { get; set; }
        public string Grade { get; set; }
        public double Length { get; set; }
        public double Weight { get; set; }
        public double Thickness { get; set; }
        public double PlateWidth { get; set; }
        public double PlateLength { get; set; }
        public bool IsSubMember { get; set; }
        public List<string> ConnectedMemberIds { get; set; } = new();
    }

    public class SteelAssembly
    {
        public string Id { get; set; }
        public List<SteelMember> Members { get; } = new();
    }

    public class SteelConnection
    {
        public string Id { get; set; }
        public List<BoltInfo> Bolts { get; set; } = new();
    }

    public class BoltInfo
    {
        public string Size { get; set; }
        public double Length { get; set; }
        public int Quantity { get; set; }
    }

    // Precast
    public class PrecastFabricationPackage
    {
        public string ProjectId { get; set; }
        public List<ShopDrawing> PieceDrawings { get; } = new();
        public List<ShopDrawing> RebarDrawings { get; } = new();
        public List<ShopDrawing> FormDrawings { get; } = new();
        public List<ErectionDrawing> ErectionDrawings { get; set; } = new();
        public Schedule EmbedSchedule { get; set; }
        public Schedule RebarSchedule { get; set; }
        public Schedule LiftingSchedule { get; set; }
        public FabricationStatistics Statistics { get; set; }
    }

    public class PrecastModel
    {
        public string ProjectId { get; set; }
        public List<PrecastElement> Elements { get; set; } = new();
    }

    public class PrecastElement
    {
        public string Id { get; set; }
        public string PieceMark { get; set; }
        public string FormType { get; set; }
        public double Weight { get; set; }
        public int ErectionSequence { get; set; }
        public string LiftInsertType { get; set; }
        public string RecommendedRigging { get; set; }
        public List<LiftPoint> LiftPoints { get; set; } = new();
        public List<EmbedInfo> Embeds { get; set; } = new();
        public List<RebarInfo> Rebar { get; set; } = new();
    }

    public class PrecastFormGroup
    {
        public string FormType { get; set; }
        public List<PrecastElement> Elements { get; set; } = new();
    }

    public class LiftPoint { public double X { get; set; } public double Y { get; set; } }
    public class EmbedInfo { public string Type { get; set; } public string Size { get; set; } public int Quantity { get; set; } public string Location { get; set; } }
    public class RebarInfo { public string Mark { get; set; } public string Size { get; set; } public string Shape { get; set; } public double Length { get; set; } public int Quantity { get; set; } }

    // MEP
    public class MEPFabricationPackage
    {
        public string ProjectId { get; set; }
        public List<ShopDrawing> SpoolDrawings { get; } = new();
        public List<ShopDrawing> IsometricDrawings { get; } = new();
        public CutList PipeCutList { get; set; }
        public CutList DuctCutList { get; set; }
        public BillOfMaterials BillOfMaterials { get; set; }
        public Schedule HangerSchedule { get; set; }
    }

    public class MEPModel
    {
        public string ProjectId { get; set; }
        public List<MEPSystem> Systems { get; set; } = new();
    }

    public class MEPSystem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public MEPSystemType Type { get; set; }
        public List<MEPRun> Runs { get; set; } = new();
        public List<HangerInfo> Hangers { get; set; } = new();
    }

    public class MEPRun
    {
        public List<MEPSegment> Segments { get; set; } = new();
        public List<FittingInfo> Fittings { get; set; } = new();
    }

    public class MEPSegment
    {
        public string Mark { get; set; }
        public string Size { get; set; }
        public string DuctSize { get; set; }
        public string Material { get; set; }
        public double Length { get; set; }
    }

    public class MEPSpool
    {
        public string SystemId { get; set; }
        public string SpoolMark { get; set; }
        public List<MEPSegment> Segments { get; } = new();
    }

    public class FittingInfo { public string Type { get; set; } public string Size { get; set; } }
    public class HangerInfo { public string Mark { get; set; } public string Type { get; set; } public string Size { get; set; } public string RodSize { get; set; } public string Location { get; set; } }

    // Curtain Wall
    public class CurtainWallFabricationPackage
    {
        public string ProjectId { get; set; }
        public List<ShopDrawing> PanelDrawings { get; } = new();
        public List<ShopDrawing> MullionDrawings { get; } = new();
        public List<CNCFile> CNCFiles { get; } = new();
        public Schedule GlassSchedule { get; set; }
        public Schedule MullionSchedule { get; set; }
        public Schedule HardwareSchedule { get; set; }
        public InstallationSequence InstallationSequence { get; set; }
    }

    public class CurtainWallModel
    {
        public string ProjectId { get; set; }
        public List<CurtainWallPanel> Panels { get; set; } = new();
        public List<CurtainWallMullion> Mullions { get; set; } = new();
    }

    public class CurtainWallPanel
    {
        public string Id { get; set; }
        public int Level { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string GlassType { get; set; }
        public string Coating { get; set; }
        public List<HardwareItem> Hardware { get; set; } = new();
    }

    public class CurtainWallMullion
    {
        public string Mark { get; set; }
        public string Profile { get; set; }
        public double Length { get; set; }
        public string Finish { get; set; }
        public List<HardwareItem> Hardware { get; set; } = new();
    }

    public class HardwareItem { public string Type { get; set; } public string Description { get; set; } public string Finish { get; set; } }

    // Common
    public class ShopDrawing
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string PieceMark { get; set; }
        public string Scale { get; set; }
        public List<DrawingView> Views { get; set; } = new();
    }

    public class DrawingView { public string Name { get; set; } public ViewType Type { get; set; } }
    public class ErectionDrawing { public int SequenceNumber { get; set; } public List<string> Elements { get; set; } }
    public class InstallationSequence { public List<InstallationStep> Steps { get; } = new(); }
    public class InstallationStep { public int StepNumber { get; set; } public string ElementId { get; set; } public string Description { get; set; } }

    public class CNCFile
    {
        public string FileName { get; set; }
        public CNCFormat Format { get; set; }
        public byte[] Content { get; set; }
    }

    public class CutList
    {
        public string Name { get; set; }
        public List<CutListItem> Items { get; } = new();
        public double TotalWeight { get; set; }
    }

    public class CutListItem
    {
        public string PieceMark { get; set; }
        public string Section { get; set; }
        public string Grade { get; set; }
        public double Length { get; set; }
        public int Quantity { get; set; }
        public double Weight { get; set; }
    }

    public class BillOfMaterials
    {
        public List<BOMItem> Items { get; } = new();
        public double TotalWeight { get; set; }
    }

    public class BOMItem
    {
        public string Description { get; set; }
        public string Size { get; set; }
        public double Length { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public double Weight { get; set; }
    }

    public class Schedule
    {
        public string Name { get; set; }
        public string[] Columns { get; set; }
        public List<string[]> Rows { get; } = new();
    }

    public class PlateNestingResult
    {
        public string SheetId { get; set; }
        public SheetSize SheetSize { get; set; }
        public double Thickness { get; set; }
        public string Grade { get; set; }
        public List<PlacedPart> PlacedParts { get; } = new();
        public double Utilization { get; set; }
    }

    public class SheetSize { public double Width { get; set; } public double Length { get; set; } }
    public class PlacedPart { public string PartId { get; set; } public double X { get; set; } public double Y { get; set; } public double Width { get; set; } public double Length { get; set; } public double Rotation { get; set; } }

    public class DXFExportOptions { public string Version { get; set; } = "AC1027"; public bool IncludeDimensions { get; set; } = true; }

    public class FabricationStatistics
    {
        public int TotalDrawings { get; set; }
        public int TotalCNCFiles { get; set; }
        public double TotalWeight { get; set; }
        public int TotalPieces { get; set; }
    }

    // Enums
    public enum SteelMemberType { Column, Beam, Brace, Girt, Purlin, Plate, Misc }
    public enum MEPSystemType { Piping, Ductwork, Electrical }
    public enum ViewType { Plan, Elevation, Section, Isometric, Detail }
    public enum CNCFormat { NC1, DSTV, GCode, DXF }
    public enum CNCMachineType { Plasma, Laser, Waterjet, Punch }

    #endregion
}
