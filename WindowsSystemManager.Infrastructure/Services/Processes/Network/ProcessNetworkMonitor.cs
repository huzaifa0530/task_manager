
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using WindowsSystemManager.Infrastructure.Services.Network;

namespace WindowsSystemManager.Infrastructure.Services.Processes.Network;

public sealed class ProcessNetworkMonitor : BackgroundService
{
    private readonly ProcessNetworkEtwMonitor _etwMonitor;
    private readonly ProcessNetworkUsageCache _networkCache;
    private readonly ProcessNetworkRateCalculator _rateCalculator;
    private readonly ILogger<ProcessNetworkMonitor> _logger;
    private readonly ConcurrentDictionary<int, ProcessNetworkRate> _latestRates = new();

    private static readonly TimeSpan SamplingInterval =
        TimeSpan.FromSeconds(1);

public ProcessNetworkMonitor(
        ProcessNetworkEtwMonitor etwMonitor,
        ProcessNetworkUsageCache networkCache,
        ProcessNetworkRateCalculator rateCalculator,
        ILogger<ProcessNetworkMonitor> logger)
    {
        _etwMonitor = etwMonitor;
        _networkCache = networkCache;
        _rateCalculator = rateCalculator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _etwMonitor.NetworkActivityReceived += OnNetworkActivityReceived;
        try
        {
                _logger.LogInformation(
                "Process network monitoring started.");

           await _etwMonitor.StartAsync(stoppingToken);

           using var timer = new PeriodicTimer(SamplingInterval);

          while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                SampleProcessNetworkRates();
            }
        }
        catch (OperationCanceledException)
               when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                  "Process network monitoring cancellation requested.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                   "Process network monitoring failed.");
        }
        finally
        {
            _etwMonitor.NetworkActivityReceived -= OnNetworkActivityReceived;

            await _etwMonitor.StopAsync(CancellationToken.None);

            _logger.LogInformation(
                "Process network monitoring stopped.");
        }
    }
    private void OnNetworkActivityReceived(
        int processId,
        long bytes,
        NetworkDirection direction)
    {
        switch (direction)
        {
            case NetworkDirection.Send:
                _networkCache.AddSentBytes(processId, bytes);
                break;

            case NetworkDirection.Receive:
                _networkCache.AddReceivedBytes(processId, bytes);
                break;
        }
    }
    private void SampleProcessNetworkRates()
    {
        Process[] processes;

        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve running processes.");

            return;
        }

        var activeProcessIds = new HashSet<int>();

        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    int processId = process.Id;

                    activeProcessIds.Add(processId);

                    DateTime processStartTime =
                        process.StartTime.ToUniversalTime();

                    ProcessNetworkRate rate =
                        _rateCalculator.Calculate(
                            processId,
                            processStartTime);

                    _latestRates[processId] = rate;
                }
                catch (Exception ex) when (
                    ex is InvalidOperationException ||
                    ex is System.ComponentModel.Win32Exception ||
                    ex is NotSupportedException)
                {
                }
            }
        }

        foreach (var processId in _latestRates.Keys)
        {
            if (!activeProcessIds.Contains(processId))
            {
                _latestRates.TryRemove(processId, out _);
                _rateCalculator.RemoveProcess(processId);
            }
        }
    }
    public ProcessNetworkRate? GetProcessNetworkRate(int processId)
    {
        return _latestRates.TryGetValue(processId, out var rate)
            ? rate
            : null;
    }

    public IReadOnlyDictionary<int, ProcessNetworkRate>
        GetAllProcessNetworkRates()
    {
        return new Dictionary<int, ProcessNetworkRate>(_latestRates);
    }
}
