using System;
using System.Collections.Generic;
using System.Text;

namespace WindowsSystemManager.Application.DTOs.Processes
{
    public class ProcessDto
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public long MemoryUsage { get; set; }
        public DateTime StartTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public double CPU { get; set; }
        public string Memory { get; set; } = string.Empty;
        public string Disk { get; set; } = string.Empty;
        public string Network { get; set; } = string.Empty;
        public string GPU { get; set; } = string.Empty;
        public string GPU_engine { get; set; } = string.Empty;
        public string Power_usage_trend { get; set; } = string.Empty;
        public string username { get; set; } = string.Empty;





    }
}
