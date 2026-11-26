using System;
using System.Collections.Generic;
using System.Text;

namespace WindowsSystemManager.Infrastructure.Services.Processes.Cpu
{
    public class ProcessCpuSnapshot
    {
        public DateTime CreationTime { get; set; }

        public long TotalProcessorTime { get; set; }

        public DateTime LastSampleTime { get; set; }
  
}
}
