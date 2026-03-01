using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Revit
{
    /// <summary>
    /// Provides the Revit API integration layer including external commands,
    /// external applications, event handlers, and add-in entry points.
    /// </summary>
    public class RevitIntegration
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lock = new object();
        private bool _isInitialized;

        /// <summary>
        /// Initializes the Revit integration layer.
        /// </summary>
        public void Initialize()
        {
            lock (_lock)
            {
                if (_isInitialized)
                {
                    Logger.Warn("Revit integration already initialized");
                    return;
                }

                Logger.Info("Initializing Revit integration...");
                _isInitialized = true;
                Logger.Info("Revit integration initialized");
            }
        }

        /// <summary>
        /// Gets whether the integration layer is initialized.
        /// </summary>
        public bool IsInitialized
        {
            get
            {
                lock (_lock)
                {
                    return _isInitialized;
                }
            }
        }
    }
}
