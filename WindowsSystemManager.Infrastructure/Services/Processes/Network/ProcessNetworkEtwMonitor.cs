
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WindowsSystemManager.Infrastructure.Services.Processes.Network;

public enum NetworkDirection
{
    Send,
    Receive
}
// Extracts TCP and UDP network events from Windows event.
public class ProcessNetworkEtwMonitor : BackgroundService
{
    private const string SessionName =
        "WindowsSystemManagerNetworkSession";
    private readonly ILogger<ProcessNetworkEtwMonitor> _logger;
    public event Action<int, long, NetworkDirection>?
        NetworkActivityReceived;
    public ProcessNetworkEtwMonitor(
        ILogger<ProcessNetworkEtwMonitor> logger)
    {
        _logger = logger;
    }
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken){
        TraceEventSession? session = null;
        try
        {
            session = new TraceEventSession(SessionName);
            _logger.LogInformation("ETW session created.");
            session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.NetworkTCPIP);
            _logger.LogInformation("Kernel network events enabled.");
            var source = session.Source;
            var kernel = source.Kernel;
            // TCP SEND
            kernel.TcpIpSend += data =>
            {
                PublishNetworkActivity(
                    data.ProcessID,
                    data.size,
                    NetworkDirection.Send);
            };
            kernel.TcpIpRecv += data =>
            {
                PublishNetworkActivity(
                    data.ProcessID,
                    data.size,
                    NetworkDirection.Receive);
            };
            // UDP
            kernel.UdpIpSend += data =>
            {
                PublishNetworkActivity(
                    data.ProcessID,
                    data.size,
                    NetworkDirection.Send);
            };
            // UDP RECEIVE (IPv4)
            kernel.UdpIpRecv += data =>
            {
                PublishNetworkActivity(
                    data.ProcessID,
                    data.size,
                    NetworkDirection.Receive);
            };
            // UDP  (IPv6)
            kernel.UdpIpSendIPV6 += data =>
            {
                PublishNetworkActivity(
                    data.ProcessID,
                    data.size,
                    NetworkDirection.Send);
            };
            // UDP  (IPv6)
            kernel.UdpIpRecvIPV6 += data =>
            {
                PublishNetworkActivity(
                    data.ProcessID,
                    data.size,
                    NetworkDirection.Receive);
            };
            _logger.LogInformation(
                "TCP and UDP IPv4/IPv6 event handlers registered.");
            using var registration = stoppingToken.Register(
                () => session?.Dispose());
            _logger.LogInformation(
                "Network ETW event processing started.");
            await Task.Run(
                () => source.Process(),
                CancellationToken.None);
        }
        catch (Exception) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("ETW monitoring stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ETW monitoring failed.");
            throw;
        }
        finally
        {
            session?.Dispose();
            _logger.LogInformation("ETW session disposed.");
        }
    }
    private void PublishNetworkActivity(
        int processId,
        long bytes,
        NetworkDirection direction)
    {
        NetworkActivityReceived?.Invoke(
            processId,
            bytes,
            direction);
    }
}
