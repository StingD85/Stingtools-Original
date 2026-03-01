// StingBIM.AI.NLP.Domain.IModelQueryService
// Interface for querying the active Revit model from NLP pipeline

using System.Collections.Generic;

namespace StingBIM.AI.NLP.Domain
{
    /// <summary>
    /// Provides model query capabilities to the NLP pipeline.
    /// Implemented by the Revit layer to provide access to the active Document.
    /// </summary>
    public interface IModelQueryService
    {
        /// <summary>
        /// Whether a Revit document is currently available.
        /// </summary>
        bool IsModelAvailable { get; }

        /// <summary>
        /// Gets a summary of the current model (project name, levels, element counts).
        /// </summary>
        string GetModelSummary();

        /// <summary>
        /// Gets room information (count, names, areas).
        /// </summary>
        string GetRoomInfo();

        /// <summary>
        /// Gets total floor area.
        /// </summary>
        string GetTotalArea();

        /// <summary>
        /// Gets element counts by category.
        /// </summary>
        string GetElementCounts();

        /// <summary>
        /// Runs a compliance check and returns results.
        /// </summary>
        string CheckCompliance(string standardName = null);

        /// <summary>
        /// Answers a free-form model query using available Revit data.
        /// </summary>
        string AnswerQuery(string query, string intentType);

        /// <summary>
        /// Gets materials used in the model with quantities, returned as expandable sections.
        /// </summary>
        QueryResult GetMaterialsDetailed();

        /// <summary>
        /// Generates a Bill of Quantities from the model, returned as expandable sections.
        /// </summary>
        QueryResult GetBOQDetailed();

        /// <summary>
        /// Generates a material takeoff from the model, returned as expandable sections.
        /// </summary>
        QueryResult GetMaterialTakeoffDetailed();

        /// <summary>
        /// Gets parameter values for elements in the model, returned as expandable sections.
        /// </summary>
        QueryResult GetParameterDetails(string category = null);
    }

    /// <summary>
    /// Structured query result with a summary message and expandable detail sections.
    /// </summary>
    public class QueryResult
    {
        public string Summary { get; set; }
        public List<QueryDetailSection> Sections { get; set; } = new List<QueryDetailSection>();
    }

    /// <summary>
    /// An expandable section in a structured query result.
    /// </summary>
    public class QueryDetailSection
    {
        public string Header { get; set; }
        public string Summary { get; set; }
        public List<QueryDetailItem> Items { get; set; } = new List<QueryDetailItem>();
    }

    /// <summary>
    /// A single item within a detail section.
    /// </summary>
    public class QueryDetailItem
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public List<QueryDetailItem> SubItems { get; set; }
        public bool HasSubItems => SubItems != null && SubItems.Count > 0;
    }
}
