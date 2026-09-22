using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// Tree node for the explorer sidebar. Directory nodes own children; file nodes
/// carry the tags that drive the hover popup and the tag badge.
///
/// Row actions live on the node as commands because a context menu is hosted in a
/// popup: it cannot resolve ancestor bindings back to the explorer ViewModel, but
/// it always inherits the row's DataContext (this node).
/// </summary>
public partial class FileTreeNode : ObservableObject
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Repository-relative path; null for directory nodes.</summary>
    public string? FullPath { get; set; }

    public bool IsDirectory { get; set; }

    /// <summary>True for leaf file nodes — the ones that can be opened or tagged.</summary>
    public bool IsFile => !IsDirectory;

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Number of tags on this file (shown as the tag badge).</summary>
    [ObservableProperty]
    private int _tagCount;

    /// <summary>Directory nodes only: how many indexed files live underneath.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFileCountBadge))]
    private int _fileCount;

    /// <summary>Whether <see cref="Tags"/> has any entries (drives the hover popup).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyTagHint))]
    private bool _hasTags;

    /// <summary>File with no tags yet — the hover popup says so explicitly.</summary>
    public bool ShowEmptyTagHint => IsFile && !HasTags;

    /// <summary>Directory with indexed files — the hover popup shows the count.</summary>
    public bool ShowFileCountBadge => IsDirectory && FileCount > 0;

    /// <summary>Tags of this file, rendered as chips in the hover popup.</summary>
    public ObservableCollection<string> Tags { get; } = new();

    public ObservableCollection<FileTreeNode> Children { get; } = new();

    /// <summary>Replace the tag set and keep the derived flags in sync.</summary>
    public void SetTags(IEnumerable<string> tags)
    {
        Tags.Clear();
        foreach (var tag in tags) Tags.Add(tag);
        HasTags = Tags.Count > 0;
        TagCount = Tags.Count;
    }

    // ── Row actions, wired by the explorer when the tree is built ───────────

    /// <summary>Open a file, or toggle a directory.</summary>
    public Action<FileTreeNode>? ActivateRequested { get; set; }

    public Action<FileTreeNode>? OpenExternallyRequested { get; set; }

    public Action<FileTreeNode>? RenameRequested { get; set; }

    public Action<FileTreeNode>? DeleteRequested { get; set; }

    [RelayCommand]
    private void Activate() => ActivateRequested?.Invoke(this);

    [RelayCommand]
    private void OpenExternally() => OpenExternallyRequested?.Invoke(this);

    [RelayCommand]
    private void Rename() => RenameRequested?.Invoke(this);

    [RelayCommand]
    private void Delete() => DeleteRequested?.Invoke(this);
}
