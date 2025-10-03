using System.IO.Compression;

namespace AIM.WinUI.Services;

public class BackupService
{
    private readonly LogService _log;
    public BackupService(LogService log) => _log = log;

    public string CreateBackupOfRootOnly(string root, string backupDir)
    {
        Directory.CreateDirectory(backupDir);
        var zip = Path.Combine(backupDir, $"AIM_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        ZipFile.CreateFromDirectory(root, zip, CompressionLevel.Optimal, includeBaseDirectory: true);
        _log.Info("BackupCreated", new { root, zip });
        return zip;
    }
}