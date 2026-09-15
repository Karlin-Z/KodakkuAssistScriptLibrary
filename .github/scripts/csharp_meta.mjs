/**
 * 从 C# 源码里找到 [ScriptType(...)] 并解析它的参数。
 *
 * 零依赖，只做「语法层」的事：定位特性、切分参数、还原字符串/数组字面量；
 * 某个字段是否符合业务规则，由 pr_review.validateScriptFile 判断。
 *
 * 为什么要手写而不是用正则：特性的参数支持具名实参且顺序任意、可以跨多行，
 * 字符串里还可能出现 ")"、"," 甚至 "]"（例如 note 里写说明），正则很容易切错。
 *
 * 支持写法：
 *     [ScriptType(name: "M1s绘图", territorys: [1226], guid: "...", author: "Karlin")]
 *     [ScriptTypeAttribute("guid", "Name", new uint[] { 1226 }, "0.0.1", "Author")]
 *     [ScriptType(guid: "g", note: @"逐字""字符串", author: "A")]
 *
 * 文件结构：常量 -> 字面量读取 -> 注释屏蔽 -> 定位特性 -> 切分参数 -> 还原取值 -> 对外入口。
 *
 * 由 csharp_meta.py 移植而来（Python 版已随迁移移除），行为需保持不变，错误文案也算在内。
 */

// --------------------------------------------------------------------------- //
// 常量
// --------------------------------------------------------------------------- //

/** ScriptTypeAttribute 构造函数的参数顺序（见 Interface/ScriptAttribute.cs） */
export const PARAM_ORDER = [
  'guid',
  'name',
  'territorys',
  'version',
  'author',
  'note',
  'updateInfo',
];

/** 能出现在特性名末尾的两种写法 */
const ATTRIBUTE_NAMES = new Set(['ScriptType', 'ScriptTypeAttribute']);

/** 简单转义序列，以及 \u / \x / \U 的位数 */
const SIMPLE_ESCAPES = new Map([
  ['n', '\n'],
  ['r', '\r'],
  ['t', '\t'],
  ['0', '\0'],
  ['a', '\x07'],
  ['b', '\b'],
  ['f', '\f'],
  ['v', '\v'],
  ['\\', '\\'],
  ['"', '"'],
  ["'", "'"],
]);
const ESCAPE_WIDTHS = new Map([
  ['u', 4],
  ['x', 2],
  ['U', 8],
]);

const IDENT_ANCHORED = /^[A-Za-z_][A-Za-z0-9_]*$/;
/** 需要从指定下标开始匹配时用粘滞模式（对应 Python 的 re.match(text, pos)） */
const IDENT_STICKY = /[A-Za-z_][A-Za-z0-9_]*/y;
/** 从当前位置起匹配，必须锚定在开头（对应 Python 的 re.match(text, pos)） */
const INT_LITERAL_PREFIX = /^-?(?:0[xX][0-9a-fA-F]+|[0-9]+)[uUlL]*/;
const INT_LITERAL_ANCHORED = /^-?(?:0[xX][0-9a-fA-F]+|[0-9]+)[uUlL]*$/;
const HEX_ANCHORED = /^[0-9a-fA-F]+$/;
const INT_SUFFIX_RE = /[uUlL]+$/;

/** 匹配 const 声明，例如 `    private const string noteStr =` */
const CONST_DECL_RE = /\bconst\s+([A-Za-z_][\w.]*(?:\s*<[^;=]*>)?(?:\s*\[\s*\])?)\s+([A-Za-z_]\w*)\s*=\s*/g;

/** 字符串 / uint 数组字面量在 C# 里的几种写法 */
const NEW_ARRAY_RE = /^new\s+(?:[A-Za-z_][\w.]*\s*)?\[\s*\]\s*\{(.*)\}$/s;
const NEW_IMPLICIT_ARRAY_RE = /^new\s*\[\s*\]\s*\{(.*)\}$/s;

/** 空白字符集合，与 Python 里显式列出的 " \t\r\n" 保持一致 */
const SPACES = ' \t\r\n';

// --------------------------------------------------------------------------- //
// 小工具
// --------------------------------------------------------------------------- //

/**
 * 复刻 Python 的 repr()，用于生成与 Python 版本一致的错误文案。
 *
 * 只覆盖这里用得到的字符串场景：引号选择、常见转义、控制字符走 \xXX。
 */
export function pyRepr(value) {
  const text = String(value);
  const quote = text.includes("'") && !text.includes('"') ? '"' : "'";
  let out = '';
  for (const ch of text) {
    const code = ch.codePointAt(0);
    if (ch === '\\') out += '\\\\';
    else if (ch === quote) out += `\\${quote}`;
    else if (ch === '\n') out += '\\n';
    else if (ch === '\r') out += '\\r';
    else if (ch === '\t') out += '\\t';
    else if (code < 0x20 || code === 0x7f) out += `\\x${code.toString(16).padStart(2, '0')}`;
    else out += ch;
  }
  return quote + out + quote;
}

/** 空白判定，对应 Python 的 `text[i] in " \t\r\n"`。 */
const isSpace = (ch) => ch !== undefined && SPACES.includes(ch);

/** 是否是字符串字面量的起点（" ' 或 @"）。 */
const startsLiteral = (text, i) =>
  text[i] === '"' || text[i] === "'" || (text[i] === '@' && text[i + 1] === '"');

// --------------------------------------------------------------------------- //
// 字面量读取：原样复制 / 跳过
// --------------------------------------------------------------------------- //

/** 复制被 quote 包裹的字面量（"..." 或 '...'，支持 \ 转义），返回结束后的下标。 */
function copyQuoted(text, i, out, quote) {
  out.push(quote);
  i += 1;
  const n = text.length;
  while (i < n) {
    const ch = text[i];
    if (ch === '\\' && i + 1 < n) {
      out.push(text.slice(i, i + 2));
      i += 2;
      continue;
    }
    out.push(ch);
    i += 1;
    if (ch === quote) break;
  }
  return i;
}

/** 从开引号处复制逐字字符串的内容（"" 表示一个引号），返回结束下标。 */
function copyVerbatimBody(text, i, out) {
  out.push('"');
  i += 1;
  const n = text.length;
  while (i < n) {
    if (text[i] === '"') {
      if (i + 1 < n && text[i + 1] === '"') {
        out.push('""');
        i += 2;
        continue;
      }
      out.push('"');
      i += 1;
      break;
    }
    out.push(text[i]);
    i += 1;
  }
  return i;
}

/**
 * 复制 $ / @ 前缀字符串的原文（插值串、逐字串、插值原始串），返回结束下标。
 *
 * 前缀一起复制进去，插值与转义语义交给 decodeString 判断。
 * 不是字符串字面量（例如单独的 $ 或 @）时返回 null，且不修改 out。
 */
function copyInterpolated(text, i, out) {
  let j = i;
  while (j < text.length && (text[j] === '$' || text[j] === '@')) j += 1;
  if (j === i || j >= text.length || text[j] !== '"') return null;

  out.push(text.slice(i, j));
  if (text.startsWith('"""', j)) return copyRaw(text, j, out);
  if (text.slice(i, j).includes('@')) return copyVerbatimBody(text, j, out);
  return copyQuoted(text, j, out, '"');
}

/** 复制三引号及以上的原始字符串字面量。 */
function copyRaw(text, i, out) {
  const n = text.length;
  let j = i;
  while (j < n && text[j] === '"') j += 1;
  const quotes = '"'.repeat(j - i);
  const end = text.indexOf(quotes, j);
  if (end === -1) {
    out.push(text.slice(i));
    return n;
  }
  out.push(text.slice(i, end + quotes.length));
  return end + quotes.length;
}

/**
 * 从 i 开始读一个字面量的原文，返回 [原文, 结束下标]；读不到则返回 [null, i]。
 *
 * 支持普通字符串、@ 逐字字符串、$ 插值字符串（含插值原始字符串）、三引号原始字符串、
 * 单引号字符字面量、整数字面量。
 */
function readLiteral(text, i) {
  if (i >= text.length) return [null, i];

  const ch = text[i];
  if (ch === '$' || ch === '@') {
    const scratch = [];
    const end = copyInterpolated(text, i, scratch);
    if (end !== null) return [scratch.join(''), end];
  }

  if (ch === '"' || ch === "'") {
    const scratch = [];
    let end;
    if (ch === '"') {
      end = text.startsWith('"""', i)
        ? copyRaw(text, i, scratch)
        : copyQuoted(text, i, scratch, '"');
    } else {
      end = copyQuoted(text, i, scratch, "'");
    }
    return [scratch.join(''), end];
  }

  const match = INT_LITERAL_PREFIX.exec(text.slice(i));
  if (match) return [match[0], i + match[0].length];
  return [null, i];
}

/** 跳过当前位置的字面量，返回结束后的下标；i 不在字面量上则原样返回。 */
function skipLiteral(text, i) {
  const [literal, end] = readLiteral(text, i);
  return literal !== null ? end : i;
}

/** 把注释字符替换成空格（保留换行），字符串字面量原样保留。 */
export function stripComments(text) {
  const out = [];
  let i = 0;
  const n = text.length;
  while (i < n) {
    const ch = text[i];
    const nxt = i + 1 < n ? text[i + 1] : '';

    if (ch === '/' && nxt === '/') {
      while (i < n && text[i] !== '\n') {
        out.push(' ');
        i += 1;
      }
      continue;
    }

    if (ch === '/' && nxt === '*') {
      out.push('  ');
      i += 2;
      while (i < n && !(text[i] === '*' && i + 1 < n && text[i + 1] === '/')) {
        out.push(text[i] === '\n' ? '\n' : ' ');
        i += 1;
      }
      if (i < n) {
        out.push('  ');
        i += 2;
      }
      continue;
    }

    if (ch === '$' || ch === '@') {
      const end = copyInterpolated(text, i, out);
      if (end !== null) {
        i = end;
        continue;
      }
    }

    if (ch === '"' || ch === "'") {
      i = text.startsWith('"""', i) ? copyRaw(text, i, out) : copyQuoted(text, i, out, ch);
      continue;
    }

    out.push(ch);
    i += 1;
  }

  return out.join('');
}

// --------------------------------------------------------------------------- //
// 定位 [ScriptType(...)]
// --------------------------------------------------------------------------- //

/** 返回与 openIndex 处 '(' 配对的 ')' 下标；不配对则返回 null。 */
function matchParen(text, openIndex) {
  let depth = 0;
  let i = openIndex;
  const n = text.length;
  while (i < n) {
    const ch = text[i];
    if (startsLiteral(text, i)) {
      i = skipLiteral(text, i);
      continue;
    }
    if (ch === '(' || ch === '[' || ch === '{') depth += 1;
    else if (ch === ')' || ch === ']' || ch === '}') {
      depth -= 1;
      if (depth === 0) return i;
    }
    i += 1;
  }
  return null;
}

/**
 * 返回 {arglists, errors}。
 *
 * arglists 是每处 [ScriptType(...)] 括号内的原文；写成 [ScriptType] 无参数时该元素为 null。
 * text 需要是 stripComments 之后的内容。
 */
export function findArglists(text) {
  const arglists = [];
  const errors = [];
  let i = 0;
  const n = text.length;

  while (i < n) {
    if (startsLiteral(text, i)) {
      i = skipLiteral(text, i);
      continue;
    }

    if (text[i] !== '[') {
      i += 1;
      continue;
    }

    const start = i + 1;
    IDENT_STICKY.lastIndex = start;
    const match = IDENT_STICKY.exec(text);
    if (!match) {
      i += 1;
      continue;
    }

    let end = match.index + match[0].length;
    while (end < n && text[end] === '.') {
      IDENT_STICKY.lastIndex = end + 1;
      const nxt = IDENT_STICKY.exec(text);
      if (!nxt) break;
      end = nxt.index + nxt[0].length;
    }

    const lastName = text.slice(start, end).split('.').pop();
    if (!ATTRIBUTE_NAMES.has(lastName)) {
      i += 1;
      continue;
    }

    // 排除 ScriptTypeHelper 这类同前缀的长标识符
    IDENT_STICKY.lastIndex = end;
    const trailing = IDENT_STICKY.exec(text);
    if (trailing) {
      i = trailing.index + trailing[0].length;
      continue;
    }

    let cursor = end;
    while (cursor < n && isSpace(text[cursor])) cursor += 1;

    if (cursor < n && text[cursor] === '(') {
      const close = matchParen(text, cursor);
      if (close === null) {
        errors.push('特性的括号不匹配');
        return { arglists, errors };
      }
      arglists.push(text.slice(cursor + 1, close));
      i = close + 1;
      continue;
    }

    if (cursor < n && text[cursor] === ']') {
      arglists.push(null);
      i = cursor + 1;
      continue;
    }

    i += 1;
  }

  return { arglists, errors };
}

// --------------------------------------------------------------------------- //
// 切分参数
// --------------------------------------------------------------------------- //

/** 按 separator 切分，忽略括号内与字符串内的分隔符。 */
function splitTopLevel(text, separator) {
  const parts = [];
  let depth = 0;
  let current = [];
  let i = 0;
  const n = text.length;
  while (i < n) {
    const ch = text[i];
    if (startsLiteral(text, i)) {
      const end = skipLiteral(text, i);
      current.push(text.slice(i, end));
      i = end;
      continue;
    }
    if (ch === '(' || ch === '[' || ch === '{') depth += 1;
    else if (ch === ')' || ch === ']' || ch === '}') depth -= 1;
    if (ch === separator && depth === 0) {
      parts.push(current.join(''));
      current = [];
      i += 1;
      continue;
    }
    current.push(ch);
    i += 1;
  }
  parts.push(current.join(''));
  return parts;
}

/** 返回顶层具名实参的 ':' 下标；没有则返回 null。 */
function findNamedColon(text) {
  let depth = 0;
  let i = 0;
  const n = text.length;
  while (i < n) {
    const ch = text[i];
    if (startsLiteral(text, i)) {
      i = skipLiteral(text, i);
      continue;
    }
    if (ch === '(' || ch === '[' || ch === '{') depth += 1;
    else if (ch === ')' || ch === ']' || ch === '}') depth -= 1;
    else if (ch === ':' && depth === 0) {
      if (i + 1 < n && text[i + 1] === ':') {
        // 跳过 ::
        i += 2;
        continue;
      }
      return i;
    }
    i += 1;
  }
  return null;
}

// --------------------------------------------------------------------------- //
// 还原取值
// --------------------------------------------------------------------------- //

/** 解析 C# 整数字面量（负号 / 十六进制 / uUlL 后缀）；不是整数字面量则返回 null。 */
function parseIntLiteral(token) {
  if (!INT_LITERAL_ANCHORED.test(token)) return null;
  let body = token.replace(INT_SUFFIX_RE, '');
  const negative = body.startsWith('-');
  body = body.replace(/^-+/, '');
  const value = body.toLowerCase().startsWith('0x') ? parseInt(body, 16) : parseInt(body, 10);
  return negative ? -value : value;
}

/** 按 C# 原始字符串字面量的语义还原内容：去掉首行换行，并按结束分隔符的缩进左对齐。 */
function decodeRawString(body) {
  let content = body.replaceAll('\r\n', '\n').replaceAll('\r', '\n');
  if (content.startsWith('\n')) content = content.slice(1);
  const lines = content.split('\n');
  const indent = lines[lines.length - 1];
  if (indent.trim() === '') {
    return lines
      .slice(0, -1)
      .map((line) => (line.startsWith(indent) ? line.slice(indent.length) : line))
      .join('\n');
  }
  return lines.join('\n');
}

/** 还原普通字符串里的转义序列；遇到不认识的转义返回 null。 */
function decodeEscapes(body) {
  const out = [];
  let i = 0;
  const n = body.length;
  while (i < n) {
    const ch = body[i];
    if (ch !== '\\') {
      out.push(ch);
      i += 1;
      continue;
    }
    i += 1;
    if (i >= n) return null;
    const esc = body[i];
    if (SIMPLE_ESCAPES.has(esc)) {
      out.push(SIMPLE_ESCAPES.get(esc));
      i += 1;
      continue;
    }
    if (ESCAPE_WIDTHS.has(esc)) {
      const width = ESCAPE_WIDTHS.get(esc);
      const digits = body.slice(i + 1, i + 1 + width);
      if (digits.length !== width || !HEX_ANCHORED.test(digits)) return null;
      out.push(String.fromCodePoint(parseInt(digits, 16)));
      i += 1 + width;
      continue;
    }
    return null;
  }
  return out.join('');
}

/** 拆出字符串字面量的 $ / @ 前缀，返回 [前缀, 余下部分]。 */
function splitStringPrefix(s) {
  let i = 0;
  while (i < s.length && (s[i] === '$' || s[i] === '@')) i += 1;
  return [s.slice(0, i), s.slice(i)];
}

/** 把插值 {标识符} 换成同文件里对应 const 的字符串值；换不了就原样保留。 */
function resolveHole(expression, constants) {
  const name = expression.trim();
  if (constants && IDENT_ANCHORED.test(name)) {
    const entry = constants.get(name);
    if (entry && entry[0] === 'string') return entry[1];
  }
  return `{${expression}}`;
}

/**
 * 处理插值字符串的花括号：连续两组花括号表示字面花括号，{标识符} 尝试取值替换。
 *
 * C# 用 $ 的个数决定插值定界符：一个 $ 时是 {expr}，两个 $ 时则是 {{expr}}。
 * 这里只认「单个标识符」的插值——复杂表达式原样留在文本里，总好过整段 note 丢失。
 */
function decodeInterpolation(content, dollars, constants) {
  if (dollars <= 0) return content;

  const openBrace = '{'.repeat(dollars);
  const closeBrace = '}'.repeat(dollars);
  const out = [];
  let i = 0;
  const n = content.length;
  while (i < n) {
    if (content.startsWith(openBrace + openBrace, i)) {
      out.push(openBrace);
      i += openBrace.length * 2;
      continue;
    }
    if (content.startsWith(closeBrace + closeBrace, i)) {
      out.push(closeBrace);
      i += closeBrace.length * 2;
      continue;
    }
    if (content.startsWith(openBrace, i)) {
      const close = content.indexOf(closeBrace, i + openBrace.length);
      if (close === -1) {
        out.push(content.slice(i));
        break;
      }
      out.push(resolveHole(content.slice(i + openBrace.length, close), constants));
      i = close + closeBrace.length;
      continue;
    }
    out.push(content[i]);
    i += 1;
  }
  return out.join('');
}

/**
 * 把整串字符串字面量还原成 JS 字符串；不是单个字面量则返回 null。
 *
 * 支持 @ 逐字字符串、$ 插值字符串、三引号原始字符串（含插值原始字符串）。
 * 传入 constants 时，插值里的 {标识符} 会尝试用同文件 const 的值替换；替换不了的原样
 * 保留（形如 {Version}），避免整段 note 因为含插值就被判成「解析不了」而丢弃。
 */
function decodeString(text, constants) {
  const s = text.trim();
  if (!s) return null;

  const [prefix, rest] = splitStringPrefix(s);
  if (!rest.startsWith('"')) return null;
  const dollars = (prefix.match(/\$/g) ?? []).length;
  const verbatim = prefix.includes('@');

  if (rest.startsWith('"""')) {
    let quotes = 0;
    while (quotes < rest.length && rest[quotes] === '"') quotes += 1;
    const closing = '"'.repeat(quotes);
    if (!rest.endsWith(closing) || rest.length < quotes * 2) return null;
    const content = decodeRawString(rest.slice(quotes, rest.length - quotes));
    return dollars === 0 ? content : decodeInterpolation(content, dollars, constants);
  }

  if (!rest.endsWith('"') || rest.length < 2) return null;

  let content;
  if (verbatim) {
    content = rest.slice(1, -1).replaceAll('""', '"');
  } else {
    content = decodeEscapes(rest.slice(1, -1));
    if (content === null) return null;
  }
  return dollars === 0 ? content : decodeInterpolation(content, dollars, constants);
}

/** 还原 uint 数组字面量；不是数组或元素不是整数字面量则返回 null。 */
function decodeIntArray(text) {
  const s = text.trim();

  if (s.startsWith('Array.Empty')) return [];
  if (s === 'null') return null;

  let inner;
  if (s.startsWith('[') && s.endsWith(']')) {
    inner = s.slice(1, -1);
  } else {
    const match = NEW_ARRAY_RE.exec(s) ?? NEW_IMPLICIT_ARRAY_RE.exec(s);
    if (!match) return null;
    inner = match[1];
  }

  if (!inner.trim()) return [];

  const values = [];
  for (const part of splitTopLevel(inner, ',')) {
    const token = part.trim();
    if (!token) continue;
    // 负数虽然对 uint 非法，但先解析出来才能给出「超出 uint 范围」这种精确提示
    const value = parseIntLiteral(token);
    if (value === null) return null;
    values.push(value);
  }
  return values;
}

/**
 * 返回 [kind, value]，kind ∈ string / int / array / null / identifier / unknown。
 *
 * kind 为 identifier 时 value 是标识符名（本文件里没有对应的 const 声明）；
 * kind 为 unknown 时 value 是该参数值的原文，便于报错时回显给贡献者。
 */
export function decodeValue(raw, constants) {
  const s = raw.trim();
  if (!s) return ['unknown', s];
  if (s === 'null' || s === 'default') return ['null', null];

  // 字面量拼接：note: "第一行" + "第二行" / note: $"v{Version}" + NoteTail
  const parts = splitTopLevel(s, '+');
  if (parts.length > 1) {
    const pieces = [];
    for (const part of parts) {
      const [kind, value] = decodeValue(part, constants);
      if (kind !== 'string') return ['unknown', s];
      pieces.push(value);
    }
    return ['string', pieces.join('')];
  }

  const [, rest] = splitStringPrefix(s);
  if (rest.startsWith('"')) {
    const value = decodeString(s, constants);
    return value !== null ? ['string', value] : ['unknown', s];
  }

  const number = parseIntLiteral(s);
  if (number !== null) return ['int', number];

  const array = decodeIntArray(s);
  if (array !== null) return ['array', array];

  if (IDENT_ANCHORED.test(s)) {
    if (constants && constants.has(s)) return constants.get(s);
    return ['identifier', s];
  }

  return ['unknown', s];
}

// --------------------------------------------------------------------------- //
// const 声明（特性里常用标识符引用长文本）
// --------------------------------------------------------------------------- //

/** 读取 const 的初值：字面量、同文件里的另一个 const，或由 + 连接的若干项。 */
function readConstExpression(text, i, constants) {
  const values = [];
  const kinds = [];
  let cursor = i;
  const n = text.length;

  for (;;) {
    while (cursor < n && isSpace(text[cursor])) cursor += 1;
    const [literal, literalEnd] = readLiteral(text, cursor);
    let kind;
    let decoded;
    let end;

    if (literal === null) {
      // const string UpdateInfo = UpdateStr;  —— 引用同文件里的另一个 const
      IDENT_STICKY.lastIndex = cursor;
      const reference = IDENT_STICKY.exec(text);
      const entry = reference && constants ? constants.get(reference[0]) : undefined;
      if (entry === undefined) return null;
      [kind, decoded] = entry;
      end = reference.index + reference[0].length;
    } else {
      [kind, decoded] = decodeValue(literal, constants);
      end = literalEnd;
    }

    if (kind === 'unknown' || kind === 'null' || kind === 'identifier') return null;
    values.push(decoded);
    kinds.push(kind);
    cursor = end;
    while (cursor < n && isSpace(text[cursor])) cursor += 1;
    if (cursor < n && text[cursor] === '+') {
      cursor += 1;
      continue;
    }
    break;
  }

  if (values.length === 1) return [kinds[0], values[0]];
  if (kinds.every((kind) => kind === 'string')) return ['string', values.join('')];
  return null;
}

/** 比较 table 里已有的 [kind, value] 与新的取值是否相同。 */
function entryEquals(existing, kind, value) {
  if (!existing || existing[0] !== kind) return false;
  const before = existing[1];
  if (Array.isArray(before) || Array.isArray(value)) {
    if (!Array.isArray(before) || !Array.isArray(value)) return false;
    if (before.length !== value.length) return false;
    return before.every((item, index) => item === value[index]);
  }
  return before === value;
}

/**
 * 收集文件里的 const 声明，供解析特性里的标识符实参使用。
 *
 * 真实脚本常见写法是先把长文本放进 const，再在特性里引用：
 *
 *     [ScriptType(..., updateInfo: updateInfoStr)]
 *     public class X {
 *         const string updateInfoStr = <插值原始字符串>;
 *     }
 *
 * 注意 const 可能声明在特性之后，也可能引用另一个 const（const string A = B;）或用
 * 插值（形如 $"v{Version}"），所以这里反复解析直到取值不再变化；解不出来的环引用被丢弃。
 */
export function collectConstants(text) {
  const decls = [];
  for (const match of text.matchAll(CONST_DECL_RE)) {
    decls.push([match[2], match[1].replaceAll(' ', ''), match.index + match[0].length]);
  }

  const table = new Map();
  for (let round = 0; round <= decls.length; round += 1) {
    let changed = false;
    for (const [name, typeText, start] of decls) {
      const result = readConstExpression(text, start, table);
      if (result === null) continue;
      const [kind, value] = result;
      if (kind === 'string' && typeText !== 'string') continue;
      if (kind === 'array' && !typeText.endsWith('[]')) continue;
      if (!entryEquals(table.get(name), kind, value)) {
        table.set(name, [kind, value]);
        changed = true;
      }
    }
    if (!changed) break;
  }
  return table;
}

// --------------------------------------------------------------------------- //
// 对外入口
// --------------------------------------------------------------------------- //

/** 把括号内的原文切成 {参数名: [kind, value]}；返回 {params, errors}。 */
export function parseArglist(arglist, constants) {
  const params = {};
  const errors = [];
  let positionalIndex = 0;

  for (const part of splitTopLevel(arglist, ',')) {
    const token = part.trim();
    if (!token) continue;

    const colon = findNamedColon(token);
    if (colon !== null) {
      const key = token.slice(0, colon).trim();
      if (!IDENT_ANCHORED.test(key)) {
        errors.push(`无法识别的具名参数：${pyRepr(token.slice(0, 60))}`);
        continue;
      }
      if (!PARAM_ORDER.includes(key)) {
        errors.push(`无法识别的具名参数 ${pyRepr(key)}；可用参数：${PARAM_ORDER.join(', ')}`);
        continue;
      }
      params[key] = decodeValue(token.slice(colon + 1), constants);
      continue;
    }

    if (positionalIndex >= PARAM_ORDER.length) {
      errors.push(`位置参数过多（最多 ${PARAM_ORDER.length} 个）`);
      break;
    }
    const name = PARAM_ORDER[positionalIndex];
    positionalIndex += 1;
    if (Object.hasOwn(params, name)) {
      errors.push(`参数 ${name} 被重复指定`);
      continue;
    }
    params[name] = decodeValue(token, constants);
  }

  return { params, errors };
}

/**
 * 解析源码里的 ScriptType 特性，返回 {params, errors}。
 *
 * params: {参数名: [kind, value]}，未出现的参数不在其中。
 */
export function extractScriptType(text) {
  const stripped = stripComments(text);
  if (!stripped.trim()) return { params: {}, errors: ['文件内容为空'] };

  const { arglists, errors } = findArglists(stripped);
  if (errors.length) return { params: {}, errors };
  if (!arglists.length) return { params: {}, errors: ['没有找到 [ScriptType(...)] 特性'] };
  if (arglists.length > 1) {
    return {
      params: {},
      errors: [`找到 ${arglists.length} 处 [ScriptType(...)]，要求每个 .cs 文件只能有一处`],
    };
  }
  if (arglists[0] === null) return { params: {}, errors: ['[ScriptType] 缺少参数列表'] };

  return parseArglist(arglists[0], collectConstants(stripped));
}
