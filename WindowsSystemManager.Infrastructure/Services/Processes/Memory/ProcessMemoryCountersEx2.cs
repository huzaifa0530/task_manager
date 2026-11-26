using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsSystemManager.Infrastructure.Services.Processes.Memory
{
  


        [StructLayout(LayoutKind.Sequential)]
        public struct ProcessMemoryCountersEx2
        {


        public uint cb;
            public uint PageFaultCount;

            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;

            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;

            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;

            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;

            public UIntPtr PrivateUsage;
            public UIntPtr PrivateWorkingSetSize;

            public ulong SharedCommitUsage;
        }
    }

