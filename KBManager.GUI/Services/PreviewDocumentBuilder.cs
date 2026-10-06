using System.Net;

namespace KBManager.GUI.Services;

/// <summary>
/// 把「渲染好的正文」组装成送进 WebView 的完整 HTML 页面。
///
/// 页面结构刻意和 Typora 一致，顺序不能变：
///   1. <b>底座 shim</b>（本文件里的那一段样式）—— Typora 的主题是叠加在它自己的
///      base.css 之上的，主题假定一批基座变量和 <c>#write</c> 盒模型已经存在。
///      <c>onelight</c> 这类主题根本不定义 <c>#write</c> 的宽度和内边距，全靠这一层兜底；
///   2. <b>主题 &lt;link&gt;</b> —— 用 file:// 指向用户的 Typora 主题文件，主题里
///      <c>@font-face url('./github/*.woff2')</c> 这类相对路径会按 CSS 自身位置解析。
///      实测 file:// 下 Web 字体能正常加载，不需要虚拟主机映射；
///   3. <b>正文</b> —— 包在 <c>&lt;div id="write"&gt;</c> 里，由 <see cref="TyporaMarkdownRenderer"/> 产出。
///
/// shim 里的属性值是「Typora 底座给主题的默认值」，是接口约定，不是从 Typora 的文件拷贝的。
/// </summary>
public static class PreviewDocumentBuilder
{
    /// <summary>
    /// 组装整页。<paramref name="baseHref"/> 非空时注入 <c>&lt;base&gt;</c>，让笔记里的相对
    /// 图片路径能解析；<paramref name="showDiagnostics"/> 打开时页面顶部会多一条诊断栏，
    /// 数出映射层产出的 Typora 钩子数量（验证用，日常预览应关掉）。
    /// </summary>
    public static string Build(
        string themeCssHref, string themeName, string bodyHtml, string? baseHref, bool showDiagnostics)
    {
        var baseTag = string.IsNullOrEmpty(baseHref)
            ? string.Empty
            : $"<base href=\"{WebUtility.HtmlEncode(baseHref)}\">\n";

        return Template
            .Replace("__BASE_TAG__", baseTag)
            .Replace("__THEME_CSS__", WebUtility.HtmlEncode(themeCssHref))
            .Replace("__THEME_NAME__", WebUtility.HtmlEncode(themeName))
            .Replace("__BODY__", bodyHtml)
            .Replace("__DIAGNOSTICS__", showDiagnostics ? DiagnosticsMarkup : string.Empty);
    }

    // 非插值 raw string：正文里全是 CSS/JS 的花括号，用 $"" 会把它们全当成占位符。
    private const string Template = """
<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
__BASE_TAG__<title>KBManager 预览</title>
<style>
/* ══ KBManager 预览底座 shim（自写，不是 Typora 的文件）══
   给出 Typora base.css 中与正文有关的那部分：基座变量 + #write 盒模型 + 块级默认值。
   必须排在主题 <link> 之前，主题才能覆盖它。 */
:root {
  --bg-color: #ffffff;
  --text-color: #333333;
  --select-text-bg-color: #B5D6FC;
  --select-text-font-color: auto;
  --monospace: "Lucida Console", Consolas, "Courier", monospace;
  --primary-color: #4183c4;
  --item-hover-bg-color: #f5f5f5;
  --item-hover-text-color: #333333;
  --side-bar-bg-color: #fafafa;
  --window-border: #efefef;
  --control-text-color: #777777;
  --control-text-hover-color: #eeeeee;
  --active-file-bg-color: #f3f3f3;
  --active-file-text-color: #333333;
  --active-file-border-color: #f3f3f3;
  --search-select-bg-color: #B5D6FC;
  --rawblock-edit-panel-bd: #e0e0e0;
}
html {
  font-size: 14px;
  background-color: var(--bg-color);
  color: var(--text-color);
  font-family: "Helvetica Neue", Helvetica, Arial, "Segoe UI Emoji", "Microsoft YaHei", sans-serif;
}
body { margin: 0; font-size: 1rem; line-height: 1.42857143; background: inherit; }
*, *::before, *::after { box-sizing: border-box; }
#write {
  margin: 0 auto;
  word-break: normal;
  word-wrap: break-word;
  position: relative;
  white-space: normal;
  overflow-x: visible;
  padding: 36px 40px 60px;
}
h1 { font-size: 2rem; }
h2 { font-size: 1.8rem; }
h3 { font-size: 1.6rem; }
h4 { font-size: 1.4rem; }
h5 { font-size: 1.2rem; }
h6 { font-size: 1rem; }
h1, h2, h3, h4, h5, h6, p, .md-math-block { margin-top: 1rem; margin-bottom: 1rem; }
p { line-height: inherit; }
li { margin: 0; position: relative; }
li p { margin: 0.5rem 0; }
blockquote { margin: 1rem 0; }
figure { overflow-x: auto; margin: 1.2em 0 0; max-width: calc(100% + 16px); padding: 0; }
figure > table { margin: 0; }
table { border-collapse: collapse; border-spacing: 0; width: 100%; text-align: left; }
img { max-width: 100%; vertical-align: middle; }
code, pre, samp, tt { font-family: var(--monospace); }
mark { background: #ffff00; color: #000000; }
sup.md-footnote { padding: 2px 4px; background-color: rgba(238, 238, 238, 0.7); color: #555555; border-radius: 4px; cursor: pointer; }
.footnotes { opacity: 0.8; font-size: 0.9rem; margin-top: 1em; margin-bottom: 1em; }
.footnote-line { margin-top: 0.714em; font-size: 0.7em; }
pre.md-meta-block { font-size: 0.8rem; min-height: 0.8rem; white-space: pre-wrap; background: #cccccc; display: block; overflow-x: hidden; }
.md-fences { font-size: 0.9rem; position: relative; display: block; overflow: visible; white-space: pre; background: inherit; }
#write pre { white-space: pre-wrap; }
.md-task-list-item { position: relative; list-style-type: none; }
.md-task-list-item > input { position: absolute; top: 0; left: 0; margin-left: -1.2em; margin-top: calc(1em - 10px); border: none; }
.task-list-item.md-task-list-item { padding-left: 0; }
.md-toc { min-height: 3.58rem; position: relative; font-size: 0.9rem; border-radius: 10px; }
.md-toc-content { position: relative; margin-left: 0; }
.md-toc-item { display: block; color: #4183c4; }
.md-toc-inner { display: inline-block; cursor: pointer; }
.md-toc-h1 .md-toc-inner { margin-left: 0; font-weight: 700; }
.md-toc-h2 .md-toc-inner { margin-left: 2em; }
.md-toc-h3 .md-toc-inner { margin-left: 4em; }
.md-toc-h4 .md-toc-inner { margin-left: 6em; }
.md-toc-h5 .md-toc-inner { margin-left: 8em; }
.md-toc-h6 .md-toc-inner { margin-left: 10em; }
.md-alert { padding: 0 1em; margin-bottom: 16px; border-left: 0.25em solid #000000; }
.md-alert-note { border-left-color: #0969da; }
.md-alert-important { border-left-color: #8250df; }
.md-alert-warning { border-left-color: #9a6700; }
.md-alert-tip { border-left-color: #1f883d; }
.md-alert-caution { border-left-color: #cf222e; }
.md-alert-text { font-size: 0.9rem; font-weight: 700; }
::selection { background: var(--select-text-bg-color); }
</style>
<link rel="stylesheet" href="__THEME_CSS__">
</head>
<body>
__DIAGNOSTICS__
<div id="write">
__BODY__</div>
<script>
/* 宿主用它在不重新导航整个页面的前提下替换正文。
   整页导航会丢弃旧文档、重新解析 CSS、重新布局、重新触发字体加载，在首帧之前露出白底 ——
   那就是「编辑时预览会闪」的来源。只替换 #write 的内容则样式表、字体、滚动位置全都不动。 */
window.kbSetBody = function (html) {
  var write = document.getElementById('write');
  if (!write) return false;
  // 整段替换会连带重置滚动位置；先把当前位置记下来再恢复，否则每敲一个字预览都会跳回顶部。
  var scroller = document.scrollingElement || document.documentElement;
  var top = scroller ? scroller.scrollTop : 0;
  write.innerHTML = html;
  if (scroller) scroller.scrollTop = top;
  if (typeof window.kbReport === 'function') window.kbReport();
  return true;
};
</script>
</body>
</html>
""";

    /// <summary>
    /// 验证用的诊断栏。刻意放在 <c>#write</c> 之外，主题的正文样式不会污染它。
    /// 里面「映射层产出的 Typora 钩子」那几行是映射层对不对的直接证据。
    /// </summary>
    private const string DiagnosticsMarkup = """
<div id="kbdiag"
     style="position: sticky; top: 0; z-index: 9; margin: 0; padding: 8px 12px;
            font: 11.5px/1.55 Consolas, 'Cascadia Mono', monospace;
            background: #101418; color: #7ee787; white-space: pre-wrap;
            border-bottom: 1px solid #30363d;">诊断中…</div>
<script>
(function () {
  function pick(selector, prop) {
    var el = document.querySelector(selector);
    return el ? (getComputedStyle(el)[prop] || '') : 'n/a';
  }
  function count(selector) { return document.querySelectorAll(selector).length; }
  function firstFamily(selector) {
    var value = pick(selector, 'fontFamily') || '';
    var first = value.split(',')[0].replace(/["']/g, '').trim();
    return first || 'n/a';
  }
  function fontState(family) {
    try {
      if (!document.fonts || !document.fonts.check) return '没有 Font Loading API';
      return document.fonts.check('16px "' + family + '"') ? '✅ 可用' : '⚠️ 未加载（回退到后备字体）';
    } catch (e) {
      return 'error: ' + e.message;
    }
  }
  function report() {
    var sheets = [].map.call(document.styleSheets, function (s) { return s.href || '(inline)'; });
    var themeApplied = sheets.some(function (h) { return h && h.indexOf('.css') >= 0; });
    var family = firstFamily('body');
    var fonts = document.fonts ? (document.fonts.size + ' 个, ' + document.fonts.status) : 'n/a';
    var broken = [].filter.call(document.images, function (i) { return !i.complete || i.naturalWidth === 0; }).length;

    var lines = [
      '── 主题：__THEME_NAME__ ──',
      '用户代理     : ' + navigator.userAgent,
      '主题 CSS     : ' + (themeApplied ? '✅ 已进入样式表列表' : '⚠️ 列表里没有 .css') + '   ' + sheets.join('  |  '),
      '',
      '【映射层产出的 Typora 钩子 —— 这几个数就是映射层对不对的直接证据】',
      '  代码块     : .md-fences=' + count('.md-fences') + '  .md-lang=' + count('.md-lang'),
      '  任务列表   : .task-list=' + count('.task-list') + '  .md-task-list-item=' + count('.md-task-list-item'),
      '  目录       : .md-toc=' + count('.md-toc') + '  .md-toc-item=' + count('.md-toc-item'),
      '  表格/图片  : figure>table=' + count('figure > table') + '  .md-image=' + count('.md-image'),
      '  前言       : pre.md-meta-block=' + count('pre.md-meta-block'),
      '  块级元素   : h1..h6=' + count('h1,h2,h3,h4,h5,h6') + '  p=' + count('p') + '  li=' + count('li') + '  blockquote=' + count('blockquote'),
      '  图片加载   : 共 ' + document.images.length + ' 张，失败 ' + broken + ' 张',
      '',
      '【主题是否真的生效】',
      '  body 字体  : ' + pick('body', 'fontFamily'),
      '  Web 字体   : "' + family + '" → ' + fontState(family) + '   [document.fonts: ' + fonts + ']',
      '  #write     : max-width=' + pick('#write', 'maxWidth') + '  padding=' + pick('#write', 'padding'),
      '  h1 对照    : font-size=' + pick('h1', 'fontSize') + '  font-weight=' + pick('h1', 'fontWeight'),
      '  p 对照     : margin=' + pick('p', 'margin'),
      '  .md-fences : background=' + pick('.md-fences', 'backgroundColor') + '  margin=' + pick('.md-fences', 'margin'),
      '  code 对照  : background=' + pick('code', 'backgroundColor')
    ];
    document.getElementById('kbdiag').textContent = lines.join('\n');
  }
  // 宿主每次只替换正文，之后要重新跑一次诊断；所以把 report 挂到 window 上。
  window.kbReport = report;
  report();
  // 字体是异步加载的，等字体就绪后再刷一次，否则字体那一行永远是「未加载」。
  if (document.fonts && document.fonts.ready) document.fonts.ready.then(report);
})();
</script>
""";
}
