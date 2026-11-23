using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
namespace WindowsSystemManager.Infrastructure
{
    public class Class1
    {
        public void GetAllProcess()
        {
            Process[] allprocesses = Process.GetProcesses();
            foreach (Process p in allprocesses)
            {
                Console.WriteLine(p);
            }
        }
    }
}