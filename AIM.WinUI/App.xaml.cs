using Microsoft.UI.Xaml;
using AIM.WinUI.Services; // if you're using the Ui dispatcher helper

namespace AIM.WinUI;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();

        // If you're using the Ui dispatcher helper:
        Ui.Dispatcher = MainWindow.DispatcherQueue;

        MainWindow.Activate();
    }
}