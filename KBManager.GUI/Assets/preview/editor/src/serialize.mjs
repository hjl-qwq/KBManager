// 文档 → 源文本。**纯拼接，不做任何规范化。**
//
// 这是整条保真链的最后一环，也是最简单的一环：深度优先把所有文本节点的 text 按顺序拼起来。
// 因为解析器把源文件的每一个字符都放进了某个文本节点（结构性符号也在内），
// 所以这里拼出来的必然就是原文 —— 逐字节一致，不是"尽量"。
//
// ProseMirror 自带的 textContent 不能用：它不会输出 horizontal_rule 这类无文本节点上
// 保存的原文，也不该被依赖来实现这么关键的约束。这里显式写出来，读代码就能确认。
export function serializeDoc(doc) {
  let out = '';
  doc.forEach((child) => {
    out += serializeNode(child);
  });
  return out;
}

function serializeNode(node) {
  if (node.isText) return node.text;

  // 水平线在 ProseMirror 里没有文本内容，原文存在属性上（`---` / `***` / `___`）。
  if (node.type.name === 'horizontal_rule') return node.attrs.text || '';

  let out = '';
  node.forEach((child) => {
    out += serializeNode(child);
  });
  return out;
}

/** 统计各类节点数量，仅用于测试输出，便于判断解析器到底认出了什么。 */
export function countNodes(doc) {
  const counts = {};
  const visit = (node) => {
    counts[node.type.name] = (counts[node.type.name] || 0) + 1;
    node.forEach(visit);
  };
  doc.forEach(visit);
  return counts;
}
