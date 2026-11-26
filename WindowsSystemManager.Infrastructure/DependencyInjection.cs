using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using WindowsSystemManager.Application.Interfaces.Processes;
using WindowsSystemManager.Infrastructure.Services.Network;
using WindowsSystemManager.Infrastructure.Services.Processes;
using WindowsSystemManager.Infrastructure.Services.Processes.Cpu;
using WindowsSystemManager.Infrastructure.Services.Processes.Disk;
using WindowsSystemManager.Infrastructure.Services.Processes.Memory;
using WindowsSystemManager.Infrastructure.Services.Processes.Network;
using WindowsSystemManager.Infrastructure.Services.Processes.User;
namespace WindowsSystemManager.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
                   this IServiceCollection services)
        {
            services.AddScoped<IProcessService, ProcessService>();


            services.AddSingleton<PowerUsageMonitor>();
            services.AddTransient<ProcessCpuMonitor>();
            services.AddSingleton<ProcessCpuUsageCache>();
            services.AddSingleton<ProcessMemoryMonitor>();
            services.AddSingleton<ProcessDiskUsageCache>();

            services.AddSingleton<ProcessDiskEtwMonitor>();
            services.AddSingleton<ProcessNetworkEtwMonitor>();
            services.AddSingleton<ProcessNetworkUsageCache>();
            services.AddSingleton<ProcessNetworkRateCalculator>();

            services.AddSingleton<ProcessNetworkMonitor>();
            services.AddHostedService(provider =>
                provider.GetRequiredService<ProcessNetworkMonitor>());

            services.AddHostedService<ProcessDiskBackgroundMonitor>();
            services.AddHostedService<ProcessCpuBackgroundMonitor>();

            services.AddSingleton<GpuUsageCache>();
            services.AddHostedService<GpuBackgroundMonitor>();
            services.AddSingleton<ProcessUserMonitor>();
            return services;
        }
    }
}
