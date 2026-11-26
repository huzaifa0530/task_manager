using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using WindowsSystemManager.Application.DTOs.Processes;

namespace WindowsSystemManager.Infrastructure.Services.Processes.Cpu
{
    public class ProcessCpuBackgroundMonitor : BackgroundService
    {
        private readonly ILogger<ProcessCpuBackgroundMonitor> _logger;
        private readonly ProcessCpuMonitor _cpuMonitor;
        private readonly ProcessCpuUsageCache _cpuUsageCache;

        private readonly ConcurrentDictionary<int, ProcessCpuSnapshot>
 _previousReadings = new();

        public ProcessCpuBackgroundMonitor(
            ILogger<ProcessCpuBackgroundMonitor> logger,
            ProcessCpuMonitor cpuMonitor,
            ProcessCpuUsageCache cpuUsageCache)
        {
            _logger = logger;
            _cpuMonitor = cpuMonitor;
            _cpuUsageCache = cpuUsageCache;
        }
        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(1));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                Process[] allProcesses = Process.GetProcesses();

                HashSet<int> activePids = new();

                foreach (Process process in allProcesses)
                {
                    try
                    {
                        int processId = process.Id;
                        activePids.Add(processId);
                        string processName = process.ProcessName;
                        DateTime creationTime = process.StartTime;
                        long currentCpuTime =
     _cpuMonitor.GetCpuTimes(processId);
                        var time = _cpuMonitor.GetCpuTimes(processId);
                        DateTime currentSampleTime = DateTime.UtcNow;
                        if (!_previousReadings.TryGetValue(
                         processId, out var previousSnapshot))
                        {
                            StoreSnapshot(
                                processId,
                                creationTime,
                                currentCpuTime,
                                currentSampleTime);

                            _cpuUsageCache.Update(processId, 0);
                        }
                        else if (previousSnapshot.CreationTime != creationTime)
                        {
                            // PID was reused by another process.

                            StoreSnapshot(
                                processId,
                                creationTime,
                                currentCpuTime,
                                currentSampleTime);

                            _cpuUsageCache.Update(processId, 0);
                        }
                        else
                        {
                            // Same process calculate CPU time difference.

                            long cpuTimeDifference =
                                currentCpuTime -
                                previousSnapshot.TotalProcessorTime;

                            TimeSpan elapsedTime =
                                currentSampleTime -
                                previousSnapshot.LastSampleTime;

                            if (cpuTimeDifference >= 0 &&
                                elapsedTime.TotalSeconds > 0)
                            {
                                double cpuSeconds =
                                    cpuTimeDifference / 10_000_000.0;

                                double cpuPercentage =
                                    (cpuSeconds /
                                    (elapsedTime.TotalSeconds *
                                    Environment.ProcessorCount)) * 100;

                                cpuPercentage = Math.Clamp(
                                    cpuPercentage, 0, 100);

                                _cpuUsageCache.Update(
                                    processId, cpuPercentage);
                            }

                            // Update previous snapshot.

                            StoreSnapshot(
                                processId,
                                creationTime,
                                currentCpuTime,
                                currentSampleTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(
                            ex,
                            "Unable to sample process CPU information.");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                // Remove terminated processes.

                foreach (var processId in _previousReadings.Keys)
                {
                    if (!activePids.Contains(processId))
                    {
                        _previousReadings.TryRemove(
                            processId, out _);

                        _cpuUsageCache.Remove(processId);
                    }
                }
            }
        }

        private void StoreSnapshot(
            int processId,
            DateTime creationTime,
            long cpuTime,
            DateTime sampleTime)
        {
            _previousReadings[processId] = new ProcessCpuSnapshot
            {
                CreationTime = creationTime,
                TotalProcessorTime = cpuTime,
                LastSampleTime = sampleTime
            };
        }
    }
}