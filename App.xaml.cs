using Microsoft.UI.Xaml;

namespace Fish;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) => (_window as MainWindow)?.RecordUnhandledException(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception error) (_window as MainWindow)?.RecordUnhandledException(error);
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var arguments = Environment.GetCommandLineArgs();
        int settingsIndex = Array.IndexOf(arguments, "--settings-directory");
        string? settingsDirectory = settingsIndex >= 0 && settingsIndex + 1 < arguments.Length
            ? arguments[settingsIndex + 1] : null;
        try
        {
            _window = new MainWindow(settingsDirectory);
            _window.Activate();
        }
        catch (Exception error)
        {
            try { Services.DiagnosticsService.Write(System.IO.Path.Combine(settingsDirectory ?? Configuration.SettingsStore.DefaultFolder, "diagnostics"), "Fish-Start fehlgeschlagen\n" + error); } catch { }
            throw;
        }
    }
}
