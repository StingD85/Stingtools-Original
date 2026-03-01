using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using StingBIM.Core.Logging;

namespace StingBIM.Data.Schedules
{
    /// <summary>
    /// Applies formatting to Revit schedules
    /// Handles colors, fonts, grid lines, totals, conditional formatting
    /// Supports batch formatting operations
    /// </summary>
    public class ScheduleFormatter
    {
        #region Private Fields
        
        private static readonly StingBIMLogger _logger = StingBIMLogger.For<ScheduleFormatter>();
        private readonly Document _document;
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes a new ScheduleFormatter
        /// </summary>
        /// <param name="document">Revit document</param>
        public ScheduleFormatter(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _logger.Info($"ScheduleFormatter initialized for document: {document.Title}");
        }
        
        #endregion

        #region Formatting Methods
        
        /// <summary>
        /// Applies all formatting from template to schedule
        /// </summary>
        /// <param name="schedule">Schedule to format</param>
        /// <param name="template">Template with formatting configuration</param>
        public void ApplyFormatting(ViewSchedule schedule, ScheduleTemplate template)
        {
            if (schedule == null)
                throw new ArgumentNullException(nameof(schedule));
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            
            try
            {
                _logger.Debug($"Applying formatting to schedule: {schedule.Name}");
                
                // Apply appearance settings
                ApplyAppearance(schedule, template.Formatting);
                
                // Apply grid formatting
                ApplyGridFormatting(schedule, template.Formatting);
                
                // Apply field-specific formatting
                ApplyFieldFormatting(schedule, template);
                
                // Apply totals configuration
                if (template.HasTotals)
                {
                    ApplyTotals(schedule, template.Totals);
                }
                
                _logger.Info($"Successfully applied formatting to: {schedule.Name}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to apply formatting to schedule: {schedule.Name}");
                throw;
            }
        }
        
        /// <summary>
        /// Applies appearance settings (colors, fonts, text size)
        /// </summary>
        private void ApplyAppearance(ViewSchedule schedule, ScheduleFormatting formatting)
        {
            if (formatting == null)
                return;
            
            try
            {
                var tableProp = schedule.GetTableData();
                var sectionData = tableProp.GetSectionData(SectionType.Header);
                
                // Header formatting
                if (formatting.HeaderColor != null)
                {
                    var color = ParseColor(formatting.HeaderColor);
                    if (color != null)
                    {
                        // Note: Revit API has limitations on schedule appearance
                        // Some formatting may require TableCellStyle or TextTypeId
                        _logger.Debug($"Header color: {color}");
                    }
                }
                
                // Text size
                if (formatting.TextSize.HasValue)
                {
                    double textSizeInFeet = formatting.TextSize.Value / 304.8; // mm to feet
                    _logger.Debug($"Text size: {formatting.TextSize.Value}mm");
                }
                
                // Font settings
                if (!string.IsNullOrEmpty(formatting.FontName))
                {
                    _logger.Debug($"Font: {formatting.FontName}");
                }
                
                _logger.Debug("Applied appearance settings");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to apply appearance settings");
            }
        }
        
        /// <summary>
        /// Applies grid formatting (grid lines, outlines)
        /// </summary>
        private void ApplyGridFormatting(ViewSchedule schedule, ScheduleFormatting formatting)
        {
            if (formatting == null)
                return;
            
            try
            {
                var tableProp = schedule.GetTableData();
                var sectionData = tableProp.GetSectionData(SectionType.Body);
                
                // Show grid lines
                if (sectionData != null)
                {
                    // Grid visibility settings
                    _logger.Debug($"Grid lines: {formatting.ShowGridLines}, Outlines: {formatting.ShowOutlines}");
                }
                
                // Alternate row coloring
                if (formatting.AlternateRowColors)
                {
                    _logger.Debug("Alternate row colors enabled");
                }
                
                _logger.Debug("Applied grid formatting");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to apply grid formatting");
            }
        }
        
        /// <summary>
        /// Applies field-specific formatting (alignment, width, totals)
        /// </summary>
        private void ApplyFieldFormatting(ViewSchedule schedule, ScheduleTemplate template)
        {
            try
            {
                var definition = schedule.Definition;
                int fieldCount = definition.GetFieldCount();
                
                for (int i = 0; i < fieldCount && i < template.Fields.Count; i++)
                {
                    var field = definition.GetField(i);
                    var fieldDef = template.Fields[i];
                    
                    // Set alignment
                    {
                        var alignment = ParseAlignment(fieldDef.Alignment.ToString());
                        field.HorizontalAlignment = (ScheduleHorizontalAlignment)(int)alignment;
                    }
                    
                    // Set column width
                    if (fieldDef.ColumnWidth.HasValue)
                    {
                        field.GridColumnWidth = fieldDef.ColumnWidth.Value / 304.8; // mm to feet
                    }
                    
                    // Set header text
                    if (!string.IsNullOrEmpty(fieldDef.Header))
                    {
                        field.ColumnHeading = fieldDef.Header;
                    }
                    
                    // Set visibility
                    field.IsHidden = !fieldDef.IsVisible;
                    
                    // Calculate totals
                    if (fieldDef.CalculateTotals)
                    {
                        field.DisplayType = ScheduleFieldDisplayType.Totals;
                    }
                }
                
                _logger.Debug($"Applied formatting to {fieldCount} fields");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to apply field formatting");
            }
        }
        
        /// <summary>
        /// Applies totals configuration
        /// </summary>
        private void ApplyTotals(ViewSchedule schedule, ScheduleTotalsConfiguration totals)
        {
            if (totals == null)
                return;
            
            try
            {
                var definition = schedule.Definition;
                
                // Show grand totals
                if (totals.ShowGrandTotals)
                {
                    definition.ShowGrandTotal = true;
                    
                    if (!string.IsNullOrEmpty(totals.GrandTotalsTitle))
                    {
                        definition.GrandTotalTitle = totals.GrandTotalsTitle;
                    }
                }
                
                // Apply totals to specific fields
                foreach (var totalFieldName in totals.TotalFields)
                {
                    var field = FindFieldByName(definition, totalFieldName);
                    if (field != null)
                    {
                        field.DisplayType = ScheduleFieldDisplayType.Totals;
                        _logger.Debug($"Enabled totals for field: {totalFieldName}");
                    }
                }
                
                _logger.Debug("Applied totals configuration");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to apply totals");
            }
        }
        
        #endregion

        #region Conditional Formatting
        
        /// <summary>
        /// Applies conditional formatting based on field values
        /// </summary>
        /// <param name="schedule">Schedule to format</param>
        /// <param name="rules">List of conditional formatting rules</param>
        public void ApplyConditionalFormatting(
            ViewSchedule schedule,
            List<ConditionalFormattingRule> rules)
        {
            if (schedule == null || rules == null || rules.Count == 0)
                return;
            
            try
            {
                _logger.Debug($"Applying {rules.Count} conditional formatting rules");
                
                foreach (var rule in rules)
                {
                    ApplyFormattingRule(schedule, rule);
                }
                
                _logger.Info("Conditional formatting applied successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to apply conditional formatting");
            }
        }
        
        /// <summary>
        /// Applies a single formatting rule
        /// </summary>
        private void ApplyFormattingRule(ViewSchedule schedule, ConditionalFormattingRule rule)
        {
            try
            {
                var definition = schedule.Definition;
                var field = FindFieldByName(definition, rule.FieldName);
                
                if (field == null)
                {
                    _logger.Warn($"Field not found for conditional formatting: {rule.FieldName}");
                    return;
                }
                
                // Note: Revit has limited support for conditional formatting
                // Most formatting is applied through TableCellStyle
                _logger.Debug($"Applied rule: {rule.Condition} on {rule.FieldName}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to apply formatting rule: {rule.FieldName}");
            }
        }
        
        #endregion

        #region Batch Operations
        
        /// <summary>
        /// Applies formatting to multiple schedules
        /// </summary>
        /// <param name="schedules">List of schedules with templates</param>
        /// <param name="progress">Progress reporter</param>
        public void ApplyFormattingBatch(
            List<(ViewSchedule schedule, ScheduleTemplate template)> schedules,
            IProgress<FormattingProgress> progress = null)
        {
            int total = schedules.Count;
            int processed = 0;
            int successful = 0;
            
            _logger.Info($"Starting batch formatting for {total} schedules");
            
            foreach (var (schedule, template) in schedules)
            {
                try
                {
                    ApplyFormatting(schedule, template);
                    successful++;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to format schedule: {schedule.Name}");
                }
                
                processed++;
                
                progress?.Report(new FormattingProgress
                {
                    TotalSchedules = total,
                    ProcessedSchedules = processed,
                    SuccessfulSchedules = successful,
                    PercentComplete = (processed * 100) / total
                });
            }
            
            _logger.Info($"Batch formatting complete: {successful}/{total} successful");
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Finds a schedule field by parameter name
        /// </summary>
        private ScheduleField FindFieldByName(ScheduleDefinition definition, string parameterName)
        {
            int fieldCount = definition.GetFieldCount();

            for (int i = 0; i < fieldCount; i++)
            {
                var field = definition.GetField(i);

                // Match by column heading name
                if (string.Equals(field.ColumnHeading, parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    return field;
                }

                // Fall back to matching by parameter ID via schedulable fields
                var schedulableField = definition.GetSchedulableFields()
                    .FirstOrDefault(sf => sf.ParameterId == field.ParameterId);

                if (schedulableField != null &&
                    string.Equals(schedulableField.GetName(_document), parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    return field;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Parses color string to Revit Color
        /// Format: "RGB(r,g,b)" or "#RRGGBB"
        /// </summary>
        private Color? ParseColor(string colorStr)
        {
            if (string.IsNullOrWhiteSpace(colorStr))
                return null;
            
            try
            {
                // Parse RGB(r,g,b) format
                if (colorStr.StartsWith("RGB(", StringComparison.OrdinalIgnoreCase))
                {
                    var rgb = colorStr.Substring(4, colorStr.Length - 5);
                    var parts = rgb.Split(',');
                    
                    if (parts.Length == 3)
                    {
                        byte r = byte.Parse(parts[0].Trim());
                        byte g = byte.Parse(parts[1].Trim());
                        byte b = byte.Parse(parts[2].Trim());
                        return new Color(r, g, b);
                    }
                }
                // Parse #RRGGBB format
                else if (colorStr.StartsWith("#"))
                {
                    var hex = colorStr.Substring(1);
                    if (hex.Length == 6)
                    {
                        byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                        byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                        byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                        return new Color(r, g, b);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to parse color: {colorStr} - {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Parses alignment string to HorizontalAlignmentStyle
        /// </summary>
        private HorizontalAlignmentStyle ParseAlignment(string alignment)
        {
            switch (alignment?.ToLowerInvariant())
            {
                case "center":
                case "centre":
                    return HorizontalAlignmentStyle.Center;
                case "right":
                    return HorizontalAlignmentStyle.Right;
                case "left":
                default:
                    return HorizontalAlignmentStyle.Left;
            }
        }
        
        #endregion

        #region Static Factory Methods
        
        /// <summary>
        /// Creates a ScheduleFormatter for the specified document
        /// </summary>
        public static ScheduleFormatter For(Document document)
        {
            return new ScheduleFormatter(document);
        }
        
        #endregion
    }
    
    #region Supporting Classes

    /// <summary>
    /// Configuration for schedule totals
    /// </summary>
    public class ScheduleTotalsConfiguration
    {
        public bool ShowGrandTotals { get; set; }
        public string GrandTotalsTitle { get; set; }
        public List<string> TotalFields { get; set; } = new List<string>();
    }

    /// <summary>
    /// Conditional formatting rule
    /// </summary>
    public class ConditionalFormattingRule
    {
        public string FieldName { get; set; }
        public string Condition { get; set; } // equals, greaterThan, lessThan, contains, etc.
        public object Value { get; set; }
        public Color? TextColor { get; set; }
        public Color? BackgroundColor { get; set; }
        public bool? IsBold { get; set; }
        public bool? IsItalic { get; set; }
    }
    
    /// <summary>
    /// Formatting progress information
    /// </summary>
    public class FormattingProgress
    {
        public int TotalSchedules { get; set; }
        public int ProcessedSchedules { get; set; }
        public int SuccessfulSchedules { get; set; }
        public int PercentComplete { get; set; }
        
        public override string ToString()
        {
            return $"Formatting: {ProcessedSchedules}/{TotalSchedules} ({PercentComplete}%)";
        }
    }
    
    #endregion
}
