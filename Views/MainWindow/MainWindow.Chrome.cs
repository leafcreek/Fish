using System.Reflection;
using Microsoft.UI.Xaml.Automation;

namespace Fish.Views;

public sealed partial class MainWindow
{
    private void InitializeChrome()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        string version = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? typeof(App).Assembly.GetName().Version?.ToString(3) ?? "–";
        VersionText.Text = "v" + version;
        AutomationProperties.SetName(VersionText, "Fish Version " + version);
    }
}
