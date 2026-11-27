using System.Globalization;
using System.Resources;

namespace NoMercy.Share.Core;

/// <summary>The user text of the app. English is the neutral resource; another language is a satellite file next to Strings.resx.</summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new("NoMercy.Share.Core.Strings", typeof(Strings).Assembly);

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en");

    public static string Get(string key) =>
        Manager.GetString(key, Culture) ?? throw new KeyNotFoundException($"no text for key {key}");

    public static string Get(string key, params object[] args) => string.Format(Culture, Get(key), args);
}
