using System;
using System.Collections.Generic;
using System.Linq;

namespace StingBIM.Data.Schedules
{
    /// <summary>
    /// Represents a schedule template with all configuration
    /// Loaded from CSV schedule definition files
    /// Supports 146 schedule templates across all disciplines
    /// </summary>
    public sealed class ScheduleTemplate : IEquatable<ScheduleTemplate>
    {
        #region Properties
        
        /// <summary>
        /// Gets the schedule name
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// Gets the Revit category for this schedule
        /// </summary>
        public string CategoryName { get; }
        
        /// <summary>
        /// Gets the schedule type (Standard, Material Takeoff, etc.)
        /// </summary>
        public ScheduleType Type { get; }
        
        /// <summary>
        /// Gets the field definitions for this schedule
        /// </summary>
        public IReadOnlyList<ScheduleFieldDefinition> Fields { get; }
        
        /// <summary>
        /// Gets the filter definitions
        /// </summary>
        public IReadOnlyList<ScheduleFilterDefinition> Filters { get; }
        
        /// <summary>
        /// Gets the sorting definitions
        /// </summary>
        public IReadOnlyList<ScheduleSortDefinition> Sorting { get; }
        
        /// <summary>
        /// Gets the grouping definitions
        /// </summary>
        public IReadOnlyList<ScheduleGroupDefinition> Grouping { get; }
        
        /// <summary>
        /// Gets the formatting configuration
        /// </summary>
        public ScheduleFormatting Formatting { get; }
        
        /// <summary>
        /// Gets the discipline this schedule belongs to
        /// </summary>
        public string Discipline { get; }
        
        /// <summary>
        /// Gets additional metadata
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }

        /// <summary>
        /// Gets the totals configuration for this schedule
        /// </summary>
        public ScheduleTotalsConfiguration Totals { get; }

        /// <summary>
        /// Gets whether this schedule has totals configuration
        /// </summary>
        public bool HasTotals => Totals != null && (Totals.ShowGrandTotals || Totals.TotalFields.Count > 0);

        #endregion

        #region Constructor
        
        /// <summary>
        /// Creates a new ScheduleTemplate
        /// </summary>
        public ScheduleTemplate(
            string name,
            string categoryName,
            ScheduleType type,
            List<ScheduleFieldDefinition> fields,
            List<ScheduleFilterDefinition> filters = null,
            List<ScheduleSortDefinition> sorting = null,
            List<ScheduleGroupDefinition> grouping = null,
            ScheduleFormatting formatting = null,
            string discipline = null,
            Dictionary<string, string> metadata = null,
            ScheduleTotalsConfiguration totals = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Schedule name cannot be null or empty", nameof(name));
            if (string.IsNullOrWhiteSpace(categoryName))
                throw new ArgumentException("Category name cannot be null or empty", nameof(categoryName));
            if (fields == null || fields.Count == 0)
                throw new ArgumentException("At least one field must be defined", nameof(fields));
            
            Name = name;
            CategoryName = categoryName;
            Type = type;
            Fields = fields ?? new List<ScheduleFieldDefinition>();
            Filters = filters ?? new List<ScheduleFilterDefinition>();
            Sorting = sorting ?? new List<ScheduleSortDefinition>();
            Grouping = grouping ?? new List<ScheduleGroupDefinition>();
            Formatting = formatting ?? new ScheduleFormatting();
            Discipline = discipline ?? ExtractDiscipline(name);
            Metadata = metadata ?? new Dictionary<string, string>();
            Totals = totals;
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Extracts discipline from schedule name
        /// </summary>
        private static string ExtractDiscipline(string scheduleName)
        {
            if (scheduleName.Contains("MEP", StringComparison.OrdinalIgnoreCase) ||
                scheduleName.Contains("Mechanical", StringComparison.OrdinalIgnoreCase) ||
                scheduleName.Contains("Electrical", StringComparison.OrdinalIgnoreCase) ||
                scheduleName.Contains("Plumbing", StringComparison.OrdinalIgnoreCase))
                return "MEP";
            
            if (scheduleName.Contains("Arch", StringComparison.OrdinalIgnoreCase) ||
                scheduleName.Contains("Door", StringComparison.OrdinalIgnoreCase) ||
                scheduleName.Contains("Window", StringComparison.OrdinalIgnoreCase) ||
                scheduleName.Contains("Room", StringComparison.OrdinalIgnoreCase))
                return "Architecture";
            
            if (scheduleName.Contains("Struct", StringComparison.OrdinalIgnoreCase))
                return "Structural";
            
            return "General";
        }
        
        /// <summary>
        /// Gets all parameter names referenced by this schedule
        /// </summary>
        public List<string> GetReferencedParameters()
        {
            var parameters = new HashSet<string>();
            
            // Add field parameters
            foreach (var field in Fields)
            {
                parameters.Add(field.ParameterName);
            }
            
            // Add filter parameters
            foreach (var filter in Filters)
            {
                parameters.Add(filter.ParameterName);
            }
            
            // Add sorting parameters
            foreach (var sort in Sorting)
            {
                parameters.Add(sort.ParameterName);
            }
            
            // Add grouping parameters
            foreach (var group in Grouping)
            {
                parameters.Add(group.ParameterName);
            }
            
            return parameters.ToList();
        }
        
        /// <summary>
        /// Checks if schedule has any filters
        /// </summary>
        public bool HasFilters => Filters.Count > 0;
        
        /// <summary>
        /// Checks if schedule has any sorting
        /// </summary>
        public bool HasSorting => Sorting.Count > 0;
        
        /// <summary>
        /// Checks if schedule has any grouping
        /// </summary>
        public bool HasGrouping => Grouping.Count > 0;
        
        /// <summary>
        /// Checks if schedule has custom formatting
        /// </summary>
        public bool HasCustomFormatting =>
            Formatting.HeaderColor != null ||
            Formatting.TextColor != null ||
            Formatting.AlternatingRowColor != null;

        #endregion

        #region Equality
        
        public bool Equals(ScheduleTemplate other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }
        
        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is ScheduleTemplate other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
        }
        
        #endregion

        #region String Representation
        
        public override string ToString()
        {
            return $"{Name} ({CategoryName}) - {Fields.Count} fields";
        }
        
        #endregion

        #region Factory Methods
        
        /// <summary>
        /// Creates a ScheduleTemplate from CSV line
        /// </summary>
        public static ScheduleTemplate FromCsvLine(string line, ScheduleType scheduleType = ScheduleType.Standard, string discipline = null, char delimiter = ',')
        {
            var parts = line.Split(delimiter);
            if (parts.Length < 3)
                throw new ArgumentException($"Invalid CSV line format: {line}");
            
            string name = parts[0].Trim();
            string category = parts[1].Trim();
            string fieldsStr = parts[2].Trim();
            
            // Parse fields
            var fields = ParseFields(fieldsStr);
            
            // Parse optional filters (column 3)
            var filters = parts.Length > 3 && !string.IsNullOrEmpty(parts[3])
                ? ParseFilters(parts[3].Trim())
                : null;
            
            // Parse optional sorting (column 4)
            var sorting = parts.Length > 4 && !string.IsNullOrEmpty(parts[4])
                ? ParseSorting(parts[4].Trim())
                : null;
            
            // Parse optional formatting (columns 6-8)
            var formatting = new ScheduleFormatting();
            if (parts.Length > 6 && !string.IsNullOrEmpty(parts[6]))
                formatting.HeaderColor = ParseColor(parts[6].Trim());
            if (parts.Length > 7 && !string.IsNullOrEmpty(parts[7]))
                formatting.TextColor = ParseColor(parts[7].Trim());
            
            return new ScheduleTemplate(
                name,
                category,
                scheduleType,
                fields,
                filters,
                sorting,
                null,
                formatting);
        }
        
        /// <summary>
        /// Parses field definitions from string
        /// Format: "Field1;Field2;Field3"
        /// </summary>
        private static List<ScheduleFieldDefinition> ParseFields(string fieldsStr)
        {
            var fields = new List<ScheduleFieldDefinition>();
            
            if (string.IsNullOrWhiteSpace(fieldsStr))
                return fields;
            
            var fieldNames = fieldsStr.Split(';');
            int order = 0;
            
            foreach (var fieldName in fieldNames)
            {
                if (!string.IsNullOrWhiteSpace(fieldName))
                {
                    fields.Add(new ScheduleFieldDefinition(fieldName.Trim(), order++));
                }
            }
            
            return fields;
        }
        
        /// <summary>
        /// Parses filter definitions from string
        /// Format: "Field1=Value1;Field2>Value2"
        /// </summary>
        private static List<ScheduleFilterDefinition> ParseFilters(string filtersStr)
        {
            var filters = new List<ScheduleFilterDefinition>();
            
            if (string.IsNullOrWhiteSpace(filtersStr))
                return filters;
            
            var filterParts = filtersStr.Split(';');
            
            foreach (var filterPart in filterParts)
            {
                if (string.IsNullOrWhiteSpace(filterPart))
                    continue;
                
                // Parse operator
                ScheduleFilterType filterType = ScheduleFilterType.Equal;
                string paramName = filterPart;
                string value = "";
                
                if (filterPart.Contains(">="))
                {
                    filterType = ScheduleFilterType.GreaterOrEqual;
                    var parts = filterPart.Split(new[] { ">=" }, StringSplitOptions.None);
                    paramName = parts[0].Trim();
                    value = parts.Length > 1 ? parts[1].Trim() : "";
                }
                else if (filterPart.Contains("<="))
                {
                    filterType = ScheduleFilterType.LessOrEqual;
                    var parts = filterPart.Split(new[] { "<=" }, StringSplitOptions.None);
                    paramName = parts[0].Trim();
                    value = parts.Length > 1 ? parts[1].Trim() : "";
                }
                else if (filterPart.Contains("!="))
                {
                    filterType = ScheduleFilterType.NotEqual;
                    var parts = filterPart.Split(new[] { "!=" }, StringSplitOptions.None);
                    paramName = parts[0].Trim();
                    value = parts.Length > 1 ? parts[1].Trim() : "";
                }
                else if (filterPart.Contains(">"))
                {
                    filterType = ScheduleFilterType.GreaterThan;
                    var parts = filterPart.Split('>');
                    paramName = parts[0].Trim();
                    value = parts.Length > 1 ? parts[1].Trim() : "";
                }
                else if (filterPart.Contains("<"))
                {
                    filterType = ScheduleFilterType.LessThan;
                    var parts = filterPart.Split('<');
                    paramName = parts[0].Trim();
                    value = parts.Length > 1 ? parts[1].Trim() : "";
                }
                else if (filterPart.Contains("="))
                {
                    filterType = ScheduleFilterType.Equal;
                    var parts = filterPart.Split('=');
                    paramName = parts[0].Trim();
                    value = parts.Length > 1 ? parts[1].Trim() : "";
                }
                
                filters.Add(new ScheduleFilterDefinition(paramName, filterType, value));
            }
            
            return filters;
        }
        
        /// <summary>
        /// Parses sorting definitions from string
        /// Format: "Field1 ASC;Field2 DESC"
        /// </summary>
        private static List<ScheduleSortDefinition> ParseSorting(string sortingStr)
        {
            var sorting = new List<ScheduleSortDefinition>();
            
            if (string.IsNullOrWhiteSpace(sortingStr))
                return sorting;
            
            var sortParts = sortingStr.Split(';');
            int order = 0;
            
            foreach (var sortPart in sortParts)
            {
                if (string.IsNullOrWhiteSpace(sortPart))
                    continue;
                
                var parts = sortPart.Trim().Split(' ');
                string paramName = parts[0];
                bool ascending = parts.Length < 2 || 
                                !parts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase);
                
                sorting.Add(new ScheduleSortDefinition(paramName, ascending, order++));
            }
            
            return sorting;
        }
        
        /// <summary>
        /// Parses color from string (hex format)
        /// Returns the color string as-is (e.g., "#FF0000" or "FF0000")
        /// </summary>
        private static string ParseColor(string colorStr)
        {
            if (string.IsNullOrWhiteSpace(colorStr))
                return null;

            return colorStr.Trim();
        }
        
        /// <summary>
        /// Creates a builder for fluent template creation
        /// </summary>
        public static ScheduleTemplateBuilder Builder()
        {
            return new ScheduleTemplateBuilder();
        }
        
        #endregion
    }
    
    #region Support Classes
    
    /// <summary>
    /// Represents a schedule field definition
    /// </summary>
    public class ScheduleFieldDefinition
    {
        public string ParameterName { get; }
        public int DisplayOrder { get; }
        public string Heading { get; set; }
        public bool ShowHeader { get; set; }
        public int Width { get; set; }
        public HorizontalAlignmentStyle Alignment { get; set; }
        public bool IsCalculatedValue { get; set; }
        public bool IsCalculated { get => IsCalculatedValue; set => IsCalculatedValue = value; }
        public string Formula { get; set; }

        /// <summary>
        /// Gets or sets the column header text (alias for Heading)
        /// </summary>
        public string Header { get => Heading; set => Heading = value; }

        /// <summary>
        /// Gets or sets whether this field is visible (alias for ShowHeader)
        /// </summary>
        public bool IsVisible { get => ShowHeader; set => ShowHeader = value; }

        /// <summary>
        /// Gets or sets whether to calculate totals for this field
        /// </summary>
        public bool CalculateTotals { get; set; }

        /// <summary>
        /// Gets or sets the column width in mm (nullable for auto-width)
        /// </summary>
        public double? ColumnWidth { get; set; }

        public ScheduleFieldDefinition(string parameterName, int displayOrder)
        {
            ParameterName = parameterName ?? throw new ArgumentNullException(nameof(parameterName));
            DisplayOrder = displayOrder;
            ShowHeader = true;
            Width = -1; // Auto width
            Alignment = HorizontalAlignmentStyle.Left;
            IsCalculatedValue = false;
            CalculateTotals = false;
        }
    }
    
    /// <summary>
    /// Represents a schedule filter definition
    /// </summary>
    public class ScheduleFilterDefinition
    {
        public string ParameterName { get; }
        public ScheduleFilterType FilterType { get; }
        public string Value { get; }
        
        public ScheduleFilterDefinition(string parameterName, ScheduleFilterType filterType, string value)
        {
            ParameterName = parameterName ?? throw new ArgumentNullException(nameof(parameterName));
            FilterType = filterType;
            Value = value ?? string.Empty;
        }
    }
    
    /// <summary>
    /// Represents a schedule sorting definition
    /// </summary>
    public class ScheduleSortDefinition
    {
        public string ParameterName { get; }
        public bool Ascending { get; }
        public int SortOrder { get; }
        
        public ScheduleSortDefinition(string parameterName, bool ascending, int sortOrder)
        {
            ParameterName = parameterName ?? throw new ArgumentNullException(nameof(parameterName));
            Ascending = ascending;
            SortOrder = sortOrder;
        }
    }
    
    /// <summary>
    /// Represents a schedule grouping definition
    /// </summary>
    public class ScheduleGroupDefinition
    {
        public string ParameterName { get; }
        public bool ShowHeader { get; }
        public bool ShowFooter { get; }
        public bool ShowBlankLine { get; }
        
        public ScheduleGroupDefinition(
            string parameterName, 
            bool showHeader = true, 
            bool showFooter = false,
            bool showBlankLine = true)
        {
            ParameterName = parameterName ?? throw new ArgumentNullException(nameof(parameterName));
            ShowHeader = showHeader;
            ShowFooter = showFooter;
            ShowBlankLine = showBlankLine;
        }
    }
    
    /// <summary>
    /// Represents schedule formatting configuration
    /// </summary>
    public class ScheduleFormatting
    {
        public string HeaderColor { get; set; }
        public string TextColor { get; set; }
        public string AlternatingRowColor { get; set; }
        public bool BoldHeaders { get; set; }
        public double? TextSize { get; set; }
        public string FontName { get; set; }
        public bool ShowGridLines { get; set; }
        public bool ShowOutlines { get; set; }
        public bool AlternateRowColors { get; set; }

        public ScheduleFormatting()
        {
            BoldHeaders = true;
            TextSize = 3.0; // mm
            FontName = "Arial";
            ShowGridLines = true;
            ShowOutlines = true;
        }
    }
    
    /// <summary>
    /// Schedule types
    /// </summary>
    public enum ScheduleType
    {
        Standard,
        Regular = Standard,
        MaterialTakeoff,
        KeySchedule,
        NoteBlock,
        ViewList,
        SheetList,
        RevisionSchedule
    }
    
    /// <summary>
    /// Horizontal alignment styles for schedule fields
    /// </summary>
    public enum HorizontalAlignmentStyle
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Schedule field alignment options (maps to HorizontalAlignmentStyle)
    /// </summary>
    public enum ScheduleFieldAlignment
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Schedule data configuration for formatting and display
    /// </summary>
    public class ScheduleDataConfiguration
    {
        public string ScheduleId { get; set; }
        public string Name { get; set; }
        public ScheduleType Type { get; set; } = ScheduleType.Standard;
        public List<ScheduleFieldDefinition> Fields { get; set; } = new List<ScheduleFieldDefinition>();
        public ScheduleFormatting Formatting { get; set; } = new ScheduleFormatting();
        public bool ShowTitle { get; set; } = true;
        public bool ShowHeaders { get; set; } = true;
        public bool StripedRows { get; set; } = false;
    }

    #endregion
    
    #region Builder
    
    /// <summary>
    /// Fluent builder for ScheduleTemplate
    /// </summary>
    public class ScheduleTemplateBuilder
    {
        private string _name;
        private string _categoryName;
        private ScheduleType _type = ScheduleType.Standard;
        private readonly List<ScheduleFieldDefinition> _fields = new List<ScheduleFieldDefinition>();
        private readonly List<ScheduleFilterDefinition> _filters = new List<ScheduleFilterDefinition>();
        private readonly List<ScheduleSortDefinition> _sorting = new List<ScheduleSortDefinition>();
        private readonly List<ScheduleGroupDefinition> _grouping = new List<ScheduleGroupDefinition>();
        private ScheduleFormatting _formatting = new ScheduleFormatting();
        private string _discipline;
        private readonly Dictionary<string, string> _metadata = new Dictionary<string, string>();
        
        public ScheduleTemplateBuilder WithName(string name)
        {
            _name = name;
            return this;
        }
        
        public ScheduleTemplateBuilder ForCategory(string categoryName)
        {
            _categoryName = categoryName;
            return this;
        }
        
        public ScheduleTemplateBuilder WithType(ScheduleType type)
        {
            _type = type;
            return this;
        }
        
        public ScheduleTemplateBuilder AddField(string parameterName, int displayOrder = -1)
        {
            int order = displayOrder >= 0 ? displayOrder : _fields.Count;
            _fields.Add(new ScheduleFieldDefinition(parameterName, order));
            return this;
        }
        
        public ScheduleTemplateBuilder AddFilter(string parameterName, ScheduleFilterType filterType, string value)
        {
            _filters.Add(new ScheduleFilterDefinition(parameterName, filterType, value));
            return this;
        }
        
        public ScheduleTemplateBuilder AddSorting(string parameterName, bool ascending = true)
        {
            _sorting.Add(new ScheduleSortDefinition(parameterName, ascending, _sorting.Count));
            return this;
        }
        
        public ScheduleTemplateBuilder AddGrouping(string parameterName, bool showHeader = true)
        {
            _grouping.Add(new ScheduleGroupDefinition(parameterName, showHeader));
            return this;
        }
        
        public ScheduleTemplateBuilder WithFormatting(Action<ScheduleFormatting> configure)
        {
            configure?.Invoke(_formatting);
            return this;
        }
        
        public ScheduleTemplateBuilder WithDiscipline(string discipline)
        {
            _discipline = discipline;
            return this;
        }
        
        public ScheduleTemplateBuilder AddMetadata(string key, string value)
        {
            _metadata[key] = value;
            return this;
        }
        
        public ScheduleTemplate Build()
        {
            return new ScheduleTemplate(
                _name,
                _categoryName,
                _type,
                _fields,
                _filters,
                _sorting,
                _grouping,
                _formatting,
                _discipline,
                _metadata);
        }
    }
    
    #endregion
}
