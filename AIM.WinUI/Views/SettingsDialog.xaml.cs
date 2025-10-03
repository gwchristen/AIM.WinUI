using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using AIM.WinUI.Services;

namespace AIM.WinUI.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    private readonly SettingsService _settings;
    private readonly LogService _log;

    public SettingsDialog(SettingsService settings, LogService log)
    {
        this.InitializeComponent();
        _settings = settings; _log = log;
        ArchiveBox.Text = _settings.ArchivePath ?? "(locked)";
        ShippedBox.Text = _settings.ShippedPath ?? "(locked)";
        BackupBox.Text  = _settings.BackupPath  ?? "(locked)";
    }

    private async void Unlock_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.Unlock(PwdBox.Password))
        {
            // If your _settings exposes decrypted properties:
            ArchiveBox.Text = _settings.ArchivePath ?? string.Empty;
            ShippedBox.Text = _settings.ShippedPath ?? string.Empty;
            BackupBox.Text = _settings.BackupPath ?? string.Empty;

            // Optional: enable the fields now that they’re unlocked
            ArchiveBox.IsEnabled = ShippedBox.IsEnabled = BackupBox.IsEnabled = true;

            // Optional: clear the password box
            PwdBox.Password = string.Empty;
        }
        else
        {
            var dlg = new ContentDialog
            {
                Title = "Unlock failed",
                Content = "Wrong password.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot // Valid because we're inside a ContentDialog
            };
            await dlg.ShowAsync();
        }
    }

    private void SetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PwdBox.Password))
        {
            _ = new ContentDialog { Title = "Password required", Content = "Enter a password first.",
                CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();
            return;
        }
        _settings.SetPassword(PwdBox.Password);
        _ = new ContentDialog { Title = "Password set", Content = "Protected directories now require unlock.",
            CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private async void BrowseArchive_Click(object sender, RoutedEventArgs e)
    {
        var p = await PickFolderAsync();
        if (p is null) return;
        _settings.ArchivePath = p; ArchiveBox.Text = p; _log.Info("ArchivePathChanged", new { p });
    }
    private async void BrowseShipped_Click(object sender, RoutedEventArgs e)
    {
        var p = await PickFolderAsync();
        if (p is null) return;
        _settings.ShippedPath = p; ShippedBox.Text = p; _log.Info("ShippedPathChanged", new { p });
    }
    private async void BrowseBackup_Click(object sender, RoutedEventArgs e)
    {
        var p = await PickFolderAsync();
        if (p is null) return;
        _settings.BackupPath = p; BackupBox.Text = p; _log.Info("BackupPathChanged", new { p });
    }
}