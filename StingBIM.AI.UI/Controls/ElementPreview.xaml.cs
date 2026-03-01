// StingBIM.AI.UI.Controls.ElementPreview
// Control for displaying a preview of Revit elements
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NLog;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Displays a thumbnail preview of a Revit element with actions.
    /// </summary>
    public partial class ElementPreview : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        #region Dependency Properties

        public static readonly DependencyProperty ElementIdProperty =
            DependencyProperty.Register(nameof(ElementId), typeof(long), typeof(ElementPreview),
                new PropertyMetadata(0L, OnElementChanged));

        public static readonly DependencyProperty ElementNameProperty =
            DependencyProperty.Register(nameof(ElementName), typeof(string), typeof(ElementPreview),
                new PropertyMetadata("Unknown Element", OnElementChanged));

        public static readonly DependencyProperty ElementTypeProperty =
            DependencyProperty.Register(nameof(ElementType), typeof(ElementCategory), typeof(ElementPreview),
                new PropertyMetadata(ElementCategory.Generic, OnElementChanged));

        public static readonly DependencyProperty LevelNameProperty =
            DependencyProperty.Register(nameof(LevelName), typeof(string), typeof(ElementPreview),
                new PropertyMetadata("-", OnElementChanged));

        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(ElementStatus), typeof(ElementPreview),
                new PropertyMetadata(ElementStatus.Created, OnStatusChanged));

        public static readonly DependencyProperty PreviewImageSourceProperty =
            DependencyProperty.Register(nameof(PreviewImageSource), typeof(ImageSource), typeof(ElementPreview),
                new PropertyMetadata(null, OnImageChanged));

        #endregion

        #region Properties

        public long ElementId
        {
            get => (long)GetValue(ElementIdProperty);
            set => SetValue(ElementIdProperty, value);
        }

        public string ElementName
        {
            get => (string)GetValue(ElementNameProperty);
            set => SetValue(ElementNameProperty, value);
        }

        public ElementCategory ElementType
        {
            get => (ElementCategory)GetValue(ElementTypeProperty);
            set => SetValue(ElementTypeProperty, value);
        }

        public string LevelName
        {
            get => (string)GetValue(LevelNameProperty);
            set => SetValue(LevelNameProperty, value);
        }

        public ElementStatus Status
        {
            get => (ElementStatus)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public ImageSource PreviewImageSource
        {
            get => (ImageSource)GetValue(PreviewImageSourceProperty);
            set => SetValue(PreviewImageSourceProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Fired when user requests to select the element in Revit.
        /// </summary>
        public event EventHandler<long> SelectRequested;

        /// <summary>
        /// Fired when user requests to zoom to the element.
        /// </summary>
        public event EventHandler<long> ZoomRequested;

        /// <summary>
        /// Fired when user requests to view element properties.
        /// </summary>
        public event EventHandler<long> PropertiesRequested;

        #endregion

        public ElementPreview()
        {
            InitializeComponent();
            UpdateDisplay();
        }

        private static void OnElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ElementPreview preview)
            {
                preview.UpdateDisplay();
            }
        }

        private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ElementPreview preview)
            {
                preview.UpdateStatusBadge();
            }
        }

        private static void OnImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ElementPreview preview)
            {
                preview.UpdateImageDisplay();
            }
        }

        private void UpdateDisplay()
        {
            ElementNameText.Text = ElementName;
            ElementIdText.Text = ElementId.ToString();
            ElementLevelText.Text = LevelName;
            ElementTypeText.Text = ElementType.ToString();

            ElementIcon.Data = Geometry.Parse(GetIconPath(ElementType));

            UpdateStatusBadge();
        }

        private void UpdateStatusBadge()
        {
            var (color, text) = Status switch
            {
                ElementStatus.Created => ("SuccessBrush", "Created"),
                ElementStatus.Modified => ("InfoBrush", "Modified"),
                ElementStatus.Deleted => ("ErrorBrush", "Deleted"),
                ElementStatus.Selected => ("AccentBrush", "Selected"),
                _ => ("ForegroundSecondaryBrush", "Unknown")
            };

            StatusBadge.Background = (Brush)FindResource(color);
            StatusText.Text = text;
        }

        private void UpdateImageDisplay()
        {
            if (PreviewImageSource != null)
            {
                PreviewImage.Source = PreviewImageSource;
                PreviewImage.Visibility = Visibility.Visible;
                PlaceholderPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                PreviewImage.Visibility = Visibility.Collapsed;
                PlaceholderPanel.Visibility = Visibility.Visible;
            }
        }

        private static string GetIconPath(ElementCategory type)
        {
            return type switch
            {
                ElementCategory.Wall => "M3,3H21V5H3V3M3,7H21V9H3V7M3,11H21V13H3V11M3,15H21V17H3V15M3,19H21V21H3V19Z",
                ElementCategory.Door => "M8,3C6.89,3 6,3.89 6,5V21H18V5C18,3.89 17.11,3 16,3H8M8,5H16V19H8V5M13,11A1,1 0 0,0 12,12A1,1 0 0,0 13,13A1,1 0 0,0 14,12A1,1 0 0,0 13,11Z",
                ElementCategory.Window => "M3,2H21V8H3V2M3,10H21V22H3V10M5,4V6H19V4H5M5,12V20H19V12H5M11,14H13V18H11V14Z",
                ElementCategory.Floor => "M13,17V9H17V17H13M9,17V5H13V17H9M5,17V13H9V17H5M19,17H21V19H3V17H5M5,9V5H9V9H5Z",
                ElementCategory.Ceiling => "M19,19V7H5V19H19M21,21H3V5H21V21M12,15A2,2 0 0,0 14,13A2,2 0 0,0 12,11A2,2 0 0,0 10,13A2,2 0 0,0 12,15Z",
                ElementCategory.Roof => "M12,3L2,12H5V20H19V12H22L12,3M12,8.5L16,12V18H8V12L12,8.5Z",
                ElementCategory.Room => "M12,3L2,12H5V20H19V12H22L12,3M12,8.75A2.25,2.25 0 0,1 14.25,11A2.25,2.25 0 0,1 12,13.25A2.25,2.25 0 0,1 9.75,11A2.25,2.25 0 0,1 12,8.75Z",
                ElementCategory.Stair => "M15,5V11H9V5H15M21,5V11H15V5H21M21,17V11H15V17H21M9,17V11H3V17H9M9,11V5H3V11H9M15,17V11H9V17H15Z",
                ElementCategory.Column => "M6,2V22H10V14.5L12,16.5L14,14.5V22H18V2H6M14,6V10.5L12,12.5L10,10.5V6H14Z",
                ElementCategory.Beam => "M4,2V22H8V2H4M10,2V22H14V2H10M16,2V22H20V2H16Z",
                _ => "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z"
            };
        }

        #region Event Handlers

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            SelectRequested?.Invoke(this, ElementId);
            Logger.Debug($"Select requested for element {ElementId}");
        }

        private void ZoomButton_Click(object sender, RoutedEventArgs e)
        {
            ZoomRequested?.Invoke(this, ElementId);
            Logger.Debug($"Zoom requested for element {ElementId}");
        }

        private void PropertiesButton_Click(object sender, RoutedEventArgs e)
        {
            PropertiesRequested?.Invoke(this, ElementId);
            Logger.Debug($"Properties requested for element {ElementId}");
        }

        #endregion
    }

    /// <summary>
    /// Categories of Revit elements for preview icons.
    /// </summary>
    public enum ElementCategory
    {
        Generic,
        Wall,
        Door,
        Window,
        Floor,
        Ceiling,
        Roof,
        Room,
        Stair,
        Column,
        Beam
    }

    /// <summary>
    /// Status of an element operation.
    /// </summary>
    public enum ElementStatus
    {
        Created,
        Modified,
        Deleted,
        Selected
    }
}
