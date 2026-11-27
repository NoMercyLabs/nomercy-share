using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// One headless browser with its own empty profile (so no cookies and no sessions). Disposing it kills the browser and its
/// children, and deletes the profile. It only ever touches the process it started.
/// </summary>
internal sealed class BrowserProcess : IDisposable
{
    private const int DeleteAttempts = 20;

    private static readonly HttpClient Http = new();

    private readonly Process _process;
    private readonly string _profile;
    private int? _browserId;

    private BrowserProcess(Process process, string profile)
    {
        _process = process;
        _profile = profile;
    }

    /// <summary>The id of the browser process itself, known once <see cref="WaitForPageAsync"/> has found it.</summary>
    public int Id => _browserId ?? _process.Id;

    public static BrowserProcess Start(string browserPath, string profile)
    {
        Directory.CreateDirectory(profile);
        ProcessStartInfo start = new(browserPath) { UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in Arguments(profile))
        {
            start.ArgumentList.Add(argument);
        }

        Process process = new() { StartInfo = start };
        BrowserProcess browser = new(process, profile);
        try
        {
            process.Start();
        }
        catch
        {
            browser.Dispose();
            throw;
        }

        return browser;
    }

    private static string[] Arguments(string profile) =>
    [
        "--headless=new",
        "--remote-debugging-port=0",
        $"--user-data-dir={profile}",
        "--hide-scrollbars",
        "--mute-audio",
        "--no-first-run",
        "--no-default-browser-check",
        "about:blank"
    ];

    /// <summary>
    /// The DevTools address of the first page. The browser writes its port and its own address to a file in the profile once it listens.
    /// Edge hands the work to another process and the one started here exits at once, so the real process id is asked of the browser.
    /// </summary>
    public async Task<Uri> WaitForPageAsync(CancellationToken cancellationToken)
    {
        string portFile = Path.Combine(_profile, "DevToolsActivePort");
        string[] lines = [];
        while (lines.Length < 2 || !int.TryParse(lines[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_process.HasExited && _process.ExitCode != 0)
            {
                throw new WebCaptureException(WebCaptureField.None, Strings.Get("web.error.failed"));
            }

            lines = ReadLines(portFile);
            if (lines.Length < 2)
            {
                await Task.Delay(50, cancellationToken);
            }
        }

        int port = int.Parse(lines[0], CultureInfo.InvariantCulture);
        _browserId = await FindBrowserIdAsync(new Uri($"ws://127.0.0.1:{port}{lines[1].Trim()}"), cancellationToken);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await FindPageAsync(port, cancellationToken) is { } page)
            {
                return page;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    public void Dispose()
    {
        Stop(_process);
        if (_browserId is { } id && id != _process.Id)
        {
            try
            {
                using Process browser = Process.GetProcessById(id);
                Stop(browser);
            }
            catch (ArgumentException)
            {
                // Already gone.
            }
        }

        _process.Dispose();
        DeleteProfile();
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
            // The process never started or is already gone.
        }
    }

    private static string[] ReadLines(string portFile)
    {
        try
        {
            return File.ReadAllLines(portFile);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return []; // the file is not there yet, or the browser is still writing it
        }
    }

    /// <summary>Asks the browser for its own process id (the process of type "browser").</summary>
    private static async Task<int?> FindBrowserIdAsync(Uri browserAddress, CancellationToken cancellationToken)
    {
        using CdpConnection connection = await CdpConnection.ConnectAsync(browserAddress, cancellationToken);
        JsonElement info = await connection.SendAsync("SystemInfo.getProcessInfo", null, cancellationToken);
        foreach (JsonElement entry in info.GetProperty("processInfo").EnumerateArray())
        {
            if (entry.GetProperty("type").GetString() == "browser")
            {
                return entry.GetProperty("id").GetInt32();
            }
        }

        return null;
    }

    private static async Task<Uri?> FindPageAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            string json = await Http.GetStringAsync(new Uri($"http://127.0.0.1:{port}/json/list"), cancellationToken);
            using JsonDocument targets = JsonDocument.Parse(json);
            foreach (JsonElement target in targets.RootElement.EnumerateArray())
            {
                if (target.GetProperty("type").GetString() == "page" && target.TryGetProperty("webSocketDebuggerUrl", out JsonElement socket))
                {
                    return new Uri(socket.GetString()!);
                }
            }
        }
        catch (HttpRequestException)
        {
            // The browser is not answering yet.
        }

        return null;
    }

    /// <summary>The browser's files stay locked for a moment after it exits, so deleting is tried again.</summary>
    private void DeleteProfile()
    {
        for (int attempt = 1; attempt <= DeleteAttempts && Directory.Exists(_profile); attempt++)
        {
            try
            {
                Directory.Delete(_profile, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
