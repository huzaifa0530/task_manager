
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsSystemManager.Infrastructure.Services.Processes.User;

public sealed class ProcessUserMonitor
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenUser = 1;//it tell which info w eneed form security token
    private const int ErrorInsufficientBuffer = 122; // it tel about how much buffer we need 

    private readonly ConcurrentDictionary<int, CacheEntry> _cache = new();

    public string GetProcessUserName(int processId, DateTime processStartTime)
    {
        if (_cache.TryGetValue(processId, out var cached) &&
            cached.StartTime == processStartTime)
        {
            return cached.UserName;
        }

        string userName = GetUserNameFromWindows(processId);

        _cache[processId] = new CacheEntry(processStartTime, userName);

        return userName;
    }

    private static string GetUserNameFromWindows(int processId)
    {
       //intrptr is for mmery addres
        IntPtr processHandle = IntPtr.Zero;
        IntPtr tokenHandle = IntPtr.Zero;
        IntPtr tokenInformation = IntPtr.Zero;//pointer refrence

        try
        {
            processHandle = OpenProcess(
                ProcessQueryLimitedInformation,
                false,
                processId);

            if (processHandle == IntPtr.Zero)
                return "Unavailable";

            if (!OpenProcessToken(
                    processHandle,
                    TokenQuery,
                    out tokenHandle))
            {
                return "Unavailable";
            }

            GetTokenInformation(
                tokenHandle,
                TokenUser,
                IntPtr.Zero,
                0,
                out uint requiredSize);

            int error = Marshal.GetLastWin32Error();

            if (requiredSize == 0 || error != ErrorInsufficientBuffer)
                return "Unavailable";

            tokenInformation = Marshal.AllocHGlobal((int)requiredSize);

            if (!GetTokenInformation(
                    tokenHandle,
                    TokenUser,
                    tokenInformation,
                    requiredSize,
                    out _))
            {
                return "Unavailable";
            }

            TOKEN_USER tokenUser =
                Marshal.PtrToStructure<TOKEN_USER>(tokenInformation);

            IntPtr sid = tokenUser.User.Sid;

            uint nameLength = 0;
            uint domainLength = 0;

            LookupAccountSid(
                null,
                sid,
                null,
                ref nameLength,
                null,
                ref domainLength,
                out _);

            error = Marshal.GetLastWin32Error();

            if (error != ErrorInsufficientBuffer)
                return "Unavailable";

            var accountName = new StringBuilder((int)nameLength);
            var domainName = new StringBuilder((int)domainLength);

            if (!LookupAccountSid(
                    null,
                    sid,
                    accountName,
                    ref nameLength,
                    domainName,
                    ref domainLength,
                    out _))
            {
                return "Unavailable";
            }

            return accountName.ToString();
        }
        catch (Exception)
        {
            return "Unavailable";
        }
        finally
        {
            if (tokenInformation != IntPtr.Zero)
                Marshal.FreeHGlobal(tokenInformation);

            if (tokenHandle != IntPtr.Zero)
                CloseHandle(tokenHandle);

            if (processHandle != IntPtr.Zero)
                CloseHandle(processHandle);
        }
    }

    public void RemoveExitedProcesses(HashSet<int> activeProcessIds)
    {
        foreach (int processId in _cache.Keys)
        {
            if (!activeProcessIds.Contains(processId))
                _cache.TryRemove(processId, out _);
        }
    }

    private sealed record CacheEntry(
        DateTime StartTime,
        string UserName);

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_USER
    {
        public SID_AND_ATTRIBUTES User;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SID_AND_ATTRIBUTES
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint desiredAccess,
        bool inheritHandle,
        int processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(
        IntPtr processHandle,
        uint desiredAccess,
        out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        IntPtr tokenInformation,
        uint tokenInformationLength,
        out uint returnLength);

    [DllImport(
        "advapi32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern bool LookupAccountSid(
        string? systemName,
        IntPtr sid,
        StringBuilder? name,
        ref uint nameLength,
        StringBuilder? referencedDomainName,
        ref uint referencedDomainNameLength,
        out int use);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
