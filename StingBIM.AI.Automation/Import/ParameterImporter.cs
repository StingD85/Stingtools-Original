// StingBIM.AI.Automation.Import.ParameterImporter
// CSV/Excel parameter import with validation and preview
// v4 Prompt Reference: Phase 7 — Import Engine

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;

namespace StingBIM.AI.Automation.Import
{
    /// <summary>
    /// Imports parameter values from CSV into Revit elements.
    /// Flow: Load CSV → Validate → Preview → Apply (with transaction).
    /// Supports matching by element ID, Mark, or Type Name.
    /// </summary>
    public class ParameterImporter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Load and validate a parameter import CSV.
        /// Returns a preview with validation results before applying.
        /// </summary>
        public ImportPreview LoadAndValidate(Document doc, string csvPath)
        {
            var preview = new ImportPreview
            {
                SourceFile = csvPath
            };

            if (!File.Exists(csvPath))
            {
                preview.Errors.Add("File not found.");
                return preview;
            }

            if (doc == null)
            {
                preview.Errors.Add("No active Revit document.");
                return preview;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length < 2)
                {
                    preview.Errors.Add("CSV file has no data rows.");
                    return preview;
                }

                var headers = ParseCsvLine(lines[0]);
                preview.Headers = headers.ToList();

                // Identify key column (ElementId, Mark, or TypeName)
                var keyColIndex = FindKeyColumn(headers);
                if (keyColIndex < 0)
                {
                    preview.Errors.Add("CSV must have a column named 'ElementId', 'Mark', or 'TypeName' to identify elements.");
                    return preview;
                }
                preview.KeyColumn = headers[keyColIndex];

                // Parse data rows
                for (int i = 1; i < lines.Length; i++)
                {
                    var cols = ParseCsvLine(lines[i]);
                    if (cols.Length < 2) continue;

                    var row = new ImportRow
                    {
                        RowNumber = i + 1,
                        KeyValue = cols.Length > keyColIndex ? cols[keyColIndex].Trim() : "",
                        ParameterValues = new Dictionary<string, string>()
                    };

                    // Map parameter columns
                    for (int c = 0; c < cols.Length && c < headers.Length; c++)
                    {
                        if (c == keyColIndex) continue;
                        var paramName = headers[c].Trim();
                        if (!string.IsNullOrWhiteSpace(paramName) && !string.IsNullOrWhiteSpace(cols[c]))
                            row.ParameterValues[paramName] = cols[c].Trim();
                    }

                    // Validate: can we find the element?
                    var element = FindElement(doc, preview.KeyColumn, row.KeyValue);
                    if (element != null)
                    {
                        row.ElementFound = true;
                        row.ElementCategory = element.Category?.Name ?? "";
                        row.MatchedElementId = element.Id;

                        // Validate each parameter exists
                        foreach (var (paramName, value) in row.ParameterValues)
                        {
                            var param = element.LookupParameter(paramName);
                            if (param == null)
                            {
                                row.Warnings.Add($"Parameter '{paramName}' not found on element.");
                            }
                            else if (param.IsReadOnly)
                            {
                                row.Warnings.Add($"Parameter '{paramName}' is read-only.");
                            }
                        }
                    }
                    else
                    {
                        row.Warnings.Add($"Element not found for {preview.KeyColumn}='{row.KeyValue}'");
                    }

                    preview.Rows.Add(row);
                }

                // Summary
                var found = preview.Rows.Count(r => r.ElementFound);
                var notFound = preview.Rows.Count - found;
                var totalParams = preview.Rows.Sum(r => r.ParameterValues.Count);

                preview.Summary = $"Loaded {preview.Rows.Count} rows from CSV.\n" +
                    $"Elements found: {found}, not found: {notFound}\n" +
                    $"Parameters to set: {totalParams}\n" +
                    (preview.Rows.Any(r => r.Warnings.Count > 0)
                        ? $"Warnings: {preview.Rows.Sum(r => r.Warnings.Count)}"
                        : "No warnings.");

                preview.IsValid = found > 0 && preview.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Parameter import validation failed");
                preview.Errors.Add($"Parse error: {ex.Message}");
            }

            return preview;
        }

        /// <summary>
        /// Apply validated parameter import to the Revit document.
        /// Must be called after LoadAndValidate returns a valid preview.
        /// </summary>
        public ImportResult Apply(Document doc, ImportPreview preview)
        {
            var result = new ImportResult();

            if (doc == null || preview == null || !preview.IsValid)
            {
                result.Error = "Invalid preview or no active document.";
                return result;
            }

            try
            {
                using (var t = new Transaction(doc, "StingBIM: Import Parameters"))
                {
                    t.Start();

                    foreach (var row in preview.Rows.Where(r => r.ElementFound))
                    {
                        var element = doc.GetElement(row.MatchedElementId);
                        if (element == null) continue;

                        foreach (var (paramName, value) in row.ParameterValues)
                        {
                            try
                            {
                                var param = element.LookupParameter(paramName);
                                if (param == null || param.IsReadOnly) continue;

                                bool set = SetParameterValue(param, value);
                                if (set)
                                    result.ParametersSet++;
                                else
                                    result.ParametersFailed++;
                            }
                            catch (Exception ex)
                            {
                                result.ParametersFailed++;
                                Logger.Debug($"Set param failed: {paramName} on {element.Id}: {ex.Message}");
                            }
                        }
                        result.ElementsUpdated++;
                    }

                    t.Commit();
                }

                result.Success = true;
                result.Message = $"Import complete: {result.ElementsUpdated} elements updated, " +
                    $"{result.ParametersSet} parameters set" +
                    (result.ParametersFailed > 0 ? $", {result.ParametersFailed} failed" : "") + ".";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Parameter import apply failed");
                result.Error = $"Import failed: {ex.Message}";
            }

            return result;
        }

        #region Private Helpers

        private int FindKeyColumn(string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var h = headers[i].Trim().ToLowerInvariant();
                if (h == "elementid" || h == "element_id" || h == "id") return i;
                if (h == "mark") return i;
                if (h == "typename" || h == "type_name" || h == "type") return i;
            }
            return -1;
        }

        private Element FindElement(Document doc, string keyColumn, string keyValue)
        {
            if (string.IsNullOrWhiteSpace(keyValue)) return null;

            var key = keyColumn.ToLowerInvariant();

            // By ElementId
            if (key == "elementid" || key == "element_id" || key == "id")
            {
                if (int.TryParse(keyValue, out var id))
                    return doc.GetElement(new ElementId(id));
            }

            // By Mark
            if (key == "mark")
            {
                var collector = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType();

                foreach (var e in collector)
                {
                    var mark = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
                    if (mark != null && mark.Equals(keyValue, StringComparison.OrdinalIgnoreCase))
                        return e;
                }
            }

            // By TypeName
            if (key == "typename" || key == "type_name" || key == "type")
            {
                var collector = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType();

                foreach (var e in collector)
                {
                    if (e.Name != null && e.Name.Equals(keyValue, StringComparison.OrdinalIgnoreCase))
                        return e;
                }
            }

            return null;
        }

        private bool SetParameterValue(Parameter param, string value)
        {
            if (param == null || param.IsReadOnly) return false;

            switch (param.StorageType)
            {
                case StorageType.String:
                    param.Set(value);
                    return true;

                case StorageType.Integer:
                    if (int.TryParse(value, out var intVal))
                    {
                        param.Set(intVal);
                        return true;
                    }
                    // Handle Yes/No
                    if (value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                        value.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        param.Set(1);
                        return true;
                    }
                    if (value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                        value.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        param.Set(0);
                        return true;
                    }
                    return false;

                case StorageType.Double:
                    if (double.TryParse(value, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var dblVal))
                    {
                        param.Set(dblVal);
                        return true;
                    }
                    return false;

                case StorageType.ElementId:
                    if (int.TryParse(value, out var eid))
                    {
                        param.Set(new ElementId(eid));
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            foreach (char c in line)
            {
                if (c == '"')
                    inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                    current.Append(c);
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        #endregion
    }

    #region Import Data Types

    public class ImportPreview
    {
        public string SourceFile { get; set; }
        public string KeyColumn { get; set; }
        public List<string> Headers { get; set; } = new List<string>();
        public List<ImportRow> Rows { get; set; } = new List<ImportRow>();
        public List<string> Errors { get; set; } = new List<string>();
        public string Summary { get; set; }
        public bool IsValid { get; set; }
    }

    public class ImportRow
    {
        public int RowNumber { get; set; }
        public string KeyValue { get; set; }
        public bool ElementFound { get; set; }
        public string ElementCategory { get; set; }
        public ElementId MatchedElementId { get; set; }
        public Dictionary<string, string> ParameterValues { get; set; } = new Dictionary<string, string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class ImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public int ElementsUpdated { get; set; }
        public int ParametersSet { get; set; }
        public int ParametersFailed { get; set; }
    }

    #endregion
}
