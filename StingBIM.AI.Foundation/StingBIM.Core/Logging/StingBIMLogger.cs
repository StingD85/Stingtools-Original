using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NLog;

namespace StingBIM.Core.Logging
{
    /// <summary>
    /// Centralized logging wrapper for StingBIM
    /// Provides structured logging, performance tracking, and async operations
    /// Wraps NLog for flexibility and additional features
    /// </summary>
    public sealed class StingBIMLogger
    {
        #region Private Fields
        
        private readonly Logger _logger;
        private readonly string _context;
        private readonly Stopwatch _performanceStopwatch;
        private readonly Dictionary<string, Stopwatch> _operationTimers;
        private readonly object _timerLock = new object();
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Creates a new StingBIMLogger instance
        /// </summary>
        /// <param name="context">Logging context (usually class name)</param>
        private StingBIMLogger(string context)
        {
            _context = context ?? "StingBIM";
            _logger = LogManager.GetLogger(context);
            _performanceStopwatch = new Stopwatch();
            _operationTimers = new Dictionary<string, Stopwatch>();
        }
        
        #endregion

        #region Factory Methods
        
        /// <summary>
        /// Creates a logger for the specified type
        /// </summary>
        /// <typeparam name="T">Type to create logger for</typeparam>
        /// <returns>Logger instance</returns>
        public static StingBIMLogger For<T>()
        {
            return new StingBIMLogger(typeof(T).FullName);
        }
        
        /// <summary>
        /// Creates a logger with the specified context
        /// </summary>
        /// <param name="context">Logging context</param>
        /// <returns>Logger instance</returns>
        public static StingBIMLogger For(string context)
        {
            return new StingBIMLogger(context);
        }
        
        #endregion

        #region Standard Logging Methods
        
        /// <summary>
        /// Logs a trace message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="memberName">Calling member name (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Source line number (auto-filled)</param>
        public void Trace(
            string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (_logger.IsTraceEnabled)
            {
                var logEvent = CreateLogEvent(LogLevel.Trace, message, memberName, filePath, lineNumber);
                _logger.Log(logEvent);
            }
        }
        
        /// <summary>
        /// Logs a debug message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="memberName">Calling member name (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Source line number (auto-filled)</param>
        public void Debug(
            string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (_logger.IsDebugEnabled)
            {
                var logEvent = CreateLogEvent(LogLevel.Debug, message, memberName, filePath, lineNumber);
                _logger.Log(logEvent);
            }
        }
        
        /// <summary>
        /// Logs an info message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="memberName">Calling member name (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Source line number (auto-filled)</param>
        public void Info(
            string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (_logger.IsInfoEnabled)
            {
                var logEvent = CreateLogEvent(LogLevel.Info, message, memberName, filePath, lineNumber);
                _logger.Log(logEvent);
            }
        }
        
        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="memberName">Calling member name (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Source line number (auto-filled)</param>
        public void Warn(
            string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (_logger.IsWarnEnabled)
            {
                var logEvent = CreateLogEvent(LogLevel.Warn, message, memberName, filePath, lineNumber);
                _logger.Log(logEvent);
            }
        }
        
        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="memberName">Calling member name (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Source line number (auto-filled)</param>
        public void Error(
            string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (_logger.IsErrorEnabled)
            {
                var logEvent = CreateLogEvent(LogLevel.Error, message, memberName, filePath, lineNumber);
                _logger.Log(logEvent);
            }
        }
        
        /// <summary>
        /// Logs an error message with exception
        /// </summary>
        /// <param name="exception">Exception to log</param>
        /// <param name="message">Additional message</param>
        /// <param name="memberName">Calling member name (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Source line number (auto-filled)</param>
        public void Error(
            Exception exception,
            string message = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (_logger.IsErrorEnabled)
            {
                var logEvent = CreateLogEvent(
                    LogLevel.Error, 
                    message ?? exception.Message, 
                    memberName, 
                    filePath, 
                    lineNumber);
                
                logEvent.Exception = exception;
                logEvent.Properties["ExceptionType"] = exception.GetType().FullName;
                logEvent.Properties["StackTrace"] = exception.StackTrace;
                
                _logger.Log(logEvent);
            }
        }
        
        /// <summary>
        /// Logs a fatal message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="memberName">Calling member name (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Source line number (auto-filled)</param>
        public void Fatal(
            string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (_logger.IsFatalEnabled)
            {
                var logEvent = CreateLogEvent(LogLevel.Fatal, message, memberName, filePath, lineNumber);
                _logger.Log(logEvent);
            }
        }
        
        /// <summary>
        /// Logs a fatal message with exception
        /// </summary>
        /// <param name="exception">Exception to log</param>
        /// <param name="message">Additional message</param>
        /// <param name="memberName">Calling member name (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Source line number (auto-filled)</param>
        public void Fatal(
            Exception exception,
            string message = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (_logger.IsFatalEnabled)
            {
                var logEvent = CreateLogEvent(
                    LogLevel.Fatal, 
                    message ?? exception.Message, 
                    memberName, 
                    filePath, 
                    lineNumber);
                
                logEvent.Exception = exception;
                logEvent.Properties["ExceptionType"] = exception.GetType().FullName;
                logEvent.Properties["StackTrace"] = exception.StackTrace;
                
                _logger.Log(logEvent);
            }
        }
        
        #endregion

        #region Structured Logging
        
        /// <summary>
        /// Logs a structured message with properties
        /// </summary>
        /// <param name="level">Log level</param>
        /// <param name="message">Message template</param>
        /// <param name="properties">Properties dictionary</param>
        public void LogStructured(LogLevel level, string message, Dictionary<string, object> properties)
        {
            if (!_logger.IsEnabled(level))
                return;
            
            var logEvent = new LogEventInfo(level, _logger.Name, message);
            logEvent.Properties["Context"] = _context;
            
            if (properties != null)
            {
                foreach (var kvp in properties)
                {
                    logEvent.Properties[kvp.Key] = kvp.Value;
                }
            }
            
            _logger.Log(logEvent);
        }
        
        /// <summary>
        /// Logs an operation start
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="properties">Optional properties</param>
        public void LogOperationStart(string operationName, Dictionary<string, object> properties = null)
        {
            var props = properties ?? new Dictionary<string, object>();
            props["OperationName"] = operationName;
            props["OperationStatus"] = "Started";
            props["Timestamp"] = DateTime.UtcNow;
            
            LogStructured(LogLevel.Info, $"Operation started: {operationName}", props);
        }
        
        /// <summary>
        /// Logs an operation completion
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        /// <param name="properties">Optional properties</param>
        public void LogOperationComplete(string operationName, long durationMs, Dictionary<string, object> properties = null)
        {
            var props = properties ?? new Dictionary<string, object>();
            props["OperationName"] = operationName;
            props["OperationStatus"] = "Completed";
            props["DurationMs"] = durationMs;
            props["Timestamp"] = DateTime.UtcNow;
            
            LogStructured(LogLevel.Info, $"Operation completed: {operationName} ({durationMs}ms)", props);
        }
        
        /// <summary>
        /// Logs an operation failure
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="exception">Exception that occurred</param>
        /// <param name="properties">Optional properties</param>
        public void LogOperationFailed(string operationName, Exception exception, Dictionary<string, object> properties = null)
        {
            var props = properties ?? new Dictionary<string, object>();
            props["OperationName"] = operationName;
            props["OperationStatus"] = "Failed";
            props["ExceptionType"] = exception.GetType().FullName;
            props["ExceptionMessage"] = exception.Message;
            props["Timestamp"] = DateTime.UtcNow;
            
            var logEvent = new LogEventInfo(LogLevel.Error, _logger.Name, 
                $"Operation failed: {operationName}");
            logEvent.Exception = exception;
            logEvent.Properties["Context"] = _context;
            
            foreach (var kvp in props)
            {
                logEvent.Properties[kvp.Key] = kvp.Value;
            }
            
            _logger.Log(logEvent);
        }
        
        #endregion

        #region Performance Tracking
        
        /// <summary>
        /// Starts a performance timer for an operation
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <returns>Disposable timer that logs duration on disposal</returns>
        public IDisposable StartPerformanceTimer(string operationName)
        {
            return new PerformanceTimer(this, operationName);
        }
        
        /// <summary>
        /// Tracks operation duration and logs if it exceeds threshold
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="thresholdMs">Threshold in milliseconds</param>
        /// <returns>Disposable timer</returns>
        public IDisposable TrackSlowOperation(string operationName, int thresholdMs)
        {
            return new SlowOperationTimer(this, operationName, thresholdMs);
        }
        
        /// <summary>
        /// Starts tracking a named operation
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        internal void StartOperation(string operationName)
        {
            lock (_timerLock)
            {
                if (!_operationTimers.ContainsKey(operationName))
                {
                    var sw = Stopwatch.StartNew();
                    _operationTimers[operationName] = sw;
                    LogOperationStart(operationName);
                }
            }
        }
        
        /// <summary>
        /// Stops tracking a named operation
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        internal void StopOperation(string operationName)
        {
            lock (_timerLock)
            {
                if (_operationTimers.TryGetValue(operationName, out Stopwatch sw))
                {
                    sw.Stop();
                    LogOperationComplete(operationName, sw.ElapsedMilliseconds);
                    _operationTimers.Remove(operationName);
                }
            }
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Creates a log event with caller information
        /// </summary>
        private LogEventInfo CreateLogEvent(
            LogLevel level, 
            string message, 
            string memberName, 
            string filePath, 
            int lineNumber)
        {
            var logEvent = new LogEventInfo(level, _logger.Name, message);
            logEvent.Properties["Context"] = _context;
            logEvent.Properties["MemberName"] = memberName;
            logEvent.Properties["SourceFile"] = System.IO.Path.GetFileName(filePath);
            logEvent.Properties["LineNumber"] = lineNumber;
            logEvent.Properties["Timestamp"] = DateTime.UtcNow;
            
            return logEvent;
        }
        
        #endregion

        #region Performance Timer Classes
        
        /// <summary>
        /// Disposable performance timer
        /// </summary>
        private class PerformanceTimer : IDisposable
        {
            private readonly StingBIMLogger _logger;
            private readonly string _operationName;
            private readonly Stopwatch _stopwatch;
            
            public PerformanceTimer(StingBIMLogger logger, string operationName)
            {
                _logger = logger;
                _operationName = operationName;
                _stopwatch = Stopwatch.StartNew();
                _logger.StartOperation(operationName);
            }
            
            public void Dispose()
            {
                _stopwatch.Stop();
                _logger.StopOperation(_operationName);
            }
        }
        
        /// <summary>
        /// Timer that logs only if operation exceeds threshold
        /// </summary>
        private class SlowOperationTimer : IDisposable
        {
            private readonly StingBIMLogger _logger;
            private readonly string _operationName;
            private readonly int _thresholdMs;
            private readonly Stopwatch _stopwatch;
            
            public SlowOperationTimer(StingBIMLogger logger, string operationName, int thresholdMs)
            {
                _logger = logger;
                _operationName = operationName;
                _thresholdMs = thresholdMs;
                _stopwatch = Stopwatch.StartNew();
            }
            
            public void Dispose()
            {
                _stopwatch.Stop();
                if (_stopwatch.ElapsedMilliseconds > _thresholdMs)
                {
                    _logger.Warn($"Slow operation detected: {_operationName} " +
                               $"took {_stopwatch.ElapsedMilliseconds}ms " +
                               $"(threshold: {_thresholdMs}ms)");
                }
            }
        }
        
        #endregion
    }
}
