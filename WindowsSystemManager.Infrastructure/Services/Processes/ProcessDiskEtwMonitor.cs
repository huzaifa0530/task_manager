
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace WindowsSystemManager.Infrastructure.Services.Processes
{
    public sealed class ProcessDiskEtwMonitor
    {
        private readonly ProcessDiskUsageCache _cache;

        private readonly ConcurrentDictionary<ulong, PendingDiskIo>
            _pendingRequests = new();

        private long _eventCount;

        public ProcessDiskEtwMonitor(ProcessDiskUsageCache cache)
        {
            _cache = cache;
        }

        public void Run(CancellationToken cancellationToken)
        {
            string sessionName =
                $"WindowsSystemManager-Disk-{Guid.NewGuid():N}";

            using var session = new TraceEventSession(sessionName);

            // 1. Enable kernel disk events FIRST.
            session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.DiskIO |
                KernelTraceEventParser.Keywords.DiskIOInit
            );

            // 2. Access the kernel event parser AFTER enabling.
            KernelTraceEventParser parser = session.Source.Kernel;

            // 3. Register disk read initialization.
            parser.DiskIOReadInit += data =>
            {
                RegisterRequest(
                    data.Irp,
                    data.ProcessID,
                    DiskIoType.Read);
            };

            // 4. Register disk write initialization.
            parser.DiskIOWriteInit += data =>
            {
                RegisterRequest(
                    data.Irp,
                    data.ProcessID,
                    DiskIoType.Write);
            };

            // 5. Register completed disk reads.
            parser.DiskIORead += data =>
            {
                CompleteRequest(
                    data.Irp,
                    data.TransferSize,
                    DiskIoType.Read);
            };

            // 6. Register completed disk writes.
            parser.DiskIOWrite += data =>
            {
                CompleteRequest(
                    data.Irp,
                    data.TransferSize,
                    DiskIoType.Write);
            };

            // 7. Register process termination.
            parser.ProcessEndGroup += data =>
            {
                _cache.RemoveProcess(data.ProcessID);
            };

            using var registration = cancellationToken.Register(
                () => session.Stop(true));

            try
            {
                session.Source.Process();
            }
            finally
            {
                _pendingRequests.Clear();
            }
        }

        private void RegisterRequest(
            ulong irp,
            int processId,
            DiskIoType type)
        {
            if (irp == 0 || processId <= 0)
                return;

            _pendingRequests[irp] = new PendingDiskIo(
                processId,
                type,
                Stopwatch.GetTimestamp()
            );

            // Periodically clean up expired requests.
            if ((Interlocked.Increment(ref _eventCount) & 1023) == 0)
            {
                RemoveExpiredRequests();
            }
        }

        private void CompleteRequest(
            ulong irp,
            int transferSize,
            DiskIoType expectedType)
        {
            if (irp == 0 || transferSize <= 0)
                return;

            if (!_pendingRequests.TryRemove(irp, out var request))
                return;

            if (request.Type != expectedType)
                return;

            if (expectedType == DiskIoType.Read)
            {
                _cache.AddReadBytes(
                    request.ProcessId,
                    transferSize);
            }
            else
            {
                _cache.AddWriteBytes(
                    request.ProcessId,
                    transferSize);
            }
        }

        private void RemoveExpiredRequests()
        {
            long now = Stopwatch.GetTimestamp();

            foreach (var item in _pendingRequests)
            {
                TimeSpan age = Stopwatch.GetElapsedTime(
                    item.Value.Timestamp,
                    now);

                if (age > TimeSpan.FromSeconds(30))
                {
                    _pendingRequests.TryRemove(item.Key, out _);
                }
            }
        }

        private enum DiskIoType
        {
            Read,
            Write
        }

        private sealed record PendingDiskIo(
            int ProcessId,
            DiskIoType Type,
            long Timestamp);
    }
}
