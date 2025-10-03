// File: ViewModels/FolderNode.cs
using System.Collections.ObjectModel;

namespace AIM.WinUI.ViewModels
{
    public sealed class FolderNode
    {
        public string Name { get; }
        public string FullPath { get; }
        public ObservableCollection<FolderNode> Children { get; } = new();

        public FolderNode(string name, string fullPath)
        {
            Name = name;
            FullPath = fullPath;
        }
    }
}