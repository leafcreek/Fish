using Fish.Services.Updates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Fish.Views;

public sealed partial class MainWindow
{
    internal void ShowUpdateError(string message) => ShowError(message);
    private static readonly HttpClient UpdateHttp = GitHubUpdates.CreateClient();
    private readonly GitHubUpdates _updates = new(UpdateHttp);
    private CancellationTokenSource? _updateCancellation;
    private ContentDialog? _updateDialog;
    private Flyout? _statusFlyout;
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
                ShowCurrentVersion(label);
                return;
            }
            var notes = new TextBox
            {
                IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 180,
                Text = string.IsNullOrWhiteSpace(release.Notes) ? "Für diese Version sind keine Änderungen beschrieben." : release.Notes
            };
            ScrollViewer.SetVerticalScrollBarVisibility(notes, ScrollBarVisibility.Auto);
            AutomationProperties.SetName(notes, "Release-Notizen");
            AutomationProperties.SetAutomationId(notes, "UpdateReleaseNotes");
            var changes = new Expander
            {
                Header = "Änderungen", Content = notes, HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetAutomationId(changes, "UpdateChanges");
            var offer = new ContentDialog
            {
                XamlRoot = Root.XamlRoot, RequestedTheme = ElementTheme.Light,
                Title = "Fish " + release.Version + " ist verfügbar", Content = changes,
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
            if (await PrepareCloseAsync()) Close();
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

    private void ShowCurrentVersion(string version)
        => ShowStatusFlyout(VersionButton, "Alles aktuell", "Fish " + version, "UpdateStatus");

    private void ShowStatusFlyout(FrameworkElement target, string title, string detail, string automationId, bool autoDismiss = false)
    {
        _statusFlyout?.Hide();
        var content = new Grid { ColumnSpacing = 10 };
        content.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        content.Children.Add(new Border
        {
            Width = 32, Height = 32, CornerRadius = new(10), Background = Appearance.AccentSoft,
            Child = new FontIcon { Glyph = "\uE73E", FontSize = 15, Foreground = Appearance.Running }
        });
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = title, FontSize = 13, Foreground = Appearance.Ink,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = detail, FontSize = 11, Foreground = Appearance.Muted });
        Grid.SetColumn(text, 1);
        content.Children.Add(text);
        AutomationProperties.SetAutomationId(content, automationId);
        var style = new Style(typeof(FlyoutPresenter));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Appearance.White));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, (Brush)Application.Current.Resources["FishStroke"]));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(12)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12)));
        style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 200d));
        style.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, 260d));
        var flyout = new Flyout
        {
            Content = content, Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
            FlyoutPresenterStyle = style
        };
        if (autoDismiss) flyout.ShowMode = FlyoutShowMode.Transient;
        _statusFlyout = flyout;
        flyout.ShowAt(target);
        if (!autoDismiss) return;
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(2);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => flyout.Hide();
        flyout.Closed += (_, _) => timer.Stop();
        timer.Start();
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
