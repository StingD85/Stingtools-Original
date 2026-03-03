// StingBIM.AI.UI.Controls.NotificationHost
// Container control for displaying toast notifications
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using NLog;
using StingBIM.AI.UI.Services;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// A host control that manages the display of toast notifications.
    /// Place this control at the top-right corner of your window.
    /// </summary>
    public partial class NotificationHost : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, ToastNotification> _activeToasts = new();

        /// <summary>
        /// Gets or sets the position of the notification host.
        /// </summary>
        public NotificationPosition Position
        {
            get => (NotificationPosition)GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        public static readonly DependencyProperty PositionProperty =
            DependencyProperty.Register(
                nameof(Position),
                typeof(NotificationPosition),
                typeof(NotificationHost),
                new PropertyMetadata(NotificationPosition.TopRight, OnPositionChanged));

        public NotificationHost()
        {
            InitializeComponent();
            SubscribeToNotificationService();
        }

        private void SubscribeToNotificationService()
        {
            var service = NotificationService.Instance;
            service.NotificationShown += OnNotificationShown;
            service.NotificationDismissed += OnNotificationDismissed;

            Unloaded += (s, e) =>
            {
                service.NotificationShown -= OnNotificationShown;
                service.NotificationDismissed -= OnNotificationDismissed;
            };

            Logger.Debug("NotificationHost subscribed to NotificationService");
        }

        private void OnNotificationShown(object sender, NotificationEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var toast = new ToastNotification();
                    toast.BindToNotification(e.Notification);
                    toast.DismissRequested += OnToastDismissRequested;

                    _activeToasts[e.Notification.Id] = toast;
                    NotificationStack.Children.Insert(0, toast);

                    toast.AnimateIn();
                    Logger.Debug($"Toast notification displayed: {e.Notification.Id}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to display notification");
                }
            });
        }

        private void OnNotificationDismissed(object sender, NotificationEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_activeToasts.TryGetValue(e.Notification.Id, out var toast))
                {
                    toast.AnimateOut(() =>
                    {
                        NotificationStack.Children.Remove(toast);
                        _activeToasts.Remove(e.Notification.Id);
                        toast.DismissRequested -= OnToastDismissRequested;
                        Logger.Debug($"Toast notification removed: {e.Notification.Id}");
                    });
                }
            });
        }

        private void OnToastDismissRequested(object sender, EventArgs e)
        {
            // The toast will handle calling NotificationService.Dismiss
        }

        private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NotificationHost host)
            {
                host.UpdatePosition();
            }
        }

        private void UpdatePosition()
        {
            switch (Position)
            {
                case NotificationPosition.TopLeft:
                    HorizontalAlignment = HorizontalAlignment.Left;
                    VerticalAlignment = VerticalAlignment.Top;
                    Margin = new Thickness(16, 16, 0, 0);
                    break;
                case NotificationPosition.TopRight:
                    HorizontalAlignment = HorizontalAlignment.Right;
                    VerticalAlignment = VerticalAlignment.Top;
                    Margin = new Thickness(0, 16, 16, 0);
                    break;
                case NotificationPosition.BottomLeft:
                    HorizontalAlignment = HorizontalAlignment.Left;
                    VerticalAlignment = VerticalAlignment.Bottom;
                    Margin = new Thickness(16, 0, 0, 16);
                    break;
                case NotificationPosition.BottomRight:
                    HorizontalAlignment = HorizontalAlignment.Right;
                    VerticalAlignment = VerticalAlignment.Bottom;
                    Margin = new Thickness(0, 0, 16, 16);
                    break;
            }
        }
    }

    /// <summary>
    /// Position options for the notification host.
    /// </summary>
    public enum NotificationPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
}
