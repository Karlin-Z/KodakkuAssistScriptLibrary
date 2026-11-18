/** 参数顺序 */
export const PARAM_ORDER = ['guid', 'name', 'territorys', 'version', 'author', 'note', 'updateInfo']

const ATTRIBUTE_NAMES = new Set(['ScriptType', 'ScriptTypeAttribute'])

const SIMPLE_ESCAPES = new Map<string, string>([
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
    ["'", "'"]
])
const ESCAPE_WIDTHS = new Map<string, number>([
    ['u', 4],
    ['x', 2],
    ['U', 8]
])

const IDENT_ANCHORED = /^[A-Za-z_][A-Za-z0-9_]*$/
const IDENT_STICKY = /[A-Za-z_][A-Za-z0-9_]*/y
const INT_LITERAL_PREFIX = /^-?(?:0[xX][0-9a-fA-F]+|[0-9]+)[uUlL]*/
const INT_LITERAL_ANCHORED = /^-?(?:0[xX][0-9a-fA-F]+|[0-9]+)[uUlL]*$/
const HEX_ANCHORED = /^[0-9a-fA-F]+$/
const INT_SUFFIX_RE = /[uUlL]+$/

const CONST_DECL_RE =
    /\bconst\s+([A-Za-z_][\w.]*(?:\s*<[^;=]*>)?(?:\s*\[\s*\])?)\s+([A-Za-z_]\w*)\s*=\s*/g

const NEW_ARRAY_RE = /^new\s+(?:[A-Za-z_][\w.]*\s*)?\[\s*\]\s*\{(.*)\}$/s
const NEW_IMPLICIT_ARRAY_RE = /^new\s*\[\s*\]\s*\{(.*)\}$/s

const SPACES = ' \t\r\n'

/** 取值类别 */
export type DecodedKind = 'string' | 'int' | 'array' | 'null' | 'identifier' | 'unknown'

/** 取值元组 */
export type DecodedValue = [DecodedKind, string | number | readonly number[] | null]

/** const 表 */
export type ConstantTable = Map<string, DecodedValue>

/** 特性参数表 */
export type ScriptTypeParams = Record<string, DecodedValue>

/** 解析结果 */
export interface ParseResult {
    params: ScriptTypeParams
    errors: string[]
}

/** 模拟 repr */
export function pyRepr(value: unknown): string {
    const text = String(value)
    const quote = text.includes("'") && !text.includes('"') ? '"' : "'"
    let out = ''
    for (const ch of text) {
        const code = ch.codePointAt(0) ?? 0
        if (ch === '\\') out += '\\\\'
        else if (ch === quote) out += `\\${quote}`
        else if (ch === '\n') out += '\\n'
        else if (ch === '\r') out += '\\r'
        else if (ch === '\t') out += '\\t'
        else if (code < 0x20 || code === 0x7f) out += `\\x${code.toString(16).padStart(2, '0')}`
        else out += ch
    }
    return quote + out + quote
}

/** 判断空白字符 */
const isSpace = (ch: string | undefined): boolean => ch !== undefined && SPACES.includes(ch)

/** 判断字面量起点 */
const startsLiteral = (text: string, i: number): boolean =>
    text[i] === '"' || text[i] === "'" || (text[i] === '@' && text[i + 1] === '"')

/** 复制引号字符串 */
function copyQuoted(text: string, i: number, out: string[], quote: string): number {
    out.push(quote)
    i += 1
    const n = text.length
    while (i < n) {
        const ch = text[i]
        // 处理转义
        if (ch === '\\' && i + 1 < n) {
            out.push(text.slice(i, i + 2))
            i += 2
            continue
        }
        out.push(ch)
        i += 1
        if (ch === quote) break
    }
    return i
}

/** 复制逐字字符串 */
function copyVerbatimBody(text: string, i: number, out: string[]): number {
    out.push('"')
    i += 1
    const n = text.length
    while (i < n) {
        if (text[i] === '"') {
            // 双引号转义
            if (i + 1 < n && text[i + 1] === '"') {
                out.push('""')
                i += 2
                continue
            }
            out.push('"')
            i += 1
            break
        }
        out.push(text[i])
        i += 1
    }
    return i
}

/** 复制插值字符串 */
function copyInterpolated(text: string, i: number, out: string[]): number | null {
    let j = i
    while (j < text.length && (text[j] === '$' || text[j] === '@')) j += 1
    if (j === i || j >= text.length || text[j] !== '"') return null

    out.push(text.slice(i, j))
    if (text.startsWith('"""', j)) return copyRaw(text, j, out)
    if (text.slice(i, j).includes('@')) return copyVerbatimBody(text, j, out)
    return copyQuoted(text, j, out, '"')
}

/** 复制原始字符串 */
function copyRaw(text: string, i: number, out: string[]): number {
    const n = text.length
    let j = i
    while (j < n && text[j] === '"') j += 1
    const quotes = '"'.repeat(j - i)
    const end = text.indexOf(quotes, j)
    if (end === -1) {
        out.push(text.slice(i))
        return n
    }
    out.push(text.slice(i, end + quotes.length))
    return end + quotes.length
}

/** 读取字面量原文 */
function readLiteral(text: string, i: number): [string | null, number] {
    if (i >= text.length) return [null, i]

    const ch = text[i]
    if (ch === '$' || ch === '@') {
        const scratch: string[] = []
        const end = copyInterpolated(text, i, scratch)
        if (end !== null) return [scratch.join(''), end]
    }

    if (ch === '"' || ch === "'") {
        const scratch: string[] = []
        let end
        if (ch === '"') {
            end = text.startsWith('"""', i)
                ? copyRaw(text, i, scratch)
                : copyQuoted(text, i, scratch, '"')
        } else {
            end = copyQuoted(text, i, scratch, "'")
        }
        return [scratch.join(''), end]
    }

    const match = INT_LITERAL_PREFIX.exec(text.slice(i))
    if (match) return [match[0], i + match[0].length]
    return [null, i]
}

/** 跳过字面量 */
function skipLiteral(text: string, i: number): number {
    const [literal, end] = readLiteral(text, i)
    return literal !== null ? end : i
}

/** 去除注释 */
export function stripComments(text: string): string {
    const out = []
    let i = 0
    const n = text.length
    while (i < n) {
        const ch = text[i]
        const nxt = i + 1 < n ? text[i + 1] : ''

        // 行注释
        if (ch === '/' && nxt === '/') {
            while (i < n && text[i] !== '\n') {
                out.push(' ')
                i += 1
            }
            continue
        }

        // 块注释
        if (ch === '/' && nxt === '*') {
            out.push('  ')
            i += 2
            while (i < n && !(text[i] === '*' && i + 1 < n && text[i + 1] === '/')) {
                out.push(text[i] === '\n' ? '\n' : ' ')
                i += 1
            }
            if (i < n) {
                out.push('  ')
                i += 2
            }
            continue
        }

        if (ch === '$' || ch === '@') {
            const end = copyInterpolated(text, i, out)
            if (end !== null) {
                i = end
                continue
            }
        }

        if (ch === '"' || ch === "'") {
            i = text.startsWith('"""', i) ? copyRaw(text, i, out) : copyQuoted(text, i, out, ch)
            continue
        }

        out.push(ch)
        i += 1
    }

    return out.join('')
}

/** 匹配右括号 */
function matchParen(text: string, openIndex: number): number | null {
    let depth = 0
    let i = openIndex
    const n = text.length
    while (i < n) {
        const ch = text[i]
        if (startsLiteral(text, i)) {
            i = skipLiteral(text, i)
            continue
        }
        if (ch === '(' || ch === '[' || ch === '{') depth += 1
        else if (ch === ')' || ch === ']' || ch === '}') {
            depth -= 1
            if (depth === 0) return i
        }
        i += 1
    }
    return null
}

/** 提取特性参数 */
export function findArglists(text: string): { arglists: (string | null)[]; errors: string[] } {
    const arglists: (string | null)[] = []
    const errors: string[] = []
    let i = 0
    const n = text.length

    while (i < n) {
        if (startsLiteral(text, i)) {
            i = skipLiteral(text, i)
            continue
        }

        if (text[i] !== '[') {
            i += 1
            continue
        }

        const start = i + 1
        IDENT_STICKY.lastIndex = start
        const match = IDENT_STICKY.exec(text)
        if (!match) {
            i += 1
            continue
        }

        let end = match.index + match[0].length
        while (end < n && text[end] === '.') {
            IDENT_STICKY.lastIndex = end + 1
            const nxt = IDENT_STICKY.exec(text)
            if (!nxt) break
            end = nxt.index + nxt[0].length
        }

        const lastName = text.slice(start, end).split('.').pop() ?? ''
        if (!ATTRIBUTE_NAMES.has(lastName)) {
            i += 1
            continue
        }

        IDENT_STICKY.lastIndex = end
        const trailing = IDENT_STICKY.exec(text)
        if (trailing) {
            i = trailing.index + trailing[0].length
            continue
        }

        let cursor = end
        while (cursor < n && isSpace(text[cursor])) cursor += 1

        // 参数列表
        if (cursor < n && text[cursor] === '(') {
            const close = matchParen(text, cursor)
            if (close === null) {
                errors.push('特性的括号不匹配')
                return { arglists, errors }
            }
            arglists.push(text.slice(cursor + 1, close))
            i = close + 1
            continue
        }

        // 无参数
        if (cursor < n && text[cursor] === ']') {
            arglists.push(null)
            i = cursor + 1
            continue
        }

        i += 1
    }

    return { arglists, errors }
}

/** 顶层切分 */
function splitTopLevel(text: string, separator: string): string[] {
    const parts: string[] = []
    let depth = 0
    let current: string[] = []
    let i = 0
    const n = text.length
    while (i < n) {
        const ch = text[i]
        if (startsLiteral(text, i)) {
            const end = skipLiteral(text, i)
            current.push(text.slice(i, end))
            i = end
            continue
        }
        if (ch === '(' || ch === '[' || ch === '{') depth += 1
        else if (ch === ')' || ch === ']' || ch === '}') depth -= 1
        if (ch === separator && depth === 0) {
            parts.push(current.join(''))
            current = []
            i += 1
            continue
        }
        current.push(ch)
        i += 1
    }
    parts.push(current.join(''))
    return parts
}

/** 查找具名冒号 */
function findNamedColon(text: string): number | null {
    let depth = 0
    let i = 0
    const n = text.length
    while (i < n) {
        const ch = text[i]
        if (startsLiteral(text, i)) {
            i = skipLiteral(text, i)
            continue
        }
        if (ch === '(' || ch === '[' || ch === '{') depth += 1
        else if (ch === ')' || ch === ']' || ch === '}') depth -= 1
        // 顶层冒号
        else if (ch === ':' && depth === 0) {
            if (i + 1 < n && text[i + 1] === ':') {
                i += 2
                continue
            }
            return i
        }
        i += 1
    }
    return null
}

/** 解析整数字面量 */
function parseIntLiteral(token: string): number | null {
    if (!INT_LITERAL_ANCHORED.test(token)) return null
    let body = token.replace(INT_SUFFIX_RE, '')
    const negative = body.startsWith('-')
    body = body.replace(/^-+/, '')
    const value = body.toLowerCase().startsWith('0x') ? parseInt(body, 16) : parseInt(body, 10)
    return negative ? -value : value
}

/** 还原原始字符串 */
function decodeRawString(body: string): string {
    let content = body.replaceAll('\r\n', '\n').replaceAll('\r', '\n')
    if (content.startsWith('\n')) content = content.slice(1)

    const lines = content.split('\n')
    const indent = lines[lines.length - 1]
    if (indent.trim() === '') {
        return lines
            .slice(0, -1)
            .map((line: string) => (line.startsWith(indent) ? line.slice(indent.length) : line))
            .join('\n')
    }
    return lines.join('\n')
}

/** 还原转义序列 */
function decodeEscapes(body: string): string | null {
    const out: string[] = []
    let i = 0
    const n = body.length
    while (i < n) {
        const ch = body[i]
        if (ch !== '\\') {
            out.push(ch)
            i += 1
            continue
        }
        i += 1
        if (i >= n) return null
        const esc = body[i]
        if (SIMPLE_ESCAPES.has(esc)) {
            out.push(SIMPLE_ESCAPES.get(esc) as string)
            i += 1
            continue
        }
        // 十六进制转义
        if (ESCAPE_WIDTHS.has(esc)) {
            const width = ESCAPE_WIDTHS.get(esc) as number
            const digits = body.slice(i + 1, i + 1 + width)
            if (digits.length !== width || !HEX_ANCHORED.test(digits)) return null
            out.push(String.fromCodePoint(parseInt(digits, 16)))
            i += 1 + width
            continue
        }
        return null
    }
    return out.join('')
}

/** 拆分字符串前缀 */
function splitStringPrefix(s: string): [string, string] {
    let i = 0
    while (i < s.length && (s[i] === '$' || s[i] === '@')) i += 1
    return [s.slice(0, i), s.slice(i)]
}

/** 解析插值占位 */
function resolveHole(expression: string, constants: ConstantTable | null | undefined): string {
    const name = expression.trim()
    if (constants && IDENT_ANCHORED.test(name)) {
        const entry = constants.get(name)
        if (entry && entry[0] === 'string') return entry[1] as string
    }
    return `{${expression}}`
}

/** 还原插值内容 */
function decodeInterpolation(
    content: string,
    dollars: number,
    constants: ConstantTable | null | undefined
): string {
    if (dollars <= 0) return content

    const openBrace = '{'.repeat(dollars)
    const closeBrace = '}'.repeat(dollars)
    const out: string[] = []
    let i = 0
    const n = content.length
    while (i < n) {
        // 转义花括号
        if (content.startsWith(openBrace + openBrace, i)) {
            out.push(openBrace)
            i += openBrace.length * 2
            continue
        }
        if (content.startsWith(closeBrace + closeBrace, i)) {
            out.push(closeBrace)
            i += closeBrace.length * 2
            continue
        }
        if (content.startsWith(openBrace, i)) {
            const close = content.indexOf(closeBrace, i + openBrace.length)
            if (close === -1) {
                out.push(content.slice(i))
                break
            }
            out.push(resolveHole(content.slice(i + openBrace.length, close), constants))
            i = close + closeBrace.length
            continue
        }
        out.push(content[i])
        i += 1
    }
    return out.join('')
}

/** 还原字符串 */
function decodeString(text: string, constants: ConstantTable | null | undefined): string | null {
    const s = text.trim()
    if (!s) return null

    const [prefix, rest] = splitStringPrefix(s)
    if (!rest.startsWith('"')) return null
    const dollars = (prefix.match(/\$/g) ?? []).length
    const verbatim = prefix.includes('@')

    if (rest.startsWith('"""')) {
        let quotes = 0
        while (quotes < rest.length && rest[quotes] === '"') quotes += 1
        const closing = '"'.repeat(quotes)
        if (!rest.endsWith(closing) || rest.length < quotes * 2) return null
        const content = decodeRawString(rest.slice(quotes, rest.length - quotes))
        return dollars === 0 ? content : decodeInterpolation(content, dollars, constants)
    }

    if (!rest.endsWith('"') || rest.length < 2) return null

    let content
    if (verbatim) {
        content = rest.slice(1, -1).replaceAll('""', '"')
    } else {
        content = decodeEscapes(rest.slice(1, -1))
        if (content === null) return null
    }
    return dollars === 0 ? content : decodeInterpolation(content, dollars, constants)
}

/** 还原整数数组 */
function decodeIntArray(text: string): number[] | null {
    const s = text.trim()

    if (s.startsWith('Array.Empty')) return []
    if (s === 'null') return null

    let inner: string
    if (s.startsWith('[') && s.endsWith(']')) {
        inner = s.slice(1, -1)
    } else {
        const match = NEW_ARRAY_RE.exec(s) ?? NEW_IMPLICIT_ARRAY_RE.exec(s)
        if (!match) return null
        inner = match[1]
    }

    if (!inner.trim()) return []

    const values: number[] = []
    for (const part of splitTopLevel(inner, ',')) {
        const token = part.trim()
        if (!token) continue
        // 先解析负数
        const value = parseIntLiteral(token)
        if (value === null) return null
        values.push(value)
    }
    return values
}

/** 还原参数值 */
export function decodeValue(raw: string, constants?: ConstantTable | null): DecodedValue {
    const s = raw.trim()
    if (!s) return ['unknown', s]
    if (s === 'null' || s === 'default') return ['null', null]

    // 处理拼接
    const parts = splitTopLevel(s, '+')
    if (parts.length > 1) {
        const pieces: string[] = []
        for (const part of parts) {
            const [kind, value] = decodeValue(part, constants)
            if (kind !== 'string') return ['unknown', s]
            pieces.push(value as string)
        }
        return ['string', pieces.join('')]
    }

    const [, rest] = splitStringPrefix(s)
    if (rest.startsWith('"')) {
        const value = decodeString(s, constants)
        return value !== null ? ['string', value] : ['unknown', s]
    }

    // 整数字面量
    const number = parseIntLiteral(s)
    if (number !== null) return ['int', number]

    // 数组字面量
    const array = decodeIntArray(s)
    if (array !== null) return ['array', array]

    // 标识符引用
    if (IDENT_ANCHORED.test(s)) {
        if (constants && constants.has(s)) return constants.get(s) as DecodedValue
        return ['identifier', s]
    }

    return ['unknown', s]
}

/** 读取 const */
function readConstExpression(
    text: string,
    i: number,
    constants: ConstantTable
): DecodedValue | null {
    const values: Array<number | string | readonly number[] | null> = []
    const kinds: DecodedKind[] = []
    let cursor = i
    const n = text.length

    for (;;) {
        while (cursor < n && isSpace(text[cursor])) cursor += 1
        const [literal, literalEnd] = readLiteral(text, cursor)
        let kind: DecodedKind
        let decoded: DecodedValue[1]
        let end: number

        if (literal === null) {
            // 引用 const
            IDENT_STICKY.lastIndex = cursor
            const reference = IDENT_STICKY.exec(text)
            if (!reference) return null
            const entry = constants.get(reference[0])
            if (entry === undefined) return null
            ;[kind, decoded] = entry
            end = reference.index + reference[0].length
        } else {
            ;[kind, decoded] = decodeValue(literal, constants)
            end = literalEnd
        }

        if (kind === 'unknown' || kind === 'null' || kind === 'identifier') return null
        values.push(decoded)
        kinds.push(kind)
        cursor = end
        while (cursor < n && isSpace(text[cursor])) cursor += 1
        if (cursor < n && text[cursor] === '+') {
            cursor += 1
            continue
        }
        break
    }

    if (values.length === 1) return [kinds[0], values[0]]
    if (kinds.every((kind) => kind === 'string')) return ['string', values.join('')]
    return null
}

/** 比较取值 */
function entryEquals(
    existing: DecodedValue | undefined,
    kind: DecodedKind,
    value: DecodedValue[1]
): boolean {
    if (!existing || existing[0] !== kind) return false
    const before = existing[1]
    if (Array.isArray(before) || Array.isArray(value)) {
        if (!Array.isArray(before) || !Array.isArray(value)) return false
        if (before.length !== value.length) return false
        return before.every((item, index) => item === value[index])
    }
    return before === value
}

/** 收集 const */
export function collectConstants(text: string): ConstantTable {
    const decls: Array<[string, string, number]> = []
    for (const match of text.matchAll(CONST_DECL_RE)) {
        decls.push([match[2], match[1].replaceAll(' ', ''), match.index + match[0].length])
    }

    const table: ConstantTable = new Map()
    // 反复求值
    for (let round = 0; round <= decls.length; round += 1) {
        let changed = false
        for (const [name, typeText, start] of decls) {
            const result = readConstExpression(text, start, table)
            if (result === null) continue
            const [kind, value] = result
            if (kind === 'string' && typeText !== 'string') continue
            if (kind === 'array' && !typeText.endsWith('[]')) continue
            if (!entryEquals(table.get(name), kind, value)) {
                table.set(name, [kind, value])
                changed = true
            }
        }
        if (!changed) break
    }
    return table
}

/** 解析参数列表 */
export function parseArglist(arglist: string, constants: ConstantTable): ParseResult {
    const params: ScriptTypeParams = {}
    const errors: string[] = []
    let positionalIndex = 0

    for (const part of splitTopLevel(arglist, ',')) {
        const token = part.trim()
        if (!token) continue

        const colon = findNamedColon(token)
        // 具名参数
        if (colon !== null) {
            const key = token.slice(0, colon).trim()
            if (!IDENT_ANCHORED.test(key)) {
                errors.push(`无法识别的具名参数：${pyRepr(token.slice(0, 60))}`)
                continue
            }
            if (!PARAM_ORDER.includes(key)) {
                errors.push(
                    `无法识别的具名参数 ${pyRepr(key)}；可用参数：${PARAM_ORDER.join(', ')}`
                )
                continue
            }
            params[key] = decodeValue(token.slice(colon + 1), constants)
            continue
        }

        // 位置参数
        if (positionalIndex >= PARAM_ORDER.length) {
            errors.push(`位置参数过多（最多 ${PARAM_ORDER.length} 个）`)
            break
        }
        const name = PARAM_ORDER[positionalIndex]
        positionalIndex += 1
        if (Object.hasOwn(params, name)) {
            errors.push(`参数 ${name} 被重复指定`)
            continue
        }
        params[name] = decodeValue(token, constants)
    }

    return { params, errors }
}

/** 解析脚本特性 */
export function extractScriptType(text: string): ParseResult {
    // 去除注释
    const stripped = stripComments(text)
    if (!stripped.trim()) return { params: {}, errors: ['文件内容为空'] }

    const { arglists, errors } = findArglists(stripped)
    if (errors.length) return { params: {}, errors }
    if (!arglists.length) return { params: {}, errors: ['没有找到 [ScriptType(...)] 特性'] }
    if (arglists.length > 1) {
        return {
            params: {},
            errors: [`找到 ${arglists.length} 处 [ScriptType(...)]，要求每个 .cs 文件只能有一处`]
        }
    }
    if (arglists[0] === null) return { params: {}, errors: ['[ScriptType] 缺少参数列表'] }

    return parseArglist(arglists[0], collectConstants(stripped))
}
