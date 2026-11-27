using System.Text.Json;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

public sealed class DiscordSettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-discord-defaults-test-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private ShareSettings Load(string json)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsFile.DefaultPath(_folder), json);
        return new SettingsFile(SettingsFile.DefaultPath(_folder)).Load();
    }

    [Fact]
    public void TheDefaultsAreNamedSettings()
    {
        DiscordSettings defaults = new();

        Assert.Equal("nomercy-share-discord", defaults.CredentialName);
        Assert.Equal(10, defaults.TimeoutSeconds);
        Assert.Equal(10, defaults.MaxUploadMegabytes);
        Assert.Equal("discord", DiscordSettings.ProviderName);
    }

    [Fact]
    public void AFileWrittenWithTheOldDiscordSwitchStillLoadsWithTheSameValues()
    {
        ShareSettings loaded = Load("""
            {"Storage":{"Provider":"r2","Endpoint":"https://storage.example.test","Bucket":"made-up-bucket"},
             "Discord":{"Enabled":true,"CredentialName":"my-discord","TimeoutSeconds":25},
             "Expiry":{"DefaultDays":7}}
            """);

        Assert.Equal("r2", loaded.Storage.Provider);
        Assert.Equal("made-up-bucket", loaded.Storage.Bucket);
        Assert.Equal("my-discord", loaded.Discord.CredentialName);
        Assert.Equal(25, loaded.Discord.TimeoutSeconds);
        Assert.Equal(10, loaded.Discord.MaxUploadMegabytes);
        Assert.Equal(7, loaded.Expiry.DefaultDays);
    }

    [Fact]
    public void AFileFromBeforeDiscordStillLoadsWithTheSameValues()
    {
        ShareSettings loaded = Load("""{"Storage":{"Provider":"s3","Endpoint":"https://storage.example.test"},"Expiry":{"DefaultDays":null}}""");

        Assert.Equal("s3", loaded.Storage.Provider);
        Assert.Equal("https://storage.example.test", loaded.Storage.Endpoint);
        Assert.Null(loaded.Expiry.DefaultDays);
        Assert.Equal(new DiscordSettings(), loaded.Discord);
    }

    [Fact]
    public void ASavedFileHoldsNoDiscordSwitch()
    {
        string path = SettingsFile.DefaultPath(_folder);
        new SettingsFile(path).Save(new ShareSettings());

        using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(path));
        Assert.False(saved.RootElement.GetProperty("Discord").TryGetProperty("Enabled", out _));
    }

    [Fact]
    public void ProviderChoiceNamesDiscord()
    {
        Assert.Equal("Discord", new ProviderChoice("discord").Label);
    }

    [Fact]
    public void RecordingKeepsItsOwnCapOffDiscord()
    {
        ShareSettings settings = new() { Recording = new RecordingSettings { MaxSizeMegabytes = 50 }, Discord = new DiscordSettings { MaxUploadMegabytes = 10 } };

        Assert.Equal(50, settings.EffectiveRecording().MaxSizeMegabytes);
    }

    [Fact]
    public void OnDiscordTheRecordingCapIsTheSmallerOfTheTwoLimits()
    {
        ShareSettings big = new()
        {
            Storage = new StorageSettings { Provider = "discord" },
            Recording = new RecordingSettings { MaxSizeMegabytes = 50 },
            Discord = new DiscordSettings { MaxUploadMegabytes = 10 }
        };
        ShareSettings small = big with { Recording = new RecordingSettings { MaxSizeMegabytes = 5 } };

        Assert.Equal(10, big.EffectiveRecording().MaxSizeMegabytes);
        Assert.Equal(5, small.EffectiveRecording().MaxSizeMegabytes);
    }

    [Fact]
    public void TheEffectiveRecordingKeepsEveryOtherValue()
    {
        ShareSettings settings = new()
        {
            Storage = new StorageSettings { Provider = "Discord" },
            Recording = new RecordingSettings { MaxSizeMegabytes = 50, FrameRate = 24 }
        };

        RecordingSettings effective = settings.EffectiveRecording();

        Assert.Equal(10, effective.MaxSizeMegabytes);
        Assert.Equal(24, effective.FrameRate);
    }
}
