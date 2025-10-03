using AIM.WinUI.Models;
using AIM.WinUI.Services;
using AIM.WinUI.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AIM.WinUI.Views
{
    public sealed partial class BrowsePage : Page
    {
        private readonly DispatcherQueue _dispatcher;

        public BrowsePage()
        {
            this.InitializeComponent();
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            LoadRootNodes();
        }

        private void LoadRootNodes()
        {
            FolderTree.RootNodes.Clear();

            // Add top-level special groups:
            AddSpecialNode("Root", AppPaths.Root);
            AddSpecialNode("Archive", AppPaths.Archive);
            AddSpecialNode("Shipped", AppPaths.Shipped);
            AddSpecialNode("Backups", AppPaths.Backups);
        }

        private void AddSpecialNode(string display, string path)
        {
            var item = new FsItem { Name = display, FullPath = path, Type = FsItemType.Folder };
            var node = new TreeViewNode { Content = item, IsExpanded = false };
            node.HasChildren = true;            // assume children; we’ll lazy-load
            node.Children.Add(CreatePlaceholder()); // placeholder for lazy load
            FolderTree.RootNodes.Add(node);
        }

        private static TreeViewNode CreatePlaceholder()
            => new TreeViewNode { Content = new FsItem { Name = "(loading...)", Type = FsItemType.File } };

        private async void FolderTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
        {
            // Lazy load only once
            var node = args.Node;
            if (node == null || (node.Children.Count == 1 && ((FsItem)node.Children[0].Content).Name == "(loading...)"))
            {
                await PopulateChildrenAsync(node);
            }
        }

        private async Task PopulateChildrenAsync(TreeViewNode node)
        {
            if (node?.Content is not FsItem folder || folder.Type != FsItemType.Folder)
                return;

            // Clear placeholder
            node.Children.Clear();

            var path = folder.FullPath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            // Load on a background thread
            var dirsTask = Task.Run(() =>
            {
                try
                {
                    return Directory.EnumerateDirectories(path)
                                    .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                                    .ToArray();
                }
                catch { return Array.Empty<string>(); }
            });

            var filesTask = Task.Run(() =>
            {
                try
                {
                    return Directory.EnumerateFiles(path)
                                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                    .ToArray();
                }
                catch { return Array.Empty<string>(); }
            });

            var dirs = await dirsTask;
            var files = await filesTask;

            // Back to UI to add nodes
            _dispatcher.TryEnqueue(() =>
            {
                foreach (var d in dirs)
                {
                    var childItem = new FsItem
                    {
                        Name = System.IO.Path.GetFileName(d),
                        FullPath = d,
                        Type = FsItemType.Folder
                    };
                    var childNode = new TreeViewNode { Content = childItem };
                    childNode.HasChildren = true;
                    childNode.Children.Add(CreatePlaceholder());
                    node.Children.Add(childNode);
                }

                foreach (var f in files)
                {
                    var childItem = new FsItem
                    {
                        Name = System.IO.Path.GetFileName(f),
                        FullPath = f,
                        Type = FsItemType.File
                    };
                    var childNode = new TreeViewNode { Content = childItem, HasChildren = false };
                    node.Children.Add(childNode);
                }
            });
        }

        private void FolderTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            // Folder double-click (or Enter) => open (expand & set current folder)
            if (args.InvokedItem is TreeViewNode node && node.Content is FsItem item && item.Type == FsItemType.Folder)
            {
                node.IsExpanded = true;
                // TODO: notify your file list ViewModel to show contents of item.FullPath
                // e.g., Messenger.Default.Send(new CurrentFolderChanged(item.FullPath));
            }
        }

        private void FolderTree_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var container = (e.OriginalSource as FrameworkElement)?.DataContext;
            // Ensure right-click selects the node under cursor
            var treeViewItem = (e.OriginalSource as FrameworkElement)?.FindAscendant<TreeViewItem>();
            if (treeViewItem is not null)
            {
                treeViewItem.IsSelected = true;
                TreeContextMenu.ShowAt((FrameworkElement)sender, e.GetPosition((FrameworkElement)sender));
            }
        }

        private FsItem? GetSelectedFsItem()
            => (FolderTree.SelectedNode?.Content as FsItem);

        // --- Context menu handlers ---
        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var item = GetSelectedFsItem();
            if (item?.Type == FsItemType.Folder)
            {
                // Navigate right pane / file list to this folder
                // Messenger.Default.Send(new CurrentFolderChanged(item.FullPath));
            }
        }

        private async void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            var parent = GetSelectedFsItem();
            if (parent?.Type != FsItemType.Folder) return;

            var name = "New Folder";
            var target = System.IO.Path.Combine(parent.FullPath, name);
            int suffix = 1;
            while (Directory.Exists(target)) target = System.IO.Path.Combine(parent.FullPath, $"{name} ({suffix++})");

            Directory.CreateDirectory(target);

            // Refresh the selected node
            var node = FolderTree.SelectedNode;
            node?.Children.Add(new TreeViewNode
            {
                Content = new FsItem { Name = System.IO.Path.GetFileName(target), FullPath = target, Type = FsItemType.Folder },
                HasChildren = true
            });
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            var item = GetSelectedFsItem();
            if (item is null) return;

            var dialog = new ContentDialog
            {
                Title = "Rename",
                Content = new TextBox { Text = item.Name },
                PrimaryButtonText = "OK",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var input = (dialog.Content as TextBox)?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(input)) return;

            var newPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(item.FullPath)!, input);

            try
            {
                if (item.Type == FsItemType.Folder)
                    Directory.Move(item.FullPath, newPath);
                else
                    File.Move(item.FullPath, newPath, overwrite: false);

                // Update node
                item.Name = input; // if you make FsItem immutable, replace the node instead
                // Easiest: rebuild the parent node to reflect changes
                var parentNode = FolderTree.SelectedNode?.Parent;
                if (parentNode != null)
                {
                    // Force refresh: clear and re-populate
                    parentNode.Children.Clear();
                    parentNode.Children.Add(CreatePlaceholder());
                    _ = PopulateChildrenAsync(parentNode);
                }
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title = "Rename failed",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                }.ShowAsync();
            }
        }

        private async void MoveToArchive_Click(object sender, RoutedEventArgs e)
        {
            var item = GetSelectedFsItem();
            if (item is null) return;

            try
            {
                var dest = System.IO.Path.Combine(AppPaths.Archive, item.Name);
                dest = GetNonCollidingPath(dest, item.Type == FsItemType.File);

                if (item.Type == FsItemType.Folder)
                    Directory.Move(item.FullPath, dest);
                else
                    File.Move(item.FullPath, dest);

                // Remove node from UI
                FolderTree.SelectedNode?.Parent?.Children?.Remove(FolderTree.SelectedNode);
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title = "Move to Archive failed",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                }.ShowAsync();
            }
        }

        private static string GetNonCollidingPath(string basePath, bool isFile)
        {
            if (isFile)
            {
                var dir = System.IO.Path.GetDirectoryName(basePath)!;
                var name = System.IO.Path.GetFileNameWithoutExtension(basePath);
                var ext = System.IO.Path.GetExtension(basePath);
                var path = basePath; int i = 1;
                while (File.Exists(path)) path = System.IO.Path.Combine(dir, $"{name} ({i++}){ext}");
                return path;
            }
            else
            {
                var dir = System.IO.Path.GetDirectoryName(basePath)!;
                var name = System.IO.Path.GetFileName(basePath);
                var path = basePath; int i = 1;
                while (Directory.Exists(path)) path = System.IO.Path.Combine(dir, $"{name} ({i++})");
                return path;
            }
        }

        private async void Properties_Click(object sender, RoutedEventArgs e)
        {
            var item = GetSelectedFsItem();
            if (item is null) return;

            var info = item.Type == FsItemType.File
                ? new FileInfo(item.FullPath).Length + " bytes"
                : $"{Directory.EnumerateFileSystemEntries(item.FullPath).Count()} items";

            await new ContentDialog
            {
                Title = "Properties",
                Content = $"{item.Name}\n{item.FullPath}\n{info}",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            }.ShowAsync();
        }
    }

    // Helper to find containing TreeViewItem from visual tree
    internal static class VisualTreeHelpers
    {
        public static T? FindAscendant<T>(this FrameworkElement fe) where T : FrameworkElement
        {
            var parent = fe.Parent as FrameworkElement;
            while (parent != null && parent is not T)
                parent = parent.Parent as FrameworkElement;
            return parent as T;
        }
    }
}