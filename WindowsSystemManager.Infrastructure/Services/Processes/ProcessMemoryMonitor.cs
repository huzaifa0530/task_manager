using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;


namespace WindowsSystemManager.Infrastructure.Services.Processes
{
    public class ProcessMemoryMonitor
    {
        public ulong GetPrivateWorkingSet(int processId)
        {
            using SafeProcessHandle processHandle =
                ProcessMemoryNativeMethods.OpenProcess(
                    0x0400,
                    false,
                    (uint)processId
                );
            ProcessMemoryCountersEx2 memory = new ProcessMemoryCountersEx2();

            memory.cb = (uint)Marshal.SizeOf<ProcessMemoryCountersEx2>();

            bool success = ProcessMemoryNativeMethods.GetProcessMemoryInfo(
                processHandle,
                ref memory,
                memory.cb
            );

            if (!success)
            {
                throw new InvalidOperationException(
                    "Failed to retrieve process memory information."
                );
            }

            return memory.PrivateWorkingSetSize.ToUInt64();
        }
    }
}
