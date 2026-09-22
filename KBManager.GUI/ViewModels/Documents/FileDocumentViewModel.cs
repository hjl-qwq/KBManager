using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// One Markdown file open in the editor area.
///
/// Owns the text buffer, dirty tracking, the line-number gutter, tag editing for
/// the file, and saving. The buffer is written back using the file's original
/// encoding and line endings so opening a note in KBManager never silently
/// rewrites it in Git.
/// </summary>
public partial class FileDocumentViewModel : DocumentViewModel
{
    private readonly IFileContentService _fileContent;
    private readonly IKnowledgeBaseService _kbService;
    private readonly GitHelper _gitHelper;
    private readonly Services.IDialogService _dialogService;

    /// <summary>Content as last read from / written to disk (dirty comparison).</summary>
    private string _savedContent = string.Empty;

    private string _lineEnding = "\n";
    private int _gutterLineCount = -1;
    private List<TagWithCountDto> _allTags = new();

    /// <summary>Whether the file on disk starts with a UTF-8 BOM (preserved on save).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EncodingLabel))]
    private bool _hasUtf8Bom;

    /// <summary>Raised after tags change so the explorer can refresh its badges.</summary>
    public event Func<Task>? TagsChanged;

    public FileDocumentViewModel(
        IFileContentService fileContent,
        IKnowledgeBaseService kbService,
        GitHelper gitHelper,
        Services.IDialogService dialogService)
    {
        _fileContent = fileContent;
        _kbService = kbService;
        _gitHelper = gitHelper;
        _dialogService = dialogService;
    }

    /// <summary>Point this document at a repository-relative path.</summary>
    public void Initialize(string relativePath) => RelativePath = relativePath.Replace('\\', '/');

    /// <summary>
    /// Follow a rename/move on disk. The buffer is untouched: the same content
    /// now lives at a new path, and subsequent saves target the new path.
    /// </summary>
    public void MoveTo(string newRelativePath)
    {
        RelativePath = newRelativePath.Replace('\\', '/');
        InfoMessage = $"已移动到 {RelativePath}";
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(LocationLabel))]
    private string _relativePath = string.Empty;

    public override string Title => System.IO.Path.GetFileName(RelativePath);

    public override string LocationLabel => RelativePath;

    public override bool CanSave => !IsLoading;

    // ── Text buffer ────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _content = string.Empty;

    /// <summary>Newline-joined line numbers, aligned with the editor's line height.</summary>
    [ObservableProperty]
    private string _gutterText = "1";

    [ObservableProperty]
    private int _lineCount = 1;

    /// <summary>Two-way bound to the TextBox so the status bar can show line/column.</summary>
    [ObservableProperty]
    private int _caretIndex;

    [ObservableProperty]
    private int _caretLine = 1;

    [ObservableProperty]
    private int _caretColumn = 1;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Per-document message (save result, load warnings).</summary>
    [ObservableProperty]
    private string _infoMessage = string.Empty;

    /// <summary>"LF" or "CRLF" — shown in the status bar and preserved on save.</summary>
    [ObservableProperty]
    private string _lineEndingLabel = "LF";

    /// <summary>True when the file was not valid UTF-8 and was decoded lossily.</summary>
    [ObservableProperty]
    private bool _hasDecodingWarning;

    /// <summary>Character count shown in the status bar.</summary>
    public int CharacterCount => Content.Length;

    /// <summary>Encoding label shown in the status bar.</summary>
    public string EncodingLabel => HasUtf8Bom ? "UTF-8 BOM" : "UTF-8";

    // ── Tags ───────────────────────────────────────────────────────────────

    public ObservableCollection<FileTagItem> Tags { get; } = new();

    public ObservableCollection<TagSuggestionItem> TagSuggestions { get; } = new();

    /// <summary>Whether the tag autocomplete dropdown should be visible.</summary>
    [ObservableProperty]
    private bool _hasTagSuggestions;

    [ObservableProperty]
    private string _newTagName = string.Empty;

    [ObservableProperty]
    private bool _hasTags;

    /// <summary>
    /// Set while programmatically changing NewTagName (e.g. accepting a
    /// suggestion) to avoid rebuilding the suggestion list mid-selection.
    /// </summary>
    private bool _suppressTagFilter;

    // ── Loading / saving ───────────────────────────────────────────────────

    /// <summary>Load (or reload) the file from disk, discarding any buffer edits.</summary>
    public async Task<bool> LoadAsync()
    {
        IsLoading = true;
        try
        {
            var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
            if (string.IsNullOrWhiteSpace(repository))
            {
                InfoMessage = "请先在设置中配置仓库目录";
                return false;
            }

            var read = await _fileContent.ReadTextAsync(repository, RelativePath);
            if (!read.Success || read.Data == null)
            {
                InfoMessage = DescribeLoadFailure(repository, read.Message);
                AppLog.Error($"打开文件失败：{RelativePath} — {InfoMessage}");
                return false;
            }

            _savedContent = read.Data.Content;
            _lineEnding = read.Data.LineEnding;
            HasUtf8Bom = read.Data.HasUtf8Bom;
            LineEndingLabel = _lineEnding == "\r\n" ? "CRLF" : "LF";
            HasDecodingWarning = read.Data.HadDecodingIssues;

            Content = read.Data.Content;   // recomputes metrics; IsDirty stays false
            IsDirty = false;
            InfoMessage = read.Data.HadDecodingIssues
                ? "该文件不是有效的 UTF-8，部分字符已被替换，保存前请确认。"
                : string.Empty;

            await LoadTagsAsync();
            return true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public override async Task<bool> SaveAsync()
    {
        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository))
        {
            InfoMessage = "请先在设置中配置仓库目录";
            return false;
        }

        var result = await _fileContent.WriteTextAsync(
            repository, RelativePath, Content, _lineEnding, HasUtf8Bom);

        if (!result.Success)
        {
            InfoMessage = result.Message;
            return false;
        }

        _savedContent = Content;
        IsDirty = false;
        InfoMessage = $"已保存 · {DateTime.Now:HH:mm:ss}";

        // A file created outside the index becomes taggable once opened and saved.
        await _kbService.EnsureFileIndexedAsync(repository, RelativePath);
        return true;
    }

    /// <summary>Save button in the editor toolbar.</summary>
    [RelayCommand]
    private async Task Save() => await SaveAsync();

    /// <summary>
    /// Reload button: discard buffer edits and re-read the file from disk, after
    /// confirming when there is anything to lose.
    /// </summary>
    [RelayCommand]
    private async Task Reload()
    {
        if (IsDirty)
        {
            var confirmed = await _dialogService.ConfirmAsync(
                "放弃更改",
                $"\"{Title}\" 有未保存的更改，重新加载会丢弃它们。确定继续吗？");
            if (!confirmed) return;
        }

        await LoadAsync();
    }

    /// <summary>
    /// Turn a raw read failure into something the user can act on. The most common
    /// case by far is an index entry whose file has since been moved or removed
    /// outside KBManager, which needs a different fix from a genuine read error.
    /// </summary>
    private string DescribeLoadFailure(string repository, string coreMessage)
    {
        if (!_fileContent.Exists(repository, RelativePath))
        {
            return $"文件不在磁盘上：{RelativePath}\n" +
                   $"仓库目录：{repository}\n\n" +
                   "这通常是因为索引里的记录过期了（文件在仓库外被移动、重命名或删除）。" +
                   "可以点击资源管理器工具栏的「同步」刷新索引，或确认「设置」里的仓库目录是否正确。";
        }

        return $"无法读取 {RelativePath}\n仓库目录：{repository}\n原因：{coreMessage}";
    }

    // ── Buffer change tracking ─────────────────────────────────────────────
    partial void OnContentChanged(string value)
    {
        IsDirty = !string.Equals(value, _savedContent, StringComparison.Ordinal);
        OnPropertyChanged(nameof(CharacterCount));
        UpdateLineMetrics(value);
        UpdateCaretPosition();
    }

    partial void OnCaretIndexChanged(int value) => UpdateCaretPosition();

    private void UpdateLineMetrics(string value)
    {
        int lines = 1;
        foreach (var ch in value)
        {
            if (ch == '\n') lines++;
        }

        LineCount = lines;

        // Rebuilding the gutter string is O(n); only do it when the count changes.
        if (lines == _gutterLineCount) return;
        _gutterLineCount = lines;
        GutterText = BuildGutterText(lines);
    }

    private static string BuildGutterText(int lines)
    {
        var sb = new StringBuilder(lines * 4);
        for (int i = 1; i <= lines; i++)
        {
            if (i > 1) sb.Append('\n');
            sb.Append(i);
        }
        return sb.ToString();
    }

    private void UpdateCaretPosition()
    {
        int index = Math.Clamp(CaretIndex, 0, Content.Length);
        int line = 1;
        int lineStart = 0;

        for (int i = 0; i < index; i++)
        {
            if (Content[i] != '\n') continue;
            line++;
            lineStart = i + 1;
        }

        CaretLine = line;
        CaretColumn = index - lineStart + 1;
    }

    // ── Tag editing ────────────────────────────────────────────────────────

    public async Task LoadTagsAsync()
    {
        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository)) return;

        var result = await _kbService.GetFileWithTagsAsync(repository, RelativePath);
        Tags.Clear();
        if (result.Success && result.Data != null)
        {
            foreach (var tag in result.Data.Tags)
                Tags.Add(new FileTagItem { TagName = tag });
        }
        HasTags = Tags.Count > 0;

        await RefreshTagSuggestionsAsync();
    }

    private async Task RefreshTagSuggestionsAsync()
    {
        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository)) return;

        var result = await _kbService.GetTagsWithFileCountAsync(repository);
        _allTags = result.Success && result.Data != null ? result.Data : new List<TagWithCountDto>();
        FilterTagSuggestions(NewTagName);
    }

    partial void OnNewTagNameChanged(string value)
    {
        if (_suppressTagFilter) return;
        FilterTagSuggestions(value);
    }

    private void FilterTagSuggestions(string filter)
    {
        var query = filter?.Trim() ?? string.Empty;

        // Only offer completions while the user is actually typing, so the strip
        // stays compact and the editor keeps its vertical space.
        if (query.Length == 0)
        {
            TagSuggestions.Clear();
            HasTagSuggestions = false;
            return;
        }

        var owned = Tags.Select(t => t.TagName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matches = _allTags
            .Where(t => !owned.Contains(t.TagName))
            .Where(t => t.TagName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();

        TagSuggestions.Clear();
        foreach (var match in matches)
        {
            TagSuggestions.Add(new TagSuggestionItem
            {
                TagName = match.TagName,
                FileCount = match.FileCount
            });
        }

        HasTagSuggestions = TagSuggestions.Count > 0;
    }

    /// <summary>Accept a suggestion: fill the box and commit the tag immediately.</summary>
    [RelayCommand]
    private async Task AcceptTagSuggestionAsync(TagSuggestionItem? suggestion)
    {
        if (suggestion == null) return;

        _suppressTagFilter = true;
        NewTagName = suggestion.TagName;
        _suppressTagFilter = false;

        await AddTagAsync();
    }

    [RelayCommand]
    public async Task AddTagAsync()
    {
        var tagName = NewTagName.Trim();
        if (string.IsNullOrEmpty(tagName))
        {
            InfoMessage = "请输入标签名称";
            return;
        }

        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository)) return;

        var result = await _kbService.AddTagToFileAsync(repository, RelativePath, tagName);
        InfoMessage = result.Message;

        if (result.Success)
        {
            NewTagName = string.Empty;
            await LoadTagsAsync();
            await RaiseTagsChangedAsync();
        }
    }

    [RelayCommand]
    private async Task RemoveTagAsync(FileTagItem? tag)
    {
        if (tag == null) return;

        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository)) return;

        var result = await _kbService.RemoveTagFromFileAsync(repository, RelativePath, tag.TagName);
        InfoMessage = result.Message;

        if (result.Success)
        {
            await LoadTagsAsync();
            await RaiseTagsChangedAsync();
        }
    }

    /// <summary>Re-read tags from the index without touching the text buffer.</summary>
    public async Task SyncTagsFromIndexAsync()
    {
        await LoadTagsAsync();
    }

    private async Task RaiseTagsChangedAsync()
    {
        var handler = TagsChanged;
        if (handler != null) await handler();
    }
}
