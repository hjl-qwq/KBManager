using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KBManager.GUI.Services;
using KBManager.GUI.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace KBManager.GUI.Views;

/// <summary>
/// Stage 1 验证页 —— 渲染管线（Markdig → 映射层 → 主题 CSS → WebView）的验证台，
/// 同时是正式预览功能的骨架。仍<b>不是</b>产品功能：编辑器一行不动，这一页只读。
///
/// 它现在跑的是<b>真实链路</b>：取「最后看过的那篇笔记」的正文 → <see cref="TyporaMarkdownRenderer"/>
/// 做 Typora class 映射 → <see cref="PreviewDocumentBuilder"/> 套上底座 shim 与主题 → WebView。
/// 来源用的是宿主的 <see cref="MainViewModel.LastActiveFile"/> 而不是 <c>ActiveFile</c>：
/// 本页自己就是一个工具页标签，切到它时 <c>ActiveFile</c> 已经是 null 了。
/// 没有打开过笔记时退回到内置样例，保证这一页本身永远能自检。
/// 编辑正文后 200ms 自动重渲染，而且<b>只替换 <c>#write</c> 的内容、不重新导航整页</b>：
/// 整页导航会重新解析 CSS、重新布局、重新加载字体，首帧之前露白底，也就是「边写边看时会闪」
/// 的原因。首次加载与切换主题仍会导航（那两次是必要的）。
///
/// 页面顶部黑色诊断栏里的「映射层产出的 Typora 钩子」那几行是关键：它们直接数
/// <c>.md-fences</c> / <c>.md-task-list-item</c> / <c>.md-toc-item</c> / <c>.md-image</c> /
/// <c>figure &gt; table</c> 的数量，所以映射层哪一类没产出，一眼就能看出来。
/// </summary>
public partial class PreviewProbeView : UserControl
{
    /// <summary>临时 HTML 的文件名前缀，方便识别与清理。</summary>
    private const string ProbeFilePrefix = "kbmanager-preview-probe-";

    /// <summary>
    /// 编辑停止多久后重渲染。60ms 在打字节奏下几乎察觉不到延迟，同时仍能把一次连打合并成一次更新。
    /// 300ms 之类的大值会明显拖手感；而「整篇重渲染 + 整段替换」是分栏预览的固有限制，
    /// 真正的增量更新要等 C1 把编辑面搬进 WebView 之后。
    /// </summary>
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(60);

    private readonly TyporaMarkdownRenderer _renderer = new();

    // Avalonia 的 DispatcherTimer 没有 IsRepeating，所以「只跑一次」是靠 Tick 里先 Stop 实现的。
    private readonly DispatcherTimer _debounce = new() { Interval = RefreshDelay };

    // 本项目的 View 都不依赖 XAML 编译器生成的命名字段（见 ExplorerView / FileEditorView），
    // 而是在 OnLoaded 里用 FindControl 取引用 —— 这里保持一致。
    private ComboBox? _themeBox;
    private Button? _reloadButton;
    private TextBlock? _themePathText;
    private TextBlock? _loadModeText;
    private TextBlock? _statusText;
    private NativeWebView? _webView;

    /// <summary>宿主 ViewModel，用来跟随「当前打开的文档」。</summary>
    private MainViewModel? _shell;

    /// <summary>正在预览的文档；订阅它的 Content 变化实现实时刷新。</summary>
    private FileDocumentViewModel? _sourceDocument;
    private string _themeDirectory = string.Empty;
    private string? _lastProbeFile;
    private int _generation;
    private bool _initialized;

    // ── 「不重新导航」所需要的状态 ──────────────────────────────────────────
    // 首次加载（以及换主题）必须整页导航，之后只替换 #write 的内容。

    /// <summary>页面已加载完成，此时 InvokeScript 才是安全的。</summary>
    private bool _pageLoaded;

    /// <summary>一次整页导航正在进行。</summary>
    private bool _navigating;

    /// <summary>导航期间又来了新内容，加载完成后补渲染一次，避免连着导航两次。</summary>
    private bool _renderPending;

    /// <summary>当前已加载页面实际使用的主题。与选中的主题不同就必须重新导航。</summary>
    private string? _loadedTheme;

    /// <summary>正在加载的那个页面用的主题。</summary>
    private string? _navigatingTheme;

    /// <summary>
    /// 把正文变成 JS 字符串字面量时要用的序列化选项。不转义非 ASCII：
    /// 中文笔记用默认编码器会被膨胀成 \uXXXX，白白放大脚本体积；
    /// 这里只是塞进 InvokeScript 的参数，不涉及 HTML 嵌入。
    /// </summary>
    private static readonly JsonSerializerOptions ScriptJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 是否在页面顶部显示诊断栏。作为工具页（验证台）时是 true，用来核对映射层产出的钩子；
    /// 作为编辑区右侧分栏日常使用时由 XAML 传 false。
    /// </summary>
    public bool ShowDiagnostics { get; set; } = true;

    public PreviewProbeView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // 控件引用与主题列表只解析一次；但订阅每次附加都要做，所以分成两段。
        if (!_initialized)
        {
            _initialized = true;

            _themeBox = this.FindControl<ComboBox>("ThemeBox");
            _reloadButton = this.FindControl<Button>("ReloadButton");
            _themePathText = this.FindControl<TextBlock>("ThemePathText");
            _loadModeText = this.FindControl<TextBlock>("LoadModeText");
            _statusText = this.FindControl<TextBlock>("StatusText");
            _webView = this.FindControl<NativeWebView>("ProbeWebView");

            if (_themeBox == null || _webView == null || _statusText == null) return;

            // 页面加载完成才知道可以安全地「只替换正文」。
            _webView.NavigationCompleted += (_, _) =>
            {
                // 页内锚点跳转（比如点目录里的链接）也会触发这个事件，但那不是重新加载，
                // 不能拿它覆盖状态，否则下一次编辑会误判成「主题变了」而整页重载。
                if (!_navigating) return;

                _pageLoaded = true;
                _navigating = false;
                _loadedTheme = _navigatingTheme;

                // 加载期间累积的新内容在这里补一次。
                if (_renderPending)
                {
                    _renderPending = false;
                    Render();
                }
            };

            _themeDirectory = ResolveThemeDirectory();
            var themes = ListThemes(_themeDirectory);

            if (_themePathText != null)
            {
                _themePathText.Text = _themeDirectory.Length == 0
                    ? "找不到 Typora 主题目录"
                    : $"{_themeDirectory} · {themes.Count} 个主题";
            }

            if (themes.Count == 0)
            {
                _statusText.Text = @"在 %APPDATA%\Typora\themes 也找不到 .css 主题文件";
                return;
            }

            _themeBox.ItemsSource = themes;
            // github.css 最接近我们最终要的默认外观，所以优先选它。
            var preferred = themes.IndexOf("github.css");
            _themeBox.SelectedIndex = preferred >= 0 ? preferred : 0;
            _themeBox.SelectionChanged += (_, _) => Render(forceNavigate: true);
            if (_reloadButton != null)
                _reloadButton.Click += (_, _) => Render(forceNavigate: true);

            _debounce.Tick += (_, _) =>
            {
                _debounce.Stop();
                Render();
            };
        }

        HookShell();
        Render();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        // 页面被切走就断开订阅，否则宿主和文档会一直被这个 view 引用住。
        if (_shell != null)
        {
            _shell.PropertyChanged -= OnShellPropertyChanged;
            _shell = null;
        }

        AttachToDocument(null);
        _debounce.Stop();
    }

    // ── 跟随当前文档 ────────────────────────────────────────────────────────

    private void HookShell()
    {
        if (_shell != null) return;

        // 两种放置方式：作为工具页时 DataContext 是 PreviewProbeViewModel；
        // 作为编辑区右侧分栏时，DataContext 从窗口继承下来，直接就是 MainViewModel。
        _shell = (DataContext as PreviewProbeViewModel)?.Shell ?? DataContext as MainViewModel;

        if (_shell == null) return;

        _shell.PropertyChanged += OnShellPropertyChanged;
        AttachToDocument(_shell.LastActiveFile);
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.LastActiveFile)) return;

        // 只认「真的换到了另一篇笔记」。切工具页时 ActiveDocument 会变但
        // LastActiveFile 不变，预览因此保持不动 —— 这正是想要的行为。
        if (AttachToDocument(_shell?.LastActiveFile))
            Render();
    }

    /// <summary>换预览来源。返回是否真的换了，调用方据此决定要不要立刻重渲染。</summary>
    private bool AttachToDocument(FileDocumentViewModel? document)
    {
        if (ReferenceEquals(_sourceDocument, document)) return false;

        if (_sourceDocument != null)
            _sourceDocument.PropertyChanged -= OnSourcePropertyChanged;

        _sourceDocument = document;

        if (_sourceDocument != null)
            _sourceDocument.PropertyChanged += OnSourcePropertyChanged;

        return true;
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 正文每敲一个字都会走到这里，所以只重置定时器，不直接渲染。
        if (e.PropertyName == nameof(FileDocumentViewModel.Content))
        {
            _debounce.Stop();
            _debounce.Start();
        }
    }

    // ── 渲染 ────────────────────────────────────────────────────────────────

    private void Render(bool forceNavigate = false)
    {
        if (_themeBox == null || _webView == null || _statusText == null) return;

        if (_themeBox.SelectedItem is not string theme || _themeDirectory.Length == 0)
        {
            _statusText.Text = "没有可用的主题";
            return;
        }

        var markdown = _sourceDocument?.Content ?? SampleMarkdown;
        var source = _sourceDocument?.RelativePath ?? "内置样例";
        var lines = markdown.Length == 0 ? 0 : markdown.Count(c => c == '\n') + 1;
        var body = _renderer.ToHtml(markdown);

        // ══ 快路径：页面还在，主题也没换 → 只替换 #write 的内容，绝不重新导航 ══
        // 这是「编辑时预览不闪」的关键：整页导航会重新解析 CSS、重新布局、重新加载字体，
        // 首帧之前露白底；只换 innerHTML 则样式表、字体、滚动位置全都不动。
        if (!forceNavigate && _pageLoaded && _loadedTheme == theme)
        {
            try
            {
                ApplyBody(_webView, body);
                SetLoadMode("只更新正文");
                _statusText.Text = $"来源：{source} · {lines} 行 · 主题 {theme} · 只替换正文（未重载页面）";
                return;
            }
            catch (InvalidOperationException)
            {
                // 页面已经不在了（例如适配器被重建）。退回整页导航，下一次就会自愈。
                _pageLoaded = false;
            }
        }

        if (_navigating)
        {
            // 正在加载时又来了新内容：记一笔，加载完成后补一次，避免连续导航。
            _renderPending = true;
            return;
        }

        try
        {
            var page = PreviewDocumentBuilder.Build(
                FileUri(Path.Combine(_themeDirectory, theme)).AbsoluteUri, theme, body, ResolveBaseHref(), ShowDiagnostics);

            // 首次加载写成一个真实文件而不是用 NavigateToString：file:// 文档加载同源样式表与字体
            // 是最常规的路径，实测 Typora 主题里的相对路径 woff2 能正常加载。
            // 换主题时也会走到这里 —— 那次导航是必要的，因为 <link> 必须换。
            var probeFile = Path.Combine(Path.GetTempPath(), $"{ProbeFilePrefix}{++_generation}.html");
            File.WriteAllText(probeFile, page, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            _navigating = true;
            _pageLoaded = false;
            _navigatingTheme = theme;

            _webView.Source = FileUri(probeFile);

            DeleteProbeFile(_lastProbeFile, probeFile);
            _lastProbeFile = probeFile;

            SetLoadMode("整页加载");
            _statusText.Text = $"来源：{source} · {lines} 行 · 主题 {theme} · 正在加载页面…";
        }
        catch (Exception ex)
        {
            _navigating = false;
            _statusText.Text = $"渲染失败：{ex.GetType().Name} — {ex.Message}";
        }
    }

    /// <summary>
    /// 把正文推进已加载页面的 <c>#write</c>。页面里定义了 <c>window.kbSetBody</c>。
    /// 页面还没加载完时 InvokeScript 会抛 InvalidOperationException（由调用方兜住）。
    /// </summary>
    private static void ApplyBody(NativeWebView webView, string body)
    {
        // JsonSerializer 产出的就是合法的 JS 字符串字面量：引号、反斜杠、换行都已正确转义。
        var payload = JsonSerializer.Serialize(body, ScriptJsonOptions);
        _ = webView.InvokeScript($"window.kbSetBody({payload});");
    }

    /// <summary>工具栏上的加载方式指示：一眼看出上次更新是替换正文还是整页重载。</summary>
    private void SetLoadMode(string mode)
    {
        if (_loadModeText != null)
            _loadModeText.Text = mode;
    }

    /// <summary>
    /// 笔记里的图片多是相对于笔记所在目录写的（<c>![](img/x.png)</c>），
    /// 而临时 HTML 落在 %TEMP%，不注入 &lt;base&gt; 的话这些相对路径全都会指错。
    /// 仓库根目录来自宿主的 RepositoryPath；拿不到就返回 null，让路径保持原样。
    /// </summary>
    private string? ResolveBaseHref()
    {
        if (_sourceDocument == null) return null;

        var root = _shell?.RepositoryPath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return null;

        var folder = Path.GetDirectoryName(_sourceDocument.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = string.IsNullOrEmpty(folder) ? root : Path.Combine(root, folder);
        if (!Directory.Exists(directory)) return null;

        // 结尾的分隔符很关键：没有它，相对路径会被解析成同级文件名而不是同级目录下的文件。
        return FileUri(directory + Path.DirectorySeparatorChar).AbsoluteUri;
    }

    // ── 主题目录 ────────────────────────────────────────────────────────────

    /// <summary>先用 Typora 的主题目录，其次用 exe 旁边的 themes/（将来把主题随应用分发时用）。</summary>
    private static string ResolveThemeDirectory()
    {
        try
        {
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (roaming.Length > 0)
            {
                var typora = Path.Combine(roaming, "Typora", "themes");
                if (Directory.Exists(typora)) return typora;
            }

            var local = Path.Combine(AppContext.BaseDirectory, "themes");
            if (Directory.Exists(local)) return local;
        }
        catch
        {
            // 取路径失败不值得让页面崩掉，落到下面的空字符串。
        }

        return string.Empty;
    }

    private static List<string> ListThemes(string directory)
    {
        if (directory.Length == 0) return new List<string>();

        try
        {
            return Directory.GetFiles(directory, "*.css")
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static void DeleteProbeFile(string? path, string keep)
    {
        if (path == null || path == keep) return;

        try
        {
            File.Delete(path);
        }
        catch
        {
            // 临时文件删不掉无所谓。
        }
    }

    /// <summary>把本地绝对路径变成 file:/// URI。不用 new Uri(path)：Unix 下的绝对路径会被判成相对 URI。</summary>
    private static Uri FileUri(string path)
    {
        var normalized = Path.GetFullPath(path).Replace('\\', '/');
        if (!normalized.StartsWith('/')) normalized = "/" + normalized;
        return new Uri("file://" + normalized);
    }

    /// <summary>
    /// 没有打开文档时的兜底样例。它是一段真实 Markdown，因此同时也是映射层的自检：
    /// 会走到 YAML 前言、标题、[TOC]、引用、围栏代码、任务列表、表格对齐、图片、强调、
    /// 行内代码、自动链接这些分支。
    /// </summary>
    private const string SampleMarkdown = """
---
title: 预览探针样例
tags: [probe, typora]
---

# 一级标题 Heading 1

这是一个用来检验映射层的段落。中文与 English mixed 混排。行内含 **粗体**、*斜体*、
~~删除线~~、==高亮==、`inline code`、[链接](https://typora.io)、自动链接 https://avaloniaui.net，
以及 <kbd>Ctrl</kbd> 这类裸 HTML。

[TOC]

## 二级标题 Heading 2

> 引用块：主题通常会加左边框与斜体。
>
> > 嵌套引用。

### 代码块

```csharp
public sealed class Probe
{
    // 检验 .md-fences 的背景、边距、等宽字体与 .md-lang 语言标签
    public string Name => "KBManager";
}
```

### 列表

- 无序列表项一
- 无序列表项二
  - 嵌套项

1. 有序列表第一项
2. 有序列表第二项

- [x] 已完成任务（检验 .md-task-list-item 的复选框定位）
- [ ] 未完成任务

### 表格

| 列 A | 列 B | 说明 |
| :--- | :---: | ---: |
| 1 | 2 | 左对齐、居中、右对齐 |
| 3 | 4 | 检验斑马纹 |

### 图片与分隔线

![probe](data:image/svg+xml;base64,PHN2ZyB4bWxucz0naHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmcnIHdpZHRoPScyNjAnIGhlaWdodD0nNjQnPjxyZWN0IHdpZHRoPScyNjAnIGhlaWdodD0nNjQnIHJ4PSc2JyBmaWxsPScjZTNmMmZkJyBzdHJva2U9JyM0MmE1ZjUnLz48dGV4dCB4PScxNCcgeT0nMzgnIGZvbnQtZmFtaWx5PSdzYW5zLXNlcmlmJyBmb250LXNpemU9JzE1JyBmaWxsPScjMTU2NWMwJz5pbWFnZS5wbmcg5Y2g5L2N5Zu+PC90ZXh0Pjwvc3ZnPg==)

---
""";
}
