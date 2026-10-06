// ProseMirror schema —— 节点类型决定「渲染成什么元素」（Typora 契约），
// 节点的**文本内容**决定「源文件长什么样」。
//
// 两条不变量：
//   1. 任何块节点的文本内容 = 该块在源文件里的**原始字符**（连 `# `、`- `、围栏符、换行都在内）；
//   2. 序列化 = 深度优先把所有文本拼起来（见 serialize.mjs），**不做任何规范化**。
// 于是「打开 → 不改 → 保存」逐字节不变；只有用户敲进去的字符才会改变文件。
import { Schema } from 'prosemirror-model';

/** 围栏代码块：`pre.md-fences` + `lang` 是主题要的形态；语言名另外放一份在 `.md-lang` 里。 */
function fencedCodeToDOM(node) {
  const lang = node.attrs.lang;
  const pre = { class: 'md-fences md-end-block' };
  if (lang) pre.lang = lang;
  const children = [];
  if (lang) children.push(['div', { class: 'md-lang' }, lang]);
  children.push(['code', 0]);
  return ['pre', pre, ...children];
}

export const schema = new Schema({
  nodes: {
    doc: { content: 'block+' },

    // 正文段落。软换行就是文本里的 \n，和 Typora 一样按空白渲染。
    paragraph: {
      content: 'inline*',
      group: 'block',
      toDOM: () => ['p', 0]
    },

    heading: {
      attrs: { level: { default: 1 } },
      content: 'inline*',
      group: 'block',
      defining: true,
      toDOM: (node) => ['h' + node.attrs.level, 0]
    },

    // 围栏代码块（含缩进代码块，lang 为空）。
    // marks 只放行 syntax_marker：围栏那两行本身要作为「可隐藏的标记」留在内容里，
    // 这样渲染时能藏掉围栏符、只看见代码与 .md-lang 语言标签。
    code_block: {
      attrs: { lang: { default: '' } },
      content: 'text*',
      marks: 'syntax_marker',
      group: 'block',
      code: true,
      defining: true,
      toDOM: fencedCodeToDOM
    },

    blockquote: {
      content: 'block+',
      group: 'block',
      defining: true,
      toDOM: () => ['blockquote', 0]
    },

    bullet_list: {
      content: 'list_item+',
      group: 'block',
      toDOM: () => ['ul', 0]
    },

    ordered_list: {
      content: 'list_item+',
      group: 'block',
      toDOM: () => ['ol', 0]
    },

    list_item: {
      content: 'paragraph block*',
      defining: true,
      toDOM: () => ['li', 0]
    },

    // 水平线在 ProseMirror 里没有文本内容，所以把它的原文存在属性上，
    // 由序列化器原样吐出（`---` / `***` / `___`，含行尾空白）。
    horizontal_rule: {
      attrs: { text: { default: '' } },
      group: 'block',
      toDOM: () => ['hr']
    },

    text: { group: 'inline' }
  },

  marks: {
    strong: { toDOM: () => ['strong', 0] },
    em: { toDOM: () => ['em', 0] },
    code: { toDOM: () => ['code', 0] },
    link: {
      attrs: { href: { default: '' }, title: { default: '' } },
      inclusive: false,
      toDOM: (mark) => ['a', { href: mark.attrs.href, title: mark.attrs.title }, 0]
    },
    // 语法符号（`**`、`# `、`- `、围栏……）作为**真实文本**留在文档里，只打上这个标记；
    // 渲染层据此在光标离开时把它们藏起来（font-size: 0），而不是删掉它们。
    // 这样文件里一个字符都不会少。
    syntax_marker: {
      attrs: { kind: { default: '' } },
      toDOM: (mark) => ['span', { class: 'md-marker', 'data-kind': mark.attrs.kind }, 0]
    }
  }
});
