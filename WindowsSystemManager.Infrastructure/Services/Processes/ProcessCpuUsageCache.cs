using System.Collections.Concurrent;

namespace WindowsSystemManager.Infrastructure.Services.Processes
{
    public class ProcessCpuUsageCache
    {
        private readonly ConcurrentDictionary<int, double> _cpuUsage = new();

        public void Update(int processId, double cpuPercentage)
        {
            _cpuUsage[processId] = cpuPercentage;
        }

        public bool TryGetCpuUsage(
            int processId,
            out double cpuPercentage)
        {
            return _cpuUsage.TryGetValue(processId, out cpuPercentage);
        }

        public void Remove(int processId)
        {
            _cpuUsage.TryRemove(processId, out _);
        }
    }
}