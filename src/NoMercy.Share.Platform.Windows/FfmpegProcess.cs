using System.Diagnostics;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// One ffmpeg run. The arguments go in as a list, never as a command line string. It keeps the last line ffmpeg wrote
/// to its error output, which is where ffmpeg says why it failed.
/// </summary>
internal sealed class FfmpegProcess : IDisposable
{
    private readonly Process _process;
    private string _lastLine = string.Empty;

    private FfmpegProcess(Process process) => _process = process;

    /// <summary>The last non-blank line ffmpeg wrote to its error output.</summary>
    public string LastLine => Volatile.Read(ref _lastLine);

    public static FfmpegProcess Start(string ffmpegPath, IReadOnlyList<string> arguments)
    {
        ProcessStartInfo start = new(ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        Process process = new() { StartInfo = start };
        FfmpegProcess run = new(process);
        process.ErrorDataReceived += (_, line) =>
        {
            if (!string.IsNullOrWhiteSpace(line.Data))
            {
                Volatile.Write(ref run._lastLine, line.Data.Trim());
            }
        };
        process.Start();
        process.BeginErrorReadLine();
        return run;
    }

    /// <summary>Asks ffmpeg to finish the file: "q" on its input is its own stop key.</summary>
    public async Task RequestStopAsync()
    {
        try
        {
            await _process.StandardInput.WriteAsync('q');
            await _process.StandardInput.FlushAsync();
            _process.StandardInput.Close();
        }
        catch (IOException)
        {
            // ffmpeg already ended, so there is nothing left to tell it.
        }
    }

    /// <summary>Waits for the exit and returns its code. When the deadline passes the process tree is killed and a <see cref="RecordingException"/> says so.</summary>
    public async Task<int> WaitAsync(TimeSpan deadline, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(deadline);
        try
        {
            await _process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _process.Kill(entireProcessTree: true);
            throw new RecordingException(Strings.Get("error.ffmpeg.timeout"));
        }

        _process.WaitForExit(); // lets the last error lines arrive
        return _process.ExitCode;
    }

    /// <summary>The failure message for an exit code that is not 0.</summary>
    public RecordingException Failure(int exitCode) => new(Strings.Get("error.ffmpeg.exited", exitCode, LastLine));

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        _process.Dispose();
    }
}
