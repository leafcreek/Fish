using System.Net;
using System.Text.Json;

namespace Fish.Services;

public sealed record ReleaseInfo(Version Version, Uri Page);
public static class UpdateService
{
    public const string Repository = "leafcreek/Fish";
    public static Version CurrentVersion => typeof(UpdateService).Assembly.GetName().Version ?? new(1, 0, 0);
    public static ReleaseInfo ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
            throw new InvalidDataException("Diese Veröffentlichung ist keine stabile Version.");
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version))
            throw new InvalidDataException("Versionstag nicht lesbar. Erwartet wird beispielsweise v1.2.3.");
        if (!Uri.TryCreate(root.GetProperty("html_url").GetString(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com")
            throw new InvalidDataException("Ungültige Release-Adresse.");
        return new(new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision)), uri);
    }
    public static async Task<ReleaseInfo?> CheckAsync(CancellationToken cancellation)
    {

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Fish/" + CurrentVersion);
        using var response = await client.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return ParseRelease(await response.Content.ReadAsStringAsync(cancellation));
    }
}
