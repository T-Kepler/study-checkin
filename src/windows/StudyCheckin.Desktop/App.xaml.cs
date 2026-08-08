using System.Windows;

namespace StudyCheckin.Desktop;

public partial class App : Application
{
    protected override void OnExit(ExitEventArgs e)
    {
        MainWindow?.Close();
        base.OnExit(e);
    }
}

