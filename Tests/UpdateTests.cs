using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fish.Services.Updates;

internal static class UpdateTests
{
    public static async Task Run(Action<bool, string> check, string folder)
    {
        Directory.CreateDirectory(folder);
        check(ReleaseVersion.TryParse("v1.10.0", out var newer) && ReleaseVersion.TryParse("1.9.0+build", out var older) && newer > older &&
            !ReleaseVersion.TryParse("v2.0.0-beta.1", out _) && !ReleaseVersion.TryParse("invalid", out _),
            "Versionen werden numerisch verglichen; Vorabversionen und ungültige Angaben abgewiesen");
        byte[] package = Encoding.UTF8.GetBytes("Fish update fixture");
        string hash = Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant();
        string Json(string tag = "v1.1.0", string? digest = null, string host = "github.com", bool prerelease = false) => JsonSerializer.Serialize(new
        {
            tag_name = tag, html_url = $"https://github.com/leafcreek/Fish/releases/tag/{tag}", draft = false, prerelease,
            assets = new object[] {
                new { name = "Fish.exe", browser_download_url = $"https://{host}/leafcreek/Fish/releases/download/{tag}/Fish.exe", size = package.Length, digest },
                new { name = "SHA256SUMS.txt", browser_download_url = $"https://github.com/leafcreek/Fish/releases/download/{tag}/SHA256SUMS.txt", size = 76 }
            }
        });
        HttpResponseMessage Respond(HttpRequestMessage request, string json, byte[]? bytes = null)
        {
            if (request.RequestUri!.Host == "api.github.com") return new(HttpStatusCode.OK) { Content = new StringContent(json) };
            if (request.RequestUri.AbsolutePath.EndsWith("SHA256SUMS.txt")) return new(HttpStatusCode.OK) { Content = new StringContent(hash + "  Fish.exe\n") };
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes ?? package) };
        }
        using var http = new HttpClient(new Handler(request => Respond(request, Json(digest: "sha256:" + hash))));
        var updates = new GitHubUpdates(http, folder);
        var release = await updates.CheckAsync("v1.0.0", default);
        check(release is { Version: "v1.1.0" } && release.Sha256 == hash, "Neues GitHub-Release und SHA-256 erkannt");
        check(await updates.CheckAsync("v1.1.0", default) == null && await updates.CheckAsync("v2.0.0", default) == null,
            "Gleiche und ältere Releases lösen kein Update aus");
        using var untrusted = new HttpClient(new Handler(request => Respond(request, Json(host: "example.com"))));
        check(await Fails(() => new GitHubUpdates(untrusted).CheckAsync("v1.0.0", default)), "Download außerhalb des festgelegten GitHub-Repositorys abgewiesen");
        using var pre = new HttpClient(new Handler(request => Respond(request, Json(prerelease: true))));
        check(await Fails(() => new GitHubUpdates(pre).CheckAsync("v1.0.0", default)), "Vorabrelease wird nicht installiert");
        check(GitHubUpdates.ParseChecksums(hash + "  Fish.exe\r\n") == hash, "Vorhandenes SHA256SUMS-Format wird unterstützt");
        using var fallback = new HttpClient(new Handler(request => Respond(request, Json())));
        var fallbackUpdates = new GitHubUpdates(fallback, folder);
        var fallbackRelease = (await fallbackUpdates.CheckAsync("v1.0.0", default))!;
        var downloaded = await fallbackUpdates.DownloadAsync(fallbackRelease, null, default);
        try
        {
            check(File.ReadAllBytes(downloaded.Package).SequenceEqual(package), "Download und Prüfsummen-Fallback erfolgreich");
            using var corrupt = new HttpClient(new Handler(request => Respond(request, Json(digest: "sha256:" + hash), Encoding.UTF8.GetBytes("Fish update altered"))));
            check(await Fails(() => new GitHubUpdates(corrupt, folder).DownloadAsync(release!, null, default)), "Beschädigter Download wird abgewiesen");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await updates.CheckAsync("v1.0.0", cancelled.Token); check(false, "Abbruch"); }
            catch (OperationCanceledException) { check(true, "Update-Prüfung lässt sich abbrechen"); }
            string target = Path.Combine(folder, "Fish.exe");
            File.WriteAllText(target, "old Fish");
            string original = UpdateInstaller.Hash(target);
            UpdateInstaller.ApplyPackage(downloaded.Package, target, original, hash);
            check(File.ReadAllBytes(target).SequenceEqual(package) && File.ReadAllText(target + ".bak") == "old Fish", "Dateitausch legt die alte EXE als Sicherung ab");
            UpdateInstaller.RestoreBackup(target, original);
            check(File.ReadAllText(target) == "old Fish", "Sicherung lässt sich nach fehlgeschlagenem Neustart wiederherstellen");
            File.WriteAllText(target, "changed by another process");
            check(await Fails(() => Task.Run(() => UpdateInstaller.ApplyPackage(downloaded.Package, target, original, hash))) &&
                File.ReadAllText(target) == "changed by another process", "Zwischenzeitlich veränderte EXE bleibt unangetastet");
            try { UpdateInstaller.SessionFolder("../outside"); check(false, "Pfadbegrenzung"); }
            catch (IOException) { check(true, "Update-Aufträge bleiben auf ihren Cache-Ordner begrenzt"); }
        }
        finally { File.Delete(downloaded.Package); }
    }

    private static async Task<bool> Fails(Func<Task> action)
    {
        try { await action(); return false; }
        catch (IOException) { return true; }
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            return Task.FromResult(response(request));
        }
    }
}
