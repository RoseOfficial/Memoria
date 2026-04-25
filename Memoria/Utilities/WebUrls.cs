using System;
using System.Text.RegularExpressions;

namespace Memoria.Utilities;

/// <summary>
/// Builds deep-link URLs into the Memoria web app.
/// </summary>
/// <remarks>
/// Pass the base URL from <c>Configuration.WebBaseUrl</c> on every call so dev/staging overrides work.
/// </remarks>
public static class WebUrls
{
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Build the per-character profile URL: <c>{base}/c/{Name-Slug}@{World}</c>.</summary>
    public static string ProfileUrl(string baseUrl, string characterName, string worldName)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL is required.", nameof(baseUrl));
        if (string.IsNullOrWhiteSpace(characterName))
            throw new ArgumentException("Character name is required.", nameof(characterName));
        if (string.IsNullOrWhiteSpace(worldName))
            throw new ArgumentException("World name is required.", nameof(worldName));

        var trimmed = baseUrl.TrimEnd('/');
        var slug = SlugifyName(characterName);
        var world = Uri.EscapeDataString(worldName);
        return $"{trimmed}/c/{slug}@{world}";
    }

    /// <summary>Build the personal dashboard URL: <c>{base}/me</c>.</summary>
    public static string MeUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL is required.", nameof(baseUrl));
        return $"{baseUrl.TrimEnd('/')}/me";
    }

    /// <summary>Build the plugin↔web link redemption URL: <c>{base}/me/link</c>.</summary>
    public static string LinkUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL is required.", nameof(baseUrl));
        return $"{baseUrl.TrimEnd('/')}/me/link";
    }

    /// <summary>Return the landing page URL.</summary>
    public static string LandingUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL is required.", nameof(baseUrl));
        return baseUrl.TrimEnd('/');
    }

    private static string SlugifyName(string name)
    {
        // Replace each whitespace run with a single dash, then percent-encode
        // anything else that isn't URL-safe. Apostrophes (Y'shtola) and other
        // special chars get %-encoded; ASCII letters and hyphens pass through unchanged.
        var dashed = WhitespaceRun.Replace(name.Trim(), "-");
        return Uri.EscapeDataString(dashed);
    }
}
