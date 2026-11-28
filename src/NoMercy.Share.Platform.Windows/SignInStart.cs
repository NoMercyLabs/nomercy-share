using System.Runtime.Versioning;
using Microsoft.Win32;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Platform.Windows;

/// <summary>Starts the app hidden when the user signs in: one value under the user's own Run key, so no admin rights are needed.</summary>
[SupportedOSPlatform("windows")]
public sealed class SignInStart(string exePath, string softwareKey = "Software")
{
    public const string RunKey = @"Microsoft\Windows\CurrentVersion\Run";

    public const string ValueName = "NoMercy Share";

    private string Command => $"\"{exePath}\" {ShareDefaults.BackgroundArgument}";

    /// <summary>Adds or removes the entry. True when something was written.</summary>
    public bool Apply(bool on)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey($@"{softwareKey}\{RunKey}");
        string? current = key.GetValue(ValueName) as string;
        if (!on)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return current is not null;
        }

        if (current == Command)
        {
            return false;
        }

        key.SetValue(ValueName, Command);
        return true;
    }
}
