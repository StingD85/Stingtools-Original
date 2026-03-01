// StingBIM.AI.Core.Models.SpeechModel
// ONNX-based speech recognition wrapper for Whisper
// Master Proposal Reference: Part 1.2 Component Specifications - Whisper-tiny (75MB)

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NLog;
using System.Linq;
using System.Numerics;

namespace StingBIM.AI.Core.Models
{
    /// <summary>
    /// Wrapper for the Whisper-tiny ONNX speech recognition model.
    /// Provides voice command transcription capabilities.
    /// Model size: 75 MB (Part 1.2)
    /// Target: Real-time transcription (Part 5.2)
    /// </summary>
    public class SpeechModel : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private InferenceSession _encoderSession;
        private InferenceSession _decoderSession;
        private readonly object _sessionLock = new object();
        private bool _isLoaded;
        private bool _disposed;

        // Model configuration
        public string EncoderModelPath { get; private set; }
        public string DecoderModelPath { get; private set; }
        public int SampleRate { get; } = 16000; // Whisper requires 16kHz audio
        public int MaxDuration { get; set; } = 30; // Maximum 30 seconds per chunk

        // Audio processing constants
        private const int N_FFT = 400;
        private const int HOP_LENGTH = 160;
        private const int N_MELS = 80;

        /// <summary>
        /// Loads the Whisper encoder and decoder models.
        /// </summary>
        public async Task LoadModelAsync(
            string encoderPath,
            string decoderPath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(encoderPath)) throw new ArgumentException("Encoder path cannot be null or empty.", nameof(encoderPath));
            if (string.IsNullOrWhiteSpace(decoderPath)) throw new ArgumentException("Decoder path cannot be null or empty.", nameof(decoderPath));

            Logger.Info("Loading Whisper speech model...");

            await Task.Run(() =>
            {
                try
                {
                    var options = new SessionOptions();
                    options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

                    lock (_sessionLock)
                    {
                        _encoderSession = new InferenceSession(encoderPath, options);
                        _decoderSession = new InferenceSession(decoderPath, options);
                        EncoderModelPath = encoderPath;
                        DecoderModelPath = decoderPath;
                        _isLoaded = true;
                    }

                    Logger.Info("Whisper speech model loaded successfully");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load Whisper model");
                    throw;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Transcribes audio samples to text.
        /// </summary>
        /// <param name="audioSamples">Audio samples (16kHz mono float32)</param>
        /// <param name="language">Language code (e.g., "en", "fr")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Transcribed text</returns>
        public async Task<TranscriptionResult> TranscribeAsync(
            float[] audioSamples,
            string language = "en",
            CancellationToken cancellationToken = default)
        {
            if (audioSamples == null || audioSamples.Length == 0) throw new ArgumentException("Audio samples cannot be null or empty.", nameof(audioSamples));

            EnsureModelLoaded();

            return await Task.Run(() =>
            {
                var startTime = DateTime.Now;

                // Trim silence from audio before processing
                var trimmedAudio = TrimSilence(audioSamples);

                // Convert audio to mel spectrogram
                var melSpectrogram = ComputeMelSpectrogram(trimmedAudio);

                // Apply per-band mean-variance normalization
                NormalizeMelSpectrogram(melSpectrogram);

                // Run encoder
                var encoderOutput = RunEncoder(melSpectrogram);

                // Run decoder (greedy decoding)
                var (tokens, avgConfidence) = RunDecoder(encoderOutput, language, cancellationToken);

                // Decode tokens to text
                var text = DecodeTokens(tokens);

                var processingTime = DateTime.Now - startTime;

                return new TranscriptionResult
                {
                    Text = text,
                    Language = language,
                    Confidence = avgConfidence,
                    ProcessingTimeMs = processingTime.TotalMilliseconds,
                    AudioDurationMs = (audioSamples.Length / (double)SampleRate) * 1000
                };
            }, cancellationToken);
        }

        /// <summary>
        /// Computes mel spectrogram from audio samples.
        /// </summary>
        private float[,] ComputeMelSpectrogram(float[] audioSamples)
        {
            // Step 1: Apply pre-emphasis filter (coefficient 0.97)
            var emphasized = new float[audioSamples.Length];
            emphasized[0] = audioSamples[0];
            for (int i = 1; i < audioSamples.Length; i++)
            {
                emphasized[i] = audioSamples[i] - 0.97f * audioSamples[i - 1];
            }

            // Step 2: Compute Hann window of size N_FFT
            var hannWindow = new float[N_FFT];
            for (int i = 0; i < N_FFT; i++)
            {
                hannWindow[i] = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / (N_FFT - 1)));
            }

            // Step 3: Compute STFT using sliding window with HOP_LENGTH
            int numFrames = Math.Max(1, (emphasized.Length - N_FFT) / HOP_LENGTH + 1);
            int fftBins = N_FFT / 2 + 1; // 201 bins

            Logger.Debug($"Computing mel spectrogram: {N_MELS}x{numFrames} (FFT bins: {fftBins})");

            // Compute power spectrum for each frame: |FFT|^2
            var powerSpectrum = new float[numFrames, fftBins];
            int fftSize = NextPowerOfTwo(N_FFT);

            for (int frame = 0; frame < numFrames; frame++)
            {
                int start = frame * HOP_LENGTH;

                // Apply Hann window and prepare complex input for FFT
                var fftInput = new Complex[fftSize];
                for (int i = 0; i < N_FFT && (start + i) < emphasized.Length; i++)
                {
                    fftInput[i] = new Complex(emphasized[start + i] * hannWindow[i], 0.0);
                }
                // Remaining samples are zero-padded (default Complex is (0,0))

                // Radix-2 DIT FFT
                FFT(fftInput, false);

                // Compute power spectrum: |FFT|^2
                for (int k = 0; k < fftBins; k++)
                {
                    double re = fftInput[k].Real;
                    double im = fftInput[k].Imaginary;
                    powerSpectrum[frame, k] = (float)(re * re + im * im);
                }
            }

            // Step 4: Build mel filterbank (N_MELS=80 filters, freq range 0-8000Hz at 16kHz sample rate)
            var melFilterbank = CreateMelFilterbank(N_MELS, fftBins, SampleRate, 0.0, 8000.0);

            // Step 5: Apply mel filterbank and convert to log scale
            var melSpec = new float[N_MELS, numFrames];
            for (int frame = 0; frame < numFrames; frame++)
            {
                for (int mel = 0; mel < N_MELS; mel++)
                {
                    float sum = 0.0f;
                    for (int k = 0; k < fftBins; k++)
                    {
                        sum += melFilterbank[mel, k] * powerSpectrum[frame, k];
                    }
                    // Log scale: log(max(melSpec, 1e-10))
                    melSpec[mel, frame] = (float)Math.Log(Math.Max(sum, 1e-10));
                }
            }

            Logger.Debug($"Computed mel spectrogram: {N_MELS}x{numFrames}");
            return melSpec;
        }

        /// <summary>
        /// Returns the next power of two >= n.
        /// </summary>
        private static int NextPowerOfTwo(int n)
        {
            int power = 1;
            while (power < n) power <<= 1;
            return power;
        }

        /// <summary>
        /// In-place radix-2 decimation-in-time FFT.
        /// </summary>
        private static void FFT(Complex[] data, bool inverse)
        {
            int n = data.Length;
            if (n <= 1) return;

            // Bit-reversal permutation
            int bits = (int)Math.Log2(n);
            for (int i = 0; i < n; i++)
            {
                int j = BitReverse(i, bits);
                if (j > i)
                {
                    var temp = data[i];
                    data[i] = data[j];
                    data[j] = temp;
                }
            }

            // Cooley-Tukey iterative FFT
            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = 2.0 * Math.PI / len * (inverse ? -1.0 : 1.0);
                var wlen = new Complex(Math.Cos(angle), Math.Sin(angle));

                for (int i = 0; i < n; i += len)
                {
                    var w = Complex.One;
                    for (int j = 0; j < len / 2; j++)
                    {
                        var u = data[i + j];
                        var v = data[i + j + len / 2] * w;
                        data[i + j] = u + v;
                        data[i + j + len / 2] = u - v;
                        w *= wlen;
                    }
                }
            }

            if (inverse)
            {
                for (int i = 0; i < n; i++)
                {
                    data[i] /= n;
                }
            }
        }

        /// <summary>
        /// Reverses the bits of an integer for FFT bit-reversal permutation.
        /// </summary>
        private static int BitReverse(int x, int bits)
        {
            int result = 0;
            for (int i = 0; i < bits; i++)
            {
                result = (result << 1) | (x & 1);
                x >>= 1;
            }
            return result;
        }

        /// <summary>
        /// Creates a mel filterbank with triangular filters spaced on the mel scale.
        /// </summary>
        private static float[,] CreateMelFilterbank(int nMels, int fftBins, int sampleRate, double fMin, double fMax)
        {
            var filterbank = new float[nMels, fftBins];

            // Convert frequency bounds to mel scale
            double melMin = HzToMel(fMin);
            double melMax = HzToMel(fMax);

            // Create nMels + 2 evenly spaced points on mel scale (extra 2 for boundary filters)
            var melPoints = new double[nMels + 2];
            for (int i = 0; i < nMels + 2; i++)
            {
                melPoints[i] = melMin + (melMax - melMin) * i / (nMels + 1);
            }

            // Convert mel points back to Hz
            var hzPoints = new double[nMels + 2];
            for (int i = 0; i < nMels + 2; i++)
            {
                hzPoints[i] = MelToHz(melPoints[i]);
            }

            // Convert Hz to FFT bin indices
            var binPoints = new int[nMels + 2];
            for (int i = 0; i < nMels + 2; i++)
            {
                binPoints[i] = (int)Math.Floor((N_FFT + 1) * hzPoints[i] / sampleRate);
            }

            // Create triangular filters
            for (int m = 0; m < nMels; m++)
            {
                int fLeft = binPoints[m];
                int fCenter = binPoints[m + 1];
                int fRight = binPoints[m + 2];

                for (int k = 0; k < fftBins; k++)
                {
                    if (k >= fLeft && k <= fCenter && fCenter > fLeft)
                    {
                        filterbank[m, k] = (float)(k - fLeft) / (fCenter - fLeft);
                    }
                    else if (k > fCenter && k <= fRight && fRight > fCenter)
                    {
                        filterbank[m, k] = (float)(fRight - k) / (fRight - fCenter);
                    }
                    // else 0 (default)
                }
            }

            return filterbank;
        }

        /// <summary>
        /// Converts frequency in Hz to mel scale: mel = 2595 * log10(1 + hz/700)
        /// </summary>
        private static double HzToMel(double hz)
        {
            return 2595.0 * Math.Log10(1.0 + hz / 700.0);
        }

        /// <summary>
        /// Converts mel scale to frequency in Hz: hz = 700 * (10^(mel/2595) - 1)
        /// </summary>
        private static double MelToHz(double mel)
        {
            return 700.0 * (Math.Pow(10.0, mel / 2595.0) - 1.0);
        }

        /// <summary>
        /// Applies per-band mean-variance normalization to the mel spectrogram.
        /// Each mel band is normalized to zero mean and unit variance for
        /// improved model robustness across different recording conditions.
        /// </summary>
        private void NormalizeMelSpectrogram(float[,] melSpec)
        {
            int nMels = melSpec.GetLength(0);
            int nFrames = melSpec.GetLength(1);

            if (nFrames < 2) return;

            for (int mel = 0; mel < nMels; mel++)
            {
                // Compute mean for this mel band
                double sum = 0;
                for (int f = 0; f < nFrames; f++)
                {
                    sum += melSpec[mel, f];
                }
                double mean = sum / nFrames;

                // Compute variance
                double varianceSum = 0;
                for (int f = 0; f < nFrames; f++)
                {
                    double diff = melSpec[mel, f] - mean;
                    varianceSum += diff * diff;
                }
                double stdDev = Math.Sqrt(varianceSum / nFrames);

                // Normalize: (x - mean) / (stdDev + epsilon)
                double invStd = stdDev > 1e-6 ? 1.0 / stdDev : 1.0;
                for (int f = 0; f < nFrames; f++)
                {
                    melSpec[mel, f] = (float)((melSpec[mel, f] - mean) * invStd);
                }
            }

            Logger.Debug($"Applied mean-variance normalization across {nMels} mel bands");
        }

        /// <summary>
        /// Trims leading and trailing silence from audio samples using
        /// energy-based Voice Activity Detection (VAD).
        /// </summary>
        /// <param name="audioSamples">Raw audio samples</param>
        /// <param name="energyThresholdDb">Energy threshold in dB below peak (default -40 dB)</param>
        /// <param name="frameMs">Analysis frame length in milliseconds</param>
        /// <returns>Trimmed audio with minimal leading/trailing silence</returns>
        private float[] TrimSilence(float[] audioSamples, float energyThresholdDb = -40f, int frameMs = 20)
        {
            if (audioSamples.Length < SampleRate * frameMs / 1000)
                return audioSamples;

            int frameSamples = SampleRate * frameMs / 1000;
            int numFrames = audioSamples.Length / frameSamples;

            if (numFrames < 3) return audioSamples;

            // Compute per-frame energy in dB
            var frameEnergies = new float[numFrames];
            float maxEnergy = float.MinValue;

            for (int i = 0; i < numFrames; i++)
            {
                float energy = 0;
                int offset = i * frameSamples;
                for (int j = 0; j < frameSamples && (offset + j) < audioSamples.Length; j++)
                {
                    energy += audioSamples[offset + j] * audioSamples[offset + j];
                }
                energy /= frameSamples;
                frameEnergies[i] = energy;
                if (energy > maxEnergy) maxEnergy = energy;
            }

            // Convert to dB relative to peak
            float peakDb = maxEnergy > 1e-10f ? 10f * (float)Math.Log10(maxEnergy) : -100f;
            float threshold = peakDb + energyThresholdDb;

            // Find first and last active frames
            int firstActive = 0;
            int lastActive = numFrames - 1;

            for (int i = 0; i < numFrames; i++)
            {
                float db = frameEnergies[i] > 1e-10f ? 10f * (float)Math.Log10(frameEnergies[i]) : -100f;
                if (db >= threshold)
                {
                    firstActive = i;
                    break;
                }
            }

            for (int i = numFrames - 1; i >= 0; i--)
            {
                float db = frameEnergies[i] > 1e-10f ? 10f * (float)Math.Log10(frameEnergies[i]) : -100f;
                if (db >= threshold)
                {
                    lastActive = i;
                    break;
                }
            }

            // Add small padding (1 frame) on each side to avoid clipping speech onset
            firstActive = Math.Max(0, firstActive - 1);
            lastActive = Math.Min(numFrames - 1, lastActive + 1);

            int startSample = firstActive * frameSamples;
            int endSample = Math.Min(audioSamples.Length, (lastActive + 1) * frameSamples);
            int trimmedLength = endSample - startSample;

            if (trimmedLength >= audioSamples.Length)
                return audioSamples;

            var trimmed = new float[trimmedLength];
            Array.Copy(audioSamples, startSample, trimmed, 0, trimmedLength);

            Logger.Debug($"Trimmed silence: {audioSamples.Length} -> {trimmedLength} samples ({firstActive}-{lastActive} of {numFrames} frames active)");

            return trimmed;
        }

        /// <summary>
        /// Performs Voice Activity Detection (VAD) on audio samples.
        /// Returns a per-frame boolean array indicating speech presence.
        /// Uses short-time energy and zero-crossing rate heuristics.
        /// </summary>
        /// <param name="audioSamples">Audio samples (16kHz mono float32)</param>
        /// <param name="frameMs">Analysis frame length in milliseconds</param>
        /// <returns>Per-frame VAD decisions (true = speech detected)</returns>
        public bool[] DetectVoiceActivity(float[] audioSamples, int frameMs = 20)
        {
            int frameSamples = SampleRate * frameMs / 1000;
            int numFrames = audioSamples.Length / frameSamples;

            if (numFrames == 0) return Array.Empty<bool>();

            var energies = new float[numFrames];
            var zeroCrossings = new int[numFrames];
            float maxEnergy = float.MinValue;

            for (int i = 0; i < numFrames; i++)
            {
                int offset = i * frameSamples;
                float energy = 0;
                int zcr = 0;

                for (int j = 0; j < frameSamples && (offset + j) < audioSamples.Length; j++)
                {
                    energy += audioSamples[offset + j] * audioSamples[offset + j];

                    if (j > 0)
                    {
                        bool signCurrent = audioSamples[offset + j] >= 0;
                        bool signPrevious = audioSamples[offset + j - 1] >= 0;
                        if (signCurrent != signPrevious) zcr++;
                    }
                }

                energies[i] = energy / frameSamples;
                zeroCrossings[i] = zcr;
                if (energies[i] > maxEnergy) maxEnergy = energies[i];
            }

            // Adaptive thresholds based on first 5 frames (assumed non-speech)
            int noiseFrames = Math.Min(5, numFrames);
            float noiseEnergy = 0;
            int noiseZcr = 0;
            for (int i = 0; i < noiseFrames; i++)
            {
                noiseEnergy += energies[i];
                noiseZcr += zeroCrossings[i];
            }
            noiseEnergy /= noiseFrames;
            noiseZcr /= noiseFrames;

            // Energy threshold: 10x noise floor or -40 dB below peak
            float energyThreshold = Math.Max(noiseEnergy * 10f, maxEnergy * 0.0001f);

            // ZCR threshold: speech typically has moderate ZCR (not too high = noise, not too low = silence)
            int zcrLow = Math.Max(5, noiseZcr / 2);
            int zcrHigh = Math.Max(frameSamples / 4, noiseZcr * 3);

            var vad = new bool[numFrames];
            for (int i = 0; i < numFrames; i++)
            {
                bool energyPass = energies[i] > energyThreshold;
                bool zcrPass = zeroCrossings[i] > zcrLow && zeroCrossings[i] < zcrHigh;

                // Speech if energy is high, or moderate energy with speech-like ZCR
                vad[i] = energyPass || (energies[i] > energyThreshold * 0.3f && zcrPass);
            }

            // Apply hangover: extend speech regions by 2 frames to avoid fragmentation
            const int Hangover = 2;
            var smoothed = new bool[numFrames];
            Array.Copy(vad, smoothed, numFrames);

            for (int i = 0; i < numFrames; i++)
            {
                if (vad[i])
                {
                    for (int h = 1; h <= Hangover; h++)
                    {
                        if (i + h < numFrames) smoothed[i + h] = true;
                        if (i - h >= 0) smoothed[i - h] = true;
                    }
                }
            }

            int activeFrames = smoothed.Count(v => v);
            Logger.Debug($"VAD: {activeFrames}/{numFrames} frames active ({100.0 * activeFrames / numFrames:F1}%)");

            return smoothed;
        }

        /// <summary>
        /// Runs the Whisper encoder on mel spectrogram.
        /// </summary>
        private float[] RunEncoder(float[,] melSpectrogram)
        {
            lock (_sessionLock)
            {
                int numMels = melSpectrogram.GetLength(0);
                int numFrames = melSpectrogram.GetLength(1);

                // Flatten and create tensor
                var flatMel = new float[1 * numMels * numFrames];
                Buffer.BlockCopy(melSpectrogram, 0, flatMel, 0, flatMel.Length * sizeof(float));

                var inputTensor = new DenseTensor<float>(flatMel, new[] { 1, numMels, numFrames });
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("mel", inputTensor)
                };

                using (var results = _encoderSession.Run(inputs))
                {
                    return results[0].AsTensor<float>().ToArray();
                }
            }
        }

        /// <summary>
        /// Runs the Whisper decoder with greedy decoding.
        /// </summary>
        private (int[] tokens, float averageConfidence) RunDecoder(float[] encoderOutput, string language, CancellationToken cancellationToken)
        {
            const int MAX_DECODER_TOKENS = 448;
            const int EOT_TOKEN = 50257;

            var tokens = new List<int>();
            var confidences = new List<float>();

            // Start with SOT (start of transcript) token
            tokens.Add(50258); // SOT token
            tokens.Add(GetLanguageToken(language));
            tokens.Add(GetTranscribeToken());

            lock (_sessionLock)
            {
                // Determine encoder output dimensions for tensor creation
                // Whisper-tiny encoder output: [1, numFrames, encoderDim]
                int encoderDim = 384;
                int encoderLength = encoderOutput.Length / encoderDim;

                var encoderTensor = new DenseTensor<float>(
                    encoderOutput, new[] { 1, encoderLength, encoderDim });

                // Autoregressive decoding loop
                for (int step = 0; step < MAX_DECODER_TOKENS; step++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Create decoder input tensor from current token sequence
                    var decoderInputIds = tokens.Select(t => (long)t).ToArray();
                    var decoderInputTensor = new DenseTensor<long>(
                        decoderInputIds, new[] { 1, decoderInputIds.Length });

                    // Prepare inputs for the decoder session
                    var decoderInputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor("input_ids", decoderInputTensor),
                        NamedOnnxValue.CreateFromTensor("encoder_hidden_states", encoderTensor)
                    };

                    try
                    {
                        // Run decoder session
                        using (var results = _decoderSession.Run(decoderInputs))
                        {
                            // Extract logits from output: shape [1, seqLen, vocabSize]
                            var logitsTensor = results[0].AsTensor<float>();
                            var dims = logitsTensor.Dimensions.ToArray();

                            int seqLen = dims.Length >= 2 ? dims[1] : 1;
                            int vocabSize = dims.Length >= 3 ? dims[2] : dims[dims.Length - 1];

                            // Extract logits for the last token position
                            int lastTokenOffset = (seqLen - 1) * vocabSize;
                            var lastLogits = new float[vocabSize];
                            for (int v = 0; v < vocabSize; v++)
                            {
                                int idx = lastTokenOffset + v;
                                if (idx < logitsTensor.Length)
                                {
                                    lastLogits[v] = logitsTensor.GetValue(idx);
                                }
                            }

                            // Greedy decoding with confidence
                            var (nextToken, confidence) = GreedySampleWithConfidence(lastLogits);

                            // If token is EOT, stop decoding
                            if (nextToken == EOT_TOKEN)
                            {
                                Logger.Debug($"Decoder reached EOT after {step + 1} steps");
                                break;
                            }

                            // Append predicted token to sequence
                            tokens.Add(nextToken);

                            // Only track confidence for non-special tokens (actual text)
                            if (nextToken < 50257)
                            {
                                confidences.Add(confidence);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Decoder step {step} failed: {ex.Message}");
                        break;
                    }
                }
            }

            Logger.Debug($"Decoder produced {tokens.Count} tokens total");
            float avgConfidence = confidences.Count > 0 ? confidences.Average() : 0f;
            return (tokens.ToArray(), avgConfidence);
        }

        /// <summary>
        /// Performs greedy sampling on logits and returns the best token with its confidence.
        /// </summary>
        private (int nextToken, float confidence) GreedySampleWithConfidence(float[] logits)
        {
            if (logits == null || logits.Length == 0)
                return (0, 0f);

            int bestToken = 0;
            float bestLogit = logits[0];

            for (int i = 1; i < logits.Length; i++)
            {
                if (logits[i] > bestLogit)
                {
                    bestLogit = logits[i];
                    bestToken = i;
                }
            }

            // Compute softmax confidence for the best token
            float maxVal = bestLogit;
            float sumExp = 0f;
            for (int i = 0; i < logits.Length; i++)
            {
                sumExp += (float)Math.Exp(logits[i] - maxVal);
            }
            float confidence = 1.0f / sumExp;

            return (bestToken, confidence);
        }

        /// <summary>
        /// Decodes token IDs to text string.
        /// </summary>
        private string DecodeTokens(int[] tokens)
        {
            // Build vocabulary mapping lazily
            if (_vocabulary == null)
            {
                _vocabulary = BuildVocabulary();
            }

            var parts = new List<string>();

            foreach (int token in tokens)
            {
                // Filter out special tokens (>= 50257 are special in Whisper)
                if (token >= SPECIAL_TOKEN_START)
                {
                    continue;
                }

                if (_vocabulary.TryGetValue(token, out string text))
                {
                    parts.Add(text);
                }
                else if (token >= 0 && token < 256)
                {
                    // Fallback: first 256 tokens map to byte values
                    parts.Add(((char)token).ToString());
                }
            }

            // Join parts and clean up whitespace
            // In Whisper BPE, tokens starting with a special character (Unicode \u0120 = 'G' with space prefix)
            // represent word boundaries. We use the convention that tokens prefixed with space in the vocab
            // already carry their spacing.
            string result = string.Join("", parts);

            // Clean up common BPE artifacts
            result = result.Replace("\u0120", " "); // GPT-2 space marker
            result = result.Replace("\u010a", "\n"); // GPT-2 newline marker

            // Normalize whitespace: collapse multiple spaces, trim
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

            return result;
        }

        /// <summary>
        /// Greedy sampling with confidence score from logits.
        /// Returns the token ID with the highest logit and the softmax confidence.
        /// </summary>
        private (int token, float confidence) GreedySampleWithConfidence(float[] logits)
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

            // Compute softmax confidence for the selected token
            // Use log-sum-exp trick for numerical stability
            float logSumExp = 0f;
            for (int i = 0; i < logits.Length; i++)
            {
                logSumExp += (float)Math.Exp(logits[i] - maxVal);
            }
            float confidence = 1.0f / logSumExp;

            return (maxIdx, confidence);
        }

        /// <summary>
        /// Special token boundary in Whisper vocabulary.
        /// Tokens with ID >= 50257 are special tokens (EOT, language, task, etc.).
        /// </summary>
        private const int SPECIAL_TOKEN_START = 50257;

        /// <summary>
        /// Cached vocabulary mapping from token IDs to string representations.
        /// </summary>
        private Dictionary<int, string> _vocabulary;

        /// <summary>
        /// Builds a basic vocabulary mapping for Whisper token decoding.
        /// Tokens 0-255 map to byte values, followed by common BPE merges
        /// including building/architecture domain terms.
        /// </summary>
        private static Dictionary<int, string> BuildVocabulary()
        {
            var vocab = new Dictionary<int, string>(4096);

            // First 256 tokens: byte-level values (printable ASCII and extended)
            for (int i = 0; i < 256; i++)
            {
                vocab[i] = ((char)i).ToString();
            }

            // Common BPE token merges (256+). In Whisper's GPT-2 based tokenizer,
            // tokens above 255 represent merged subword units.
            // Space-prefixed tokens use a leading space to denote word boundaries.
            int idx = 256;

            // High-frequency English subword tokens
            var commonTokens = new[]
            {
                " the", " a", " an", " is", " are", " was", " were", " be", " been", " being",
                " have", " has", " had", " do", " does", " did", " will", " would", " could",
                " should", " may", " might", " shall", " can", " need", " must",
                " not", " no", " yes", " and", " or", " but", " if", " then", " else",
                " for", " of", " in", " on", " at", " to", " from", " by", " with", " as",
                " that", " this", " these", " those", " it", " its", " they", " them", " their",
                " we", " our", " you", " your", " he", " she", " his", " her",
                " what", " which", " who", " where", " when", " how", " why",
                " all", " each", " every", " both", " few", " more", " most", " some", " any",
                "ing", "tion", "ed", "er", "ly", "al", "ment", "ness", "ous", "ive",
                "able", "ible", "ful", "less", "ity", "ence", "ance", "ure", "ise", "ize",
                " I", " me", " my", " mine",
                // Numbers and punctuation
                " one", " two", " three", " four", " five", " six", " seven", " eight", " nine", " ten",
                " zero", " hundred", " thousand", " million",
                " first", " second", " third",
                // Common words
                " get", " set", " put", " make", " take", " go", " come", " see", " look",
                " give", " find", " think", " know", " want", " tell", " say", " use",
                " new", " old", " big", " small", " long", " short", " high", " low",
                " good", " bad", " great", " right", " left", " up", " down",
                " time", " year", " day", " way", " part", " place", " case", " point",
                " work", " system", " program", " number", " world", " area",
                // Building/Architecture domain terms
                " wall", " floor", " ceiling", " roof", " door", " window", " room",
                " beam", " column", " slab", " foundation", " structure", " structural",
                " building", " design", " plan", " section", " elevation", " detail",
                " concrete", " steel", " timber", " brick", " glass", " stone",
                " load", " stress", " force", " moment", " shear", " tension",
                " HVAC", " duct", " pipe", " valve", " pump", " fan",
                " electrical", " mechanical", " plumbing", " fire",
                " insulation", " thermal", " acoustic", " ventilation",
                " schedule", " parameter", " family", " type", " instance",
                " level", " grid", " axis", " dimension", " annotation",
                " Revit", " BIM", " model", " project", " sheet", " view",
                " height", " width", " length", " depth", " thickness", " area",
                " volume", " weight", " density", " capacity", " rating",
                " code", " standard", " compliance", " regulation", " requirement",
                " material", " finish", " color", " texture", " pattern",
                " install", " construct", " maintain", " inspect", " replace",
                " measure", " calculate", " analyze", " evaluate", " verify",
                " architect", " engineer", " contractor", " client", " consultant",
                " space", " zone", " corridor", " stair", " elevator", " ramp",
                " water", " air", " gas", " power", " energy", " light",
                " supply", " return", " exhaust", " intake", " outlet",
                " pressure", " temperature", " humidity", " flow", " velocity",
                " size", " spec", " note", " tag", " mark", " label",
                " north", " south", " east", " west",
                " open", " close", " add", " remove", " move", " copy", " edit",
                " create", " delete", " select", " show", " hide", " place",
            };

            foreach (var token in commonTokens)
            {
                vocab[idx++] = token;
            }

            return vocab;
        }

        private int GetLanguageToken(string language)
        {
            // Whisper language tokens
            var languageTokens = new Dictionary<string, int>
            {
                { "en", 50259 },
                { "fr", 50265 },
                { "ar", 50272 },
                { "sw", 50318 }, // Swahili
            };
            return languageTokens.GetValueOrDefault(language, 50259);
        }

        private int GetTranscribeToken() => 50359;

        private void EnsureModelLoaded()
        {
            if (!_isLoaded)
            {
                throw new InvalidOperationException("Speech model not loaded. Call LoadModelAsync first.");
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
                        _encoderSession?.Dispose();
                        _decoderSession?.Dispose();
                        _encoderSession = null;
                        _decoderSession = null;
                    }
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Result of speech transcription.
    /// </summary>
    public class TranscriptionResult
    {
        public string Text { get; set; }
        public string Language { get; set; }
        public float Confidence { get; set; }
        public double ProcessingTimeMs { get; set; }
        public double AudioDurationMs { get; set; }
        public double RealTimeFactor => ProcessingTimeMs / AudioDurationMs;
    }
}
