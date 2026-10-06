// 行内解析：把一个块的原始文本切成「普通文本 + 语法符号 + 带标记的内容」。
//
// ══ 为什么切分不影响保真 ══
// 序列化是所有文本节点按顺序拼接，所以**怎么切分都不改变结果**。这个模块无论写得多保守、
// 漏认多少构造，都动不了文件的一个字节 —— 它只决定"渲染得好不好看"。
// 因此这里的策略是**极度保守**：拿不准就当成普通文本（原样显示），绝不猜。
//
// 本期只认：代码 span、行内链接、强调（`**`/`__` 与 `*`/`_`）。
// 图片、行内 HTML、删除线、数学公式等一律当普通文本（原样显示，不给假样式）。
//
// 每个语法的**符号本身**（`**`、`` ` ``、`[`、`](url)`）都作为带 syntax_marker 标记的
// 文本节点留在文档里 —— 渲染层据此在光标离开时把它们藏起来，而不是删掉。
import { schema } from './schema.mjs';

function runLength(text, start, ch) {
  let n = 0;
  while (start + n < text.length && text[start + n] === ch) n++;
  return n;
}

function isSpace(ch) {
  return ch === undefined || /\s/.test(ch);
}

/** `_` 不做词内强调（避免 my_var_name 被吃掉），`*` 不受此限。 */
function isValidDelimiter(text, openAt, closeAt, ch, len) {
  const before = text[openAt - 1];
  const afterOpen = text[openAt + len];
  const beforeClose = text[closeAt - 1];
  const afterClose = text[closeAt + len];

  // 开符号后面不能是空白，闭符号前面不能是空白 —— 这条能挡掉 `2 * 3 * 4` 这类误判。
  if (isSpace(afterOpen) || isSpace(beforeClose)) return false;
  if (ch === '_') {
    if (before !== undefined && /[\w]/.test(before)) return false;
    if (afterClose !== undefined && /[\w]/.test(afterClose)) return false;
  }
  return true;
}

/** 找到与 openAt 处定界符配对的闭符号下标；找不到返回 -1。 */
function findCloser(text, openAt, ch, len) {
  const delim = ch.repeat(len);
  let at = openAt + len;
  while (at < text.length) {
    const found = text.indexOf(delim, at);
    if (found < 0) return -1;
    if (text[found + len] === ch && len === 1) {
      // 单个 `*` 遇到 `**` 时不要错配
      at = found + 1;
      continue;
    }
    if (isValidDelimiter(text, openAt, found, ch, len)) return found;
    at = found + len;
  }
  return -1;
}

/** 给一批行内节点统一加上某个 mark（链接内容要渲染成 <a>）。 */
function withMark(nodes, mark) {
  return nodes.map((node) => (node.isText ? schema.text(node.text, [...node.marks, mark]) : node));
}

function marker(kind, text) {
  return schema.text(text, [schema.marks.syntax_marker.create({ kind })]);
}

/**
 * 原始文本 → 行内节点数组。
 * 任何未识别的部分都原样成为普通文本节点，因此拼接结果恒等于输入。
 */
export function parseInline(text) {
  const out = [];
  let buffer = '';
  const flush = () => {
    if (buffer.length) {
      out.push(schema.text(buffer));
      buffer = '';
    }
  };

  let i = 0;
  while (i < text.length) {
    const ch = text[i];

    // ── 代码 span：`code` / ``code`` ──
    if (ch === '`') {
      const len = runLength(text, i, '`');
      const closer = findCloser(text, i, '`', len);
      if (closer > i + len) {
        flush();
        const tick = '`'.repeat(len);
        out.push(marker('code', tick));
        const inner = text.slice(i + len, closer);
        out.push(schema.text(inner, [schema.marks.code.create()]));
        out.push(marker('code', tick));
        i = closer + len;
        continue;
      }
    }

    // ── 行内链接：[文字](地址) ──
    if (ch === '[') {
      const closeBracket = text.indexOf('](', i + 1);
      if (closeBracket > i) {
        const closeParen = text.indexOf(')', closeBracket + 2);
        if (closeParen > closeBracket) {
          const label = text.slice(i + 1, closeBracket);
          const href = text.slice(closeBracket + 2, closeParen);
          if (label.length && !label.includes('[')) {
            flush();
            const link = schema.marks.link.create({ href, title: '' });
            out.push(marker('link', '['));
            out.push(...withMark(parseInline(label), link));
            out.push(marker('link', text.slice(closeBracket, closeParen + 1)));
            i = closeParen + 1;
            continue;
          }
        }
      }
    }

    // ── 强调：`**`/`__` → strong，`*`/`_` → em ──
    if (ch === '*' || ch === '_') {
      const run = runLength(text, i, ch);
      const len = run >= 2 ? 2 : 1;
      const closer = findCloser(text, i, ch, len);
      if (closer > i + len) {
        flush();
        const kind = len === 2 ? 'strong' : 'em';
        const delim = ch.repeat(len);
        const mark = len === 2 ? schema.marks.strong.create() : schema.marks.em.create();
        out.push(marker(kind, delim));
        out.push(...withMark(parseInline(text.slice(i + len, closer)), mark));
        out.push(marker(kind, delim));
        i = closer + len;
        continue;
      }
    }

    buffer += ch;
    i++;
  }

  flush();
  return out;
}
