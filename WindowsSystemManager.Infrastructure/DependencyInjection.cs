using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using WindowsSystemManager.Application.Interfaces.Processes;
using WindowsSystemManager.Infrastructure.Services.Processes;
namespace WindowsSystemManager.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
                   this IServiceCollection services)
        {
            services.AddScoped<IProcessService, ProcessService>();
            services.AddSingleton<ProcessNetworkMonitor>();

            services.AddHostedService(sp =>
                sp.GetRequiredService<ProcessNetworkMonitor>());

            services.AddSingleton<PowerUsageMonitor>();
            services.AddTransient<ProcessCpuMonitor>();
            services.AddSingleton<ProcessCpuUsageCache>();

            services.AddHostedService<ProcessCpuBackgroundMonitor>();
            return services;
        }
    }
}
