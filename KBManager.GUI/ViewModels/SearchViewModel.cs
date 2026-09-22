using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// The search sidebar: find indexed files by tag, then open one in the editor.
///
/// Focusing the box with an empty query lists every tag ordered by usage, so the
/// panel doubles as a tag browser.
/// </summary>
public partial class SearchViewModel : ViewModelBase
{
    private readonly IKnowledgeBaseService _kbService;
    private readonly GitHelper _gitHelper;

    /// <summary>Set by the shell during composition.</summary>
    public IWorkspaceShell? Shell { get; set; }

    private List<TagWithCountDto> _allTags = new();
    private bool _tagsLoaded;

    /// <summary>
    /// Set while programmatically changing <see cref="SearchTag"/> (accepting a
    /// suggestion) to avoid mutating the suggestion list mid-selection.
    /// </summary>
    private bool _suppressSuggestionFilter;

    public SearchViewModel(IKnowledgeBaseService kbService, GitHelper gitHelper)
    {
        _kbService = kbService;
        _gitHelper = gitHelper;
    }

    // ── Query ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _searchTag = string.Empty;

    [ObservableProperty]
    private ObservableCollection<TagSuggestionItem> _tagSuggestions = new();

    [ObservableProperty]
    private bool _isSuggestionOpen;

    [ObservableProperty]
    private string _suggestionHeader = "全部标签";

    // ── Results ────────────────────────────────────────────────────────────

    public ObservableCollection<FileSearchResultItem> Results { get; } = new();

    [ObservableProperty]
    private string _resultSummary = "输入标签名称以检索文件";

    [ObservableProperty]
    private bool _hasSearched;

    // ── Suggestions ────────────────────────────────────────────────────────

    partial void OnSearchTagChanged(string value)
    {
        if (!_suppressSuggestionFilter) FilterSuggestions(value);
    }

    /// <summary>Drop cached tag data so the next focus re-reads the index.</summary>
    public void Invalidate()
    {
        _tagsLoaded = false;
        _allTags = new List<TagWithCountDto>();
        FilterSuggestions(SearchTag);
    }

    [RelayCommand]
    private async Task LoadSuggestionsAsync()
    {
        if (_tagsLoaded) return;

        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository)) return;

        try
        {
            var result = await _kbService.GetTagsWithFileCountAsync(repository);
            if (result.Success && result.Data != null)
            {
                _allTags = result.Data;
                _tagsLoaded = true;
                FilterSuggestions(SearchTag);
            }
        }
        catch
        {
            // Suggestions are a convenience; a failure here must not break search.
        }
    }

    private void FilterSuggestions(string filter)
    {
        TagSuggestions.Clear();

        var query = filter?.Trim() ?? string.Empty;

        var matches = query.Length == 0
            ? _allTags
            : _allTags
                .Where(t => t.TagName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        foreach (var tag in matches)
        {
            TagSuggestions.Add(new TagSuggestionItem
            {
                TagName = tag.TagName,
                FileCount = tag.FileCount
            });
        }

        SuggestionHeader = query.Length == 0 ? "全部标签" : "匹配的标签";
        IsSuggestionOpen = TagSuggestions.Count > 0;
    }

    [RelayCommand]
    private async Task SelectSuggestionAsync(TagSuggestionItem? suggestion)
    {
        if (suggestion == null) return;

        _suppressSuggestionFilter = true;
        SearchTag = suggestion.TagName;
        _suppressSuggestionFilter = false;

        IsSuggestionOpen = false;
        await SearchAsync();
    }

    [RelayCommand]
    private void CloseSuggestions() => IsSuggestionOpen = false;

    // ── Search ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SearchAsync()
    {
        var tagName = SearchTag.Trim();
        if (tagName.Length == 0)
        {
            StatusMessage = "请输入标签名称";
            return;
        }

        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository))
        {
            StatusMessage = "请先在设置中配置仓库目录";
            return;
        }

        SetBusy("检索中…");
        try
        {
            var result = await _kbService.SearchFilesByTagAsync(repository, tagName);

            Results.Clear();
            if (result.Success && result.Data != null)
            {
                foreach (var entry in result.Data)
                    Results.Add(FileSearchResultItem.From(entry));
            }

            HasSearched = true;
            ResultSummary = Results.Count > 0
                ? $"标签「{tagName}」匹配 {Results.Count} 个文件"
                : $"没有文件使用标签「{tagName}」";
            ClearBusy(ResultSummary);
        }
        catch (Exception ex)
        {
            ClearBusy();
            StatusMessage = $"检索失败: {ex.Message}";
        }
    }

    /// <summary>Open a result row in the editor area.</summary>
    [RelayCommand]
    private async Task OpenResultAsync(FileSearchResultItem? item)
    {
        if (item == null || Shell == null) return;
        await Shell.OpenFileAsync(item.RelativePath);
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchTag = string.Empty;
        Results.Clear();
        HasSearched = false;
        ResultSummary = "输入标签名称以检索文件";
        FilterSuggestions(string.Empty);
    }
}
