using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// Tree node for hierarchical file display.
/// </summary>
public partial class FileTreeNode : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string? FullPath { get; set; }       // null for directory nodes
    public bool IsDirectory { get; set; }
    public bool IsExpanded { get; set; }
    public int TagCount { get; set; }

    [ObservableProperty]
    private bool _isSelected;

    public ObservableCollection<FileTreeNode> Children { get; set; } = new();
}
