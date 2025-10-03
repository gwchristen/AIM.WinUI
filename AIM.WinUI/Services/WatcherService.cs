namespace AIM.WinUI.Services;

public sealed class WatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly LogService _log;

    public event FileSystemEventHandler? ChangedOrCreated;
    public event RenamedEventHandler? Renamed;
    public event ErrorEventHandler? Error;

    public WatcherService(LogService log) => _log = log;

    public void Start(string root)
    {
        Stop();
        _watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        // Watch .csv, .log, .txt
        _watcher.Filters.Add("*.csv");
        _watcher.Filters.Add("*.log");
        _watcher.Filters.Add("*.txt");

        _watcher.Created += (s,e) => ChangedOrCreated?.Invoke(s,e);
        _watcher.Changed += (s,e) => ChangedOrCreated?.Invoke(s,e);
        _watcher.Deleted += (s,e) => ChangedOrCreated?.Invoke(s,e);
        _watcher.Renamed += (s,e) => Renamed?.Invoke(s,e);
        _watcher.Error += (s,e) => Error?.Invoke(s,e);
    }

    public void Stop()
    {
        if (_watcher is null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    public void Dispose() => Stop();
}