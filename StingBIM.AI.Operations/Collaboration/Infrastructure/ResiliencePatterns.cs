// ===================================================================
// StingBIM.AI.Collaboration - Resilience and Error Handling Patterns
// Provides robust error handling, retry logic, and circuit breaker
// Addresses exception handling anti-patterns identified in codebase
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StingBIM.AI.Collaboration.Infrastructure
{
    #region Custom Exceptions

    /// <summary>
    /// Base exception for StingBIM collaboration errors
    /// </summary>
    public class CollaborationException : Exception
    {
        public string ErrorCode { get; }
        public string CorrelationId { get; }
        public Dictionary<string, object> Context { get; }

        public CollaborationException(
            string message,
            string errorCode = "COLLAB_ERROR",
            Exception? innerException = null,
            string? correlationId = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            CorrelationId = correlationId ?? Guid.NewGuid().ToString();
            Context = new Dictionary<string, object>();
        }

        public CollaborationException AddContext(string key, object value)
        {
            Context[key] = value;
            return this;
        }
    }

    /// <summary>
    /// Validation exception
    /// </summary>
    public class ValidationException : CollaborationException
    {
        public List<ValidationError> Errors { get; }

        public ValidationException(string message, List<ValidationError>? errors = null)
            : base(message, "VALIDATION_ERROR")
        {
            Errors = errors ?? new List<ValidationError>();
        }

        public ValidationException(List<ValidationError> errors)
            : this($"Validation failed: {errors.Count} errors", errors)
        {
        }
    }

    /// <summary>
    /// Single validation error
    /// </summary>
    public class ValidationError
    {
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public object? AttemptedValue { get; set; }

        public override string ToString() => $"{Field}: {Message}";
    }

    /// <summary>
    /// Resource not found exception
    /// </summary>
    public class NotFoundException : CollaborationException
    {
        public string ResourceType { get; }
        public string ResourceId { get; }

        public NotFoundException(string resourceType, string resourceId)
            : base($"{resourceType} with ID '{resourceId}' not found", "NOT_FOUND")
        {
            ResourceType = resourceType;
            ResourceId = resourceId;
            AddContext("resourceType", resourceType);
            AddContext("resourceId", resourceId);
        }
    }

    /// <summary>
    /// Conflict exception (optimistic concurrency)
    /// </summary>
    public class ConflictException : CollaborationException
    {
        public long ExpectedVersion { get; }
        public long ActualVersion { get; }

        public ConflictException(string resourceType, string resourceId, long expected, long actual)
            : base($"Conflict on {resourceType} '{resourceId}': expected version {expected}, found {actual}",
                  "CONFLICT")
        {
            ExpectedVersion = expected;
            ActualVersion = actual;
        }
    }

    /// <summary>
    /// Rate limit exception
    /// </summary>
    public class RateLimitException : CollaborationException
    {
        public TimeSpan RetryAfter { get; }

        public RateLimitException(TimeSpan retryAfter)
            : base($"Rate limit exceeded. Retry after {retryAfter.TotalSeconds}s", "RATE_LIMIT")
        {
            RetryAfter = retryAfter;
        }
    }

    /// <summary>
    /// Circuit breaker open exception
    /// </summary>
    public class CircuitBreakerOpenException : CollaborationException
    {
        public string CircuitName { get; }
        public DateTime? NextAttemptTime { get; }

        public CircuitBreakerOpenException(string circuitName, DateTime? nextAttempt = null)
            : base($"Circuit '{circuitName}' is open", "CIRCUIT_OPEN")
        {
            CircuitName = circuitName;
            NextAttemptTime = nextAttempt;
        }
    }

    /// <summary>
    /// Timeout exception
    /// </summary>
    public class OperationTimeoutException : CollaborationException
    {
        public TimeSpan Timeout { get; }
        public string OperationName { get; }

        public OperationTimeoutException(string operationName, TimeSpan timeout)
            : base($"Operation '{operationName}' timed out after {timeout.TotalSeconds}s", "TIMEOUT")
        {
            OperationName = operationName;
            Timeout = timeout;
        }
    }

    #endregion

    #region Result Pattern

    /// <summary>
    /// Result type for operations that can fail
    /// </summary>
    public class Result<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public string? Error { get; }
        public string? ErrorCode { get; }
        public Exception? Exception { get; }

        private Result(T value)
        {
            IsSuccess = true;
            Value = value;
        }

        private Result(string error, string? errorCode = null, Exception? exception = null)
        {
            IsSuccess = false;
            Error = error;
            ErrorCode = errorCode;
            Exception = exception;
        }

        public static Result<T> Success(T value) => new(value);

        public static Result<T> Failure(string error, string? errorCode = null) =>
            new(error, errorCode);

        public static Result<T> FromException(Exception ex) =>
            new(ex.Message, ex is CollaborationException ce ? ce.ErrorCode : "EXCEPTION", ex);

        public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure) =>
            IsSuccess ? onSuccess(Value!) : onFailure(Error!);

        public Result<TNew> Map<TNew>(Func<T, TNew> mapper) =>
            IsSuccess ? Result<TNew>.Success(mapper(Value!)) : Result<TNew>.Failure(Error!, ErrorCode);

        public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper) =>
            IsSuccess
                ? Result<TNew>.Success(await mapper(Value!))
                : Result<TNew>.Failure(Error!, ErrorCode);

        public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder) =>
            IsSuccess ? binder(Value!) : Result<TNew>.Failure(Error!, ErrorCode);

        public T GetValueOrThrow() =>
            IsSuccess ? Value! : throw Exception ?? new InvalidOperationException(Error);

        public T GetValueOrDefault(T defaultValue) =>
            IsSuccess ? Value! : defaultValue;
    }

    /// <summary>
    /// Result without value
    /// </summary>
    public class Result
    {
        public bool IsSuccess { get; }
        public string? Error { get; }
        public string? ErrorCode { get; }
        public Exception? Exception { get; }

        private Result(bool success, string? error = null, string? errorCode = null, Exception? ex = null)
        {
            IsSuccess = success;
            Error = error;
            ErrorCode = errorCode;
            Exception = ex;
        }

        public static Result Success() => new(true);
        public static Result Failure(string error, string? errorCode = null) => new(false, error, errorCode);
        public static Result FromException(Exception ex) =>
            new(false, ex.Message, ex is CollaborationException ce ? ce.ErrorCode : "EXCEPTION", ex);
    }

    #endregion

    #region Retry Policy

    /// <summary>
    /// Retry configuration
    /// </summary>
    public class RetryOptions
    {
        public int MaxRetries { get; set; } = 3;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
        public double BackoffMultiplier { get; set; } = 2.0;
        public bool AddJitter { get; set; } = true;
        public Func<Exception, bool>? ShouldRetry { get; set; }
        public Action<Exception, int, TimeSpan>? OnRetry { get; set; }
    }

    /// <summary>
    /// Retry policy implementation
    /// </summary>
    public class RetryPolicy
    {
        private readonly RetryOptions _options;
        private readonly ILogger? _logger;
        private static readonly Random _random = new();

        public RetryPolicy(RetryOptions? options = null, ILogger? logger = null)
        {
            _options = options ?? new RetryOptions();
            _logger = logger;

            // Default retry predicate: transient failures only
            _options.ShouldRetry ??= ex => ex is TimeoutException
                                         or OperationTimeoutException
                                         or RateLimitException
                                         or System.Net.Http.HttpRequestException;
        }

        /// <summary>
        /// Execute with retry
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken ct = default,
            [CallerMemberName] string? operationName = null)
        {
            var attempt = 0;
            var delay = _options.InitialDelay;

            while (true)
            {
                try
                {
                    attempt++;
                    return await action(ct);
                }
                catch (Exception ex) when (attempt <= _options.MaxRetries && _options.ShouldRetry!(ex))
                {
                    if (ex is RateLimitException rle && rle.RetryAfter > TimeSpan.Zero)
                    {
                        delay = rle.RetryAfter;
                    }

                    var actualDelay = _options.AddJitter
                        ? AddJitter(delay)
                        : delay;

                    _logger?.LogWarning(
                        "Attempt {Attempt}/{MaxAttempts} for {Operation} failed: {Error}. Retrying in {Delay}ms",
                        attempt, _options.MaxRetries + 1, operationName, ex.Message, actualDelay.TotalMilliseconds);

                    _options.OnRetry?.Invoke(ex, attempt, actualDelay);

                    await Task.Delay(actualDelay, ct);

                    // Exponential backoff
                    delay = TimeSpan.FromMilliseconds(
                        Math.Min(delay.TotalMilliseconds * _options.BackoffMultiplier,
                                _options.MaxDelay.TotalMilliseconds));
                }
            }
        }

        /// <summary>
        /// Execute with retry (no return value)
        /// </summary>
        public async Task ExecuteAsync(
            Func<CancellationToken, Task> action,
            CancellationToken ct = default,
            [CallerMemberName] string? operationName = null)
        {
            await ExecuteAsync(async ct2 =>
            {
                await action(ct2);
                return true;
            }, ct, operationName);
        }

        private TimeSpan AddJitter(TimeSpan delay)
        {
            var jitter = delay.TotalMilliseconds * 0.2 * (_random.NextDouble() - 0.5);
            return TimeSpan.FromMilliseconds(delay.TotalMilliseconds + jitter);
        }
    }

    #endregion

    #region Circuit Breaker

    /// <summary>
    /// Circuit breaker state
    /// </summary>
    public enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    /// <summary>
    /// Circuit breaker configuration
    /// </summary>
    public class CircuitBreakerOptions
    {
        public int FailureThreshold { get; set; } = 5;
        public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan SamplingWindow { get; set; } = TimeSpan.FromMinutes(1);
        public int HalfOpenSuccessThreshold { get; set; } = 2;
        public Func<Exception, bool>? ShouldTrack { get; set; }
        public Action<CircuitState, CircuitState>? OnStateChange { get; set; }
    }

    /// <summary>
    /// Circuit breaker implementation
    /// </summary>
    public class CircuitBreaker
    {
        private readonly string _name;
        private readonly CircuitBreakerOptions _options;
        private readonly ILogger? _logger;
        private readonly object _lock = new();

        private CircuitState _state = CircuitState.Closed;
        private int _failureCount = 0;
        private int _halfOpenSuccesses = 0;
        private DateTime _openedAt = DateTime.MinValue;
        private readonly Queue<DateTime> _failureTimestamps = new();

        public CircuitState State => _state;
        public string Name => _name;

        public CircuitBreaker(string name, CircuitBreakerOptions? options = null, ILogger? logger = null)
        {
            _name = name;
            _options = options ?? new CircuitBreakerOptions();
            _logger = logger;

            _options.ShouldTrack ??= ex => !(ex is ValidationException or NotFoundException);
        }

        /// <summary>
        /// Execute operation through circuit breaker
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken ct = default)
        {
            EnsureCircuitAllowsExecution();

            try
            {
                var result = await action(ct);
                OnSuccess();
                return result;
            }
            catch (Exception ex)
            {
                OnFailure(ex);
                throw;
            }
        }

        /// <summary>
        /// Execute operation through circuit breaker (no return value)
        /// </summary>
        public async Task ExecuteAsync(
            Func<CancellationToken, Task> action,
            CancellationToken ct = default)
        {
            await ExecuteAsync(async ct2 =>
            {
                await action(ct2);
                return true;
            }, ct);
        }

        private void EnsureCircuitAllowsExecution()
        {
            lock (_lock)
            {
                switch (_state)
                {
                    case CircuitState.Closed:
                        return;

                    case CircuitState.Open:
                        if (DateTime.UtcNow >= _openedAt + _options.OpenDuration)
                        {
                            TransitionTo(CircuitState.HalfOpen);
                            return;
                        }
                        throw new CircuitBreakerOpenException(_name,
                            _openedAt + _options.OpenDuration);

                    case CircuitState.HalfOpen:
                        return; // Allow test request
                }
            }
        }

        private void OnSuccess()
        {
            lock (_lock)
            {
                switch (_state)
                {
                    case CircuitState.Closed:
                        _failureCount = 0;
                        break;

                    case CircuitState.HalfOpen:
                        _halfOpenSuccesses++;
                        if (_halfOpenSuccesses >= _options.HalfOpenSuccessThreshold)
                        {
                            TransitionTo(CircuitState.Closed);
                        }
                        break;
                }
            }
        }

        private void OnFailure(Exception ex)
        {
            if (!_options.ShouldTrack!(ex)) return;

            lock (_lock)
            {
                var now = DateTime.UtcNow;

                // Clean old failures outside sampling window
                while (_failureTimestamps.Count > 0 &&
                       _failureTimestamps.Peek() < now - _options.SamplingWindow)
                {
                    _failureTimestamps.Dequeue();
                }

                _failureTimestamps.Enqueue(now);
                _failureCount = _failureTimestamps.Count;

                switch (_state)
                {
                    case CircuitState.Closed:
                        if (_failureCount >= _options.FailureThreshold)
                        {
                            TransitionTo(CircuitState.Open);
                        }
                        break;

                    case CircuitState.HalfOpen:
                        TransitionTo(CircuitState.Open);
                        break;
                }
            }
        }

        private void TransitionTo(CircuitState newState)
        {
            var oldState = _state;
            _state = newState;

            if (newState == CircuitState.Open)
            {
                _openedAt = DateTime.UtcNow;
            }
            else if (newState == CircuitState.Closed)
            {
                _failureCount = 0;
                _halfOpenSuccesses = 0;
                _failureTimestamps.Clear();
            }
            else if (newState == CircuitState.HalfOpen)
            {
                _halfOpenSuccesses = 0;
            }

            _logger?.LogWarning("Circuit {Name} transitioned from {OldState} to {NewState}",
                _name, oldState, newState);

            _options.OnStateChange?.Invoke(oldState, newState);
        }

        /// <summary>
        /// Manually reset circuit
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                TransitionTo(CircuitState.Closed);
            }
        }

        /// <summary>
        /// Get circuit statistics
        /// </summary>
        public CircuitStats GetStats()
        {
            lock (_lock)
            {
                return new CircuitStats
                {
                    Name = _name,
                    State = _state,
                    FailureCount = _failureCount,
                    OpenedAt = _state == CircuitState.Open ? _openedAt : null,
                    NextAttemptAt = _state == CircuitState.Open
                        ? _openedAt + _options.OpenDuration
                        : null
                };
            }
        }
    }

    /// <summary>
    /// Circuit breaker statistics
    /// </summary>
    public class CircuitStats
    {
        public string Name { get; set; } = string.Empty;
        public CircuitState State { get; set; }
        public int FailureCount { get; set; }
        public DateTime? OpenedAt { get; set; }
        public DateTime? NextAttemptAt { get; set; }
    }

    #endregion

    #region Rate Limiter

    /// <summary>
    /// Rate limiter using token bucket algorithm
    /// </summary>
    public class RateLimiter
    {
        private readonly int _maxTokens;
        private readonly TimeSpan _refillInterval;
        private readonly int _tokensPerRefill;
        private readonly ILogger? _logger;

        private double _tokens;
        private DateTime _lastRefill;
        private readonly object _lock = new();

        public RateLimiter(
            int maxTokens = 100,
            TimeSpan? refillInterval = null,
            int tokensPerRefill = 10,
            ILogger? logger = null)
        {
            _maxTokens = maxTokens;
            _refillInterval = refillInterval ?? TimeSpan.FromSeconds(1);
            _tokensPerRefill = tokensPerRefill;
            _logger = logger;

            _tokens = maxTokens;
            _lastRefill = DateTime.UtcNow;
        }

        /// <summary>
        /// Try to acquire a token
        /// </summary>
        public bool TryAcquire(int tokens = 1)
        {
            lock (_lock)
            {
                Refill();

                if (_tokens >= tokens)
                {
                    _tokens -= tokens;
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Acquire token or throw
        /// </summary>
        public void Acquire(int tokens = 1)
        {
            if (!TryAcquire(tokens))
            {
                var retryAfter = GetTimeUntilAvailable(tokens);
                throw new RateLimitException(retryAfter);
            }
        }

        /// <summary>
        /// Wait for token to become available
        /// </summary>
        public async Task<bool> WaitAsync(int tokens = 1, TimeSpan? maxWait = null, CancellationToken ct = default)
        {
            var deadline = DateTime.UtcNow + (maxWait ?? TimeSpan.FromSeconds(30));

            while (DateTime.UtcNow < deadline)
            {
                if (TryAcquire(tokens))
                    return true;

                var waitTime = GetTimeUntilAvailable(tokens);
                if (DateTime.UtcNow + waitTime > deadline)
                    return false;

                await Task.Delay(waitTime, ct);
            }

            return false;
        }

        /// <summary>
        /// Get time until tokens available
        /// </summary>
        public TimeSpan GetTimeUntilAvailable(int tokens = 1)
        {
            lock (_lock)
            {
                Refill();

                if (_tokens >= tokens)
                    return TimeSpan.Zero;

                var tokensNeeded = tokens - _tokens;
                var refillsNeeded = Math.Ceiling(tokensNeeded / _tokensPerRefill);
                return TimeSpan.FromTicks((long)(_refillInterval.Ticks * refillsNeeded));
            }
        }

        private void Refill()
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastRefill;
            var refills = elapsed.Ticks / _refillInterval.Ticks;

            if (refills >= 1)
            {
                _tokens = Math.Min(_maxTokens, _tokens + (refills * _tokensPerRefill));
                _lastRefill = now;
            }
        }
    }

    #endregion

    #region Timeout Handler

    /// <summary>
    /// Timeout handler for operations
    /// </summary>
    public class TimeoutHandler
    {
        private readonly ILogger? _logger;

        public TimeoutHandler(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Execute with timeout
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> action,
            TimeSpan timeout,
            CancellationToken ct = default,
            [CallerMemberName] string? operationName = null)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                return await action(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                stopwatch.Stop();
                _logger?.LogWarning(
                    "Operation {Operation} timed out after {Elapsed}ms (limit: {Timeout}ms)",
                    operationName, stopwatch.ElapsedMilliseconds, timeout.TotalMilliseconds);

                throw new OperationTimeoutException(operationName ?? "unknown", timeout);
            }
        }

        /// <summary>
        /// Execute with timeout (no return value)
        /// </summary>
        public async Task ExecuteAsync(
            Func<CancellationToken, Task> action,
            TimeSpan timeout,
            CancellationToken ct = default,
            [CallerMemberName] string? operationName = null)
        {
            await ExecuteAsync(async ct2 =>
            {
                await action(ct2);
                return true;
            }, timeout, ct, operationName);
        }
    }

    #endregion

    #region Bulkhead (Resource Isolation)

    /// <summary>
    /// Bulkhead pattern for resource isolation
    /// </summary>
    public class Bulkhead
    {
        private readonly string _name;
        private readonly SemaphoreSlim _semaphore;
        private readonly int _maxConcurrency;
        private readonly int _maxQueueSize;
        private readonly ILogger? _logger;
        private int _queuedCount = 0;

        public int AvailableSlots => _semaphore.CurrentCount;
        public int QueuedCount => _queuedCount;

        public Bulkhead(
            string name,
            int maxConcurrency = 10,
            int maxQueueSize = 100,
            ILogger? logger = null)
        {
            _name = name;
            _maxConcurrency = maxConcurrency;
            _maxQueueSize = maxQueueSize;
            _logger = logger;
            _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        }

        /// <summary>
        /// Execute within bulkhead
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> action,
            TimeSpan? timeout = null,
            CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _queuedCount) > _maxQueueSize)
            {
                Interlocked.Decrement(ref _queuedCount);
                throw new CollaborationException(
                    $"Bulkhead '{_name}' queue full ({_maxQueueSize})",
                    "BULKHEAD_FULL");
            }

            try
            {
                var waitResult = await _semaphore.WaitAsync(
                    timeout ?? TimeSpan.FromSeconds(30), ct);

                if (!waitResult)
                {
                    throw new OperationTimeoutException(
                        $"Waiting for bulkhead '{_name}'",
                        timeout ?? TimeSpan.FromSeconds(30));
                }

                try
                {
                    Interlocked.Decrement(ref _queuedCount);
                    return await action(ct);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch
            {
                Interlocked.Decrement(ref _queuedCount);
                throw;
            }
        }

        /// <summary>
        /// Get bulkhead statistics
        /// </summary>
        public BulkheadStats GetStats()
        {
            return new BulkheadStats
            {
                Name = _name,
                MaxConcurrency = _maxConcurrency,
                AvailableSlots = _semaphore.CurrentCount,
                QueuedCount = _queuedCount,
                MaxQueueSize = _maxQueueSize
            };
        }
    }

    /// <summary>
    /// Bulkhead statistics
    /// </summary>
    public class BulkheadStats
    {
        public string Name { get; set; } = string.Empty;
        public int MaxConcurrency { get; set; }
        public int AvailableSlots { get; set; }
        public int QueuedCount { get; set; }
        public int MaxQueueSize { get; set; }
    }

    #endregion

    #region Resilience Pipeline

    /// <summary>
    /// Combines multiple resilience patterns
    /// </summary>
    public class ResiliencePipeline
    {
        private readonly string _name;
        private readonly RetryPolicy? _retryPolicy;
        private readonly CircuitBreaker? _circuitBreaker;
        private readonly RateLimiter? _rateLimiter;
        private readonly Bulkhead? _bulkhead;
        private readonly TimeoutHandler? _timeoutHandler;
        private readonly TimeSpan? _timeout;
        private readonly ILogger? _logger;

        public ResiliencePipeline(
            string name,
            RetryPolicy? retryPolicy = null,
            CircuitBreaker? circuitBreaker = null,
            RateLimiter? rateLimiter = null,
            Bulkhead? bulkhead = null,
            TimeoutHandler? timeoutHandler = null,
            TimeSpan? timeout = null,
            ILogger? logger = null)
        {
            _name = name;
            _retryPolicy = retryPolicy;
            _circuitBreaker = circuitBreaker;
            _rateLimiter = rateLimiter;
            _bulkhead = bulkhead;
            _timeoutHandler = timeoutHandler;
            _timeout = timeout;
            _logger = logger;
        }

        /// <summary>
        /// Execute through the resilience pipeline
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken ct = default,
            [CallerMemberName] string? operationName = null)
        {
            // Rate limiting (outermost)
            _rateLimiter?.Acquire();

            // Bulkhead
            Func<CancellationToken, Task<T>> bulkheadAction = _bulkhead != null
                ? ct2 => _bulkhead.ExecuteAsync(action, _timeout, ct2)
                : action;

            // Circuit breaker
            Func<CancellationToken, Task<T>> circuitAction = _circuitBreaker != null
                ? ct2 => _circuitBreaker.ExecuteAsync(bulkheadAction, ct2)
                : bulkheadAction;

            // Timeout
            Func<CancellationToken, Task<T>> timeoutAction =
                _timeoutHandler != null && _timeout.HasValue
                    ? ct2 => _timeoutHandler.ExecuteAsync(circuitAction, _timeout.Value, ct2, operationName)
                    : circuitAction;

            // Retry (innermost, wraps everything)
            Func<CancellationToken, Task<T>> retryAction = _retryPolicy != null
                ? ct2 => _retryPolicy.ExecuteAsync(timeoutAction, ct2, operationName)
                : timeoutAction;

            try
            {
                return await retryAction(ct);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Pipeline {Name} failed for operation {Operation}",
                    _name, operationName);
                throw;
            }
        }

        /// <summary>
        /// Execute with result type
        /// </summary>
        public async Task<Result<T>> ExecuteSafeAsync<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken ct = default,
            [CallerMemberName] string? operationName = null)
        {
            try
            {
                var result = await ExecuteAsync(action, ct, operationName);
                return Result<T>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<T>.FromException(ex);
            }
        }
    }

    /// <summary>
    /// Builder for resilience pipeline
    /// </summary>
    public class ResiliencePipelineBuilder
    {
        private string _name = "default";
        private RetryPolicy? _retryPolicy;
        private CircuitBreaker? _circuitBreaker;
        private RateLimiter? _rateLimiter;
        private Bulkhead? _bulkhead;
        private TimeoutHandler? _timeoutHandler;
        private TimeSpan? _timeout;
        private ILogger? _logger;

        public ResiliencePipelineBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public ResiliencePipelineBuilder WithRetry(RetryOptions? options = null)
        {
            _retryPolicy = new RetryPolicy(options, _logger);
            return this;
        }

        public ResiliencePipelineBuilder WithCircuitBreaker(CircuitBreakerOptions? options = null)
        {
            _circuitBreaker = new CircuitBreaker(_name, options, _logger);
            return this;
        }

        public ResiliencePipelineBuilder WithRateLimit(int maxTokens, TimeSpan refillInterval)
        {
            _rateLimiter = new RateLimiter(maxTokens, refillInterval, logger: _logger);
            return this;
        }

        public ResiliencePipelineBuilder WithBulkhead(int maxConcurrency, int maxQueueSize = 100)
        {
            _bulkhead = new Bulkhead(_name, maxConcurrency, maxQueueSize, _logger);
            return this;
        }

        public ResiliencePipelineBuilder WithTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
            _timeoutHandler = new TimeoutHandler(_logger);
            return this;
        }

        public ResiliencePipelineBuilder WithLogger(ILogger logger)
        {
            _logger = logger;
            return this;
        }

        public ResiliencePipeline Build()
        {
            return new ResiliencePipeline(
                _name,
                _retryPolicy,
                _circuitBreaker,
                _rateLimiter,
                _bulkhead,
                _timeoutHandler,
                _timeout,
                _logger);
        }
    }

    #endregion

    #region Health Check

    /// <summary>
    /// Health check result
    /// </summary>
    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }

    /// <summary>
    /// Health check result
    /// </summary>
    public class HealthCheckResult
    {
        public HealthStatus Status { get; set; } = HealthStatus.Healthy;
        public string Name { get; set; } = string.Empty;
        public string? Message { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Aggregated health result
    /// </summary>
    public class AggregatedHealthResult
    {
        public HealthStatus OverallStatus { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan TotalDuration { get; set; }
        public List<HealthCheckResult> Checks { get; set; } = new();
    }

    /// <summary>
    /// Health check manager
    /// </summary>
    public class HealthCheckManager
    {
        private readonly ConcurrentDictionary<string, Func<CancellationToken, Task<HealthCheckResult>>> _checks = new();
        private readonly ILogger? _logger;

        public HealthCheckManager(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Register a health check
        /// </summary>
        public void Register(string name, Func<CancellationToken, Task<HealthCheckResult>> check)
        {
            _checks[name] = check;
        }

        /// <summary>
        /// Run all health checks
        /// </summary>
        public async Task<AggregatedHealthResult> CheckAsync(CancellationToken ct = default)
        {
            var result = new AggregatedHealthResult();
            var stopwatch = Stopwatch.StartNew();

            var tasks = _checks.Select(async kvp =>
            {
                var checkStopwatch = Stopwatch.StartNew();
                try
                {
                    var checkResult = await kvp.Value(ct);
                    checkResult.Name = kvp.Key;
                    checkResult.Duration = checkStopwatch.Elapsed;
                    return checkResult;
                }
                catch (Exception ex)
                {
                    return new HealthCheckResult
                    {
                        Name = kvp.Key,
                        Status = HealthStatus.Unhealthy,
                        Message = ex.Message,
                        Exception = ex,
                        Duration = checkStopwatch.Elapsed
                    };
                }
            });

            result.Checks = (await Task.WhenAll(tasks)).ToList();
            result.TotalDuration = stopwatch.Elapsed;

            // Determine overall status
            if (result.Checks.Any(c => c.Status == HealthStatus.Unhealthy))
                result.OverallStatus = HealthStatus.Unhealthy;
            else if (result.Checks.Any(c => c.Status == HealthStatus.Degraded))
                result.OverallStatus = HealthStatus.Degraded;
            else
                result.OverallStatus = HealthStatus.Healthy;

            _logger?.LogInformation("Health check completed: {Status} in {Duration}ms",
                result.OverallStatus, result.TotalDuration.TotalMilliseconds);

            return result;
        }
    }

    #endregion
}
