using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.TenantManagement
{
    /// <summary>
    /// Manages tenant operations including occupancy tracking,
    /// lease management, space allocation, and tenant services.
    /// </summary>
    public class TenantManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, Tenant> _tenants;
        private readonly object _lock = new object();

        public TenantManager()
        {
            _tenants = new Dictionary<string, Tenant>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets all registered tenants.
        /// </summary>
        /// <returns>List of tenants</returns>
        public List<Tenant> GetAllTenants()
        {
            lock (_lock)
            {
                return new List<Tenant>(_tenants.Values);
            }
        }

        /// <summary>
        /// Registers a new tenant.
        /// </summary>
        /// <param name="tenant">The tenant to register</param>
        public void RegisterTenant(Tenant tenant)
        {
            if (tenant == null)
                throw new ArgumentNullException(nameof(tenant));

            lock (_lock)
            {
                _tenants[tenant.TenantId] = tenant;
            }

            Logger.Info($"Registered tenant: {tenant.Name}");
        }
    }

    /// <summary>
    /// Represents a building tenant.
    /// </summary>
    public class Tenant
    {
        public string TenantId { get; set; }
        public string Name { get; set; }
        public string SpaceId { get; set; }
        public DateTime LeaseStart { get; set; }
        public DateTime LeaseEnd { get; set; }
        public double AllocatedArea { get; set; }
        public string Status { get; set; }
    }
}
