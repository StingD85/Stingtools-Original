// StingBIM.AI.UI.Controls.MessageBubble
// Chat message bubble control with different styles for user/assistant/error/system
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// A chat message bubble that displays differently based on sender type.
    /// Supports user, assistant, error (with expandable details), and system messages.
    /// </summary>
    public partial class MessageBubble : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(MessageBubble),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IsUserProperty =
            DependencyProperty.Register(
                nameof(IsUser),
                typeof(bool),
                typeof(MessageBubble),
                new PropertyMetadata(false, OnMessageTypeChanged));

        public static readonly DependencyProperty IsErrorProperty =
            DependencyProperty.Register(
                nameof(IsError),
                typeof(bool),
                typeof(MessageBubble),
                new PropertyMetadata(false, OnMessageTypeChanged));

        public static readonly DependencyProperty IsSystemProperty =
            DependencyProperty.Register(
                nameof(IsSystem),
                typeof(bool),
                typeof(MessageBubble),
                new PropertyMetadata(false, OnMessageTypeChanged));

        public static readonly DependencyProperty TimestampProperty =
            DependencyProperty.Register(
                nameof(Timestamp),
                typeof(DateTime),
                typeof(MessageBubble),
                new PropertyMetadata(DateTime.Now, OnTimestampChanged));

        public static readonly DependencyProperty ShowTimestampProperty =
            DependencyProperty.Register(
                nameof(ShowTimestamp),
                typeof(bool),
                typeof(MessageBubble),
                new PropertyMetadata(true, OnShowTimestampChanged));

        public static readonly DependencyProperty ErrorCodeProperty =
            DependencyProperty.Register(
                nameof(ErrorCode),
                typeof(string),
                typeof(MessageBubble),
                new PropertyMetadata(null, OnErrorDetailsChanged));

        public static readonly DependencyProperty ErrorDetailsProperty =
            DependencyProperty.Register(
                nameof(ErrorDetails),
                typeof(string),
                typeof(MessageBubble),
                new PropertyMetadata(null, OnErrorDetailsChanged));

        public static readonly DependencyProperty MessageIdProperty =
            DependencyProperty.Register(
                nameof(MessageId),
                typeof(string),
                typeof(MessageBubble),
                new PropertyMetadata(null));

        public static readonly DependencyProperty FeedbackGivenProperty =
            DependencyProperty.Register(
                nameof(FeedbackGiven),
                typeof(FeedbackType),
                typeof(MessageBubble),
                new PropertyMetadata(FeedbackType.None));

        public static readonly DependencyProperty ShowActionsProperty =
            DependencyProperty.Register(
                nameof(ShowActions),
                typeof(bool),
                typeof(MessageBubble),
                new PropertyMetadata(true));

        #endregion

        #region Events

        /// <summary>
        /// Fired when the copy button is clicked.
        /// </summary>
        public event EventHandler CopyClicked;

        /// <summary>
        /// Fired when the retry button is clicked.
        /// </summary>
        public event EventHandler RetryClicked;

        /// <summary>
        /// Fired when feedback is given (thumbs up/down).
        /// </summary>
        public event EventHandler<FeedbackEventArgs> FeedbackGiven;

        #endregion

        #region Properties

        /// <summary>
        /// The message text.
        /// </summary>
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>
        /// Whether this is a user message (true) or assistant message (false).
        /// </summary>
        public bool IsUser
        {
            get => (bool)GetValue(IsUserProperty);
            set => SetValue(IsUserProperty, value);
        }

        /// <summary>
        /// Whether this is an error message.
        /// </summary>
        public bool IsError
        {
            get => (bool)GetValue(IsErrorProperty);
            set => SetValue(IsErrorProperty, value);
        }

        /// <summary>
        /// The message timestamp.
        /// </summary>
        public DateTime Timestamp
        {
            get => (DateTime)GetValue(TimestampProperty);
            set => SetValue(TimestampProperty, value);
        }

        /// <summary>
        /// Whether to show the timestamp.
        /// </summary>
        public bool ShowTimestamp
        {
            get => (bool)GetValue(ShowTimestampProperty);
            set => SetValue(ShowTimestampProperty, value);
        }

        /// <summary>
        /// Whether this is a system message.
        /// </summary>
        public bool IsSystem
        {
            get => (bool)GetValue(IsSystemProperty);
            set => SetValue(IsSystemProperty, value);
        }

        /// <summary>
        /// The error code (e.g., "TIMEOUT", "INVALID_INPUT").
        /// </summary>
        public string ErrorCode
        {
            get => (string)GetValue(ErrorCodeProperty);
            set => SetValue(ErrorCodeProperty, value);
        }

        /// <summary>
        /// Detailed error information (shown in expandable section).
        /// </summary>
        public string ErrorDetails
        {
            get => (string)GetValue(ErrorDetailsProperty);
            set => SetValue(ErrorDetailsProperty, value);
        }

        /// <summary>
        /// Unique identifier for this message.
        /// </summary>
        public string MessageId
        {
            get => (string)GetValue(MessageIdProperty);
            set => SetValue(MessageIdProperty, value);
        }

        /// <summary>
        /// The type of feedback given for this message.
        /// </summary>
        public FeedbackType Feedback
        {
            get => (FeedbackType)GetValue(FeedbackGivenProperty);
            set => SetValue(FeedbackGivenProperty, value);
        }

        /// <summary>
        /// Whether to show action buttons on hover.
        /// </summary>
        public bool ShowActions
        {
            get => (bool)GetValue(ShowActionsProperty);
            set => SetValue(ShowActionsProperty, value);
        }

        #endregion

        #region Responsive Layout Constants

        /// <summary>
        /// The percentage of container width that message bubbles should use (0.0 to 1.0).
        /// </summary>
        private const double BubbleWidthRatio = 0.75;

        /// <summary>
        /// Minimum width for message bubbles in pixels.
        /// </summary>
        private const double MinBubbleWidth = 200;

        /// <summary>
        /// Maximum width for message bubbles in pixels (for very wide screens).
        /// </summary>
        private const double MaxBubbleWidth = 800;

        #endregion

        public MessageBubble()
        {
            InitializeComponent();
            UpdateVisibility();

            // Setup hover behavior for action buttons
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;

            // Set initial bubble widths
            Loaded += (s, e) => UpdateBubbleMaxWidths();
        }

        /// <summary>
        /// Handles size changes to update bubble widths responsively.
        /// </summary>
        private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateBubbleMaxWidths();
        }

        /// <summary>
        /// Updates the MaxWidth of all message bubbles based on the current container width.
        /// This makes paragraphs elongate (use fewer lines) when the UI is expanded.
        /// </summary>
        private void UpdateBubbleMaxWidths()
        {
            // Calculate the responsive width based on actual width
            var containerWidth = ActualWidth > 0 ? ActualWidth : 400; // Default fallback
            var calculatedWidth = containerWidth * BubbleWidthRatio;

            // Clamp to min/max bounds
            var bubbleMaxWidth = Math.Max(MinBubbleWidth, Math.Min(MaxBubbleWidth, calculatedWidth));

            // Apply to all bubble types
            UserBubble.MaxWidth = bubbleMaxWidth;

            if (AssistantBubble != null)
                AssistantBubble.MaxWidth = bubbleMaxWidth;

            if (ErrorBubble != null)
                ErrorBubble.MaxWidth = bubbleMaxWidth;

            if (SystemBubble != null)
                SystemBubble.MaxWidth = bubbleMaxWidth * 0.9; // System messages slightly narrower
        }

        private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!IsUser && !IsSystem && !IsError && ShowActions)
            {
                ShowActionsPanel();
            }
        }

        private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!IsUser && !IsSystem && !IsError)
            {
                HideActionsPanel();
            }
        }

        private void ShowActionsPanel()
        {
            var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            ActionsPanel.BeginAnimation(OpacityProperty, animation);
        }

        private void HideActionsPanel()
        {
            var animation = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            ActionsPanel.BeginAnimation(OpacityProperty, animation);
        }

        private static void OnMessageTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MessageBubble bubble)
            {
                bubble.UpdateVisibility();
            }
        }

        private static void OnTimestampChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MessageBubble bubble)
            {
                bubble.UpdateTimestamp();
            }
        }

        private static void OnShowTimestampChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MessageBubble bubble)
            {
                bubble.UpdateTimestamp();
            }
        }

        private static void OnErrorDetailsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MessageBubble bubble)
            {
                bubble.UpdateErrorDetails();
            }
        }

        private void UpdateVisibility()
        {
            // Hide all containers first
            UserBubble.Visibility = Visibility.Collapsed;
            AssistantContainer.Visibility = Visibility.Collapsed;
            ErrorContainer.Visibility = Visibility.Collapsed;
            SystemContainer.Visibility = Visibility.Collapsed;

            if (IsError)
            {
                ErrorContainer.Visibility = Visibility.Visible;
                UpdateErrorDetails();
            }
            else if (IsSystem)
            {
                SystemContainer.Visibility = Visibility.Visible;
            }
            else if (IsUser)
            {
                UserBubble.Visibility = Visibility.Visible;
            }
            else
            {
                AssistantContainer.Visibility = Visibility.Visible;
            }
        }

        private void UpdateTimestamp()
        {
            if (ShowTimestamp && !IsUser && !IsSystem)
            {
                var timeStr = Timestamp.ToString("HH:mm");
                TimestampText.Text = timeStr;
                TimestampText.Visibility = Visibility.Visible;

                if (IsError)
                {
                    ErrorTimestampText.Text = timeStr;
                    ErrorTimestampText.Visibility = Visibility.Visible;
                }
            }
            else
            {
                TimestampText.Visibility = Visibility.Collapsed;
                ErrorTimestampText.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateErrorDetails()
        {
            if (!IsError)
                return;

            // Update error code badge
            if (!string.IsNullOrEmpty(ErrorCode))
            {
                ErrorCodeText.Text = ErrorCode;
                ErrorCodeBadge.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorCodeBadge.Visibility = Visibility.Collapsed;
            }

            // Update expandable details section
            if (!string.IsNullOrEmpty(ErrorDetails))
            {
                ErrorDetailsText.Text = ErrorDetails;
                DetailsExpander.Visibility = Visibility.Visible;
            }
            else
            {
                DetailsExpander.Visibility = Visibility.Collapsed;
            }
        }

        private void CopyErrorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var errorText = Text;

                if (!string.IsNullOrEmpty(ErrorCode))
                {
                    errorText = $"[{ErrorCode}] {errorText}";
                }

                if (!string.IsNullOrEmpty(ErrorDetails))
                {
                    errorText += $"\n\nDetails:\n{ErrorDetails}";
                }

                Clipboard.SetText(errorText);

                // Brief visual feedback (could be enhanced with a tooltip)
                CopyErrorButton.ToolTip = "Copied!";
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, args) =>
                {
                    CopyErrorButton.ToolTip = "Copy error details";
                    timer.Stop();
                };
                timer.Start();
            }
            catch
            {
                // Clipboard operations can fail silently
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(Text);
                CopyClicked?.Invoke(this, EventArgs.Empty);

                // Brief visual feedback via notification
                Services.NotificationService.Instance.ShowSuccess("Copied", "Message copied to clipboard", TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Clipboard operations can fail silently
            }
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            RetryClicked?.Invoke(this, EventArgs.Empty);
        }

        private void ThumbsUpButton_Click(object sender, RoutedEventArgs e)
        {
            Feedback = FeedbackType.Positive;
            FeedbackGiven?.Invoke(this, new FeedbackEventArgs(MessageId, FeedbackType.Positive));
        }

        private void ThumbsDownButton_Click(object sender, RoutedEventArgs e)
        {
            Feedback = FeedbackType.Negative;
            FeedbackGiven?.Invoke(this, new FeedbackEventArgs(MessageId, FeedbackType.Negative));
        }
    }

    /// <summary>
    /// Types of message feedback.
    /// </summary>
    public enum FeedbackType
    {
        None,
        Positive,
        Negative
    }

    /// <summary>
    /// Event arguments for feedback events.
    /// </summary>
    public class FeedbackEventArgs : EventArgs
    {
        public string MessageId { get; }
        public FeedbackType Feedback { get; }

        public FeedbackEventArgs(string messageId, FeedbackType feedback)
        {
            MessageId = messageId;
            Feedback = feedback;
        }
    }
}
