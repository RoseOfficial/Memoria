using System;

namespace AlphaScope.Utilities;

/// <summary>
/// Builds deep-link URLs into the AlphaScope web app.
/// </summary>
/// <remarks>
/// Pass the base URL from <c>Configuration.WebBaseUrl</c> on every call so dev/staging overrides work.
/// </remarks>
public static class WebUrls
{
    /// <summary>Build the per-character profile URL: <c>{base}/c/{Name-Slug}@{World}</c>.</summary>
    public static string ProfileUrl(string baseUrl, string characterName, string worldName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            throw new ArgumentException("Character name is required.", nameof(characterName));
        if (string.IsNullOrWhiteSpace(worldName))
            throw new ArgumentException("World name is required.", nameof(worldName));

        var trimmed = (baseUrl ?? string.Empty).TrimEnd('/');
        var slug = SlugifyName(characterName);
        var world = Uri.EscapeDataString(worldName);
        return $"{trimmed}/c/{slug}@{world}";
    }

    /// <summary>Build the personal dashboard URL: <c>{base}/me</c>.</summary>
    public static string MeUrl(string baseUrl) => $"{(baseUrl ?? string.Empty).TrimEnd('/')}/me";

    /// <summary>Return the landing page URL.</summary>
    public static string LandingUrl(string baseUrl) => (baseUrl ?? string.Empty).TrimEnd('/');

    private static string SlugifyName(string name)
    {
        // Replace each whitespace run with a single dash, then percent-encode
        // anything else that isn't URL-safe. Apostrophes (Y'shtola) and other
        // special chars get %-encoded; ASCII letters and dashes pass through.
        var dashed = System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"\s+", "-");
        return Uri.EscapeDataString(dashed).Replace("%2D", "-");
    }
}
