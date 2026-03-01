// ============================================================================
// StingBIM Core - Service Locator
// Lightweight dependency injection and service management
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NLog;

namespace StingBIM.Core.Services
{
    /// <summary>
    /// Service locator for dependency injection.
    /// Provides singleton and transient service registration.
    /// </summary>
    public class ServiceLocator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Lazy<ServiceLocator> _instance =
            new Lazy<ServiceLocator>(() => new ServiceLocator());

        private readonly ConcurrentDictionary<Type, ServiceRegistration> _registrations;
        private readonly ConcurrentDictionary<Type, object> _singletons;

        public static ServiceLocator Instance => _instance.Value;

        private ServiceLocator()
        {
            _registrations = new ConcurrentDictionary<Type, ServiceRegistration>();
            _singletons = new ConcurrentDictionary<Type, object>();
        }

        /// <summary>
        /// Registers a service as a singleton.
        /// </summary>
        public void RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService, new()
        {
            _registrations[typeof(TService)] = new ServiceRegistration
            {
                ServiceType = typeof(TService),
                ImplementationType = typeof(TImplementation),
                Lifetime = ServiceLifetime.Singleton
            };
            Logger.Debug($"Registered singleton: {typeof(TService).Name} -> {typeof(TImplementation).Name}");
        }

        /// <summary>
        /// Registers a service instance as a singleton.
        /// </summary>
        public void RegisterInstance<TService>(TService instance)
        {
            _registrations[typeof(TService)] = new ServiceRegistration
            {
                ServiceType = typeof(TService),
                ImplementationType = instance.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Instance = instance
            };
            _singletons[typeof(TService)] = instance;
            Logger.Debug($"Registered instance: {typeof(TService).Name}");
        }

        /// <summary>
        /// Registers a service as transient (new instance each time).
        /// </summary>
        public void RegisterTransient<TService, TImplementation>()
            where TImplementation : TService, new()
        {
            _registrations[typeof(TService)] = new ServiceRegistration
            {
                ServiceType = typeof(TService),
                ImplementationType = typeof(TImplementation),
                Lifetime = ServiceLifetime.Transient
            };
            Logger.Debug($"Registered transient: {typeof(TService).Name} -> {typeof(TImplementation).Name}");
        }

        /// <summary>
        /// Registers a service with a factory function.
        /// </summary>
        public void RegisterFactory<TService>(Func<TService> factory, ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            _registrations[typeof(TService)] = new ServiceRegistration
            {
                ServiceType = typeof(TService),
                Lifetime = lifetime,
                Factory = () => factory()
            };
            Logger.Debug($"Registered factory: {typeof(TService).Name} ({lifetime})");
        }

        /// <summary>
        /// Resolves a service instance.
        /// </summary>
        public TService Resolve<TService>()
        {
            return (TService)Resolve(typeof(TService));
        }

        /// <summary>
        /// Resolves a service instance by type.
        /// </summary>
        public object Resolve(Type serviceType)
        {
            if (!_registrations.TryGetValue(serviceType, out var registration))
            {
                throw new InvalidOperationException($"Service not registered: {serviceType.Name}");
            }

            if (registration.Lifetime == ServiceLifetime.Singleton)
            {
                return _singletons.GetOrAdd(serviceType, _ => CreateInstance(registration));
            }

            return CreateInstance(registration);
        }

        /// <summary>
        /// Tries to resolve a service instance.
        /// </summary>
        public bool TryResolve<TService>(out TService service)
        {
            try
            {
                service = Resolve<TService>();
                return true;
            }
            catch
            {
                service = default;
                return false;
            }
        }

        /// <summary>
        /// Checks if a service is registered.
        /// </summary>
        public bool IsRegistered<TService>()
        {
            return _registrations.ContainsKey(typeof(TService));
        }

        /// <summary>
        /// Gets all registered service types.
        /// </summary>
        public IEnumerable<Type> GetRegisteredServices()
        {
            return _registrations.Keys;
        }

        /// <summary>
        /// Clears all registrations.
        /// </summary>
        public void Clear()
        {
            _registrations.Clear();
            _singletons.Clear();
            Logger.Info("Service locator cleared");
        }

        private object CreateInstance(ServiceRegistration registration)
        {
            if (registration.Instance != null)
            {
                return registration.Instance;
            }

            if (registration.Factory != null)
            {
                return registration.Factory();
            }

            return Activator.CreateInstance(registration.ImplementationType);
        }
    }

    /// <summary>
    /// Service registration information.
    /// </summary>
    internal class ServiceRegistration
    {
        public Type ServiceType { get; set; }
        public Type ImplementationType { get; set; }
        public ServiceLifetime Lifetime { get; set; }
        public object Instance { get; set; }
        public Func<object> Factory { get; set; }
    }

    /// <summary>
    /// Service lifetime options.
    /// </summary>
    public enum ServiceLifetime
    {
        Singleton,
        Transient
    }

    /// <summary>
    /// Service collection for fluent registration.
    /// </summary>
    public class ServiceCollection
    {
        private readonly List<Action<ServiceLocator>> _registrations = new List<Action<ServiceLocator>>();

        public ServiceCollection AddSingleton<TService, TImplementation>()
            where TImplementation : TService, new()
        {
            _registrations.Add(sl => sl.RegisterSingleton<TService, TImplementation>());
            return this;
        }

        public ServiceCollection AddInstance<TService>(TService instance)
        {
            _registrations.Add(sl => sl.RegisterInstance(instance));
            return this;
        }

        public ServiceCollection AddTransient<TService, TImplementation>()
            where TImplementation : TService, new()
        {
            _registrations.Add(sl => sl.RegisterTransient<TService, TImplementation>());
            return this;
        }

        public void Build()
        {
            foreach (var registration in _registrations)
            {
                registration(ServiceLocator.Instance);
            }
        }
    }
}
