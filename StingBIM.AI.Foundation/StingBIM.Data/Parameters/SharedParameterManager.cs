// ============================================================================
// StingBIM Data - Shared Parameter Manager
// ISO 19650 compliant parameter management with GUID stability
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NLog;

namespace StingBIM.Data.Parameters
{
    /// <summary>
    /// Manages shared parameters for Revit integration.
    /// Ensures GUID stability and ISO 19650 compliance.
    /// </summary>
    public class SharedParameterManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, SharedParameter> _parameters;
        private readonly Dictionary<Guid, SharedParameter> _parametersByGuid;
        private readonly string _sharedParameterFilePath;

        public SharedParameterManager(string sharedParameterFilePath = null)
        {
            _parameters = new Dictionary<string, SharedParameter>(StringComparer.OrdinalIgnoreCase);
            _parametersByGuid = new Dictionary<Guid, SharedParameter>();
            _sharedParameterFilePath = sharedParameterFilePath;

            if (!string.IsNullOrEmpty(_sharedParameterFilePath) && File.Exists(_sharedParameterFilePath))
            {
                LoadFromFile(_sharedParameterFilePath);
            }
        }

        /// <summary>
        /// Gets a parameter by name.
        /// </summary>
        public SharedParameter GetParameter(string name)
        {
            return _parameters.TryGetValue(name, out var param) ? param : null;
        }

        /// <summary>
        /// Gets a parameter by GUID.
        /// </summary>
        public SharedParameter GetParameter(Guid guid)
        {
            return _parametersByGuid.TryGetValue(guid, out var param) ? param : null;
        }

        /// <summary>
        /// Gets all parameters in a group.
        /// </summary>
        public IEnumerable<SharedParameter> GetParametersByGroup(string groupName)
        {
            return _parameters.Values.Where(p =>
                p.Group.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets all parameters for a category.
        /// </summary>
        public IEnumerable<SharedParameter> GetParametersForCategory(string category)
        {
            return _parameters.Values.Where(p =>
                p.Categories.Contains(category, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Registers a new parameter (GUID will be stable based on name).
        /// </summary>
        public SharedParameter RegisterParameter(SharedParameterDefinition definition)
        {
            // Generate stable GUID from name to ensure consistency
            var guid = GenerateStableGuid(definition.Name);

            // Check if already exists
            if (_parametersByGuid.ContainsKey(guid))
            {
                Logger.Warn($"Parameter already registered: {definition.Name}");
                return _parametersByGuid[guid];
            }

            var parameter = new SharedParameter
            {
                Guid = guid,
                Name = definition.Name,
                DataType = definition.DataType,
                Group = definition.Group,
                Description = definition.Description,
                Categories = definition.Categories.ToList(),
                IsInstance = definition.IsInstance,
                IsISO19650Compliant = definition.IsISO19650Compliant,
                NamingConvention = definition.NamingConvention
            };

            _parameters[parameter.Name] = parameter;
            _parametersByGuid[parameter.Guid] = parameter;

            Logger.Info($"Registered parameter: {parameter.Name} [{parameter.Guid}]");
            return parameter;
        }

        /// <summary>
        /// Loads parameters from a shared parameter file.
        /// </summary>
        public void LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Warn($"Shared parameter file not found: {filePath}");
                return;
            }

            var lines = File.ReadAllLines(filePath);
            var currentGroup = "";

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("*GROUP"))
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 3)
                    {
                        currentGroup = parts[2];
                    }
                }
                else if (line.StartsWith("PARAM"))
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 6)
                    {
                        try
                        {
                            var param = new SharedParameter
                            {
                                Guid = Guid.Parse(parts[1]),
                                Name = parts[2],
                                DataType = ParseDataType(parts[3]),
                                Group = currentGroup,
                                IsVisible = parts[5] == "1"
                            };

                            if (parts.Length >= 7)
                            {
                                param.Description = parts[6];
                            }

                            _parameters[param.Name] = param;
                            _parametersByGuid[param.Guid] = param;
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, $"Failed to parse parameter line: {line}");
                        }
                    }
                }
            }

            Logger.Info($"Loaded {_parameters.Count} parameters from {filePath}");
        }

        /// <summary>
        /// Saves parameters to a shared parameter file.
        /// </summary>
        public void SaveToFile(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# StingBIM Shared Parameters");
            sb.AppendLine("# ISO 19650 Compliant");
            sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine("*META\tVERSION\tMINVERSION");
            sb.AppendLine("META\t2\t1");

            var groups = _parameters.Values.GroupBy(p => p.Group);
            var groupId = 1;

            foreach (var group in groups.OrderBy(g => g.Key))
            {
                sb.AppendLine($"*GROUP\t{groupId}\t{group.Key}");

                foreach (var param in group.OrderBy(p => p.Name))
                {
                    var dataTypeStr = GetDataTypeString(param.DataType);
                    var visible = param.IsVisible ? "1" : "0";
                    sb.AppendLine($"PARAM\t{param.Guid}\t{param.Name}\t{dataTypeStr}\t\t{visible}\t{param.Description ?? ""}");
                }

                groupId++;
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, sb.ToString());
            Logger.Info($"Saved {_parameters.Count} parameters to {filePath}");
        }

        /// <summary>
        /// Gets all registered parameters.
        /// </summary>
        public IReadOnlyList<SharedParameter> GetAllParameters()
        {
            return _parameters.Values.ToList();
        }

        /// <summary>
        /// Gets parameter groups.
        /// </summary>
        public IEnumerable<string> GetGroups()
        {
            return _parameters.Values.Select(p => p.Group).Distinct().OrderBy(g => g);
        }

        /// <summary>
        /// Generates a stable GUID from a parameter name.
        /// This ensures the same parameter always gets the same GUID.
        /// </summary>
        private Guid GenerateStableGuid(string name)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes($"StingBIM.Parameter.{name}"));
                return new Guid(hash);
            }
        }

        private ParameterDataType ParseDataType(string dataType)
        {
            return dataType?.ToUpperInvariant() switch
            {
                "TEXT" => ParameterDataType.Text,
                "INTEGER" => ParameterDataType.Integer,
                "NUMBER" => ParameterDataType.Number,
                "LENGTH" => ParameterDataType.Length,
                "AREA" => ParameterDataType.Area,
                "VOLUME" => ParameterDataType.Volume,
                "ANGLE" => ParameterDataType.Angle,
                "YESNO" => ParameterDataType.YesNo,
                "URL" => ParameterDataType.URL,
                "MATERIAL" => ParameterDataType.Material,
                "IMAGE" => ParameterDataType.Image,
                _ => ParameterDataType.Text
            };
        }

        private string GetDataTypeString(ParameterDataType dataType)
        {
            return dataType switch
            {
                ParameterDataType.Text => "TEXT",
                ParameterDataType.Integer => "INTEGER",
                ParameterDataType.Number => "NUMBER",
                ParameterDataType.Length => "LENGTH",
                ParameterDataType.Area => "AREA",
                ParameterDataType.Volume => "VOLUME",
                ParameterDataType.Angle => "ANGLE",
                ParameterDataType.YesNo => "YESNO",
                ParameterDataType.URL => "URL",
                ParameterDataType.Material => "MATERIAL",
                ParameterDataType.Image => "IMAGE",
                _ => "TEXT"
            };
        }
    }

    /// <summary>
    /// Shared parameter definition.
    /// </summary>
    public class SharedParameter
    {
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public ParameterDataType DataType { get; set; }
        public string Group { get; set; }
        public string Description { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public bool IsInstance { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public bool IsISO19650Compliant { get; set; }
        public string NamingConvention { get; set; }
    }

    /// <summary>
    /// Parameter definition for registration.
    /// </summary>
    public class SharedParameterDefinition
    {
        public string Name { get; set; }
        public ParameterDataType DataType { get; set; }
        public string Group { get; set; }
        public string Description { get; set; }
        public IEnumerable<string> Categories { get; set; } = new List<string>();
        public bool IsInstance { get; set; } = true;
        public bool IsISO19650Compliant { get; set; }
        public string NamingConvention { get; set; }
    }

    /// <summary>
    /// Parameter data types.
    /// </summary>
    public enum ParameterDataType
    {
        Text,
        Integer,
        Number,
        Length,
        Area,
        Volume,
        Angle,
        YesNo,
        URL,
        Material,
        Image,
        FamilyType,
        MultilineText
    }
}
