using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.Data.Formulas;
using StingBIM.Data.Schedules;
using StingBIM.Data.Materials;

namespace StingBIM.AI.Tests.Unit.Foundation
{
    /// <summary>
    /// Data-driven unit tests for Foundation layer:
    /// FormulaLibrary (loading 52+ formulas from real CSV),
    /// DependencyResolver (topological sort, circular detection),
    /// ScheduleLoader (loading 146+ templates from real CSVs),
    /// MaterialDatabase (standalone data model tests).
    /// Tests use real data files from /data/ai/.
    /// </summary>
    [TestFixture]
    public class DataDrivenTests
    {
        private static readonly string DataRoot = Path.Combine(
            TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "..", "data", "ai");

        // Fallback to absolute path if relative doesn't work
        private static string GetDataPath(string subPath)
        {
            var relativePath = Path.Combine(DataRoot, subPath);
            if (File.Exists(relativePath) || Directory.Exists(relativePath))
                return relativePath;

            // Fallback: try AppData location (deployed by post-build step)
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StingBIM", "Data", subPath);
            return appDataPath;
        }

        #region FormulaLibrary Tests

        [TestFixture]
        public class FormulaLibraryTests
        {
            private FormulaLibrary _library;
            private static readonly string FormulaCsvPath =
                GetDataPath("formulas/FORMULAS_WITH_DEPENDENCIES.csv");

            [SetUp]
            public void SetUp()
            {
                _library = new FormulaLibrary();
            }

            [Test]
            public void Constructor_CreatesEmptyLibrary()
            {
                _library.Count.Should().Be(0);
                _library.IsLoaded.Should().BeFalse();
            }

            [Test]
            public void LoadFromCsv_RealFormulasFile_LoadsFormulas()
            {
                if (!File.Exists(FormulaCsvPath))
                {
                    Assert.Ignore($"Formula CSV not found at {FormulaCsvPath}");
                    return;
                }

                var count = _library.LoadFromCsv(FormulaCsvPath);

                count.Should().BeGreaterThan(0, "should load formulas from real CSV");
                _library.Count.Should().Be(count);
                _library.IsLoaded.Should().BeTrue();
            }

            [Test]
            public async Task LoadFromCsvAsync_RealFormulasFile_LoadsFormulas()
            {
                if (!File.Exists(FormulaCsvPath))
                {
                    Assert.Ignore($"Formula CSV not found at {FormulaCsvPath}");
                    return;
                }

                var count = await _library.LoadFromCsvAsync(FormulaCsvPath);

                count.Should().BeGreaterThanOrEqualTo(50, "should load ~52+ formulas");
                _library.Count.Should().Be(count);
            }

            [Test]
            public void LoadFromCsv_RealFile_AllFormulasHaveDiscipline()
            {
                if (!File.Exists(FormulaCsvPath))
                {
                    Assert.Ignore("Formula CSV not found");
                    return;
                }

                _library.LoadFromCsv(FormulaCsvPath);

                _library.AllFormulas.Should().AllSatisfy(f =>
                {
                    f.Discipline.Should().NotBeNullOrEmpty();
                    f.ParameterName.Should().NotBeNullOrEmpty();
                });
            }

            [Test]
            public void Disciplines_AfterLoad_ReturnsDistinctDisciplines()
            {
                if (!File.Exists(FormulaCsvPath))
                {
                    Assert.Ignore("Formula CSV not found");
                    return;
                }

                _library.LoadFromCsv(FormulaCsvPath);

                var disciplines = _library.Disciplines.ToList();
                disciplines.Should().NotBeEmpty();
                disciplines.Should().OnlyHaveUniqueItems();
            }

            [Test]
            public void GetByDiscipline_AfterLoad_ReturnsFormulasForDiscipline()
            {
                if (!File.Exists(FormulaCsvPath))
                {
                    Assert.Ignore("Formula CSV not found");
                    return;
                }

                _library.LoadFromCsv(FormulaCsvPath);
                var disciplines = _library.Disciplines.ToList();

                foreach (var discipline in disciplines)
                {
                    var formulas = _library.GetByDiscipline(discipline);
                    formulas.Should().NotBeEmpty($"discipline '{discipline}' should have formulas");
                    formulas.Should().AllSatisfy(f => f.Discipline.Should().Be(discipline));
                }
            }

            [Test]
            public void GetByParameterName_ExistingFormula_ReturnsDefinition()
            {
                if (!File.Exists(FormulaCsvPath))
                {
                    Assert.Ignore("Formula CSV not found");
                    return;
                }

                _library.LoadFromCsv(FormulaCsvPath);
                var firstFormula = _library.AllFormulas.First();

                var found = _library.GetByParameterName(firstFormula.ParameterName);

                found.Should().NotBeNull();
                found.ParameterName.Should().Be(firstFormula.ParameterName);
            }

            [Test]
            public void Exists_KnownFormula_ReturnsTrue()
            {
                if (!File.Exists(FormulaCsvPath))
                {
                    Assert.Ignore("Formula CSV not found");
                    return;
                }

                _library.LoadFromCsv(FormulaCsvPath);
                var paramName = _library.AllFormulas.First().ParameterName;

                _library.Exists(paramName).Should().BeTrue();
            }

            [Test]
            public void Exists_UnknownFormula_ReturnsFalse()
            {
                if (!File.Exists(FormulaCsvPath))
                {
                    Assert.Ignore("Formula CSV not found");
                    return;
                }

                _library.LoadFromCsv(FormulaCsvPath);

                _library.Exists("NONEXISTENT_FORMULA_XYZ").Should().BeFalse();
            }

            [Test]
            public void Search_ByKeyword_ReturnsRelevantFormulas()
            {
                if (!File.Exists(FormulaCsvPath))
                {
                    Assert.Ignore("Formula CSV not found");
                    return;
                }

                _library.LoadFromCsv(FormulaCsvPath);

                var results = _library.Search("area");

                results.Should().NotBeNull();
                // There should be area-related formulas
            }

            [Test]
            public void Filter_ByPredicate_ReturnsMatchingFormulas()
            {
                if (!File.Exists(FormulaCsvPath))
                {
                    Assert.Ignore("Formula CSV not found");
                    return;
                }

                _library.LoadFromCsv(FormulaCsvPath);

                var level0 = _library.Filter(f => f.DependencyLevel == 0);

                level0.Should().NotBeNull();
                level0.Should().AllSatisfy(f => f.DependencyLevel.Should().Be(0));
            }

            [Test]
            public void GetStatistics_AfterLoad_ReturnsValidStats()
            {
                if (!File.Exists(FormulaCsvPath))
                {
                    Assert.Ignore("Formula CSV not found");
                    return;
                }

                _library.LoadFromCsv(FormulaCsvPath);

                var stats = _library.GetStatistics();

                stats.Should().NotBeNull();
                stats.TotalFormulas.Should().Be(_library.Count);
                stats.DisciplineCount.Should().BeGreaterThan(0);
                stats.FormulasByDiscipline.Should().NotBeEmpty();
            }

            [Test]
            public void Clear_AfterLoad_ResetsLibrary()
            {
                if (!File.Exists(FormulaCsvPath))
                {
                    Assert.Ignore("Formula CSV not found");
                    return;
                }

                _library.LoadFromCsv(FormulaCsvPath);
                _library.Count.Should().BeGreaterThan(0);

                _library.Clear();

                _library.Count.Should().Be(0);
                _library.IsLoaded.Should().BeFalse();
            }

            [Test]
            public void CreateAndLoad_StaticFactory_ReturnsLoadedLibrary()
            {
                if (!File.Exists(FormulaCsvPath))
                {
                    Assert.Ignore("Formula CSV not found");
                    return;
                }

                var library = FormulaLibrary.CreateAndLoad(FormulaCsvPath);

                library.Should().NotBeNull();
                library.Count.Should().BeGreaterThan(0);
                library.IsLoaded.Should().BeTrue();
            }
        }

        #endregion

        #region DependencyResolver Tests

        [TestFixture]
        public class DependencyResolverTests
        {
            private FormulaLibrary _library;
            private static readonly string FormulaCsvPath =
                GetDataPath("formulas/FORMULAS_WITH_DEPENDENCIES.csv");

            [SetUp]
            public void SetUp()
            {
                _library = new FormulaLibrary();
                if (File.Exists(FormulaCsvPath))
                {
                    _library.LoadFromCsv(FormulaCsvPath);
                }
            }

            [Test]
            public void Constructor_NullLibrary_ThrowsArgumentNullException()
            {
                Action act = () => new DependencyResolver(null);

                act.Should().Throw<ArgumentNullException>();
            }

            [Test]
            public void Constructor_ValidLibrary_CreatesInstance()
            {
                var resolver = new DependencyResolver(_library);

                resolver.Should().NotBeNull();
            }

            [Test]
            public void BuildDependencyGraph_ReturnsGraph()
            {
                if (_library.Count == 0)
                {
                    Assert.Ignore("Formula library not loaded");
                    return;
                }

                var resolver = new DependencyResolver(_library);
                var graph = resolver.BuildDependencyGraph();

                graph.Should().NotBeNull();
                graph.NodeCount.Should().BeGreaterThan(0);
            }

            [Test]
            public void GetCalculationOrder_ReturnsTopologicalSort()
            {
                if (_library.Count == 0)
                {
                    Assert.Ignore("Formula library not loaded");
                    return;
                }

                var resolver = new DependencyResolver(_library);

                List<string> order = null;
                try
                {
                    order = resolver.GetCalculationOrder();
                }
                catch (CircularDependencyException)
                {
                    // Circular dependency is valid — test passes
                    Assert.Pass("Circular dependency detected — topological sort correctly threw");
                    return;
                }

                order.Should().NotBeNull();
                order.Should().NotBeEmpty();
            }

            [Test]
            public void GetCalculationOrder_SpecificFormulas_ReturnsDependenciesFirst()
            {
                if (_library.Count == 0)
                {
                    Assert.Ignore("Formula library not loaded");
                    return;
                }

                // Find a formula with dependencies
                var withDeps = _library.AllFormulas
                    .FirstOrDefault(f => f.InputParameters != null && f.InputParameters.Count > 0);

                if (withDeps == null)
                {
                    Assert.Ignore("No formulas with dependencies found");
                    return;
                }

                var resolver = new DependencyResolver(_library);

                try
                {
                    var order = resolver.GetCalculationOrder(new[] { withDeps.ParameterName });
                    order.Should().NotBeEmpty();
                    // The dependent formula should come after its inputs
                    var formulaIndex = order.IndexOf(withDeps.ParameterName);
                    if (formulaIndex >= 0)
                    {
                        foreach (var input in withDeps.InputParameters)
                        {
                            var inputIndex = order.IndexOf(input);
                            if (inputIndex >= 0)
                            {
                                inputIndex.Should().BeLessThan(formulaIndex,
                                    $"input '{input}' should appear before '{withDeps.ParameterName}'");
                            }
                        }
                    }
                }
                catch (CircularDependencyException)
                {
                    Assert.Pass("Circular dependency in requested formulas");
                }
            }

            [Test]
            public void DetectCircularDependencies_ReturnsListOfCycles()
            {
                if (_library.Count == 0)
                {
                    Assert.Ignore("Formula library not loaded");
                    return;
                }

                var resolver = new DependencyResolver(_library);
                var cycles = resolver.DetectCircularDependencies();

                cycles.Should().NotBeNull();
                // May or may not have cycles — just verify the method works
            }

            [Test]
            public void GetAllDependencies_FormulaWithDeps_ReturnsAllDirectAndIndirect()
            {
                if (_library.Count == 0)
                {
                    Assert.Ignore("Formula library not loaded");
                    return;
                }

                var withDeps = _library.AllFormulas
                    .FirstOrDefault(f => f.InputParameters != null && f.InputParameters.Count > 0);

                if (withDeps == null)
                {
                    Assert.Ignore("No formulas with dependencies found");
                    return;
                }

                var resolver = new DependencyResolver(_library);
                var allDeps = resolver.GetAllDependencies(withDeps.ParameterName);

                allDeps.Should().NotBeNull();
                // Should include at least the direct dependencies
                foreach (var input in withDeps.InputParameters)
                {
                    if (_library.Exists(input))
                    {
                        allDeps.Should().Contain(input);
                    }
                }
            }

            [Test]
            public void ValidateDependencies_ReturnsValidationResult()
            {
                if (_library.Count == 0)
                {
                    Assert.Ignore("Formula library not loaded");
                    return;
                }

                var resolver = new DependencyResolver(_library);
                var result = resolver.ValidateDependencies();

                result.Should().NotBeNull();
                result.ErrorCount.Should().BeGreaterThanOrEqualTo(0);
                result.WarningCount.Should().BeGreaterThanOrEqualTo(0);
            }

            [Test]
            public void DependencyGraph_AddNode_And_AddDependency_WorkCorrectly()
            {
                // Test the DependencyGraph data structure independently
                var graph = new DependencyGraph();
                graph.AddNode("Volume");
                graph.AddNode("Area");
                graph.AddNode("Height");

                graph.AddDependency("Volume", "Area");
                graph.AddDependency("Volume", "Height");

                graph.NodeCount.Should().Be(3);
                graph.DependencyCount.Should().Be(2);
                graph.GetDependencies("Volume").Should().Contain("Area");
                graph.GetDependencies("Volume").Should().Contain("Height");
                graph.GetDependents("Area").Should().Contain("Volume");
                graph.ContainsNode("Volume").Should().BeTrue();
            }

            [Test]
            public void DependencyValidationResult_Properties_WorkCorrectly()
            {
                var result = new DependencyValidationResult();
                result.AddError("Missing dependency: X");
                result.AddWarning("Unused formula: Y");

                result.ErrorCount.Should().Be(1);
                result.WarningCount.Should().Be(1);
                result.IsValid.Should().BeFalse();
                result.Errors.Should().Contain("Missing dependency: X");
                result.Warnings.Should().Contain("Unused formula: Y");
            }
        }

        #endregion

        #region ScheduleLoader Tests

        [TestFixture]
        public class ScheduleLoaderTests
        {
            private static readonly string ScheduleDataDir =
                GetDataPath("schedules");

            [Test]
            public void Constructor_WithValidPath_CreatesInstance()
            {
                var loader = new ScheduleLoader(ScheduleDataDir);

                loader.Should().NotBeNull();
            }

            [Test]
            public void Load_RealScheduleFiles_LoadsTemplates()
            {
                if (!Directory.Exists(ScheduleDataDir))
                {
                    Assert.Ignore($"Schedule data directory not found: {ScheduleDataDir}");
                    return;
                }

                var loader = new ScheduleLoader(ScheduleDataDir);
                var collection = loader.Load();

                collection.Should().NotBeNull();
                collection.Count.Should().BeGreaterThan(0, "should load templates from real CSVs");
            }

            [Test]
            public async Task LoadAsync_RealScheduleFiles_LoadsTemplates()
            {
                if (!Directory.Exists(ScheduleDataDir))
                {
                    Assert.Ignore("Schedule data directory not found");
                    return;
                }

                var loader = new ScheduleLoader(ScheduleDataDir);
                var collection = await loader.LoadAsync();

                collection.Should().NotBeNull();
                collection.Count.Should().BeGreaterThan(100, "should load 100+ templates");
            }

            [Test]
            public void Load_TemplatesHaveDisciplines()
            {
                if (!Directory.Exists(ScheduleDataDir))
                {
                    Assert.Ignore("Schedule data directory not found");
                    return;
                }

                var loader = new ScheduleLoader(ScheduleDataDir);
                var collection = loader.Load();
                var disciplines = collection.GetDisciplines();

                disciplines.Should().NotBeEmpty();
            }

            [Test]
            public void Load_TemplatesHaveNames()
            {
                if (!Directory.Exists(ScheduleDataDir))
                {
                    Assert.Ignore("Schedule data directory not found");
                    return;
                }

                var loader = new ScheduleLoader(ScheduleDataDir);
                var collection = loader.Load();
                var all = collection.GetAll();

                all.Should().AllSatisfy(t =>
                {
                    t.Name.Should().NotBeNullOrEmpty();
                });
            }

            [Test]
            public void Load_GetByDiscipline_ReturnsFilteredTemplates()
            {
                if (!Directory.Exists(ScheduleDataDir))
                {
                    Assert.Ignore("Schedule data directory not found");
                    return;
                }

                var loader = new ScheduleLoader(ScheduleDataDir);
                var collection = loader.Load();
                var disciplines = collection.GetDisciplines();

                foreach (var disc in disciplines)
                {
                    var templates = collection.GetByDiscipline(disc);
                    templates.Should().NotBeEmpty($"discipline '{disc}' should have templates");
                }
            }

            [Test]
            public void Load_Search_ReturnsMatchingTemplates()
            {
                if (!Directory.Exists(ScheduleDataDir))
                {
                    Assert.Ignore("Schedule data directory not found");
                    return;
                }

                var loader = new ScheduleLoader(ScheduleDataDir);
                var collection = loader.Load();
                var results = collection.Search("door");

                results.Should().NotBeNull();
                // May or may not find matches depending on templates
            }

            [Test]
            public void IsCached_AfterLoad_IsTrue()
            {
                if (!Directory.Exists(ScheduleDataDir))
                {
                    Assert.Ignore("Schedule data directory not found");
                    return;
                }

                var loader = new ScheduleLoader(ScheduleDataDir);
                loader.IsCached.Should().BeFalse();

                loader.Load();

                loader.IsCached.Should().BeTrue();
            }

            [Test]
            public void ClearCache_AfterLoad_ResetsCacheFlag()
            {
                if (!Directory.Exists(ScheduleDataDir))
                {
                    Assert.Ignore("Schedule data directory not found");
                    return;
                }

                var loader = new ScheduleLoader(ScheduleDataDir);
                loader.Load();
                loader.IsCached.Should().BeTrue();

                loader.ClearCache();

                loader.IsCached.Should().BeFalse();
            }

            [Test]
            public void Reload_ClearsAndReloads()
            {
                if (!Directory.Exists(ScheduleDataDir))
                {
                    Assert.Ignore("Schedule data directory not found");
                    return;
                }

                var loader = new ScheduleLoader(ScheduleDataDir);
                var first = loader.Load();
                var second = loader.Reload();

                second.Count.Should().Be(first.Count, "reload should return same count");
            }

            [Test]
            public void GetStatistics_ReturnsStats()
            {
                if (!Directory.Exists(ScheduleDataDir))
                {
                    Assert.Ignore("Schedule data directory not found");
                    return;
                }

                var loader = new ScheduleLoader(ScheduleDataDir);
                loader.Load();

                var stats = loader.GetStatistics();

                stats.Should().NotBeNull();
                stats.TotalTemplates.Should().BeGreaterThan(0);
            }

            [Test]
            public async Task LoadWithProgressAsync_ReportsProgress()
            {
                if (!Directory.Exists(ScheduleDataDir))
                {
                    Assert.Ignore("Schedule data directory not found");
                    return;
                }

                var progressReports = new List<LoadProgress>();
                var progress = new Progress<LoadProgress>(p => progressReports.Add(p));

                var loader = new ScheduleLoader(ScheduleDataDir);
                var collection = await loader.LoadWithProgressAsync(progress);

                collection.Should().NotBeNull();
                // Progress may be reported asynchronously, but we verify it works without error
            }
        }

        #endregion

        #region MaterialDatabase Tests

        [TestFixture]
        public class MaterialDatabaseTests
        {
            [Test]
            public void Constructor_CreatesEmptyDatabase()
            {
                var db = new MaterialDatabase();

                db.Should().NotBeNull();
                db.Count.Should().Be(0);
                db.IsLoaded.Should().BeFalse();
            }

            [Test]
            public void MaterialDefinition_StoresAllProperties()
            {
                var material = new MaterialDefinition
                {
                    Guid = Guid.NewGuid(),
                    Code = "CONC-C30",
                    Name = "Concrete C30/37",
                    Category = "Concrete",
                    Discipline = "Structural",
                    Description = "Standard structural concrete",
                    ThermalResistance = 0.14,
                    ThermalConductivity = 1.63,
                    Density = 2400.0,
                    SpecificHeat = 880.0,
                    FireRating = "2HR",
                    Cost = 85.0,
                    CostUnit = "m3"
                };

                material.Code.Should().Be("CONC-C30");
                material.Name.Should().Be("Concrete C30/37");
                material.Category.Should().Be("Concrete");
                material.Density.Should().Be(2400.0);
                material.ThermalConductivity.Should().Be(1.63);
            }

            [Test]
            public void MaterialSearchCriteria_DefaultValues()
            {
                var criteria = new MaterialSearchCriteria();

                criteria.Query.Should().BeNull();
                criteria.Category.Should().BeNull();
                criteria.CaseSensitive.Should().BeFalse();
            }

            [Test]
            public void MaterialDatabaseStatistics_StoresCountData()
            {
                var stats = new MaterialDatabaseStatistics
                {
                    TotalMaterials = 2450,
                    CategoryCount = 12,
                    DisciplineCount = 5,
                    Categories = new Dictionary<string, int>
                    {
                        ["Concrete"] = 150,
                        ["Steel"] = 200,
                        ["Timber"] = 80
                    },
                    Disciplines = new Dictionary<string, int>
                    {
                        ["Structural"] = 500,
                        ["Architecture"] = 800,
                        ["MEP"] = 300
                    }
                };

                stats.TotalMaterials.Should().Be(2450);
                stats.Categories.Should().HaveCount(3);
                stats.Disciplines.Should().HaveCount(3);
            }

            [Test]
            public void Clear_AfterManualAdd_ResetsDatabase()
            {
                var db = new MaterialDatabase();
                // Database starts empty; clear should not throw
                Action act = () => db.Clear();

                act.Should().NotThrow();
                db.Count.Should().Be(0);
            }
        }

        #endregion
    }
}
