using CommunityToolkit.Mvvm.ComponentModel;
using KBManager.core;
using System.Collections.Generic;
using System.Linq;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// A tag attached to a file, rendered as a removable chip.
/// </summary>
public partial class FileTagItem : ObservableObject
{
    public string TagName { get; set; } = string.Empty;
}

/// <summary>
/// A tag suggestion shown in an autocomplete dropdown, annotated with how many
/// files already use it (most-used first).
/// </summary>
public partial class TagSuggestionItem : ObservableObject
{
    public string TagName { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public string DisplayText => $"{TagName}  ({FileCount} 个文件)";
}

/// <summary>
/// One row in the tag search results list. Clicking it opens the file in the editor.
/// </summary>
public partial class FileSearchResultItem : ObservableObject
{
    /// <summary>Repository-relative path — the identity used to open the file.</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>File name shown prominently.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Parent folder, shown dimmed under the file name.</summary>
    public string DirectoryLabel { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();

    public string TagsDisplay => Tags.Count > 0 ? string.Join(" · ", Tags) : "—";

    public bool HasDirectoryLabel => !string.IsNullOrEmpty(DirectoryLabel);

    /// <summary>Build a result row from an index entry.</summary>
    public static FileSearchResultItem From(FileEntryDto entry)
    {
        var path = entry.FileName.Replace('\\', '/');
        var slash = path.LastIndexOf('/');
        return new FileSearchResultItem
        {
            RelativePath = path,
            DisplayName = slash >= 0 ? path[(slash + 1)..] : path,
            DirectoryLabel = slash >= 0 ? path[..slash] : string.Empty,
            Tags = entry.Tags.OrderBy(t => t, System.StringComparer.OrdinalIgnoreCase).ToList()
        };
    }
}
