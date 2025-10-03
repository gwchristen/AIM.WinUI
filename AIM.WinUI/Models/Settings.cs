namespace AIM.WinUI.Models;

public class Settings
{
    public int SettingsVersion { get; set; } = 1;
    public string? RootPath { get; set; }
    public string? ArchivePathEnc { get; set; }
    public string? ShippedPathEnc { get; set; }
    public string? BackupPathEnc { get; set; }
    public CryptoInfo Crypto { get; set; } = new();
    public string Theme { get; set; } = "Dark";
    public int PreviewMaxBytes { get; set; } = 5 * 1024 * 1024;
}
public class CryptoInfo
{
    public string Kdf { get; set; } = "PBKDF2-SHA256";
    public int Iterations { get; set; } = 150000;
    public string? Salt { get; set; }
    public string? PasswordHash { get; set; }
}