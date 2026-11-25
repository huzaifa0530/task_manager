using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
namespace WindowsSystemManager.Infrastructure.Services.Processes.Network
{
    public class ProcessNetworkUsageCache
    {
        private readonly ConcurrentDictionary<int, ProcessNetworkStats>
     _networkStats = new();

        public void AddSentBytes(int processId, long bytes)
        {
            if (processId <= 0 || bytes <= 0)
                return;

            var stats = _networkStats.GetOrAdd(
                processId,
                _ => new ProcessNetworkStats()
            );

            Interlocked.Add(ref stats.BytesSent, bytes);
        }

        public void AddReceivedBytes(int processId, long bytes)
        {
            if (processId <= 0 || bytes <= 0)
                return;

            var stats = _networkStats.GetOrAdd(
                processId,
                _ => new ProcessNetworkStats()
            );

            Interlocked.Add(ref stats.BytesReceived, bytes);
        }
        //tell you how many byte a proces has snt and received so fra.
        public ProcessNetworkStats GetNetworkStats(int processId)
        {
            if (_networkStats.TryGetValue(processId, out var stats))
            {
                return new ProcessNetworkStats
                {
                    BytesSent = Interlocked.Read(ref stats.BytesSent),
                    BytesReceived = Interlocked.Read(ref stats.BytesReceived)
                };
            }

            return new ProcessNetworkStats();
        }
        public sealed class ProcessNetworkStats
        {
            public long BytesSent;
            public long BytesReceived;
        }
    }
}
