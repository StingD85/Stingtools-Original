using System;
using System.Collections.Generic;

namespace StingBIM.Data.Parameters
{
    /// <summary>
    /// Repository interface for parameter operations
    /// Follows Repository pattern for data access abstraction
    /// </summary>
    public interface IParameterRepository
    {
        /// <summary>
        /// Gets a parameter by its GUID
        /// </summary>
        /// <param name="guid">Parameter GUID</param>
        /// <returns>Parameter definition or null if not found</returns>
        ParameterDefinition GetByGuid(Guid guid);
        
        /// <summary>
        /// Gets a parameter by its name
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <returns>Parameter definition or null if not found</returns>
        ParameterDefinition GetByName(string name);
        
        /// <summary>
        /// Gets all parameters for a specific discipline
        /// </summary>
        /// <param name="discipline">Discipline name (e.g., "Electrical", "HVAC")</param>
        /// <returns>List of parameters for the discipline</returns>
        List<ParameterDefinition> GetByDiscipline(string discipline);
        
        /// <summary>
        /// Gets all parameters for a specific system
        /// </summary>
        /// <param name="system">System name (e.g., "Electrical Power", "HVAC Systems")</param>
        /// <returns>List of parameters for the system</returns>
        List<ParameterDefinition> GetBySystem(string system);
        
        /// <summary>
        /// Gets all parameters
        /// </summary>
        /// <returns>Complete list of all parameters</returns>
        List<ParameterDefinition> GetAll();
        
        /// <summary>
        /// Searches parameters by keyword
        /// Searches in name and description
        /// </summary>
        /// <param name="keyword">Search keyword</param>
        /// <returns>List of matching parameters</returns>
        List<ParameterDefinition> Search(string keyword);
    }
}
