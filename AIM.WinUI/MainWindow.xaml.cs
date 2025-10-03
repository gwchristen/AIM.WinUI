using AIM.WinUI.Services;
using AIM.WinUI.ViewModels;
using AIM.WinUI.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using Windows.UI.ApplicationSettings;

namespace AIM.WinUI;

public sealed partial class MainWindow : Window
{

    public ViewModels.MainViewModel VM { get; }

    public MainWindow()
    {
        InitializeComponent();
        Title = "AIM";

        // Capture the window's UI dispatcher for global use
        Ui.Dispatcher = this.DispatcherQueue;

        // Create your VM (injection optional)
        VM = new ViewModels.MainViewModel();

        // If you’re binding via a named root element:
        // RootGrid.DataContext = VM;
    }


    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Views.SettingsDialog(AppServices.Settings, AppServices.Log);
        await dlg.ShowAsync();
    }

    private string _lastTag = "browse";

    public MainWindow()
    {
        this.InitializeComponent();
        NavView.SelectedItem = NavView.MenuItems[1]; // Browse by default
        ContentFrame.Navigate(typeof(BrowsePage));
    }

    private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem nvi) return;

        var tag = (string)nvi.Tag;

        // Gate restricted sections
        if (tag is "archive" or "shipped" or "backups")
        {
            var ok = await EnsureAccessAsync(tag);
            if (!ok)
            {
                // revert selection
                ReSelect(_lastTag);
                return;
            }
        }

        Navigate(tag);
        _lastTag = tag;
    }

    private void Navigate(string tag)
    {
        switch (tag)
        {
            case "home": ContentFrame.Navigate(typeof(HomePage)); break;
            case "browse": ContentFrame.Navigate(typeof(BrowsePage)); break;
            case "archive": ContentFrame.Navigate(typeof(ArchivePage)); break;
            case "shipped": ContentFrame.Navigate(typeof(ShippedPage)); break;
            case "backups": ContentFrame.Navigate(typeof(BackupsPage)); break;
            case "settings": ContentFrame.Navigate(typeof(SettingsPage)); break;
        }
    }

    private void ReSelect(string tag)
    {
        foreach (var item in NavView.MenuItems)
        {
            if (item is NavigationViewItem nvi && (string)nvi.Tag == tag)
            {
                NavView.SelectedItem = nvi;
                break;
            }
        }
    }

    // TODO: Replace with your real password service dialog
    private async Task<bool> EnsureAccessAsync(string tag)
    {
        var dialog = new ContentDialog
        {
            Title = "Restricted",
            Content = $"Enter password to open {tag}. (stub)",
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            XamlRoot = this.Content.XamlRoot
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}