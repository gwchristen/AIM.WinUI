using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AIM.WinUI.Models;
using AIM.WinUI.Services;
using System.IO;
using Microsoft.UI.Dispatching;

namespace AIM.WinUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<TreeNode> RootNodes { get; } = new();
    public SettingsService Settings { get; }
    public LogService Log { get; }
    public FileSystemService Fs { get; }
    public IndexerService Indexer { get; }
    public WatcherService Watcher { get; }

    [ObservableProperty] private bool isWatching;

    public MainViewModel()
    {
        Log = new LogService();
        Settings = new SettingsService(Log);
        Fs = new FileSystemService(Log);
        Indexer = new IndexerService(Log);
        Watcher = new WatcherService(Log);
        Watcher.ChangedOrCreated += Watcher_ChangedOrCreated;
        Watcher.Renamed += Watcher_Renamed;
        Watcher.Error += Watcher_Error;
    }

    public async Task SetRootAsync(string root)
    {
        Settings.RootPath = root;
        RootNodes.Clear();
        foreach (var n in Fs.BuildTree(root)) RootNodes.Add(n);
        await Indexer.BuildAsync(root);
        Watcher.Start(root); IsWatching = true;
        Log.Info("RootSelected", new { root });
    }


    private void Watcher_ChangedOrCreated(object? sender, FileSystemEventArgs e)
    {
        if (Settings.RootPath is null) return;

        try
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted)
                Indexer.Remove(e.FullPath);
            else
                Indexer.IndexFile(e.FullPath);

            var parentDir = Path.GetDirectoryName(e.FullPath) ?? Settings.RootPath;

            // Enqueue UI updates via our dispatcher helper
            Ui.Enqueue(() =>
            {
                RootNodes.Clear();
                foreach (var n in Fs.BuildTree(Settings.RootPath!))
                    RootNodes.Add(n);
            });
        }
        catch
        {
            // swallow in starter
        }
    }

    private void Watcher_Renamed(object? sender, RenamedEventArgs e)
    {
        Indexer.Remove(e.OldFullPath);
        Indexer.IndexFile(e.FullPath);
        Watcher_ChangedOrCreated(sender, new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(e.FullPath)!, Path.GetFileName(e.FullPath)));
    }

    private void Watcher_Error(object? sender, ErrorEventArgs e)
        => Log.Info("WatcherError", new { e.GetException().Message });
}