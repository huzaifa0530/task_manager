
using System.Collections.Concurrent;

namespace WindowsSystemManager.Infrastructure.Services.Processes;

public class PowerMetric
{
    public string PowerUsage { get; set; } = "Very low";
    public string PowerTrend { get; set; } = "Very low";
    public double EstimatedScore { get; set; }
}

public sealed class PowerUsageMonitor
{
    private const int HistoryDurationSeconds = 120;

    private readonly ConcurrentDictionary<int, ProcessPowerHistory>
        _processHistories = new();

    public PowerMetric Calculate(
        int processId,
        double cpuUsage,
        double gpuUsage,
        double readMBps,
        double writeMBps)
    {
        double diskActivity = Math.Clamp(
            readMBps + writeMBps, 0, 100);

        double score =
            (Math.Clamp(cpuUsage, 0, 100) * 0.55) +
            (Math.Clamp(gpuUsage, 0, 100) * 0.30) +
            (diskActivity * 0.15);

        score = Math.Clamp(score, 0, 100);

        var history = _processHistories.GetOrAdd(
            processId,
            _ => new ProcessPowerHistory());

        double averageScore = history.AddAndGetAverage(
            score,
            DateTime.UtcNow);

        return new PowerMetric
        {
            EstimatedScore = score,
            PowerUsage = GetPowerLevel(score),
            PowerTrend = GetPowerLevel(averageScore)
        };
    }

    private static string GetPowerLevel(double score) => score switch
    {
        < 3 => "Very low",
        < 10 => "Low",
        < 30 => "Moderate",
        < 60 => "High",
        _ => "Very high"
    };

    public void RemoveExitedProcesses(HashSet<int> activeProcessIds)
    {
        foreach (int processId in _processHistories.Keys)
        {
            if (!activeProcessIds.Contains(processId))
            {
                _processHistories.TryRemove(processId, out _);
            }
        }
    }

    private sealed class ProcessPowerHistory
    {
        private readonly object _lock = new();

        private readonly Queue<(DateTime Timestamp, double Score)>
            _samples = new();

        public double AddAndGetAverage(
            double score,
            DateTime timestamp)
        {
            lock (_lock)
            {
                _samples.Enqueue((timestamp, score));

                DateTime cutoff = timestamp.AddSeconds(
                    -HistoryDurationSeconds);

                while (_samples.Count > 0 &&
                       _samples.Peek().Timestamp < cutoff)
                {
                    _samples.Dequeue();
                }

                return _samples.Average(x => x.Score);
            }
        }
    }
}
