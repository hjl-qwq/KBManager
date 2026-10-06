// KBManager 就地编辑面 —— C1：保真模型 + Typora 契约渲染。
//
// 与上一版的根本区别：文档不再由 prosemirror-markdown 解析。这里用的是我们自己的
// schema / parse / serialize（见 schema.mjs / parse.mjs / serialize.mjs），它们保证
// **源文件的每一个字符都在文档里**（`**`、`# `、`- `、围栏符、缩进、行尾空白、空行），
// 因此「打开 → 不改 → 写回」逐字节不变。诊断栏里的「往返保真」就是这个断言在应用里的实测。
//
// 语法符号是真实文本节点，只打了 syntax_marker 标记：
//   · 光标不在那个块里 → CSS 把标记缩成 0 号字（藏起来，但光标仍能走进去）
//   · 光标在那个块里   → 标记显形，可以直接改（和 Typora 一样）
import { EditorState, Plugin, PluginKey } from 'prosemirror-state';
import { EditorView, Decoration, DecorationSet } from 'prosemirror-view';
import { keymap } from 'prosemirror-keymap';
import { baseKeymap } from 'prosemirror-commands';
import { history, undo, redo } from 'prosemirror-history';
import { schema } from './schema.mjs';
import { parseDoc } from './parse.mjs';
import { serializeDoc } from './serialize.mjs';
import { kbInputRules } from './input-rules.mjs';

let view = null;
let original = '';

const activeBlockKey = new PluginKey('kbActiveBlock');

/**
 * 给「光标所在的那个块」加一个 md-active class。
 * 只做这一个判断，而不是逐字符算该藏哪一段：把显隐交给 CSS（.md-marker 与 .md-active .md-marker），
 * 逻辑就只是"光标在哪个块"，位置算术少得多、也不容易错。
 */
function activeBlockPlugin() {
  return new Plugin({
    key: activeBlockKey,
    props: {
      decorations(state) {
        const $from = state.selection.$from;
        for (let depth = $from.depth; depth > 0; depth--) {
          const node = $from.node(depth);
          if (!node || !node.isTextblock) continue;
          const start = $from.before(depth);
          return DecorationSet.create(state.doc, [
            Decoration.node(start, start + node.nodeSize, { class: 'md-active' })
          ]);
        }
        return DecorationSet.empty;
      }
    }
  });
}

/** 源侧最外层块的类型统计，用于诊断栏。 */
function blockKinds(doc) {
  const counts = {};
  doc.forEach((node) => {
    counts[node.type.name] = (counts[node.type.name] || 0) + 1;
  });
  return counts;
}

function updateInfo(error) {
  const info = { mounted: false, error: error || '' };
  if (view) {
    let blocks = 0;
    let chars = 0;
    view.state.doc.descendants((node) => {
      if (node.isTextblock) {
        blocks++;
        chars += node.textContent.length;
      }
    });

    // 保真实测：把当前文档序列化回 Markdown，和打开时的原文逐字节比。
    // 用户没有改动时它必须是"一致" —— 这是"文件不会因为用了编辑器而变化"在应用里的证明。
    let roundTripIdentical = false;
    let roundTripDetail = '';
    try {
      const text = serializeDoc(view.state.doc);
      if (text === original) {
        roundTripIdentical = true;
        roundTripDetail = '与打开时逐字节一致';
      } else {
        // 找出第一处差异，方便判断是不是真的被编辑器改动了
        const n = Math.min(text.length, original.length);
        let at = -1;
        for (let i = 0; i < n; i++) {
          if (text[i] !== original[i]) {
            at = i;
            break;
          }
        }
        if (at < 0) at = n;
        let line = 1;
        for (let i = 0; i < at && i < original.length; i++) if (original[i] === '\n') line++;
        const a = original.split('\n')[line - 1] ?? '(不存在)';
        const b = text.split('\n')[line - 1] ?? '(不存在)';
        roundTripDetail =
          '原文 ' + original.length + ' 字符 / 回写 ' + text.length + ' 字符，首个差异在第 ' + line + ' 行';
        if (a !== b) roundTripDetail += `：${JSON.stringify(a.slice(0, 60))} → ${JSON.stringify(b.slice(0, 60))}`;
      }
    } catch (e) {
      roundTripDetail = 'error: ' + e.message;
    }

    const kinds = blockKinds(view.state.doc);
    info.mounted = true;
    info.blocks = blocks;
    info.chars = chars;
    info.roundTripIdentical = roundTripIdentical;
    info.roundTripDetail = roundTripDetail;
    info.contract =
      'h1..h6=' + (document.querySelectorAll('#write h1,#write h2,#write h3,#write h4,#write h5,#write h6').length) +
      '  p=' + document.querySelectorAll('#write p').length +
      '  strong=' + document.querySelectorAll('#write strong').length +
      '  em=' + document.querySelectorAll('#write em').length +
      '  code=' + document.querySelectorAll('#write code').length +
      '  blockquote=' + document.querySelectorAll('#write blockquote').length +
      '  li=' + document.querySelectorAll('#write li').length +
      '  pre.md-fences=' + document.querySelectorAll('#write pre.md-fences').length;
    info.kinds = Object.entries(kinds)
      .sort((a, b) => b[1] - a[1])
      .map(([k, v]) => `${k}=${v}`)
      .join('  ');
    info.markers = document.querySelectorAll('#write .md-marker').length;
  }
  window.kbEditorInfo = info;
  if (typeof window.kbReport === 'function') window.kbReport();
}

function mount(markdown) {
  const host = document.getElementById('kbEditor');
  if (!host) {
    updateInfo('页面里没有 #kbEditor');
    return false;
  }
  if (view) {
    view.destroy();
    view = null;
  }

  original = markdown || '';
  try {
    const doc = parseDoc(original);
    view = new EditorView(host, {
      state: EditorState.create({
        doc,
        plugins: [
          // 输入规则放在最前：敲完 `**粗体**` 的最后一个 `*` 时立刻变成真正的 <strong>
          // （规则内部有"字符必须完全一致"的保护，见 input-rules.mjs）。
          kbInputRules(),
          history(),
          keymap({ 'Mod-z': undo, 'Mod-y': redo, 'Mod-Shift-z': redo }),
          keymap(baseKeymap),
          activeBlockPlugin()
        ]
      }),
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
    if (view) {
      view.destroy();
      view = null;
    }
    window.kbEditorInfo = { mounted: false, error: '已卸载' };
  },
  // 将来 C# 通过 InvokeScript 取回正文（当前切片还不写盘）。
  markdown() {
    return view ? serializeDoc(view.state.doc) : '';
  }
};
