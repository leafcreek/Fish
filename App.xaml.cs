using Microsoft.UI.Xaml;
namespace Fish;
public partial class App : Application
{
    private Window? _window;
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var arguments = Environment.GetCommandLineArgs();
        int settingsIndex = Array.IndexOf(arguments, "--settings-directory");
        string? settingsDirectory = settingsIndex >= 0 && settingsIndex + 1 < arguments.Length ? arguments[settingsIndex + 1] : null;
        _window = new MainWindow(settingsDirectory);
        _window.Activate();
    }
}