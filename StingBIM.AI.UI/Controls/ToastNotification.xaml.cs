// StingBIM.AI.UI.Controls.ToastNotification
// Toast notification control for displaying alerts and status updates
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StingBIM.AI.UI.Services;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// A toast notification control that displays alerts with auto-dismiss functionality.
    /// </summary>
    public partial class ToastNotification : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty NotificationIdProperty =
            DependencyProperty.Register(
                nameof(NotificationId),
                typeof(string),
                typeof(ToastNotification),
                new PropertyMetadata(null));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(ToastNotification),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(
                nameof(Message),
                typeof(string),
                typeof(ToastNotification),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty NotificationTypeProperty =
            DependencyProperty.Register(
                nameof(NotificationType),
                typeof(NotificationType),
                typeof(ToastNotification),
                new PropertyMetadata(NotificationType.Info, OnNotificationTypeChanged));

        public static readonly DependencyProperty IconPathProperty =
            DependencyProperty.Register(
                nameof(IconPath),
                typeof(string),
                typeof(ToastNotification),
                new PropertyMetadata(GetDefaultIconPath(NotificationType.Info)));

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register(
                nameof(Progress),
                typeof(int),
                typeof(ToastNotification),
                new PropertyMetadata(0, OnProgressChanged));

        #endregion

        #region Properties

        /// <summary>
        /// The notification ID.
        /// </summary>
        public string NotificationId
        {
            get => (string)GetValue(NotificationIdProperty);
            set => SetValue(NotificationIdProperty, value);
        }

        /// <summary>
        /// The notification title.
        /// </summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>
        /// The notification message.
        /// </summary>
        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        /// <summary>
        /// The notification type (Info, Success, Warning, Error, Progress).
        /// </summary>
        public NotificationType NotificationType
        {
            get => (NotificationType)GetValue(NotificationTypeProperty);
            set => SetValue(NotificationTypeProperty, value);
        }

        /// <summary>
        /// The icon path data.
        /// </summary>
        public string IconPath
        {
            get => (string)GetValue(IconPathProperty);
            set => SetValue(IconPathProperty, value);
        }

        /// <summary>
        /// The progress value (0-100) for progress notifications.
        /// </summary>
        public int Progress
        {
            get => (int)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        #endregion

        /// <summary>
        /// Event fired when the notification requests dismissal.
        /// </summary>
        public event EventHandler DismissRequested;

        private Storyboard _spinAnimation;

        public ToastNotification()
        {
            InitializeComponent();
            UpdateTypeAppearance();
        }

        /// <summary>
        /// Binds this control to a Notification object.
        /// </summary>
        public void BindToNotification(Notification notification)
        {
            NotificationId = notification.Id;
            Title = notification.Title;
            Message = notification.Message;
            NotificationType = notification.Type;
            IconPath = notification.IconPath;

            // Set up actions
            if (notification.Actions?.Count > 0)
            {
                ActionsContainer.Visibility = Visibility.Visible;
                ActionsContainer.Items.Clear();

                foreach (var action in notification.Actions)
                {
                    var button = new Button
                    {
                        Content = action.Label,
                        Tag = action.Id,
                        Style = action.IsPrimary
                            ? (Style)Resources["PrimaryActionButtonStyle"]
                            : (Style)Resources["ActionButtonStyle"]
                    };
                    button.Click += ActionButton_Click;
                    ActionsContainer.Items.Add(button);
                }
            }
            else
            {
                ActionsContainer.Visibility = Visibility.Collapsed;
            }

            // Handle progress notifications
            if (notification is ProgressNotification progressNotif)
            {
                Progress = progressNotif.Progress;
                progressNotif.ProgressChanged += OnProgressNotificationChanged;
            }
        }

        /// <summary>
        /// Plays the show animation.
        /// </summary>
        public void AnimateIn()
        {
            var storyboard = (Storyboard)Resources["SlideInAnimation"];
            storyboard.Begin(ToastBorder);
        }

        /// <summary>
        /// Plays the dismiss animation.
        /// </summary>
        public void AnimateOut(Action onComplete = null)
        {
            var storyboard = (Storyboard)Resources["SlideOutAnimation"];
            if (onComplete != null)
            {
                EventHandler handler = null;
                handler = (s, e) =>
                {
                    storyboard.Completed -= handler;
                    onComplete();
                };
                storyboard.Completed += handler;
            }
            storyboard.Begin(ToastBorder);
        }

        private void OnProgressNotificationChanged(object sender, int progress)
        {
            Dispatcher.InvokeAsync(() =>
            {
                Progress = progress;
            });
        }

        private static void OnNotificationTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ToastNotification toast)
            {
                toast.UpdateTypeAppearance();
            }
        }

        private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ToastNotification toast)
            {
                toast.UpdateProgress();
            }
        }

        private void UpdateTypeAppearance()
        {
            var (brush, icon) = NotificationType switch
            {
                NotificationType.Success => (FindBrush("SuccessAccentBrush"), GetDefaultIconPath(NotificationType.Success)),
                NotificationType.Warning => (FindBrush("WarningAccentBrush"), GetDefaultIconPath(NotificationType.Warning)),
                NotificationType.Error => (FindBrush("ErrorAccentBrush"), GetDefaultIconPath(NotificationType.Error)),
                NotificationType.Progress => (FindBrush("ProgressAccentBrush"), GetDefaultIconPath(NotificationType.Progress)),
                _ => (FindBrush("InfoAccentBrush"), GetDefaultIconPath(NotificationType.Info))
            };

            TypeIndicator.Background = brush;
            IconPathElement.Fill = brush;
            IconPath = icon;

            // Handle progress-specific UI
            if (NotificationType == NotificationType.Progress)
            {
                ProgressContainer.Visibility = Visibility.Visible;
                IconPathElement.Visibility = Visibility.Collapsed;
                ProgressIcon.Visibility = Visibility.Visible;
                StartSpinAnimation();
            }
            else
            {
                ProgressContainer.Visibility = Visibility.Collapsed;
                IconPathElement.Visibility = Visibility.Visible;
                ProgressIcon.Visibility = Visibility.Collapsed;
                StopSpinAnimation();
            }
        }

        private void UpdateProgress()
        {
            ProgressBar.Value = Progress;
            ProgressText.Text = $"{Progress}%";

            // Change appearance when complete
            if (Progress >= 100)
            {
                StopSpinAnimation();
                ProgressIcon.Visibility = Visibility.Collapsed;
                IconPathElement.Visibility = Visibility.Visible;

                var successBrush = FindBrush("SuccessAccentBrush");
                TypeIndicator.Background = successBrush;
                IconPathElement.Fill = successBrush;
                IconPath = GetDefaultIconPath(NotificationType.Success);
            }
        }

        private void StartSpinAnimation()
        {
            if (_spinAnimation != null)
                return;

            _spinAnimation = new Storyboard();
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(1),
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(animation, ProgressIcon);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            _spinAnimation.Children.Add(animation);
            _spinAnimation.Begin();
        }

        private void StopSpinAnimation()
        {
            _spinAnimation?.Stop();
            _spinAnimation = null;
        }

        private Brush FindBrush(string key)
        {
            return Resources.Contains(key)
                ? (Brush)Resources[key]
                : new SolidColorBrush(Colors.Gray);
        }

        private static string GetDefaultIconPath(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => "M9,20.42L2.79,14.21L5.62,11.38L9,14.77L18.88,4.88L21.71,7.71L9,20.42Z",
                NotificationType.Warning => "M13,14H11V10H13M13,18H11V16H13M1,21H23L12,2L1,21Z",
                NotificationType.Error => "M12,2C17.53,2 22,6.47 22,12C22,17.53 17.53,22 12,22C6.47,22 2,17.53 2,12C2,6.47 6.47,2 12,2M15.59,7L12,10.59L8.41,7L7,8.41L10.59,12L7,15.59L8.41,17L12,13.41L15.59,17L17,15.59L13.41,12L17,8.41L15.59,7Z",
                NotificationType.Progress => "M12,4V2A10,10 0 0,0 2,12H4A8,8 0 0,1 12,4Z",
                _ => "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4M11,17V15H13V17H11M11,13V7H13V13H11Z"
            };
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            DismissRequested?.Invoke(this, EventArgs.Empty);
            NotificationService.Instance.Dismiss(NotificationId);
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string actionId)
            {
                NotificationService.Instance.TriggerAction(NotificationId, actionId);
            }
        }
    }
}
