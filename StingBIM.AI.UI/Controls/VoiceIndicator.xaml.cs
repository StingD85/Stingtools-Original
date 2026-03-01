// StingBIM.AI.UI.Controls.VoiceIndicator
// Visual indicator for voice input state (listening, speaking, processing)
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - Voice Input

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Visual indicator showing the current voice input state.
    /// Displays animations for listening, speaking, and processing states.
    /// </summary>
    public partial class VoiceIndicator : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(
                nameof(State),
                typeof(VoiceState),
                typeof(VoiceIndicator),
                new PropertyMetadata(VoiceState.Idle, OnStateChanged));

        public static readonly DependencyProperty ShowStatusTextProperty =
            DependencyProperty.Register(
                nameof(ShowStatusText),
                typeof(bool),
                typeof(VoiceIndicator),
                new PropertyMetadata(true, OnShowStatusTextChanged));

        public static readonly DependencyProperty AudioLevelProperty =
            DependencyProperty.Register(
                nameof(AudioLevel),
                typeof(double),
                typeof(VoiceIndicator),
                new PropertyMetadata(0.0, OnAudioLevelChanged));

        #endregion

        #region Properties

        /// <summary>
        /// Current voice input state.
        /// </summary>
        public VoiceState State
        {
            get => (VoiceState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        /// <summary>
        /// Whether to show the status text.
        /// </summary>
        public bool ShowStatusText
        {
            get => (bool)GetValue(ShowStatusTextProperty);
            set => SetValue(ShowStatusTextProperty, value);
        }

        /// <summary>
        /// Current audio input level (0.0 to 1.0).
        /// </summary>
        public double AudioLevel
        {
            get => (double)GetValue(AudioLevelProperty);
            set => SetValue(AudioLevelProperty, value);
        }

        #endregion

        private Storyboard _waveAnimation;
        private Storyboard _pulseAnimation;

        public VoiceIndicator()
        {
            InitializeComponent();

            // Get storyboard references
            _waveAnimation = (Storyboard)Resources["WaveAnimation"];
            _pulseAnimation = (Storyboard)Resources["PulseAnimation"];

            UpdateState();
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VoiceIndicator indicator)
            {
                indicator.UpdateState();
            }
        }

        private static void OnShowStatusTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VoiceIndicator indicator)
            {
                indicator.StatusText.Visibility = (bool)e.NewValue && indicator.State != VoiceState.Idle
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private static void OnAudioLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VoiceIndicator indicator && indicator.State == VoiceState.Speaking)
            {
                indicator.UpdateAudioBars((double)e.NewValue);
            }
        }

        private void UpdateState()
        {
            // Stop all animations first
            _waveAnimation?.Stop();
            _pulseAnimation?.Stop();

            switch (State)
            {
                case VoiceState.Idle:
                    MicrophoneIcon.Visibility = Visibility.Collapsed;
                    WaveBars.Visibility = Visibility.Collapsed;
                    PulseCircle.Opacity = 0;
                    StatusText.Visibility = Visibility.Collapsed;
                    break;

                case VoiceState.Listening:
                    MicrophoneIcon.Visibility = Visibility.Visible;
                    WaveBars.Visibility = Visibility.Collapsed;
                    PulseCircle.Visibility = Visibility.Visible;
                    StatusText.Text = "Listening...";
                    StatusText.Visibility = ShowStatusText ? Visibility.Visible : Visibility.Collapsed;
                    _pulseAnimation?.Begin();
                    break;

                case VoiceState.Speaking:
                    MicrophoneIcon.Visibility = Visibility.Collapsed;
                    WaveBars.Visibility = Visibility.Visible;
                    PulseCircle.Visibility = Visibility.Collapsed;
                    StatusText.Text = "Hearing you...";
                    StatusText.Visibility = ShowStatusText ? Visibility.Visible : Visibility.Collapsed;
                    _waveAnimation?.Begin();
                    break;

                case VoiceState.Processing:
                    MicrophoneIcon.Visibility = Visibility.Visible;
                    WaveBars.Visibility = Visibility.Collapsed;
                    PulseCircle.Visibility = Visibility.Visible;
                    PulseCircle.Opacity = 0.5;
                    StatusText.Text = "Processing...";
                    StatusText.Visibility = ShowStatusText ? Visibility.Visible : Visibility.Collapsed;
                    break;

                case VoiceState.Error:
                    MicrophoneIcon.Visibility = Visibility.Visible;
                    WaveBars.Visibility = Visibility.Collapsed;
                    PulseCircle.Visibility = Visibility.Collapsed;
                    StatusText.Text = "Error";
                    StatusText.Visibility = ShowStatusText ? Visibility.Visible : Visibility.Collapsed;
                    break;
            }
        }

        private void UpdateAudioBars(double level)
        {
            // Scale bar heights based on audio level
            var baseHeight = 8.0;
            var maxHeight = 48.0;
            var range = maxHeight - baseHeight;

            // Apply level with some variation for visual interest
            Bar1.Height = baseHeight + (range * level * 0.6);
            Bar2.Height = baseHeight + (range * level * 0.8);
            Bar3.Height = baseHeight + (range * level * 1.0);
            Bar4.Height = baseHeight + (range * level * 0.8);
            Bar5.Height = baseHeight + (range * level * 0.6);
        }

        /// <summary>
        /// Starts listening animation.
        /// </summary>
        public void StartListening()
        {
            State = VoiceState.Listening;
        }

        /// <summary>
        /// Shows speaking detected state.
        /// </summary>
        public void SetSpeaking()
        {
            State = VoiceState.Speaking;
        }

        /// <summary>
        /// Shows processing state.
        /// </summary>
        public void SetProcessing()
        {
            State = VoiceState.Processing;
        }

        /// <summary>
        /// Returns to idle state.
        /// </summary>
        public void Stop()
        {
            State = VoiceState.Idle;
        }

        /// <summary>
        /// Shows error state.
        /// </summary>
        public void ShowError()
        {
            State = VoiceState.Error;
        }
    }

    /// <summary>
    /// Voice input states.
    /// </summary>
    public enum VoiceState
    {
        /// <summary>Not listening.</summary>
        Idle,

        /// <summary>Listening for speech.</summary>
        Listening,

        /// <summary>Speech detected.</summary>
        Speaking,

        /// <summary>Processing transcription.</summary>
        Processing,

        /// <summary>Error occurred.</summary>
        Error
    }
}
