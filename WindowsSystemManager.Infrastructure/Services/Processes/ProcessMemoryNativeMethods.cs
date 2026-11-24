using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using static WindowsSystemManager.Infrastructure.Services.Processes.ProcessCpuMonitor;

namespace WindowsSystemManager.Infrastructure.Services.Processes
{
    static class ProcessMemoryNativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern SafeProcessHandle OpenProcess(
    uint dwDesiredAccess,
    [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
    uint dwProcessId
);

        //gave structure of a memry so i cna fill data and tell teh size of memry in byte
        [DllImport("Psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetProcessMemoryInfo(
                    SafeProcessHandle Process,
                    ref ProcessMemoryCountersEx2 ppsmemCounters,
                    uint cb
                );
    }
}
