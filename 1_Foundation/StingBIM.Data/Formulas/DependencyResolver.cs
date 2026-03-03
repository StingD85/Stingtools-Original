using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.Data.Formulas
{
    /// <summary>
    /// Resolves formula dependencies and determines calculation order.
    /// Builds dependency graphs, detects circular dependencies, and provides topological sorting.
    /// </summary>
    public class DependencyResolver
    {
        #region Fields

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly FormulaLibrary _formulaLibrary;
        private readonly FormulaParser _parser;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyResolver"/> class.
        /// </summary>
        /// <param name="formulaLibrary">The formula library containing all formulas</param>
        public DependencyResolver(FormulaLibrary formulaLibrary)
        {
            _formulaLibrary = formulaLibrary ?? throw new ArgumentNullException(nameof(formulaLibrary));
            _parser = new FormulaParser();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Builds a dependency graph for all formulas.
        /// </summary>
        /// <returns>Dependency graph mapping each parameter to its dependencies</returns>
        public DependencyGraph BuildDependencyGraph()
        {
            Logger.Info("Building dependency graph for all formulas");

            var graph = new DependencyGraph();
            var allFormulas = _formulaLibrary.AllFormulas;

            foreach (var formula in allFormulas)
            {
                var paramName = formula.ParameterName;
                var dependencies = _parser.ExtractParameterReferences(formula.Formula);

                graph.AddNode(paramName);

                foreach (var dependency in dependencies)
                {
                    graph.AddDependency(paramName, dependency);
                }
            }

            Logger.Info($"Dependency graph built with {graph.NodeCount} nodes and {graph.DependencyCount} dependencies");
            return graph;
        }

        /// <summary>
        /// Gets the calculation order for formulas (topological sort).
        /// </summary>
        /// <returns>List of parameter names in calculation order (dependencies first)</returns>
        /// <exception cref="CircularDependencyException">Thrown if circular dependency is detected</exception>
        public List<string> GetCalculationOrder()
        {
            Logger.Info("Determining formula calculation order");

            var graph = BuildDependencyGraph();
            var order = TopologicalSort(graph);

            Logger.Info($"Calculation order determined for {order.Count} formulas");
            return order;
        }

        /// <summary>
        /// Gets the calculation order for specific formulas.
        /// </summary>
        /// <param name="parameterNames">Parameter names to calculate</param>
        /// <returns>List of parameter names in calculation order</returns>
        public List<string> GetCalculationOrder(IEnumerable<string> parameterNames)
        {
            if (parameterNames == null)
            {
                throw new ArgumentNullException(nameof(parameterNames));
            }

            var targetParams = parameterNames.ToList();
            Logger.Info($"Determining calculation order for {targetParams.Count} specific formulas");

            var graph = BuildDependencyGraph();

            // Extract subgraph for target parameters and their dependencies
            var subgraph = ExtractSubgraph(graph, targetParams);

            // Sort subgraph
            var order = TopologicalSort(subgraph);

            Logger.Info($"Calculation order determined for {order.Count} formulas (including dependencies)");
            return order;
        }

        /// <summary>
        /// Detects circular dependencies in formulas.
        /// </summary>
        /// <returns>List of circular dependency chains (empty if none found)</returns>
        public List<List<string>> DetectCircularDependencies()
        {
            Logger.Info("Detecting circular dependencies");

            var graph = BuildDependencyGraph();
            var cycles = FindCycles(graph);

            if (cycles.Count > 0)
            {
                Logger.Warn($"Found {cycles.Count} circular dependency chains");
            }
            else
            {
                Logger.Info("No circular dependencies detected");
            }

            return cycles;
        }

        /// <summary>
        /// Gets all dependencies for a parameter (recursive).
        /// </summary>
        /// <param name="parameterName">The parameter name</param>
        /// <returns>List of all dependencies (direct and indirect)</returns>
        public List<string> GetAllDependencies(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new ArgumentNullException(nameof(parameterName));
            }

            var graph = BuildDependencyGraph();
            var dependencies = new HashSet<string>();
            var visited = new HashSet<string>();

            CollectDependencies(graph, parameterName, dependencies, visited);

            return dependencies.ToList();
        }

        /// <summary>
        /// Gets all dependents for a parameter (recursive).
        /// </summary>
        /// <param name="parameterName">The parameter name</param>
        /// <returns>List of all parameters that depend on this parameter</returns>
        public List<string> GetAllDependents(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new ArgumentNullException(nameof(parameterName));
            }

            var graph = BuildDependencyGraph();
            var dependents = new HashSet<string>();
            var visited = new HashSet<string>();

            CollectDependents(graph, parameterName, dependents, visited);

            return dependents.ToList();
        }

        /// <summary>
        /// Validates that all formula dependencies can be resolved.
        /// </summary>
        /// <returns>Validation result with any errors found</returns>
        public DependencyValidationResult ValidateDependencies()
        {
            Logger.Info("Validating formula dependencies");

            var result = new DependencyValidationResult();
            var graph = BuildDependencyGraph();
            var allFormulas = _formulaLibrary.AllFormulas;

            // Check for circular dependencies
            var cycles = FindCycles(graph);
            foreach (var cycle in cycles)
            {
                result.AddError($"Circular dependency detected: {string.Join(" -> ", cycle)} -> {cycle[0]}");
            }

            // Check for missing dependencies
            foreach (var formula in allFormulas)
            {
                var dependencies = _parser.ExtractParameterReferences(formula.Formula);

                foreach (var dependency in dependencies)
                {
                    // Check if dependency exists
                    var depFormula = _formulaLibrary.GetByParameterName(dependency);
                    if (depFormula == null)
                    {
                        // Check if it's a built-in parameter or user-provided value
                        if (!IsBuiltInParameter(dependency))
                        {
                            result.AddWarning($"Formula '{formula.ParameterName}' references unknown parameter '{dependency}'");
                        }
                    }
                }
            }

            Logger.Info($"Validation complete: {result.ErrorCount} errors, {result.WarningCount} warnings");
            return result;
        }

        #endregion

        #region Private Methods - Graph Operations

        /// <summary>
        /// Performs topological sort on dependency graph.
        /// </summary>
        /// <param name="graph">The dependency graph</param>
        /// <returns>Sorted list of parameter names</returns>
        /// <exception cref="CircularDependencyException">Thrown if circular dependency is detected</exception>
        private List<string> TopologicalSort(DependencyGraph graph)
        {
            var sorted = new List<string>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            foreach (var node in graph.GetAllNodes())
            {
                if (!visited.Contains(node))
                {
                    TopologicalSortVisit(graph, node, visited, visiting, sorted);
                }
            }

            return sorted;
        }

        /// <summary>
        /// Recursive visit for topological sort (DFS).
        /// </summary>
        private void TopologicalSortVisit(DependencyGraph graph, string node,
            HashSet<string> visited, HashSet<string> visiting, List<string> sorted)
        {
            if (visiting.Contains(node))
            {
                // Circular dependency detected
                throw new CircularDependencyException($"Circular dependency detected involving '{node}'");
            }

            if (visited.Contains(node))
            {
                return;
            }

            visiting.Add(node);

            var dependencies = graph.GetDependencies(node);
            foreach (var dependency in dependencies)
            {
                TopologicalSortVisit(graph, dependency, visited, visiting, sorted);
            }

            visiting.Remove(node);
            visited.Add(node);
            sorted.Add(node);
        }

        /// <summary>
        /// Finds all cycles in the dependency graph.
        /// </summary>
        private List<List<string>> FindCycles(DependencyGraph graph)
        {
            var cycles = new List<List<string>>();
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();
            var currentPath = new List<string>();

            foreach (var node in graph.GetAllNodes())
            {
                if (!visited.Contains(node))
                {
                    FindCyclesVisit(graph, node, visited, recursionStack, currentPath, cycles);
                }
            }

            return cycles;
        }

        /// <summary>
        /// Recursive visit for cycle detection.
        /// </summary>
        private void FindCyclesVisit(DependencyGraph graph, string node,
            HashSet<string> visited, HashSet<string> recursionStack,
            List<string> currentPath, List<List<string>> cycles)
        {
            visited.Add(node);
            recursionStack.Add(node);
            currentPath.Add(node);

            var dependencies = graph.GetDependencies(node);
            foreach (var dependency in dependencies)
            {
                if (!visited.Contains(dependency))
                {
                    FindCyclesVisit(graph, dependency, visited, recursionStack, currentPath, cycles);
                }
                else if (recursionStack.Contains(dependency))
                {
                    // Cycle detected
                    var cycleStart = currentPath.IndexOf(dependency);
                    var cycle = currentPath.GetRange(cycleStart, currentPath.Count - cycleStart);
                    cycles.Add(new List<string>(cycle));
                }
            }

            currentPath.RemoveAt(currentPath.Count - 1);
            recursionStack.Remove(node);
        }

        /// <summary>
        /// Extracts a subgraph containing only specified nodes and their dependencies.
        /// </summary>
        private DependencyGraph ExtractSubgraph(DependencyGraph graph, List<string> targetNodes)
        {
            var subgraph = new DependencyGraph();
            var visited = new HashSet<string>();

            foreach (var node in targetNodes)
            {
                ExtractSubgraphVisit(graph, node, subgraph, visited);
            }

            return subgraph;
        }

        /// <summary>
        /// Recursive visit for subgraph extraction.
        /// </summary>
        private void ExtractSubgraphVisit(DependencyGraph graph, string node,
            DependencyGraph subgraph, HashSet<string> visited)
        {
            if (visited.Contains(node))
            {
                return;
            }

            visited.Add(node);
            subgraph.AddNode(node);

            var dependencies = graph.GetDependencies(node);
            foreach (var dependency in dependencies)
            {
                subgraph.AddDependency(node, dependency);
                ExtractSubgraphVisit(graph, dependency, subgraph, visited);
            }
        }

        /// <summary>
        /// Recursively collects all dependencies.
        /// </summary>
        private void CollectDependencies(DependencyGraph graph, string node,
            HashSet<string> dependencies, HashSet<string> visited)
        {
            if (visited.Contains(node))
            {
                return;
            }

            visited.Add(node);

            var nodeDependencies = graph.GetDependencies(node);
            foreach (var dependency in nodeDependencies)
            {
                dependencies.Add(dependency);
                CollectDependencies(graph, dependency, dependencies, visited);
            }
        }

        /// <summary>
        /// Recursively collects all dependents.
        /// </summary>
        private void CollectDependents(DependencyGraph graph, string node,
            HashSet<string> dependents, HashSet<string> visited)
        {
            if (visited.Contains(node))
            {
                return;
            }

            visited.Add(node);

            var nodeDependents = graph.GetDependents(node);
            foreach (var dependent in nodeDependents)
            {
                dependents.Add(dependent);
                CollectDependents(graph, dependent, dependents, visited);
            }
        }

        /// <summary>
        /// Checks if a parameter is a built-in Revit parameter.
        /// </summary>
        private bool IsBuiltInParameter(string parameterName)
        {
            // Common built-in parameters (expand as needed)
            var builtInParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Length", "Width", "Height", "Area", "Volume",
                "Level", "Offset", "Mark", "Comments",
                "Family", "Type", "Category"
            };

            return builtInParams.Contains(parameterName);
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Represents a dependency graph for formulas.
    /// </summary>
    public class DependencyGraph
    {
        private readonly Dictionary<string, HashSet<string>> _dependencies;
        private readonly Dictionary<string, HashSet<string>> _dependents;
        private readonly HashSet<string> _nodes;

        /// <summary>
        /// Number of nodes in the graph
        /// </summary>
        public int NodeCount => _nodes.Count;

        /// <summary>
        /// Total number of dependencies
        /// </summary>
        public int DependencyCount => _dependencies.Values.Sum(d => d.Count);

        /// <summary>
        /// Constructor
        /// </summary>
        public DependencyGraph()
        {
            _dependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _dependents = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _nodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds a node to the graph.
        /// </summary>
        public void AddNode(string node)
        {
            if (!_nodes.Contains(node))
            {
                _nodes.Add(node);
                _dependencies[node] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _dependents[node] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Adds a dependency: node depends on dependency.
        /// </summary>
        public void AddDependency(string node, string dependency)
        {
            AddNode(node);
            AddNode(dependency);

            _dependencies[node].Add(dependency);
            _dependents[dependency].Add(node);
        }

        /// <summary>
        /// Gets all nodes in the graph.
        /// </summary>
        public IEnumerable<string> GetAllNodes()
        {
            return _nodes;
        }

        /// <summary>
        /// Gets direct dependencies for a node.
        /// </summary>
        public IEnumerable<string> GetDependencies(string node)
        {
            return _dependencies.ContainsKey(node)
                ? _dependencies[node]
                : Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets direct dependents for a node (parameters that depend on this node).
        /// </summary>
        public IEnumerable<string> GetDependents(string node)
        {
            return _dependents.ContainsKey(node)
                ? _dependents[node]
                : Enumerable.Empty<string>();
        }

        /// <summary>
        /// Checks if a node exists in the graph.
        /// </summary>
        public bool ContainsNode(string node)
        {
            return _nodes.Contains(node);
        }
    }

    /// <summary>
    /// Result of dependency validation.
    /// </summary>
    public class DependencyValidationResult
    {
        private readonly List<string> _errors;
        private readonly List<string> _warnings;

        /// <summary>
        /// List of errors
        /// </summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>
        /// List of warnings
        /// </summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Number of errors
        /// </summary>
        public int ErrorCount => _errors.Count;

        /// <summary>
        /// Number of warnings
        /// </summary>
        public int WarningCount => _warnings.Count;

        /// <summary>
        /// Indicates if validation passed (no errors)
        /// </summary>
        public bool IsValid => _errors.Count == 0;

        /// <summary>
        /// Constructor
        /// </summary>
        public DependencyValidationResult()
        {
            _errors = new List<string>();
            _warnings = new List<string>();
        }

        /// <summary>
        /// Adds an error.
        /// </summary>
        public void AddError(string error)
        {
            _errors.Add(error);
        }

        /// <summary>
        /// Adds a warning.
        /// </summary>
        public void AddWarning(string warning)
        {
            _warnings.Add(warning);
        }

        /// <summary>
        /// Returns summary string.
        /// </summary>
        public override string ToString()
        {
            return $"Validation: {(IsValid ? "PASSED" : "FAILED")} - {ErrorCount} errors, {WarningCount} warnings";
        }
    }

    /// <summary>
    /// Exception thrown when circular dependency is detected.
    /// </summary>
    public class CircularDependencyException : Exception
    {
        public CircularDependencyException(string message) : base(message) { }
        public CircularDependencyException(string message, Exception innerException) : base(message, innerException) { }
    }

    #endregion
}
