
using System.Collections.Concurrent;

namespace WindowsSystemManager.Infrastructure.Services.Processes;

public sealed class GpuUsageCache
{
    private readonly ConcurrentDictionary<int, GpuMetric> _gpuMetrics = new();

    public void Update(Dictionary<int, GpuMetric> metrics)
    {
        _gpuMetrics.Clear();

        foreach (var item in metrics)
        {
            _gpuMetrics[item.Key] = item.Value;
        }
    }

    public GpuMetric? Get(int processId)
    {
        return _gpuMetrics.TryGetValue(processId, out var metric)
            ? metric
            : null;
    }

    public IReadOnlyDictionary<int, GpuMetric> GetAll()
    {
        return new Dictionary<int, GpuMetric>(_gpuMetrics);
    }
}
