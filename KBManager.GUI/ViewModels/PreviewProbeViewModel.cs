namespace KBManager.GUI.ViewModels;

/// <summary>
/// Stage 1 验证页的 ViewModel。
///
/// 它本身几乎不做事，只把宿主交给视图：预览要跟随「当前打开的文档」，
/// 而这件事需要访问 <see cref="MainViewModel.ActiveFile"/> 与仓库根目录。
/// 订阅与防抖留在视图层，和 FileEditorView 处理编辑器事件的方式一致。
/// 验证结束后本文件即可删除。
/// </summary>
public class PreviewProbeViewModel : ViewModelBase
{
    public PreviewProbeViewModel(MainViewModel shell) => Shell = shell;

    /// <summary>宿主，用来读取当前文档与仓库根目录。</summary>
    public MainViewModel Shell { get; }
}
