namespace NoMercy.Share.Core.Capture;

/// <summary>One pass of a two-pass encode: 1 studies the clip into the log, 2 reads the log and writes the file.</summary>
public readonly record struct EncodePass(int Number, string LogPath);
