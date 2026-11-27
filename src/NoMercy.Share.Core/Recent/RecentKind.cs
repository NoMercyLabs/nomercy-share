using System.Text.Json.Serialization;

namespace NoMercy.Share.Core.Recent;

/// <summary>What a shared link points at; the list shows a thumbnail for an image and an icon for a video.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecentKind>))]
public enum RecentKind
{
    Image,

    Video
}
