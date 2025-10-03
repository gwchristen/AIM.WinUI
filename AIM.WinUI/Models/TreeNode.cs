namespace AIM.WinUI.Models;

public class TreeNode
{
    public string FullPath { get; }
    public string Name { get; }
    public bool IsFolder { get; }
    public System.Collections.ObjectModel.ObservableCollection<TreeNode> Children { get; } = new();

    public TreeNode(string fullPath, bool isFolder)
    {
        FullPath = fullPath;
        Name = System.IO.Path.GetFileName(fullPath.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(Name)) Name = fullPath; // root edge case
        IsFolder = isFolder;
    }
}