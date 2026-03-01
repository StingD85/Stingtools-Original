using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace StingBIM.AI.Tests.Data
{
    /// <summary>
    /// Tests for Uganda construction cost database validation
    /// </summary>
    public class CostDatabaseTests
    {
        private readonly string _dataPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..", "data", "costs");

        [Fact]
        public void LaborRates_ShouldHaveValidStructure()
        {
            var filePath = Path.Combine(_dataPath, "COST_LABOR_RATES_UGX.csv");
            Assert.True(File.Exists(filePath), "Labor rates file should exist");

            var lines = File.ReadAllLines(filePath);
            Assert.True(lines.Length > 1, "File should have data rows");

            var headers = lines[0].Split(',');
            Assert.Contains("trade_code", headers);
            Assert.Contains("trade_name", headers);
            Assert.Contains("hourly_rate_ugx", headers);
            Assert.Contains("region", headers);
        }

        [Fact]
        public void LaborRates_ShouldHaveUniqueTradesCodes()
        {
            var filePath = Path.Combine(_dataPath, "COST_LABOR_RATES_UGX.csv");
            var lines = File.ReadAllLines(filePath).Skip(1);
            var codes = lines.Select(l => l.Split(',')[0]).ToList();

            var duplicates = codes.GroupBy(c => c)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.Empty(duplicates);
        }

        [Fact]
        public void LaborRates_ShouldHaveValidRegions()
        {
            var validRegions = new[] { "Kampala", "Wakiso", "Mbarara", "Jinja", "Gulu", "National" };
            var filePath = Path.Combine(_dataPath, "COST_LABOR_RATES_UGX.csv");
            var lines = File.ReadAllLines(filePath).Skip(1);

            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length > 7)
                {
                    var region = parts[7];
                    Assert.Contains(region, validRegions);
                }
            }
        }

        [Fact]
        public void MaterialsCosts_ShouldHaveValidStructure()
        {
            var filePath = Path.Combine(_dataPath, "COST_MATERIALS_REGIONAL.csv");
            Assert.True(File.Exists(filePath), "Materials file should exist");

            var lines = File.ReadAllLines(filePath);
            Assert.True(lines.Length > 50, "Should have substantial material data");

            var headers = lines[0].Split(',');
            Assert.Contains("material_code", headers);
            Assert.Contains("unit_cost_ugx", headers);
            Assert.Contains("unit_cost_usd", headers);
        }

        [Fact]
        public void MaterialsCosts_ShouldHaveUniqueCodes()
        {
            var filePath = Path.Combine(_dataPath, "COST_MATERIALS_REGIONAL.csv");
            var lines = File.ReadAllLines(filePath).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));
            var codes = lines.Select(l => l.Split(',')[0]).ToList();

            var duplicates = codes.GroupBy(c => c)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.Empty(duplicates);
        }

        [Fact]
        public void MaterialsCosts_USDRateShouldBeReasonable()
        {
            var filePath = Path.Combine(_dataPath, "COST_MATERIALS_REGIONAL.csv");
            var lines = File.ReadAllLines(filePath).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));

            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length > 5 && decimal.TryParse(parts[4], out var ugx) && decimal.TryParse(parts[5], out var usd))
                {
                    // Exchange rate should be roughly 3700-3800 UGX/USD
                    if (usd > 0)
                    {
                        var impliedRate = ugx / usd;
                        Assert.InRange(impliedRate, 3500, 4000);
                    }
                }
            }
        }

        [Fact]
        public void EquipmentRental_ShouldHaveValidStructure()
        {
            var filePath = Path.Combine(_dataPath, "COST_EQUIPMENT_RENTAL.csv");
            Assert.True(File.Exists(filePath), "Equipment rental file should exist");

            var lines = File.ReadAllLines(filePath);
            Assert.True(lines.Length > 30, "Should have equipment data");

            var headers = lines[0].Split(',');
            Assert.Contains("equipment_code", headers);
            Assert.Contains("daily_rate_ugx", headers);
            Assert.Contains("weekly_rate_ugx", headers);
        }

        [Fact]
        public void EquipmentRental_WeeklyRateShouldBeLessThan7xDaily()
        {
            var filePath = Path.Combine(_dataPath, "COST_EQUIPMENT_RENTAL.csv");
            var lines = File.ReadAllLines(filePath).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));

            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length > 6 &&
                    decimal.TryParse(parts[5], out var daily) &&
                    decimal.TryParse(parts[6], out var weekly))
                {
                    // Weekly rate should offer discount (less than 7x daily)
                    Assert.True(weekly <= daily * 7, $"Weekly rate should be <= 7x daily for {parts[0]}");
                }
            }
        }

        [Fact]
        public void AllCostFiles_ShouldExist()
        {
            var requiredFiles = new[]
            {
                "COST_LABOR_RATES_UGX.csv",
                "COST_MATERIALS_REGIONAL.csv",
                "COST_EQUIPMENT_RENTAL.csv"
            };

            foreach (var file in requiredFiles)
            {
                var path = Path.Combine(_dataPath, file);
                Assert.True(File.Exists(path), $"{file} should exist");
            }
        }
    }
}
