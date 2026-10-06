// 往返保真闸门：把一批 .md 逐个跑「parseDoc → serializeDoc」，比对是否逐字节一致。
//
//   node tools/roundtrip.mjs /mnt/c/Users/20806/Documents/hjl-qwq-knowledge-private
//
// 这是 C1 最重要的一道自动化约束：用户的三条要求里最硬的一条是
// 「我的文件不应该因为用了这个编辑器而发生内容格式上的变化」，而这条可以完全自动验证。
//
// 只读文件，不写任何东西，不启动 GUI / WebView，不碰 git。
// 退出码 0 = 全部逐字节一致；1 = 有差异（打印行号与两边内容）。
//
// 用法：node tools/roundtrip.mjs <目录> [--show N] [--all]

import fs from 'node:fs';
import path from 'node:path';
import { parseDoc } from '../src/parse.mjs';
import { serializeDoc, countNodes } from '../src/serialize.mjs';

const SKIP_DIRS = new Set(['.git', '.kbdatabase', 'node_modules', '.obsidian', '.trash']);

function collectMarkdown(root) {
  const out = [];
  const walk = (dir) => {
    let entries;
    try {
      entries = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
      return;
    }
    for (const e of entries) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) {
        if (!SKIP_DIRS.has(e.name)) walk(p);
      } else if (e.isFile() && /\.(md|markdown|mdown|mkd|mkdn|mdwn)$/i.test(e.name)) {
        out.push(p);
      }
    }
  };
  walk(root);
  return out.sort();
}

function firstDifference(a, b) {
  const n = Math.min(a.length, b.length);
  for (let i = 0; i < n; i++) if (a[i] !== b[i]) return i;
  return a.length === b.length ? -1 : n;
}

function lineNumberAt(text, index) {
  let line = 1;
  for (let i = 0; i < index && i < text.length; i++) if (text[i] === '\n') line++;
  return line;
}

function lineAt(text, line) {
  const l = text.split('\n')[line - 1];
  return l === undefined ? '(不存在)' : JSON.stringify(l.slice(0, 140));
}

const args = process.argv.slice(2);
// 目录必须是第一个参数：否则 `--show 5 <目录>` 里的 "5" 会被当成目录。
const root = args[0] && !args[0].startsWith('--') ? args[0] : null;
if (!root) {
  console.error('用法: node tools/roundtrip.mjs <笔记目录> [--show N] [--all]');
  process.exit(2);
}
const showArg = args.indexOf('--show');
const showCount = showArg >= 0 ? Number(args[showArg + 1] || 5) : 5;
const showAll = args.includes('--all');

const files = collectMarkdown(root);
if (files.length === 0) {
  console.error('没找到 .md 文件:', root);
  process.exit(2);
}

let identical = 0;
let totalBytes = 0;
const failures = [];
const nodeTotals = {};
let crashed = 0;

// 结构识别自检用的源侧计数。
// 为什么需要：往返测试**按设计**发现不了解析器"什么都没认出来" —— 不认识的构造会原样
// 落进文本块，保真照样成立（块识别的正则曾经整体失效，当时往返仍是 81/81 全绿）。
// 所以必须单独盯住"识别率"，否则解析器可以静默退化成一个纯文本搬运工。
//
// 计数本身必须是"上下文感知"的，否则会误报：代码块里的 `#` 是注释不是标题，
// 围栏里的 ``` 也不是新的围栏。所以这里跟着围栏状态机走，并跳过引用行
// （引用里的构造由引用分支整段吃掉，本来就不会单独成节点）。
const ATX_LINE = /^ {0,3}#{1,6}([ \t]|$)/;
const FENCE_OPEN_LINE = /^ {0,3}(`{3,}|~{3,})(.*)$/;
const FENCE_CLOSE_LINE = /^ {0,3}(`{3,}|~{3,})[ \t]*$/;
let atxLines = 0;
let fenceOpeners = 0;

function countExpectations(source) {
  let inFence = false;
  let fenceChar = '';
  let fenceLen = 0;
  for (let raw of source.split('\n')) {
    const body = raw.replace(/\r$/, '');
    if (inFence) {
      const close = FENCE_CLOSE_LINE.exec(body);
      if (close && close[1][0] === fenceChar && close[1].length >= fenceLen) inFence = false;
      continue;
    }
    const open = FENCE_OPEN_LINE.exec(body);
    if (open) {
      inFence = true;
      fenceChar = open[1][0];
      fenceLen = open[1].length;
      fenceOpeners++;
      continue;
    }
    if (/^ {0,3}>/.test(body)) continue;
    if (ATX_LINE.test(body)) atxLines++;
  }
}

for (const file of files) {
  const original = fs.readFileSync(file, 'utf8');
  totalBytes += Buffer.byteLength(original, 'utf8');
  countExpectations(original);

  let round;
  let counts;
  try {
    const doc = parseDoc(original);
    counts = countNodes(doc);
    round = serializeDoc(doc);
  } catch (e) {
    crashed++;
    failures.push({
      file: path.relative(root, file),
      originalLength: original.length,
      roundLength: -1,
      at: -1,
      line: -1,
      originalLine: '-',
      roundLine: '-',
      error: `${e.name}: ${e.message}`
    });
    continue;
  }

  for (const k of Object.keys(counts)) nodeTotals[k] = (nodeTotals[k] || 0) + counts[k];

  if (round === original) {
    identical++;
    continue;
  }

  const at = firstDifference(original, round);
  const line = at >= 0 ? lineNumberAt(original, at) : -1;
  failures.push({
    file: path.relative(root, file),
    originalLength: original.length,
    roundLength: round.length,
    at,
    line,
    originalLine: at >= 0 ? lineAt(original, line) : '-',
    roundLine: at >= 0 ? lineAt(round, line) : '-',
    error: ''
  });
}

console.log('════ 往返保真闸门 ════');
console.log(`  目录      : ${root}`);
console.log(`  文件      : ${files.length} 篇，共 ${(totalBytes / 1024).toFixed(0)} KB`);
console.log(
  '  解析出的节点: ' +
    Object.entries(nodeTotals)
      .sort((a, b) => b[1] - a[1])
      .map(([k, v]) => `${k}=${v}`)
      .join('  ')
);
console.log('');
console.log(`  ✅ 逐字节一致 : ${identical} / ${files.length}`);
console.log(`  ❌ 有差异     : ${failures.length}${crashed ? `（其中解析抛错 ${crashed}）` : ''}`);

// ── 结构识别自检 ──────────────────────────────────────────────────────────────
// 期望：ATX 标题行每条都该变成 heading（Setext 只会让 heading 更多）；每个围栏开启行
// 都该起一个 code_block。这里不要求精确相等（缩进在列表项里的构造合理地不会被单独识别），
// 但**识别率低到一半以下就说明解析器在静默退化**，必须报红。
const headings = nodeTotals.heading || 0;
const codeBlocks = nodeTotals.code_block || 0;
const headingRatio = atxLines ? headings / atxLines : 1;
const codeRatio = fenceOpeners ? codeBlocks / fenceOpeners : 1;
const headingOk = atxLines === 0 || headingRatio >= 0.5;
const codeOk = fenceOpeners === 0 || codeRatio >= 0.5;
console.log('');
console.log('  结构识别自检（源侧计数已排除代码块内部与引用行）:');
console.log(
  `    ATX 标题行 ${atxLines} → heading 节点 ${headings}   识别率 ${(headingRatio * 100).toFixed(0)}%   ${headingOk ? '✅' : '❌ 解析器在静默退化'}`
);
console.log(
  `    围栏开启行 ${fenceOpeners} → code_block 节点 ${codeBlocks}   识别率 ${(codeRatio * 100).toFixed(0)}%   ${codeOk ? '✅' : '❌ 解析器在静默退化'}`
);

const recognitionBroken = !headingOk || !codeOk;
if (recognitionBroken) {
  console.log('');
  console.log('  ⚠️ 结构识别不完整：保真不受影响（原文照旧），但渲染会退化成朴素文本。');
}

const shown = showAll ? failures : failures.slice(0, showCount);
if (shown.length) {
  console.log('\n──── 差异明细 ────');
  for (const f of shown) {
    console.log(`\n✗ ${f.file}`);
    if (f.error) {
      console.log(`    解析抛错: ${f.error}`);
      continue;
    }
    console.log(`    长度  原 ${f.originalLength} / 回 ${f.roundLength}`);
    if (f.at >= 0) {
      console.log(`    首个差异: 第 ${f.line} 行`);
      console.log(`      原: ${f.originalLine}`);
      console.log(`      回: ${f.roundLine}`);
    }
  }
  if (!showAll && failures.length > shown.length) {
    console.log(`\n  …还有 ${failures.length - shown.length} 篇未显示（加 --all 看全部）`);
  }
} else {
  console.log('\n  全部笔记「打开 → 原样写回」逐字节一致：文件不会因为用了编辑器而变化。');
}

process.exit(failures.length === 0 && !recognitionBroken ? 0 : 1);
