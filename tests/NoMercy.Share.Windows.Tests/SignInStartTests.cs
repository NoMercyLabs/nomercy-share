using System.Runtime.Versioning;
using Microsoft.Win32;
using NoMercy.Share.Platform.Windows;

namespace NoMercy.Share.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed class SignInStartTests : IDisposable
{
    private const string Exe = @"C:\Apps\Share\nomercy-share.exe";

    private readonly string _softwareKey = $@"Software\NoMercyShareTest\{Guid.NewGuid():N}";

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(@"Software\NoMercyShareTest", throwOnMissingSubKey: false);

    private string? RunValue() =>
        Registry.CurrentUser.OpenSubKey($@"{_softwareKey}\{SignInStart.RunKey}")?.GetValue(SignInStart.ValueName) as string;

    [Fact]
    public void OnStartsTheAppHiddenAtSignIn()
    {
        Assert.True(new SignInStart(Exe, _softwareKey).Apply(true));

        Assert.Equal($"\"{Exe}\" --background", RunValue());
    }

    [Fact]
    public void ASecondApplyWithNothingChangedWritesNothing()
    {
        new SignInStart(Exe, _softwareKey).Apply(true);

        Assert.False(new SignInStart(Exe, _softwareKey).Apply(true));
    }

    [Fact]
    public void AMovedExeIsWrittenAgain()
    {
        new SignInStart(Exe, _softwareKey).Apply(true);

        Assert.True(new SignInStart(@"C:\Other\nomercy-share.exe", _softwareKey).Apply(true));
        Assert.Equal("\"C:\\Other\\nomercy-share.exe\" --background", RunValue());
    }

    [Fact]
    public void OffRemovesTheEntry()
    {
        new SignInStart(Exe, _softwareKey).Apply(true);

        Assert.True(new SignInStart(Exe, _softwareKey).Apply(false));
        Assert.Null(RunValue());
        Assert.False(new SignInStart(Exe, _softwareKey).Apply(false));
    }
}
