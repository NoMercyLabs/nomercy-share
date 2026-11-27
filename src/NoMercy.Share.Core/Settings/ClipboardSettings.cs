using System.Text.Json.Serialization;

namespace NoMercy.Share.Core.Settings;

/// <summary>What the clipboard holds after a capture has been shared.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClipboardContent>))]
public enum ClipboardContent
{
    /// <summary>The share link as text.</summary>
    Link,

    /// <summary>The picture, as a capture did before sharing existed.</summary>
    Image,

    /// <summary>The picture and the link: a paste into a text field gives the link, into an editor the picture.</summary>
    Both
}

public sealed record ClipboardSettings
{
    public ClipboardContent Content { get; init; } = ShareDefaults.ClipboardContent;
}
