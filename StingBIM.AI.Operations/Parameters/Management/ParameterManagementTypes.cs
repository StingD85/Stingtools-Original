using System;
using System.Collections.Generic;

namespace StingBIM.AI.Parameters.Management
{
    #region Enums

    public enum OperationType
    {
        BatchCreate,
        BatchUpdate,
        BatchDelete,
        Import,
        Export,
        Synchronize
    }

    public enum ConflictSeverity
    {
        Info,
        Warning,
        Critical
    }

    public enum ConflictType
    {
        DataTypeMismatch,
        GuidMismatch,
        NamingViolation,
        GroupMismatch,
        FormulaMismatch
    }

    public enum SyncStatus
    {
        Success,
        CompletedWithErrors,
        Failed
    }

    public enum FamilyOpStatus
    {
        Success,
        FileNotFound,
        NoChanges,
        Failed
    }

    public enum ResolutionAction
    {
        StandardizeGuid,
        Rename,
        ChangeType,
        ChangeGroup,
        Skip
    }

    public enum ExportFormat
    {
        CSV,
        Excel,
        JSON,
        SharedParameterFile,
        FamilyParameterFile
    }

    public enum SuggestionPriority
    {
        Required,
        Recommended,
        Optional
    }

    public enum BatchOperationStatus
    {
        Success,
        Failed,
        ValidationFailed,
        ConflictsDetected,
        CompletedWithErrors
    }

    public enum ResolutionStrategy
    {
        Auto,
        Manual,
        Conservative,
        Aggressive
    }

    #endregion

    #region Core Parameter Types

    public class SharedParameterDefinition
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string Group { get; set; }
        public Guid Guid { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Discipline { get; set; }
        public string Category { get; set; }
        public bool IsVisible { get; set; } = true;
        public string Description { get; set; }
        public string Formula { get; set; }
        public bool IsInstance { get; set; } = true;
        public string DataCategory { get; set; }
        public int UsageCount { get; set; }

        public SharedParameterDefinition Clone()
        {
            return new SharedParameterDefinition
            {
                Name = Name,
                DataType = DataType,
                Group = Group,
                Guid = Guid,
                CreatedAt = CreatedAt,
                Discipline = Discipline,
                Category = Category,
                IsVisible = IsVisible,
                Description = Description,
                Formula = Formula,
                IsInstance = IsInstance,
                DataCategory = DataCategory,
                UsageCount = UsageCount
            };
        }
    }

    public class ParameterDefinitionTemplate
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string Group { get; set; }
        public bool IsInstance { get; set; } = true;
        public List<string> ApplicableCategories { get; set; } = new List<string>();
        public string Formula { get; set; }
        public string Description { get; set; }
        public Guid Guid { get; set; } = Guid.NewGuid();
    }

    public class ParameterTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public List<ParameterDefinitionTemplate> Parameters { get; set; } = new List<ParameterDefinitionTemplate>();
    }

    public class FamilyParameterProfile
    {
        public string Category { get; set; }
        public List<string> RequiredParameters { get; set; } = new List<string>();
        public List<string> RecommendedParameters { get; set; } = new List<string>();
        public Dictionary<string, string> DefaultFormulas { get; set; } = new Dictionary<string, string>();
    }

    #endregion

    #region Conflict Types

    public class ParameterConflict
    {
        public string ParameterName { get; set; }
        public ConflictType Type { get; set; }
        public string ExistingValue { get; set; }
        public string NewValue { get; set; }
        public ConflictSeverity Severity { get; set; }
        public List<string> AffectedFamilies { get; set; } = new List<string>();
        public string SuggestedResolution { get; set; }
    }

    public class ConflictAnalysisResult
    {
        public List<ParameterConflict> GuidConflicts { get; set; } = new List<ParameterConflict>();
        public List<ParameterConflict> TypeConflicts { get; set; } = new List<ParameterConflict>();
        public List<ParameterConflict> GroupConflicts { get; set; } = new List<ParameterConflict>();
        public List<ParameterConflict> NamingViolations { get; set; } = new List<ParameterConflict>();
        public List<ParameterConflict> FormulaConflicts { get; set; } = new List<ParameterConflict>();
        public List<ResolutionSuggestion> ResolutionSuggestions { get; set; } = new List<ResolutionSuggestion>();
        public List<ParameterConflict> AllConflicts
        {
            get
            {
                var all = new List<ParameterConflict>();
                all.AddRange(GuidConflicts);
                all.AddRange(TypeConflicts);
                all.AddRange(GroupConflicts);
                all.AddRange(NamingViolations);
                all.AddRange(FormulaConflicts);
                return all;
            }
        }
    }

    public class ConflictResolution
    {
        public string ParameterName { get; set; }
        public ConflictType ConflictType { get; set; }
        public string Action { get; set; }
        public bool Success { get; set; }
    }

    public class ConflictResolutionResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<ConflictResolution> Resolutions { get; set; } = new List<ConflictResolution>();
        public int ResolvedCount { get; set; }
        public int UnresolvedCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class ResolutionSuggestion
    {
        public string ConflictId { get; set; }
        public string Description { get; set; }
        public ResolutionAction Action { get; set; }
        public string Impact { get; set; }
    }

    #endregion

    #region Batch Operation Types

    public class BatchParameterSpecification
    {
        public List<ParameterDefinitionTemplate> Parameters { get; set; } = new List<ParameterDefinitionTemplate>();
        public List<string> TargetCategories { get; set; } = new List<string>();
        public string Name { get; set; }
    }

    public class BatchCreationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public BatchParameterSpecification Specification { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<ParameterConflict> Conflicts { get; set; } = new List<ParameterConflict>();
        public BatchOperationStatus Status { get; set; }
        public List<SharedParameterDefinition> CreatedParameters { get; set; } = new List<SharedParameterDefinition>();
        public int ParameterCount { get; set; }
    }

    public class BatchOperationProgress
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string Message { get; set; }
        public string CurrentItem { get; set; }
        public double PercentComplete { get; set; }
    }

    public class ParameterOperation
    {
        public OperationType Type { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public int ParameterCount { get; set; }
        public string Details { get; set; }
    }

    #endregion

    #region Family Sync Types

    public class FamilyInfo
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public List<string> Parameters { get; set; } = new List<string>();
        public List<FamilyParameterInfo> ParameterDetails { get; set; } = new List<FamilyParameterInfo>();
    }

    public class FamilyParameterInfo
    {
        public string Name { get; set; }
        public Guid Guid { get; set; }
        public string DataType { get; set; }
    }

    public class SynchronizationOptions
    {
        public bool IncludeRecommended { get; set; } = true;
        public bool RemoveNonStandard { get; set; }
        public bool PreviewOnly { get; set; }
        public string TemplateId { get; set; }
    }

    public class SynchronizationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<FamilySyncResult> FamilyResults { get; set; } = new List<FamilySyncResult>();
        public int TotalParametersAdded { get; set; }
        public int TotalParametersModified { get; set; }
        public int TotalParametersRemoved { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public SyncStatus Status { get; set; }
    }

    public class FamilySyncResult
    {
        public string FamilyName { get; set; }
        public string FamilyPath { get; set; }
        public List<string> ParametersToAdd { get; set; } = new List<string>();
        public List<string> ParametersToRemove { get; set; } = new List<string>();
        public int ParametersAdded { get; set; }
        public int ParametersModified { get; set; }
        public int ParametersRemoved { get; set; }
    }

    public class BatchFamilyResult
    {
        public List<FamilyOperationResult> Results { get; set; } = new List<FamilyOperationResult>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalFamilies { get; set; }
        public int SuccessfulFamilies { get; set; }
        public int FailedFamilies { get; set; }
    }

    public class BatchFamilyOptions
    {
        public bool SkipExisting { get; set; }
        public bool UpdateExisting { get; set; }
        public bool CreateBackup { get; set; }
    }

    public class FamilyOperationResult
    {
        public string FamilyPath { get; set; }
        public string FamilyName { get; set; }
        public FamilyOpStatus Status { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> SkippedParameters { get; set; } = new List<string>();
        public List<string> UpdatedParameters { get; set; } = new List<string>();
        public List<string> AddedParameters { get; set; } = new List<string>();
    }

    public class ParameterAddition
    {
        public string ParameterName { get; set; }
    }

    #endregion

    #region Suggestion Types

    public class ParameterSuggestion
    {
        public string ParameterName { get; set; }
        public string DataType { get; set; }
        public string Group { get; set; }
        public bool IsRequired { get; set; }
        public SuggestionPriority Priority { get; set; }
        public string Reason { get; set; }
        public SharedParameterDefinition ExistingDefinition { get; set; }
        public bool ExistsInSharedFile { get; set; }
    }

    public class FamilyAnalysis
    {
        public string FamilyName { get; set; }
        public string Category { get; set; }
        public bool Has3DGeometry { get; set; }
        public bool HasGeometry { get; set; }
        public List<string> ExistingParameters { get; set; } = new List<string>();
        public string ProjectContext { get; set; }
    }

    public class ParameterSuggestionResult
    {
        public string FamilyName { get; set; }
        public string Category { get; set; }
        public List<ParameterSuggestion> RequiredSuggestions { get; set; } = new List<ParameterSuggestion>();
        public List<ParameterSuggestion> RecommendedSuggestions { get; set; } = new List<ParameterSuggestion>();
        public List<FormulaSuggestion> FormulaSuggestions { get; set; } = new List<FormulaSuggestion>();
        public List<ParameterSuggestion> GeometryBasedSuggestions { get; set; } = new List<ParameterSuggestion>();
        public List<ParameterSuggestion> RelatedSuggestions { get; set; } = new List<ParameterSuggestion>();
        public List<ParameterSuggestion> ComplianceSuggestions { get; set; } = new List<ParameterSuggestion>();
        public List<ParameterSuggestion> LearnedSuggestions { get; set; } = new List<ParameterSuggestion>();
    }

    public class SuggestionOptions
    {
        public bool IncludeFormulaSuggestions { get; set; } = true;
        public bool IncludeGeometry { get; set; } = true;
        public bool IncludeRelated { get; set; } = true;
        public bool IncludeCompliance { get; set; } = true;
        public bool IncludeProjectPatterns { get; set; }
        public bool CheckCompliance { get; set; }
        public string BuildingCode { get; set; }
        public bool LearnFromProject { get; set; }
    }

    #endregion

    #region Version Control Types

    public class ParameterVersion
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }
        public SharedParameterDefinition Definition { get; set; }
    }

    public class ParameterSnapshot
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public List<SharedParameterDefinition> Parameters { get; set; } = new List<SharedParameterDefinition>();
    }

    public class SnapshotComparison
    {
        public string Snapshot1Id { get; set; }
        public string Snapshot2Id { get; set; }
        public List<string> Added { get; set; } = new List<string>();
        public List<string> Removed { get; set; } = new List<string>();
    }

    public class RollbackResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public int RestoredParameterCount { get; set; }
        public DateTime SnapshotDate { get; set; }
    }

    #endregion

    #region Report Types

    public class ParameterReport
    {
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public int TotalParameters { get; set; }
        public Dictionary<string, List<SharedParameterDefinition>> ByDiscipline { get; set; } = new Dictionary<string, List<SharedParameterDefinition>>();
        public Dictionary<string, List<SharedParameterDefinition>> ByCategory { get; set; } = new Dictionary<string, List<SharedParameterDefinition>>();
        public Dictionary<string, List<SharedParameterDefinition>> ByDataType { get; set; } = new Dictionary<string, List<SharedParameterDefinition>>();
        public List<SharedParameterDefinition> UnusedParameters { get; set; } = new List<SharedParameterDefinition>();
        public List<SharedParameterDefinition> ParametersWithFormulas { get; set; } = new List<SharedParameterDefinition>();
        public List<NamingViolation> NamingViolations { get; set; } = new List<NamingViolation>();
    }

    public class ReportOptions
    {
        public bool IncludeUsageAnalysis { get; set; } = true;
        public bool IncludeFormulaAnalysis { get; set; } = true;
        public bool IncludeNamingAnalysis { get; set; } = true;
    }

    public class NamingViolation
    {
        public string ParameterName { get; set; }
        public string Suggestion { get; set; }
    }

    #endregion

    #region Validation Types

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    #endregion

    #region Export/Import Types

    public class ExportResult
    {
        public ExportFormat Format { get; set; }
        public string OutputPath { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public int ExportedCount { get; set; }
    }

    public class ExportOptions
    {
        public List<string> SelectedParameters { get; set; } = new List<string>();
    }

    public class ImportResult
    {
        public string InputPath { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public int ImportedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
    }

    public class ImportOptions
    {
        public bool OverwriteExisting { get; set; }
    }

    public class TemplateApplicationOptions
    {
        public bool OverwriteExisting { get; set; }
        public bool IncludeFormulas { get; set; } = true;
        public bool ApplyCategoryDefaults { get; set; }
        public List<string> ExcludeParameters { get; set; } = new List<string>();
    }

    #endregion
}

namespace StingBIM.AI.Parameters.Integration
{
    /// <summary>
    /// Mapping between a parameter and a schedule field.
    /// </summary>
    public class ScheduleFieldMapping
    {
        public string ParameterName { get; set; }
        public string FieldName { get; set; }
        public string ScheduleName { get; set; }
        public string DataType { get; set; }
        public bool IsKey { get; set; }
    }
}
