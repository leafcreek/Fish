using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fish.Services;

public sealed record ReleaseInfo(Version Version, Uri Page, string Notes);
public static class UpdateService
{
    public static Version CurrentVersion => typeof(UpdateService).Assembly.GetName().Version ?? new(1, 0, 0);
    public static string ValidateRepository(string repository)
    {
        repository = repository.Trim();
        if (!Regex.IsMatch(repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))
            throw new ArgumentException("GitHub-Projekt als Besitzer/Projekt angeben, zum Beispiel leafcreek/Fish.");
        return repository;
    }
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
        return new(new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision)), uri,
            root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "");
    }
    public static async Task<ReleaseInfo?> CheckAsync(string repository, CancellationToken cancellation)
    {
        repository = ValidateRepository(repository);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Fish/" + CurrentVersion);
        using var response = await client.GetAsync($"https://api.github.com/repos/{repository}/releases/latest", cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return ParseRelease(await response.Content.ReadAsStringAsync(cancellation));
    }
}
