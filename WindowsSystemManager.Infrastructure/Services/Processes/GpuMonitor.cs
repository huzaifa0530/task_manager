
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace WindowsSystemManager.Infrastructure.Services.Processes
{
    public class GpuMetric
    {
        public double UsagePercentage { get; set; }
        public string Engine { get; set; } = "N/A";
    }

    public static class GpuMonitor
    {
        public static async Task<Dictionary<int, GpuMetric>>
            GetGpuMetricsAsync()
        {
            var results = new Dictionary<int, GpuMetric>();

            var category = new PerformanceCounterCategory("GPU Engine");

            var counters = new List<(
                int ProcessId,
                string Engine,
                PerformanceCounter Counter)>();

            try
            {
                foreach (string instance in category.GetInstanceNames())
                {
                    string[] parts = instance.Split('_');

                    int pidIndex = Array.IndexOf(parts, "pid");
                    int engineIndex = Array.IndexOf(parts, "engtype");

                    if (pidIndex < 0 || pidIndex + 1 >= parts.Length)
                        continue;

                    if (!int.TryParse(
                        parts[pidIndex + 1],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int processId))
                        continue;

                    string engine = engineIndex >= 0 &&
                                    engineIndex + 1 < parts.Length
                        ? parts[engineIndex + 1]
                        : "Unknown";

                    var counter = new PerformanceCounter(
                        "GPU Engine",
                        "Utilization Percentage",
                        instance,
                        true);

                    counters.Add((processId, engine, counter));

                    // Initialize counter
                    counter.NextValue();
                }

                // Allow counters to collect the first sampling interval
                //await Task.Delay(1000);

                foreach (var item in counters)
                {
                    double usage = item.Counter.NextValue();

                    if (usage < 0)
                        continue;

                    if (!results.TryGetValue(
                        item.ProcessId, out GpuMetric? existing) ||
                        usage > existing.UsagePercentage)
                    {
                        results[item.ProcessId] = new GpuMetric
                        {
                            UsagePercentage = Math.Clamp(usage, 0, 100),
                            Engine = item.Engine
                        };
                    }
                }

                return results;
            }
            finally
            {
                foreach (var item in counters)
                {
                    item.Counter.Dispose();
                }
            }
        }
    }
}
