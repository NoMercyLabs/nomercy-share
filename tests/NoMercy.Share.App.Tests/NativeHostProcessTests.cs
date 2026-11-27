using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.NativeHost;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.App.Tests;

public sealed class NativeHostProcessTests : IDisposable
{
    private readonly string _data = Path.Combine(Path.GetTempPath(), $"share-host-{Guid.NewGuid():N}");

    public NativeHostProcessTests() => Directory.CreateDirectory(_data);

    public void Dispose() => Directory.Delete(_data, recursive: true);

    private static string Exe => Path.Combine(AppContext.BaseDirectory, "nomercy-share.exe");

    private Process Start(string origin)
    {
        ProcessStartInfo info = new(Exe)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add(origin);
        info.Environment[SettingsFile.DataFolderVariable] = _data;
        return Process.Start(info)!;
    }

    private async Task<(JsonElement Reply, bool Exited)> Exchange(string origin, byte[] request)
    {
        new SettingsFile(SettingsFile.DefaultPath(_data)).Save(new ShareSettings { Capture = new CaptureSettings { Folder = Path.Combine(_data, "captures") } });
        using Process process = Start(origin);
        NativeMessageCodec codec = new(new NativeHostSettings());
        await codec.WriteAsync(process.StandardInput.BaseStream, request);
        await process.StandardInput.BaseStream.FlushAsync();
        byte[]? reply = await codec.ReadAsync(process.StandardOutput.BaseStream);
        bool exited = process.WaitForExit(5000);
        if (!exited)
        {
            process.Kill();
        }

        return (JsonDocument.Parse(reply ?? []).RootElement, exited);
    }

    private static byte[] Request() => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        type = "share",
        pngBase64 = Convert.ToBase64String(PngEncoder.Encode(new ScreenFrame(0, 0, 2, 2, new byte[16]))),
        pageUrl = "https://page.example.test/",
        mode = "visible",
    }));

    [Fact]
    public async Task TheExeServesOneRequestWithNoShareHostAndExits()
    {
        (JsonElement reply, bool exited) = await Exchange($"chrome-extension://{ShareDefaults.ExtensionId}/", Request());

        Assert.True(reply.GetProperty("ok").GetBoolean());
        Assert.True(reply.GetProperty("local").GetBoolean());
        Assert.True(File.Exists(reply.GetProperty("path").GetString()));
        Assert.True(exited);
    }

    [Fact]
    public async Task AnotherOriginGetsAnErrorReplyAndTheExeExits()
    {
        (JsonElement reply, bool exited) = await Exchange("chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/", Request());

        Assert.False(reply.GetProperty("ok").GetBoolean());
        Assert.True(exited);
        Assert.False(Directory.Exists(Path.Combine(_data, "captures")));
    }
}
