using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace StingBIM.AI.Tests.Data
{
    /// <summary>
    /// Tests for specialty systems data validation (Fire, Accessibility, Site Work, Drainage)
    /// </summary>
    public class SpecialtySystemsTests
    {
        private readonly string _dataPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..", "data", "specialty");

        #region Fire Protection Tests

        [Fact]
        public void FireProtection_ShouldHaveValidStructure()
        {
            var filePath = Path.Combine(_dataPath, "FIRE_PROTECTION_SYSTEMS.csv");
            Assert.True(File.Exists(filePath), "Fire protection file should exist");

            var lines = File.ReadAllLines(filePath);
            Assert.True(lines.Length > 20, "Should have substantial fire protection data");

            var headers = lines[0].Split(',');
            Assert.Contains("system_code", headers);
            Assert.Contains("system_name", headers);
            Assert.Contains("category", headers);
            Assert.Contains("nfpa_reference", headers);
        }

        [Fact]
        public void FireProtection_ShouldHaveUniqueCodes()
        {
            var filePath = Path.Combine(_dataPath, "FIRE_PROTECTION_SYSTEMS.csv");
            var lines = File.ReadAllLines(filePath).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));
            var codes = lines.Select(l => l.Split(',')[0]).ToList();

            var duplicates = codes.GroupBy(c => c)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.Empty(duplicates);
        }

        [Fact]
        public void FireProtection_ShouldHaveValidCategories()
        {
            var validCategories = new[] { "Suppression", "Detection", "Passive", "Egress", "Mechanical", "Portable" };
            var filePath = Path.Combine(_dataPath, "FIRE_PROTECTION_SYSTEMS.csv");
            var lines = File.ReadAllLines(filePath).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));

            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length > 2)
                {
                    Assert.Contains(parts[2], validCategories);
                }
            }
        }

        #endregion

        #region Accessibility Tests

        [Fact]
        public void Accessibility_ShouldHaveValidStructure()
        {
            var filePath = Path.Combine(_dataPath, "ACCESSIBILITY_REQUIREMENTS.csv");
            Assert.True(File.Exists(filePath), "Accessibility file should exist");

            var lines = File.ReadAllLines(filePath);
            Assert.True(lines.Length > 50, "Should have comprehensive accessibility data");

            var headers = lines[0].Split(',');
            Assert.Contains("requirement_code", headers);
            Assert.Contains("standard_reference", headers);
            Assert.Contains("min_dimension_mm", headers);
        }

        [Fact]
        public void Accessibility_ShouldHaveUniqueCodes()
        {
            var filePath = Path.Combine(_dataPath, "ACCESSIBILITY_REQUIREMENTS.csv");
            var lines = File.ReadAllLines(filePath).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));
            var codes = lines.Select(l => l.Split(',')[0]).ToList();

            var duplicates = codes.GroupBy(c => c)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.Empty(duplicates);
        }

        [Fact]
        public void Accessibility_ShouldReferenceADAStandards()
        {
            var filePath = Path.Combine(_dataPath, "ACCESSIBILITY_REQUIREMENTS.csv");
            var lines = File.ReadAllLines(filePath).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));

            var adaReferences = lines.Count(l => l.Contains("ADA"));
            Assert.True(adaReferences > 50, "Should have substantial ADA references");
        }

        [Fact]
        public void Accessibility_DimensionsShouldBeReasonable()
        {
            var filePath = Path.Combine(_dataPath, "ACCESSIBILITY_REQUIREMENTS.csv");
            var lines = File.ReadAllLines(filePath).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));

            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length > 6 && decimal.TryParse(parts[5], out var minDim))
                {
                    // Dimensions should be reasonable (1mm to 10000mm)
                    if (minDim > 0)
                    {
                        Assert.InRange(minDim, 1, 10000);
                    }
                }
            }
        }

        #endregion

        #region Site Work Tests

        [Fact]
        public void SiteWork_ShouldHaveValidStructure()
        {
            var filePath = Path.Combine(_dataPath, "SITE_WORK_SPECIFICATIONS.csv");
            Assert.True(File.Exists(filePath), "Site work file should exist");

            var lines = File.ReadAllLines(filePath);
            Assert.True(lines.Length > 40, "Should have substantial site work data");

            var headers = lines[0].Split(',');
            Assert.Contains("work_code", headers);
            Assert.Contains("unit_rate_ugx", headers);
            Assert.Contains("category", headers);
        }

        [Fact]
        public void SiteWork_ShouldHaveUniqueCodes()
        {
            var filePath = Path.Combine(_dataPath, "SITE_WORK_SPECIFICATIONS.csv");
            var lines = File.ReadAllLines(filePath).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));
            var codes = lines.Select(l => l.Split(',')[0]).ToList();

            var duplicates = codes.GroupBy(c => c)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.Empty(duplicates);
        }

        [Fact]
        public void SiteWork_ShouldCoverMajorCategories()
        {
            var requiredCategories = new[] { "Clearing", "Earthworks", "Compaction", "Grading", "Paving", "Landscaping" };
            var filePath = Path.Combine(_dataPath, "SITE_WORK_SPECIFICATIONS.csv");
            var content = File.ReadAllText(filePath);

            foreach (var category in requiredCategories)
            {
                Assert.Contains(category, content);
            }
        }

        #endregion

        #region Drainage Tests

        [Fact]
        public void Drainage_ShouldHaveValidStructure()
        {
            var filePath = Path.Combine(_dataPath, "DRAINAGE_SYSTEMS.csv");
            Assert.True(File.Exists(filePath), "Drainage file should exist");

            var lines = File.ReadAllLines(filePath);
            Assert.True(lines.Length > 40, "Should have substantial drainage data");

            var headers = lines[0].Split(',');
            Assert.Contains("drainage_code", headers);
            Assert.Contains("category", headers);
            Assert.Contains("capacity_lps", headers);
        }

        [Fact]
        public void Drainage_ShouldHaveUniqueCodes()
        {
            var filePath = Path.Combine(_dataPath, "DRAINAGE_SYSTEMS.csv");
            var lines = File.ReadAllLines(filePath).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));
            var codes = lines.Select(l => l.Split(',')[0]).ToList();

            var duplicates = codes.GroupBy(c => c)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.Empty(duplicates);
        }

        [Fact]
        public void Drainage_ShouldCoverMajorCategories()
        {
            var requiredCategories = new[] { "Surface", "Collection", "Piping", "Subsurface", "Roof", "SuDS" };
            var filePath = Path.Combine(_dataPath, "DRAINAGE_SYSTEMS.csv");
            var content = File.ReadAllText(filePath);

            foreach (var category in requiredCategories)
            {
                Assert.Contains(category, content);
            }
        }

        [Fact]
        public void Drainage_PipeSlopesShouldBeReasonable()
        {
            var filePath = Path.Combine(_dataPath, "DRAINAGE_SYSTEMS.csv");
            var lines = File.ReadAllLines(filePath).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));

            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length > 6 && decimal.TryParse(parts[6], out var slope))
                {
                    // Pipe slopes typically 0.1% to 5%
                    if (slope > 0)
                    {
                        Assert.InRange(slope, 0.1m, 5.0m);
                    }
                }
            }
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void AllSpecialtyFiles_ShouldExist()
        {
            var requiredFiles = new[]
            {
                "FIRE_PROTECTION_SYSTEMS.csv",
                "ACCESSIBILITY_REQUIREMENTS.csv",
                "SITE_WORK_SPECIFICATIONS.csv",
                "DRAINAGE_SYSTEMS.csv"
            };

            foreach (var file in requiredFiles)
            {
                var path = Path.Combine(_dataPath, file);
                Assert.True(File.Exists(path), $"{file} should exist");
            }
        }

        [Fact]
        public void AllSpecialtyFiles_ShouldHaveConsistentEncoding()
        {
            var files = Directory.GetFiles(_dataPath, "*.csv");

            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                // Check for BOM or encoding issues
                Assert.DoesNotContain("\uFEFF", content);
                Assert.DoesNotContain("ï»¿", content);
            }
        }

        #endregion
    }
}
