// StingBIM.AI.UI.Controls.TypingIndicator
// Animated typing indicator showing AI processing state
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Animated typing indicator that shows when the AI is processing.
    /// Displays bouncing dots and rotating status messages.
    /// </summary>
    public partial class TypingIndicator : UserControl
    {
        private Storyboard _bounceAnimation;
        private Storyboard _statusAnimation;
        private DispatcherTimer _statusRotationTimer;
        private int _currentStatusIndex;

        private static readonly string[] DefaultStatusMessages = new[]
        {
            "Thinking...",
            "Analyzing your request...",
            "Processing...",
            "Understanding context...",
            "Generating response..."
        };

        #region Dependency Properties

        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(
                nameof(IsActive),
                typeof(bool),
                typeof(TypingIndicator),
                new PropertyMetadata(false, OnIsActiveChanged));

        public static readonly DependencyProperty StatusMessageProperty =
            DependencyProperty.Register(
                nameof(StatusMessage),
                typeof(string),
                typeof(TypingIndicator),
                new PropertyMetadata("Thinking..."));

        public static readonly DependencyProperty RotateMessagesProperty =
            DependencyProperty.Register(
                nameof(RotateMessages),
                typeof(bool),
                typeof(TypingIndicator),
                new PropertyMetadata(true));

        public static readonly DependencyProperty CustomMessagesProperty =
            DependencyProperty.Register(
                nameof(CustomMessages),
                typeof(string[]),
                typeof(TypingIndicator),
                new PropertyMetadata(null));

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether the indicator is active (animating).
        /// </summary>
        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        /// <summary>
        /// Gets or sets the current status message.
        /// </summary>
        public string StatusMessage
        {
            get => (string)GetValue(StatusMessageProperty);
            set => SetValue(StatusMessageProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to rotate through status messages automatically.
        /// </summary>
        public bool RotateMessages
        {
            get => (bool)GetValue(RotateMessagesProperty);
            set => SetValue(RotateMessagesProperty, value);
        }

        /// <summary>
        /// Gets or sets custom status messages to rotate through.
        /// </summary>
        public string[] CustomMessages
        {
            get => (string[])GetValue(CustomMessagesProperty);
            set => SetValue(CustomMessagesProperty, value);
        }

        #endregion

        public TypingIndicator()
        {
            InitializeComponent();
            InitializeAnimations();
            Visibility = Visibility.Collapsed;
        }

        private void InitializeAnimations()
        {
            _bounceAnimation = (Storyboard)Resources["BounceAnimation"];
            _statusAnimation = (Storyboard)Resources["StatusFadeAnimation"];

            _statusRotationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.5)
            };
            _statusRotationTimer.Tick += OnStatusRotationTick;
        }

        private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TypingIndicator indicator)
            {
                if ((bool)e.NewValue)
                {
                    indicator.Start();
                }
                else
                {
                    indicator.Stop();
                }
            }
        }

        /// <summary>
        /// Starts the typing indicator animation.
        /// </summary>
        public void Start()
        {
            Visibility = Visibility.Visible;
            _currentStatusIndex = 0;

            var messages = CustomMessages ?? DefaultStatusMessages;
            if (messages.Length > 0)
            {
                StatusMessage = messages[0];
            }

            _bounceAnimation?.Begin();
            _statusAnimation?.Begin();

            if (RotateMessages)
            {
                _statusRotationTimer.Start();
            }
        }

        /// <summary>
        /// Stops the typing indicator animation.
        /// </summary>
        public void Stop()
        {
            _bounceAnimation?.Stop();
            _statusAnimation?.Stop();
            _statusRotationTimer.Stop();
            Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Sets a specific status message without rotation.
        /// </summary>
        public void SetStatus(string message)
        {
            _statusRotationTimer.Stop();
            StatusMessage = message;
        }

        /// <summary>
        /// Sets status messages for specific processing phases.
        /// </summary>
        public void SetPhase(ProcessingPhase phase)
        {
            StatusMessage = phase switch
            {
                ProcessingPhase.Understanding => "Understanding your request...",
                ProcessingPhase.Analyzing => "Analyzing context...",
                ProcessingPhase.Reasoning => "Reasoning about the best approach...",
                ProcessingPhase.Generating => "Generating response...",
                ProcessingPhase.Validating => "Validating result...",
                ProcessingPhase.Executing => "Executing command...",
                _ => "Processing..."
            };
        }

        private void OnStatusRotationTick(object sender, EventArgs e)
        {
            var messages = CustomMessages ?? DefaultStatusMessages;
            if (messages.Length == 0) return;

            _currentStatusIndex = (_currentStatusIndex + 1) % messages.Length;
            StatusMessage = messages[_currentStatusIndex];
        }
    }

    /// <summary>
    /// Processing phases for the typing indicator.
    /// </summary>
    public enum ProcessingPhase
    {
        Understanding,
        Analyzing,
        Reasoning,
        Generating,
        Validating,
        Executing
    }
}
