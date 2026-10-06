// KBManager 预览/编辑面 —— C1 切片一：只验证「就地编辑能不能成立」。
//
// 这一片刻意什么都不做全：
//   · 不实现语法符号的隐显（Milkup 那套 syntax_marker 设计是下一片的事）
//   · 不写盘（WebView 里的编辑不碰笔记文件，只用来判断手感）
//   · 不处理表格 / YAML 前言 / 公式（markdown-it 默认不认识，下一片补 parser spec）
//
// 它要回答三个问题：
//   ① ProseMirror 能不能在 WebView 里跑起来（classic script + file:// 加载）
//   ② Typora 主题能不能作用于它的 DOM —— 这里的关键是：ProseMirror 的 mark 会渲染成
//      真实元素（<strong>/<em>/<code>），所以主题的 #write strong 这类规则照样命中。
//      这正是选 ProseMirror 而不是 CodeMirror 6 的理由（CM6 的行式 DOM 命中不了）。
//   ③ 中文输入法手感。
//
// 整个切片一只用 prosemirror-markdown 导出的那一个 schema 实例：它的 defaultMarkdownParser
// 产出的是该实例的节点，如果另建一个 schema 会直接抛 "Cannot use node from a different schema"。
import { EditorState } from 'prosemirror-state';
import { EditorView } from 'prosemirror-view';
import { exampleSetup } from 'prosemirror-example-setup';
import { schema, defaultMarkdownParser, defaultMarkdownSerializer } from 'prosemirror-markdown';

let view = null;
let original = '';

function count(selector) {
  const host = document.getElementById('write');
  return host ? host.querySelectorAll(selector).length : -1;
}

// ProseMirror 实际产出的 DOM 契约：主题能不能生效，全看这几项。
function domContract() {
  return 'h1..h6=' + count('h1,h2,h3,h4,h5,h6') +
         '  p=' + count('p') +
         '  strong=' + count('strong') +
         '  em=' + count('em') +
         '  code=' + count('code') +
         '  blockquote=' + count('blockquote') +
         '  li=' + count('li') +
         '  pre=' + count('pre') +
         '  【主题要的 pre.md-fences=' + count('pre.md-fences') + '】';
}

function firstDifference(a, b) {
  const left = a.split('\n');
  const right = b.split('\n');
  for (let i = 0; i < Math.max(left.length, right.length); i++) {
    if (left[i] !== right[i]) {
      return '第 ' + (i + 1) + ' 行  ' + JSON.stringify(left[i] ?? null) + '  →  ' + JSON.stringify(right[i] ?? null);
    }
  }
  return '无';
}

// 往返保真：把当前文档序列化回 Markdown，和原始文本逐行比。
// 切片一用的是 prosemirror-markdown 的默认序列化器，它**会规范化 Markdown** ——
// 这里把差异量出来，正好说明为什么正式实现需要 syntax_marker 那套设计。
function roundTrip() {
  if (!view) return { identical: false, detail: '未挂载' };
  let text;
  try {
    text = defaultMarkdownSerializer.serialize(view.state.doc);
  } catch (e) {
    return { identical: false, detail: 'error: ' + e.message };
  }
  const a = original.replace(/\s+$/, '');
  const b = text.replace(/\s+$/, '');
  return { identical: a === b, detail: a === b ? '无' : firstDifference(a, b) };
}

function updateInfo(error) {
  const info = { mounted: false, error: error || '' };
  if (view) {
    let blocks = 0;
    let chars = 0;
    view.state.doc.descendants(function (node) {
      if (node.isTextblock) {
        blocks++;
        chars += node.textContent.length;
      }
    });
    const trip = roundTrip();
    info.mounted = true;
    info.blocks = blocks;
    info.chars = chars;
    info.contract = domContract();
    info.roundTripIdentical = trip.identical;
    info.roundTripDetail = trip.detail;
  }
  window.kbEditorInfo = info;
  if (typeof window.kbReport === 'function') window.kbReport();
}

function mount(markdown) {
  const host = document.getElementById('kbEditor');
  if (!host) { updateInfo('页面里没有 #kbEditor'); return false; }
  if (view) { view.destroy(); view = null; }

  original = markdown || '';
  try {
    const doc = defaultMarkdownParser.parse(original);
    view = new EditorView(host, {
      state: EditorState.create({ doc, plugins: exampleSetup({ schema, menuBar: false }) }),
      // 自己接管事务，才能在每次编辑后刷新诊断。
      dispatchTransaction(tr) {
        view.updateState(view.state.apply(tr));
        updateInfo();
      }
    });
  } catch (e) {
    view = null;
    updateInfo(e.name + ': ' + e.message);
    return false;
  }
  updateInfo();
  return true;
}

window.kbEditor = {
  mount,
  destroy() {
    if (view) { view.destroy(); view = null; }
    window.kbEditorInfo = { mounted: false, error: '已卸载' };
  },
  // 将来 C# 通过 InvokeScript 取回正文（切片一还不写盘）。
  markdown() {
    return view ? defaultMarkdownSerializer.serialize(view.state.doc) : '';
  }
};
