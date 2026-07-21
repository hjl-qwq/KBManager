using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using KBManager.GUI.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// Display model for a file entry in the data grid.
/// </summary>
public partial class FileEntryDisplay : ObservableObject
{
    public string FileName { get; set; } = string.Empty;
    public string TagsDisplay { get; set; } = "—";
    public int TagCount { get; set; }

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Display model for a tag suggestion in the autocomplete dropdown.
/// </summary>
public partial class TagSuggestion : ObservableObject
{
    public string TagName { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public string DisplayText => $"{TagName}  ({FileCount} 个文件)";
}

/// <summary>
/// ViewModel for the Search page.
/// </summary>
public partial class SearchViewModel : ViewModelBase
{
    private readonly IKnowledgeBaseService _kbService;
    private readonly GitHelper _gitHelper;

    [ObservableProperty]
    private string _searchTag = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FileEntryDisplay> _results = new();

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private bool _hasResults;

    // --- Tag suggestion / autocomplete ---

    /// <summary>All tags loaded from the database, cached for filtering.</summary>
    private List<TagWithCountDto> _allTagsCache = new();

    /// <summary>Filtered suggestions shown in the dropdown popup.</summary>
    [ObservableProperty]
    private ObservableCollection<TagSuggestion> _tagSuggestions = new();

    /// <summary>Whether the suggestion dropdown is visible.</summary>
    [ObservableProperty]
    private bool _isSuggestionOpen;

    /// <summary>True when tags have been loaded (even if empty).</summary>
    private bool _tagsLoaded;

    /// <summary>
    /// Set to true while programmatically changing SearchTag (e.g. selecting
    /// a suggestion) to prevent OnSearchTagChanged from clearing the ListBox
    /// ItemsSource mid-selection and crashing Avalonia.
    /// </summary>
    private bool _suppressSuggestionFilter;

    public SearchViewModel(IKnowledgeBaseService kbService, GitHelper gitHelper)
    {
        _kbService = kbService;
        _gitHelper = gitHelper;
    }

    /// <summary>
    /// Called by CommunityToolkit.Mvvm whenever SearchTag changes.
    /// Filters the cached tag list and updates the suggestion dropdown.
    /// </summary>
    partial void OnSearchTagChanged(string value)
    {
        if (!_suppressSuggestionFilter)
            FilterSuggestions(value);
    }

    /// <summary>
    /// Load all tags from the database (with file counts) and cache them.
    /// Called when the user focuses the search TextBox.
    /// </summary>
    [RelayCommand]
    private async Task LoadSuggestionsAsync()
    {
        if (_tagsLoaded) return;

        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory))
            return;

        try
        {
            var result = await _kbService.GetTagsWithFileCountAsync(config.RepositoryDirectory);
            if (result.Success && result.Data != null)
            {
                _allTagsCache = result.Data;
                _tagsLoaded = true;
                FilterSuggestions(SearchTag);
            }
        }
        catch
        {
            // Silently ignore load failures — suggestions are non-critical
        }
    }

    /// <summary>
    /// Filter cached tags by the current search text (fuzzy / contains match).
    /// </summary>
    private void FilterSuggestions(string filter)
    {
        TagSuggestions.Clear();

        if (!_tagsLoaded || string.IsNullOrWhiteSpace(filter))
        {
            // Show all tags sorted by file count when input is empty
            var source = _tagsLoaded
                ? _allTagsCache
                : new List<TagWithCountDto>();

            foreach (var tag in source)
            {
                TagSuggestions.Add(new TagSuggestion
                {
                    TagName = tag.TagName,
                    FileCount = tag.FileCount
                });
            }

            IsSuggestionOpen = TagSuggestions.Count > 0;
            return;
        }

        // Case-insensitive contains match
        var filtered = _allTagsCache
            .Where(t => t.TagName.Contains(filter, System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var tag in filtered)
        {
            TagSuggestions.Add(new TagSuggestion
            {
                TagName = tag.TagName,
                FileCount = tag.FileCount
            });
        }

        IsSuggestionOpen = TagSuggestions.Count > 0;
    }

    /// <summary>
    /// Select a tag suggestion: fill the search box and auto-search.
    /// </summary>
    [RelayCommand]
    private async Task SelectSuggestionAsync(TagSuggestion? suggestion)
    {
        if (suggestion == null) return;

        // Suppress OnSearchTagChanged so we don't mutate TagSuggestions
        // while the ListBox is still processing SelectionChanged.
        _suppressSuggestionFilter = true;
        SearchTag = suggestion.TagName;
        _suppressSuggestionFilter = false;

        IsSuggestionOpen = false;
        await SearchAsync();
    }

    /// <summary>
    /// Close the suggestion dropdown (e.g. when TextBox loses focus).
    /// </summary>
    [RelayCommand]
    private void CloseSuggestions()
    {
        IsSuggestionOpen = false;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchTag))
        {
            StatusMessage = "请输入标签名称";
            return;
        }

        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory))
        {
            StatusMessage = "请先在设置中配置仓库目录";
            return;
        }

        SetBusy("搜索中...");
        try
        {
            var result = await _kbService.SearchFilesByTagAsync(
                config.RepositoryDirectory, SearchTag.Trim());

            Results.Clear();
            if (result.Success && result.Data != null)
            {
                foreach (var file in result.Data)
                {
                    Results.Add(new FileEntryDisplay
                    {
                        FileName = file.FileName,
                        TagsDisplay = file.Tags.Count > 0
                            ? string.Join(", ", file.Tags)
                            : "—",
                        TagCount = file.Tags.Count
                    });
                }
                HasResults = Results.Count > 0;
                ResultSummary = $"找到 {Results.Count} 个结果";
            }
            else
            {
                HasResults = false;
                ResultSummary = result.Message;
            }

            ClearBusy(ResultSummary);
        }
        catch (System.Exception ex)
        {
            ClearBusy();
            StatusMessage = $"搜索失败: {ex.Message}";
        }
    }
}
