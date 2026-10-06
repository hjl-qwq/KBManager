using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace KBManager.GUI.Services;

/// <summary>
/// Markdown → <b>Typora 兼容 HTML</b> 的映射层。
///
/// 为什么需要它：Markdig 默认吐出来的 HTML，Typora 主题认不出来。
/// 例如 Markdig 给代码块的是 <c>&lt;pre&gt;&lt;code class="language-x"&gt;</c>，
/// 而 <c>github.css</c> / <c>night.css</c> 这些主题要的是
/// <c>&lt;pre class="md-fences md-end-block" lang="x"&gt;&lt;div class="md-lang"&gt;</c>。
/// 主题里所有 <c>.md-*</c> 规则都靠这些 class 才能命中，所以这一层是
/// 「沿用 Typora 主题」的必需品，不是可选装饰。
///
/// 实现方式：直接遍历 Markdig 的 AST 自己输出 HTML，而不是继承 Markdig 的
/// <c>HtmlRenderer</c>。这样每个 class 都由这里显式决定，不必依赖 Markdig
/// 渲染器管道的内部约定；代价是行内元素要自己递归。
///
/// 已覆盖的 Typora 钩子（括号内是使用它的主题数）：
///   <c>.md-fences</c> + <c>.md-lang</c>（代码块）、<c>.md-meta-block</c>（YAML 前言）、
///   <c>.task-list</c> + <c>.md-task-list-item</c>（任务列表）、<c>figure &gt; table</c>（表格）、
///   <c>.md-image</c>（图片）、<c>.md-toc</c> + <c>.md-toc-item.md-toc-hN</c> + <c>.md-toc-inner</c>（目录）。
/// 尚未覆盖（各自独立，见 README 的路线图）：代码高亮 token 配色、MathJax 公式、mermaid 图表、
/// 脚注 <c>.footnotes</c>、<c>.md-alert</c> 提示块。
/// </summary>
public sealed class TyporaMarkdownRenderer
{
    /// <summary>
    /// 管线：只开 Typora 也认的扩展。刻意不开 <c>UseGenericAttributes</c>、
    /// <c>UseMathematics</c> 等——用不到的能力只会让输出更难对齐主题。
    /// </summary>
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .UsePipeTables()
        .UseTaskLists()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .UseCjkFriendlyEmphasis()
        .Build();

    private readonly StringBuilder _html = new();

    /// <summary>标题 → 锚点 id。目录要能跳转，所以先扫一遍分配锚点。</summary>
    private readonly Dictionary<HeadingBlock, string> _anchors = new();

    /// <summary>标题文本与锚点，供 [TOC] 展开。</summary>
    private readonly List<(int Level, string Text, string Anchor)> _outline = new();

    /// <summary>
    /// 把 Markdown 渲染成 <c>&lt;div id="write"&gt;</c> 里面那段 HTML（不含外层 div）。
    /// 相同的输入永远得到相同的输出，且不使用静态可变状态，所以可以安全复用实例。
    /// </summary>
    public string ToHtml(string? markdown)
    {
        _html.Clear();
        _anchors.Clear();
        _outline.Clear();

        // trackTrivia 保持关闭：这里只渲染，不需要源码位置信息。
        var document = Markdown.Parse(markdown ?? string.Empty, Pipeline);

        CollectHeadings(document);
        WriteBlocks(document);

        return _html.ToString();
    }

    // ── 第一趟：分配锚点并收集大纲 ────────────────────────────────────────────

    private void CollectHeadings(ContainerBlock container)
    {
        foreach (var block in container)
        {
            if (block is HeadingBlock heading)
            {
                // 锚点用序号而不是 slug：不依赖标题文本，永远不会重复或为空。
                var anchor = "kb-h" + (_anchors.Count + 1);
                _anchors[heading] = anchor;
                _outline.Add((heading.Level, InlineToPlainText(heading.Inline), anchor));
            }

            // 标题可能嵌在引用块或列表里，所以递归。
            if (block is ContainerBlock nested)
                CollectHeadings(nested);
        }
    }

    // ── 第二趟：输出块级元素 ────────────────────────────────────────────────

    private void WriteBlocks(ContainerBlock container)
    {
        foreach (var block in container)
            WriteBlock(block);
    }

    private void WriteBlock(Block block)
    {
        // 顺序有讲究：YamlFrontMatterBlock 和 FencedCodeBlock 都派生自 CodeBlock，
        // 必须排在 CodeBlock 之前，否则会被更宽的模式先接住。
        switch (block)
        {
            case HeadingBlock heading:
                WriteHeading(heading);
                break;
            case ParagraphBlock paragraph:
                WriteParagraph(paragraph);
                break;
            case YamlFrontMatterBlock frontMatter:
                WriteMetaBlock(frontMatter);
                break;
            case FencedCodeBlock fenced:
                WriteFencedCode(fenced);
                break;
            case CodeBlock indented:
                WriteIndentedCode(indented);
                break;
            case QuoteBlock quote:
                _html.Append("<blockquote>\n");
                WriteBlocks(quote);
                _html.Append("</blockquote>\n");
                break;
            case ListBlock list:
                WriteList(list);
                break;
            case Table table:
                WriteTable(table);
                break;
            case ThematicBreakBlock:
                _html.Append("<hr>\n");
                break;
            case HtmlBlock html:
                // 原样放行：Typora 也渲染笔记里的裸 HTML。
                // ⚠️ 这里放行的 <script> 目前会执行——在加上 C#↔JS 桥接之前必须先解决
                //（净化 / 关闭脚本 / CSP），否则笔记内容就能调用宿主。
                _html.Append(html.Lines.ToString());
                break;
            case ContainerBlock container:
                WriteBlocks(container);
                break;
        }
    }

    private void WriteHeading(HeadingBlock heading)
    {
        var anchor = _anchors.TryGetValue(heading, out var value) ? value : "kb-h";
        _html.Append("<h").Append(heading.Level).Append(" id=\"").Append(anchor).Append("\">");
        WriteInlines(heading.Inline);
        _html.Append("</h").Append(heading.Level).Append(">\n");
    }

    private void WriteParagraph(ParagraphBlock paragraph)
    {
        if (IsTableOfContentsPlaceholder(paragraph))
        {
            WriteTableOfContents();
            return;
        }

        _html.Append("<p>");
        WriteInlines(paragraph.Inline);
        _html.Append("</p>\n");
    }

    /// <summary>Typora 用单独一行 <c>[TOC]</c> 展开目录，这里识别同样写法（大小写不敏感）。</summary>
    private static bool IsTableOfContentsPlaceholder(ParagraphBlock paragraph)
    {
        var first = paragraph.Inline?.FirstChild;
        return first is LiteralInline literal
               && first.NextSibling is null
               && literal.Content.ToString().Trim().Equals("[TOC]", StringComparison.OrdinalIgnoreCase);
    }

    private void WriteTableOfContents()
    {
        _html.Append("<div class=\"md-toc\">\n<div class=\"md-toc-content\">\n");
        foreach (var (level, text, anchor) in _outline)
        {
            _html.Append("<span class=\"md-toc-item md-toc-h").Append(level).Append("\">")
                 .Append("<a class=\"md-toc-inner\" href=\"#").Append(anchor).Append("\">")
                 .Append(WebUtility.HtmlEncode(text))
                 .Append("</a></span>\n");
        }

        _html.Append("</div>\n</div>\n");
    }

    private void WriteMetaBlock(YamlFrontMatterBlock frontMatter)
    {
        _html.Append("<pre class=\"md-meta-block\">")
             .Append(WebUtility.HtmlEncode(frontMatter.Lines.ToString()))
             .Append("</pre>\n");
    }

    private void WriteFencedCode(FencedCodeBlock fenced)
    {
        var language = (fenced.Info ?? string.Empty).Trim();

        _html.Append("<pre class=\"md-fences md-end-block\"");
        if (language.Length > 0)
            _html.Append(" lang=\"").Append(WebUtility.HtmlEncode(language)).Append('"');
        _html.Append('>');

        // Typora 把语言名放在代码块左上角，并给它 .md-lang（github.css 会把它染成 #b4654d）。
        if (language.Length > 0)
            _html.Append("<div class=\"md-lang\">").Append(WebUtility.HtmlEncode(language)).Append("</div>");

        _html.Append("<code>").Append(WebUtility.HtmlEncode(fenced.Lines.ToString())).Append("</code></pre>\n");
    }

    private void WriteIndentedCode(CodeBlock code)
    {
        _html.Append("<pre class=\"md-fences md-end-block\"><code>")
             .Append(WebUtility.HtmlEncode(code.Lines.ToString()))
             .Append("</code></pre>\n");
    }

    private void WriteList(ListBlock list)
    {
        var hasTaskItem = false;
        foreach (var child in list)
        {
            if (child is ListItemBlock item && FindTaskList(item) != null)
            {
                hasTaskItem = true;
                break;
            }
        }

        var tag = list.IsOrdered ? "ol" : "ul";
        _html.Append('<').Append(tag);
        if (hasTaskItem)
            _html.Append(" class=\"task-list\"");
        if (list.IsOrdered && !string.IsNullOrEmpty(list.OrderedStart) && list.OrderedStart != "1")
            _html.Append(" start=\"").Append(WebUtility.HtmlEncode(list.OrderedStart!)).Append('"');
        _html.Append(">\n");

        foreach (var child in list)
        {
            if (child is ListItemBlock item)
                WriteListItem(item, list.IsLoose);
        }

        _html.Append("</").Append(tag).Append(">\n");
    }

    private void WriteListItem(ListItemBlock item, bool loose)
    {
        var task = FindTaskList(item);

        _html.Append("<li");
        if (task != null)
            _html.Append(" class=\"md-task-list-item task-list-item\"");
        _html.Append('>');

        // 复选框必须是 <li> 的直接子元素：主题用 .md-task-list-item > input 绝对定位它。
        // disabled 是刻意的——预览不接收交互，而 Typora 在导出时也是禁用指针事件的。
        if (task != null)
            _html.Append("<input type=\"checkbox\" disabled").Append(task.Checked ? " checked" : string.Empty).Append('>');

        foreach (var child in item)
        {
            if (child is ParagraphBlock paragraph)
            {
                // 紧凑列表不套 <p>，否则每项都会多出一段块间距（和 Markdig 自己的行为一致）。
                if (loose)
                {
                    _html.Append("<p>");
                    WriteInlines(paragraph.Inline);
                    _html.Append("</p>\n");
                }
                else
                {
                    WriteInlines(paragraph.Inline);
                    _html.Append('\n');
                }

                continue;
            }

            WriteBlock(child);
        }

        _html.Append("</li>\n");
    }

    /// <summary>任务项的标志是「第一个块的第一行内节点是 TaskList」（Markdig 的 TaskLists 扩展约定）。</summary>
    private static TaskList? FindTaskList(ListItemBlock item)
    {
        foreach (var child in item)
        {
            if (child is ParagraphBlock paragraph)
                return paragraph.Inline?.FirstChild as TaskList;
            break;
        }

        return null;
    }

    private void WriteTable(Table table)
    {
        // Typora 用 <figure> 包住表格，主题的横向滚动和 bleed 边距都挂在这个 figure 上。
        _html.Append("<figure>\n<table class=\"md-table\">\n");

        var bodyOpen = false;
        var column = 0;

        foreach (var child in table)
        {
            if (child is not TableRow row)
                continue;

            if (row.IsHeader)
            {
                _html.Append("<thead>\n");
                WriteTableRow(row, table, isHeader: true, ref column);
                _html.Append("</thead>\n");
                continue;
            }

            if (!bodyOpen)
            {
                _html.Append("<tbody>\n");
                bodyOpen = true;
            }

            WriteTableRow(row, table, isHeader: false, ref column);
        }

        if (bodyOpen)
            _html.Append("</tbody>\n");

        _html.Append("</table>\n</figure>\n");
    }

    private void WriteTableRow(TableRow row, Table table, bool isHeader, ref int column)
    {
        var tag = isHeader ? "th" : "td";

        _html.Append("<tr>\n");
        foreach (var child in row)
        {
            if (child is not TableCell cell)
                continue;

            _html.Append('<').Append(tag);
            var alignment = AlignmentStyle(table, column);
            if (alignment != null)
                _html.Append(" style=\"").Append(alignment).Append('"');
            if (cell.ColumnSpan > 1)
                _html.Append(" colspan=\"").Append(cell.ColumnSpan).Append('"');
            _html.Append('>');

            WriteTableCellContent(cell);

            _html.Append("</").Append(tag).Append(">\n");
            column += Math.Max(1, cell.ColumnSpan);
        }

        _html.Append("</tr>\n");
    }

    /// <summary>
    /// 单元格内容。这里刻意<b>不</b>套 <c>&lt;p&gt;</c>：主题给 <c>p</c> 的块级边距
    /// （例如 github.css 的 <c>0.8em</c>）会把每行撑得很高，而 Typora 的单元格里也没有 <c>&lt;p&gt;</c>。
    /// Markdig 自己的表格渲染器同样会丢掉这一层。
    /// </summary>
    private void WriteTableCellContent(TableCell cell)
    {
        foreach (var child in cell)
        {
            if (child is ParagraphBlock paragraph)
            {
                WriteInlines(paragraph.Inline);
                continue;
            }

            WriteBlock(child);
        }
    }

    private static string? AlignmentStyle(Table table, int column)
    {
        if (column >= table.ColumnDefinitions.Count)
            return null;

        return table.ColumnDefinitions[column].Alignment switch
        {
            TableColumnAlign.Center => "text-align:center",
            TableColumnAlign.Right => "text-align:right",
            TableColumnAlign.Left => "text-align:left",
            _ => null
        };
    }

    // ── 行内元素 ────────────────────────────────────────────────────────────

    private void WriteInlines(ContainerInline? container)
    {
        for (var inline = container?.FirstChild; inline != null; inline = inline.NextSibling)
            WriteInline(inline);
    }

    private void WriteInline(Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                _html.Append(WebUtility.HtmlEncode(literal.Content.ToString()));
                break;

            case CodeInline code:
                _html.Append("<code>").Append(WebUtility.HtmlEncode(code.Content)).Append("</code>");
                break;

            case LinkInline { IsImage: true } image:
                WriteImage(image);
                break;

            case LinkInline link:
                _html.Append("<a href=\"").Append(WebUtility.HtmlEncode(link.Url ?? string.Empty)).Append('"');
                if (!string.IsNullOrEmpty(link.Title))
                    _html.Append(" title=\"").Append(WebUtility.HtmlEncode(link.Title!)).Append('"');
                _html.Append('>');
                WriteInlines(link);
                _html.Append("</a>");
                break;

            case AutolinkInline autolink:
                var url = autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url;
                _html.Append("<a href=\"").Append(WebUtility.HtmlEncode(url)).Append("\">")
                     .Append(WebUtility.HtmlEncode(autolink.Url))
                     .Append("</a>");
                break;

            case EmphasisInline emphasis:
                WriteEmphasis(emphasis);
                break;

            case LineBreakInline lineBreak:
                _html.Append(lineBreak.IsHard ? "<br>\n" : "\n");
                break;

            case HtmlInline html:
                // 同 HtmlBlock：原样放行，加上桥接前必须先解决脚本执行问题。
                _html.Append(html.Tag);
                break;

            case TaskList:
                // 由 WriteListItem 负责输出复选框，这里跳过，避免出现两个。
                break;

            case ContainerInline container:
                WriteInlines(container);
                break;
        }
    }

    private void WriteImage(LinkInline image)
    {
        _html.Append("<span class=\"md-image\"><img src=\"")
             .Append(WebUtility.HtmlEncode(image.Url ?? string.Empty))
             .Append("\" alt=\"")
             .Append(WebUtility.HtmlEncode(InlineToPlainText(image)))
             .Append('"');
        if (!string.IsNullOrEmpty(image.Title))
            _html.Append(" title=\"").Append(WebUtility.HtmlEncode(image.Title!)).Append('"');
        _html.Append("></span>");
    }

    /// <summary>
    /// 强调。Markdig 把定界符和数量原样交给我们，由这里决定标签：
    /// <c>**</c>/<c>__</c>→strong，<c>*</c>/<c>_</c>→em，<c>~~</c>→del，<c>~</c>→sub，
    /// <c>==</c>→mark，<c>++</c>→ins，<c>^</c>→sup（分别来自 EmphasisExtras 扩展）。
    /// </summary>
    private void WriteEmphasis(EmphasisInline emphasis)
    {
        var (open, close) = emphasis.DelimiterChar switch
        {
            '~' when emphasis.DelimiterCount >= 2 => ("<del>", "</del>"),
            '~' => ("<sub>", "</sub>"),
            '=' => ("<mark>", "</mark>"),
            '+' => ("<ins>", "</ins>"),
            '^' => ("<sup>", "</sup>"),
            _ when emphasis.DelimiterCount >= 2 => ("<strong>", "</strong>"),
            _ => ("<em>", "</em>")
        };

        _html.Append(open);
        WriteInlines(emphasis);
        _html.Append(close);
    }

    /// <summary>取一段行内的纯文本（目录项、图片 alt 用）。</summary>
    private static string InlineToPlainText(ContainerInline? container)
    {
        var text = new StringBuilder();
        AppendPlainText(text, container);
        return text.ToString().Trim();
    }

    private static void AppendPlainText(StringBuilder text, ContainerInline? container)
    {
        for (var inline = container?.FirstChild; inline != null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case LineBreakInline:
                    text.Append(' ');
                    break;
                // HTML 标签、复选框不参与纯文本。
                case HtmlInline:
                case TaskList:
                    break;
                case ContainerInline nested:
                    AppendPlainText(text, nested);
                    break;
            }
        }
    }
}
