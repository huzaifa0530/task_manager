using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Diagnostics.Tracing.StackSources;
using System.Diagnostics;
using WindowsSystemManager.Application.DTOs.Processes;
using WindowsSystemManager.Application.Interfaces.Processes;
using WindowsSystemManager.Infrastructure.Services.Processes;

namespace WindowsSystemManager.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProcessesController : ControllerBase
    {
        private readonly IProcessService _processService;
        private readonly ProcessCpuMonitor _cpuMonitor;
        private readonly ProcessMemoryMonitor _memoryMonitor;

        public ProcessesController(
            IProcessService processService,
            ProcessCpuMonitor cpuMonitor,
            ProcessMemoryMonitor memoryMonitor)
        {
            _processService = processService;
            _cpuMonitor = cpuMonitor;
            _memoryMonitor = memoryMonitor;
        }
        [HttpGet]
        public Task<List<ProcessDto>> GetProducts()
        {
            var processes= _processService.GetAllProcesses();
            return processes;
        }

        [HttpGet("{pid}")]
        public ProcessDto GetProcessById(int pid)
        {
            var process = _processService.GetProcessById(pid);
            return process;
        }
        [HttpGet("test-cpu")]
        [HttpGet("test-cpu/{pid}")]
        public async Task<IActionResult> TestCpu(int pid)
        {
            if (pid <= 0)
                return BadRequest("Invalid process ID.");

            Stopwatch stopwatch = Stopwatch.StartNew();

            long firstReading = _cpuMonitor.GetCpuTimes(pid);

            await Task.Delay(1000);

            long secondReading = _cpuMonitor.GetCpuTimes(pid);

            stopwatch.Stop();

            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

            double cpuUsage = _cpuMonitor.CalculateCpuUsage(
                firstReading,
                secondReading,
                elapsedSeconds
            );

            return Ok(new
            {
                ProcessId = pid,
                FirstReading = firstReading,
                SecondReading = secondReading,
                ElapsedSeconds = elapsedSeconds,
                CpuUsage = $"{cpuUsage:F2}%"
            });
        }


        [HttpGet("test-memory")]
        public IActionResult TestMemory()
        {
            var results = new List<object>();

            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (process.Id == 0)
                            continue;

                        ulong privateWorkingSet =
                            _memoryMonitor.GetPrivateWorkingSet(process.Id);

                        results.Add(new
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            PrivateWorkingSetBytes = privateWorkingSet,
                            PrivateWorkingSetMB =
                                (privateWorkingSet / (1024.0 * 1024.0)).ToString("F2") + " MB"
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new
                        {
                            ProcessId = process.Id,
                            ProcessName = "Unavailable",
                            Error = ex.Message
                        });
                    }
                }
            }

            return Ok(results);
        }
    }
}
