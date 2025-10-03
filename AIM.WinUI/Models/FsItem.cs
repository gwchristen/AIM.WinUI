// Models/FsItem.cs
namespace AIM.WinUI.Models
{
    public enum FsItemType { Folder, File }

    public sealed class FsItem
    {
        public string Name { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
        public FsItemType Type { get; init; }
        public string Glyph => Type == FsItemType.Folder ? "\uE8B7" : "\uE8A5"; // Folder/File glyphs (Segoe Fluent Icons)
        public override string ToString() => Name;
    }
}
