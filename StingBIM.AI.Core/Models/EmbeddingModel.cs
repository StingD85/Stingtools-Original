// StingBIM.AI.Core.Models.EmbeddingModel
// ONNX-based embedding model wrapper for MiniLM
// Master Proposal Reference: Part 1.2 Component Specifications - all-MiniLM-L6-v2 (22MB)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NLog;

namespace StingBIM.AI.Core.Models
{
    /// <summary>
    /// Wrapper for the all-MiniLM-L6-v2 ONNX embedding model.
    /// Provides semantic similarity and vector search capabilities.
    /// Model size: 22 MB (Part 1.2)
    /// </summary>
    public class EmbeddingModel : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private InferenceSession _session;
        private readonly object _sessionLock = new object();
        private bool _isLoaded;
        private bool _disposed;

        // Model configuration
        public string ModelPath { get; private set; }
        public int EmbeddingDimension { get; private set; } = 384; // MiniLM default
        public int MaxSequenceLength { get; set; } = 512;

        /// <summary>
        /// Loads the embedding model from the specified path.
        /// </summary>
        public async Task LoadModelAsync(string modelPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(modelPath)) throw new ArgumentException("Model path cannot be null or empty.", nameof(modelPath));

            Logger.Info($"Loading embedding model from: {modelPath}");

            await Task.Run(() =>
            {
                try
                {
                    var options = new SessionOptions();
                    options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                    options.InterOpNumThreads = Environment.ProcessorCount;

                    lock (_sessionLock)
                    {
                        _session = new InferenceSession(modelPath, options);
                        ModelPath = modelPath;
                        _isLoaded = true;
                    }

                    Logger.Info("Embedding model loaded successfully");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load embedding model");
                    throw;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Generates embedding vector for input text.
        /// </summary>
        /// <param name="inputIds">Tokenized input IDs</param>
        /// <param name="attentionMask">Attention mask</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Embedding vector (384 dimensions for MiniLM)</returns>
        public async Task<float[]> GetEmbeddingAsync(
            long[] inputIds,
            long[] attentionMask,
            CancellationToken cancellationToken = default)
        {
            if (inputIds == null) throw new ArgumentNullException(nameof(inputIds));

            EnsureModelLoaded();

            return await Task.Run(() =>
            {
                lock (_sessionLock)
                {
                    var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
                    var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });

                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                        NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
                    };

                    using (var results = _session.Run(inputs))
                    {
                        var outputTensor = results[0].AsTensor<float>();
                        return MeanPooling(outputTensor, attentionMask);
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Generates embeddings for multiple texts in batch.
        /// </summary>
        public async Task<float[][]> GetEmbeddingsBatchAsync(
            long[][] inputIdsBatch,
            long[][] attentionMaskBatch,
            CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task<float[]>>();
            for (int i = 0; i < inputIdsBatch.Length; i++)
            {
                int index = i;
                tasks.Add(GetEmbeddingAsync(inputIdsBatch[index], attentionMaskBatch[index], cancellationToken));
            }
            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Calculates cosine similarity between two embedding vectors.
        /// </summary>
        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Vectors must have the same dimension");

            float dotProduct = 0f;
            float normA = 0f;
            float normB = 0f;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        /// <summary>
        /// Finds the most similar vectors from a collection.
        /// </summary>
        public IEnumerable<(int Index, float Similarity)> FindMostSimilar(
            float[] queryEmbedding,
            IList<float[]> candidates,
            int topK = 5)
        {
            return candidates
                .Select((embedding, index) => (Index: index, Similarity: CosineSimilarity(queryEmbedding, embedding)))
                .OrderByDescending(x => x.Similarity)
                .Take(topK);
        }

        private float[] MeanPooling(Tensor<float> tokenEmbeddings, long[] attentionMask)
        {
            // Mean pooling: average of token embeddings weighted by attention mask
            var embeddingDim = EmbeddingDimension;
            var seqLength = attentionMask.Length;
            var result = new float[embeddingDim];
            float maskSum = attentionMask.Sum();

            for (int d = 0; d < embeddingDim; d++)
            {
                float sum = 0f;
                for (int t = 0; t < seqLength; t++)
                {
                    if (attentionMask[t] == 1)
                    {
                        sum += tokenEmbeddings[0, t, d];
                    }
                }
                result[d] = sum / maskSum;
            }

            // L2 normalize
            float norm = (float)Math.Sqrt(result.Sum(x => x * x));
            for (int i = 0; i < result.Length; i++)
            {
                result[i] /= norm;
            }

            return result;
        }

        private void EnsureModelLoaded()
        {
            if (!_isLoaded)
            {
                throw new InvalidOperationException("Embedding model not loaded. Call LoadModelAsync first.");
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
}
