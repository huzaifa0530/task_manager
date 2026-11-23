using System;
using System.Collections.Generic;
using System.Text;
using WindowsSystemManager.Application.DTOs.Processes;

namespace WindowsSystemManager.Application.Interfaces.Processes
{
    public interface IProcessService
    {
        Task<List<ProcessDto>> GetAllProcesses();
        ProcessDto GetProcessById(int processid);

    }
}
