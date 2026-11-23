
using System.Collections.Concurrent;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WindowsSystemManager.Infrastructure.Services.Processes
{
    public class ProcessNetworkMonitor : BackgroundService
    {
        private readonly ILogger<ProcessNetworkMonitor> _logger;

        private readonly ConcurrentDictionary<int, TrafficCounter> _traffic = new();

        private readonly ConcurrentDictionary<int, NetworkRate> _rates = new();

        public ProcessNetworkMonitor(
            ILogger<ProcessNetworkMonitor> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            if ((bool)!TraceEventSession.IsElevated())
            {
                throw new InvalidOperationException(
                    "Administrator privileges are required for ETW network monitoring.");
            }

            using var session = new TraceEventSession(
                "WindowsSystemManager-Network");

            session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.NetworkTCPIP |
                KernelTraceEventParser.Keywords.Process);

            session.Source.Kernel.TcpIpSend += data =>
            {
                AddSentBytes(data.ProcessID, data.size);
            };

            session.Source.Kernel.TcpIpRecv += data =>
            {
                AddReceivedBytes(data.ProcessID, data.size);
            };

            Task processingTask = Task.Run(
                () => session.Source.Process(),
                CancellationToken.None);

            try
            {
                using PeriodicTimer timer =
                    new(TimeSpan.FromSeconds(1));

                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    foreach (var item in _traffic)
                    {
                        long sent = Interlocked.Exchange(
                            ref item.Value.SentBytes, 0);

                        long received = Interlocked.Exchange(
                            ref item.Value.ReceivedBytes, 0);

                        _rates[item.Key] = new NetworkRate
                        {
                            UploadBytesPerSecond = sent,
                            DownloadBytesPerSecond = received
                        };
                    }
                }
            }
            finally
            {
                session.Stop();
                await processingTask;
            }
        }

        private void AddSentBytes(int processId, long bytes)
        {
            var counter = _traffic.GetOrAdd(
                processId, _ => new TrafficCounter());

            Interlocked.Add(ref counter.SentBytes, bytes);
        }

        private void AddReceivedBytes(int processId, long bytes)
        {
            var counter = _traffic.GetOrAdd(
                processId, _ => new TrafficCounter());

            Interlocked.Add(ref counter.ReceivedBytes, bytes);
        }

        public NetworkRate GetRate(int processId)
        {
            return _rates.TryGetValue(processId, out var rate)
                ? rate
                : new NetworkRate();
        }

        private class TrafficCounter
        {
            public long SentBytes;
            public long ReceivedBytes;
        }
    }

    public class NetworkRate
    {
        public double UploadBytesPerSecond { get; set; }

        public double DownloadBytesPerSecond { get; set; }
    }
}
