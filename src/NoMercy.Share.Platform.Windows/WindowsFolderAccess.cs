using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;
using NoMercy.Share.Core;

namespace NoMercy.Share.Platform.Windows;

/// <summary>Windows controlled folder access. It allows apps, not folders, so the ask adds this program to the allowed apps.</summary>
public sealed class WindowsFolderAccess : IFolderAccess
{
    private const string PolicyKey = @"SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access";
    private const string EnabledValue = "EnableControlledFolderAccess";
    private const int ProtectionOn = 1;
    private const int UacDeclined = 1223;

    public bool IsProtectionOn()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(PolicyKey);
            return IsOn(key?.GetValue(EnabledValue) as int?);
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>Only the value 1 is "on"; audit mode and block-only modes do not stop the app.</summary>
    public static bool IsOn(int? value) => value == ProtectionOn;

    /// <summary>The PowerShell arguments that add one program to the allowed apps, its path in single quotes.</summary>
    public static string[] AllowCommandArguments(string programPath) =>
        ["-NoProfile", "-Command", $"Add-MpPreference -ControlledFolderAccessAllowedApplications '{programPath.Replace("'", "''")}'"];

    public async Task<bool> RequestAccessAsync(CancellationToken cancellationToken = default)
    {
        if (Environment.ProcessPath is not { } program)
        {
            return false;
        }

        ProcessStartInfo start = new("powershell.exe")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (string argument in AllowCommandArguments(program))
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using Process? process = Process.Start(start);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch (Win32Exception error) when (error.NativeErrorCode == UacDeclined)
        {
            return false;
        }
    }
}
