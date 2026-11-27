using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// Records one default sound device to a WAV file: the microphone, or what the default output plays (loopback).
/// A loopback sends no samples while nothing plays, so a gap is filled with silence and the file keeps real time.
/// </summary>
internal sealed class WasapiWavRecorder : IDisposable
{
    private const int GapToleranceDivisor = 10; // a gap under a tenth of a second is timer jitter, not silence

    private const int ZeroChunkBytes = 64 * 1024;

    private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(5);

    private readonly IWaveIn _capture;
    private readonly WaveFileWriter _writer;
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _written;

    private WasapiWavRecorder(IWaveIn capture, WaveFileWriter writer)
    {
        _capture = capture;
        _writer = writer;
    }

    /// <summary>The <see cref="Stopwatch.GetTimestamp"/> value taken just before the device was told to start.</summary>
    public long StartedAt { get; private set; }

    /// <summary>Starts the default output (loopback) or the default microphone. Null when the machine has no such device or it cannot be opened.</summary>
    public static WasapiWavRecorder? TryStart(bool loopback, string path)
    {
        IWaveIn? capture = null;
        WaveFileWriter? writer = null;
        try
        {
            capture = loopback ? new WasapiLoopbackCapture() : new WasapiCapture();
            writer = new WaveFileWriter(path, capture.WaveFormat);
            WasapiWavRecorder recorder = new(capture, writer);
            capture.DataAvailable += recorder.OnData;
            capture.RecordingStopped += (_, _) => recorder._stopped.TrySetResult();
            recorder.StartedAt = Stopwatch.GetTimestamp();
            capture.StartRecording();
            return recorder;
        }
        catch (COMException)
        {
            writer?.Dispose();
            capture?.Dispose();
            File.Delete(path);
            return null;
        }
    }

    /// <summary>Stops the device, fills the silence up to now and closes the file.</summary>
    public async Task StopAsync()
    {
        long stoppedAt = Stopwatch.GetTimestamp();
        _capture.StopRecording();
        await Task.WhenAny(_stopped.Task, Task.Delay(StopWait));
        FillGap(0, stoppedAt);
        Dispose();
    }

    public void Dispose()
    {
        _writer.Dispose();
        _capture.Dispose();
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        FillGap(e.BytesRecorded, Stopwatch.GetTimestamp());
        _writer.Write(e.Buffer, 0, e.BytesRecorded);
        _written += e.BytesRecorded;
    }

    /// <summary>Writes silence for the time up to <paramref name="until"/> that passed without samples, less the block about to be written.</summary>
    private void FillGap(int incoming, long until)
    {
        WaveFormat format = _capture.WaveFormat;
        double seconds = (until - StartedAt) / (double)Stopwatch.Frequency;
        long gap = (long)(seconds * format.AverageBytesPerSecond) - _written - incoming;
        if (gap < format.AverageBytesPerSecond / GapToleranceDivisor)
        {
            return;
        }

        gap -= gap % format.BlockAlign;
        byte[] zeros = new byte[(int)Math.Min(gap, ZeroChunkBytes - (ZeroChunkBytes % format.BlockAlign))];
        for (long left = gap; left > 0; left -= zeros.Length)
        {
            int count = (int)Math.Min(left, zeros.Length);
            _writer.Write(zeros, 0, count);
            _written += count;
        }
    }
}
