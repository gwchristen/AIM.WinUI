using System;

namespace AIM.WinUI.Services;

public static class AppServices
{
    public static LogService Log { get; private set; } = null!;
    public static SettingsService Settings { get; private set; } = null!;

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        Log = new LogService();
        Settings = new SettingsService(Log);
        _initialized = true;
    }
}