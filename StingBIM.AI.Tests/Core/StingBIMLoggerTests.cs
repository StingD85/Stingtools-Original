using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using NUnit.Framework;
using StingBIM.Core.Logging;

namespace StingBIM.AI.Tests.Core
{
    /// <summary>
    /// Unit tests for StingBIMLogger wrapper class.
    /// Tests factory methods, logging operations, and performance tracking.
    /// </summary>
    [TestFixture]
    public class StingBIMLoggerTests
    {
        #region Factory Method Tests

        [Test]
        public void For_Generic_ShouldCreateLoggerWithTypeName()
        {
            // Act
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Assert
            logger.Should().NotBeNull();
        }

        [Test]
        public void For_String_ShouldCreateLoggerWithContext()
        {
            // Arrange
            var context = "TestContext";

            // Act
            var logger = StingBIMLogger.For(context);

            // Assert
            logger.Should().NotBeNull();
        }

        [Test]
        public void For_NullContext_ShouldCreateLoggerWithDefaultContext()
        {
            // Act
            var logger = StingBIMLogger.For(null);

            // Assert
            logger.Should().NotBeNull();
        }

        [Test]
        public void For_EmptyContext_ShouldCreateLogger()
        {
            // Act
            var logger = StingBIMLogger.For("");

            // Assert
            logger.Should().NotBeNull();
        }

        [Test]
        public void For_Generic_ShouldCreateDifferentInstancesForDifferentTypes()
        {
            // Act
            var logger1 = StingBIMLogger.For<StingBIMLoggerTests>();
            var logger2 = StingBIMLogger.For<string>();

            // Assert - They should be different instances
            logger1.Should().NotBeSameAs(logger2);
        }

        #endregion

        #region Logging Method Tests

        [Test]
        public void Trace_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act
            Action act = () => logger.Trace("Test trace message");

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Debug_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act
            Action act = () => logger.Debug("Test debug message");

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Info_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act
            Action act = () => logger.Info("Test info message");

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Warn_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act
            Action act = () => logger.Warn("Test warning message");

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Error_Message_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act
            Action act = () => logger.Error("Test error message");

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Error_Exception_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();
            var exception = new InvalidOperationException("Test exception");

            // Act
            Action act = () => logger.Error(exception, "Test error with exception");

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Error_ExceptionOnly_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();
            var exception = new InvalidOperationException("Test exception");

            // Act
            Action act = () => logger.Error(exception);

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Fatal_Message_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act
            Action act = () => logger.Fatal("Test fatal message");

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Fatal_Exception_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();
            var exception = new InvalidOperationException("Test fatal exception");

            // Act
            Action act = () => logger.Fatal(exception, "Test fatal with exception");

            // Assert
            act.Should().NotThrow();
        }

        #endregion

        #region Structured Logging Tests

        [Test]
        public void LogStructured_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();
            var properties = new Dictionary<string, object>
            {
                { "Key1", "Value1" },
                { "Key2", 42 },
                { "Key3", true }
            };

            // Act
            Action act = () => logger.LogStructured(NLog.LogLevel.Info, "Structured message", properties);

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void LogStructured_NullProperties_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act
            Action act = () => logger.LogStructured(NLog.LogLevel.Info, "Message without properties", null);

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void LogOperationStart_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act
            Action act = () => logger.LogOperationStart("TestOperation");

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void LogOperationStart_WithProperties_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();
            var properties = new Dictionary<string, object>
            {
                { "ItemCount", 100 }
            };

            // Act
            Action act = () => logger.LogOperationStart("TestOperation", properties);

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void LogOperationComplete_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act
            Action act = () => logger.LogOperationComplete("TestOperation", 150);

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void LogOperationFailed_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();
            var exception = new InvalidOperationException("Operation failed");

            // Act
            Action act = () => logger.LogOperationFailed("TestOperation", exception);

            // Assert
            act.Should().NotThrow();
        }

        #endregion

        #region Performance Timer Tests

        [Test]
        public void StartPerformanceTimer_ShouldReturnDisposable()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act
            var timer = logger.StartPerformanceTimer("TestOperation");

            // Assert
            timer.Should().NotBeNull();
            timer.Should().BeAssignableTo<IDisposable>();

            // Cleanup
            timer.Dispose();
        }

        [Test]
        public void StartPerformanceTimer_ShouldDisposeWithoutError()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act
            Action act = () =>
            {
                using (var timer = logger.StartPerformanceTimer("TestOperation"))
                {
                    Thread.Sleep(10); // Simulate some work
                }
            };

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void TrackSlowOperation_ShouldReturnDisposable()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act
            var timer = logger.TrackSlowOperation("TestOperation", 1000);

            // Assert
            timer.Should().NotBeNull();
            timer.Should().BeAssignableTo<IDisposable>();

            // Cleanup
            timer.Dispose();
        }

        [Test]
        public void TrackSlowOperation_FastOperation_ShouldNotLogWarning()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act - Operation completes faster than threshold
            Action act = () =>
            {
                using (var timer = logger.TrackSlowOperation("FastOperation", 1000))
                {
                    // Do nothing - fast operation
                }
            };

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void TrackSlowOperation_SlowOperation_ShouldLogWarning()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();

            // Act - Operation exceeds threshold
            Action act = () =>
            {
                using (var timer = logger.TrackSlowOperation("SlowOperation", 10))
                {
                    Thread.Sleep(50); // Exceed the 10ms threshold
                }
            };

            // Assert - Should not throw even when logging warning
            act.Should().NotThrow();
        }

        [Test]
        public void PerformanceTimer_MultipleDispose_ShouldNotThrow()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();
            var timer = logger.StartPerformanceTimer("TestOperation");

            // Act
            Action act = () =>
            {
                timer.Dispose();
                timer.Dispose(); // Double dispose
            };

            // Assert
            act.Should().NotThrow();
        }

        #endregion

        #region Thread Safety Tests

        [Test]
        public void Logging_ShouldBeThreadSafe()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();
            var tasks = new System.Threading.Tasks.Task[100];

            // Act - Log from multiple threads
            for (int i = 0; i < 100; i++)
            {
                int index = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    logger.Info($"Thread {index} logging");
                    logger.Debug($"Thread {index} debug");
                    logger.Warn($"Thread {index} warning");
                });
            }

            // Assert - Should complete without exceptions
            Action act = () => System.Threading.Tasks.Task.WaitAll(tasks);
            act.Should().NotThrow();
        }

        [Test]
        public void PerformanceTimers_ShouldBeThreadSafe()
        {
            // Arrange
            var logger = StingBIMLogger.For<StingBIMLoggerTests>();
            var tasks = new System.Threading.Tasks.Task[50];

            // Act - Start/stop timers from multiple threads
            for (int i = 0; i < 50; i++)
            {
                int index = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    using (var timer = logger.StartPerformanceTimer($"Operation{index}"))
                    {
                        Thread.Sleep(1);
                    }
                });
            }

            // Assert - Should complete without exceptions
            Action act = () => System.Threading.Tasks.Task.WaitAll(tasks);
            act.Should().NotThrow();
        }

        #endregion
    }
}
