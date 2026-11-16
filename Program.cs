using Fish.Services.Updates;

namespace Fish;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // The copied, self-contained EXE also serves as the updater, without opening WinUI.
        if (args is ["--apply-update", var session])
        {
            Environment.ExitCode = UpdateInstaller.RunHelper(session);
            return;
        }
        XamlGeneratedProgram.XamlGeneratedMain();
    }
}
