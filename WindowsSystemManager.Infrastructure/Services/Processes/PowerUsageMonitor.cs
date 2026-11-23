
using System.Collections.Concurrent;

namespace WindowsSystemManager.Infrastructure.Services.Processes
{
    public class PowerMetric
    {
        public string PowerUsage { get; set; } = "Very low";
        public string PowerTrend { get; set; } = "Stable";
        public double EstimatedScore { get; set; }
    }

    public class PowerUsageMonitor
    {
        private readonly ConcurrentDictionary<int, double>
            _previousScores = new();

        public PowerMetric Calculate(
            int processId,
            double cpuUsage,
            double gpuUsage,
            double readMBps,
            double writeMBps,
            double downloadBytesPerSecond,
            double uploadBytesPerSecond)
        {
            // Normalize disk and network activity to 0-100.
            double diskActivity = Math.Min(
                ((readMBps + writeMBps) / 100) * 100, 100);

            double networkMBps =
                (downloadBytesPerSecond + uploadBytesPerSecond)
                / (1024 * 1024);

            double networkActivity = Math.Min(
                networkMBps, 100);

            // Custom estimated activity score.
            double score =
                (cpuUsage * 0.55) +
                (gpuUsage * 0.30) +
                (diskActivity * 0.10) +
                (networkActivity * 0.05);

            score = Math.Clamp(score, 0, 100);

            string powerUsage = score switch
            {
                < 5 => "Very low",
                < 20 => "Low",
                < 50 => "Moderate",
                < 75 => "High",
                _ => "Very high"
            };

            string trend = "Stable";

            if (_previousScores.TryGetValue(
                processId, out double previousScore))
            {
                double difference = score - previousScore;

                if (difference > 5)
                    trend = "Increasing";
                else if (difference < -5)
                    trend = "Decreasing";
            }

            _previousScores[processId] = score;

            return new PowerMetric
            {
                PowerUsage = powerUsage,
                PowerTrend = trend,
                EstimatedScore = score
            };
        }
    }
}
