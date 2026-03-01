using System;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using StingBIM.Core.Config;

namespace StingBIM.AI.Tests.Core
{
    /// <summary>
    /// Unit tests for StingBIMConfig singleton configuration manager.
    /// Tests configuration loading, validation, and hot reload functionality.
    /// </summary>
    [TestFixture]
    public class StingBIMConfigTests
    {
        #region Singleton Tests

        [Test]
        public void Instance_ShouldReturnSameInstance()
        {
            // Arrange & Act
            var instance1 = StingBIMConfig.Instance;
            var instance2 = StingBIMConfig.Instance;

            // Assert
            instance1.Should().BeSameAs(instance2);
        }

        [Test]
        public void Instance_ShouldNotBeNull()
        {
            // Act
            var instance = StingBIMConfig.Instance;

            // Assert
            instance.Should().NotBeNull();
        }

        #endregion

        #region Default Value Tests

        [Test]
        public void EnableGPUAcceleration_ShouldHaveDefaultValue()
        {
            // Act
            var config = StingBIMConfig.Instance;

            // Assert - Default is true per the code
            config.EnableGPUAcceleration.Should().BeTrue();
        }

        [Test]
        public void BatchProcessingSize_ShouldHaveDefaultValue()
        {
            // Act
            var config = StingBIMConfig.Instance;

            // Assert - Default is 1000 per the code
            config.BatchProcessingSize.Should().BeGreaterThan(0);
            config.BatchProcessingSize.Should().BeLessOrEqualTo(100000);
        }

        [Test]
        public void EnableAIFeatures_ShouldHaveDefaultValue()
        {
            // Act
            var config = StingBIMConfig.Instance;

            // Assert - Default is true per the code
            config.EnableAIFeatures.Should().BeTrue();
        }

        [Test]
        public void LogLevel_ShouldHaveValidDefaultValue()
        {
            // Arrange
            var validLogLevels = new[] { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" };

            // Act
            var config = StingBIMConfig.Instance;

            // Assert
            config.LogLevel.Should().NotBeNullOrEmpty();
            validLogLevels.Should().Contain(config.LogLevel, StringComparer.OrdinalIgnoreCase);
        }

        [Test]
        public void CacheSizeLimitMB_ShouldBeWithinValidRange()
        {
            // Act
            var config = StingBIMConfig.Instance;

            // Assert - Valid range is 10-10000 MB per the code
            config.CacheSizeLimitMB.Should().BeGreaterOrEqualTo(10);
            config.CacheSizeLimitMB.Should().BeLessOrEqualTo(10000);
        }

        [Test]
        public void DataDirectory_ShouldNotBeNullOrEmpty()
        {
            // Act
            var config = StingBIMConfig.Instance;

            // Assert
            config.DataDirectory.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void CustomSettings_ShouldNotBeNull()
        {
            // Act
            var config = StingBIMConfig.Instance;

            // Assert
            config.CustomSettings.Should().NotBeNull();
        }

        #endregion

        #region Update Method Tests

        [Test]
        public void UpdateBatchProcessingSize_ShouldRejectInvalidSize_TooSmall()
        {
            // Arrange
            var config = StingBIMConfig.Instance;

            // Act
            Action act = () => config.UpdateBatchProcessingSize(0);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void UpdateBatchProcessingSize_ShouldRejectInvalidSize_TooLarge()
        {
            // Arrange
            var config = StingBIMConfig.Instance;

            // Act
            Action act = () => config.UpdateBatchProcessingSize(100001);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void UpdateDataDirectory_ShouldRejectNullPath()
        {
            // Arrange
            var config = StingBIMConfig.Instance;

            // Act
            Action act = () => config.UpdateDataDirectory(null);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void UpdateDataDirectory_ShouldRejectEmptyPath()
        {
            // Arrange
            var config = StingBIMConfig.Instance;

            // Act
            Action act = () => config.UpdateDataDirectory("");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void UpdateDataDirectory_ShouldRejectWhitespacePath()
        {
            // Arrange
            var config = StingBIMConfig.Instance;

            // Act
            Action act = () => config.UpdateDataDirectory("   ");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        #endregion

        #region Custom Settings Tests

        [Test]
        public void UpdateCustomSetting_ShouldRejectNullKey()
        {
            // Arrange
            var config = StingBIMConfig.Instance;

            // Act
            Action act = () => config.UpdateCustomSetting(null, "value");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void UpdateCustomSetting_ShouldRejectEmptyKey()
        {
            // Arrange
            var config = StingBIMConfig.Instance;

            // Act
            Action act = () => config.UpdateCustomSetting("", "value");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void GetCustomSetting_ShouldReturnDefaultForMissingKey()
        {
            // Arrange
            var config = StingBIMConfig.Instance;
            var defaultValue = 42;

            // Act
            var result = config.GetCustomSetting("NonExistentKey", defaultValue);

            // Assert
            result.Should().Be(defaultValue);
        }

        [Test]
        public void GetCustomSetting_ShouldReturnDefaultForMissingKey_String()
        {
            // Arrange
            var config = StingBIMConfig.Instance;
            var defaultValue = "default";

            // Act
            var result = config.GetCustomSetting("AnotherNonExistentKey", defaultValue);

            // Assert
            result.Should().Be(defaultValue);
        }

        #endregion

        #region Thread Safety Tests

        [Test]
        public void Instance_ShouldBeThreadSafe()
        {
            // Arrange
            StingBIMConfig[] instances = new StingBIMConfig[100];
            var tasks = new System.Threading.Tasks.Task[100];

            // Act - Access instance from multiple threads
            for (int i = 0; i < 100; i++)
            {
                int index = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    instances[index] = StingBIMConfig.Instance;
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);

            // Assert - All instances should be the same
            for (int i = 1; i < 100; i++)
            {
                instances[i].Should().BeSameAs(instances[0]);
            }
        }

        #endregion
    }
}
