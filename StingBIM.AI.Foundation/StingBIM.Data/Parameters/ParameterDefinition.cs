using System;
using System.Collections.Generic;
using System.Linq;

namespace StingBIM.Data.Parameters
{
    /// <summary>
    /// Custom parameter type enum replacing the deprecated Revit ParameterType.
    /// Maps internal data types for Revit 2025+ compatibility.
    /// </summary>
    public enum StingBIMParameterType
    {
        Text,
        Integer,
        Number,
        Length,
        Area,
        Volume,
        Angle,
        URL,
        YesNo,
        Currency,
        ElectricalCurrent,
        ElectricalPotential,
        ElectricalPower,
        Invalid
    }

    /// <summary>
    /// Represents a Revit shared parameter definition with all metadata
    /// Parsed from MR_PARAMETERS.txt format
    /// </summary>
    public class ParameterDefinition
    {
        #region Properties
        
        /// <summary>
        /// Gets or sets the parameter GUID (unique identifier)
        /// </summary>
        public Guid Guid { get; set; }
        
        /// <summary>
        /// Gets or sets the parameter name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the data type (TEXT, LENGTH, NUMBER, etc.)
        /// </summary>
        public string DataType { get; set; }
        
        /// <summary>
        /// Gets or sets the data category (for unit-based parameters)
        /// </summary>
        public string DataCategory { get; set; }
        
        /// <summary>
        /// Gets or sets the parameter group ID
        /// </summary>
        public int GroupId { get; set; }
        
        /// <summary>
        /// Gets or sets the parameter group name
        /// </summary>
        public string GroupName { get; set; }
        
        /// <summary>
        /// Gets or sets whether the parameter is visible in UI
        /// </summary>
        public bool IsVisible { get; set; }
        
        /// <summary>
        /// Gets or sets the parameter description
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Gets or sets whether the parameter is user modifiable
        /// </summary>
        public bool IsUserModifiable { get; set; }
        
        /// <summary>
        /// Gets or sets whether to hide when no value is present
        /// </summary>
        public bool HideWhenNoValue { get; set; }
        
        /// <summary>
        /// Gets or sets the parameter type enumeration
        /// Converted from DataType string
        /// </summary>
        public StingBIMParameterType RevitParameterType { get; set; }
        
        /// <summary>
        /// Gets or sets the category names this parameter is bound to
        /// </summary>
        public List<string> BoundCategories { get; set; }
        
        /// <summary>
        /// Gets or sets whether this parameter has a formula
        /// </summary>
        public bool HasFormula { get; set; }
        
        /// <summary>
        /// Gets or sets the formula expression (if any)
        /// </summary>
        public string Formula { get; set; }
        
        /// <summary>
        /// Gets or sets the discipline (Architecture, MEP, Structural, etc.)
        /// Extracted from parameter name prefix
        /// </summary>
        public string Discipline { get; set; }
        
        /// <summary>
        /// Gets or sets the system/subsystem (from group name)
        /// </summary>
        public string System { get; set; }
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes a new instance of ParameterDefinition
        /// </summary>
        public ParameterDefinition()
        {
            BoundCategories = new List<string>();
            IsVisible = true;
            IsUserModifiable = true;
            HideWhenNoValue = false;
        }
        
        #endregion

        #region Factory Methods
        
        /// <summary>
        /// Creates a ParameterDefinition from a shared parameter file line
        /// Format: PARAM GUID NAME DATATYPE DATACATEGORY GROUP VISIBLE DESCRIPTION USERMODIFIABLE HIDEWHENNOVALUE
        /// </summary>
        /// <param name="line">Line from MR_PARAMETERS.txt</param>
        /// <param name="groups">Dictionary of group ID to group name</param>
        /// <returns>ParameterDefinition instance</returns>
        public static ParameterDefinition FromSharedParameterLine(string line, Dictionary<int, string> groups)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentException("Line cannot be null or empty", nameof(line));
            
            if (!line.StartsWith("PARAM", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Line must start with 'PARAM'", nameof(line));
            
            var parts = line.Split('\t');
            if (parts.Length < 10)
                throw new ArgumentException($"Invalid parameter line format. Expected 10 parts, got {parts.Length}", nameof(line));
            
            var param = new ParameterDefinition
            {
                Guid = ParseGuid(parts[1]),
                Name = parts[2],
                DataType = parts[3],
                DataCategory = parts[4],
                GroupId = ParseInt(parts[5]),
                IsVisible = ParseBool(parts[6]),
                Description = parts[7],
                IsUserModifiable = ParseBool(parts[8]),
                HideWhenNoValue = ParseBool(parts[9])
            };
            
            // Set group name from dictionary
            if (groups != null && groups.ContainsKey(param.GroupId))
            {
                param.GroupName = groups[param.GroupId];
                param.System = MapGroupNameToSystem(param.GroupName);
            }
            
            // Convert data type to Revit ParameterType
            param.RevitParameterType = ConvertDataTypeToParameterType(param.DataType);
            
            // Extract discipline from parameter name
            param.Discipline = ExtractDiscipline(param.Name);
            
            return param;
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Parses a GUID from string
        /// </summary>
        private static Guid ParseGuid(string guidString)
        {
            if (Guid.TryParse(guidString, out Guid result))
                return result;
            
            throw new ArgumentException($"Invalid GUID format: {guidString}");
        }
        
        /// <summary>
        /// Parses an integer from string
        /// </summary>
        private static int ParseInt(string intString)
        {
            if (int.TryParse(intString, out int result))
                return result;
            
            throw new ArgumentException($"Invalid integer format: {intString}");
        }
        
        /// <summary>
        /// Parses a boolean from string (1 = true, 0 = false)
        /// </summary>
        private static bool ParseBool(string boolString)
        {
            if (string.IsNullOrWhiteSpace(boolString))
                return false;
            
            return boolString == "1" || boolString.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Converts data type string to StingBIMParameterType
        /// </summary>
        private static StingBIMParameterType ConvertDataTypeToParameterType(string dataType)
        {
            switch (dataType?.ToUpperInvariant())
            {
                case "TEXT":
                    return StingBIMParameterType.Text;
                case "INTEGER":
                    return StingBIMParameterType.Integer;
                case "NUMBER":
                    return StingBIMParameterType.Number;
                case "LENGTH":
                    return StingBIMParameterType.Length;
                case "AREA":
                    return StingBIMParameterType.Area;
                case "VOLUME":
                    return StingBIMParameterType.Volume;
                case "ANGLE":
                    return StingBIMParameterType.Angle;
                case "URL":
                    return StingBIMParameterType.URL;
                case "YESNO":
                    return StingBIMParameterType.YesNo;
                case "CURRENCY":
                    return StingBIMParameterType.Currency;
                case "ELECTRICAL_CURRENT":
                    return StingBIMParameterType.ElectricalCurrent;
                case "ELECTRICAL_POTENTIAL":
                    return StingBIMParameterType.ElectricalPotential;
                case "ELECTRICAL_POWER":
                    return StingBIMParameterType.ElectricalPower;
                default:
                    return StingBIMParameterType.Text; // Default to text for unknown types
            }
        }
        
        /// <summary>
        /// Extracts discipline from parameter name prefix
        /// Examples: BLE_WALL_HEIGHT_MM -> BLE (Building Elements)
        ///           ELC_CBL_SZ_MM -> ELC (Electrical)
        ///           HVC_DUCT_SZ_MM -> HVC (HVAC)
        /// </summary>
        private static string ExtractDiscipline(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                return "Unknown";
            
            // Check for common discipline prefixes
            var prefixes = new Dictionary<string, string>
            {
                { "BLE_", "Building Elements" },
                { "ELC_", "Electrical" },
                { "HVC_", "HVAC" },
                { "PLM_", "Plumbing" },
                { "STR_", "Structural" },
                { "ASS_", "Asset Management" },
                { "CST_", "Cost/Procurement" },
                { "PER_", "Performance" },
                { "PRJ_", "Project" },
                { "MAT_", "Materials" },
                { "FLS_", "Fire/Life Safety" },
                { "RGL_", "Regulatory" },
                { "LTG_", "Lighting" },
                { "COM_", "Communications" },
                { "PROP_", "Properties" }
            };
            
            foreach (var kvp in prefixes)
            {
                if (parameterName.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            
            return "General";
        }
        
        /// <summary>
        /// Maps group name to system/subsystem
        /// </summary>
        private static string MapGroupNameToSystem(string groupName)
        {
            var systemMap = new Dictionary<string, string>
            {
                { "PER_SUST", "Performance - Sustainability" },
                { "ASS_MNG", "Asset Management" },
                { "CST_PROC", "Cost & Procurement" },
                { "COM_DAT", "Communications & Data" },
                { "LTG_CONTROLS", "Lighting Controls" },
                { "TPL_TRACKING", "Template Tracking" },
                { "HVC_SYSTEMS", "HVAC Systems" },
                { "PLM_DRN", "Plumbing & Drainage" },
                { "ELC_PWR", "Electrical Power" },
                { "MAT_INFO", "Material Information" },
                { "PRJ_INFORMATION", "Project Information" },
                { "PROP_PHYSICAL", "Physical Properties" },
                { "FLS_LIFE_SFTY", "Fire & Life Safety" },
                { "BLE_ELES", "Building Elements" },
                { "RGL_CMPL", "Regulatory Compliance" }
            };
            
            return systemMap.TryGetValue(groupName, out string system) ? system : groupName;
        }
        
        #endregion

        #region Validation
        
        /// <summary>
        /// Validates the parameter definition
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValid()
        {
            if (Guid == Guid.Empty)
                return false;
            
            if (string.IsNullOrWhiteSpace(Name))
                return false;
            
            if (string.IsNullOrWhiteSpace(DataType))
                return false;
            
            if (GroupId < 0)
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Gets validation errors
        /// </summary>
        /// <returns>List of validation error messages</returns>
        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();
            
            if (Guid == Guid.Empty)
                errors.Add("GUID cannot be empty");
            
            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("Name cannot be null or empty");
            
            if (string.IsNullOrWhiteSpace(DataType))
                errors.Add("DataType cannot be null or empty");
            
            if (GroupId < 0)
                errors.Add("GroupId must be non-negative");
            
            return errors;
        }
        
        #endregion

        #region Utility Methods
        
        /// <summary>
        /// Gets a display name for the parameter (formatted)
        /// </summary>
        public string GetDisplayName()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return "Unknown Parameter";
            
            // Replace underscores with spaces and title case
            return Name.Replace('_', ' ');
        }
        
        /// <summary>
        /// Checks if parameter belongs to a specific discipline
        /// </summary>
        public bool BelongsToDiscipline(string discipline)
        {
            if (string.IsNullOrWhiteSpace(discipline))
                return false;
            
            return Discipline?.Equals(discipline, StringComparison.OrdinalIgnoreCase) ?? false;
        }
        
        /// <summary>
        /// Checks if parameter belongs to a specific system
        /// </summary>
        public bool BelongsToSystem(string system)
        {
            if (string.IsNullOrWhiteSpace(system))
                return false;
            
            return System?.Equals(system, StringComparison.OrdinalIgnoreCase) ?? false;
        }
        
        /// <summary>
        /// Creates a clone of this parameter definition
        /// </summary>
        public ParameterDefinition Clone()
        {
            return new ParameterDefinition
            {
                Guid = this.Guid,
                Name = this.Name,
                DataType = this.DataType,
                DataCategory = this.DataCategory,
                GroupId = this.GroupId,
                GroupName = this.GroupName,
                IsVisible = this.IsVisible,
                Description = this.Description,
                IsUserModifiable = this.IsUserModifiable,
                HideWhenNoValue = this.HideWhenNoValue,
                RevitParameterType = this.RevitParameterType,
                BoundCategories = new List<string>(this.BoundCategories),
                HasFormula = this.HasFormula,
                Formula = this.Formula,
                Discipline = this.Discipline,
                System = this.System
            };
        }
        
        #endregion

        #region Overrides
        
        /// <summary>
        /// Returns a string representation of the parameter
        /// </summary>
        public override string ToString()
        {
            return $"{Name} ({DataType}) - {Description}";
        }
        
        /// <summary>
        /// Determines equality based on GUID
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is ParameterDefinition other)
                return this.Guid.Equals(other.Guid);
            
            return false;
        }
        
        /// <summary>
        /// Gets hash code based on GUID
        /// </summary>
        public override int GetHashCode()
        {
            return Guid.GetHashCode();
        }
        
        #endregion
    }
    
    /// <summary>
    /// Collection of parameter definitions with search and filter capabilities
    /// </summary>
    public class ParameterDefinitionCollection
    {
        private readonly Dictionary<Guid, ParameterDefinition> _parametersByGuid;
        private readonly Dictionary<string, ParameterDefinition> _parametersByName;
        private readonly Dictionary<string, List<ParameterDefinition>> _parametersByDiscipline;
        private readonly Dictionary<string, List<ParameterDefinition>> _parametersBySystem;
        
        /// <summary>
        /// Gets the total count of parameters
        /// </summary>
        public int Count => _parametersByGuid.Count;
        
        /// <summary>
        /// Initializes a new collection
        /// </summary>
        public ParameterDefinitionCollection()
        {
            _parametersByGuid = new Dictionary<Guid, ParameterDefinition>();
            _parametersByName = new Dictionary<string, ParameterDefinition>(StringComparer.OrdinalIgnoreCase);
            _parametersByDiscipline = new Dictionary<string, List<ParameterDefinition>>(StringComparer.OrdinalIgnoreCase);
            _parametersBySystem = new Dictionary<string, List<ParameterDefinition>>(StringComparer.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Adds a parameter to the collection
        /// </summary>
        public void Add(ParameterDefinition parameter)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));
            
            _parametersByGuid[parameter.Guid] = parameter;
            _parametersByName[parameter.Name] = parameter;
            
            // Index by discipline
            if (!string.IsNullOrEmpty(parameter.Discipline))
            {
                if (!_parametersByDiscipline.ContainsKey(parameter.Discipline))
                    _parametersByDiscipline[parameter.Discipline] = new List<ParameterDefinition>();
                
                _parametersByDiscipline[parameter.Discipline].Add(parameter);
            }
            
            // Index by system
            if (!string.IsNullOrEmpty(parameter.System))
            {
                if (!_parametersBySystem.ContainsKey(parameter.System))
                    _parametersBySystem[parameter.System] = new List<ParameterDefinition>();
                
                _parametersBySystem[parameter.System].Add(parameter);
            }
        }
        
        /// <summary>
        /// Gets a parameter by GUID
        /// </summary>
        public ParameterDefinition GetByGuid(Guid guid)
        {
            return _parametersByGuid.TryGetValue(guid, out var param) ? param : null;
        }
        
        /// <summary>
        /// Gets a parameter by name
        /// </summary>
        public ParameterDefinition GetByName(string name)
        {
            return _parametersByName.TryGetValue(name, out var param) ? param : null;
        }
        
        /// <summary>
        /// Gets all parameters for a discipline
        /// </summary>
        public List<ParameterDefinition> GetByDiscipline(string discipline)
        {
            return _parametersByDiscipline.TryGetValue(discipline, out var list) 
                ? new List<ParameterDefinition>(list) 
                : new List<ParameterDefinition>();
        }
        
        /// <summary>
        /// Gets all parameters for a system
        /// </summary>
        public List<ParameterDefinition> GetBySystem(string system)
        {
            return _parametersBySystem.TryGetValue(system, out var list) 
                ? new List<ParameterDefinition>(list) 
                : new List<ParameterDefinition>();
        }
        
        /// <summary>
        /// Gets all parameters
        /// </summary>
        public List<ParameterDefinition> GetAll()
        {
            return _parametersByGuid.Values.ToList();
        }
        
        /// <summary>
        /// Gets all discipline names
        /// </summary>
        public List<string> GetDisciplines()
        {
            return _parametersByDiscipline.Keys.ToList();
        }
        
        /// <summary>
        /// Gets all system names
        /// </summary>
        public List<string> GetSystems()
        {
            return _parametersBySystem.Keys.ToList();
        }
    }

    /// <summary>
    /// Parameter types for Revit shared parameters
    /// Replaces deprecated Autodesk.Revit.DB.ParameterType in Revit 2025+
    /// </summary>
    public enum StingBIMParameterType
    {
        Text,
        Integer,
        Number,
        Length,
        Area,
        Volume,
        Angle,
        URL,
        YesNo,
        Currency,
        ElectricalCurrent,
        ElectricalPotential,
        ElectricalPower,
        Mass,
        Time,
        Speed,
        Force,
        Stress,
        HVACPressure,
        HVACAirflow,
        HVACDuctSize,
        HVACEnergy,
        PipingFlow,
        PipingPressure,
        Material,
        Image,
        Invalid
    }
}
