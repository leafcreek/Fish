using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fish.Services.Updates;

internal sealed record UpdateRelease(string Version, Uri Page, Uri Download, long Size, string? Sha256, Uri? Checksums, string? Notes);
internal sealed record DownloadedUpdate(string Session, string Package, string Sha256);

internal sealed class GitHubUpdates(HttpClient client, string? downloadRoot = null)
{
    internal const string Repository = "leafcreek/Fish";
    internal const long MaximumPackageSize = 512L * 1024 * 1024;
    public static HttpClient CreateClient() => new() { Timeout = Timeout.InfiniteTimeSpan };

    public async Task<UpdateRelease?> CheckAsync(string currentVersion, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var request = Request(new Uri($"https://api.github.com/repos/{Repository}/releases/latest"));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new IOException("Das Release ist nicht erreichbar.");
        response.EnsureSuccessStatusCode();
        await response.Content.LoadIntoBufferAsync(1024 * 1024, timeout.Token).ConfigureAwait(false);
        var release = await response.Content.ReadFromJsonAsync<Release>(timeout.Token).ConfigureAwait(false)
            ?? throw new IOException("GitHub hat kein gültiges Release geliefert.");
        if (!ReleaseVersion.TryParse(currentVersion, out var current)) throw new IOException("Die installierte Version ist ungültig.");
        if (release.Draft || release.Prerelease || !ReleaseVersion.TryParse(release.Tag, out var latest))
            throw new IOException("GitHub hat keine reguläre Version geliefert.");
        if (latest <= current) return null;
        var package = release.Assets.SingleOrDefault(asset => asset.Name == "Fish.exe")
            ?? throw new IOException("Im Release fehlt Fish.exe.");
        if (package.Size is < 1 or > MaximumPackageSize) throw new IOException("Die Größe des Updates ist ungültig.");
        var checksums = release.Assets.SingleOrDefault(asset => asset.Name == "SHA256SUMS.txt");
        string? hash = ParseDigest(package.Digest);
        if (hash == null && checksums == null) throw new IOException("Im Release fehlt die Prüfsumme.");
        return new(release.Tag, GitHubUri(release.Page, "/releases/tag/"),
            GitHubUri(package.Url, "/releases/download/"), package.Size, hash,
            checksums == null ? null : GitHubUri(checksums.Url, "/releases/download/"), release.Body);
    }

    public async Task<DownloadedUpdate> DownloadAsync(UpdateRelease release, IProgress<double>? progress, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        string hash = release.Sha256 ?? await ReadChecksumAsync(release.Checksums!, timeout.Token).ConfigureAwait(false);
        string session = Guid.NewGuid().ToString("N");
        string folder = Path.Combine(downloadRoot ?? UpdateInstaller.CacheFolder, session);
        Directory.CreateDirectory(folder);
        string package = Path.Combine(folder, "Fish.exe");
        try
        {
            using var request = Request(release.Download);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is { } size && size != release.Size)
                throw new IOException("Der Download hat eine unerwartete Größe.");
            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            await using (var destination = new FileStream(package, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            using (var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[65536];
                long total = 0;
                int count;
                while ((count = await source.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) != 0)
                {
                    total += count;
                    if (total > release.Size) throw new IOException("Der Download ist größer als erwartet.");
                    digest.AppendData(buffer, 0, count);
                    await destination.WriteAsync(buffer.AsMemory(0, count), timeout.Token).ConfigureAwait(false);
                    progress?.Report(total * 100.0 / release.Size);
                }
                if (total != release.Size || !string.Equals(Convert.ToHexString(digest.GetHashAndReset()), hash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Die Prüfsumme des Downloads stimmt nicht. Das Update wurde verworfen.");
            }
            return new(session, package, hash);
        }
        catch
        {
            // Only remove the exact temporary file created by this download.
            File.Delete(package);
            throw;
        }
    }

    private async Task<string> ReadChecksumAsync(Uri uri, CancellationToken cancellation)
    {
        using var request = Request(uri);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await response.Content.LoadIntoBufferAsync(65536, cancellation).ConfigureAwait(false);
        string text = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
        return ParseChecksums(text);
    }

    internal static string ParseChecksums(string text)
    {
        var hashes = text.Split('\n').Select(line => line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 2 && parts[1].TrimStart('*') == "Fish.exe")
            .Select(parts => ParseDigest("sha256:" + parts[0]) ?? throw new IOException("Die Prüfsumme ist ungültig.")).ToArray();
        return hashes.Length == 1 ? hashes[0] : throw new IOException("Die Prüfsumme für Fish.exe fehlt oder ist mehrdeutig.");
    }

    internal static string? ParseDigest(string? digest) => digest is { Length: 71 } &&
        digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) && digest[7..].All(Uri.IsHexDigit)
            ? digest[7..].ToLowerInvariant() : null;

    private static Uri GitHubUri(string text, string suffix)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com" ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0 || !uri.AbsolutePath.StartsWith("/" + Repository + suffix, StringComparison.Ordinal))
            throw new IOException("Die Download-Adresse ist ungültig.");
        return uri;
    }

    private static HttpRequestMessage Request(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Fish", "1.0"));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        return request;
    }

    private sealed class Release
    {
        [JsonPropertyName("tag_name")] public string Tag { get; init; } = "";
        [JsonPropertyName("html_url")] public string Page { get; init; } = "";
        [JsonPropertyName("body")] public string? Body { get; init; }
        [JsonPropertyName("draft")] public bool Draft { get; init; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; init; }
        [JsonPropertyName("assets")] public Asset[] Assets { get; init; } = [];
    }
    private sealed class Asset
    {
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("browser_download_url")] public string Url { get; init; } = "";
        [JsonPropertyName("size")] public long Size { get; init; }
        [JsonPropertyName("digest")] public string? Digest { get; init; }
    }
}
