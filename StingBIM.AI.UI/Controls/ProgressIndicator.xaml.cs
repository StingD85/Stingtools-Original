// StingBIM.AI.UI.Controls.ProgressIndicator
// Animated progress/loading indicator with multiple styles
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - Visual Feedback

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Animated progress indicator with multiple display styles.
    /// </summary>
    public partial class ProgressIndicator : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(
                nameof(IsActive),
                typeof(bool),
                typeof(ProgressIndicator),
                new PropertyMetadata(false, OnIsActiveChanged));

        public static readonly DependencyProperty StyleTypeProperty =
            DependencyProperty.Register(
                nameof(StyleType),
                typeof(ProgressStyle),
                typeof(ProgressIndicator),
                new PropertyMetadata(ProgressStyle.Spinner, OnStyleTypeChanged));

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register(
                nameof(Progress),
                typeof(double),
                typeof(ProgressIndicator),
                new PropertyMetadata(0.0, OnProgressChanged));

        public static readonly DependencyProperty IsIndeterminateProperty =
            DependencyProperty.Register(
                nameof(IsIndeterminate),
                typeof(bool),
                typeof(ProgressIndicator),
                new PropertyMetadata(true));

        #endregion

        #region Properties

        /// <summary>
        /// Whether the progress indicator is active/animating.
        /// </summary>
        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        /// <summary>
        /// The visual style of the progress indicator.
        /// </summary>
        public ProgressStyle StyleType
        {
            get => (ProgressStyle)GetValue(StyleTypeProperty);
            set => SetValue(StyleTypeProperty, value);
        }

        /// <summary>
        /// Current progress value (0.0 to 1.0) for determinate mode.
        /// </summary>
        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        /// <summary>
        /// Whether the progress is indeterminate (unknown duration).
        /// </summary>
        public bool IsIndeterminate
        {
            get => (bool)GetValue(IsIndeterminateProperty);
            set => SetValue(IsIndeterminateProperty, value);
        }

        #endregion

        private Storyboard _spinAnimation;
        private Storyboard _dotAnimation;

        public ProgressIndicator()
        {
            InitializeComponent();

            _spinAnimation = (Storyboard)Resources["SpinAnimation"];
            _dotAnimation = (Storyboard)Resources["DotAnimation"];

            UpdateStyle();
        }

        private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProgressIndicator indicator)
            {
                if ((bool)e.NewValue)
                {
                    indicator.StartAnimation();
                }
                else
                {
                    indicator.StopAnimation();
                }
            }
        }

        private static void OnStyleTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProgressIndicator indicator)
            {
                indicator.UpdateStyle();
                if (indicator.IsActive)
                {
                    indicator.StartAnimation();
                }
            }
        }

        private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProgressIndicator indicator && !indicator.IsIndeterminate)
            {
                indicator.UpdateProgress((double)e.NewValue);
            }
        }

        private void UpdateStyle()
        {
            // Hide all containers
            SpinnerContainer.Visibility = Visibility.Collapsed;
            DotsContainer.Visibility = Visibility.Collapsed;
            BrainContainer.Visibility = Visibility.Collapsed;

            // Show selected style
            switch (StyleType)
            {
                case ProgressStyle.Spinner:
                    SpinnerContainer.Visibility = Visibility.Visible;
                    break;
                case ProgressStyle.Dots:
                    DotsContainer.Visibility = Visibility.Visible;
                    break;
                case ProgressStyle.Brain:
                    BrainContainer.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void StartAnimation()
        {
            switch (StyleType)
            {
                case ProgressStyle.Spinner:
                    _spinAnimation?.Begin();
                    break;
                case ProgressStyle.Dots:
                    _dotAnimation?.Begin();
                    break;
                case ProgressStyle.Brain:
                    _spinAnimation?.Begin(); // Reuse spin for brain pulse
                    break;
            }
        }

        private void StopAnimation()
        {
            _spinAnimation?.Stop();
            _dotAnimation?.Stop();
        }

        private void UpdateProgress(double progress)
        {
            // For determinate progress, update the arc length
            // This would modify the arc segment to show progress
            // For now, just using indeterminate spinner
        }

        /// <summary>
        /// Starts the progress animation.
        /// </summary>
        public void Start()
        {
            IsActive = true;
        }

        /// <summary>
        /// Stops the progress animation.
        /// </summary>
        public void Stop()
        {
            IsActive = false;
        }

        /// <summary>
        /// Sets progress for determinate mode.
        /// </summary>
        public void SetProgress(double progress)
        {
            IsIndeterminate = false;
            Progress = Math.Clamp(progress, 0.0, 1.0);
        }
    }

    /// <summary>
    /// Progress indicator visual styles.
    /// </summary>
    public enum ProgressStyle
    {
        /// <summary>Circular spinner.</summary>
        Spinner,

        /// <summary>Animated dots.</summary>
        Dots,

        /// <summary>Brain/thinking icon.</summary>
        Brain
    }
}
