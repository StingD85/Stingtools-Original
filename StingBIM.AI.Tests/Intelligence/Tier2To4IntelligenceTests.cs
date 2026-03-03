// ===================================================================
// StingBIM Tier 2-4 Intelligence Modules - Comprehensive Tests
// Tests for remaining intelligence engines
// ===================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace StingBIM.AI.Tests.Intelligence
{
    /// <summary>
    /// Comprehensive tests for Tier 2-4 Intelligence modules
    /// </summary>
    public class Tier2To4IntelligenceTests
    {
        #region Facility Management Intelligence Tests

        [Fact]
        public void FacilityManagementIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.FacilityManagementIntelligence.FacilityManagementIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.FacilityManagementIntelligence.FacilityManagementIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void FacilityManagementIntelligence_CreateFacility_CreatesValidFacility()
        {
            var engine = StingBIM.AI.Intelligence.FacilityManagementIntelligence.FacilityManagementIntelligenceEngine.Instance;
            var facility = engine.CreateFacility("Office Building A", "123 Main Street", 50000);

            Assert.NotNull(facility);
            Assert.Equal("Office Building A", facility.Name);
            Assert.Equal(50000, facility.GrossArea);
        }

        #endregion

        #region Digital Twin Intelligence Tests

        [Fact]
        public void DigitalTwinIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.DigitalTwinIntelligence.DigitalTwinIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.DigitalTwinIntelligence.DigitalTwinIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void DigitalTwinIntelligence_CreateDigitalTwin_CreatesValidTwin()
        {
            var engine = StingBIM.AI.Intelligence.DigitalTwinIntelligence.DigitalTwinIntelligenceEngine.Instance;
            var twin = engine.CreateDigitalTwin("facility-001", "Building A Digital Twin");

            Assert.NotNull(twin);
            Assert.Equal("Building A Digital Twin", twin.Name);
        }

        #endregion

        #region Site Logistics Intelligence Tests

        [Fact]
        public void SiteLogisticsIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.SiteLogisticsIntelligence.SiteLogisticsIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.SiteLogisticsIntelligence.SiteLogisticsIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void SiteLogisticsIntelligence_CreateSiteLogisticsPlan_CreatesValidPlan()
        {
            var engine = StingBIM.AI.Intelligence.SiteLogisticsIntelligence.SiteLogisticsIntelligenceEngine.Instance;
            var plan = engine.CreateSiteLogisticsPlan("proj-site-001", "Site Logistics Plan");

            Assert.NotNull(plan);
            Assert.Equal("Site Logistics Plan", plan.Name);
        }

        #endregion

        #region Supply Chain Intelligence Tests

        [Fact]
        public void SupplyChainIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.SupplyChainIntelligence.SupplyChainIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.SupplyChainIntelligence.SupplyChainIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void SupplyChainIntelligence_CreateSupplyChain_CreatesValidChain()
        {
            var engine = StingBIM.AI.Intelligence.SupplyChainIntelligence.SupplyChainIntelligenceEngine.Instance;
            var chain = engine.CreateSupplyChainProject("proj-supply-001", "Supply Chain Project");

            Assert.NotNull(chain);
        }

        #endregion

        #region Data Analytics Intelligence Tests

        [Fact]
        public void DataAnalyticsIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.DataAnalyticsIntelligence.DataAnalyticsIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.DataAnalyticsIntelligence.DataAnalyticsIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void DataAnalyticsIntelligence_CreateDashboard_CreatesValidDashboard()
        {
            var engine = StingBIM.AI.Intelligence.DataAnalyticsIntelligence.DataAnalyticsIntelligenceEngine.Instance;
            var dashboard = engine.CreateDashboard("proj-dash-001", "Project Dashboard");

            Assert.NotNull(dashboard);
            Assert.Equal("Project Dashboard", dashboard.Name);
        }

        #endregion

        #region Financial Intelligence Tests

        [Fact]
        public void FinancialIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.FinancialIntelligence.FinancialIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.FinancialIntelligence.FinancialIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void FinancialIntelligence_CreateFinancialProject_CreatesValidProject()
        {
            var engine = StingBIM.AI.Intelligence.FinancialIntelligence.FinancialIntelligenceEngine.Instance;
            var project = engine.CreateFinancialProject("proj-fin-001", "Financial Project", 1000000m);

            Assert.NotNull(project);
            Assert.Equal(1000000m, project.ContractValue);
        }

        #endregion

        #region Legal Claims Intelligence Tests

        [Fact]
        public void LegalClaimsIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.LegalClaimsIntelligence.LegalClaimsIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.LegalClaimsIntelligence.LegalClaimsIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void LegalClaimsIntelligence_CreateClaimProject_CreatesValidProject()
        {
            var engine = StingBIM.AI.Intelligence.LegalClaimsIntelligence.LegalClaimsIntelligenceEngine.Instance;
            var project = engine.CreateClaimProject("proj-legal-001", "Claims Project");

            Assert.NotNull(project);
        }

        #endregion

        #region Simulation Intelligence Tests

        [Fact]
        public void SimulationIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.SimulationIntelligence.SimulationIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.SimulationIntelligence.SimulationIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void SimulationIntelligence_CreateSimulationProject_CreatesValidProject()
        {
            var engine = StingBIM.AI.Intelligence.SimulationIntelligence.SimulationIntelligenceEngine.Instance;
            var project = engine.CreateSimulationProject("proj-sim-001", "Simulation Project");

            Assert.NotNull(project);
        }

        #endregion

        #region Geographic Intelligence Tests

        [Fact]
        public void GeographicIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.GeographicIntelligence.GeographicIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.GeographicIntelligence.GeographicIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void GeographicIntelligence_CreateSite_CreatesValidSite()
        {
            var engine = StingBIM.AI.Intelligence.GeographicIntelligence.GeographicIntelligenceEngine.Instance;
            var site = engine.CreateSite("Site A", 40.7128, -74.0060, 25000);

            Assert.NotNull(site);
            Assert.Equal("Site A", site.Name);
            Assert.Equal(40.7128, site.Latitude);
        }

        #endregion

        #region Prefabrication Intelligence Tests

        [Fact]
        public void PrefabricationIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.PrefabricationIntelligence.PrefabricationIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.PrefabricationIntelligence.PrefabricationIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void PrefabricationIntelligence_CreatePrefabProject_CreatesValidProject()
        {
            var engine = StingBIM.AI.Intelligence.PrefabricationIntelligence.PrefabricationIntelligenceEngine.Instance;
            var project = engine.CreatePrefabProject("proj-prefab-001", "Prefab Project");

            Assert.NotNull(project);
        }

        #endregion

        #region MEP Systems Intelligence Tests

        [Fact]
        public void MEPSystemsIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.MEPSystemsIntelligence.MEPSystemsIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.MEPSystemsIntelligence.MEPSystemsIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void MEPSystemsIntelligence_CreateMEPProject_CreatesValidProject()
        {
            var engine = StingBIM.AI.Intelligence.MEPSystemsIntelligence.MEPSystemsIntelligenceEngine.Instance;
            var project = engine.CreateMEPProject("proj-mep-001", "MEP Project");

            Assert.NotNull(project);
        }

        #endregion

        #region Lean Construction Intelligence Tests

        [Fact]
        public void LeanConstructionIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.LeanConstructionIntelligence.LeanConstructionIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.LeanConstructionIntelligence.LeanConstructionIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void LeanConstructionIntelligence_CreateLeanProject_CreatesValidProject()
        {
            var engine = StingBIM.AI.Intelligence.LeanConstructionIntelligence.LeanConstructionIntelligenceEngine.Instance;
            var project = engine.CreateLeanProject("proj-lean-001", "Lean Construction Project");

            Assert.NotNull(project);
        }

        #endregion

        #region Handover Intelligence Tests

        [Fact]
        public void HandoverIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.HandoverIntelligence.HandoverIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.HandoverIntelligence.HandoverIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void HandoverIntelligence_CreateHandoverProject_CreatesValidProject()
        {
            var engine = StingBIM.AI.Intelligence.HandoverIntelligence.HandoverIntelligenceEngine.Instance;
            var project = engine.CreateHandoverProject("proj-handover-001", "Handover Project");

            Assert.NotNull(project);
        }

        #endregion

        #region Forensic Intelligence Tests

        [Fact]
        public void ForensicIntelligence_Singleton_ReturnsSameInstance()
        {
            var instance1 = StingBIM.AI.Intelligence.ForensicIntelligence.ForensicIntelligenceEngine.Instance;
            var instance2 = StingBIM.AI.Intelligence.ForensicIntelligence.ForensicIntelligenceEngine.Instance;
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void ForensicIntelligence_CreateInvestigation_CreatesValidInvestigation()
        {
            var engine = StingBIM.AI.Intelligence.ForensicIntelligence.ForensicIntelligenceEngine.Instance;
            var investigation = engine.CreateInvestigation("proj-forensic-001", "Failure Investigation");

            Assert.NotNull(investigation);
            Assert.Equal("Failure Investigation", investigation.Title);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void AllIntelligenceEngines_AreAccessible()
        {
            // Verify all 20 engines are accessible via singleton pattern
            Assert.NotNull(StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.ClashIntelligence.ClashIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.StakeholderIntelligence.StakeholderIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.ChangeManagementIntelligence.ChangeManagementIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.CommunicationIntelligence.CommunicationIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.SafetyIntelligence.SafetyIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.FacilityManagementIntelligence.FacilityManagementIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.DigitalTwinIntelligence.DigitalTwinIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.SiteLogisticsIntelligence.SiteLogisticsIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.SupplyChainIntelligence.SupplyChainIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.DataAnalyticsIntelligence.DataAnalyticsIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.FinancialIntelligence.FinancialIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.LegalClaimsIntelligence.LegalClaimsIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.SimulationIntelligence.SimulationIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.GeographicIntelligence.GeographicIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.PrefabricationIntelligence.PrefabricationIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.MEPSystemsIntelligence.MEPSystemsIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.LeanConstructionIntelligence.LeanConstructionIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.HandoverIntelligence.HandoverIntelligenceEngine.Instance);
            Assert.NotNull(StingBIM.AI.Intelligence.ForensicIntelligence.ForensicIntelligenceEngine.Instance);
        }

        [Fact]
        public void AllIntelligenceEngines_ThreadSafe()
        {
            // Test thread safety of singleton pattern
            var tasks = new List<Task>();
            var instances = new System.Collections.Concurrent.ConcurrentBag<object>();

            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    instances.Add(StingBIM.AI.Intelligence.ScheduleIntelligence.ScheduleIntelligenceEngine.Instance);
                }));
            }

            Task.WaitAll(tasks.ToArray());

            var uniqueInstances = new HashSet<object>(instances);
            Assert.Single(uniqueInstances); // All should be same instance
        }

        #endregion
    }
}
