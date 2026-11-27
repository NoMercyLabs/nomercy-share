using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoMercy.Share.Core.NativeHost;

/// <summary>What the app answers: a link, a local path when no share host is set up, or the reason it could not share.</summary>
public sealed record NativeShareReply(bool Ok, string? Link = null, string? Path = null, bool? Local = null, string? Error = null)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static NativeShareReply Failure(string key, params object[] args) => new(false, Error: Strings.Get(key, args));

    public byte[] ToJson() => JsonSerializer.SerializeToUtf8Bytes(this, Json);
}
