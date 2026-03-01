// StingBIM.AI.NLP.Domain.BIMKnowledgeBase
// BIM domain knowledge for answering informational queries

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StingBIM.AI.NLP.Domain
{
    /// <summary>
    /// Contains BIM domain knowledge for answering user questions
    /// about BIM concepts, standards, workflows, and best practices.
    /// </summary>
    public static class BIMKnowledgeBase
    {
        private static readonly Dictionary<string, string> KnowledgeEntries =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Core BIM concepts
            ["bim"] = "BIM (Building Information Modeling) is a digital representation of the physical and functional characteristics of a building. It serves as a shared knowledge resource for information about a facility, forming a reliable basis for decisions during its lifecycle from inception to demolition. BIM integrates 3D modeling with data-rich elements including geometry, spatial relationships, material properties, cost data, and scheduling information.",

            ["bim process"] = "The BIM process typically follows these stages:\n\n1. Pre-Design: Define BIM goals, create BEP (BIM Execution Plan), set up CDE (Common Data Environment)\n2. Conceptual Design: Create massing models (LOD 100), define spatial relationships\n3. Schematic Design: Develop approximate geometry (LOD 200), establish building systems\n4. Design Development: Precise geometry (LOD 300), detailed MEP coordination, clash detection\n5. Construction Documentation: Construction-level detail (LOD 350), quantity takeoffs, specifications\n6. Construction: 4D scheduling, site coordination, fabrication models (LOD 400)\n7. Handover: As-built models (LOD 500), FM data, digital twin setup\n8. Operations: Facility management, predictive maintenance, space management\n\nKey processes include: model authoring, coordination, clash detection, quantity takeoff, compliance checking, and information exchange via IFC/COBie.",

            ["bim workflow"] = "A BIM workflow encompasses the collaborative processes for creating, managing, and using building information models. Key workflows include:\n\n- Model Authoring: Creating 3D parametric models in BIM software (Revit, ArchiCAD)\n- Coordination: Multi-discipline model federation and clash detection\n- Review: Model-based design reviews and stakeholder collaboration\n- Documentation: Automated drawing generation from the model\n- Quantity Takeoff: Extracting material quantities for cost estimation\n- Simulation: Energy, structural, lighting, and acoustic analysis\n- Construction: 4D scheduling and site logistics planning\n- Handover: Delivering as-built models with operational data\n\nISO 19650 provides the framework for information management across these workflows.",

            ["bim standards"] = "Major BIM standards include:\n\n- ISO 19650 (Parts 1-5): International standard for BIM information management, covering concepts, delivery phase, operational phase, and security\n- ISO 16739 (IFC): Industry Foundation Classes for open BIM data exchange\n- ISO 12006-2: Building construction classification systems\n- BS 1192: UK BIM collaboration standard (predecessor to ISO 19650)\n- PAS 1192 series: UK specifications for information management using BIM\n- COBie: Construction Operations Building Information Exchange\n- LOD Specification: Level of Development definitions (AIA/BIMForum)\n- buildingSMART standards: IFC, BCF, bSDD for open BIM\n\nRegional standards include: IBC (US), Eurocodes (EU), ASHRAE (HVAC), NFPA (Fire), ASCE 7 (Loads), ACI 318 (Concrete), and 32 others supported by StingBIM.",

            ["iso standards"] = "Key ISO standards for BIM and construction:\n\n- ISO 19650-1: Concepts and principles for BIM information management\n- ISO 19650-2: Delivery phase of assets (design and construction)\n- ISO 19650-3: Operational phase of assets\n- ISO 19650-5: Security-minded approach to BIM\n- ISO 16739: IFC (Industry Foundation Classes) data schema\n- ISO 12006-2: Classification of construction information\n- ISO 29481: Information delivery manual methodology\n- ISO 21597: Information container for linked document delivery\n- ISO 23386/23387: Building data dictionaries\n\nThese standards define how information is structured, exchanged, and managed throughout the building lifecycle.",

            ["revit"] = "Autodesk Revit is a BIM software used by architects, structural engineers, MEP engineers, designers, and contractors. It allows users to design buildings and structures in 3D, annotate models with 2D drafting elements, and access building information from the model's database. Revit supports multi-discipline collaboration through worksharing and linked models.",

            ["ifc"] = "IFC (Industry Foundation Classes) is an open, international standard for BIM data exchange (ISO 16739). It enables interoperability between different BIM software platforms. IFC defines a schema that represents building elements, properties, relationships, and processes in a vendor-neutral format.",

            ["lod"] = "LOD (Level of Development) defines the degree of detail and reliability of BIM elements at different project stages. LOD 100: Conceptual, LOD 200: Approximate geometry, LOD 300: Precise geometry, LOD 350: Construction-level detail with connections, LOD 400: Fabrication-level detail, LOD 500: As-built verified.",

            ["bep"] = "A BIM Execution Plan (BEP) is a document that defines how BIM will be implemented on a project. It covers: BIM goals and uses, project information, organizational roles and responsibilities, BIM process design, information exchange requirements, technology infrastructure, model standards, quality control procedures, and deliverable requirements. It is typically developed before project start and updated throughout.",

            ["cde"] = "A Common Data Environment (CDE) is a single source of information for any project, used to collect, manage, and disseminate documentation, the graphical model, and non-graphical data. As defined by ISO 19650, the CDE has four states: Work in Progress, Shared, Published, and Archived.",

            // Standards
            ["iso 19650"] = "ISO 19650 is the international standard for managing information over the whole lifecycle of a built asset using BIM. It covers: Part 1 — Concepts and principles; Part 2 — Delivery phase (design and construction); Part 3 — Operational phase; Part 5 — Security-minded approach. It defines the information management framework including naming conventions, information containers, the Common Data Environment (CDE), and information exchange requirements.",

            ["iso19650"] = "ISO 19650 is the international standard for managing information over the whole lifecycle of a built asset using BIM. It covers: Part 1 — Concepts and principles; Part 2 — Delivery phase (design and construction); Part 3 — Operational phase; Part 5 — Security-minded approach. It defines the information management framework including naming conventions, information containers, the Common Data Environment (CDE), and information exchange requirements.",

            ["ashrae"] = "ASHRAE (American Society of Heating, Refrigerating and Air-Conditioning Engineers) develops standards for HVAC systems. Key standards: ASHRAE 90.1 (Energy Standard for Buildings), ASHRAE 62.1 (Ventilation for Acceptable Indoor Air Quality), ASHRAE 55 (Thermal Environmental Conditions), and ASHRAE 189.1 (High-Performance Green Buildings).",

            ["ibc"] = "The International Building Code (IBC) is a model code developed by the International Code Council (ICC). It covers structural design, fire safety, means of egress, accessibility, building envelope, interior environment, and structural loads. The 2021 edition includes updates to seismic design, mass timber construction, and energy efficiency.",

            ["eurocode"] = "Eurocodes are a set of European standards for structural design. Key codes: EN 1990 (Basis of design), EN 1991 (Actions/loads), EN 1992 (Concrete), EN 1993 (Steel), EN 1994 (Composite), EN 1995 (Timber), EN 1996 (Masonry), EN 1997 (Geotechnical), EN 1998 (Seismic), EN 1999 (Aluminium).",

            ["nfpa"] = "NFPA (National Fire Protection Association) develops fire safety standards. Key standards: NFPA 1 (Fire Code), NFPA 13 (Sprinkler Systems), NFPA 72 (Fire Alarm Systems), NFPA 101 (Life Safety Code), and NFPA 5000 (Building Construction Code).",

            // BIM workflows
            ["collaboration"] = "BIM Collaboration involves multiple disciplines working together on a shared model. Key approaches include: Central Model with worksharing (Revit), federated models via IFC exchange, cloud-based collaboration platforms, clash detection and coordination, and cross-discipline reviews. ISO 19650 defines the framework for information exchange between parties.",

            ["clash detection"] = "Clash Detection is the process of identifying conflicts between building systems in a BIM model. Types: Hard clashes (physical intersections), soft clashes (clearance violations), and workflow clashes (scheduling conflicts). Tools like Navisworks, Solibri, and BIMcollab are commonly used for automated clash detection.",

            ["4d bim"] = "4D BIM adds the dimension of time to 3D BIM models, linking construction activities to model elements for schedule visualization. It enables: construction sequencing simulation, progress tracking, logistics planning, and phasing visualization. Elements are linked to tasks in a construction schedule (e.g., MS Project, Primavera).",

            ["5d bim"] = "5D BIM adds cost data to 4D BIM, enabling automated quantity takeoff and cost estimation directly from the model. It supports: real-time cost tracking, budget forecasting, value engineering, and lifecycle cost analysis. Quantities are extracted from model elements and linked to cost databases.",

            ["6d bim"] = "6D BIM adds facility management data, supporting building operations after handover. It includes: asset information, maintenance schedules, warranty data, equipment specifications, spare parts, and operational procedures. The model becomes a digital twin for ongoing facility management.",

            ["7d bim"] = "7D BIM adds sustainability data for environmental analysis. It includes: energy performance simulation, carbon footprint analysis, lifecycle assessment, daylighting analysis, and renewable energy integration. It supports green building certification (LEED, BREEAM, Green Star).",

            // Design concepts
            ["parametric design"] = "Parametric Design uses parameters and rules to define relationships between design elements. In BIM, this means: changing one dimension automatically updates related elements, formulas can drive parameter values, and design intent is captured through constraints. This enables rapid design exploration and ensures consistency.",

            ["generative design"] = "Generative Design uses algorithms to explore design alternatives based on goals and constraints. It can optimize for: spatial layout, structural efficiency, energy performance, cost, views, and daylighting. The designer sets goals and constraints, and the system generates multiple options ranked by performance.",

            ["mep"] = "MEP (Mechanical, Electrical, and Plumbing) encompasses building services systems. Mechanical: HVAC, ductwork, piping. Electrical: power distribution, lighting, fire alarm, data/communications. Plumbing: water supply, drainage, fixtures. BIM enables coordination between MEP and architectural/structural disciplines.",

            ["shared parameters"] = "Shared Parameters in Revit are parameter definitions stored in an external text file that can be shared across projects and families. They enable: consistent data across projects, schedule generation, tagging, filtering, and data exchange. StingBIM uses 818 ISO 19650-compliant shared parameters organized by discipline.",

            ["schedules"] = "Schedules in Revit are tabular views that extract data from model elements. Types include: Material Takeoff, Room Schedule, Door Schedule, Window Schedule, Equipment Schedule. StingBIM can auto-generate schedules from templates covering Architectural, MEP, FM, and Material Takeoff categories.",

            ["facility management"] = "Facility Management (FM) in BIM context involves using the building information model for ongoing operations. This includes: preventive and predictive maintenance scheduling, asset lifecycle management, space management, energy monitoring, equipment replacement planning, and spare parts tracking. 6D BIM supports FM by embedding operational data in the model.",

            ["digital twin"] = "A Digital Twin is a real-time digital replica of a physical building, continuously updated with sensor data, IoT devices, and operational information. It enables: predictive maintenance, energy optimization, space utilization analysis, emergency response planning, and lifecycle management. The BIM model serves as the foundation for the digital twin.",

            // Construction concepts
            ["quantity takeoff"] = "Quantity Takeoff (QTO) is the process of extracting material quantities from BIM models for cost estimation. BIM enables automated QTO by calculating: areas, volumes, lengths, and counts of building elements. This replaces manual measurement and improves accuracy. StingBIM automates QTO for materials, elements, and assemblies.",

            ["construction sequencing"] = "Construction Sequencing (4D BIM) plans the order of construction activities. It considers: structural dependencies, material logistics, crane access, weather constraints, and resource availability. StingBIM's Construction Sequencing Engine generates optimized sequences considering safety, cost, and schedule constraints.",
        };

        /// <summary>
        /// Looks up BIM knowledge based on user query.
        /// Returns a relevant answer or null if no match found.
        /// </summary>
        public static string LookupKnowledge(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;

            var normalizedQuery = query.Trim().ToLowerInvariant();

            // Strip common question prefixes
            normalizedQuery = Regex.Replace(normalizedQuery,
                @"^(what is|what's|what are|define|explain|tell me about|describe|i need to know about|about|give me|give me the|what are the|what's the|list|list the|show me|show me the)\s+",
                "", RegexOptions.IgnoreCase);
            normalizedQuery = Regex.Replace(normalizedQuery,
                @"^(the|a|an|major|main|key|important|primary)\s+", "", RegexOptions.IgnoreCase);
            // Strip trailing context phrases
            normalizedQuery = Regex.Replace(normalizedQuery,
                @"\s+(in this|in the|for this|for the|of this|of the)\s+(model|project|floor\s*plan|building).*$", "", RegexOptions.IgnoreCase);
            // Strip "process to complete a" type prefixes
            normalizedQuery = Regex.Replace(normalizedQuery,
                @"^(process to complete a|steps to complete|how to do|how to complete|process of|process for)\s+",
                "", RegexOptions.IgnoreCase);
            normalizedQuery = normalizedQuery.Trim().TrimEnd('?', '.', '!');

            // Direct key match
            if (KnowledgeEntries.TryGetValue(normalizedQuery, out var directMatch))
            {
                return directMatch;
            }

            // Try multi-word key matches (e.g., "bim iso standards" -> "bim standards" or "iso standards")
            foreach (var key in KnowledgeEntries.Keys.OrderByDescending(k => k.Length))
            {
                if (normalizedQuery.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    return KnowledgeEntries[key];
                }
            }

            // Try matching individual words/phrases against keys
            var bestMatch = FindBestMatch(normalizedQuery);
            if (bestMatch != null)
            {
                return bestMatch;
            }

            return null;
        }

        /// <summary>
        /// Tries to find the best matching knowledge entry for the query.
        /// </summary>
        private static string FindBestMatch(string query)
        {
            int bestScore = 0;
            string bestEntry = null;

            foreach (var (key, value) in KnowledgeEntries)
            {
                int score = 0;

                // Exact key match in query
                if (query.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    score = key.Length * 10;
                }
                // Key words match
                else
                {
                    var keyWords = key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var matches = keyWords.Count(kw =>
                        queryWords.Any(qw => qw.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                                             kw.Contains(qw, StringComparison.OrdinalIgnoreCase)));
                    if (matches > 0)
                    {
                        score = matches * 5;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestEntry = value;
                }
            }

            return bestScore >= 5 ? bestEntry : null;
        }

        /// <summary>
        /// Gets all available knowledge topics.
        /// </summary>
        public static IEnumerable<string> GetTopics()
        {
            return KnowledgeEntries.Keys.Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }
}
