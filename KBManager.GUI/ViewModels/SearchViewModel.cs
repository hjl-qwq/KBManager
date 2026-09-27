using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using KBManager.GUI.Services;
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
    private readonly IFileContentService _fileContentService;
    private readonly GitHelper _gitHelper;
    private readonly IDialogService _dialogService;

    /// <summary>Set by the shell during composition.</summary>
    public IWorkspaceShell? Shell { get; set; }

    private List<TagWithCountDto> _allTags = new();
    private bool _tagsLoaded;

    /// <summary>
    /// Set while programmatically changing <see cref="SearchTag"/> (accepting a
    /// suggestion) to avoid mutating the suggestion list mid-selection.
    /// </summary>
    private bool _suppressSuggestionFilter;

    public SearchViewModel(
        IKnowledgeBaseService kbService,
        IFileContentService fileContentService,
        GitHelper gitHelper,
        IDialogService dialogService)
    {
        _kbService = kbService;
        _fileContentService = fileContentService;
        _gitHelper = gitHelper;
        _dialogService = dialogService;
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
                {
                    var item = FileSearchResultItem.From(entry);

                    // The index can outlive the file: flag those rows instead of
                    // hiding them, and let the user decide what to do about them.
                    item.ExistsOnDisk = _fileContentService.Exists(repository, item.RelativePath);
                    Results.Add(item);
                }
            }

            int missing = Results.Count(r => r.IsMissing);
            HasSearched = true;
            ResultSummary = Results.Count == 0
                ? $"没有文件使用标签「{tagName}」"
                : missing == 0
                    ? $"标签「{tagName}」匹配 {Results.Count} 个文件"
                    : $"标签「{tagName}」匹配 {Results.Count} 个文件，其中 {missing} 个在磁盘上已不存在";
            ClearBusy(ResultSummary);

            if (missing > 0)
                AppLog.Warn($"检索「{tagName}」：{missing} 条索引记录在磁盘上不存在");
        }
        catch (Exception ex)
        {
            ClearBusy();
            StatusMessage = $"检索失败: {ex.Message}";
            AppLog.Error("检索异常", ex);
        }
    }

    /// <summary>Open a result row in the editor area.</summary>
    [RelayCommand]
    private async Task OpenResultAsync(FileSearchResultItem? item)
    {
        if (item == null || Shell == null) return;
        if (item.IsMissing)
        {
            StatusMessage = $"{item.DisplayName} 在磁盘上已不存在，请先清理失效记录";
            return;
        }

        // A search hit is a deliberate choice, so it opens for real rather than as a
        // provisional preview tab.
        await Shell.OpenFileAsync(item.RelativePath, preview: false);
    }

    /// <summary>
    /// Drop a stale index record (and its tag links) for a file that is no longer
    /// on disk, then refresh the result list.
    /// </summary>
    [RelayCommand]
    private async Task RemoveStaleRecordAsync(FileSearchResultItem? item)
    {
        if (item == null) return;

        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository)) return;

        var confirmed = await _dialogService.ConfirmAsync(
            "清理失效记录",
            $"\"{item.RelativePath}\" 在磁盘上已不存在。\n\n" +
            "将移除它在索引中的记录以及对应标签，磁盘文件不受影响。确定继续吗？");
        if (!confirmed) return;

        var result = await _kbService.DeleteFileAsync(repository, item.RelativePath);
        if (!result.Success)
        {
            AppLog.Warn($"清理失效记录失败：{item.RelativePath} — {result.Message}");
            StatusMessage = result.Message;
            return;
        }

        AppLog.Info($"已清理失效记录：{item.RelativePath}");
        StatusMessage = $"已清理失效记录: {item.RelativePath}";

        // Re-run so the row disappears and the counts stay truthful.
        await SearchAsync();
        if (Shell != null) await Shell.RefreshIndexAsync(rebuildTree: true);
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
