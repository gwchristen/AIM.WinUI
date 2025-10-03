using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIM.WinUI.Services;

namespace AIM.WinUI.ViewModels;

public partial class BrowseViewModel : ObservableObject
{
    [ObservableProperty] private string? previewText;
    [ObservableProperty] private bool isEditing;

    private readonly SettingsService _settings;
    private readonly LogService _log;

    public BrowseViewModel(SettingsService settings, LogService log)
    { _settings = settings; _log = log; }

    public static readonly HashSet<string> AllowedPreviewExtensions = new(StringComparer.OrdinalIgnoreCase) { ".txt", ".csv", ".log" };

    public async Task LoadPreviewAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var ext = Path.GetExtension(path);
        if (!AllowedPreviewExtensions.Contains(ext)) { PreviewText = "Preview not available for this file type."; return; }
        var fi = new FileInfo(path);
        if (fi.Length > _settings.PreviewMaxBytes) { PreviewText = $"File is larger than {_settings.PreviewMaxBytes / (1024*1024)} MB."; return; }
        using var sr = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
        PreviewText = await sr.ReadToEndAsync();
    }

    [RelayCommand] private void StartEdit() => IsEditing = true;
}