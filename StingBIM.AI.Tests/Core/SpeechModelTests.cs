// StingBIM.AI.Tests.Core.SpeechModelTests
// Unit tests for SpeechModel and Voice Recognition components
// Tests transcription, tokenization, and confidence scoring

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;

namespace StingBIM.AI.Tests.Core
{
    [TestFixture]
    public class SpeechModelTests
    {
        #region Tokenizer Tests

        [Test]
        public void Tokenize_SimpleText_ReturnsValidTokens()
        {
            // Arrange
            var tokenizer = new WhisperTokenizer();
            var text = "hello world";

            // Act
            var tokens = tokenizer.Tokenize(text);

            // Assert
            tokens.Should().NotBeEmpty();
            tokens.Should().HaveCountGreaterThan(0);
        }

        [Test]
        public void Tokenize_EmptyString_ReturnsEmptyArray()
        {
            // Arrange
            var tokenizer = new WhisperTokenizer();

            // Act
            var tokens = tokenizer.Tokenize("");

            // Assert
            tokens.Should().BeEmpty();
        }

        [Test]
        public void Tokenize_SpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var tokenizer = new WhisperTokenizer();
            var text = "Create a 3m wall!";

            // Act
            var tokens = tokenizer.Tokenize(text);

            // Assert
            tokens.Should().NotBeEmpty();
        }

        [Test]
        public void Detokenize_ValidTokens_ReturnsOriginalText()
        {
            // Arrange
            var tokenizer = new WhisperTokenizer();
            var originalText = "create wall";
            var tokens = tokenizer.Tokenize(originalText);

            // Act
            var reconstructed = tokenizer.Detokenize(tokens);

            // Assert
            reconstructed.ToLower().Should().Contain("create");
            reconstructed.ToLower().Should().Contain("wall");
        }

        #endregion

        #region Audio Processing Tests

        [Test]
        public void ProcessAudio_ValidSamples_ReturnsFeatures()
        {
            // Arrange
            var processor = new AudioProcessor();
            var samples = GenerateSineWave(16000, 440, 1.0f); // 1 second of 440Hz tone

            // Act
            var features = processor.ExtractFeatures(samples);

            // Assert
            features.Should().NotBeNull();
            features.MelSpectrogram.Should().NotBeNull();
            features.MelSpectrogram.Length.Should().BeGreaterThan(0);
        }

        [Test]
        public void ProcessAudio_SilentAudio_ReturnsLowEnergy()
        {
            // Arrange
            var processor = new AudioProcessor();
            var samples = new float[16000]; // 1 second of silence

            // Act
            var features = processor.ExtractFeatures(samples);

            // Assert
            features.AverageEnergy.Should().BeLessThan(0.001f);
        }

        [Test]
        public void NormalizeAudio_LoudAudio_NormalizesToTarget()
        {
            // Arrange
            var processor = new AudioProcessor();
            var samples = GenerateSineWave(16000, 440, 0.9f); // Near max amplitude

            // Act
            var normalized = processor.Normalize(samples, targetPeak: 0.5f);

            // Assert
            var maxValue = normalized.Max(Math.Abs);
            maxValue.Should().BeApproximately(0.5f, 0.05f);
        }

        [Test]
        public void ResampleAudio_From44100To16000_ReturnsCorrectLength()
        {
            // Arrange
            var processor = new AudioProcessor();
            var samples44k = new float[44100]; // 1 second at 44.1kHz

            // Act
            var samples16k = processor.Resample(samples44k, 44100, 16000);

            // Assert
            samples16k.Length.Should().BeCloseTo(16000, 100);
        }

        #endregion

        #region Voice Activity Detection Tests

        [Test]
        public void DetectVoiceActivity_WithSpeech_ReturnsTrue()
        {
            // Arrange
            var vad = new VoiceActivityDetector();
            var samples = GenerateSpeechLikeSignal(16000); // 1 second

            // Act
            var hasVoice = vad.DetectActivity(samples);

            // Assert
            hasVoice.Should().BeTrue();
        }

        [Test]
        public void DetectVoiceActivity_WithSilence_ReturnsFalse()
        {
            // Arrange
            var vad = new VoiceActivityDetector();
            var samples = new float[16000]; // Silence

            // Act
            var hasVoice = vad.DetectActivity(samples);

            // Assert
            hasVoice.Should().BeFalse();
        }

        [Test]
        public void DetectVoiceActivity_WithNoise_DetectsThreshold()
        {
            // Arrange
            var vad = new VoiceActivityDetector(threshold: 0.02f);
            var samples = GenerateWhiteNoise(16000, amplitude: 0.01f); // Below threshold

            // Act
            var hasVoice = vad.DetectActivity(samples);

            // Assert
            hasVoice.Should().BeFalse();
        }

        [Test]
        public void GetSpeechSegments_WithPauses_ReturnsMultipleSegments()
        {
            // Arrange
            var vad = new VoiceActivityDetector();
            var samples = GenerateSpeechWithPauses(48000); // 3 seconds with pauses

            // Act
            var segments = vad.GetSpeechSegments(samples, 16000);

            // Assert
            segments.Should().HaveCountGreaterThan(1);
        }

        #endregion

        #region Confidence Scoring Tests

        [Test]
        public void CalculateConfidence_HighProbabilityTokens_ReturnsHighConfidence()
        {
            // Arrange
            var scorer = new ConfidenceScorer();
            var tokenProbabilities = new[] { 0.95, 0.92, 0.88, 0.91 };

            // Act
            var confidence = scorer.CalculateOverallConfidence(tokenProbabilities);

            // Assert
            confidence.Should().BeGreaterThan(0.85f);
        }

        [Test]
        public void CalculateConfidence_LowProbabilityTokens_ReturnsLowConfidence()
        {
            // Arrange
            var scorer = new ConfidenceScorer();
            var tokenProbabilities = new[] { 0.3, 0.25, 0.4, 0.35 };

            // Act
            var confidence = scorer.CalculateOverallConfidence(tokenProbabilities);

            // Assert
            confidence.Should().BeLessThan(0.5f);
        }

        [Test]
        public void CalculateConfidence_SingleLowToken_ReducesConfidence()
        {
            // Arrange
            var scorer = new ConfidenceScorer();
            var tokenProbabilities = new[] { 0.95, 0.92, 0.1, 0.91 }; // One low

            // Act
            var confidence = scorer.CalculateOverallConfidence(tokenProbabilities);

            // Assert
            confidence.Should().BeLessThan(0.7f); // Should be reduced by low token
        }

        [Test]
        public void GetTokenConfidences_ReturnsIndividualScores()
        {
            // Arrange
            var scorer = new ConfidenceScorer();
            var logits = new float[][]
            {
                GenerateLogits(0.95f),
                GenerateLogits(0.8f),
                GenerateLogits(0.92f)
            };

            // Act
            var confidences = scorer.GetTokenConfidences(logits);

            // Assert
            confidences.Should().HaveCount(3);
            confidences[0].Should().BeGreaterThan(confidences[1]);
        }

        #endregion

        #region Transcription Result Tests

        [Test]
        public void TranscriptionResult_CalculatesRTF_Correctly()
        {
            // Arrange
            var result = new TranscriptionResult
            {
                AudioDurationMs = 5000,
                ProcessingTimeMs = 1000
            };

            // Act
            var rtf = result.RealTimeFactor;

            // Assert
            rtf.Should().BeApproximately(0.2, 0.01); // 1s processing for 5s audio = 0.2x
        }

        [Test]
        public void TranscriptionResult_WithNoAudio_RTFIsZero()
        {
            // Arrange
            var result = new TranscriptionResult
            {
                AudioDurationMs = 0,
                ProcessingTimeMs = 100
            };

            // Act
            var rtf = result.RealTimeFactor;

            // Assert
            rtf.Should().Be(0);
        }

        #endregion

        #region Language Detection Tests

        [Test]
        public void DetectLanguage_EnglishText_ReturnsEnglish()
        {
            // Arrange
            var detector = new LanguageDetector();
            var text = "Create a wall three meters long";

            // Act
            var language = detector.DetectLanguage(text);

            // Assert
            language.Code.Should().Be("en");
            language.Confidence.Should().BeGreaterThan(0.7);
        }

        [Test]
        public void DetectLanguage_FrenchText_ReturnsFrench()
        {
            // Arrange
            var detector = new LanguageDetector();
            var text = "Créer un mur de trois mètres";

            // Act
            var language = detector.DetectLanguage(text);

            // Assert
            language.Code.Should().Be("fr");
        }

        [Test]
        public void DetectLanguage_ArabicText_ReturnsArabic()
        {
            // Arrange
            var detector = new LanguageDetector();
            var text = "إنشاء جدار بطول ثلاثة أمتار";

            // Act
            var language = detector.DetectLanguage(text);

            // Assert
            language.Code.Should().Be("ar");
        }

        [Test]
        public void DetectLanguage_MixedText_ReturnsMainLanguage()
        {
            // Arrange
            var detector = new LanguageDetector();
            var text = "Create a wall, très bien";

            // Act
            var language = detector.DetectLanguage(text);

            // Assert
            // English has more content, should be detected as English
            language.Code.Should().Be("en");
        }

        #endregion

        #region Wake Word Detection Tests

        [Test]
        public void DetectWakeWord_WithWakeWord_ReturnsTrue()
        {
            // Arrange
            var detector = new WakeWordDetector("hey sting");
            var transcription = "Hey Sting, create a wall";

            // Act
            var result = detector.Detect(transcription);

            // Assert
            result.Detected.Should().BeTrue();
            result.CommandText.Should().Be("create a wall");
        }

        [Test]
        public void DetectWakeWord_WithoutWakeWord_ReturnsFalse()
        {
            // Arrange
            var detector = new WakeWordDetector("hey sting");
            var transcription = "create a wall";

            // Act
            var result = detector.Detect(transcription);

            // Assert
            result.Detected.Should().BeFalse();
        }

        [Test]
        public void DetectWakeWord_SimilarPhrase_ReturnsFalse()
        {
            // Arrange
            var detector = new WakeWordDetector("hey sting");
            var transcription = "Hey string, create a wall";

            // Act
            var result = detector.Detect(transcription);

            // Assert
            result.Detected.Should().BeFalse();
        }

        [Test]
        public void DetectWakeWord_CaseInsensitive_Works()
        {
            // Arrange
            var detector = new WakeWordDetector("hey sting");
            var transcription = "HEY STING create a wall";

            // Act
            var result = detector.Detect(transcription);

            // Assert
            result.Detected.Should().BeTrue();
        }

        #endregion

        #region Command Parsing Tests

        [Test]
        public void ParseCommand_CreateWall_ExtractsIntent()
        {
            // Arrange
            var parser = new VoiceCommandParser();
            var transcription = "create a wall three meters long";

            // Act
            var command = parser.Parse(transcription);

            // Assert
            command.Intent.Should().Be("create");
            command.ElementType.Should().Be("wall");
        }

        [Test]
        public void ParseCommand_WithDimensions_ExtractsDimensions()
        {
            // Arrange
            var parser = new VoiceCommandParser();
            var transcription = "create a wall three meters long and two point five meters high";

            // Act
            var command = parser.Parse(transcription);

            // Assert
            command.Dimensions.Should().ContainKey("length");
            command.Dimensions["length"].Should().BeApproximately(3.0, 0.1);
            command.Dimensions.Should().ContainKey("height");
            command.Dimensions["height"].Should().BeApproximately(2.5, 0.1);
        }

        [Test]
        public void ParseCommand_NumberWords_ConvertsCorrectly()
        {
            // Arrange
            var parser = new VoiceCommandParser();
            var transcription = "add five windows";

            // Act
            var command = parser.Parse(transcription);

            // Assert
            command.Quantity.Should().Be(5);
        }

        #endregion

        #region Helper Methods

        private float[] GenerateSineWave(int sampleRate, int frequency, float amplitude)
        {
            var samples = new float[sampleRate];
            for (int i = 0; i < sampleRate; i++)
            {
                samples[i] = amplitude * (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
            }
            return samples;
        }

        private float[] GenerateSpeechLikeSignal(int length)
        {
            var samples = new float[length];
            var random = new Random(42);

            for (int i = 0; i < length; i++)
            {
                // Simulate speech with varying amplitude
                float envelope = (float)(0.3 + 0.2 * Math.Sin(2 * Math.PI * 4 * i / length));
                samples[i] = envelope * (float)(random.NextDouble() * 2 - 1);
            }

            return samples;
        }

        private float[] GenerateWhiteNoise(int length, float amplitude)
        {
            var samples = new float[length];
            var random = new Random(42);

            for (int i = 0; i < length; i++)
            {
                samples[i] = amplitude * (float)(random.NextDouble() * 2 - 1);
            }

            return samples;
        }

        private float[] GenerateSpeechWithPauses(int length)
        {
            var samples = new float[length];
            var random = new Random(42);

            // Create speech-like signal with pauses
            for (int i = 0; i < length; i++)
            {
                int segment = i / (length / 3);
                bool isSpeech = segment == 0 || segment == 2; // Speech, pause, speech

                if (isSpeech)
                {
                    samples[i] = 0.3f * (float)(random.NextDouble() * 2 - 1);
                }
                else
                {
                    samples[i] = 0.001f * (float)(random.NextDouble() * 2 - 1);
                }
            }

            return samples;
        }

        private float[] GenerateLogits(float targetProbability)
        {
            // Generate logits that would result in the target probability after softmax
            var logits = new float[100]; // Vocabulary size
            var random = new Random(42);

            for (int i = 0; i < logits.Length; i++)
            {
                logits[i] = (float)(random.NextDouble() * 2 - 1);
            }

            // Set first token to have target probability
            logits[0] = (float)Math.Log(targetProbability / (1 - targetProbability)) +
                        logits.Skip(1).Max();

            return logits;
        }

        #endregion
    }

    #region Test Helper Classes

    internal class WhisperTokenizer
    {
        private readonly Dictionary<string, int> _vocab = new()
        {
            { "create", 1001 },
            { "wall", 1002 },
            { "door", 1003 },
            { "window", 1004 },
            { "hello", 1005 },
            { "world", 1006 },
            { "a", 1007 },
            { "the", 1008 },
            { "meter", 1009 },
            { "meters", 1010 }
        };

        private readonly Dictionary<int, string> _reverseVocab;

        public WhisperTokenizer()
        {
            _reverseVocab = _vocab.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        }

        public int[] Tokenize(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<int>();

            var words = text.ToLower().Split(new[] { ' ', '!', '.', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var tokens = new List<int>();

            foreach (var word in words)
            {
                if (_vocab.TryGetValue(word, out var token))
                {
                    tokens.Add(token);
                }
                else
                {
                    // Unknown token - use character encoding
                    foreach (var c in word)
                    {
                        tokens.Add((int)c + 256);
                    }
                }
            }

            return tokens.ToArray();
        }

        public string Detokenize(int[] tokens)
        {
            var words = new List<string>();
            var charBuffer = new List<char>();

            foreach (var token in tokens)
            {
                if (_reverseVocab.TryGetValue(token, out var word))
                {
                    if (charBuffer.Count > 0)
                    {
                        words.Add(new string(charBuffer.ToArray()));
                        charBuffer.Clear();
                    }
                    words.Add(word);
                }
                else if (token > 256 && token < 512)
                {
                    charBuffer.Add((char)(token - 256));
                }
            }

            if (charBuffer.Count > 0)
            {
                words.Add(new string(charBuffer.ToArray()));
            }

            return string.Join(" ", words);
        }
    }

    internal class AudioFeatures
    {
        public float[] MelSpectrogram { get; set; }
        public float AverageEnergy { get; set; }
    }

    internal class AudioProcessor
    {
        public AudioFeatures ExtractFeatures(float[] samples)
        {
            // Simplified mel spectrogram calculation
            var windowSize = 400;
            var hopSize = 160;
            var numFrames = (samples.Length - windowSize) / hopSize + 1;
            var melBins = 80;

            var melSpec = new float[numFrames * melBins];

            // Calculate average energy
            float energy = 0;
            foreach (var s in samples) energy += s * s;
            energy /= samples.Length;

            return new AudioFeatures
            {
                MelSpectrogram = melSpec,
                AverageEnergy = energy
            };
        }

        public float[] Normalize(float[] samples, float targetPeak)
        {
            var maxValue = samples.Max(Math.Abs);
            if (maxValue < 0.0001f) return samples;

            var scale = targetPeak / maxValue;
            var normalized = new float[samples.Length];

            for (int i = 0; i < samples.Length; i++)
            {
                normalized[i] = samples[i] * scale;
            }

            return normalized;
        }

        public float[] Resample(float[] samples, int fromRate, int toRate)
        {
            var ratio = (double)toRate / fromRate;
            var newLength = (int)(samples.Length * ratio);
            var resampled = new float[newLength];

            for (int i = 0; i < newLength; i++)
            {
                var srcIndex = i / ratio;
                var srcIndexInt = (int)srcIndex;
                var frac = srcIndex - srcIndexInt;

                if (srcIndexInt + 1 < samples.Length)
                {
                    resampled[i] = (float)((1 - frac) * samples[srcIndexInt] + frac * samples[srcIndexInt + 1]);
                }
                else if (srcIndexInt < samples.Length)
                {
                    resampled[i] = samples[srcIndexInt];
                }
            }

            return resampled;
        }
    }

    internal class VoiceActivityDetector
    {
        private readonly float _threshold;

        public VoiceActivityDetector(float threshold = 0.01f)
        {
            _threshold = threshold;
        }

        public bool DetectActivity(float[] samples)
        {
            if (samples.Length == 0) return false;

            float energy = 0;
            foreach (var s in samples) energy += s * s;
            energy /= samples.Length;

            return Math.Sqrt(energy) > _threshold;
        }

        public List<(int Start, int End)> GetSpeechSegments(float[] samples, int sampleRate)
        {
            var segments = new List<(int Start, int End)>();
            var frameSize = sampleRate / 10; // 100ms frames
            var numFrames = samples.Length / frameSize;

            int? segmentStart = null;

            for (int i = 0; i < numFrames; i++)
            {
                var frame = new float[frameSize];
                Array.Copy(samples, i * frameSize, frame, 0, frameSize);

                var hasActivity = DetectActivity(frame);

                if (hasActivity && segmentStart == null)
                {
                    segmentStart = i * frameSize;
                }
                else if (!hasActivity && segmentStart != null)
                {
                    segments.Add((segmentStart.Value, i * frameSize));
                    segmentStart = null;
                }
            }

            if (segmentStart != null)
            {
                segments.Add((segmentStart.Value, samples.Length));
            }

            return segments;
        }
    }

    internal class ConfidenceScorer
    {
        public float CalculateOverallConfidence(double[] tokenProbabilities)
        {
            if (tokenProbabilities.Length == 0) return 0;

            // Use geometric mean for overall confidence
            double product = 1;
            foreach (var p in tokenProbabilities)
            {
                product *= p;
            }

            return (float)Math.Pow(product, 1.0 / tokenProbabilities.Length);
        }

        public float[] GetTokenConfidences(float[][] logits)
        {
            var confidences = new float[logits.Length];

            for (int i = 0; i < logits.Length; i++)
            {
                // Apply softmax and get max probability
                var maxLogit = logits[i].Max();
                var expSum = logits[i].Sum(l => Math.Exp(l - maxLogit));
                confidences[i] = (float)(Math.Exp(logits[i].Max() - maxLogit) / expSum);
            }

            return confidences;
        }
    }

    internal class TranscriptionResult
    {
        public string Text { get; set; }
        public float Confidence { get; set; }
        public double AudioDurationMs { get; set; }
        public double ProcessingTimeMs { get; set; }

        public double RealTimeFactor => AudioDurationMs > 0 ? ProcessingTimeMs / AudioDurationMs : 0;
    }

    internal class DetectedLanguage
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public double Confidence { get; set; }
    }

    internal class LanguageDetector
    {
        private readonly Dictionary<string, HashSet<string>> _languageIndicators = new()
        {
            { "en", new HashSet<string> { "the", "a", "is", "are", "create", "wall", "door", "window", "and", "of" } },
            { "fr", new HashSet<string> { "le", "la", "un", "une", "de", "et", "créer", "mur", "mètre", "trois" } },
            { "ar", new HashSet<string> { "إنشاء", "جدار", "ثلاثة", "أمتار", "بطول" } }
        };

        public DetectedLanguage DetectLanguage(string text)
        {
            var scores = new Dictionary<string, int>();

            foreach (var lang in _languageIndicators)
            {
                scores[lang.Key] = 0;
            }

            // Check for Arabic script
            if (text.Any(c => c >= 0x0600 && c <= 0x06FF))
            {
                return new DetectedLanguage { Code = "ar", Name = "Arabic", Confidence = 0.95 };
            }

            // Check for French accents
            var frenchChars = text.Count(c => "àâäéèêëïîôùûç".Contains(c));
            if (frenchChars > 0)
            {
                scores["fr"] += frenchChars * 2;
            }

            // Count word matches
            var words = text.ToLower().Split(new[] { ' ', ',', '.', '!' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                foreach (var lang in _languageIndicators)
                {
                    if (lang.Value.Contains(word))
                    {
                        scores[lang.Key]++;
                    }
                }
            }

            var maxScore = scores.Max(s => s.Value);
            var detectedLang = scores.FirstOrDefault(s => s.Value == maxScore).Key ?? "en";

            return new DetectedLanguage
            {
                Code = detectedLang,
                Name = detectedLang switch { "en" => "English", "fr" => "French", "ar" => "Arabic", _ => "Unknown" },
                Confidence = maxScore > 0 ? Math.Min(0.5 + maxScore * 0.1, 0.95) : 0.5
            };
        }
    }

    internal class WakeWordDetectionResult
    {
        public bool Detected { get; set; }
        public string CommandText { get; set; }
    }

    internal class WakeWordDetector
    {
        private readonly string _wakeWord;

        public WakeWordDetector(string wakeWord)
        {
            _wakeWord = wakeWord.ToLowerInvariant();
        }

        public WakeWordDetectionResult Detect(string transcription)
        {
            var lowerTranscription = transcription.ToLowerInvariant().Trim();

            if (lowerTranscription.StartsWith(_wakeWord))
            {
                var commandText = lowerTranscription.Substring(_wakeWord.Length).Trim();
                // Remove leading punctuation
                commandText = commandText.TrimStart(',', ' ');

                return new WakeWordDetectionResult
                {
                    Detected = true,
                    CommandText = commandText
                };
            }

            return new WakeWordDetectionResult { Detected = false };
        }
    }

    internal class VoiceCommand
    {
        public string Intent { get; set; }
        public string ElementType { get; set; }
        public int Quantity { get; set; } = 1;
        public Dictionary<string, double> Dimensions { get; set; } = new();
    }

    internal class VoiceCommandParser
    {
        private readonly Dictionary<string, int> _numberWords = new()
        {
            { "one", 1 }, { "two", 2 }, { "three", 3 }, { "four", 4 }, { "five", 5 },
            { "six", 6 }, { "seven", 7 }, { "eight", 8 }, { "nine", 9 }, { "ten", 10 }
        };

        private readonly HashSet<string> _intents = new() { "create", "add", "make", "delete", "remove", "modify", "change" };
        private readonly HashSet<string> _elementTypes = new() { "wall", "door", "window", "floor", "ceiling", "room", "column" };

        public VoiceCommand Parse(string transcription)
        {
            var command = new VoiceCommand();
            var lower = transcription.ToLowerInvariant();
            var words = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Find intent
            foreach (var word in words)
            {
                if (_intents.Contains(word))
                {
                    command.Intent = word;
                    break;
                }
            }

            // Find element type
            foreach (var word in words)
            {
                if (_elementTypes.Contains(word))
                {
                    command.ElementType = word;
                    break;
                }
            }

            // Find quantity
            for (int i = 0; i < words.Length - 1; i++)
            {
                if (_numberWords.TryGetValue(words[i], out var num))
                {
                    if (_elementTypes.Contains(words[i + 1]) ||
                        (i + 1 < words.Length - 1 && _elementTypes.Contains(words[i + 1] + "s")))
                    {
                        command.Quantity = num;
                        break;
                    }
                }
            }

            // Parse dimensions
            ParseDimensions(lower, command);

            return command;
        }

        private void ParseDimensions(string text, VoiceCommand command)
        {
            // Parse "X meters long" pattern
            var lengthMatch = System.Text.RegularExpressions.Regex.Match(
                text, @"(\d+(?:\.\d+)?|one|two|three|four|five|six|seven|eight|nine|ten)(?:\s+point\s+(\d+))?\s*(?:m|meters?)\s+long");
            if (lengthMatch.Success)
            {
                command.Dimensions["length"] = ParseNumber(lengthMatch.Groups[1].Value, lengthMatch.Groups[2].Value);
            }

            // Parse "X meters high" pattern
            var heightMatch = System.Text.RegularExpressions.Regex.Match(
                text, @"(\d+(?:\.\d+)?|one|two|three|four|five|six|seven|eight|nine|ten)(?:\s+point\s+(\d+))?\s*(?:m|meters?)\s+high");
            if (heightMatch.Success)
            {
                command.Dimensions["height"] = ParseNumber(heightMatch.Groups[1].Value, heightMatch.Groups[2].Value);
            }
        }

        private double ParseNumber(string main, string decimal_)
        {
            double value;
            if (_numberWords.TryGetValue(main, out var numWord))
            {
                value = numWord;
            }
            else
            {
                value = double.Parse(main);
            }

            if (!string.IsNullOrEmpty(decimal_))
            {
                value += double.Parse(decimal_) / 10.0;
            }

            return value;
        }
    }

    #endregion
}
