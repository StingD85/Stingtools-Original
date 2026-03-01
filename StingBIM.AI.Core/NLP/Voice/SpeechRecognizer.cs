// StingBIM.AI.NLP.Voice.SpeechRecognizer
// Real-time speech recognition using Whisper
// Master Proposal Reference: Part 1.1 User Interface Layer - Voice Input (Whisper)

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NLog;
using StingBIM.AI.Core.Models;

namespace StingBIM.AI.NLP.Voice
{
    /// <summary>
    /// Handles real-time speech recognition for voice commands.
    /// Uses NAudio for audio capture and Whisper for transcription.
    /// Target: Real-time transcription (Part 5.2)
    /// </summary>
    public class SpeechRecognizer : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly SpeechModel _speechModel;
        private WaveInEvent _waveIn;
        private readonly List<float> _audioBuffer;
        private readonly object _bufferLock = new object();

        private bool _isListening;
        private bool _disposed;
        private CancellationTokenSource _listeningCts;

        // Audio configuration (Whisper requires 16kHz mono)
        private const int SampleRate = 16000;
        private const int BitsPerSample = 16;
        private const int Channels = 1;

        // Voice activity detection
        private const float SilenceThreshold = 0.01f;
        private const int MinSpeechDurationMs = 500;
        private const int MaxSilenceDurationMs = 1500;
        private const int MaxRecordingDurationMs = 30000;

        private DateTime _speechStartTime;
        private DateTime _lastSpeechTime;
        private bool _isSpeaking;

        /// <summary>
        /// Event fired when speech is detected.
        /// </summary>
        public event EventHandler SpeechDetected;

        /// <summary>
        /// Event fired when transcription is complete.
        /// </summary>
        public event EventHandler<TranscriptionEventArgs> TranscriptionComplete;

        /// <summary>
        /// Event fired when an error occurs.
        /// </summary>
        public event EventHandler<SpeechErrorEventArgs> Error;

        /// <summary>
        /// Current listening state.
        /// </summary>
        public bool IsListening => _isListening;

        /// <summary>
        /// Language for recognition (e.g., "en", "fr", "ar").
        /// </summary>
        public string Language { get; set; } = "en";

        /// <summary>
        /// Whether to use wake word detection.
        /// </summary>
        public bool UseWakeWord { get; set; } = false;

        /// <summary>
        /// The wake word to listen for (e.g., "Hey Sting").
        /// </summary>
        public string WakeWord { get; set; } = "hey sting";

        public SpeechRecognizer(SpeechModel speechModel)
        {
            _speechModel = speechModel ?? throw new ArgumentNullException(nameof(speechModel));
            _audioBuffer = new List<float>();
        }

        /// <summary>
        /// Starts listening for speech input.
        /// </summary>
        public void StartListening()
        {
            if (_isListening)
            {
                Logger.Warn("Already listening");
                return;
            }

            try
            {
                _listeningCts = new CancellationTokenSource();
                _audioBuffer.Clear();
                _isSpeaking = false;

                // Initialize audio capture
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                    BufferMilliseconds = 100
                };

                _waveIn.DataAvailable += OnAudioDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;

                _waveIn.StartRecording();
                _isListening = true;

                Logger.Info("Started listening for speech input");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start listening");
                Error?.Invoke(this, new SpeechErrorEventArgs { Error = ex.Message });
            }
        }

        /// <summary>
        /// Stops listening for speech input.
        /// </summary>
        public void StopListening()
        {
            if (!_isListening)
                return;

            try
            {
                _listeningCts?.Cancel();
                _waveIn?.StopRecording();
                _isListening = false;

                Logger.Info("Stopped listening");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error stopping listener");
            }
        }

        /// <summary>
        /// Transcribes a single audio utterance (push-to-talk mode).
        /// </summary>
        public async Task<string> TranscribeOnceAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<string>();

            void OnTranscription(object sender, TranscriptionEventArgs e)
            {
                tcs.TrySetResult(e.Text);
            }

            void OnError(object sender, SpeechErrorEventArgs e)
            {
                tcs.TrySetException(new Exception(e.Error));
            }

            try
            {
                TranscriptionComplete += OnTranscription;
                Error += OnError;

                StartListening();

                using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                {
                    return await tcs.Task;
                }
            }
            finally
            {
                TranscriptionComplete -= OnTranscription;
                Error -= OnError;
                StopListening();
            }
        }

        /// <summary>
        /// Gets available audio input devices.
        /// </summary>
        public static IEnumerable<AudioDevice> GetAudioDevices()
        {
            var devices = new List<AudioDevice>();

            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                devices.Add(new AudioDevice
                {
                    DeviceIndex = i,
                    Name = caps.ProductName,
                    Channels = caps.Channels
                });
            }

            return devices;
        }

        /// <summary>
        /// Sets the audio input device.
        /// </summary>
        public void SetAudioDevice(int deviceIndex)
        {
            if (_waveIn != null)
            {
                _waveIn.DeviceNumber = deviceIndex;
            }
        }

        private void OnAudioDataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                // Convert bytes to float samples
                var samples = ConvertBytesToFloats(e.Buffer, e.BytesRecorded);

                // Calculate audio level
                var level = CalculateAudioLevel(samples);

                // Voice activity detection
                ProcessVoiceActivity(samples, level);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error processing audio data");
            }
        }

        private void ProcessVoiceActivity(float[] samples, float level)
        {
            var now = DateTime.Now;

            if (level > SilenceThreshold)
            {
                // Speech detected
                if (!_isSpeaking)
                {
                    _isSpeaking = true;
                    _speechStartTime = now;
                    SpeechDetected?.Invoke(this, EventArgs.Empty);
                    Logger.Debug("Speech detected");
                }

                _lastSpeechTime = now;

                // Add to buffer
                lock (_bufferLock)
                {
                    _audioBuffer.AddRange(samples);

                    // Check max duration
                    var durationMs = _audioBuffer.Count / (float)SampleRate * 1000;
                    if (durationMs > MaxRecordingDurationMs)
                    {
                        // Force transcription due to max duration
                        ProcessAccumulatedAudio();
                    }
                }
            }
            else if (_isSpeaking)
            {
                // Silence after speech
                var silenceDuration = (now - _lastSpeechTime).TotalMilliseconds;

                // Still add to buffer during short silence
                lock (_bufferLock)
                {
                    _audioBuffer.AddRange(samples);
                }

                if (silenceDuration > MaxSilenceDurationMs)
                {
                    // End of utterance detected
                    var speechDuration = (now - _speechStartTime).TotalMilliseconds;

                    if (speechDuration >= MinSpeechDurationMs)
                    {
                        ProcessAccumulatedAudio();
                    }
                    else
                    {
                        // Too short, discard
                        Logger.Debug("Speech too short, discarding");
                        lock (_bufferLock)
                        {
                            _audioBuffer.Clear();
                        }
                    }

                    _isSpeaking = false;
                }
            }
        }

        /// <summary>
        /// Processes accumulated audio buffer for transcription.
        /// Note: This is async void because it's a fire-and-forget event handler pattern.
        /// All exceptions are caught and logged internally.
        /// </summary>
        private async void ProcessAccumulatedAudio()
        {
            // Outer try-catch to handle any synchronous exceptions
            try
            {
                float[] audioData;

                lock (_bufferLock)
                {
                    if (_audioBuffer.Count == 0)
                        return;

                    audioData = _audioBuffer.ToArray();
                    _audioBuffer.Clear();
                }

                try
                {
                    Logger.Debug($"Transcribing {audioData.Length} samples ({audioData.Length / (float)SampleRate:F1}s)");

                    var result = await _speechModel.TranscribeAsync(
                        audioData,
                        Language,
                        _listeningCts?.Token ?? CancellationToken.None);

                    if (!string.IsNullOrWhiteSpace(result.Text))
                    {
                        var text = result.Text.Trim();

                        // Check for wake word if enabled
                        if (UseWakeWord)
                        {
                            if (text.StartsWith(WakeWord, StringComparison.OrdinalIgnoreCase))
                            {
                                text = text.Substring(WakeWord.Length).Trim();
                            }
                            else
                            {
                                Logger.Debug($"Wake word not detected in: {text}");
                                return;
                            }
                        }

                        Logger.Info($"Transcription: {text} (confidence: {result.Confidence:F2})");

                        TranscriptionComplete?.Invoke(this, new TranscriptionEventArgs
                        {
                            Text = text,
                            Confidence = result.Confidence,
                            ProcessingTimeMs = result.ProcessingTimeMs,
                            AudioDurationMs = result.AudioDurationMs
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.Debug("Transcription cancelled");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Transcription failed");
                    Error?.Invoke(this, new SpeechErrorEventArgs { Error = ex.Message });
                }
            }
            catch (Exception ex)
            {
                // Catch any exceptions that occur outside the async operation
                Logger.Error(ex, "ProcessAccumulatedAudio failed unexpectedly");
                try
                {
                    Error?.Invoke(this, new SpeechErrorEventArgs { Error = ex.Message });
                }
                catch
                {
                    // Prevent event handler exceptions from propagating
                }
            }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                Logger.Error(e.Exception, "Recording stopped due to error");
                Error?.Invoke(this, new SpeechErrorEventArgs { Error = e.Exception.Message });
            }

            _isListening = false;
        }

        private float[] ConvertBytesToFloats(byte[] buffer, int bytesRecorded)
        {
            var samples = new float[bytesRecorded / 2];

            for (int i = 0; i < samples.Length; i++)
            {
                short sample = BitConverter.ToInt16(buffer, i * 2);
                samples[i] = sample / 32768f;
            }

            return samples;
        }

        private float CalculateAudioLevel(float[] samples)
        {
            if (samples.Length == 0)
                return 0;

            float sum = 0;
            foreach (var sample in samples)
            {
                sum += Math.Abs(sample);
            }

            return sum / samples.Length;
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
                    StopListening();

                    if (_waveIn != null)
                    {
                        _waveIn.DataAvailable -= OnAudioDataAvailable;
                        _waveIn.RecordingStopped -= OnRecordingStopped;
                        _waveIn.Dispose();
                        _waveIn = null;
                    }

                    _listeningCts?.Dispose();
                }

                _disposed = true;
            }
        }
    }

    #region Supporting Classes

    public class TranscriptionEventArgs : EventArgs
    {
        public string Text { get; set; }
        public float Confidence { get; set; }
        public double ProcessingTimeMs { get; set; }
        public double AudioDurationMs { get; set; }
    }

    public class SpeechErrorEventArgs : EventArgs
    {
        public string Error { get; set; }
    }

    public class AudioDevice
    {
        public int DeviceIndex { get; set; }
        public string Name { get; set; }
        public int Channels { get; set; }
    }

    #endregion
}
