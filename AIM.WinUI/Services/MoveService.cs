using System.IO;

namespace AIM.WinUI.Services;

public static class MoveService
{
    public static async Task MoveToAsync(IEnumerable<string> sources, string root, string specialDir, string action, LogService log)
    {
        foreach (var src in sources)
        {
            if (string.IsNullOrWhiteSpace(src)) continue;
            var rel = Path.GetRelativePath(root, src);
            // If not really under root, still preserve name (fallback)
            if (rel.StartsWith("..")) rel = Path.GetFileName(src);

            var dst = Path.Combine(specialDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

            var final = GetNonConflictingPath(dst);
            if (File.Exists(src))
                File.Move(src, final);
            else if (Directory.Exists(src))
                Directory.Move(src, final);

            log.Info(action, new { from = src, to = final });
        }
        await Task.CompletedTask;
    }

    public static string GetNonConflictingPath(string desiredPath)
    {
        if (!File.Exists(desiredPath) && !Directory.Exists(desiredPath))
            return desiredPath;

        var dir = Path.GetDirectoryName(desiredPath)!;
        var name = Path.GetFileNameWithoutExtension(desiredPath);
        var ext = Path.GetExtension(desiredPath);
        int i = 2;
        string candidate;
        do { candidate = Path.Combine(dir, $"{name} ({i++}){ext}"); }
        while (File.Exists(candidate) || Directory.Exists(candidate));
        return candidate;
    }
}