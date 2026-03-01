// StingBIM.AI.UI.Controls.VoiceWaveform
// Voice waveform visualization during audio recording
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using NLog;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Displays an animated waveform visualization for voice recording.
    /// </summary>
    public partial class VoiceWaveform : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly DispatcherTimer _updateTimer;
        private readonly DispatcherTimer _durationTimer;
        private readonly List<Rectangle> _bars;
        private readonly Random _random;
        private readonly Queue<double[]> _audioLevelHistory;

        private Storyboard _pulseAnimation;
        private Storyboard _idleWaveAnimation;
        private DateTime _recordingStartTime;
        private WaveformState _currentState = WaveformState.Idle;
        private double[] _currentLevels;
        private const int BAR_COUNT = 15;

        #region Dependency Properties

        public static readonly DependencyProperty IsRecordingProperty =
            DependencyProperty.Register(nameof(IsRecording), typeof(bool), typeof(VoiceWaveform),
                new PropertyMetadata(false, OnIsRecordingChanged));

        public static readonly DependencyProperty ShowDurationProperty =
            DependencyProperty.Register(nameof(ShowDuration), typeof(bool), typeof(VoiceWaveform),
                new PropertyMetadata(true));

        public static readonly DependencyProperty BarColorProperty =
            DependencyProperty.Register(nameof(BarColor), typeof(Brush), typeof(VoiceWaveform),
                new PropertyMetadata(null, OnBarColorChanged));

        public static readonly DependencyProperty RecordingColorProperty =
            DependencyProperty.Register(nameof(RecordingColor), typeof(Brush), typeof(VoiceWaveform),
                new PropertyMetadata(null));

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether recording is active.
        /// </summary>
        public bool IsRecording
        {
            get => (bool)GetValue(IsRecordingProperty);
            set => SetValue(IsRecordingProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the duration display.
        /// </summary>
        public bool ShowDuration
        {
            get => (bool)GetValue(ShowDurationProperty);
            set => SetValue(ShowDurationProperty, value);
        }

        /// <summary>
        /// Gets or sets the color of the waveform bars.
        /// </summary>
        public Brush BarColor
        {
            get => (Brush)GetValue(BarColorProperty);
            set => SetValue(BarColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the color of the recording indicator.
        /// </summary>
        public Brush RecordingColor
        {
            get => (Brush)GetValue(RecordingColorProperty);
            set => SetValue(RecordingColorProperty, value);
        }

        /// <summary>
        /// Gets the current recording duration.
        /// </summary>
        public TimeSpan Duration { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when recording starts.
        /// </summary>
        public event EventHandler RecordingStarted;

        /// <summary>
        /// Event fired when recording stops.
        /// </summary>
        public event EventHandler RecordingStopped;

        #endregion

        public VoiceWaveform()
        {
            InitializeComponent();

            _random = new Random();
            _bars = new List<Rectangle>();
            _audioLevelHistory = new Queue<double[]>();
            _currentLevels = new double[BAR_COUNT];

            // Initialize bars list
            _bars.Add(Bar0); _bars.Add(Bar1); _bars.Add(Bar2); _bars.Add(Bar3); _bars.Add(Bar4);
            _bars.Add(Bar5); _bars.Add(Bar6); _bars.Add(Bar7); _bars.Add(Bar8); _bars.Add(Bar9);
            _bars.Add(Bar10); _bars.Add(Bar11); _bars.Add(Bar12); _bars.Add(Bar13); _bars.Add(Bar14);

            // Setup update timer for waveform animation
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _updateTimer.Tick += UpdateTimer_Tick;

            // Setup duration timer
            _durationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _durationTimer.Tick += DurationTimer_Tick;

            // Get storyboards
            _pulseAnimation = (Storyboard)FindResource("PulseAnimation");
            _idleWaveAnimation = (Storyboard)FindResource("IdleWaveAnimation");

            SetState(WaveformState.Idle);
        }

        #region Public Methods

        /// <summary>
        /// Updates the waveform with audio level data.
        /// </summary>
        /// <param name="levels">Array of audio levels (0.0 to 1.0) for each bar.</param>
        public void UpdateLevels(double[] levels)
        {
            if (levels == null || levels.Length == 0)
                return;

            // Interpolate to match bar count
            _currentLevels = InterpolateLevels(levels, BAR_COUNT);

            // Add to history for smoothing
            _audioLevelHistory.Enqueue(_currentLevels.ToArray());
            while (_audioLevelHistory.Count > 5)
            {
                _audioLevelHistory.Dequeue();
            }
        }

        /// <summary>
        /// Updates the waveform with a single audio level value.
        /// </summary>
        /// <param name="level">Audio level (0.0 to 1.0).</param>
        public void UpdateLevel(double level)
        {
            // Generate a simulated multi-bar visualization from single level
            var levels = new double[BAR_COUNT];
            for (int i = 0; i < BAR_COUNT; i++)
            {
                // Create a natural-looking waveform centered around the level
                var distanceFromCenter = Math.Abs(i - BAR_COUNT / 2.0) / (BAR_COUNT / 2.0);
                var randomVariation = _random.NextDouble() * 0.3;
                levels[i] = level * (1 - distanceFromCenter * 0.5) * (0.7 + randomVariation);
            }
            UpdateLevels(levels);
        }

        /// <summary>
        /// Starts the waveform animation for recording.
        /// </summary>
        public void StartRecording()
        {
            IsRecording = true;
        }

        /// <summary>
        /// Stops the waveform animation.
        /// </summary>
        public void StopRecording()
        {
            IsRecording = false;
        }

        /// <summary>
        /// Resets the waveform to idle state.
        /// </summary>
        public void Reset()
        {
            StopRecording();
            Duration = TimeSpan.Zero;
            DurationText.Text = "0:00";
            StatusText.Text = "Ready";
            _audioLevelHistory.Clear();
            Array.Clear(_currentLevels, 0, _currentLevels.Length);
        }

        #endregion

        #region Private Methods

        private static void OnIsRecordingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VoiceWaveform waveform)
            {
                var isRecording = (bool)e.NewValue;
                if (isRecording)
                {
                    waveform.SetState(WaveformState.Recording);
                    waveform.RecordingStarted?.Invoke(waveform, EventArgs.Empty);
                }
                else
                {
                    waveform.SetState(WaveformState.Idle);
                    waveform.RecordingStopped?.Invoke(waveform, EventArgs.Empty);
                }
            }
        }

        private static void OnBarColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VoiceWaveform waveform && e.NewValue is Brush brush)
            {
                foreach (var bar in waveform._bars)
                {
                    bar.Fill = brush;
                }
            }
        }

        private void SetState(WaveformState state)
        {
            _currentState = state;

            switch (state)
            {
                case WaveformState.Idle:
                    _updateTimer.Stop();
                    _durationTimer.Stop();
                    _pulseAnimation?.Stop();

                    RecordingIndicator.Visibility = Visibility.Collapsed;
                    IdleIndicator.Visibility = Visibility.Visible;
                    StatusText.Text = "Ready";

                    // Reset bars to minimum height
                    foreach (var bar in _bars)
                    {
                        bar.Height = 8;
                    }
                    break;

                case WaveformState.Recording:
                    _recordingStartTime = DateTime.Now;
                    Duration = TimeSpan.Zero;
                    DurationText.Text = "0:00";

                    RecordingIndicator.Visibility = Visibility.Visible;
                    IdleIndicator.Visibility = Visibility.Collapsed;
                    StatusText.Text = "Listening...";

                    _updateTimer.Start();
                    _durationTimer.Start();
                    _pulseAnimation?.Begin();

                    Logger.Debug("Waveform recording started");
                    break;

                case WaveformState.Processing:
                    _updateTimer.Stop();
                    _durationTimer.Stop();
                    StatusText.Text = "Processing...";
                    Logger.Debug("Waveform processing");
                    break;
            }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (_currentState != WaveformState.Recording)
                return;

            // Get smoothed levels from history
            var smoothedLevels = GetSmoothedLevels();

            // Update bar heights
            for (int i = 0; i < _bars.Count && i < smoothedLevels.Length; i++)
            {
                var targetHeight = Math.Max(4, smoothedLevels[i] * 40);

                // Smooth animation
                var currentHeight = _bars[i].Height;
                var newHeight = currentHeight + (targetHeight - currentHeight) * 0.3;

                _bars[i].Height = newHeight;
            }

            // If no audio data, generate simulated activity
            if (_audioLevelHistory.Count == 0)
            {
                GenerateSimulatedActivity();
            }
        }

        private void DurationTimer_Tick(object sender, EventArgs e)
        {
            Duration = DateTime.Now - _recordingStartTime;
            DurationText.Text = Duration.ToString(@"m\:ss");

            // Update status based on duration
            if (Duration.TotalSeconds < 2)
            {
                StatusText.Text = "Listening...";
            }
            else if (Duration.TotalSeconds < 10)
            {
                StatusText.Text = "Recording...";
            }
            else
            {
                StatusText.Text = $"Recording ({Duration:m\\:ss})";
            }
        }

        private double[] GetSmoothedLevels()
        {
            if (_audioLevelHistory.Count == 0)
                return _currentLevels;

            var result = new double[BAR_COUNT];
            var historyArray = _audioLevelHistory.ToArray();

            for (int i = 0; i < BAR_COUNT; i++)
            {
                var sum = 0.0;
                var weight = 0.0;
                for (int h = 0; h < historyArray.Length; h++)
                {
                    var w = h + 1; // More recent samples have higher weight
                    sum += historyArray[h][i] * w;
                    weight += w;
                }
                result[i] = sum / weight;
            }

            return result;
        }

        private void GenerateSimulatedActivity()
        {
            // Generate random but natural-looking activity when no real audio data
            var baseLevel = 0.2 + _random.NextDouble() * 0.3;

            for (int i = 0; i < _currentLevels.Length; i++)
            {
                var noise = _random.NextDouble() * 0.4 - 0.2;
                var centerWeight = 1 - Math.Abs(i - BAR_COUNT / 2.0) / (BAR_COUNT / 2.0) * 0.3;
                _currentLevels[i] = Math.Max(0, Math.Min(1, (baseLevel + noise) * centerWeight));
            }

            _audioLevelHistory.Enqueue(_currentLevels.ToArray());
            while (_audioLevelHistory.Count > 5)
            {
                _audioLevelHistory.Dequeue();
            }
        }

        private static double[] InterpolateLevels(double[] input, int targetCount)
        {
            var result = new double[targetCount];

            if (input.Length == targetCount)
            {
                Array.Copy(input, result, targetCount);
                return result;
            }

            var ratio = (double)(input.Length - 1) / (targetCount - 1);

            for (int i = 0; i < targetCount; i++)
            {
                var srcIndex = i * ratio;
                var lowerIndex = (int)srcIndex;
                var upperIndex = Math.Min(lowerIndex + 1, input.Length - 1);
                var fraction = srcIndex - lowerIndex;

                result[i] = input[lowerIndex] * (1 - fraction) + input[upperIndex] * fraction;
            }

            return result;
        }

        #endregion
    }

    /// <summary>
    /// Waveform display states.
    /// </summary>
    public enum WaveformState
    {
        Idle,
        Recording,
        Processing
    }
}
