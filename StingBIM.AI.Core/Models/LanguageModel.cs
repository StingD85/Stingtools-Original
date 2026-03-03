// StingBIM.AI.Core.Models.LanguageModel
// ONNX-based language model wrapper for Phi-3-mini
// Master Proposal Reference: Part 1.2 Component Specifications

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NLog;

namespace StingBIM.AI.Core.Models
{
    /// <summary>
    /// Wrapper for the Phi-3-mini-4k ONNX language model.
    /// Provides natural language understanding and generation capabilities.
    /// Target: < 200ms command understanding latency (Part 5.2)
    /// </summary>
    public class LanguageModel : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private InferenceSession _session;
        private readonly object _sessionLock = new object();
        private bool _isLoaded;
        private bool _disposed;

        // Model configuration
        public string ModelPath { get; private set; }
        public int MaxSequenceLength { get; set; } = 4096;
        public int VocabSize { get; private set; }
        public bool IsGpuEnabled { get; private set; }

        /// <summary>
        /// Loads the ONNX language model from the specified path.
        /// </summary>
        /// <param name="modelPath">Path to the ONNX model file</param>
        /// <param name="useGpu">Whether to use GPU acceleration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task LoadModelAsync(string modelPath, bool useGpu = false, CancellationToken cancellationToken = default)
        {
            Logger.Info($"Loading language model from: {modelPath}");

            await Task.Run(() =>
            {
                try
                {
                    var options = new SessionOptions();

                    if (useGpu)
                    {
                        try
                        {
                            options.AppendExecutionProvider_CUDA(0);
                            IsGpuEnabled = true;
                            Logger.Info("GPU acceleration enabled (CUDA)");
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"GPU not available, falling back to CPU: {ex.Message}");
                            IsGpuEnabled = false;
                        }
                    }

                    options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                    options.InterOpNumThreads = Environment.ProcessorCount;
                    options.IntraOpNumThreads = Environment.ProcessorCount;

                    lock (_sessionLock)
                    {
                        _session = new InferenceSession(modelPath, options);
                        ModelPath = modelPath;
                        _isLoaded = true;
                    }

                    Logger.Info("Language model loaded successfully");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load language model");
                    throw;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Generates a response from the language model.
        /// </summary>
        /// <param name="inputTokens">Tokenized input</param>
        /// <param name="maxNewTokens">Maximum new tokens to generate</param>
        /// <param name="temperature">Sampling temperature</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated token IDs</returns>
        public async Task<long[]> GenerateAsync(
            long[] inputTokens,
            int maxNewTokens = 256,
            float temperature = 0.7f,
            CancellationToken cancellationToken = default)
        {
            EnsureModelLoaded();

            return await Task.Run(() =>
            {
                lock (_sessionLock)
                {
                    // Create input tensor
                    var inputTensor = new DenseTensor<long>(inputTokens, new[] { 1, inputTokens.Length });
                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor("input_ids", inputTensor)
                    };

                    // Run inference
                    using (var results = _session.Run(inputs))
                    {
                        // Extract output logits and sample with temperature
                        var outputTensor = results[0].AsTensor<float>();
                        return SampleFromLogits(outputTensor, temperature, maxNewTokens);
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Gets embeddings for the input tokens (hidden states).
        /// </summary>
        public async Task<float[]> GetEmbeddingsAsync(long[] inputTokens, CancellationToken cancellationToken = default)
        {
            EnsureModelLoaded();

            return await Task.Run(() =>
            {
                lock (_sessionLock)
                {
                    var inputTensor = new DenseTensor<long>(inputTokens, new[] { 1, inputTokens.Length });
                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor("input_ids", inputTensor)
                    };

                    using (var results = _session.Run(inputs))
                    {
                        // Return the last hidden state as embeddings
                        var hiddenStates = results[0].AsTensor<float>();
                        return hiddenStates.ToArray();
                    }
                }
            }, cancellationToken);
        }

        private long[] SampleFromLogits(Tensor<float> logits, float temperature, int maxNewTokens = 256, int topK = 40, float topP = 0.9f)
        {
            var outputTokens = new List<long>();
            var random = new Random();

            // Get dimensions from logits tensor
            var dimensions = logits.Dimensions.ToArray();
            if (dimensions.Length < 2) return outputTokens.ToArray();

            int sequenceLength = dimensions[dimensions.Length - 2];
            int vocabSize = dimensions[dimensions.Length - 1];

            // Get logits for the last position in the sequence
            int lastPosition = sequenceLength - 1;
            var lastLogits = new float[vocabSize];

            for (int v = 0; v < vocabSize; v++)
            {
                // Access the logits for the last token position
                int index = lastPosition * vocabSize + v;
                if (index < logits.Length)
                {
                    lastLogits[v] = logits.GetValue(index);
                }
            }

            // Apply temperature scaling
            if (temperature > 0 && temperature != 1.0f)
            {
                for (int i = 0; i < lastLogits.Length; i++)
                {
                    lastLogits[i] /= temperature;
                }
            }

            // Apply top-k filtering
            if (topK > 0 && topK < vocabSize)
            {
                lastLogits = ApplyTopKFiltering(lastLogits, topK);
            }

            // Apply top-p (nucleus) filtering
            if (topP < 1.0f)
            {
                lastLogits = ApplyTopPFiltering(lastLogits, topP);
            }

            // Convert to probabilities using softmax
            var probabilities = Softmax(lastLogits);

            // Sample from the probability distribution
            long sampledToken = SampleFromDistribution(probabilities, random);
            outputTokens.Add(sampledToken);

            return outputTokens.ToArray();
        }

        /// <summary>
        /// Applies top-k filtering to logits, keeping only the k highest values.
        /// </summary>
        private float[] ApplyTopKFiltering(float[] logits, int k)
        {
            var result = new float[logits.Length];
            Array.Copy(logits, result, logits.Length);

            // Find the k-th highest value
            var sorted = logits.OrderByDescending(x => x).ToArray();
            float threshold = sorted[Math.Min(k - 1, sorted.Length - 1)];

            // Set all values below threshold to negative infinity
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] < threshold)
                {
                    result[i] = float.NegativeInfinity;
                }
            }

            return result;
        }

        /// <summary>
        /// Applies top-p (nucleus) filtering to logits.
        /// </summary>
        private float[] ApplyTopPFiltering(float[] logits, float p)
        {
            var result = new float[logits.Length];
            Array.Copy(logits, result, logits.Length);

            // Convert to probabilities
            var probs = Softmax(logits);

            // Sort by probability descending and track indices
            var sortedIndices = Enumerable.Range(0, probs.Length)
                .OrderByDescending(i => probs[i])
                .ToArray();

            // Find cumulative probability threshold
            float cumSum = 0;
            int cutoffIndex = sortedIndices.Length;

            for (int i = 0; i < sortedIndices.Length; i++)
            {
                cumSum += probs[sortedIndices[i]];
                if (cumSum > p)
                {
                    cutoffIndex = i + 1;
                    break;
                }
            }

            // Create mask for tokens to keep
            var keepMask = new HashSet<int>(sortedIndices.Take(cutoffIndex));

            // Set filtered tokens to negative infinity
            for (int i = 0; i < result.Length; i++)
            {
                if (!keepMask.Contains(i))
                {
                    result[i] = float.NegativeInfinity;
                }
            }

            return result;
        }

        /// <summary>
        /// Computes softmax probabilities from logits.
        /// </summary>
        private float[] Softmax(float[] logits)
        {
            var result = new float[logits.Length];

            // Find max for numerical stability
            float maxLogit = logits.Max();

            // Compute exp(logits - max) and sum
            float sum = 0;
            for (int i = 0; i < logits.Length; i++)
            {
                if (float.IsNegativeInfinity(logits[i]))
                {
                    result[i] = 0;
                }
                else
                {
                    result[i] = (float)Math.Exp(logits[i] - maxLogit);
                    sum += result[i];
                }
            }

            // Normalize
            if (sum > 0)
            {
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] /= sum;
                }
            }

            return result;
        }

        /// <summary>
        /// Samples a token index from a probability distribution.
        /// </summary>
        private long SampleFromDistribution(float[] probabilities, Random random)
        {
            float sample = (float)random.NextDouble();
            float cumSum = 0;

            for (int i = 0; i < probabilities.Length; i++)
            {
                cumSum += probabilities[i];
                if (sample < cumSum)
                {
                    return i;
                }
            }

            // Fallback to last valid token
            return probabilities.Length - 1;
        }

        /// <summary>
        /// Performs greedy decoding (temperature = 0).
        /// </summary>
        private long GreedySample(float[] logits)
        {
            int maxIdx = 0;
            float maxVal = logits[0];

            for (int i = 1; i < logits.Length; i++)
            {
                if (logits[i] > maxVal)
                {
                    maxVal = logits[i];
                    maxIdx = i;
                }
            }

            return maxIdx;
        }

        #region Autoregressive Text Generation (T3-11)

        /// <summary>
        /// Generates a full text sequence autoregressively by running the model in a loop,
        /// appending each sampled token to the input until a stop condition is met.
        /// Supports repetition penalty, stop tokens, and early termination.
        /// </summary>
        public async Task<GenerationResult> GenerateTextAsync(
            long[] inputTokens,
            GenerationConfig config = null,
            CancellationToken cancellationToken = default)
        {
            EnsureModelLoaded();
            config ??= new GenerationConfig();

            var result = new GenerationResult
            {
                InputTokenCount = inputTokens.Length,
                GeneratedTokens = new List<long>(),
                TokenConfidences = new List<float>()
            };

            var currentSequence = new List<long>(inputTokens);
            var startTime = DateTime.UtcNow;

            for (int step = 0; step < config.MaxNewTokens; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Truncate to max sequence length if needed
                var inputSlice = currentSequence.Count > MaxSequenceLength
                    ? currentSequence.Skip(currentSequence.Count - MaxSequenceLength).ToArray()
                    : currentSequence.ToArray();

                // Run single forward pass
                var (nextToken, confidence) = await GenerateNextTokenAsync(
                    inputSlice, config, result.GeneratedTokens, cancellationToken);

                // Check stop tokens
                if (config.StopTokenIds != null && config.StopTokenIds.Contains(nextToken))
                {
                    result.StopReason = StopReason.StopToken;
                    break;
                }

                result.GeneratedTokens.Add(nextToken);
                result.TokenConfidences.Add(confidence);
                currentSequence.Add(nextToken);

                // Check max length
                if (step == config.MaxNewTokens - 1)
                {
                    result.StopReason = StopReason.MaxLength;
                }
            }

            result.TotalTokenCount = currentSequence.Count;
            result.GenerationTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            result.TokensPerSecond = result.GeneratedTokens.Count > 0
                ? result.GeneratedTokens.Count / (result.GenerationTimeMs / 1000.0)
                : 0;

            return result;
        }

        /// <summary>
        /// Generates a single next token with repetition penalty and confidence tracking.
        /// </summary>
        private async Task<(long Token, float Confidence)> GenerateNextTokenAsync(
            long[] inputTokens,
            GenerationConfig config,
            List<long> previouslyGenerated,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                lock (_sessionLock)
                {
                    var inputTensor = new DenseTensor<long>(inputTokens, new[] { 1, inputTokens.Length });
                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor("input_ids", inputTensor)
                    };

                    using (var results = _session.Run(inputs))
                    {
                        var outputTensor = results[0].AsTensor<float>();
                        var dimensions = outputTensor.Dimensions.ToArray();
                        if (dimensions.Length < 2) return (0L, 0f);

                        int sequenceLength = dimensions[dimensions.Length - 2];
                        int vocabSize = dimensions[dimensions.Length - 1];
                        int lastPosition = sequenceLength - 1;

                        // Extract logits for last position
                        var logits = new float[vocabSize];
                        for (int v = 0; v < vocabSize; v++)
                        {
                            int index = lastPosition * vocabSize + v;
                            if (index < outputTensor.Length)
                                logits[v] = outputTensor.GetValue(index);
                        }

                        // Apply repetition penalty
                        if (config.RepetitionPenalty > 1.0f && previouslyGenerated.Count > 0)
                        {
                            ApplyRepetitionPenalty(logits, previouslyGenerated, config.RepetitionPenalty);
                        }

                        // Temperature scaling
                        if (config.Temperature > 0 && config.Temperature != 1.0f)
                        {
                            for (int i = 0; i < logits.Length; i++)
                                logits[i] /= config.Temperature;
                        }

                        // Top-k filtering
                        if (config.TopK > 0 && config.TopK < vocabSize)
                        {
                            logits = ApplyTopKFiltering(logits, config.TopK);
                        }

                        // Top-p filtering
                        if (config.TopP < 1.0f)
                        {
                            logits = ApplyTopPFiltering(logits, config.TopP);
                        }

                        // Compute probabilities and sample
                        var probs = Softmax(logits);
                        var random = new Random();
                        long token;
                        float confidence;

                        if (config.Temperature == 0)
                        {
                            token = GreedySample(logits);
                            confidence = probs[token];
                        }
                        else
                        {
                            token = SampleFromDistribution(probs, random);
                            confidence = probs[token];
                        }

                        return (token, confidence);
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Applies repetition penalty to logits for tokens that have already been generated.
        /// Tokens seen before get their logits divided (if positive) or multiplied (if negative) by the penalty.
        /// </summary>
        private void ApplyRepetitionPenalty(float[] logits, List<long> generatedTokens, float penalty)
        {
            var seenTokens = new HashSet<long>(generatedTokens);
            foreach (var tokenId in seenTokens)
            {
                if (tokenId >= 0 && tokenId < logits.Length)
                {
                    if (logits[tokenId] > 0)
                    {
                        logits[tokenId] /= penalty;
                    }
                    else
                    {
                        logits[tokenId] *= penalty;
                    }
                }
            }
        }

        #endregion

        private void EnsureModelLoaded()
        {
            if (!_isLoaded)
            {
                throw new InvalidOperationException("Language model not loaded. Call LoadModelAsync first.");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (_sessionLock)
                    {
                        _session?.Dispose();
                        _session = null;
                    }
                }
                _disposed = true;
            }
        }
    }

    #region Generation Types (T3-11)

    /// <summary>
    /// Configuration for autoregressive text generation.
    /// </summary>
    public class GenerationConfig
    {
        public int MaxNewTokens { get; set; } = 256;
        public float Temperature { get; set; } = 0.7f;
        public int TopK { get; set; } = 40;
        public float TopP { get; set; } = 0.9f;
        public float RepetitionPenalty { get; set; } = 1.2f;
        public HashSet<long> StopTokenIds { get; set; }
    }

    /// <summary>
    /// Result of autoregressive text generation.
    /// </summary>
    public class GenerationResult
    {
        public int InputTokenCount { get; set; }
        public int TotalTokenCount { get; set; }
        public List<long> GeneratedTokens { get; set; } = new List<long>();
        public List<float> TokenConfidences { get; set; } = new List<float>();
        public StopReason StopReason { get; set; } = StopReason.MaxLength;
        public double GenerationTimeMs { get; set; }
        public double TokensPerSecond { get; set; }
        public float AverageConfidence => TokenConfidences.Count > 0 ? TokenConfidences.Average() : 0f;
    }

    public enum StopReason
    {
        MaxLength,
        StopToken,
        EndOfSequence
    }

    #endregion
}
