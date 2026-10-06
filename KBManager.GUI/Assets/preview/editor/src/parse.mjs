// 源文本 → ProseMirror 文档。块级（行内标记下一轮加）。
//
// ══ 唯一的目标：一个字符都不能丢，一个字符都不能加 ══
// 每个块节点的文本内容 = 该块在源文件里的**原始字符**，包括结构性符号（`# `、`- `、`> `、
// 围栏符、缩进、行尾空白、换行）。因此 serialize(parse(x)) === x 是**结构上**成立的。
//
// 解析器"认不认识"某个构造只影响两件事：渲染好不好看、编辑时结构对不对；
// **它不影响保真** —— 不认识的构造落进普通段落，原文照旧。（这不是理论：块识别曾经因为
// 一个正则写错而整体失效，当时往返测试仍然是 81/81 全绿，文件一个字符都没变。）
//
// ══ 换行符的处理 ══
// splitLines 保留换行符（这样 CRLF / 结尾无换行都能原样还原），所以**分类用的正则必须
// 跑在去掉行尾换行的 body 上**：JS 的 `$` 不匹配 `\n` 之前的位置，`/^#+.*$/` 这类
// 模式永远匹配不上带换行符的行。文本一律取原行（lines[i]），分类一律取 bodies[i]。
import { schema } from './schema.mjs';
import { parseInline } from './inline.mjs';

const FENCE_OPEN = /^ {0,3}(`{3,}|~{3,})(.*)$/;
const FENCE_CLOSE = /^ {0,3}(`{3,}|~{3,})[ \t]*$/;
const ATX = /^ {0,3}(#{1,6})(?:[ \t]+(.*?))?[ \t]*$/;
const SETEXT = /^ {0,3}(=+|-+)[ \t]*$/;
const HR = /^ {0,3}(?:(?:\*[ \t]*){3,}|(?:-[ \t]*){3,}|(?:_[ \t]*){3,})$/;
const BULLET = /^( {0,3})([-*+])[ \t]+/;
const BULLET_EMPTY = /^( {0,3})([-*+])[ \t]*$/;
const ORDERED = /^( {0,3})(\d{1,9})([.)])[ \t]+/;
const QUOTE = /^ {0,3}>/;
const BLANK = /^[ \t]*$/;
const EOL = /(?:\r\n|\n|\r)$/;

/** 按行切分，**连换行符一起保留**：CRLF、混合换行、结尾无换行都能原样还原。 */
export function splitLines(text) {
  const lines = [];
  let start = 0;
  for (let i = 0; i < text.length; i++) {
    if (text[i] === '\n') {
      lines.push(text.slice(start, i + 1));
      start = i + 1;
    }
  }
  if (start < text.length) lines.push(text.slice(start));
  return lines;
}

/** 去掉行尾换行，专供分类正则使用。 */
export function stripEol(line) {
  return line.replace(EOL, '');
}

/** 只含空白（或空串）的行算空行 —— 行内的空格 / Tab 由原行保留。 */
export function isBlank(body) {
  return BLANK.test(body);
}

function bulletMatch(body) {
  return BULLET.exec(body) || BULLET_EMPTY.exec(body);
}

function closesFence(body, char, length) {
  const m = FENCE_CLOSE.exec(body);
  return !!m && m[1][0] === char && m[1].length >= length;
}

/** 该行会不会打断一个正在收集的段落。 */
function interruptsParagraph(body) {
  return (
    isBlank(body) ||
    FENCE_OPEN.test(body) ||
    ATX.test(body) ||
    HR.test(body) ||
    SETEXT.test(body) ||
    !!bulletMatch(body) ||
    ORDERED.test(body) ||
    QUOTE.test(body)
  );
}

function isIndented(body, indent) {
  const m = /^( *)/.exec(body);
  return m[1].length > indent;
}

/** 带 syntax_marker 标记的文本节点：符号本身，渲染层据此把它藏起来（而不是删掉）。 */
function markerNode(kind, text) {
  return schema.text(text, [schema.marks.syntax_marker.create({ kind })]);
}

/**
 * 把块开头的结构符号（`- `、`> `、`# `）切出来单独作为标记节点，其余走行内解析。
 * 切分不改变拼接结果，所以对保真没有影响。
 */
function inlineWithLeadMarker(verbatim, leadRe, kind) {
  if (!verbatim.length) return [];
  const m = leadRe.exec(verbatim);
  if (!m || !m[0].length) return parseInline(verbatim);
  const head = m[0];
  return [markerNode(kind, head), ...parseInline(verbatim.slice(head.length))];
}

/**
 * 段落内容。ProseMirror 不允许空文本节点，所以要判空。
 * 文本走行内解析：把 `**`、`` ` ``、链接等切成「符号 + 带标记的内容」，
 * 这样渲染层既能把内容渲染成 <strong> 等真实元素，又能把符号藏起来。
 */
function paragraphOf(verbatim) {
  return verbatim.length ? schema.node('paragraph', null, parseInline(verbatim)) : schema.node('paragraph');
}

/** 列表项：把 `- ` / `1. ` 这类标记切出来。 */
function listItemOf(verbatim) {
  return schema.node(
    'list_item',
    null,
    [schema.node('paragraph', null, inlineWithLeadMarker(verbatim, /^ {0,3}(?:[-*+]|\d{1,9}[.)])[ \t]+/, 'list'))]
  );
}

/** 引用：把开头的 `> ` 切出来（多行引用的其余前缀留在行内文本里）。 */
function quoteOf(verbatim) {
  return schema.node(
    'blockquote',
    null,
    [schema.node('paragraph', null, inlineWithLeadMarker(verbatim, /^ {0,3}>[ \t]?/, 'quote'))]
  );
}

/** 标题：ATX 的 `# ` 在开头，Setext 的 `===` / `---` 在最后一行 —— 两种都要切成标记。 */
function headingOf(level, verbatim) {
  if (!verbatim.length) return schema.node('heading', { level }, []);

  const atx = /^ {0,3}(#{1,6}[ \t]+)/.exec(verbatim);
  if (atx) {
    return schema.node('heading', { level }, [
      markerNode('heading', atx[1]),
      ...parseInline(verbatim.slice(atx[1].length))
    ]);
  }

  // Setext：最后一行是下划线。它必须原样留在文档里（只是被藏起来），否则文件会少一行。
  const trimmed = verbatim.replace(/\n$/, '');
  const lastNl = trimmed.lastIndexOf('\n');
  if (lastNl < 0) return schema.node('heading', { level }, parseInline(verbatim));
  const content = verbatim.slice(0, lastNl + 1);
  const underline = verbatim.slice(lastNl + 1);
  return schema.node('heading', { level }, [...parseInline(content), markerNode('setext', underline)]);
}

/** 围栏代码块：开启行与闭合行切成标记，中间是代码原文。 */
function codeBlockOf(verbatim, lang, closed) {
  const openEnd = verbatim.indexOf('\n');
  const firstLine = openEnd < 0 ? verbatim : verbatim.slice(0, openEnd + 1);
  let rest = verbatim.slice(firstLine.length);
  let closeLine = '';

  if (closed && rest.length) {
    const trimmed = rest.replace(/\n$/, '');
    const lastNl = trimmed.lastIndexOf('\n');
    const cut = lastNl < 0 ? 0 : lastNl + 1;
    closeLine = rest.slice(cut);
    rest = rest.slice(0, cut);
  }

  const content = [markerNode('fence', firstLine)];
  if (rest.length) content.push(schema.text(rest));
  if (closeLine.length) content.push(markerNode('fence', closeLine));
  return schema.node('code_block', { lang }, content);
}

export function parseDoc(text) {
  const src = typeof text === 'string' ? text : '';
  const lines = splitLines(src);
  const bodies = lines.map(stripEol);
  const blocks = [];
  const slice = (from, to) => lines.slice(from, to).join('');

  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    const body = bodies[i];

    // 空行：连续空行并成一个段落，每行的空格 / Tab 都在原文里。
    if (isBlank(body)) {
      const start = i;
      while (i < lines.length && isBlank(bodies[i])) i++;
      blocks.push(paragraphOf(slice(start, i)));
      continue;
    }

    // 围栏代码块：代码里的空行不能把块切开；没闭合就一直吃到文件末尾。
    const fence = FENCE_OPEN.exec(body);
    if (fence) {
      const char = fence[1][0];
      const length = fence[1].length;
      const lang = (fence[2] || '').trim().split(/\s+/)[0] || '';
      const start = i;
      i++;
      while (i < lines.length && !closesFence(bodies[i], char, length)) i++;
      const closed = i < lines.length;
      if (closed) i++;
      blocks.push(codeBlockOf(slice(start, i), lang, closed));
      continue;
    }

    // ATX 标题：整行（含 `#` 与行尾空白）原样进文本。
    const atx = ATX.exec(body);
    if (atx) {
      blocks.push(headingOf(atx[1].length, line));
      i++;
      continue;
    }

    // 引用：连续 `>` 行并成一个引用块（嵌套引用留在同一段文本里）。
    if (QUOTE.test(body)) {
      const start = i;
      while (i < lines.length && QUOTE.test(bodies[i])) i++;
      blocks.push(quoteOf(slice(start, i)));
      continue;
    }

    // 列表：同层缩进的连续项合成一个列表；项的文本从它的标记一直到下一项之前（含续行）。
    const bullet = bulletMatch(body);
    const ordered = bullet ? null : ORDERED.exec(body);
    if (bullet || ordered) {
      const isOrdered = !bullet;
      const indent = (bullet ? bullet[1] : ordered[1]).length;
      const items = [];
      while (i < lines.length) {
        const m = isOrdered ? ORDERED.exec(bodies[i]) : bulletMatch(bodies[i]);
        if (!m || m[1].length !== indent) break;
        const start = i;
        i++;
        while (i < lines.length) {
          if (isBlank(bodies[i])) {
            // 空行后面还缩进 → 仍属于本项；否则本项结束，空行留给外层（位置不变）。
            if (i + 1 < lines.length && isIndented(bodies[i + 1], indent)) {
              i += 2;
              continue;
            }
            break;
          }
          const same = isOrdered ? ORDERED.exec(bodies[i]) : bulletMatch(bodies[i]);
          if (same && same[1].length === indent) break;
          if (isIndented(bodies[i], indent)) {
            i++;
            continue;
          }
          break;
        }
        items.push(listItemOf(slice(start, i)));
      }
      blocks.push(schema.node(isOrdered ? 'ordered_list' : 'bullet_list', null, items));
      continue;
    }

    // 水平线。注意 `---` 有两种身份：紧跟段落行时是 Setext 下划线（下面的段落分支已经把它
    // 吃掉了），其余位置（文件开头、空行之后）才是水平线。所以这里不需要再排除 SETEXT。
    if (HR.test(body)) {
      blocks.push(schema.node('horizontal_rule', { text: line }));
      i++;
      continue;
    }

    // 普通段落：连续收集到遇到会打断它的行为止。
    {
      const start = i;
      while (i < lines.length && !interruptsParagraph(bodies[i])) i++;
      if (i === start) i++; // 兜底：绝不死循环

      // Setext 标题：段落紧跟 `===` / `---` 时整段升级为标题（下划线行一起进文本）。
      if (i < lines.length) {
        const setext = SETEXT.exec(bodies[i]);
        if (setext) {
          const level = setext[1][0] === '=' ? 1 : 2;
          i++;
          blocks.push(headingOf(level, slice(start, i)));
          continue;
        }
      }

      blocks.push(paragraphOf(slice(start, i)));
    }
  }

  // 空文件也要是一个合法文档（doc 的 content 是 block+）。
  if (blocks.length === 0) blocks.push(schema.node('paragraph'));

  return schema.node('doc', null, blocks);
}
