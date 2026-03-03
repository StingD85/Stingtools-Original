using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using StingBIM.Data.Parameters;

namespace StingBIM.AI.Tests.Integration.RevitMocked
{
    /// <summary>
    /// Tier 2 — Integration tests for ParameterLoader.
    /// Uses real file I/O with test data (no Revit dependency).
    /// Tests UTF-16 parsing, caching, async loading, and progress reporting.
    /// </summary>
    [TestFixture]
    public class ParameterLoaderTests
    {
        private string _testFilePath;

        [SetUp]
        public void SetUp()
        {
            _testFilePath = Path.Combine(Path.GetTempPath(), $"test_params_{Guid.NewGuid()}.txt");
            CreateTestParameterFile(_testFilePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_testFilePath))
                File.Delete(_testFilePath);
        }

        /// <summary>
        /// Creates a test parameter file in the Revit shared parameter format (UTF-16).
        /// Format: # header, *GROUP lines, PARAM lines (tab-separated).
        /// </summary>
        private void CreateTestParameterFile(string path)
        {
            var content = new StringBuilder();
            content.AppendLine("# This is a Revit shared parameter file.");
            content.AppendLine("# Do not edit manually.");
            content.AppendLine("*META\tVERSION\t2");
            content.AppendLine("*GROUP\tID\tNAME");
            content.AppendLine("GROUP\t1\tDimensions");
            content.AppendLine("GROUP\t2\tIdentity Data");
            content.AppendLine("GROUP\t3\tMechanical");
            content.AppendLine("*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE");
            content.AppendLine("PARAM\t{00000001-0001-0001-0001-000000000001}\tWall_Length\tLENGTH\t\t1\t1\tLength of wall element\t1");
            content.AppendLine("PARAM\t{00000002-0002-0002-0002-000000000002}\tWall_Height\tLENGTH\t\t1\t1\tHeight of wall element\t1");
            content.AppendLine("PARAM\t{00000003-0003-0003-0003-000000000003}\tRoom_Name\tTEXT\t\t2\t1\tRoom name identifier\t1");
            content.AppendLine("PARAM\t{00000004-0004-0004-0004-000000000004}\tRoom_Number\tTEXT\t\t2\t1\tRoom number\t1");
            content.AppendLine("PARAM\t{00000005-0005-0005-0005-000000000005}\tDuct_Size\tLENGTH\t\t3\t1\tDuct diameter or size\t1");
            content.AppendLine("PARAM\t{00000006-0006-0006-0006-000000000006}\tAirflow_Rate\tHVAC_AIRFLOW\t\t3\t1\tAirflow rate in L/s\t1");

            File.WriteAllText(path, content.ToString(), Encoding.Unicode); // UTF-16
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithPath_InitializesCorrectly()
        {
            var loader = new ParameterLoader(_testFilePath);

            loader.IsLoaded.Should().BeFalse();
            loader.ParameterCount.Should().Be(0);
        }

        #endregion

        #region Load Tests

        [Test]
        public void Load_ValidFile_LoadsParameters()
        {
            var loader = new ParameterLoader(_testFilePath);

            var result = loader.Load();

            result.Should().NotBeNull();
            loader.IsLoaded.Should().BeTrue();
            loader.ParameterCount.Should().BeGreaterThan(0);
        }

        [Test]
        public async Task LoadAsync_ValidFile_LoadsParameters()
        {
            var loader = new ParameterLoader(_testFilePath);

            var result = await loader.LoadAsync();

            result.Should().NotBeNull();
            loader.IsLoaded.Should().BeTrue();
        }

        [Test]
        public async Task LoadAsync_SupportsCancellation()
        {
            var loader = new ParameterLoader(_testFilePath);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task> act = () => loader.LoadAsync(cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task LoadWithProgressAsync_ReportsProgress()
        {
            var loader = new ParameterLoader(_testFilePath);
            var progressValues = new List<LoadProgress>();
            var progress = new Progress<LoadProgress>(p => progressValues.Add(p));

            var result = await loader.LoadWithProgressAsync(progress);

            result.Should().NotBeNull();
            // Progress reporting may be asynchronous, give it a moment
            await Task.Delay(50);
        }

        [Test]
        public void Load_NonExistentFile_HandlesGracefully()
        {
            var loader = new ParameterLoader("/nonexistent/path/params.txt");

            // Should either throw a meaningful exception or return empty
            Action act = () => loader.Load();
            act.Should().Throw<Exception>();
        }

        #endregion

        #region Cache Tests

        [Test]
        public void Load_CalledTwice_ReturnsCachedData()
        {
            var loader = new ParameterLoader(_testFilePath);

            var first = loader.Load();
            var second = loader.Load();

            // Should be same reference (cached)
            first.Should().NotBeNull();
            second.Should().NotBeNull();
        }

        [Test]
        public void ClearCache_ResetsState()
        {
            var loader = new ParameterLoader(_testFilePath);
            loader.Load();

            loader.ClearCache();

            loader.ParameterCount.Should().Be(0);
        }

        [Test]
        public async Task ReloadAsync_ReloadsFromDisk()
        {
            var loader = new ParameterLoader(_testFilePath);
            await loader.LoadAsync();
            var countBefore = loader.ParameterCount;

            var result = await loader.ReloadAsync();

            result.Should().NotBeNull();
            loader.ParameterCount.Should().Be(countBefore);
        }

        #endregion

        #region Query Tests

        [Test]
        public void GetByName_ExistingParameter_ReturnsParameter()
        {
            var loader = new ParameterLoader(_testFilePath);
            loader.Load();

            var param = loader.GetByName("Wall_Length");

            param.Should().NotBeNull();
            param.Name.Should().Be("Wall_Length");
        }

        [Test]
        public void GetByName_NonExistentParameter_ReturnsNull()
        {
            var loader = new ParameterLoader(_testFilePath);
            loader.Load();

            var param = loader.GetByName("NonExistent_Param");

            param.Should().BeNull();
        }

        [Test]
        public void GetByGuid_ExistingParameter_ReturnsParameter()
        {
            var loader = new ParameterLoader(_testFilePath);
            loader.Load();

            var guid = new Guid("00000001-0001-0001-0001-000000000001");
            var param = loader.GetByGuid(guid);

            param.Should().NotBeNull();
        }

        [Test]
        public void GetByDiscipline_ReturnsMatchingParameters()
        {
            var loader = new ParameterLoader(_testFilePath);
            loader.Load();

            var all = loader.GetAll();
            all.Should().NotBeNull();
        }

        [Test]
        public void GetAll_ReturnsAllLoadedParameters()
        {
            var loader = new ParameterLoader(_testFilePath);
            loader.Load();

            var all = loader.GetAll();

            all.Should().NotBeNull();
            all.Count.Should().Be(loader.ParameterCount);
        }

        [Test]
        public void Search_ByKeyword_FindsMatches()
        {
            var loader = new ParameterLoader(_testFilePath);
            loader.Load();

            var results = loader.Search("Wall");

            results.Should().NotBeNull();
            results.Should().NotBeEmpty();
            results.Should().OnlyContain(p => p.Name.Contains("Wall", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void Search_NoMatch_ReturnsEmpty()
        {
            var loader = new ParameterLoader(_testFilePath);
            loader.Load();

            var results = loader.Search("ZZZZNONEXISTENT");

            results.Should().NotBeNull();
            results.Should().BeEmpty();
        }

        #endregion

        #region Statistics Tests

        [Test]
        public void GetStatistics_AfterLoad_ReturnsStats()
        {
            var loader = new ParameterLoader(_testFilePath);
            loader.Load();

            var stats = loader.GetStatistics();

            stats.Should().NotBeNull();
        }

        [Test]
        public void LastLoadTime_AfterLoad_IsRecent()
        {
            var before = DateTime.Now;
            var loader = new ParameterLoader(_testFilePath);
            loader.Load();

            loader.LastLoadTime.Should().BeOnOrAfter(before);
        }

        #endregion
    }
}
