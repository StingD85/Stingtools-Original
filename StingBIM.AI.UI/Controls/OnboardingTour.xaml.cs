// StingBIM.AI.UI.Controls.OnboardingTour
// Guided onboarding tour for new users
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NLog;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Interactive onboarding tour that guides users through the interface.
    /// </summary>
    public partial class OnboardingTour : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private List<TourStep> _steps;
        private int _currentStepIndex;
        private FrameworkElement _hostElement;
        private Dictionary<string, FrameworkElement> _targetElements;

        /// <summary>
        /// Event fired when the tour is completed.
        /// </summary>
        public event EventHandler TourCompleted;

        /// <summary>
        /// Event fired when the tour is skipped.
        /// </summary>
        public event EventHandler TourSkipped;

        /// <summary>
        /// Event fired when a step is shown.
        /// </summary>
        public event EventHandler<TourStep> StepShown;

        /// <summary>
        /// Gets whether the tour is currently active.
        /// </summary>
        public bool IsActive => OverlayGrid.Visibility == Visibility.Visible;

        public OnboardingTour()
        {
            InitializeComponent();
            _steps = new List<TourStep>();
            _targetElements = new Dictionary<string, FrameworkElement>();
            _currentStepIndex = 0;
        }

        #region Public Methods

        /// <summary>
        /// Registers a target element for highlighting during the tour.
        /// </summary>
        public void RegisterTarget(string key, FrameworkElement element)
        {
            _targetElements[key] = element;
        }

        /// <summary>
        /// Sets the host element (usually the main window or panel).
        /// </summary>
        public void SetHost(FrameworkElement host)
        {
            _hostElement = host;
        }

        /// <summary>
        /// Starts the default StingBIM tour.
        /// </summary>
        public void StartDefaultTour()
        {
            _steps = GetDefaultSteps();
            StartTour();
        }

        /// <summary>
        /// Starts a custom tour with provided steps.
        /// </summary>
        public void StartTour(List<TourStep> steps)
        {
            _steps = steps ?? new List<TourStep>();
            StartTour();
        }

        /// <summary>
        /// Ends the tour immediately.
        /// </summary>
        public void EndTour()
        {
            OverlayGrid.Visibility = Visibility.Collapsed;
            _currentStepIndex = 0;
            Logger.Info("Tour ended");
        }

        /// <summary>
        /// Goes to a specific step by index.
        /// </summary>
        public void GoToStep(int index)
        {
            if (index >= 0 && index < _steps.Count)
            {
                _currentStepIndex = index;
                ShowCurrentStep();
            }
        }

        #endregion

        #region Private Methods

        private void StartTour()
        {
            if (_steps.Count == 0)
            {
                Logger.Warn("No tour steps defined");
                return;
            }

            _currentStepIndex = 0;
            OverlayGrid.Visibility = Visibility.Visible;
            CreateStepIndicators();
            ShowCurrentStep();
            Logger.Info($"Tour started with {_steps.Count} steps");
        }

        private void ShowCurrentStep()
        {
            if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Count)
                return;

            var step = _steps[_currentStepIndex];

            // Update content
            StepNumberText.Text = $"Step {_currentStepIndex + 1} of {_steps.Count}";
            StepTitleText.Text = step.Title;
            StepDescriptionText.Text = step.Description;
            StepIcon.Data = Geometry.Parse(GetStepIconPath(step.StepType));

            // Update navigation buttons
            PreviousButton.Visibility = _currentStepIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
            NextButton.Content = _currentStepIndex == _steps.Count - 1 ? "Finish" : "Next";

            // Update step indicators
            UpdateStepIndicators();

            // Position tooltip and highlight
            PositionElements(step);

            StepShown?.Invoke(this, step);
            Logger.Debug($"Showing tour step {_currentStepIndex + 1}: {step.Title}");
        }

        private void PositionElements(TourStep step)
        {
            // Get target element
            FrameworkElement target = null;
            if (!string.IsNullOrEmpty(step.TargetKey) && _targetElements.TryGetValue(step.TargetKey, out target))
            {
                // Element found - position highlight and tooltip relative to it
                PositionHighlight(target, step);
                PositionTooltip(target, step);
            }
            else
            {
                // No target - center tooltip
                HighlightBorder.Visibility = Visibility.Collapsed;
                TooltipArrow.Visibility = Visibility.Collapsed;
                CenterTooltip();
            }
        }

        private void PositionHighlight(FrameworkElement target, TourStep step)
        {
            try
            {
                var transform = target.TransformToAncestor(_hostElement ?? Application.Current.MainWindow);
                var position = transform.Transform(new Point(0, 0));

                var padding = step.HighlightPadding;
                HighlightBorder.Margin = new Thickness(
                    position.X - padding,
                    position.Y - padding,
                    0, 0);
                HighlightBorder.Width = target.ActualWidth + padding * 2;
                HighlightBorder.Height = target.ActualHeight + padding * 2;
                HighlightBorder.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not position highlight: {ex.Message}");
                HighlightBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void PositionTooltip(FrameworkElement target, TourStep step)
        {
            try
            {
                var transform = target.TransformToAncestor(_hostElement ?? Application.Current.MainWindow);
                var targetPos = transform.Transform(new Point(0, 0));

                double tooltipLeft = 0;
                double tooltipTop = 0;
                const double spacing = 16;

                TooltipBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var tooltipSize = TooltipBorder.DesiredSize;

                switch (step.TooltipPosition)
                {
                    case TooltipPosition.Right:
                        tooltipLeft = targetPos.X + target.ActualWidth + spacing;
                        tooltipTop = targetPos.Y + (target.ActualHeight - tooltipSize.Height) / 2;
                        SetArrow(ArrowDirection.Left, targetPos.X + target.ActualWidth, targetPos.Y + target.ActualHeight / 2);
                        break;

                    case TooltipPosition.Left:
                        tooltipLeft = targetPos.X - tooltipSize.Width - spacing;
                        tooltipTop = targetPos.Y + (target.ActualHeight - tooltipSize.Height) / 2;
                        SetArrow(ArrowDirection.Right, targetPos.X, targetPos.Y + target.ActualHeight / 2);
                        break;

                    case TooltipPosition.Bottom:
                        tooltipLeft = targetPos.X + (target.ActualWidth - tooltipSize.Width) / 2;
                        tooltipTop = targetPos.Y + target.ActualHeight + spacing;
                        SetArrow(ArrowDirection.Up, targetPos.X + target.ActualWidth / 2, targetPos.Y + target.ActualHeight);
                        break;

                    case TooltipPosition.Top:
                        tooltipLeft = targetPos.X + (target.ActualWidth - tooltipSize.Width) / 2;
                        tooltipTop = targetPos.Y - tooltipSize.Height - spacing;
                        SetArrow(ArrowDirection.Down, targetPos.X + target.ActualWidth / 2, targetPos.Y);
                        break;

                    default:
                        CenterTooltip();
                        return;
                }

                // Ensure tooltip stays within bounds
                tooltipLeft = Math.Max(16, tooltipLeft);
                tooltipTop = Math.Max(16, tooltipTop);

                TooltipBorder.Margin = new Thickness(tooltipLeft, tooltipTop, 0, 0);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not position tooltip: {ex.Message}");
                CenterTooltip();
            }
        }

        private void SetArrow(ArrowDirection direction, double x, double y)
        {
            string pathData = direction switch
            {
                ArrowDirection.Left => "M0,10 L10,0 L10,20 Z",
                ArrowDirection.Right => "M10,10 L0,0 L0,20 Z",
                ArrowDirection.Up => "M10,0 L0,10 L20,10 Z",
                ArrowDirection.Down => "M10,10 L0,0 L20,0 Z",
                _ => ""
            };

            TooltipArrow.Data = Geometry.Parse(pathData);

            double arrowLeft = x;
            double arrowTop = y;

            switch (direction)
            {
                case ArrowDirection.Left:
                    arrowLeft = x + 2;
                    arrowTop = y - 10;
                    break;
                case ArrowDirection.Right:
                    arrowLeft = x - 12;
                    arrowTop = y - 10;
                    break;
                case ArrowDirection.Up:
                    arrowLeft = x - 10;
                    arrowTop = y + 2;
                    break;
                case ArrowDirection.Down:
                    arrowLeft = x - 10;
                    arrowTop = y - 12;
                    break;
            }

            TooltipArrow.Margin = new Thickness(arrowLeft, arrowTop, 0, 0);
            TooltipArrow.Visibility = Visibility.Visible;
        }

        private void CenterTooltip()
        {
            TooltipBorder.HorizontalAlignment = HorizontalAlignment.Center;
            TooltipBorder.VerticalAlignment = VerticalAlignment.Center;
            TooltipBorder.Margin = new Thickness(0);
            TooltipArrow.Visibility = Visibility.Collapsed;
        }

        private void CreateStepIndicators()
        {
            StepIndicators.Children.Clear();
            for (int i = 0; i < _steps.Count; i++)
            {
                var indicator = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = (Brush)FindResource("ForegroundSecondaryBrush"),
                    Margin = new Thickness(3, 0, 3, 0)
                };
                StepIndicators.Children.Add(indicator);
            }
        }

        private void UpdateStepIndicators()
        {
            for (int i = 0; i < StepIndicators.Children.Count; i++)
            {
                if (StepIndicators.Children[i] is Ellipse indicator)
                {
                    indicator.Fill = i == _currentStepIndex
                        ? (Brush)FindResource("AccentBrush")
                        : (Brush)FindResource("ForegroundSecondaryBrush");
                    indicator.Width = i == _currentStepIndex ? 10 : 8;
                    indicator.Height = i == _currentStepIndex ? 10 : 8;
                }
            }
        }

        private static string GetStepIconPath(TourStepType type)
        {
            return type switch
            {
                TourStepType.Welcome => "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M15,16.34V7H13V14.26L9.5,16.7L10.53,18.16L15,16.34Z",
                TourStepType.Feature => "M12,17.27L18.18,21L16.54,13.97L22,9.24L14.81,8.62L12,2L9.19,8.62L2,9.24L7.45,13.97L5.82,21L12,17.27Z",
                TourStepType.Tip => "M9,20.42L2.79,14.21L5.62,11.38L9,14.77L18.88,4.88L21.71,7.71L9,20.42Z",
                TourStepType.Warning => "M12,2L1,21H23M12,6L19.53,19H4.47M11,10V14H13V10M11,16V18H13V16",
                TourStepType.Action => "M13,9V3.5L18.5,9M6,2C4.89,2 4,2.89 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2H6Z",
                _ => "M13,9H11V7H13M13,17H11V11H13M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z"
            };
        }

        private List<TourStep> GetDefaultSteps()
        {
            return new List<TourStep>
            {
                new TourStep
                {
                    Title = "Welcome to StingBIM AI",
                    Description = "Let's take a quick tour of the AI assistant interface. This will help you get the most out of the intelligent BIM design features.",
                    StepType = TourStepType.Welcome,
                    TooltipPosition = TooltipPosition.Center
                },
                new TourStep
                {
                    Title = "Chat Input",
                    Description = "Type your commands and questions here. Use natural language to describe what you want to do - like 'Create a wall from point A to point B' or 'Show me all doors on Level 1'.",
                    TargetKey = "ChatInput",
                    StepType = TourStepType.Feature,
                    TooltipPosition = TooltipPosition.Top
                },
                new TourStep
                {
                    Title = "Voice Commands",
                    Description = "Click the microphone icon or press Ctrl+M to use voice commands. Speak naturally and the AI will understand your intent.",
                    TargetKey = "VoiceButton",
                    StepType = TourStepType.Feature,
                    TooltipPosition = TooltipPosition.Top
                },
                new TourStep
                {
                    Title = "Quick Actions",
                    Description = "Press Ctrl+Q or click here to access common actions quickly - create elements, run analyses, generate schedules, and more.",
                    TargetKey = "QuickActionsButton",
                    StepType = TourStepType.Tip,
                    TooltipPosition = TooltipPosition.Left
                },
                new TourStep
                {
                    Title = "Context Panel",
                    Description = "The sidebar shows your current Revit context - active view, selection, and model statistics. It also displays recent commands for quick re-execution.",
                    TargetKey = "ContextSidebar",
                    StepType = TourStepType.Feature,
                    TooltipPosition = TooltipPosition.Left
                },
                new TourStep
                {
                    Title = "Keyboard Shortcuts",
                    Description = "Use keyboard shortcuts for faster workflows:\n• Ctrl+Enter: Send message\n• Ctrl+M: Toggle voice\n• Ctrl+Q: Quick actions\n• Ctrl+F: Search chat\n• Escape: Cancel operation",
                    StepType = TourStepType.Tip,
                    TooltipPosition = TooltipPosition.Center
                },
                new TourStep
                {
                    Title = "You're Ready!",
                    Description = "That's it! Start by typing a command or asking a question. Try 'Help' to see what I can do, or dive right in with a BIM task.",
                    StepType = TourStepType.Action,
                    TooltipPosition = TooltipPosition.Center
                }
            };
        }

        #endregion

        #region Event Handlers

        private void DimmingLayer_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Click outside tooltip - do nothing or skip
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            EndTour();
            TourSkipped?.Invoke(this, EventArgs.Empty);
            Logger.Info("Tour skipped by user");
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStepIndex > 0)
            {
                _currentStepIndex--;
                ShowCurrentStep();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStepIndex < _steps.Count - 1)
            {
                _currentStepIndex++;
                ShowCurrentStep();
            }
            else
            {
                // Tour completed
                EndTour();
                TourCompleted?.Invoke(this, EventArgs.Empty);
                Logger.Info("Tour completed");
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a step in the onboarding tour.
    /// </summary>
    public class TourStep
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string TargetKey { get; set; }
        public TourStepType StepType { get; set; } = TourStepType.Info;
        public TooltipPosition TooltipPosition { get; set; } = TooltipPosition.Bottom;
        public double HighlightPadding { get; set; } = 8;
    }

    /// <summary>
    /// Types of tour steps for icon display.
    /// </summary>
    public enum TourStepType
    {
        Info,
        Welcome,
        Feature,
        Tip,
        Warning,
        Action
    }

    /// <summary>
    /// Position of the tooltip relative to the target element.
    /// </summary>
    public enum TooltipPosition
    {
        Top,
        Bottom,
        Left,
        Right,
        Center
    }

    /// <summary>
    /// Direction for tooltip arrow.
    /// </summary>
    internal enum ArrowDirection
    {
        Up,
        Down,
        Left,
        Right
    }
}
