using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Timers;
using WindowsSystemManager.Application.DTOs.Processes;

namespace WindowsSystemManager.Infrastructure.Services.Processes.Cpu
{
    public class ProcessCpuMonitor
    {

        private readonly ILogger<ProcessCpuMonitor> _logger;


        public ProcessCpuMonitor(
            ILogger<ProcessCpuMonitor> logger)
        {
            _logger = logger;
        }
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(
        uint dwDesiredAccess,
        bool bInheritHandle,
        uint dwProcessId
);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessTimes(
    SafeProcessHandle hProcess,
    out FILETIME lpCreationTime,
    out FILETIME lpExitTime,
    out FILETIME lpKernelTime,
    out FILETIME lpUserTime
);
        public SafeProcessHandle GetProcessHandle(int Pid)
        {
            if (Pid <= 0)
            {
                throw new ArgumentException("Invalid process ID.", nameof(Pid));
            }
            SafeProcessHandle handle = OpenProcess(
                PROCESS_QUERY_LIMITED_INFORMATION,
                false,
              (uint)Pid
                );
                return handle;
        }
        public long GetCpuTimes(int Pid)
        {
            using SafeProcessHandle handle = GetProcessHandle(Pid);
            if (handle == null || handle.IsInvalid)
            {
                _logger.LogWarning(
                       "Unexpected error while retrieving process {ProcessId}.", Pid
                       );
                return 0;
            }
            FILETIME creationTime;
            FILETIME exitTime;
            FILETIME kernelTime;
            FILETIME userTime;
            bool success =  GetProcessTimes(
                       handle,
                       out creationTime,
                       out exitTime,
                       out kernelTime,
                       out userTime
                   );
            if (!success)
            {
                int error = Marshal.GetLastWin32Error();

                _logger.LogWarning(
                    "Unable to retrieve CPU times for process {ProcessId}. Error: {Error}",
                    Pid,
                    error
                );

                return 0;
            }
            long kernel = ConvertFileTimeToLong(kernelTime);
            long user = ConvertFileTimeToLong(userTime);

            long totalCpuTime = kernel + user;
            return totalCpuTime;
        }

        public double CalculateCpuUsage(
    long firstReading,
    long secondReading,
    double elapsedSeconds)
        {

            if (secondReading < firstReading || elapsedSeconds <= 0)
            {
                _logger.LogWarning("Invalid CPU time readings.");
                return 0;
            }
            double time_difference = (secondReading - firstReading) / 10_000_000.0;

            int processorCount = Environment.ProcessorCount;
            double denominator_value = elapsedSeconds * processorCount;

            double cpuUsage = (time_difference / denominator_value) * 100;
            cpuUsage = Math.Clamp(cpuUsage, 0, 100);

            return cpuUsage;
      

        }

        // this si used to convert FILETIME to long  suing bitwise
        private long ConvertFileTimeToLong(FILETIME time)
        {
            return ((long)time.dwHighDateTime << 32) |
                   (long)time.dwLowDateTime;
        }


    }
}
