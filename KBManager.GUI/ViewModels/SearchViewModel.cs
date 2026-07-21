using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using KBManager.GUI.Services;
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

    public SearchViewModel(IKnowledgeBaseService kbService, GitHelper gitHelper)
    {
        _kbService = kbService;
        _gitHelper = gitHelper;
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
