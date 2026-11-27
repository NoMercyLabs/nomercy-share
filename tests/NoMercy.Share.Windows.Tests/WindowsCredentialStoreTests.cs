using System.Diagnostics;
using NoMercy.Share.Core;
using NoMercy.Share.Platform.Windows;

namespace NoMercy.Share.Windows.Tests;

public sealed class WindowsCredentialStoreTests
{
    private static void CmdKey(params string[] arguments)
    {
        ProcessStartInfo start = new("cmdkey") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    [Fact]
    public void ANameThatIsNotStoredGivesNull()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Null(new WindowsCredentialStore().Read($"nomercy-share-test-missing-{Guid.NewGuid():N}"));
    }

    [Fact]
    public void AWrittenCredentialIsReadBackAndAnotherWriteReplacesIt()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string name = $"nomercy-share-test-{Guid.NewGuid():N}";
        WindowsCredentialStore store = new();
        try
        {
            store.Write(name, new StoredCredential("made-up-access-key", "made-up-secret-value"));
            Assert.Equal(new StoredCredential("made-up-access-key", "made-up-secret-value"), store.Read(name));

            store.Write(name, new StoredCredential("another-made-up-key", "another-made-up-secret"));
            Assert.Equal(new StoredCredential("another-made-up-key", "another-made-up-secret"), store.Read(name));
        }
        finally
        {
            CmdKey($"/delete:{name}");
        }
    }

    [Fact]
    public void AStoredCredentialIsReadBackAndItsToStringHidesTheSecret()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string name = $"nomercy-share-test-{Guid.NewGuid():N}";
        CmdKey($"/generic:{name}", "/user:made-up-access-key", "/pass:made-up-secret-value");
        try
        {
            StoredCredential? read = new WindowsCredentialStore().Read(name);

            Assert.Equal("made-up-access-key", read?.AccessKeyId);
            Assert.Equal("made-up-secret-value", read?.SecretAccessKey);
            Assert.DoesNotContain("made-up-secret-value", read?.ToString());
        }
        finally
        {
            CmdKey($"/delete:{name}");
        }
    }
}
