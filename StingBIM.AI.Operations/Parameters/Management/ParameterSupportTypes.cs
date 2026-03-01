// ParameterSupportTypes.cs
// StingBIM AI - Support types for IntelligentParameterManager and SmartFormulaBuilder
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace StingBIM.AI.Parameters.Management
{
    #region Shared Parameter Definition

    /// <summary>
    /// Represents a shared parameter definition used by the IntelligentParameterManager.
    /// Note: This is distinct from StingBIM.AI.Parameters.AutoPopulation.SharedParameterDefinition.
    /// </summary>
    public class SharedParameterDefinition
    {
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public string DataType { get; set; }
        public string DataCategory { get; set; }
        public string Group { get; set; }
        public bool IsVisible { get; set; }
        public string Description { get; set; }
        public bool IsInstance { get; set; }
        public string Formula { get; set; }
        public DateTime CreatedAt { get; set; }
        public int UsageCount { get; set; }
        public string Category { get; set; }
        public string Discipline { get; set; }

        public SharedParameterDefinition Clone()
        {
            return new SharedParameterDefinition
            {
                Guid = Guid,
                Name = Name,
                DataType = DataType,
                DataCategory = DataCategory,
                Group = Group,
                IsVisible = IsVisible,
                Description = Description,
                IsInstance = IsInstance,
                Formula = Formula,
                CreatedAt = CreatedAt,
                UsageCount = UsageCount,
                Category = Category,
                Discipline = Discipline
            };
        }
    }

    #endregion

    #region Parameter Templates

    public class ParameterTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<ParameterDefinitionTemplate> Parameters { get; set; } = new List<ParameterDefinitionTemplate>();
    }

    public class ParameterDefinitionTemplate
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string Group { get; set; }
        public string Description { get; set; }
        public bool IsInstance { get; set; }
        public List<string> ApplicableCategories { get; set; } = new List<string>();
        public string Formula { get; set; }
        public Guid Guid { get; set; }
    }

    public class TemplateApplicationOptions
    {
        public bool ApplyCategoryDefaults { get; set; } = true;
    }

    #endregion

    #region Family Profiles

    public class FamilyParameterProfile
    {
        public string Category { get; set; }
        public List<string> RequiredParameters { get; set; } = new List<string>();
        public List<string> RecommendedParameters { get; set; } = new List<string>();
        public Dictionary<string, string> DefaultFormulas { get; set; } = new Dictionary<string, string>();
    }

    #endregion

    #region Batch Operations

    public class BatchParameterSpecification
    {
        public string Name { get; set; }
        public List<ParameterDefinitionTemplate> Parameters { get; set; } = new List<ParameterDefinitionTemplate>();
        public List<string> TargetCategories { get; set; } = new List<string>();
    }

    public class BatchCreationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public BatchParameterSpecification Specification { get; set; }
        public BatchOperationStatus Status { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<ParameterConflict> Conflicts { get; set; } = new List<ParameterConflict>();
        public List<SharedParameterDefinition> CreatedParameters { get; set; } = new List<SharedParameterDefinition>();
    }

    public class BatchOperationProgress
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string CurrentItem { get; set; }
        public double PercentComplete { get; set; }
        public string Message { get; set; }
    }

    public enum BatchOperationStatus
    {
        Success,
        CompletedWithErrors,
        ValidationFailed,
        ConflictsDetected,
        Failed
    }

    #endregion

    #region Conflicts

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

    public enum ConflictType
    {
        DataTypeMismatch,
        GuidMismatch,
        NamingViolation,
        GroupMismatch,
        FormulaConflict
    }

    public enum ConflictSeverity
    {
        Info,
        Warning,
        Critical
    }

    public class ConflictAnalysisResult
    {
        public List<ParameterConflict> GuidConflicts { get; set; } = new List<ParameterConflict>();
        public List<ParameterConflict> TypeConflicts { get; set; } = new List<ParameterConflict>();
        public List<ParameterConflict> GroupConflicts { get; set; } = new List<ParameterConflict>();
        public List<ParameterConflict> NamingViolations { get; set; } = new List<ParameterConflict>();
        public List<ParameterConflict> FormulaConflicts { get; set; } = new List<ParameterConflict>();
        public List<ResolutionSuggestion> ResolutionSuggestions { get; set; } = new List<ResolutionSuggestion>();

        public IEnumerable<ParameterConflict> AllConflicts =>
            GuidConflicts
                .Concat(TypeConflicts)
                .Concat(GroupConflicts)
                .Concat(NamingViolations)
                .Concat(FormulaConflicts);
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

    public class ConflictResolution
    {
        public string ParameterName { get; set; }
        public ConflictType ConflictType { get; set; }
        public string Action { get; set; }
        public bool Success { get; set; }
    }

    public class ResolutionSuggestion
    {
        public string ConflictId { get; set; }
        public string Description { get; set; }
        public ResolutionAction Action { get; set; }
        public string Impact { get; set; }
    }

    public enum ResolutionAction
    {
        StandardizeGuid,
        Rename,
        ChangeType,
        MoveGroup
    }

    /// <summary>
    /// Resolution strategy for parameter conflict resolution.
    /// Distinct from StingBIM.AI.Collaboration.Protocol.ResolutionStrategy.
    /// </summary>
    public class ResolutionStrategy
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    #endregion

    #region Family Analysis and Suggestions

    public class FamilyAnalysis
    {
        public string FamilyName { get; set; }
        public string Category { get; set; }
        public List<string> ExistingParameters { get; set; } = new List<string>();
        public bool HasGeometry { get; set; }
        public bool Has3DGeometry { get; set; }
        public object ProjectContext { get; set; }
    }

    public class SuggestionOptions
    {
        public bool IncludeFormulaSuggestions { get; set; } = true;
        public bool CheckCompliance { get; set; }
        public string BuildingCode { get; set; }
        public bool LearnFromProject { get; set; }
    }

    public class ParameterSuggestionResult
    {
        public string FamilyName { get; set; }
        public string Category { get; set; }
        public List<ParameterSuggestion> RequiredSuggestions { get; set; } = new List<ParameterSuggestion>();
        public List<ParameterSuggestion> RecommendedSuggestions { get; set; } = new List<ParameterSuggestion>();
        public List<ParameterSuggestion> GeometryBasedSuggestions { get; set; } = new List<ParameterSuggestion>();
        public List<ParameterSuggestion> RelatedSuggestions { get; set; } = new List<ParameterSuggestion>();
        public List<FormulaSuggestion> FormulaSuggestions { get; set; } = new List<FormulaSuggestion>();
        public List<ParameterSuggestion> ComplianceSuggestions { get; set; } = new List<ParameterSuggestion>();
        public List<ParameterSuggestion> LearnedSuggestions { get; set; } = new List<ParameterSuggestion>();
    }

    public enum SuggestionPriority
    {
        Optional,
        Recommended,
        Required
    }

    public class ParameterSuggestion
    {
        public string ParameterName { get; set; }
        public string DataType { get; set; }
        public string Group { get; set; }
        public SuggestionPriority Priority { get; set; }
        public string Reason { get; set; }
        public bool ExistsInSharedFile { get; set; }
    }

    #endregion

    #region Family Synchronization

    public class SynchronizationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public SyncStatus Status { get; set; }
        public List<FamilySyncResult> FamilyResults { get; set; } = new List<FamilySyncResult>();
        public int TotalParametersAdded { get; set; }
        public int TotalParametersModified { get; set; }
        public int TotalParametersRemoved { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class SynchronizationOptions
    {
        public bool IncludeRecommended { get; set; }
        public string TemplateId { get; set; }
        public bool RemoveNonStandard { get; set; }
        public bool PreviewOnly { get; set; }
    }

    public enum SyncStatus
    {
        Success,
        CompletedWithErrors,
        Failed
    }

    public class FamilyInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Category { get; set; }
        public List<string> Parameters { get; set; } = new List<string>();
        public List<FamilyParameterInfo> ParameterDetails { get; set; } = new List<FamilyParameterInfo>();
    }

    public class FamilyParameterInfo
    {
        public string Name { get; set; }
        public Guid Guid { get; set; }
        public string DataType { get; set; }
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

    #endregion

    #region Batch Family Operations

    public class ParameterAddition
    {
        public string ParameterName { get; set; }
    }

    public class BatchFamilyOptions
    {
        public bool SkipExisting { get; set; } = true;
        public bool UpdateExisting { get; set; }
        public bool CreateBackup { get; set; } = true;
    }

    public class BatchFamilyResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<FamilyOperationResult> Results { get; set; } = new List<FamilyOperationResult>();
        public int TotalFamilies { get; set; }
        public int SuccessfulFamilies { get; set; }
        public int FailedFamilies { get; set; }
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

    public enum FamilyOpStatus
    {
        Success,
        NoChanges,
        FileNotFound,
        Failed
    }

    #endregion

    #region Version Control

    public class ParameterSnapshot
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public List<SharedParameterDefinition> Parameters { get; set; } = new List<SharedParameterDefinition>();
    }

    public class ParameterVersion
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }
        public SharedParameterDefinition Definition { get; set; }
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

    #region Operation History

    public class ParameterOperation
    {
        public OperationType Type { get; set; }
        public DateTime Timestamp { get; set; }
        public int ParameterCount { get; set; }
        public string Details { get; set; }
    }

    public enum OperationType
    {
        BatchCreate,
        BatchUpdate,
        BatchDelete,
        Synchronize,
        Import,
        Export,
        Rollback
    }

    #endregion

    #region Reporting

    public class ParameterReport
    {
        public DateTime GeneratedAt { get; set; }
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
        public bool IncludeUsageAnalysis { get; set; }
        public bool IncludeFormulaAnalysis { get; set; }
        public bool IncludeNamingAnalysis { get; set; }
    }

    public class NamingViolation
    {
        public string ParameterName { get; set; }
        public string Suggestion { get; set; }
    }

    #endregion

    #region Export / Import

    public enum ExportFormat
    {
        CSV,
        Excel,
        JSON,
        SharedParameterFile,
        FamilyParameterFile
    }

    public class ExportResult
    {
        public ExportFormat Format { get; set; }
        public string OutputPath { get; set; }
        public bool Success { get; set; }
        public int ExportedCount { get; set; }
        public string Error { get; set; }
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

    #endregion

    #region Validation

    /// <summary>
    /// Validation result for parameter specifications.
    /// Distinct from StingBIM.AI.Parameters.AutoPopulation.ValidationResult
    /// and StingBIM.AI.Collaboration.Protocol.ValidationResult.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    #endregion
}

namespace StingBIM.AI.Parameters.Integration
{
    #region Schedule Field Mapping

    /// <summary>
    /// Defines a mapping between a schedule field and a parameter for the ScheduleParameterIntegrator.
    /// </summary>
    public class ScheduleFieldMapping
    {
        public string FieldId { get; set; }
        public string ParameterId { get; set; }
        public string ParameterName { get; set; }
        public string DisplayName { get; set; }
        public string DataType { get; set; }
        public bool IsRequired { get; set; }
        public bool CanAutoPopulate { get; set; }
        public string AutoPopulateSource { get; set; }
        public int SortOrder { get; set; }
    }

    #endregion
}
