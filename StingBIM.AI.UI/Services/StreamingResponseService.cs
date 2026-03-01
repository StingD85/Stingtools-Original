// StingBIM.AI.UI.Services.StreamingResponseService
// Service for streaming AI responses with typing effect
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NLog;

namespace StingBIM.AI.UI.Services
{
    /// <summary>
    /// Service for displaying AI responses with a streaming/typing effect.
    /// </summary>
    public sealed class StreamingResponseService
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Lazy<StreamingResponseService> _instance =
            new Lazy<StreamingResponseService>(() => new StreamingResponseService());

        public static StreamingResponseService Instance => _instance.Value;

        private CancellationTokenSource _currentCts;
        private bool _isStreaming;

        /// <summary>
        /// Event fired when streaming starts.
        /// </summary>
        public event EventHandler StreamingStarted;

        /// <summary>
        /// Event fired when a character is added during streaming.
        /// </summary>
        public event EventHandler<StreamingCharacterEventArgs> CharacterAdded;

        /// <summary>
        /// Event fired when streaming completes.
        /// </summary>
        public event EventHandler<StreamingCompletedEventArgs> StreamingCompleted;

        /// <summary>
        /// Event fired when streaming is cancelled.
        /// </summary>
        public event EventHandler StreamingCancelled;

        /// <summary>
        /// Gets whether streaming is currently in progress.
        /// </summary>
        public bool IsStreaming => _isStreaming;

        /// <summary>
        /// Gets or sets the base delay between characters in milliseconds.
        /// </summary>
        public int BaseDelayMs { get; set; } = 20;

        /// <summary>
        /// Gets or sets whether to use variable speed (slower for punctuation).
        /// </summary>
        public bool UseVariableSpeed { get; set; } = true;

        /// <summary>
        /// Gets or sets whether streaming is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        private StreamingResponseService()
        {
        }

        #region Public Methods

        /// <summary>
        /// Streams text with a typing effect, calling back for each character.
        /// </summary>
        /// <param name="text">The full text to stream.</param>
        /// <param name="onCharacter">Callback for each character added.</param>
        /// <param name="dispatcher">WPF dispatcher for UI updates.</param>
        /// <returns>Task that completes when streaming is done.</returns>
        public async Task StreamTextAsync(string text, Action<string> onCharacter, Dispatcher dispatcher = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                onCharacter?.Invoke(text);
                return;
            }

            if (!IsEnabled)
            {
                // If streaming is disabled, show all at once
                onCharacter?.Invoke(text);
                StreamingCompleted?.Invoke(this, new StreamingCompletedEventArgs(text, false));
                return;
            }

            // Cancel any existing stream
            CancelStreaming();

            _currentCts = new CancellationTokenSource();
            var token = _currentCts.Token;
            _isStreaming = true;

            StreamingStarted?.Invoke(this, EventArgs.Empty);
            Logger.Debug($"Starting streaming response: {text.Length} characters");

            var currentText = "";

            try
            {
                for (int i = 0; i < text.Length; i++)
                {
                    token.ThrowIfCancellationRequested();

                    var c = text[i];
                    currentText += c;

                    // Update UI
                    if (dispatcher != null)
                    {
                        await dispatcher.InvokeAsync(() => onCharacter?.Invoke(currentText));
                    }
                    else
                    {
                        onCharacter?.Invoke(currentText);
                    }

                    CharacterAdded?.Invoke(this, new StreamingCharacterEventArgs(c, i, text.Length));

                    // Calculate delay
                    var delay = GetDelayForCharacter(c);
                    if (delay > 0)
                    {
                        await Task.Delay(delay, token);
                    }
                }

                _isStreaming = false;
                StreamingCompleted?.Invoke(this, new StreamingCompletedEventArgs(text, false));
                Logger.Debug("Streaming completed");
            }
            catch (OperationCanceledException)
            {
                _isStreaming = false;

                // Show remaining text immediately
                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() => onCharacter?.Invoke(text));
                }
                else
                {
                    onCharacter?.Invoke(text);
                }

                StreamingCancelled?.Invoke(this, EventArgs.Empty);
                Logger.Debug("Streaming cancelled");
            }
            catch (Exception ex)
            {
                _isStreaming = false;
                Logger.Error(ex, "Error during streaming");

                // Show full text on error
                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() => onCharacter?.Invoke(text));
                }
                else
                {
                    onCharacter?.Invoke(text);
                }

                StreamingCompleted?.Invoke(this, new StreamingCompletedEventArgs(text, true));
            }
        }

        /// <summary>
        /// Streams text to a callback that receives the full text each time.
        /// </summary>
        public async Task StreamToCallbackAsync(string text, Action<string, bool> onUpdate)
        {
            if (string.IsNullOrEmpty(text))
            {
                onUpdate?.Invoke(text, true);
                return;
            }

            if (!IsEnabled)
            {
                onUpdate?.Invoke(text, true);
                return;
            }

            CancelStreaming();
            _currentCts = new CancellationTokenSource();
            var token = _currentCts.Token;
            _isStreaming = true;

            StreamingStarted?.Invoke(this, EventArgs.Empty);

            var currentText = "";

            try
            {
                for (int i = 0; i < text.Length; i++)
                {
                    token.ThrowIfCancellationRequested();

                    currentText += text[i];
                    onUpdate?.Invoke(currentText, false);

                    var delay = GetDelayForCharacter(text[i]);
                    if (delay > 0)
                    {
                        await Task.Delay(delay, token);
                    }
                }

                onUpdate?.Invoke(text, true);
                _isStreaming = false;
                StreamingCompleted?.Invoke(this, new StreamingCompletedEventArgs(text, false));
            }
            catch (OperationCanceledException)
            {
                onUpdate?.Invoke(text, true);
                _isStreaming = false;
                StreamingCancelled?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Streams text word by word instead of character by character.
        /// </summary>
        public async Task StreamByWordAsync(string text, Action<string> onUpdate, Dispatcher dispatcher = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                onUpdate?.Invoke(text);
                return;
            }

            if (!IsEnabled)
            {
                onUpdate?.Invoke(text);
                return;
            }

            CancelStreaming();
            _currentCts = new CancellationTokenSource();
            var token = _currentCts.Token;
            _isStreaming = true;

            StreamingStarted?.Invoke(this, EventArgs.Empty);

            var words = text.Split(' ');
            var currentText = "";

            try
            {
                for (int i = 0; i < words.Length; i++)
                {
                    token.ThrowIfCancellationRequested();

                    if (i > 0) currentText += " ";
                    currentText += words[i];

                    if (dispatcher != null)
                    {
                        await dispatcher.InvokeAsync(() => onUpdate?.Invoke(currentText));
                    }
                    else
                    {
                        onUpdate?.Invoke(currentText);
                    }

                    // Delay based on word length and punctuation
                    var delay = BaseDelayMs * 3;
                    if (words[i].EndsWith(".") || words[i].EndsWith("!") || words[i].EndsWith("?"))
                    {
                        delay += 100;
                    }
                    else if (words[i].EndsWith(",") || words[i].EndsWith(";") || words[i].EndsWith(":"))
                    {
                        delay += 50;
                    }

                    await Task.Delay(delay, token);
                }

                _isStreaming = false;
                StreamingCompleted?.Invoke(this, new StreamingCompletedEventArgs(text, false));
            }
            catch (OperationCanceledException)
            {
                onUpdate?.Invoke(text);
                _isStreaming = false;
                StreamingCancelled?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Cancels any ongoing streaming.
        /// </summary>
        public void CancelStreaming()
        {
            if (_currentCts != null && !_currentCts.IsCancellationRequested)
            {
                _currentCts.Cancel();
                _currentCts.Dispose();
                _currentCts = null;
            }
            _isStreaming = false;
        }

        /// <summary>
        /// Skips to the end of the current streaming (shows full text immediately).
        /// </summary>
        public void SkipToEnd()
        {
            CancelStreaming();
        }

        #endregion

        #region Private Methods

        private int GetDelayForCharacter(char c)
        {
            if (!UseVariableSpeed)
            {
                return BaseDelayMs;
            }

            // Variable delays for more natural typing feel
            return c switch
            {
                '.' or '!' or '?' => BaseDelayMs * 8,  // Long pause after sentences
                ',' or ';' or ':' => BaseDelayMs * 4,  // Medium pause after clauses
                '\n' => BaseDelayMs * 6,               // Pause at line breaks
                ' ' => BaseDelayMs / 2,                // Quick for spaces
                _ => BaseDelayMs                       // Normal for other characters
            };
        }

        #endregion
    }

    #region Event Args

    /// <summary>
    /// Event args for character added during streaming.
    /// </summary>
    public class StreamingCharacterEventArgs : EventArgs
    {
        public char Character { get; }
        public int Index { get; }
        public int TotalLength { get; }
        public double Progress => TotalLength > 0 ? (double)(Index + 1) / TotalLength : 1.0;

        public StreamingCharacterEventArgs(char character, int index, int totalLength)
        {
            Character = character;
            Index = index;
            TotalLength = totalLength;
        }
    }

    /// <summary>
    /// Event args for streaming completion.
    /// </summary>
    public class StreamingCompletedEventArgs : EventArgs
    {
        public string FullText { get; }
        public bool HadError { get; }

        public StreamingCompletedEventArgs(string fullText, bool hadError)
        {
            FullText = fullText;
            HadError = hadError;
        }
    }

    #endregion
}
