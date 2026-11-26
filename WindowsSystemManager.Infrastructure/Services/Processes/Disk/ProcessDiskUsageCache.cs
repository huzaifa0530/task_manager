
using System.Collections.Concurrent;

namespace WindowsSystemManager.Infrastructure.Services.Processes.Disk
{
    public sealed class ProcessDiskUsageCache
    {
        private readonly ConcurrentDictionary<int, ProcessDiskEntry> _processes = new();

       // thsi method receive read bytes and accumulates for particuar procces in bytes
        public void AddReadBytes(int processId, long bytes)
        {
            if (processId <= 0 || bytes <= 0)
                return;

           // if alredy exst return avlue fi not craete new
            ProcessDiskEntry entry =
                _processes.GetOrAdd(processId, _ => new ProcessDiskEntry());

            Interlocked.Add(ref entry.PendingReadBytes, bytes);
        }

        public void AddWriteBytes(int processId, long bytes)
        {
            if (processId <= 0 || bytes <= 0)
                return;

            ProcessDiskEntry entry =
                _processes.GetOrAdd(processId, _ => new ProcessDiskEntry());

            Interlocked.Add(ref entry.PendingWriteBytes, bytes);
        }

        public void PublishRates(double elapsedSeconds)
        {
            if (elapsedSeconds <= 0)
                return;

            foreach (var process in _processes)
            {
                ProcessDiskEntry entry = process.Value;

                long readBytes = Interlocked.Exchange(
                    ref entry.PendingReadBytes, 0);

                long writeBytes = Interlocked.Exchange(
                    ref entry.PendingWriteBytes, 0);

                double readMBps =
                    readBytes / elapsedSeconds / 1_000_000d;

                double writeMBps =
                    writeBytes / elapsedSeconds / 1_000_000d;

                Volatile.Write(ref entry.ReadMBps, readMBps);
                Volatile.Write(ref entry.WriteMBps, writeMBps);
            }
        }

        public (double ReadMBps, double WriteMBps) GetRates(int processId)
        {
            if (!_processes.TryGetValue(processId, out var entry))
                return (0, 0);

            return (
                Volatile.Read(ref entry.ReadMBps),
                Volatile.Read(ref entry.WriteMBps)
            );
        }

        public void RemoveProcess(int processId)
        {
            _processes.TryRemove(processId, out _);
        }

        private sealed class ProcessDiskEntry
        {
            public long PendingReadBytes;
            public long PendingWriteBytes;

            public double ReadMBps;
            public double WriteMBps;
        }
    }
}
