using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.IoT
{
    /// <summary>
    /// Manages IoT sensor integration, real-time monitoring,
    /// device management, and environmental tracking.
    /// </summary>
    public class IoTManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<string, SensorReading> _latestReadings;
        private readonly object _lock = new object();

        public IoTManager()
        {
            _latestReadings = new ConcurrentDictionary<string, SensorReading>();
        }

        /// <summary>
        /// Processes incoming sensor data.
        /// </summary>
        /// <param name="reading">The sensor reading to process</param>
        public void ProcessSensorReading(SensorReading reading)
        {
            if (reading == null)
                throw new ArgumentNullException(nameof(reading));

            _latestReadings.AddOrUpdate(reading.SensorId, reading, (key, existing) => reading);
            Logger.Debug($"Processed reading from sensor: {reading.SensorId}");
        }

        /// <summary>
        /// Gets the latest readings for all sensors.
        /// </summary>
        /// <returns>Dictionary of sensor ID to latest reading</returns>
        public Dictionary<string, SensorReading> GetLatestReadings()
        {
            return new Dictionary<string, SensorReading>(_latestReadings);
        }
    }

    /// <summary>
    /// Represents a sensor reading from an IoT device.
    /// </summary>
    public class SensorReading
    {
        public string SensorId { get; set; }
        public string SensorType { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public DateTime Timestamp { get; set; }
        public string Location { get; set; }
    }
}
