// StingBIM.AI.NLP.Pipeline.Tokenizer
// BPE Tokenizer for text processing
// Master Proposal Reference: Part 1.1 Language Understanding - Tokenizer (BPE)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.NLP.Pipeline
{
    /// <summary>
    /// Byte-Pair Encoding (BPE) tokenizer for text processing.
    /// Compatible with GPT-style tokenization.
    /// </summary>
    public class Tokenizer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private Dictionary<string, int> _encoder;
        private Dictionary<int, string> _decoder;
        private Dictionary<(string, string), int> _bpeRanks;
        private Regex _pattern;
        private bool _isLoaded;

        // Special tokens
        public int PadTokenId { get; private set; } = 0;
        public int BosTokenId { get; private set; } = 1;
        public int EosTokenId { get; private set; } = 2;
        public int UnkTokenId { get; private set; } = 3;

        public int VocabSize => _encoder?.Count ?? 0;

        /// <summary>
        /// Loads the tokenizer vocabulary and merge rules.
        /// </summary>
        public async Task LoadAsync(string vocabPath, string mergesPath)
        {
            Logger.Info("Loading tokenizer...");

            await Task.Run(() =>
            {
                try
                {
                    // Load vocabulary
                    var vocabJson = File.ReadAllText(vocabPath);
                    _encoder = JsonConvert.DeserializeObject<Dictionary<string, int>>(vocabJson);
                    _decoder = _encoder.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

                    // Load BPE merges
                    var mergesText = File.ReadAllText(mergesPath);
                    var mergeLines = mergesText.Split('\n')
                        .Skip(1) // Skip header
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .ToList();

                    _bpeRanks = new Dictionary<(string, string), int>();
                    for (int i = 0; i < mergeLines.Count; i++)
                    {
                        var parts = mergeLines[i].Split(' ');
                        if (parts.Length >= 2)
                        {
                            _bpeRanks[(parts[0], parts[1])] = i;
                        }
                    }

                    // GPT-2 style pattern for splitting
                    _pattern = new Regex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+",
                        RegexOptions.Compiled);

                    _isLoaded = true;
                    Logger.Info($"Tokenizer loaded: {VocabSize} tokens, {_bpeRanks.Count} merges");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load tokenizer");
                    throw;
                }
            });
        }

        /// <summary>
        /// Encodes text to token IDs.
        /// </summary>
        public long[] Encode(string text, bool addSpecialTokens = true)
        {
            EnsureLoaded();

            var tokens = new List<int>();

            if (addSpecialTokens)
            {
                tokens.Add(BosTokenId);
            }

            // Split text into words/subwords
            var matches = _pattern.Matches(text);
            foreach (Match match in matches)
            {
                var word = match.Value;
                var wordTokens = BpeEncode(word);
                tokens.AddRange(wordTokens);
            }

            if (addSpecialTokens)
            {
                tokens.Add(EosTokenId);
            }

            return tokens.Select(t => (long)t).ToArray();
        }

        /// <summary>
        /// Decodes token IDs back to text.
        /// </summary>
        public string Decode(long[] tokenIds, bool skipSpecialTokens = true)
        {
            EnsureLoaded();

            var tokens = tokenIds.Select(t => (int)t);

            if (skipSpecialTokens)
            {
                var specialTokens = new HashSet<int> { PadTokenId, BosTokenId, EosTokenId, UnkTokenId };
                tokens = tokens.Where(t => !specialTokens.Contains(t));
            }

            var sb = new StringBuilder();
            foreach (var tokenId in tokens)
            {
                if (_decoder.TryGetValue(tokenId, out var token))
                {
                    sb.Append(token);
                }
            }

            // Decode byte-level tokens back to text
            return DecodeBytes(sb.ToString());
        }

        /// <summary>
        /// Creates attention mask for the given tokens.
        /// </summary>
        public long[] CreateAttentionMask(long[] tokenIds)
        {
            return tokenIds.Select(t => t != PadTokenId ? 1L : 0L).ToArray();
        }

        /// <summary>
        /// Pads or truncates token sequence to specified length.
        /// </summary>
        public (long[] TokenIds, long[] AttentionMask) PadOrTruncate(long[] tokenIds, int maxLength, bool padOnRight = true)
        {
            var result = new long[maxLength];
            var mask = new long[maxLength];

            var copyLength = Math.Min(tokenIds.Length, maxLength);

            if (padOnRight)
            {
                Array.Copy(tokenIds, result, copyLength);
                for (int i = 0; i < copyLength; i++) mask[i] = 1;
                for (int i = copyLength; i < maxLength; i++) result[i] = PadTokenId;
            }
            else
            {
                var offset = maxLength - copyLength;
                Array.Copy(tokenIds, 0, result, offset, copyLength);
                for (int i = offset; i < maxLength; i++) mask[i] = 1;
                for (int i = 0; i < offset; i++) result[i] = PadTokenId;
            }

            return (result, mask);
        }

        private List<int> BpeEncode(string word)
        {
            var tokens = new List<int>();

            // Convert word to byte-level representation
            var byteWord = EncodeBytes(word);
            var wordPieces = byteWord.Select(c => c.ToString()).ToList();

            // Apply BPE merges
            while (wordPieces.Count > 1)
            {
                // Find the pair with lowest rank
                int? minRank = null;
                int minIndex = -1;

                for (int i = 0; i < wordPieces.Count - 1; i++)
                {
                    var pair = (wordPieces[i], wordPieces[i + 1]);
                    if (_bpeRanks.TryGetValue(pair, out var rank))
                    {
                        if (minRank == null || rank < minRank)
                        {
                            minRank = rank;
                            minIndex = i;
                        }
                    }
                }

                if (minIndex == -1) break;

                // Merge the pair
                var merged = wordPieces[minIndex] + wordPieces[minIndex + 1];
                wordPieces[minIndex] = merged;
                wordPieces.RemoveAt(minIndex + 1);
            }

            // Convert to token IDs
            foreach (var piece in wordPieces)
            {
                if (_encoder.TryGetValue(piece, out var tokenId))
                {
                    tokens.Add(tokenId);
                }
                else
                {
                    tokens.Add(UnkTokenId);
                }
            }

            return tokens;
        }

        private string EncodeBytes(string text)
        {
            // Convert to byte-level representation for GPT-style tokenization
            var bytes = Encoding.UTF8.GetBytes(text);
            return string.Concat(bytes.Select(b => ByteToChar(b)));
        }

        private string DecodeBytes(string text)
        {
            var bytes = text.Select(c => CharToByte(c)).ToArray();
            return Encoding.UTF8.GetString(bytes);
        }

        private char ByteToChar(byte b)
        {
            // GPT-2 byte encoding mapping
            if (b >= 33 && b <= 126) return (char)b;
            if (b >= 161 && b <= 172) return (char)b;
            if (b >= 174 && b <= 255) return (char)b;
            return (char)(b + 256);
        }

        private byte CharToByte(char c)
        {
            int i = c;
            if (i >= 256) return (byte)(i - 256);
            return (byte)i;
        }

        private void EnsureLoaded()
        {
            if (!_isLoaded)
            {
                throw new InvalidOperationException("Tokenizer not loaded. Call LoadAsync first.");
            }
        }
    }
}
