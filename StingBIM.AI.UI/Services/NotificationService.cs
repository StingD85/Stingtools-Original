// StingBIM.AI.UI.Services.NotificationService
// Manages toast notifications for background task completion and alerts
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using NLog;

namespace StingBIM.AI.UI.Services
{
    /// <summary>
    /// Manages toast notifications for the application.
    /// Provides a centralized notification system for background tasks and alerts.
    /// </summary>
    public class NotificationService
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static NotificationService _instance;
        private static readonly object _lock = new object();

        private readonly ObservableCollection<Notification> _activeNotifications;
        private readonly Queue<Notification> _notificationQueue;
        private readonly int _maxVisibleNotifications;
        private readonly TimeSpan _defaultDuration;
        private readonly Dispatcher _dispatcher;

        /// <summary>
        /// Gets the singleton instance of the NotificationService.
        /// </summary>
        public static NotificationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new NotificationService();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Event fired when a new notification is shown.
        /// </summary>
        public event EventHandler<NotificationEventArgs> NotificationShown;

        /// <summary>
        /// Event fired when a notification is dismissed.
        /// </summary>
        public event EventHandler<NotificationEventArgs> NotificationDismissed;

        /// <summary>
        /// Event fired when a notification action is clicked.
        /// </summary>
        public event EventHandler<NotificationActionEventArgs> NotificationActionClicked;

        /// <summary>
        /// Gets the collection of active notifications.
        /// </summary>
        public IReadOnlyCollection<Notification> ActiveNotifications => _activeNotifications;

        /// <summary>
        /// Gets the total number of pending notifications (including queued).
        /// </summary>
        public int PendingCount => _activeNotifications.Count + _notificationQueue.Count;

        private NotificationService(int maxVisible = 3, int defaultDurationSeconds = 5)
        {
            _maxVisibleNotifications = maxVisible;
            _defaultDuration = TimeSpan.FromSeconds(defaultDurationSeconds);
            _activeNotifications = new ObservableCollection<Notification>();
            _notificationQueue = new Queue<Notification>();
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            Logger.Info("NotificationService initialized");
        }

        /// <summary>
        /// Shows a notification with the specified parameters.
        /// </summary>
        /// <param name="title">The notification title.</param>
        /// <param name="message">The notification message.</param>
        /// <param name="type">The notification type.</param>
        /// <param name="duration">Optional duration (null for persistent).</param>
        /// <param name="actions">Optional action buttons.</param>
        /// <returns>The notification ID.</returns>
        public string Show(
            string title,
            string message,
            NotificationType type = NotificationType.Info,
            TimeSpan? duration = null,
            params NotificationAction[] actions)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title,
                Message = message,
                Type = type,
                Duration = duration ?? _defaultDuration,
                CreatedAt = DateTime.Now,
                Actions = actions?.ToList() ?? new List<NotificationAction>()
            };

            EnqueueNotification(notification);
            return notification.Id;
        }

        /// <summary>
        /// Shows a success notification.
        /// </summary>
        public string ShowSuccess(string title, string message, TimeSpan? duration = null)
        {
            return Show(title, message, NotificationType.Success, duration);
        }

        /// <summary>
        /// Shows an error notification.
        /// </summary>
        public string ShowError(string title, string message, TimeSpan? duration = null)
        {
            // Errors stay longer by default
            return Show(title, message, NotificationType.Error, duration ?? TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Shows a warning notification.
        /// </summary>
        public string ShowWarning(string title, string message, TimeSpan? duration = null)
        {
            return Show(title, message, NotificationType.Warning, duration ?? TimeSpan.FromSeconds(7));
        }

        /// <summary>
        /// Shows an info notification.
        /// </summary>
        public string ShowInfo(string title, string message, TimeSpan? duration = null)
        {
            return Show(title, message, NotificationType.Info, duration);
        }

        /// <summary>
        /// Shows a task completion notification.
        /// </summary>
        public string ShowTaskComplete(string taskName, bool success, string details = null)
        {
            var type = success ? NotificationType.Success : NotificationType.Error;
            var title = success ? "Task Complete" : "Task Failed";
            var message = success
                ? $"{taskName} completed successfully"
                : $"{taskName} failed{(details != null ? $": {details}" : "")}";

            return Show(title, message, type);
        }

        /// <summary>
        /// Shows a progress notification that can be updated.
        /// </summary>
        public ProgressNotification ShowProgress(string title, string message)
        {
            var notification = new ProgressNotification
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title,
                Message = message,
                Type = NotificationType.Progress,
                Duration = TimeSpan.Zero, // Persistent until dismissed
                CreatedAt = DateTime.Now,
                Progress = 0
            };

            EnqueueNotification(notification);
            return notification;
        }

        /// <summary>
        /// Updates a progress notification.
        /// </summary>
        public void UpdateProgress(string notificationId, int progress, string message = null)
        {
            _dispatcher.InvokeAsync(() =>
            {
                var notification = _activeNotifications.FirstOrDefault(n => n.Id == notificationId);
                if (notification is ProgressNotification progressNotif)
                {
                    progressNotif.Progress = Math.Clamp(progress, 0, 100);
                    if (message != null)
                    {
                        progressNotif.Message = message;
                    }

                    if (progress >= 100)
                    {
                        progressNotif.Type = NotificationType.Success;
                        progressNotif.Duration = _defaultDuration;
                        StartDismissTimer(progressNotif);
                    }
                }
            });
        }

        /// <summary>
        /// Dismisses a specific notification.
        /// </summary>
        public void Dismiss(string notificationId)
        {
            _dispatcher.InvokeAsync(() =>
            {
                var notification = _activeNotifications.FirstOrDefault(n => n.Id == notificationId);
                if (notification != null)
                {
                    RemoveNotification(notification);
                }
            });
        }

        /// <summary>
        /// Dismisses all active notifications.
        /// </summary>
        public void DismissAll()
        {
            _dispatcher.InvokeAsync(() =>
            {
                var notifications = _activeNotifications.ToList();
                foreach (var notification in notifications)
                {
                    RemoveNotification(notification);
                }
                _notificationQueue.Clear();
            });
        }

        /// <summary>
        /// Triggers an action on a notification.
        /// </summary>
        internal void TriggerAction(string notificationId, string actionId)
        {
            var notification = _activeNotifications.FirstOrDefault(n => n.Id == notificationId);
            var action = notification?.Actions.FirstOrDefault(a => a.Id == actionId);

            if (action != null)
            {
                try
                {
                    action.Callback?.Invoke();
                    NotificationActionClicked?.Invoke(this, new NotificationActionEventArgs(notification, action));
                    Logger.Debug($"Notification action triggered: {actionId}");

                    if (action.DismissOnClick)
                    {
                        Dismiss(notificationId);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Error executing notification action: {actionId}");
                }
            }
        }

        private void EnqueueNotification(Notification notification)
        {
            _dispatcher.InvokeAsync(() =>
            {
                if (_activeNotifications.Count >= _maxVisibleNotifications)
                {
                    _notificationQueue.Enqueue(notification);
                    Logger.Debug($"Notification queued: {notification.Id}");
                }
                else
                {
                    ShowNotificationInternal(notification);
                }
            });
        }

        private void ShowNotificationInternal(Notification notification)
        {
            _activeNotifications.Add(notification);
            notification.IsVisible = true;

            Logger.Debug($"Notification shown: {notification.Type} - {notification.Title}");
            NotificationShown?.Invoke(this, new NotificationEventArgs(notification));

            if (notification.Duration > TimeSpan.Zero && notification.Type != NotificationType.Progress)
            {
                StartDismissTimer(notification);
            }
        }

        private void StartDismissTimer(Notification notification)
        {
            var timer = new DispatcherTimer
            {
                Interval = notification.Duration
            };

            timer.Tick += (s, e) =>
            {
                timer.Stop();
                RemoveNotification(notification);
            };

            notification.DismissTimer = timer;
            timer.Start();
        }

        private void RemoveNotification(Notification notification)
        {
            notification.DismissTimer?.Stop();
            notification.IsVisible = false;
            _activeNotifications.Remove(notification);

            Logger.Debug($"Notification dismissed: {notification.Id}");
            NotificationDismissed?.Invoke(this, new NotificationEventArgs(notification));

            // Show next queued notification
            if (_notificationQueue.Count > 0 && _activeNotifications.Count < _maxVisibleNotifications)
            {
                var next = _notificationQueue.Dequeue();
                ShowNotificationInternal(next);
            }
        }
    }

    /// <summary>
    /// Types of notifications.
    /// </summary>
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error,
        Progress
    }

    /// <summary>
    /// Represents a notification.
    /// </summary>
    public class Notification
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public NotificationType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsVisible { get; set; }
        public List<NotificationAction> Actions { get; set; } = new List<NotificationAction>();
        internal DispatcherTimer DismissTimer { get; set; }

        /// <summary>
        /// Gets the icon path based on notification type.
        /// </summary>
        public string IconPath => Type switch
        {
            NotificationType.Success => "M9,20.42L2.79,14.21L5.62,11.38L9,14.77L18.88,4.88L21.71,7.71L9,20.42Z",
            NotificationType.Warning => "M13,14H11V10H13M13,18H11V16H13M1,21H23L12,2L1,21Z",
            NotificationType.Error => "M12,2C17.53,2 22,6.47 22,12C22,17.53 17.53,22 12,22C6.47,22 2,17.53 2,12C2,6.47 6.47,2 12,2M15.59,7L12,10.59L8.41,7L7,8.41L10.59,12L7,15.59L8.41,17L12,13.41L15.59,17L17,15.59L13.41,12L17,8.41L15.59,7Z",
            NotificationType.Progress => "M12,4V2A10,10 0 0,0 2,12H4A8,8 0 0,1 12,4Z",
            _ => "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4M11,17V15H13V17H11M11,13V7H13V13H11Z"
        };
    }

    /// <summary>
    /// Represents a progress notification that can be updated.
    /// </summary>
    public class ProgressNotification : Notification
    {
        private int _progress;

        public int Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                ProgressChanged?.Invoke(this, _progress);
            }
        }

        public event EventHandler<int> ProgressChanged;
    }

    /// <summary>
    /// Represents an action button on a notification.
    /// </summary>
    public class NotificationAction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Label { get; set; }
        public Action Callback { get; set; }
        public bool DismissOnClick { get; set; } = true;
        public bool IsPrimary { get; set; }

        public NotificationAction() { }

        public NotificationAction(string label, Action callback, bool isPrimary = false)
        {
            Label = label;
            Callback = callback;
            IsPrimary = isPrimary;
        }
    }

    /// <summary>
    /// Event arguments for notification events.
    /// </summary>
    public class NotificationEventArgs : EventArgs
    {
        public Notification Notification { get; }

        public NotificationEventArgs(Notification notification)
        {
            Notification = notification;
        }
    }

    /// <summary>
    /// Event arguments for notification action events.
    /// </summary>
    public class NotificationActionEventArgs : NotificationEventArgs
    {
        public NotificationAction Action { get; }

        public NotificationActionEventArgs(Notification notification, NotificationAction action)
            : base(notification)
        {
            Action = action;
        }
    }
}
