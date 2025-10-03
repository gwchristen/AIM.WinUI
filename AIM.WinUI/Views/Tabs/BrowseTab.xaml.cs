using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using AIM.WinUI.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace AIM.WinUI.Views.Tabs;

public sealed partial class BrowseTab : UserControl
{
    private readonly ObservableCollection<string> _items = new();

    public BrowseTab()
    {
        this.InitializeComponent();
        FileList.ItemsSource = _items;
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshList();
    }

    private void RefreshList()
    {
        _items.Clear();
        var root = AppServices.Settings.RootPath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;

        // Show top-level files to keep it snappy; we can deepen later
        foreach (var f in Directory.EnumerateFiles(root))
            _items.Add(f);
    }

    private async void Archive_Click(object sender, RoutedEventArgs e)
    {
        await MoveSelectionAsync(target: AppServices.Settings.ArchivePath, action: "Archived");
    }

    private async void Shipped_Click(object sender, RoutedEventArgs e)
    {
        await MoveSelectionAsync(target: AppServices.Settings.ShippedPath, action: "Shipped");
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshList();

    private async Task MoveSelectionAsync(string? target, string action)
    {
        var root = AppServices.Settings.RootPath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            await ShowInfoAsync("Root not set",
                "Use 'Choose Root' on the toolbar to pick a root folder.");
            return;
        }
        if (string.IsNullOrWhiteSpace(target) || !Directory.Exists(target))
        {
            await ShowInfoAsync($"{action} folder not set",
                $"Open Settings and configure the {action} folder (outside the Root).");
            return;
        }
        if (FileList.SelectedItems.Count == 0)
        {
            await ShowInfoAsync("No selection", "Select one or more files to move.");
            return;
        }

        var sources = FileList.SelectedItems.Cast<string>().ToArray();
        await MoveService.MoveToAsync(sources, root, target, action, AppServices.Log);
        RefreshList();
    }

    private async Task ShowInfoAsync(string title, string content)
    {
        var dlg = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dlg.ShowAsync();
    }

    private void FileList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        // For now, double-click opens in default app
        if (FileList.SelectedItem is string path && File.Exists(path))
            _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
    }
}