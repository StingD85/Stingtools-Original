// ============================================================================
// StingBIM AI Core - ONNX Model Loader
// Centralized model loading and inference management for AI models
// Supports Phi-3-mini, MiniLM, and Whisper models
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Core.ModelLoader
{
    /// <summary>
    /// Centralized ONNX model loader and manager.
    /// Handles model loading, caching, and lifecycle management.
    /// </summary>
    public class ONNXModelLoader : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Lazy<ONNXModelLoader> _instance =
            new Lazy<ONNXModelLoader>(() => new ONNXModelLoader());

        private readonly ConcurrentDictionary<string, LoadedModel> _loadedModels;
        private readonly ConcurrentDictionary<string, ModelMetadata> _modelRegistry;
        private readonly SemaphoreSlim _loadSemaphore;
        private readonly object _lock = new object();
        private bool _disposed;

        // Configuration
        public string ModelsDirectory { get; set; } = "models";
        public int MaxConcurrentLoads { get; set; } = 2;
        public long MaxMemoryUsageMB { get; set; } = 4096; // 4GB default

        // Events
        public event EventHandler<ModelLoadedEventArgs> ModelLoaded;
        public event EventHandler<ModelUnloadedEventArgs> ModelUnloaded;
        public event EventHandler<ModelErrorEventArgs> ModelError;

        public static ONNXModelLoader Instance => _instance.Value;

        public ONNXModelLoader()
        {
            _loadedModels = new ConcurrentDictionary<string, LoadedModel>();
            _modelRegistry = new ConcurrentDictionary<string, ModelMetadata>();
            _loadSemaphore = new SemaphoreSlim(MaxConcurrentLoads);

            InitializeModelRegistry();
        }

        #region Model Registration

        /// <summary>
        /// Registers a model for loading.
        /// </summary>
        public void RegisterModel(ModelMetadata metadata)
        {
            _modelRegistry[metadata.ModelId] = metadata;
            Logger.Info($"Registered model: {metadata.ModelId} ({metadata.ModelType})");
        }

        /// <summary>
        /// Gets metadata for a registered model.
        /// </summary>
        public ModelMetadata GetModelMetadata(string modelId)
        {
            return _modelRegistry.TryGetValue(modelId, out var metadata) ? metadata : null;
        }

        /// <summary>
        /// Gets all registered models.
        /// </summary>
        public IEnumerable<ModelMetadata> GetRegisteredModels()
        {
            return _modelRegistry.Values;
        }

        private void InitializeModelRegistry()
        {
            // Language Model (Phi-3-mini-4k)
            RegisterModel(new ModelMetadata
            {
                ModelId = "phi-3-mini-4k",
                ModelType = ModelType.LanguageModel,
                DisplayName = "Phi-3 Mini 4K",
                Description = "Small language model for natural language understanding",
                FileName = "phi-3-mini-4k-instruct.onnx",
                ExpectedSizeMB = 2300,
                InputNames = new List<string> { "input_ids", "attention_mask" },
                OutputNames = new List<string> { "logits" },
                MaxSequenceLength = 4096,
                RequiredMemoryMB = 3000
            });

            // Embedding Model (MiniLM)
            RegisterModel(new ModelMetadata
            {
                ModelId = "all-MiniLM-L6-v2",
                ModelType = ModelType.EmbeddingModel,
                DisplayName = "MiniLM L6 v2",
                Description = "Sentence embedding model for semantic similarity",
                FileName = "all-MiniLM-L6-v2.onnx",
                ExpectedSizeMB = 22,
                InputNames = new List<string> { "input_ids", "attention_mask", "token_type_ids" },
                OutputNames = new List<string> { "sentence_embedding" },
                MaxSequenceLength = 256,
                EmbeddingDimension = 384,
                RequiredMemoryMB = 100
            });

            // Speech Model (Whisper)
            RegisterModel(new ModelMetadata
            {
                ModelId = "whisper-tiny",
                ModelType = ModelType.SpeechModel,
                DisplayName = "Whisper Tiny",
                Description = "Speech-to-text model for voice commands",
                FileName = "whisper-tiny.onnx",
                ExpectedSizeMB = 75,
                InputNames = new List<string> { "audio_input" },
                OutputNames = new List<string> { "transcription" },
                SampleRate = 16000,
                RequiredMemoryMB = 200
            });
        }

        #endregion

        #region Model Loading

        /// <summary>
        /// Loads a model asynchronously.
        /// </summary>
        public async Task<LoadedModel> LoadModelAsync(
            string modelId,
            CancellationToken cancellationToken = default)
        {
            // Check if already loaded
            if (_loadedModels.TryGetValue(modelId, out var existing))
            {
                existing.LastAccessTime = DateTime.UtcNow;
                return existing;
            }

            // Get metadata
            var metadata = GetModelMetadata(modelId);
            if (metadata == null)
            {
                throw new ArgumentException($"Model not registered: {modelId}");
            }

            // Check memory constraints
            await EnsureMemoryAvailableAsync(metadata.RequiredMemoryMB, cancellationToken);

            // Load with semaphore to limit concurrent loads
            await _loadSemaphore.WaitAsync(cancellationToken);

            try
            {
                // Double-check after acquiring semaphore
                if (_loadedModels.TryGetValue(modelId, out existing))
                {
                    return existing;
                }

                Logger.Info($"Loading model: {modelId}");
                var stopwatch = Stopwatch.StartNew();

                var modelPath = GetModelPath(metadata);
                if (!File.Exists(modelPath))
                {
                    Logger.Warn($"Model file not found: {modelPath}. AI features requiring this model will be unavailable.");
                    Logger.Warn("To enable AI features, download ONNX models to: " +
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StingBIM", "Models"));

                    // Return a stub model so the system can continue without AI features
                    var stubModel = new LoadedModel
                    {
                        ModelId = modelId,
                        Metadata = metadata,
                        ModelPath = modelPath,
                        LoadedAt = DateTime.UtcNow,
                        LastAccessTime = DateTime.UtcNow,
                        IsStub = true
                    };
                    _loadedModels.TryAdd(modelId, stubModel);
                    return stubModel;
                }

                // Create inference session placeholder
                // In real implementation, use Microsoft.ML.OnnxRuntime.InferenceSession
                var loadedModel = new LoadedModel
                {
                    ModelId = modelId,
                    Metadata = metadata,
                    ModelPath = modelPath,
                    LoadedAt = DateTime.UtcNow,
                    LastAccessTime = DateTime.UtcNow,
                    // Session = new InferenceSession(modelPath)  // Uncomment with ONNX Runtime
                };

                // Validate model
                await ValidateModelAsync(loadedModel, cancellationToken);

                _loadedModels[modelId] = loadedModel;

                stopwatch.Stop();
                loadedModel.LoadTimeMs = stopwatch.ElapsedMilliseconds;

                Logger.Info($"Model loaded: {modelId} in {loadedModel.LoadTimeMs}ms");
                ModelLoaded?.Invoke(this, new ModelLoadedEventArgs(loadedModel));

                return loadedModel;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load model: {modelId}");
                ModelError?.Invoke(this, new ModelErrorEventArgs(modelId, ex));
                throw;
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }

        /// <summary>
        /// Loads multiple models in parallel.
        /// </summary>
        public async Task<IEnumerable<LoadedModel>> LoadModelsAsync(
            IEnumerable<string> modelIds,
            CancellationToken cancellationToken = default)
        {
            var tasks = modelIds.Select(id => LoadModelAsync(id, cancellationToken));
            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Unloads a model to free memory.
        /// </summary>
        public void UnloadModel(string modelId)
        {
            if (_loadedModels.TryRemove(modelId, out var model))
            {
                model.Dispose();
                Logger.Info($"Model unloaded: {modelId}");
                ModelUnloaded?.Invoke(this, new ModelUnloadedEventArgs(modelId));
            }
        }

        /// <summary>
        /// Checks if a model is loaded.
        /// </summary>
        public bool IsModelLoaded(string modelId)
        {
            return _loadedModels.ContainsKey(modelId);
        }

        /// <summary>
        /// Gets a loaded model.
        /// </summary>
        public LoadedModel GetLoadedModel(string modelId)
        {
            if (_loadedModels.TryGetValue(modelId, out var model))
            {
                model.LastAccessTime = DateTime.UtcNow;
                return model;
            }
            return null;
        }

        #endregion

        #region Inference

        /// <summary>
        /// Runs inference on a loaded model.
        /// </summary>
        public async Task<InferenceResult> RunInferenceAsync(
            string modelId,
            Dictionary<string, object> inputs,
            CancellationToken cancellationToken = default)
        {
            var model = GetLoadedModel(modelId);
            if (model == null)
            {
                // Auto-load if not loaded
                model = await LoadModelAsync(modelId, cancellationToken);
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // In real implementation, run inference using ONNX Runtime
                // var results = model.Session.Run(inputs);

                var result = new InferenceResult
                {
                    ModelId = modelId,
                    Success = true,
                    InferenceTimeMs = stopwatch.ElapsedMilliseconds,
                    Outputs = new Dictionary<string, object>()
                };

                // Update metrics
                model.InferenceCount++;
                model.TotalInferenceTimeMs += result.InferenceTimeMs;

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Inference failed for model: {modelId}");
                return new InferenceResult
                {
                    ModelId = modelId,
                    Success = false,
                    ErrorMessage = ex.Message,
                    InferenceTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        /// <summary>
        /// Generates embeddings using the embedding model.
        /// </summary>
        public async Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            var modelId = "all-MiniLM-L6-v2";
            var model = await LoadModelAsync(modelId, cancellationToken);

            // Placeholder - in real implementation, tokenize and run inference
            // For now, return a placeholder embedding
            var embeddingDim = model.Metadata.EmbeddingDimension ?? 384;
            var embedding = new float[embeddingDim];

            // Simple hash-based placeholder
            var hash = text.GetHashCode();
            var random = new Random(hash);
            for (int i = 0; i < embeddingDim; i++)
            {
                embedding[i] = (float)(random.NextDouble() * 2 - 1);
            }

            // Normalize
            var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
            for (int i = 0; i < embeddingDim; i++)
            {
                embedding[i] /= magnitude;
            }

            return embedding;
        }

        #endregion

        #region Memory Management

        /// <summary>
        /// Gets current memory usage statistics.
        /// </summary>
        public ModelMemoryStats GetMemoryStats()
        {
            var stats = new ModelMemoryStats
            {
                LoadedModelCount = _loadedModels.Count,
                TotalAllocatedMB = _loadedModels.Values.Sum(m => m.Metadata.RequiredMemoryMB),
                MaxAllowedMB = MaxMemoryUsageMB
            };

            foreach (var model in _loadedModels.Values)
            {
                stats.ModelStats[model.ModelId] = new SingleModelStats
                {
                    MemoryMB = model.Metadata.RequiredMemoryMB,
                    InferenceCount = model.InferenceCount,
                    AverageInferenceMs = model.InferenceCount > 0
                        ? model.TotalInferenceTimeMs / model.InferenceCount
                        : 0,
                    LastAccessTime = model.LastAccessTime
                };
            }

            return stats;
        }

        /// <summary>
        /// Ensures memory is available for loading a model.
        /// </summary>
        private async Task EnsureMemoryAvailableAsync(
            long requiredMB,
            CancellationToken cancellationToken)
        {
            var stats = GetMemoryStats();
            var availableMB = MaxMemoryUsageMB - stats.TotalAllocatedMB;

            if (availableMB >= requiredMB)
            {
                return;
            }

            Logger.Warn($"Insufficient memory. Required: {requiredMB}MB, Available: {availableMB}MB. Unloading least recently used models.");

            // Unload least recently used models
            var modelsToUnload = _loadedModels.Values
                .OrderBy(m => m.LastAccessTime)
                .ToList();

            foreach (var model in modelsToUnload)
            {
                cancellationToken.ThrowIfCancellationRequested();

                UnloadModel(model.ModelId);
                availableMB += model.Metadata.RequiredMemoryMB;

                if (availableMB >= requiredMB)
                {
                    break;
                }
            }

            if (availableMB < requiredMB)
            {
                throw new OutOfMemoryException(
                    $"Cannot allocate {requiredMB}MB. Maximum: {MaxMemoryUsageMB}MB");
            }

            // Allow GC to collect
            GC.Collect();
            await Task.Delay(100, cancellationToken);
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates a model file before loading.
        /// </summary>
        public async Task<ModelValidationResult> ValidateModelFileAsync(
            string modelPath,
            CancellationToken cancellationToken = default)
        {
            var result = new ModelValidationResult { ModelPath = modelPath };

            if (!File.Exists(modelPath))
            {
                result.IsValid = false;
                result.Errors.Add("Model file not found");
                return result;
            }

            var fileInfo = new FileInfo(modelPath);
            result.FileSizeMB = fileInfo.Length / (1024 * 1024);

            // Check file extension
            if (!modelPath.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add("File does not have .onnx extension");
            }

            // Validate ONNX header (magic number)
            try
            {
                using var stream = File.OpenRead(modelPath);
                var header = new byte[8];
                await stream.ReadAsync(header, 0, 8, cancellationToken);

                // ONNX files should start with specific bytes
                // This is a simplified check
                if (header[0] != 0x08)
                {
                    result.Warnings.Add("File may not be a valid ONNX model");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error reading file: {ex.Message}");
            }

            result.IsValid = !result.Errors.Any();
            return result;
        }

        private async Task ValidateModelAsync(
            LoadedModel model,
            CancellationToken cancellationToken)
        {
            var validation = await ValidateModelFileAsync(model.ModelPath, cancellationToken);

            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Model validation failed: {string.Join(", ", validation.Errors)}");
            }

            foreach (var warning in validation.Warnings)
            {
                Logger.Warn($"Model {model.ModelId}: {warning}");
            }
        }

        #endregion

        #region Helpers

        private string GetModelPath(ModelMetadata metadata)
        {
            var basePath = ModelsDirectory;

            // Check environment variable
            var envPath = Environment.GetEnvironmentVariable("STINGBIM_MODELS_PATH");
            if (!string.IsNullOrEmpty(envPath))
            {
                basePath = envPath;
            }

            return Path.Combine(basePath, metadata.FileName);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var model in _loadedModels.Values)
            {
                model.Dispose();
            }

            _loadedModels.Clear();
            _loadSemaphore.Dispose();

            Logger.Info("ONNX Model Loader disposed");
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Metadata describing a model.
    /// </summary>
    public class ModelMetadata
    {
        public string ModelId { get; set; }
        public ModelType ModelType { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string FileName { get; set; }
        public long ExpectedSizeMB { get; set; }
        public long RequiredMemoryMB { get; set; }
        public List<string> InputNames { get; set; } = new List<string>();
        public List<string> OutputNames { get; set; } = new List<string>();
        public int MaxSequenceLength { get; set; }
        public int? EmbeddingDimension { get; set; }
        public int? SampleRate { get; set; }
    }

    /// <summary>
    /// Model type enumeration.
    /// </summary>
    public enum ModelType
    {
        LanguageModel,
        EmbeddingModel,
        SpeechModel,
        VisionModel,
        Custom
    }

    /// <summary>
    /// Loaded model instance.
    /// </summary>
    public class LoadedModel : IDisposable
    {
        public string ModelId { get; set; }
        public ModelMetadata Metadata { get; set; }
        public string ModelPath { get; set; }
        public DateTime LoadedAt { get; set; }
        public DateTime LastAccessTime { get; set; }
        public long LoadTimeMs { get; set; }
        public long InferenceCount { get; set; }
        public long TotalInferenceTimeMs { get; set; }
        /// <summary>
        /// True if this is a stub model (ONNX file not found). AI features will be unavailable.
        /// </summary>
        public bool IsStub { get; set; }

        // In real implementation, this would be InferenceSession
        // public InferenceSession Session { get; set; }

        public void Dispose()
        {
            // Session?.Dispose();
        }
    }

    /// <summary>
    /// Result of model inference.
    /// </summary>
    public class InferenceResult
    {
        public string ModelId { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public long InferenceTimeMs { get; set; }
        public Dictionary<string, object> Outputs { get; set; }
    }

    /// <summary>
    /// Memory statistics for loaded models.
    /// </summary>
    public class ModelMemoryStats
    {
        public int LoadedModelCount { get; set; }
        public long TotalAllocatedMB { get; set; }
        public long MaxAllowedMB { get; set; }
        public Dictionary<string, SingleModelStats> ModelStats { get; set; } = new Dictionary<string, SingleModelStats>();
    }

    /// <summary>
    /// Statistics for a single model.
    /// </summary>
    public class SingleModelStats
    {
        public long MemoryMB { get; set; }
        public long InferenceCount { get; set; }
        public long AverageInferenceMs { get; set; }
        public DateTime LastAccessTime { get; set; }
    }

    /// <summary>
    /// Model validation result.
    /// </summary>
    public class ModelValidationResult
    {
        public string ModelPath { get; set; }
        public bool IsValid { get; set; }
        public long FileSizeMB { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    // Event Args
    public class ModelLoadedEventArgs : EventArgs
    {
        public LoadedModel Model { get; }
        public ModelLoadedEventArgs(LoadedModel model) => Model = model;
    }

    public class ModelUnloadedEventArgs : EventArgs
    {
        public string ModelId { get; }
        public ModelUnloadedEventArgs(string modelId) => ModelId = modelId;
    }

    public class ModelErrorEventArgs : EventArgs
    {
        public string ModelId { get; }
        public Exception Error { get; }
        public ModelErrorEventArgs(string modelId, Exception error)
        {
            ModelId = modelId;
            Error = error;
        }
    }

    #endregion
}
