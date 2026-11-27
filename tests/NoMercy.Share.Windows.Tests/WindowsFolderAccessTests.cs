using NoMercy.Share.Platform.Windows;

namespace NoMercy.Share.Windows.Tests;

public sealed class WindowsFolderAccessTests
{
    [Fact]
    public void ProtectionIsReadFromTheRegistryWithoutThrowing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Exception? thrown = Record.Exception(() => new WindowsFolderAccess().IsProtectionOn());

        Assert.Null(thrown);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(0, false)]
    [InlineData(null, false)]
    public void OnlyTheValueOneMeansProtectionIsOn(int? value, bool expected) =>
        Assert.Equal(expected, WindowsFolderAccess.IsOn(value));

    [Fact]
    public void TheElevatedCommandAddsOnlyThisProgramAndQuotesItsPath()
    {
        string[] arguments = WindowsFolderAccess.AllowCommandArguments(@"C:\Apps\My App's\share.exe");

        Assert.Equal(["-NoProfile", "-Command", @"Add-MpPreference -ControlledFolderAccessAllowedApplications 'C:\Apps\My App''s\share.exe'"], arguments);
    }
}
