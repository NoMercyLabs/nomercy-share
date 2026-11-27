using System.Diagnostics;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// The sound devices of one recording and the WAV file each writes. A device the machine lacks is left out and named in <see cref="Notice"/>.
/// </summary>
internal sealed class RecordingSound
{
    private readonly List<(WasapiWavRecorder Recorder, string Path)> _devices = [];
    private readonly List<AudioTrack> _tracks = [];

    private RecordingSound()
    {
    }

    /// <summary>The sound files, system sound first; their offsets are measured from the first video frame.</summary>
    public IReadOnlyList<AudioTrack> Tracks => _tracks;

    /// <summary>What was left out and why, for the user; null when every device asked for is recording.</summary>
    public string? Notice { get; private set; }

    /// <summary>Starts every device the settings ask for. <paramref name="firstFrame"/> is the <see cref="Stopwatch.GetTimestamp"/> value of the first video frame.</summary>
    public static RecordingSound Start(AudioSettings audio, Func<string> newPath, long firstFrame)
    {
        RecordingSound sound = new();
        if (audio.SystemSound)
        {
            sound.Add(loopback: true, newPath(), firstFrame, "notice.recording.nooutput");
        }

        if (audio.Microphone)
        {
            sound.Add(loopback: false, newPath(), firstFrame, "notice.recording.nomic");
        }

        return sound;
    }

    /// <summary>Stops every device and closes its file.</summary>
    public async Task StopAsync()
    {
        foreach ((WasapiWavRecorder recorder, _) in _devices)
        {
            await recorder.StopAsync();
        }
    }

    /// <summary>Stops every device and removes its file.</summary>
    public async Task DiscardAsync()
    {
        await StopAsync();
        Delete();
    }

    /// <summary>Removes the WAV files. A file that cannot go is left for the user.</summary>
    public void Delete()
    {
        foreach ((_, string path) in _devices)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // The video is what matters; a stray WAV in the captures folder is harmless.
            }
        }
    }

    private void Add(bool loopback, string path, long firstFrame, string missingKey)
    {
        if (WasapiWavRecorder.TryStart(loopback, path) is not { } recorder)
        {
            Notice = Notice is null ? Strings.Get(missingKey) : $"{Notice} {Strings.Get(missingKey)}";
            return;
        }

        _devices.Add((recorder, path));
        _tracks.Add(new AudioTrack(path, Math.Max(0, (recorder.StartedAt - firstFrame) / (double)Stopwatch.Frequency)));
    }
}
