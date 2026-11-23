using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Timers;
using WindowsSystemManager.Application.DTOs.Processes;
using WindowsSystemManager.Application.Interfaces.Processes;
namespace WindowsSystemManager.Infrastructure.Services.Processes
{
    public class ProcessService : IProcessService
    {
        private readonly ILogger<ProcessService> _logger;
        private readonly PowerUsageMonitor _powerMonitor;
        private readonly ProcessNetworkMonitor _networkMonitor;


        public ProcessService(
            ILogger<ProcessService> logger,
            PowerUsageMonitor powerMonitor,
            ProcessNetworkMonitor networkMonitor)
        {
            _logger = logger;
            _powerMonitor = powerMonitor;
            _networkMonitor = networkMonitor;
        }

        //to fetch all the proccess
        public async Task<List<ProcessDto>> GetAllProcesses()
        {
            Process[] allprocesses = Array.Empty<Process>();

            try
            {
                List<ProcessDto> processes = new List<ProcessDto>();
                _logger.LogInformation("GetAllProcesses started");
                allprocesses = Process.GetProcesses();
                _logger.LogInformation(
    "Total processes found: {Count}",
    allprocesses.Length);
                _logger.LogInformation("Starting GPU metrics collection");

                var gpuTimer = Stopwatch.StartNew();

                var gpuMetrics = await GpuMonitor.GetGpuMetricsAsync();

                gpuTimer.Stop();

                _logger.LogInformation(
                    "GPU metrics completed in {Time} ms",
                    gpuTimer.ElapsedMilliseconds);
                foreach (Process p in allprocesses)

                {
                    _logger.LogInformation("Processing PID: {ProcessId}", p.Id);
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
                            int logicalProcessors = Environment.ProcessorCount;
                            TimeSpan firstCpuTime = p.TotalProcessorTime;
                            Stopwatch stopwatch = Stopwatch.StartNew();
                            //await Task.Delay(500);
                            TimeSpan secondCpuTime = p.TotalProcessorTime;
                            stopwatch.Stop();
                            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                            double cpuSeconds = (secondCpuTime - firstCpuTime).TotalSeconds;
                            double cpuUsage = 0;

                            if (elapsedSeconds > 0 && cpuSeconds >= 0 && logicalProcessors > 0)
                            {
                                cpuUsage = (cpuSeconds / (elapsedSeconds * logicalProcessors)) * 100;

                                cpuUsage = Math.Clamp(cpuUsage, 0, 100);
                            }

                            PerformanceCounterCategory category =
                                new PerformanceCounterCategory("Process");


                            string? instanceName = null;

                            foreach (string instance in category.GetInstanceNames())
                            {
                                try
                                {
                                    using PerformanceCounter idCounter =
                                        new PerformanceCounter(
                                            "Process",
                                            "ID Process",
                                            instance,
                                            true);

                                    if ((int)idCounter.RawValue == p.Id)
                                    {
                                        instanceName = instance;
                                        break;
                                    }
                                }
                                catch (InvalidOperationException)
                                {
                                    // PerformanceCounter instance disappeared.
                                    continue;
                                }
                                catch (Win32Exception)
                                {
                                    // Windows denied access to this instance.
                                    continue;
                                }
                            }


                            double readMBps = 0;
                            double writeMBps = 0;

                            if (instanceName != null)
                            {
                                using PerformanceCounter readCounter =
                                    new PerformanceCounter(
                                        "Process",
                                        "IO Read Bytes/sec",
                                        instanceName,
                                        true);

                                using PerformanceCounter writeCounter =
                                    new PerformanceCounter(
                                        "Process",
                                        "IO Write Bytes/sec",
                                        instanceName,
                                        true);

                                readCounter.NextValue();
                                writeCounter.NextValue();

                                //await Task.Delay(1000);

                                double readBytesPerSecond = readCounter.NextValue();
                                double writeBytesPerSecond = writeCounter.NextValue();

                                readMBps = readBytesPerSecond / (1024 * 1024);
                                writeMBps = writeBytesPerSecond / (1024 * 1024);
                            }

                            var networkRate = _networkMonitor.GetRate(p.Id);
                            gpuMetrics.TryGetValue(p.Id, out var gpuMetric);
                            double gpuUsage = gpuMetric?.UsagePercentage ?? 0;

                            double downloadBytesPerSecond =
                                networkRate.DownloadBytesPerSecond;

                            double uploadBytesPerSecond =
                                networkRate.UploadBytesPerSecond;

                            var powerMetric = _powerMonitor.Calculate(
                                p.Id,
                                cpuUsage,
                                gpuUsage,
                                readMBps,
                                writeMBps,
                                downloadBytesPerSecond,
                                uploadBytesPerSecond);
                            processes.Add(new ProcessDto
                            {
                                ProcessId = p.Id,
                                ProcessName = p.ProcessName,
                                MemoryUsage = p.WorkingSet64,
                                StartTime = p.StartTime,

                                Status = "Running",
                                CPU = cpuUsage,

                                Memory = (p.WorkingSet64 / (1024.0 * 1024.0))
                                    .ToString("F2") + " MB",

                                Disk = $"Read: {readMBps:F2} MB/s | Write: {writeMBps:F2} MB/s",

                                Network = $"Download: {downloadBytesPerSecond:F2} B/s | Upload: {uploadBytesPerSecond:F2} B/s",

                                GPU = gpuUsage.ToString("F2") + "%",

                                GPU_engine = gpuMetric?.Engine ?? "N/A",

                                Power_usage_trend = powerMetric.PowerTrend,

                                username = "Unavailable"
                            });
                            _logger.LogInformation("Completed PID: {ProcessId}", p.Id);
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
                    catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
                    {
                        _logger.LogWarning(
                            ex,
                            "Windows access denied for process {ProcessId}.",
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
