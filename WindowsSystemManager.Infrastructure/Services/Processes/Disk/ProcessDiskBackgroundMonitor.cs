
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace WindowsSystemManager.Infrastructure.Services.Processes.Disk
{
    public sealed class ProcessDiskBackgroundMonitor : BackgroundService
    {
        private readonly ProcessDiskEtwMonitor _etwMonitor;
        private readonly ProcessDiskUsageCache _cache;
        private readonly ILogger<ProcessDiskBackgroundMonitor> _logger;

        public ProcessDiskBackgroundMonitor(
            ProcessDiskEtwMonitor etwMonitor,
            ProcessDiskUsageCache cache,
            ILogger<ProcessDiskBackgroundMonitor> logger)
        {
            _etwMonitor = etwMonitor;
            _cache = cache;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            using var linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    stoppingToken);

            Task etwTask = Task.Factory.StartNew(
                () => _etwMonitor.Run(linkedCts.Token),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            long previousTimestamp = Stopwatch.GetTimestamp();

            try
            {
                using var timer = new PeriodicTimer(
                    TimeSpan.FromSeconds(1));

                while (await timer.WaitForNextTickAsync(linkedCts.Token))
                {
                    if (etwTask.IsCompleted)
                    {
                        await etwTask;
                        break;
                    }

                    long currentTimestamp = Stopwatch.GetTimestamp();

                    double elapsedSeconds =
                        Stopwatch.GetElapsedTime(
                            previousTimestamp,
                            currentTimestamp).TotalSeconds;

                    previousTimestamp = currentTimestamp;

                    _cache.PublishRates(elapsedSeconds);
                }
            }
            catch (OperationCanceledException)
                when (linkedCts.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Disk ETW monitoring failed.");
            }
            finally
            {
                linkedCts.Cancel();

                try
                {
                    await etwTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown.
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "ETW collection stopped unexpectedly.");
                }
            }
        }
    }
}
