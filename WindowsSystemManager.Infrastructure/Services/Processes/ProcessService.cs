using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Timers;
using WindowsSystemManager.Application.DTOs.Processes;
using WindowsSystemManager.Application.Interfaces.Processes;
using WindowsSystemManager.Infrastructure.Services.Processes.Cpu;
using WindowsSystemManager.Infrastructure.Services.Processes.Disk;
using WindowsSystemManager.Infrastructure.Services.Processes.Memory;
using WindowsSystemManager.Infrastructure.Services.Processes.Network;
using WindowsSystemManager.Infrastructure.Services.Processes.User;
namespace WindowsSystemManager.Infrastructure.Services.Processes
{
    public class ProcessService : IProcessService
    {
        private readonly ILogger<ProcessService> _logger;
        private readonly PowerUsageMonitor _powerMonitor;
        private readonly ProcessNetworkMonitor _networkMonitor;
        private readonly ProcessCpuUsageCache _cpuUsageCache;
        private readonly ProcessMemoryMonitor _memoryMonitor;
        private readonly ProcessDiskUsageCache _diskUsageCache;
        private readonly GpuUsageCache _gpuUsageCache;
        private readonly ProcessUserMonitor _processUserMonitor;

        public ProcessService(
            ILogger<ProcessService> logger,
            PowerUsageMonitor powerMonitor,
            ProcessNetworkMonitor networkMonitor,
                ProcessCpuUsageCache cpuUsageCache, ProcessMemoryMonitor memoryMonitor, ProcessDiskUsageCache diskUsageCache, GpuUsageCache gpuUsageCache, ProcessUserMonitor processUserMonitor)
        {
            _logger = logger;
            _powerMonitor = powerMonitor;
            _networkMonitor = networkMonitor;
            _cpuUsageCache = cpuUsageCache;
            _memoryMonitor = memoryMonitor;
            _diskUsageCache = diskUsageCache;
            _gpuUsageCache = gpuUsageCache;
            _processUserMonitor = processUserMonitor;
        }

        //to fetch all the proccess
        public async Task<List<ProcessDto>> GetAllProcesses()
        {
            Process[] allprocesses = Array.Empty<Process>();

            try
            {
                List<ProcessDto> processes = new List<ProcessDto>();
                allprocesses = Process.GetProcesses();
                var activeProcessIds = allprocesses
    .Select(p => p.Id)
    .ToHashSet();
                foreach (Process p in allprocesses)

                {
                    int processId = p.Id;

                    if (processId == 0)
                    {
                        p.Dispose();
                        continue;
                    }
                    //why seond try catch bcz if 21 procces show and error in 22 block without try it will exit
                    try
                    {
                        //release resource using using keyword when this excutue it automaticlaly call dispose 
                        using (p)
                        {
                            // CPU usage calculation
                            double cpuUsage = 0;
                            _cpuUsageCache.TryGetCpuUsage(processId, out cpuUsage);
                            ulong privateWorkingSet =
     _memoryMonitor.GetPrivateWorkingSet(processId);
                            var diskRate = _diskUsageCache.GetRates(processId);
                            double readMBps = diskRate.ReadMBps;
                            double writeMBps = diskRate.WriteMBps;

                            var networkRate = _networkMonitor.GetProcessNetworkRate(p.Id);
                            double downloadBytesPerSecond =
                                networkRate.DownloadBytesPerSecond;
                            double uploadBytesPerSecond =
                                networkRate.UploadBytesPerSecond;
                            double totalNetworkBytesPerSecond =
    downloadBytesPerSecond + uploadBytesPerSecond;

                            double networkMbps =
                                (totalNetworkBytesPerSecond * 8) / 1_000_000;


                            var gpuMetric = _gpuUsageCache.Get(processId);
                            double gpuUsage = gpuMetric?.UsagePercentage ?? 0;

                            var powerMetric = _powerMonitor.Calculate(
                                processId,
                                cpuUsage,
                                gpuUsage,
                                readMBps,
                                writeMBps);
                            processes.Add(new ProcessDto
                            {
                                ProcessId = p.Id,
                                ProcessName = p.ProcessName,
                                MemoryUsage = (long)privateWorkingSet,
                                StartTime = p.StartTime,

                                Status = "Running",
                                CPU = cpuUsage,

                                Memory = (privateWorkingSet / (1024.0 * 1024.0))
    .ToString("F2") + " MB",

                                Disk = $"Read: {readMBps:F2} MB/s | Write: {writeMBps:F2} MB/s",

                                Network = $"{networkMbps:F1} Mbps ,Download: {downloadBytesPerSecond:F2} B/s | Upload: {uploadBytesPerSecond:F2} B/s",


                                GPU = gpuUsage.ToString("F2") + "%",

                                GPU_engine = gpuMetric?.Engine ?? "N/A",

                                Power_usage = powerMetric.PowerUsage,
                                Power_usage_trend = powerMetric.PowerTrend,

                                username = _processUserMonitor.GetProcessUserName(
    processId,
    p.StartTime)
                            });
                        }
                    }
                    catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
                    {
                        _logger.LogWarning(
                            ex,
                            "Access denied for process {ProcessId}.",
                            processId);
                    }

                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Access denied while retrieving process {ProcessId}.",
                            processId);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Process {ProcessId} is no longer available.",
                            processId);
                    }
                    catch (ArgumentException ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Invalid process argument for process {ProcessId}.",
                            processId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Unexpected error while retrieving process {ProcessId}.",
                            processId);
                    }

                }
                _powerMonitor.RemoveExitedProcesses(activeProcessIds);
                _logger.LogInformation("Retrieved {Count} processes.", processes.Count);
                return processes;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Epecton eror .");

                throw;
            }
        }
        public ProcessDto GetProcessById(int Pid)
        {
            try
            {
                using (Process process = Process.GetProcessById(Pid))
                {
                    ProcessDto processDto = new ProcessDto
                    {
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName,
                        MemoryUsage = process.WorkingSet64,
                        StartTime = process.StartTime,
                    };
                    return processDto;
                }
            }
            catch (UnauthorizedAccessException un)
            {
                _logger.LogError(un, "Win expection error .");

                throw;
            }
            catch (InvalidOperationException io)
            {
                _logger.LogError(io, "Win expection error .");

                throw;

            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
            {
                _logger.LogWarning(ex, "Access denied while reading process");
                throw;
            }
            catch (ArgumentException ag)

            {
                _logger.LogError(ag, "Win expection error .");

                throw;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Epecton eror .");

                throw;
            }

        }
    }
}
