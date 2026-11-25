
using System.Collections.Concurrent;
using WindowsSystemManager.Infrastructure.Services.Processes.Network;

namespace WindowsSystemManager.Infrastructure.Services.Network;

public sealed class ProcessNetworkRateCalculator
{
    private readonly ProcessNetworkUsageCache _networkCache;

    private readonly ConcurrentDictionary<int, ProcessNetworkSnapshot>
        _previousMeasurements = new();

    public ProcessNetworkRateCalculator(
        ProcessNetworkUsageCache networkCache)
    {
        _networkCache = networkCache;
    }

    public ProcessNetworkRate Calculate(
        int processId,
        DateTime processStartTime)
    {
        var currentStats = _networkCache.GetNetworkStats(processId);

        var currentTime = DateTime.UtcNow;

        var currentMeasurement = new ProcessNetworkSnapshot
        {
            BytesSent = currentStats.BytesSent,
            BytesReceived = currentStats.BytesReceived,
            Timestamp = currentTime,
            ProcessStartTime = processStartTime
        };
        if (!_previousMeasurements.TryGetValue(
                processId, out var previousMeasurement)
            || previousMeasurement.ProcessStartTime != processStartTime)
        {
            _previousMeasurements[processId] = currentMeasurement;

            return new ProcessNetworkRate(0, 0);
        }
        var elapsedSeconds =
            (currentTime - previousMeasurement.Timestamp).TotalSeconds;
        if (elapsedSeconds <= 0 ||
            currentStats.BytesSent < previousMeasurement.BytesSent ||
            currentStats.BytesReceived < previousMeasurement.BytesReceived)
        {
            _previousMeasurements[processId] = currentMeasurement;

            return new ProcessNetworkRate(0, 0);
        }
        var sentDifference =
               currentStats.BytesSent - previousMeasurement.BytesSent;

      var receivedDifference =
            currentStats.BytesReceived - previousMeasurement.BytesReceived;
   var uploadBytesPerSecond =
            sentDifference / elapsedSeconds;
        var downloadBytesPerSecond =
            receivedDifference / elapsedSeconds;
        _previousMeasurements[processId] = currentMeasurement;
        return new ProcessNetworkRate(
            uploadBytesPerSecond,
            downloadBytesPerSecond);
    }

    public void RemoveProcess(int processId)
    {
        _previousMeasurements.TryRemove(processId, out _);
    }
}

public sealed class ProcessNetworkSnapshot
{
    public long BytesSent { get; set; }

    public long BytesReceived { get; set; }

    public DateTime Timestamp { get; set; }

    public DateTime ProcessStartTime { get; set; }
}

public sealed record ProcessNetworkRate(
    double UploadBytesPerSecond,
    double DownloadBytesPerSecond);
