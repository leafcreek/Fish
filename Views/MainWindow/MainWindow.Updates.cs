using Fish.Services.Updates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fish.Views;

public sealed partial class MainWindow
{
    internal void ShowUpdateError(string message) => ShowError(message);
    private static readonly HttpClient UpdateHttp = GitHubUpdates.CreateClient();
    private readonly GitHubUpdates _updates = new(UpdateHttp);
    private CancellationTokenSource? _updateCancellation;
    private ContentDialog? _updateDialog;
    private UpdateHandoff? _updateHandoff;
    private bool _installingUpdate;

    private async void Version_Click(object sender, RoutedEventArgs args)
    {
        if (_updateCancellation != null || _closing) return;
        string label = VersionText.Text;
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        VersionButton.IsEnabled = false;
        UpdateRelease? release = null;
        try
        {
            VersionText.Text = "Prüfe …";
            release = await _updates.CheckAsync(label, cancellation.Token);
            VersionText.Text = label;
            if (_closing) return;
            if (release == null)
            {
                await ShowUpdateDialogAsync("Alles aktuell", "Fish ist aktuell.");
                return;
            }
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(new TextBlock { Text = "Beim Aktualisieren wird das Angeln beendet und Fish neu gestartet.", TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new HyperlinkButton { Content = "Änderungen", NavigateUri = release.Page, Padding = new Thickness(0) });
            var offer = new ContentDialog
            {
                XamlRoot = Root.XamlRoot, RequestedTheme = ElementTheme.Light,
                Title = "Fish " + release.Version + " ist verfügbar", Content = content,
                PrimaryButtonText = "Aktualisieren", CloseButtonText = "Später", DefaultButton = ContentDialogButton.Primary
            };
            _updateDialog = offer;
            if (await offer.ShowAsync() != ContentDialogResult.Primary || _closing) return;
            var download = await DownloadUpdateAsync(release, cancellation.Token);
            if (download == null || _closing) return;
            // Prepare and permission-check before stopping a working fishing session.
            VersionText.Text = "Bereite vor …";
            _updateHandoff = await UpdateInstaller.StartAsync(download, _store.Folder, cancellation.Token);
            if (_closing) { _updateHandoff.Dispose(); _updateHandoff = null; return; }
            _installingUpdate = true;
            SessionButton.IsEnabled = false;
            _cancellation?.Cancel();
            if (_session is { IsCompleted: false }) await _session;
            if (_closing) return;
            if (!_saveAllowed) throw new IOException("Die Einstellungen können gerade nicht gespeichert werden.");
            // OnClosing saves settings and approves the handoff only when closing succeeds.
            Close();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            _updateHandoff?.Dispose();
            _updateHandoff = null;
            if (!_closing)
                await ShowUpdateDialogAsync("Update nicht möglich",
                    error switch
                    {
                        OperationCanceledException => "GitHub antwortet gerade nicht. Bitte später erneut versuchen.",
                        HttpRequestException => "GitHub ist gerade nicht erreichbar. Bitte später erneut versuchen.",
                        UnauthorizedAccessException => "Fish kann seine EXE hier nicht ersetzen. Lade die neue Version herunter und ersetze die Datei selbst.",
                        IOException => error.Message,
                        _ => "Das Update konnte nicht vorbereitet werden. Bitte später erneut versuchen."
                    },
                    release?.Page);
        }
        finally
        {
            _updateCancellation = null;
            _updateDialog = null;
            _installingUpdate = false;
            if (!_closeAllowed)
            {
                VersionText.Text = label;
                VersionButton.IsEnabled = true;
                SessionButton.IsEnabled = !_closing;
            }
        }
    }

    private async Task<DownloadedUpdate?> DownloadUpdateAsync(UpdateRelease release, CancellationToken cancellation)
    {
        using var downloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 8, Width = 260 };
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot, RequestedTheme = ElementTheme.Light, Title = "Update wird geladen …",
            Content = progress, CloseButtonText = "Abbrechen", DefaultButton = ContentDialogButton.Close
        };
        dialog.Closed += (_, _) => downloadCancellation.Cancel();
        _updateDialog = dialog;
        var showing = dialog.ShowAsync().AsTask();
        try
        {
            return await _updates.DownloadAsync(release, new Progress<double>(value => progress.Value = value), downloadCancellation.Token);
        }
        catch (OperationCanceledException) when (downloadCancellation.IsCancellationRequested) { return null; }
        finally { dialog.Hide(); await showing; _updateDialog = null; }
    }

    private async Task ShowUpdateDialogAsync(string title, string text, Uri? download = null)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot, RequestedTheme = ElementTheme.Light, Title = title,
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = download == null ? "" : "Herunterladen",
            CloseButtonText = "Schließen", DefaultButton = ContentDialogButton.Close
        };
        _updateDialog = dialog;
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && download != null)
            await Windows.System.Launcher.LaunchUriAsync(download);
        _updateDialog = null;
    }
}
