
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WindowsSystemManager.Infrastructure.Services.Processes;

public sealed class GpuBackgroundMonitor : BackgroundService
{
    private readonly GpuUsageCache _gpuCache;
    private readonly ILogger<GpuBackgroundMonitor> _logger;

    private static readonly TimeSpan SamplingInterval =
        TimeSpan.FromSeconds(1);

    public GpuBackgroundMonitor(
        GpuUsageCache gpuCache,
        ILogger<GpuBackgroundMonitor> logger)
    {
        _gpuCache = gpuCache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("GPU monitoring started.");

        using var timer = new PeriodicTimer(SamplingInterval);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var metrics = await GpuMonitor.GetGpuMetricsAsync();

                _gpuCache.Update(metrics);

                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("GPU monitoring stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GPU monitoring failed.");
        }
    }
}
