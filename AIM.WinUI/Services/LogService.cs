using System.Text.Json;

namespace AIM.WinUI.Services;

public record LogEntry(DateTimeOffset TimestampUtc, string User, string Action, object? Data);

public class LogService
{
    private readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIM", "Logs");

    public void Info(string action, object? data)
    {
        Directory.CreateDirectory(_dir);
        var file = Path.Combine(_dir, $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl");
        var entry = new LogEntry(DateTimeOffset.UtcNow, Environment.UserName, action, data);
        File.AppendAllText(file, JsonSerializer.Serialize(entry) + Environment.NewLine);
    }
}