using AIM.WinUI.Models;

namespace AIM.WinUI.Services;

public class FileSystemService
{
    private readonly LogService _log;
    public FileSystemService(LogService log) => _log = log;

    public IEnumerable<TreeNode> BuildTree(string root)
    {
        var rootNode = new TreeNode(root, true);
        Populate(rootNode);
        return new[] { rootNode };
    }

    private void Populate(TreeNode node)
    {
        if (!node.IsFolder) return;
        try
        {
            foreach (var d in Directory.EnumerateDirectories(node.FullPath))
            {
                var c = new TreeNode(d, true);
                node.Children.Add(c);
                Populate(c);
            }
            foreach (var f in Directory.EnumerateFiles(node.FullPath))
            {
                node.Children.Add(new TreeNode(f, false));
            }
        }
        catch { /* ignore access errors */ }
    }

    public static string GetNonConflictingPath(string desiredPath)
    {
        if (!File.Exists(desiredPath) && !Directory.Exists(desiredPath)) return desiredPath;
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