using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Maintenance
{
    /// <summary>
    /// Manages maintenance operations including predictive maintenance,
    /// work orders, equipment tracking, and spare parts inventory.
    /// </summary>
    public class MaintenanceManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly List<MaintenanceTask> _tasks;
        private readonly object _lock = new object();

        public MaintenanceManager()
        {
            _tasks = new List<MaintenanceTask>();
        }

        /// <summary>
        /// Gets all pending maintenance tasks.
        /// </summary>
        /// <returns>List of pending maintenance tasks</returns>
        public List<MaintenanceTask> GetPendingTasks()
        {
            lock (_lock)
            {
                return _tasks.Where(t => t.Status == "Pending").ToList();
            }
        }

        /// <summary>
        /// Schedules a new maintenance task.
        /// </summary>
        /// <param name="task">The maintenance task to schedule</param>
        public void ScheduleTask(MaintenanceTask task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            lock (_lock)
            {
                _tasks.Add(task);
            }

            Logger.Info($"Scheduled maintenance task: {task.Description}");
        }
    }

    /// <summary>
    /// Represents a maintenance task.
    /// </summary>
    public class MaintenanceTask
    {
        public string TaskId { get; set; }
        public string Description { get; set; }
        public string EquipmentId { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
        public DateTime ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string AssignedTo { get; set; }
    }
}
