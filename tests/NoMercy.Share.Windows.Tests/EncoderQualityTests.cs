using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using Xunit.Abstractions;

namespace NoMercy.Share.Windows.Tests;

/// <summary>Bamo's two-pass VP9 against the one-pass H.264 of before, on the same clip and the same tight budget, with the app's own arguments.</summary>
[Collection(RecordingCollection.Name)]
public sealed partial class EncoderQualityTests(ITestOutputHelper output) : IDisposable
{
    private const double Seconds = 20;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-quality-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [RecordingFact]
    public void TwoPassVp9KeepsMorePictureThanOnePassH264InTheSameSize()
    {
        Directory.CreateDirectory(_folder);
        string raw = Path.Combine(_folder, "raw.mkv");
        Run(["-f", "lavfi", "-i", string.Create(CultureInfo.InvariantCulture, $"testsrc2=s=1920x1080:r=30:d={Seconds}"), "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18", "-y", raw]);
        RecordingSettings tight = new() { MaxSizeMegabytes = 2 }; // 2 MB over 20 s: the squeeze a 10 MB Discord cap puts on a 100 s clip
        VideoPlan plan = VideoBudget.Plan(Seconds, 1920, 1080, tight);

        string h264 = Path.Combine(_folder, "h264.mp4");
        Run(FfmpegArguments.Compress(raw, h264, plan, encoder: VideoEncoder.From(tight)));
        string vp9 = Path.Combine(_folder, "vp9.webm");
        VideoEncoder twoPass = VideoEncoder.From(tight with { Codec = "vp9", Speed = "balanced" });
        EncodePass study = new(1, Path.Combine(_folder, "pass"));
        Run(FfmpegArguments.Compress(raw, vp9, plan, encoder: twoPass, pass: study));
        Run(FfmpegArguments.Compress(raw, vp9, plan, encoder: twoPass, pass: study with { Number = 2 }));

        (double h264Score, double vp9Score) = (Ssim(h264, raw, plan), Ssim(vp9, raw, plan));
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"plan {plan.Width}x{plan.Height} at {plan.VideoKbps} kbps; h264 {Megabytes(h264):F2} MB ssim {h264Score:F4}; vp9 two-pass {Megabytes(vp9):F2} MB ssim {vp9Score:F4}"));
        Assert.True(Megabytes(vp9) <= tight.MaxSizeMegabytes, $"vp9 is {Megabytes(vp9):F2} MB");
        Assert.True(vp9Score > h264Score, $"vp9 {vp9Score:F4} is not above h264 {h264Score:F4}");
    }

    private static double Megabytes(string file) => new FileInfo(file).Length / 1024.0 / 1024.0;

    /// <summary>The SSIM of the encode against the source scaled to the same size; 1 is identical.</summary>
    private static double Ssim(string encoded, string source, VideoPlan plan)
    {
        string log = Run(["-i", encoded, "-i", source, "-lavfi",
            string.Create(CultureInfo.InvariantCulture, $"[1:v]scale={plan.Width}:{plan.Height}:flags=lanczos[ref];[0:v][ref]ssim"), "-f", "null", "-"], logLevel: "info");
        return double.Parse(SsimAll().Match(log).Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static string Run(IReadOnlyList<string> arguments, string logLevel = "error")
    {
        ProcessStartInfo start = new(FfmpegTools.Ffmpeg()!) { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in arguments.Where(argument => argument is not ("-loglevel" or "warning")))
        {
            start.ArgumentList.Add(argument);
        }

        start.ArgumentList.Insert(0, logLevel);
        start.ArgumentList.Insert(0, "-loglevel");
        using Process process = Process.Start(start)!;
        string errors = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, errors);
        return errors;
    }

    [GeneratedRegex(@"All:([0-9.]+)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SsimAll();
}
