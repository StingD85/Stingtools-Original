// StingBIM.AI.UI.Controls.DropZone
// Drag and drop support for file uploads and data transfer
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using NLog;

namespace StingBIM.AI.UI.Controls
{
    /// <summary>
    /// Drop zone control that accepts dragged files and data.
    /// </summary>
    public partial class DropZone : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private Storyboard _pulseAnimation;
        private Storyboard _bounceAnimation;

        /// <summary>
        /// Supported file extensions for drop operations.
        /// </summary>
        public static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // BIM/CAD files
            ".ifc", ".rvt", ".rfa", ".dwg", ".dxf", ".dgn",
            // Data files
            ".csv", ".xlsx", ".xls", ".json", ".xml",
            // Image files
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff",
            // Document files
            ".pdf", ".txt", ".md",
            // Schedule/parameter files
            ".sch", ".param"
        };

        #region Dependency Properties

        public static readonly DependencyProperty AllowedExtensionsProperty =
            DependencyProperty.Register(nameof(AllowedExtensions), typeof(HashSet<string>), typeof(DropZone),
                new PropertyMetadata(SupportedExtensions));

        public static readonly DependencyProperty ShowOverlayProperty =
            DependencyProperty.Register(nameof(ShowOverlay), typeof(bool), typeof(DropZone),
                new PropertyMetadata(true));

        public static readonly DependencyProperty DropTitleProperty =
            DependencyProperty.Register(nameof(DropTitle), typeof(string), typeof(DropZone),
                new PropertyMetadata("Drop files here"));

        public static readonly DependencyProperty DropSubtitleProperty =
            DependencyProperty.Register(nameof(DropSubtitle), typeof(string), typeof(DropZone),
                new PropertyMetadata("Images, IFC files, CSV schedules"));

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the allowed file extensions for drop operations.
        /// </summary>
        public HashSet<string> AllowedExtensions
        {
            get => (HashSet<string>)GetValue(AllowedExtensionsProperty);
            set => SetValue(AllowedExtensionsProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the overlay when dragging.
        /// </summary>
        public bool ShowOverlay
        {
            get => (bool)GetValue(ShowOverlayProperty);
            set => SetValue(ShowOverlayProperty, value);
        }

        /// <summary>
        /// Gets or sets the title text shown during drag.
        /// </summary>
        public string DropTitle
        {
            get => (string)GetValue(DropTitleProperty);
            set => SetValue(DropTitleProperty, value);
        }

        /// <summary>
        /// Gets or sets the subtitle text shown during drag.
        /// </summary>
        public string DropSubtitle
        {
            get => (string)GetValue(DropSubtitleProperty);
            set => SetValue(DropSubtitleProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when files are dropped.
        /// </summary>
        public event EventHandler<FileDropEventArgs> FilesDropped;

        /// <summary>
        /// Event fired when text is dropped.
        /// </summary>
        public event EventHandler<TextDropEventArgs> TextDropped;

        /// <summary>
        /// Event fired when IFC data is dropped.
        /// </summary>
        public event EventHandler<IFCDropEventArgs> IFCDropped;

        /// <summary>
        /// Event fired when a drop is rejected (unsupported file type).
        /// </summary>
        public event EventHandler<DropRejectedEventArgs> DropRejected;

        #endregion

        public DropZone()
        {
            InitializeComponent();

            _pulseAnimation = (Storyboard)FindResource("PulseAnimation");
            _bounceAnimation = (Storyboard)FindResource("BounceAnimation");

            DropTitleText.Text = DropTitle;
            DropSubtitleText.Text = DropSubtitle;
        }

        #region Drag Event Handlers

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (CanAcceptDrop(e))
            {
                e.Effects = DragDropEffects.Copy;
                ShowDropOverlay();
                UpdateDropVisual(e);
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            HideDropOverlay();
            e.Handled = true;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (CanAcceptDrop(e))
            {
                e.Effects = DragDropEffects.Copy;
                UpdateDropVisual(e);
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            HideDropOverlay();

            try
            {
                // Handle file drop
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    ProcessFilesDrop(files);
                }
                // Handle text drop
                else if (e.Data.GetDataPresent(DataFormats.Text))
                {
                    var text = (string)e.Data.GetData(DataFormats.Text);
                    ProcessTextDrop(text);
                }
                // Handle other formats
                else if (e.Data.GetDataPresent(DataFormats.Serializable))
                {
                    var data = e.Data.GetData(DataFormats.Serializable);
                    Logger.Debug($"Serializable data dropped: {data?.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error processing drop");
            }

            e.Handled = true;
        }

        #endregion

        #region Private Methods

        private bool CanAcceptDrop(DragEventArgs e)
        {
            // Check for file drop
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                return files?.Any(f => IsFileSupported(f)) ?? false;
            }

            // Check for text drop
            if (e.Data.GetDataPresent(DataFormats.Text))
            {
                return true;
            }

            return false;
        }

        private bool IsFileSupported(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            return AllowedExtensions.Contains(ext);
        }

        private void ShowDropOverlay()
        {
            if (ShowOverlay)
            {
                DropOverlay.Visibility = Visibility.Visible;
                _pulseAnimation?.Begin();
                _bounceAnimation?.Begin();
            }
        }

        private void HideDropOverlay()
        {
            DropOverlay.Visibility = Visibility.Collapsed;
            _pulseAnimation?.Stop();
        }

        private void UpdateDropVisual(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var supportedFiles = files?.Where(f => IsFileSupported(f)).ToList();

                if (supportedFiles != null && supportedFiles.Count > 0)
                {
                    var ext = Path.GetExtension(supportedFiles[0]).ToLowerInvariant();
                    var (icon, title) = GetDropVisualForExtension(ext);
                    IconPath.Data = System.Windows.Media.Geometry.Parse(icon);
                    DropTitleText.Text = supportedFiles.Count == 1
                        ? $"Drop {Path.GetFileName(supportedFiles[0])}"
                        : $"Drop {supportedFiles.Count} files";
                    DropSubtitleText.Text = title;
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.Text))
            {
                IconPath.Data = System.Windows.Media.Geometry.Parse("M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20Z");
                DropTitleText.Text = "Drop text";
                DropSubtitleText.Text = "Paste text content";
            }
        }

        private (string icon, string subtitle) GetDropVisualForExtension(string ext)
        {
            return ext switch
            {
                ".ifc" => ("M12,2L22,8.5V15.5L12,22L2,15.5V8.5L12,2M12,4.15L4,9.07V14.93L12,19.85L20,14.93V9.07L12,4.15M12,6.87L17.13,10L12,13.13L6.87,10L12,6.87Z", "IFC Building Model"),
                ".rvt" or ".rfa" => ("M12,2L22,8.5V15.5L12,22L2,15.5V8.5L12,2M12,4.15L4,9.07V14.93L12,19.85L20,14.93V9.07L12,4.15Z", "Revit File"),
                ".dwg" or ".dxf" => ("M12,2L22,8.5V15.5L12,22L2,15.5V8.5L12,2M12,4.15L4,9.07V14.93L12,19.85L20,14.93V9.07L12,4.15Z", "CAD Drawing"),
                ".csv" or ".xlsx" or ".xls" => ("M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M10,19H8V12H10V19M14,19H12V14H14V19M18,19H16V16H18V19M13,9V3.5L18.5,9H13Z", "Spreadsheet Data"),
                ".json" or ".xml" => ("M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M13,13H15V18H13V13M13,9V11H15V9H13M9,13H11V18H9V13M9,9V11H11V9H9Z", "Data File"),
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" => ("M19,19H5V5H19M19,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3M13.96,12.29L11.21,15.83L9.25,13.47L6.5,17H17.5L13.96,12.29Z", "Image File"),
                ".pdf" => ("M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M13,9V3.5L18.5,9H13M10.5,11C9.67,11 9,11.67 9,12.5V13.5C9,14.33 9.67,15 10.5,15H11.5V14H10.5V13H11.5V12H10.5C10.22,12 10,12.22 10,12.5V14.5C10,14.78 10.22,15 10.5,15H12V11H10.5Z", "PDF Document"),
                _ => ("M9,16V10H5L12,3L19,10H15V16H9M5,20V18H19V20H5Z", "File")
            };
        }

        private void ProcessFilesDrop(string[] files)
        {
            var supportedFiles = new List<DroppedFile>();
            var rejectedFiles = new List<string>();

            foreach (var file in files)
            {
                if (IsFileSupported(file))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    var category = GetFileCategory(ext);

                    supportedFiles.Add(new DroppedFile
                    {
                        FilePath = file,
                        FileName = Path.GetFileName(file),
                        Extension = ext,
                        Category = category,
                        SizeBytes = new FileInfo(file).Length
                    });

                    Logger.Info($"File dropped: {file} ({category})");
                }
                else
                {
                    rejectedFiles.Add(file);
                    Logger.Warn($"File rejected (unsupported type): {file}");
                }
            }

            if (supportedFiles.Count > 0)
            {
                // Check for IFC files specifically
                var ifcFiles = supportedFiles.Where(f => f.Extension == ".ifc").ToList();
                if (ifcFiles.Count > 0)
                {
                    IFCDropped?.Invoke(this, new IFCDropEventArgs
                    {
                        Files = ifcFiles
                    });
                }

                FilesDropped?.Invoke(this, new FileDropEventArgs
                {
                    Files = supportedFiles
                });
            }

            if (rejectedFiles.Count > 0)
            {
                DropRejected?.Invoke(this, new DropRejectedEventArgs
                {
                    RejectedPaths = rejectedFiles,
                    Reason = "Unsupported file type"
                });
            }
        }

        private void ProcessTextDrop(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            Logger.Debug($"Text dropped: {text.Length} characters");

            // Try to detect if it's structured data
            var textType = DetectTextType(text);

            TextDropped?.Invoke(this, new TextDropEventArgs
            {
                Text = text,
                DetectedType = textType
            });
        }

        private static FileCategory GetFileCategory(string ext)
        {
            return ext switch
            {
                ".ifc" => FileCategory.IFC,
                ".rvt" or ".rfa" => FileCategory.Revit,
                ".dwg" or ".dxf" or ".dgn" => FileCategory.CAD,
                ".csv" or ".xlsx" or ".xls" => FileCategory.Spreadsheet,
                ".json" or ".xml" => FileCategory.Data,
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tiff" => FileCategory.Image,
                ".pdf" => FileCategory.PDF,
                ".txt" or ".md" => FileCategory.Text,
                _ => FileCategory.Other
            };
        }

        private static DroppedTextType DetectTextType(string text)
        {
            text = text.Trim();

            // Check for JSON
            if ((text.StartsWith("{") && text.EndsWith("}")) ||
                (text.StartsWith("[") && text.EndsWith("]")))
            {
                return DroppedTextType.Json;
            }

            // Check for XML
            if (text.StartsWith("<") && text.Contains(">"))
            {
                return DroppedTextType.Xml;
            }

            // Check for CSV (has commas and multiple lines)
            if (text.Contains(",") && text.Contains("\n"))
            {
                return DroppedTextType.Csv;
            }

            // Check for IFC content
            if (text.Contains("ISO-10303-21") || text.Contains("IFCPROJECT"))
            {
                return DroppedTextType.IFC;
            }

            return DroppedTextType.PlainText;
        }

        #endregion
    }

    #region Event Args and Supporting Classes

    /// <summary>
    /// Event args for file drop events.
    /// </summary>
    public class FileDropEventArgs : EventArgs
    {
        public List<DroppedFile> Files { get; set; } = new List<DroppedFile>();
    }

    /// <summary>
    /// Event args for text drop events.
    /// </summary>
    public class TextDropEventArgs : EventArgs
    {
        public string Text { get; set; }
        public DroppedTextType DetectedType { get; set; }
    }

    /// <summary>
    /// Event args for IFC file drop events.
    /// </summary>
    public class IFCDropEventArgs : EventArgs
    {
        public List<DroppedFile> Files { get; set; } = new List<DroppedFile>();
    }

    /// <summary>
    /// Event args for rejected drops.
    /// </summary>
    public class DropRejectedEventArgs : EventArgs
    {
        public List<string> RejectedPaths { get; set; } = new List<string>();
        public string Reason { get; set; }
    }

    /// <summary>
    /// Represents a dropped file.
    /// </summary>
    public class DroppedFile
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Extension { get; set; }
        public FileCategory Category { get; set; }
        public long SizeBytes { get; set; }

        public string SizeFormatted
        {
            get
            {
                if (SizeBytes < 1024) return $"{SizeBytes} B";
                if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
                return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
            }
        }
    }

    /// <summary>
    /// Categories of dropped files.
    /// </summary>
    public enum FileCategory
    {
        IFC,
        Revit,
        CAD,
        Spreadsheet,
        Data,
        Image,
        PDF,
        Text,
        Other
    }

    /// <summary>
    /// Types of dropped text content.
    /// </summary>
    public enum DroppedTextType
    {
        PlainText,
        Json,
        Xml,
        Csv,
        IFC
    }

    #endregion
}
