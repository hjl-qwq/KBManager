// 行内输入规则：敲完 `**粗体**` 的最后一个 `*` 时，内容立刻变成真正的 <strong>。
//
// 自己实现而不用 prosemirror-inputrules：只需要 handleTextInput 这一个钩子，
// 与其为此多背一个依赖，不如写清楚这 60 行。
//
// ══ 结构上不可能污染文件 ══
// 输入规则是整个链上**唯一**会改写用户字符的地方（解析/序列化都是纯函数，且往返闸门
// 81/81 盯着它）。所以这里不靠"小心写正则"：写回去的文本节点拼接结果必须与它替换掉的
// 原文**逐字节相同**，否则直接放弃（返回 false，让 PM 走默认插入）。
// 最坏情况只是"没转成粗体"，不可能是"少字/多字/串位"。
import { Plugin, PluginKey } from 'prosemirror-state';
import { schema } from './schema.mjs';

const inputRulesKey = new PluginKey('kbInputRules');

function markerText(kind, text) {
  return schema.text(text, [schema.marks.syntax_marker.create({ kind })]);
}

/**
 * 内容模式：内容两侧都不能贴空白，而且**不能包含定界符本身**。
 *
 * 两条都是实测逼出来的：
 *   · 缺"两侧不能贴空白" → `2 * 3 * 4` / `a * b * c` 被误判成斜体；
 *   · 缺"不能包含定界符"（只写 `\S` 是不够的，因为 `*` 也满足 `\S`）
 *     → `** 空格 **` 里斜体规则会把 `*` 当成内容吃掉。
 */
function contentPattern(delim) {
  const x = `[^${delim}\\s]`; // 非空白、且不是定界符
  return `((?:${x}[^${delim}\\n]*${x}|${x}))`;
}

/**
 * 规则表。单字符定界符（`*` / `_`）前面要求行首或空白，否则 `2*3*4` 会被吃掉；
 * 那个被多匹配的前导字符会原样放回去，所以不会吞掉用户的空格。
 * 代码 span 不套"两侧不能贴空白"：`` ` x ` `` 里的前后空格是合法的。
 */
const RULES = [
  {
    re: new RegExp(`\\*\\*${contentPattern('*')}\\*\\*$`),
    kind: 'strong',
    delim: '**',
    mark: () => schema.marks.strong.create()
  },
  {
    re: new RegExp(`__${contentPattern('_')}__$`),
    kind: 'strong',
    delim: '__',
    mark: () => schema.marks.strong.create()
  },
  {
    re: new RegExp(`(?:^|\\s)\\*${contentPattern('*')}\\*$`),
    kind: 'em',
    delim: '*',
    mark: () => schema.marks.em.create()
  },
  {
    re: new RegExp(`(?:^|\\s)_${contentPattern('_')}_$`),
    kind: 'em',
    delim: '_',
    mark: () => schema.marks.em.create()
  },
  { re: /`([^`\n]+)`$/, kind: 'code', delim: '`', mark: () => schema.marks.code.create() }
];

/**
 * 试着把「光标前已有的文本 + 刚要插入的字符」中，结尾处的一段行内语法重新标记一遍。
 * 命中则返回要替换的区间与节点；否则返回 null。
 *
 * 注意 handleTextInput 是在字符**插入之前**调用的，所以候选文本要自己把 text 拼上，
 * 而替换区间只覆盖文档里已经存在的那部分。
 */
export function matchInputRule(state, from, to, text) {
  if (!text || from !== to) return null;

  const $from = state.doc.resolve(from);
  if (!$from.parent || !$from.parent.isTextblock) return null;

  const blockStart = $from.start();
  const before = state.doc.textBetween(blockStart, from, '');
  const candidate = before + text;

  for (const rule of RULES) {
    const m = rule.re.exec(candidate);
    if (!m) continue;

    const content = m[1];
    if (!content) continue;

    const matchStart = candidate.length - m[0].length;
    if (matchStart < 0) continue;

    const leadLen = m[0].length - rule.delim.length * 2 - content.length;
    const leading = leadLen > 0 ? m[0].slice(0, leadLen) : '';

    const nodes = [
      ...(leading ? [schema.text(leading)] : []),
      markerText(rule.kind, rule.delim),
      schema.text(content, [rule.mark()]),
      markerText(rule.kind, rule.delim)
    ];

    // 唯一的放行条件：写回去的字符和吃掉的那段完全一样。
    if (nodes.map((node) => node.text).join('') !== m[0]) continue;

    return { from: blockStart + matchStart, to: from, nodes };
  }

  return null;
}

export function kbInputRules() {
  return new Plugin({
    key: inputRulesKey,
    props: {
      handleTextInput(view, from, to, text) {
        const hit = matchInputRule(view.state, from, to, text);
        if (!hit) return false;
        view.dispatch(view.state.tr.replaceWith(hit.from, hit.to, hit.nodes));
        return true; // 已经处理，PM 不要再做默认插入
      }
    }
  });
}
