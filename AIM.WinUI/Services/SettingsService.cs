using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIM.WinUI.Models;

namespace AIM.WinUI.Services;

public class SettingsService
{
    private readonly LogService _log;
    private Settings _settings = new();
    public Settings Current { get; private set; } = new();
    public SettingsService(LogService log) { _log = log; Load(); }

    public string? RootPath { get => _settings.RootPath; set { _settings.RootPath = value; Save(); } }

    public string? ArchivePath { get => TryDecrypt(_settings.ArchivePathEnc); set { _settings.ArchivePathEnc = EncryptField(value); Save(); } }
    public string? ShippedPath { get => TryDecrypt(_settings.ShippedPathEnc); set { _settings.ShippedPathEnc = EncryptField(value); Save(); } }
    public string? BackupPath  { get => TryDecrypt(_settings.BackupPathEnc);  set { _settings.BackupPathEnc  = EncryptField(value); Save(); } }

    public bool HasPassword => !string.IsNullOrWhiteSpace(_settings.Crypto.PasswordHash);
    private string SettingsFilePath => AppContext.BaseDirectory + Path.DirectorySeparatorChar + "settings.json";

    public int PreviewMaxBytes
    {
        get => Current.PreviewMaxBytes;
        set => Current.PreviewMaxBytes = value;
    }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
                _settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsFilePath)) ?? new Settings();
            else Save();
        }
        catch (Exception ex) { _log.Info("SettingsLoadError", new { ex.Message }); }
    }

    public void Save() => File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));

    // Password management
    public bool SetPassword(string newPassword)
    {
        var (salt, hash) = HashPassword(newPassword, _settings.Crypto.Iterations);
        _settings.Crypto.Salt = Convert.ToBase64String(salt);
        _settings.Crypto.PasswordHash = Convert.ToBase64String(hash);
        Save();
        return true;
    }
    public bool VerifyPassword(string password)
    {
        if (_settings.Crypto.Salt is null || _settings.Crypto.PasswordHash is null) return false;
        var salt = Convert.FromBase64String(_settings.Crypto.Salt);
        var expected = Convert.FromBase64String(_settings.Crypto.PasswordHash);
        return Verify(password, salt, expected, _settings.Crypto.Iterations);
    }

    private string? _unlockedPassword; // session cache
    public bool Unlock(string password)
    {
        if (!HasPassword) return true;
        if (VerifyPassword(password)) { _unlockedPassword = password; return true; }
        return false;
    }

    private string? TryDecrypt(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        if (!HasPassword || string.IsNullOrEmpty(_unlockedPassword)) return null; // locked
        try { var key = DeriveKey(_unlockedPassword!, GetSalt()); return DecryptField(payload, key); }
        catch { return null; }
    }
    private string? EncryptField(string? value)
    {
        if (value is null) return null;
        if (!HasPassword || string.IsNullOrEmpty(_unlockedPassword)) return value; // plaintext until password set
        var key = DeriveKey(_unlockedPassword!, GetSalt());
        return EncryptField(value, key);
    }

    private byte[] GetSalt() => _settings.Crypto.Salt is null ? RandomNumberGenerator.GetBytes(16) : Convert.FromBase64String(_settings.Crypto.Salt);

    // Crypto helpers
    public static (byte[] Salt, byte[] Hash) HashPassword(string password, int iterations)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        using var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return (salt, kdf.GetBytes(32));
    }
    public static bool Verify(string password, byte[] salt, byte[] expected, int iterations)
    {
        using var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return CryptographicOperations.FixedTimeEquals(expected, kdf.GetBytes(32));
    }
    public static byte[] DeriveKey(string password, byte[] salt, int iterations = 150000)
    {
        using var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return kdf.GetBytes(32);
    }
    public static string EncryptField(string plaintext, byte[] key)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        using var aes = new AesGcm(key);
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var ct = new byte[pt.Length];
        var tag = new byte[16];
        aes.Encrypt(nonce, pt, ct, tag);
        var blob = new byte[nonce.Length + ct.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, nonce.Length);
        Buffer.BlockCopy(ct, 0, blob, nonce.Length, ct.Length);
        Buffer.BlockCopy(tag, 0, blob, nonce.Length + ct.Length, tag.Length);
        return Convert.ToBase64String(blob);
    }
    public static string DecryptField(string base64, byte[] key)
    {
        var blob = Convert.FromBase64String(base64);
        var nonce = blob.AsSpan(0,12).ToArray();
        var tag   = blob.AsSpan(blob.Length-16,16).ToArray();
        var ct    = blob.AsSpan(12, blob.Length-28).ToArray();
        using var aes = new AesGcm(key);
        var pt = new byte[ct.Length];
        aes.Decrypt(nonce, ct, tag, pt);
        return Encoding.UTF8.GetString(pt);
    }
}