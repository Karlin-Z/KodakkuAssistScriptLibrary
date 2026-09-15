/**
 * KodakkuAssistScriptLibrary PR 自动审核。
 *
 * 审核规则（见 README.md）：
 *   1. 只允许改动「以 PR 作者的 GitHub 用户名命名」的第一层文件夹；
 *   2. 只允许新增 / 修改 / 删除 .cs 文件（重命名要求两端都是 .cs）；
 *   3. 每个 .cs 必须包含且只包含一处 [ScriptType(...)] 特性，字段合法：
 *      Name / Guid / Version / Author / Repo / DownloadUrl / Note / UpdateInfo / TerritoryIds
 *      对应插件里的 KodakkuAssist.Script.OnlineScriptInfo（Interface/ScriptAttribute.cs）。
 *
 * 零依赖，只用 Node 内置模块；只通过网络 API 读取 PR 内容，从不执行 PR 中的代码，
 * 也不会编译 C#——[ScriptType(...)] 是当作文本解析的（复用 csharp_meta.mjs）。
 *
 * 由 pr_review.py 移植而来（Python 版已随迁移移除），行为需保持不变，错误文案也算在内。
 */

import fs from 'node:fs'
import path from 'node:path'
import process from 'node:process'
import { parseArgs } from 'node:util'
import { fileURLToPath, pathToFileURL } from 'node:url'

import { extractScriptType, pyRepr } from './csharp_meta.mjs'

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.dirname(path.dirname(SCRIPT_DIR))

const CONFIG_PATH = path.join(SCRIPT_DIR, 'pr_review_rules.json')

// --------------------------------------------------------------------------- //
// 规则常量
// --------------------------------------------------------------------------- //

/** 生成的 OnlineRepo.json 里允许出现的字段 */
export const ALLOWED_FIELDS = new Set([
  'Name', 'Guid', 'Version', 'Author', 'Repo',
  'DownloadUrl', 'Note', 'UpdateInfo', 'UpdateTime', 'TerritoryIds',
])
const REQUIRED_STRING_FIELDS = ['Name', 'Guid', 'Version', 'Author', 'DownloadUrl']
const OPTIONAL_STRING_FIELDS = ['Repo', 'Note', 'UpdateInfo', 'UpdateTime']
const NON_EMPTY_FIELDS = ['Name', 'Guid', 'Version', 'Author']

// 插件用 NuGetVersion 解析 Version（ScriptManager.cs / ScriptBrowserColumn.cs），
// 非法值会在 UI 渲染时抛异常，因此这里按 NuGet 版本号格式校验。
// Python 的 \d 是 Unicode 感知的（等价于 \p{Nd}），这里用 \p{Nd} 保持等价。
const VERSION_RE = /^[\p{Nd}]+(\.[\p{Nd}]+){0,3}(-[0-9A-Za-z.\-]+)?(\+[0-9A-Za-z.\-]+)?$/u
const UUID_RE = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/
const URL_RE = /^https?:\/\/[^\s]+$/
// merge_repos 写入的 UpdateTime（UTC ISO8601），由 .cs 的最后提交时间生成
const UPDATE_TIME_RE = /^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$/

// OnlineScriptInfo.TerritoryIds 是 HashSet<uint>
export const UINT_MAX = 0xFFFFFFFF

// Name / Author 会被插件拼成保存文件名（"Name_Author.cs"），必须能安全用作文件名
const INVALID_FILENAME_CHARS = new Set('<>:"/\\|?*'.split(''))
const WINDOWS_RESERVED_NAMES = new Set([
  'CON', 'PRN', 'AUX', 'NUL',
  ...Array.from({ length: 9 }, (_, i) => `COM${i + 1}`),
  ...Array.from({ length: 9 }, (_, i) => `LPT${i + 1}`),
])

// C# 侧 author 参数的默认值，等同于「没写作者」
const AUTHOR_DEFAULT = 'Unknown'
const NAME_DEFAULT = 'Default Script'
const VERSION_DEFAULT = '0.0.0.1'

// 已提交的 guid -> 源文件 映射（由 merge_repos.mjs 生成），用于 PR 阶段查重
export const GUID_MAP_PATH = '.github/index/guid-map.json'

// 单个文件大小上限
const MAX_FILE_BYTES = 2 * 1024 * 1024

export const DEFAULT_CONFIG = {
  // 用户名与文件夹名比较时是否忽略大小写（GitHub 用户名本身不区分大小写）
  username_case_insensitive: true,
  // 是否允许在 <用户名>/ 下再建子目录
  allow_subfolders: true,
  // 贡献者文件夹里允许的文件扩展名
  allowed_extensions: ['.cs'],
  // 单个 PR 允许改动的文件数上限
  max_files_per_pr: 100,
  // 不参与索引的目录名（任意层级匹配），例如放模板的 examples / templates
  ignore_dirs: [],
  // 白名单账号：列表中的登录名不受「只能改自己文件夹」限制（仍会校验内容）。默认空。
  maintainers: [],
  // 生成结果（OnlineRepo.json）里出现未知字段时的处理：warn / error / ignore
  unknown_fields: 'warn',
}

const STATUS_LABEL = {
  added: '新增',
  modified: '修改',
  removed: '删除',
  renamed: '重命名',
  copied: '复制',
  changed: '变更',
  unchanged: '未变',
}

// --------------------------------------------------------------------------- //
// 工具
// --------------------------------------------------------------------------- //

/** 输出一行到标准输出（对应 Python 的 print）。 */
function println(text) {
  process.stdout.write(String(text) + '\n')
}

/**
 * 判断一个码点是否属于 Python str.isspace() 的空白集合。
 *
 * 与 JS 的 \s / trim() 并不完全一致：Python 会去掉 \x1c-\x1f、\x85，却不会去掉
 * \ufeff；JS 恰好相反。这里显式列出，保证与 Python 的 strip() 等价。
 */
function isPySpace(code) {
  return (
    (code >= 0x09 && code <= 0x0d) ||
    (code >= 0x1c && code <= 0x1f) ||
    code === 0x20 || code === 0x85 || code === 0xa0 || code === 0x1680 ||
    (code >= 0x2000 && code <= 0x200a) ||
    code === 0x2028 || code === 0x2029 || code === 0x202f || code === 0x205f ||
    code === 0x3000
  )
}

/** 复刻 Python 的 str.strip()（无参数，按 str.isspace() 判定）。 */
function pyStrip(text) {
  const chars = Array.from(text)
  let start = 0
  let end = chars.length
  while (start < end && isPySpace(chars[start].codePointAt(0))) start += 1
  while (end > start && isPySpace(chars[end - 1].codePointAt(0))) end -= 1
  return chars.slice(start, end).join('')
}

/** 复刻 Python 的 repr()，覆盖这里会遇到的取值类型（字符串走 csharp_meta.pyRepr）。 */
function pyReprAny(value) {
  if (typeof value === 'string') return pyRepr(value)
  if (value === null || value === undefined) return 'None'
  if (typeof value === 'boolean') return value ? 'True' : 'False'
  if (typeof value === 'bigint') return value.toString()
  if (typeof value === 'number') {
    return Number.isInteger(value) ? String(value) : pyFloatRepr(value)
  }
  if (Array.isArray(value)) return `[${value.map(pyReprAny).join(', ')}]`
  return pyRepr(String(value))
}

/** 复刻 Python 的 float repr（仅用于极少数错误文案，保持基本格式）。 */
export function pyFloatRepr(value) {
  if (Number.isNaN(value)) return 'nan'
  if (value === Infinity) return 'inf'
  if (value === -Infinity) return '-inf'
  if (Number.isInteger(value)) {
    // Python repr(1.0) == '1.0'
    return Object.is(value, -0) ? '-0.0' : `${value}.0`
  }
  return String(value)
}

/**
 * JSON 里写成小数形态的值（`0.0`、`1e3` 这种）。
 *
 * 必须和整数区分开：Python 的 `0.0` 是 float，校验时按「小数」拒绝；而 JS 的 Number
 * 分不出 `0.0` 与 `0`，`Number.isInteger(0.0)` 仍为 true，会让非法的 `TerritoryIds: [1.0]`
 * 被当成合法整数放行。用 Number 子类承载即可：比较与算术走 valueOf 与数值一致，
 * 但 typeof 是 object，于是整数类型检查会正确地判它不合法。
 */
export class PyFloat extends Number {}

/** 把 JS/Python 类型翻译成中文，便于写错误信息（对应 type_name）。 */
export function type_name(value) {
  if (value === null || value === undefined) return 'null'
  if (value instanceof PyFloat) return '小数'
  const t = typeof value
  if (t === 'boolean') return '布尔值'
  if (t === 'string') return '字符串'
  if (t === 'bigint') return '整数'
  if (t === 'number') return Number.isInteger(value) ? '整数' : '小数'
  if (Array.isArray(value)) return '数组'
  if (t === 'object') return '对象'
  return t
}

/** 返回默认配置的深拷贝。 */
export function default_config() {
  return structuredClone(DEFAULT_CONFIG)
}

/** 返回默认配置的深拷贝（merge_repos / selftest 契约名）。 */
export function defaultConfig() {
  return default_config()
}

/** 读取 pr_review_rules.json，覆盖默认配置；文件缺失或损坏时退回默认值。 */
export function load_config() {
  const cfg = default_config()
  let raw
  try {
    raw = fs.readFileSync(CONFIG_PATH, 'utf8')
  } catch (err) {
    if (err && err.code === 'ENOENT') return cfg
    println(`::warning::无法读取审核规则配置 ${CONFIG_PATH}: ${pyOsErrorString(err)}`)
    return cfg
  }
  try {
    const user_cfg = pyJsonLoads(raw)
    if (isPlainObject(user_cfg)) Object.assign(cfg, user_cfg)
  } catch (err) {
    println(`::warning::无法读取审核规则配置 ${CONFIG_PATH}: ${err}`)
  }
  return cfg
}

/** load_config 的契约名。 */
export function loadConfig() {
  return load_config()
}

/** 规范化登录名以便比较；GitHub 用户名本身不区分大小写。 */
export function normalize_login(login, case_insensitive) {
  const text = pyStrip(login || '')
  return case_insensitive ? text.toLowerCase() : text
}

/** 对应 Python 的 str.rstrip(" .")：去掉结尾的空格与点。 */
function rstripSpaceDot(text) {
  let end = text.length
  while (end > 0 && (text[end - 1] === ' ' || text[end - 1] === '.')) end -= 1
  return text.slice(0, end)
}

/**
 * Name / Author 会被插件拼成保存文件名：
 *
 *     Path.Combine(saveFolder, $"{info.Name}_{info.Author}.cs")   # ScriptManager.cs
 *
 * 含路径分隔符或 `..` 时可能写到 ScriptCache 目录之外，含非法字符时直接失败。
 */
export function check_filename_component(value, label, errors) {
  if (Array.from(value).some((ch) => ch.codePointAt(0) < 32)) {
    errors.push(`${label} 含有控制字符，不能用作文件名`)
  }
  const bad = Array.from(new Set(Array.from(value).filter((ch) => INVALID_FILENAME_CHARS.has(ch)))).sort()
  if (bad.length) {
    errors.push(
      `${label} 含有不能用于文件名的字符：${bad.join(' ')}` +
      `（插件用「Name_Author.cs」作为保存文件名）`
    )
  }
  if (value !== pyStrip(value)) {
    errors.push(`${label} 首尾有多余空白`)
  }
  const stem = rstripSpaceDot(value)
  if (stem !== value) {
    errors.push(`${label} 不能以点或空格结尾`)
  } else if (WINDOWS_RESERVED_NAMES.has(stem.toUpperCase())) {
    errors.push(`${label} 是 Windows 保留设备名，不能用作文件名`)
  }
}

// --------------------------------------------------------------------------- //
// 规则 1 + 2：路径校验
// --------------------------------------------------------------------------- //

/** 校验单个路径是否满足「自己的文件夹 / 只允许指定扩展名」两条规则。 */
export function check_path_rules(filePath, author, cfg, errors, ctx) {
  if (typeof filePath !== 'string' || !filePath) {
    errors.push(`${ctx}：路径为空或非法`)
    return
  }

  if (filePath.startsWith('/') || filePath.includes('\\')) {
    errors.push(`${ctx}：${pyReprAny(filePath)} 不是合法的仓库相对路径`)
    return
  }

  const parts = filePath.split('/')
  if (parts.some((part) => part === '' || part === '.' || part === '..')) {
    errors.push(`${ctx}：${pyReprAny(filePath)} 包含非法路径片段`)
    return
  }

  if (parts.length < 2) {
    errors.push(
      `${ctx}：文件 ${pyReprAny(filePath)} 必须放在以你的 GitHub 用户名命名的文件夹里，` +
      `例如 ${author}/MyScript.cs`
    )
    return
  }

  const folder = parts[0]
  const same = normalize_login(folder, cfg.username_case_insensitive) ===
    normalize_login(author, cfg.username_case_insensitive)
  if (!same) {
    errors.push(
      `${ctx}：第一层文件夹 ${pyReprAny(folder)} 与 PR 作者 ${pyReprAny(author)} 不一致；` +
      `你只能修改自己的文件夹 ${author}/`
    )
  }

  if (!(cfg.allow_subfolders ?? true) && parts.length > 2) {
    errors.push(`${ctx}：不允许在 ${folder}/ 下创建子文件夹（${pyReprAny(filePath)}）`)
  }

  const extensions = (cfg.allowed_extensions ?? ['.cs']).map((e) => String(e).toLowerCase())
  const filename = parts[parts.length - 1]
  const filenameLower = filename.toLowerCase()
  if (!extensions.some((ext) => filenameLower.endsWith(ext))) {
    errors.push(
      `${ctx}：只允许 ${extensions.join(' / ')} 文件，${pyReprAny(filename)} 不合法`
    )
  }
}

/** 契约名：就地往 collected 数组里 push 错误字符串。 */
export function checkPathRules(filePath, author, cfg, collected, label) {
  check_path_rules(filePath, author, cfg, collected, label)
}

// --------------------------------------------------------------------------- //
// 规则 3：.cs 里的 ScriptType 特性
// --------------------------------------------------------------------------- //

/** 从 params 里取 [kind, value]，缺失时等价于 Python 的 (None, None)。 */
function getParam(params, key) {
  return Object.hasOwn(params, key) ? params[key] : [null, null]
}

/** 校验一个 .cs 文件，返回 [meta, errors, warnings]。有错误时 meta 为 None。 */
export function validate_script_file(text, rel_path, folder_owner, cfg) {
  const errors = []
  const warnings = []

  const err = (msg) => errors.push(`${rel_path}：${msg}`)
  const warn = (msg) => warnings.push(`${rel_path}：${msg}`)

  const { params, errors: parse_errors } = extractScriptType(text)
  if (parse_errors.length) {
    for (const item of parse_errors) err(item)
    return [null, errors, warnings]
  }

  // 参数值解析不出来时直接报原文，避免后面用「实际是null」这类看不懂的措辞
  for (const [field, entry] of Object.entries(params)) {
    const [kind, value] = entry
    if (kind === 'unknown') {
      if (field === 'territorys') {
        err(`territorys 必须是 uint 数组字面量（如 [1226, 1228]），原文：${pyReprAny(value)}`)
      } else {
        err(`${field} 无法解析，原文：${pyReprAny(value)}`)
      }
    } else if (kind === 'identifier') {
      if (field === 'note' || field === 'updateInfo') {
        warn(
          `${field} 引用了标识符 ${pyReprAny(value)}，但本文件里找不到对应的 const 声明，` +
          `将按空字符串处理`
        )
        delete params[field]
      } else {
        err(
          `${field} 引用了标识符 ${pyReprAny(value)}，但本文件里找不到对应的 const 声明` +
          `（只解析本文件内的 const）`
        )
      }
    }
  }
  if (errors.length) return [null, errors, warnings]

  // guid：构造函数第一个参数，没有默认值
  let guid = ''
  {
    const [kind, value] = getParam(params, 'guid')
    if (kind === null) {
      err('缺少 guid 参数')
    } else if (kind !== 'string') {
      err(`guid 必须是字符串，实际是${type_name(value)}`)
    } else if (!pyStrip(String(value))) {
      err('guid 不能为空')
    } else {
      guid = pyStrip(String(value))
    }
  }

  // name
  let name = NAME_DEFAULT
  {
    const [kind, value] = getParam(params, 'name')
    if (kind === null) {
      warn(`没有写 name，将使用插件默认的 ${pyReprAny(NAME_DEFAULT)}`)
    } else if (kind !== 'string') {
      err(`name 必须是字符串，实际是${type_name(value)}`)
    } else if (!pyStrip(String(value))) {
      err('name 不能为空')
    } else {
      name = String(value)
    }
  }

  // version
  let version = VERSION_DEFAULT
  {
    const [kind, value] = getParam(params, 'version')
    if (kind === null) {
      warn(`没有写 version，将使用插件默认的 ${pyReprAny(VERSION_DEFAULT)}`)
    } else if (kind !== 'string') {
      err(`version 必须是字符串，实际是${type_name(value)}`)
    } else if (!VERSION_RE.test(pyStrip(String(value)))) {
      err(
        `version ${pyReprAny(String(value))} 不是合法的版本号 ` +
        `（插件用 NuGetVersion 解析，如 0.0.1 / 0.0.0.9 / 1.0.0-beta）`
      )
    } else {
      version = pyStrip(String(value))
    }
  }

  // author：未写或写成默认值 Unknown 时，回退到文件夹名（即 GitHub 用户名）
  let author = ''
  {
    const [kind, value] = getParam(params, 'author')
    if (kind === null) {
      author = folder_owner
      warn(`没有写 author，改用文件夹名 ${pyReprAny(folder_owner)}`)
    } else if (kind !== 'string') {
      err(`author 必须是字符串，实际是${type_name(value)}`)
    } else {
      const candidate = pyStrip(String(value))
      if (!candidate || candidate === AUTHOR_DEFAULT) {
        author = folder_owner
        warn(
          `author 是默认值 ${pyReprAny(candidate || '空')}，改用文件夹名 ${pyReprAny(folder_owner)}`
        )
      } else {
        author = candidate
      }
    }
  }

  // territorys
  const territorys = []
  {
    const [kind, value] = getParam(params, 'territorys')
    if (kind === null) {
      warn('没有写 territorys，该脚本不会按地图过滤')
    } else if (kind === 'null') {
      warn('territorys 写成了 null，等同于空数组')
    } else if (kind !== 'array') {
      err(`territorys 必须是 uint 数组字面量，实际是${type_name(value)}`)
    } else {
      value.forEach((tid, pos) => {
        if (
          typeof tid === 'boolean' ||
          !(typeof tid === 'bigint' || (typeof tid === 'number' && Number.isInteger(tid))) ||
          !(0 <= tid && tid <= UINT_MAX)
        ) {
          err(`territorys[${pos}] = ${pyReprAny(tid)} 超出 uint 范围（0~${UINT_MAX}）`)
        } else {
          territorys.push(tid)
        }
      })
    }
  }

  // note / updateInfo
  let note = ''
  {
    const [kind, value] = getParam(params, 'note')
    if (kind !== null && kind !== 'string') {
      err(`note 必须是字符串，实际是${type_name(value)}`)
    } else if (kind === 'string') {
      note = String(value)
    }
  }

  let update_info = ''
  {
    const [kind, value] = getParam(params, 'updateInfo')
    if (kind !== null && kind !== 'string') {
      err(`updateInfo 必须是字符串，实际是${type_name(value)}`)
    } else if (kind === 'string') {
      update_info = String(value)
    }
  }

  // 插件用 "{Name}_{Author}.cs" 作为保存文件名
  if (pyStrip(name)) {
    check_filename_component(name, `${rel_path} 的 name`, errors)
  }
  if (pyStrip(author)) {
    check_filename_component(author, `${rel_path} 的 author`, errors)
  }

  if (guid && !UUID_RE.test(guid)) {
    warn(`guid ${pyReprAny(guid)} 不是标准 UUID 格式；插件按字符串处理仍可用，但建议修正`)
  }

  if (errors.length) return [null, errors, warnings]

  return [{
    guid,
    name,
    version,
    author,
    note,
    update_info,
    territorys,
  }, errors, warnings]
}

/** 契约名：返回对象 {meta, errors, warnings}。 */
export function validateScriptFile(text, relPath, owner, cfg) {
  const [meta, errors, warnings] = validate_script_file(text, relPath, owner, cfg)
  return { meta, errors, warnings }
}

// --------------------------------------------------------------------------- //
// 生成的 OnlineRepo.json 自检
// --------------------------------------------------------------------------- //

/** 判断是否为普通对象（对应 Python 的 isinstance(x, dict)，不含数组 / null）。 */
function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
}

/** 校验生成出来的 OnlineRepo.json（OnlineScriptInfo 数组），返回 [errors, warnings]。 */
export function validate_json_document(text, jsonPath, cfg) {
  const errors = []
  const warnings = []

  if (text.startsWith('\ufeff')) {
    warnings.push(`${jsonPath}：文件带有 UTF-8 BOM，已忽略`)
    text = text.slice(1)
  }

  if (!pyStrip(text)) {
    errors.push(`${jsonPath}：文件内容为空`)
    return [errors, warnings]
  }

  let data
  try {
    data = pyJsonLoads(text)
  } catch (exc) {
    errors.push(
      `${jsonPath}：JSON 语法错误 —— ${exc.msg}（第 ${exc.lineno} 行第 ${exc.colno} 列）`
    )
    return [errors, warnings]
  }

  if (!Array.isArray(data)) {
    errors.push(`${jsonPath}：顶层必须是数组 [ ... ]，实际是${type_name(data)}`)
    return [errors, warnings]
  }

  if (!data.length) {
    warnings.push(`${jsonPath}：顶层数组为空，没有任何脚本条目`)
  }

  const guid_seen = new Map()
  const name_seen = new Map()
  data.forEach((entry, idx) => {
    validate_entry(idx, entry, jsonPath, cfg, errors, warnings, guid_seen, name_seen)
  })
  return [errors, warnings]
}

/** 契约名：返回对象 {errors, warnings}。 */
export function validateJsonDocument(text, filename, cfg) {
  const [errors, warnings] = validate_json_document(text, filename, cfg)
  return { errors, warnings }
}

/** 校验生成结果里的单个条目，并把 guid / name 记入去重表。 */
export function validate_entry(idx, entry, jsonPath, cfg, errors, warnings, guid_seen, name_seen) {
  const label = `${jsonPath}[${idx}]`

  if (!isPlainObject(entry)) {
    errors.push(`${label}：每一项必须是 JSON 对象，实际是${type_name(entry)}`)
    return
  }

  for (const field of REQUIRED_STRING_FIELDS) {
    if (!Object.hasOwn(entry, field)) {
      errors.push(`${label}：缺少必填字段 ${field}`)
    } else if (typeof entry[field] !== 'string') {
      errors.push(`${label}：${field} 必须是字符串，实际是${type_name(entry[field])}`)
    }
  }

  for (const field of OPTIONAL_STRING_FIELDS) {
    if (Object.hasOwn(entry, field) && typeof entry[field] !== 'string') {
      errors.push(`${label}：${field} 必须是字符串，实际是${type_name(entry[field])}`)
    }
  }

  for (const field of NON_EMPTY_FIELDS) {
    const value = entry[field]
    if (typeof value === 'string' && !pyStrip(value)) {
      errors.push(`${label}：${field} 不能为空`)
    }
  }

  const version = entry.Version
  if (typeof version === 'string' && pyStrip(version) && !VERSION_RE.test(pyStrip(version))) {
    errors.push(`${label}：Version ${pyReprAny(version)} 不是合法的版本号`)
  }

  const guid = entry.Guid
  if (typeof guid === 'string' && pyStrip(guid)) {
    if (!UUID_RE.test(pyStrip(guid))) {
      warnings.push(`${label}：Guid ${pyReprAny(guid)} 不是标准 UUID 格式`)
    }
    const key = pyStrip(guid).toLowerCase()
    if (guid_seen.has(key)) {
      errors.push(`${label}：Guid 与 ${jsonPath}[${guid_seen.get(key)}] 重复`)
    } else {
      guid_seen.set(key, idx)
    }
  }

  const name = entry.Name
  if (typeof name === 'string' && pyStrip(name)) {
    if (name_seen.has(pyStrip(name))) {
      warnings.push(`${label}：Name 与 ${jsonPath}[${name_seen.get(pyStrip(name))}] 重复`)
    } else {
      name_seen.set(pyStrip(name), idx)
    }
  }

  // TerritoryIds 可以省略，等同于空数组（脚本在所有区域生效）
  const territory_ids = entry.TerritoryIds
  if (territory_ids !== null && territory_ids !== undefined && !Array.isArray(territory_ids)) {
    errors.push(
      `${label}：TerritoryIds 必须是整数数组，实际是${type_name(territory_ids)}`
    )
  } else if (Array.isArray(territory_ids)) {
    territory_ids.forEach((tid, pos) => {
      if (typeof tid === 'boolean' || !(typeof tid === 'bigint' || (typeof tid === 'number' && Number.isInteger(tid)))) {
        errors.push(`${label}：TerritoryIds[${pos}] 必须是整数，实际是${type_name(tid)}`)
      } else if (!(0 <= tid && tid <= UINT_MAX)) {
        errors.push(`${label}：TerritoryIds[${pos}] = ${tid} 超出 uint 范围`)
      }
    })
  }

  const download = entry.DownloadUrl
  if (typeof download === 'string' && pyStrip(download) && !URL_RE.test(pyStrip(download))) {
    errors.push(`${label}：DownloadUrl 必须以 http:// 或 https:// 开头`)
  }

  const update_time = entry.UpdateTime
  if (
    typeof update_time === 'string' && pyStrip(update_time) &&
    !UPDATE_TIME_RE.test(pyStrip(update_time))
  ) {
    warnings.push(
      `${label}：UpdateTime ${pyReprAny(update_time)} 不是 UTC ISO8601（形如 2026-09-12T05:45:00Z）`
    )
  }

  const unknown = Object.keys(entry).filter((key) => !ALLOWED_FIELDS.has(key)).sort()
  if (unknown.length) {
    const message = `${label}：存在未知字段 ${unknown.join(', ')}`
    const mode = cfg.unknown_fields ?? 'warn'
    if (mode === 'error') {
      errors.push(message)
    } else if (mode === 'warn') {
      warnings.push(message)
    }
  }
}

// --------------------------------------------------------------------------- //
// GitHub API
// --------------------------------------------------------------------------- //

const API_URL = process.env.GITHUB_API_URL ?? 'https://api.github.com'

/** 构造 GitHub API 请求头，带 token 时附带 Authorization。 */
function _api_headers(accept) {
  const token = process.env.GITHUB_TOKEN || process.env.GH_TOKEN || ''
  const headers = {
    Accept: accept,
    'X-GitHub-Api-Version': '2022-11-28',
    'User-Agent': 'kasl-pr-reviewer',
  }
  if (token) {
    headers.Authorization = `Bearer ${token}`
  }
  return headers
}

/** 对应 urllib.parse.quote，保留 safe 里的字符。 */
function pyQuote(text, safe = '') {
  const unreserved = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_.-~'
  let out = ''
  for (const ch of text) {
    if (unreserved.includes(ch) || safe.includes(ch)) {
      out += ch
    } else {
      for (const byte of Buffer.from(ch, 'utf8')) {
        out += `%${byte.toString(16).toUpperCase().padStart(2, '0')}`
      }
    }
  }
  return out
}

/** HTTP 错误（对应 urllib.error.HTTPError）。 */
class HttpError extends Error {
  constructor(status, statusText) {
    super(`HTTP Error ${status}: ${statusText}`)
    this.name = 'HTTPError'
    this.code = status
  }
}

/** GET 一个 API 路径并解析 JSON。 */
async function api_json(api_path) {
  const response = await fetch(`${API_URL}${api_path}`, {
    headers: _api_headers('application/vnd.github+json'),
    signal: AbortSignal.timeout(30000),
  })
  if (!response.ok) throw new HttpError(response.status, response.statusText)
  const body = Buffer.from(await response.arrayBuffer())
  return body.length ? pyJsonLoads(body.toString('utf8')) : null
}

/** GET 一个 API 路径并取原始字节。 */
async function api_raw(api_path) {
  const response = await fetch(`${API_URL}${api_path}`, {
    headers: _api_headers('application/vnd.github.raw'),
    signal: AbortSignal.timeout(30000),
  })
  if (!response.ok) throw new HttpError(response.status, response.statusText)
  return Buffer.from(await response.arrayBuffer())
}

/** 分页拉取 PR 改动的文件列表。 */
export async function list_changed_files(repo, pr_number) {
  const files = []
  let page = 1
  for (;;) {
    const batch = await api_json(`/repos/${repo}/pulls/${pr_number}/files?per_page=100&page=${page}`)
    if (!batch) break
    files.push(...batch)
    if (batch.length < 100) break
    page += 1
  }
  return files
}

/** 对应 Python 的 base64.b64decode：非法内容时抛错（这里近似处理填充）。 */
function pyB64Decode(text) {
  const cleaned = String(text).replace(/[^A-Za-z0-9+/=]/g, '')
  if (cleaned.length % 4 !== 0 || /=/.test(cleaned.slice(0, -2)) || (cleaned.includes('=') && !/=+$/.test(cleaned))) {
    throw new Error('Invalid base64-encoded string')
  }
  return Buffer.from(cleaned, 'base64')
}

/** 读取 PR 中某个文件的原始字节。返回 [data, error_message]。 */
export async function fetch_file_bytes(repo, filePath, ref) {
  const quoted = pyQuote(filePath, '/')
  const ref_q = pyQuote(ref, '')
  const info = await api_json(`/repos/${repo}/contents/${quoted}?ref=${ref_q}`)
  if (!isPlainObject(info) || info.type !== 'file') {
    const kind = isPlainObject(info) ? info.type : type_name(info)
    return [null, `只允许普通文件（不允许符号链接 / 子模块 / 目录），实际类型为 ${kind}`]
  }
  const size = info.size || 0
  if (size > MAX_FILE_BYTES) {
    return [null, `文件过大（${size} 字节，上限 ${MAX_FILE_BYTES} 字节）`]
  }
  if (info.encoding === 'base64' && info.content) {
    try {
      return [pyB64Decode(info.content), null]
    } catch (exc) {
      return [null, `内容解码失败：${exc}`]
    }
  }
  // 超过 1 MB 时 contents API 不返回 content，改用 raw 媒体类型
  return [await api_raw(`/repos/${repo}/contents/${quoted}?ref=${ref_q}`), null]
}

/** 读取已提交的 guid -> 源文件 映射。不存在时返回 None（表示本次无法查重）。 */
export async function load_guid_map(repo, ref) {
  if (!ref) return null
  const quoted = pyQuote(GUID_MAP_PATH, '/')
  let info
  try {
    info = await api_json(`/repos/${repo}/contents/${quoted}?ref=${pyQuote(ref, '')}`)
  } catch (exc) {
    if (exc instanceof HttpError && exc.code === 404) return null
    throw exc
  }
  if (!isPlainObject(info) || !info.content) return null
  let data
  try {
    data = pyJsonLoads(pyB64Decode(info.content).toString('utf8'))
  } catch {
    return null
  }
  if (!isPlainObject(data)) return null
  const out = {}
  for (const [key, value] of Object.entries(data)) {
    out[String(key).toLowerCase()] = String(value)
  }
  return out
}

/** Python dict / JS Map / 普通对象 的「是否为空」。 */
function mappingIsEmpty(mapping) {
  if (!mapping) return true
  if (mapping instanceof Map) return mapping.size === 0
  if (Array.isArray(mapping)) return mapping.length === 0
  if (typeof mapping === 'object') return Object.keys(mapping).length === 0
  return true
}

/** Python dict.get(key)（兼容 Map 与普通对象）。 */
function mappingGet(mapping, key) {
  if (!mapping) return undefined
  if (mapping instanceof Map) return mapping.get(key)
  return Object.hasOwn(mapping, key) ? mapping[key] : undefined
}

/** 判断 own_paths（set）里是否包含 path。 */
function ownPathsHas(own_paths, candidate) {
  if (!own_paths) return false
  if (own_paths instanceof Set) return own_paths.has(candidate)
  if (Array.isArray(own_paths)) return own_paths.includes(candidate)
  if (typeof own_paths === 'object') return Object.hasOwn(own_paths, candidate)
  return false
}

/**
 * 检查 guid 是否与已有脚本或同一 PR 内的其它文件冲突。
 *
 * script_guids: [[路径, guid]]
 * own_paths:    本次 PR 涉及的全部路径（含重命名前路径），用于放行改名/移动
 * guid_map:     已有的 guid -> 源文件；None 表示拿不到、本次不查重
 */
export function check_guid_collisions(script_guids, own_paths, author, guid_map, cfg) {
  const errors = []

  const seen = new Map()
  for (const [scriptPath, guid] of script_guids) {
    const key = guid.toLowerCase()
    if (seen.has(key)) {
      errors.push(
        `${scriptPath}：guid ${guid} 与本次 PR 里的 ${seen.get(key)} 重复；` +
        `同一 guid 只会保留一个脚本`
      )
    } else {
      seen.set(key, scriptPath)
    }
  }

  if (mappingIsEmpty(guid_map)) {
    return errors
  }

  for (const [scriptPath, guid] of script_guids) {
    const mapped = mappingGet(guid_map, guid.toLowerCase())
    // mapped in own_paths：作者在改名 / 移动自己的文件，不算冲突
    if (!mapped || mapped === scriptPath || ownPathsHas(own_paths, mapped)) {
      continue
    }
    const folder = mapped.includes('/') ? mapped.split('/')[0] : mapped
    const same_owner = normalize_login(folder, cfg.username_case_insensitive) ===
      normalize_login(author, cfg.username_case_insensitive)
    if (same_owner) {
      errors.push(
        `${scriptPath}：guid ${guid} 已被你自己文件夹里的 ${mapped} 使用；` +
        `同一 guid 只会保留一个脚本，请换成互不相同的 guid`
      )
    } else {
      errors.push(
        `${scriptPath}：guid ${guid} 已被 ${mapped} 使用；` +
        `插件把相同 guid 视为同一个脚本，请换成唯一的 guid`
      )
    }
  }
  return errors
}

/** 契约名。 */
export function checkGuidCollisions(entries, renamedFiles, author, guidMap, cfg) {
  return check_guid_collisions(entries, renamedFiles, author, guidMap, cfg)
}

// --------------------------------------------------------------------------- //
// 审核主流程
// --------------------------------------------------------------------------- //

/** 路径层面校验。返回 [errors, warnings, rows]。 */
export function check_change_set(files, author, cfg) {
  const errors = []
  const warnings = []
  const rows = []

  const max_files = cfg.max_files_per_pr ?? 100
  if (files.length > max_files) {
    errors.push(`本 PR 改动了 ${files.length} 个文件，超过上限 ${max_files} 个`)
  }

  // 文件夹名如果被 ignore_dirs 排除，PR 通过也永远不会被收录，提前说清楚
  const ignore_dirs = new Set((cfg.ignore_dirs || []).map((d) => normalize_login(String(d), true)))
  if (ignore_dirs.has(normalize_login(author, true))) {
    errors.push(
      `你的文件夹 ${author}/ 在 ignore_dirs 排除名单里，不会被收录；请联系维护者`
    )
  }

  const bypass = (cfg.maintainers || []).includes(author)
  if (bypass) {
    warnings.push(`${author} 在维护者白名单中，跳过「只能修改自己文件夹」限制`)
  }

  for (const item of files) {
    const status = item.status ?? 'changed'
    const filePath = item.filename ?? ''
    const previous = item.previous_filename ?? null
    const label = STATUS_LABEL[status] ?? status
    rows.push([label, filePath])

    if (!bypass) {
      check_path_rules(filePath, author, cfg, errors, `文件 ${filePath}`)
    }

    // 重命名 / 复制：来源路径同样必须合法，否则等于绕过限制
    if ((status === 'renamed' || status === 'copied') && previous) {
      const prefix = status === 'renamed' ? '重命名前' : '复制来源'
      rows.push([`${label}（来源）`, previous])
      if (!bypass) {
        check_path_rules(previous, author, cfg, errors, `文件 ${previous}（${prefix}路径）`)
      }
    }
  }

  return [errors, warnings, rows]
}

/** 契约名：返回对象 {errors, warnings, rows}。 */
export function checkChangeSet(files, author, cfg) {
  const [errors, warnings, rows] = check_change_set(files, author, cfg)
  return { errors, warnings, rows }
}

/** 生成报告正文：改动清单 + 错误 + 提醒。 */
export function describe(files, errors, warnings) {
  const lines = ['### 改动清单', '', '| 状态 | 文件 |', '| --- | --- |']
  for (const [label, filePath] of files) {
    lines.push(`| ${label} | \`${filePath}\` |`)
  }
  lines.push('')

  if (errors.length) {
    lines.push('### ❌ 需要修复')
    lines.push('')
    errors.forEach((item, index) => {
      lines.push(`${index + 1}. ${item}`)
    })
    lines.push('')
  }

  if (warnings.length) {
    lines.push('### ⚠️ 提醒（不影响合并）')
    lines.push('')
    for (const item of warnings) {
      lines.push(`- ${item}`)
    }
    lines.push('')
  }

  if (!errors.length && !warnings.length) {
    lines.push('未发现问题。')
    lines.push('')
  }

  lines.push(
    '> 规则：只能修改以自己 GitHub 用户名命名的文件夹，只能新增 / 修改 / 删除 `.cs`，' +
    '每个 `.cs` 必须包含且只包含一处 `[ScriptType(...)]`。详见 `README.md`。'
  )
  return lines.join('\n')
}

/** 拼出完整的 PR 评论内容。 */
export function build_report(title, rows, errors, warnings) {
  return [`## 🤖 PR 自动审核：${title}`, '', describe(rows, errors, warnings)].join('\n')
}

/** 追加到 Actions 运行摘要；不在 Actions 环境下静默跳过。 */
export function write_step_summary(markdown) {
  const summary_path = process.env.GITHUB_STEP_SUMMARY
  if (summary_path) {
    try {
      fs.appendFileSync(summary_path, markdown + '\n', 'utf8')
    } catch {
      // OSError：静默跳过
    }
  }
}

/** 在 PR 下发一条评论。 */
export async function post_comment(repo, pr_number, body) {
  const response = await fetch(`${API_URL}/repos/${repo}/issues/${pr_number}/comments`, {
    method: 'POST',
    body: Buffer.from(JSON.stringify({ body }), 'utf8'),
    headers: _api_headers('application/vnd.github+json'),
    signal: AbortSignal.timeout(30000),
  })
  if (!response.ok) throw new HttpError(response.status, response.statusText)
  await response.arrayBuffer()
}

/** 在 PR 下留言；失败只告警，不影响审核结论。 */
export async function post_comment_safely(repo, pr_number, body) {
  try {
    await post_comment(repo, pr_number, body)
  } catch (exc) {
    println(`::warning::无法在 PR 下留言：${exc}`)
  }
}

/** CI 入口：审核 PR 并输出报告。返回 0 通过 / 1 未通过 / 2 无法执行。 */
export async function run_ci() {
  const repo = process.env.GITHUB_REPOSITORY ?? ''
  const pr_number = process.env.PR_NUMBER ?? ''
  const author = process.env.PR_AUTHOR ?? ''
  const head_sha = process.env.PR_HEAD_SHA ?? ''

  const missing = [
    ['GITHUB_REPOSITORY', repo], ['PR_NUMBER', pr_number], ['PR_AUTHOR', author],
  ].filter(([, value]) => !value).map(([key]) => key)
  if (missing.length) {
    println(`::error::缺少环境变量：${missing.join(', ')}`)
    return 2
  }

  const cfg = load_config()

  let files
  try {
    files = await list_changed_files(repo, pr_number)
  } catch (exc) {
    const message = `无法读取 PR 改动（GitHub API 错误）：${exc}`
    println(`::error::${message}`)
    write_step_summary(`## 🤖 PR 自动审核：无法执行\n\n${message}\n`)
    try {
      await post_comment(repo, pr_number, `## 🤖 PR 自动审核：无法执行\n\n${message}\n\n未做合并，请维护者手动检查。`)
    } catch {
      // 忽略
    }
    return 2
  }

  const script_guids = []
  let errors
  let warnings
  let rows
  if (!files.length) {
    errors = ['该 PR 没有包含任何文件改动']
    warnings = []
    rows = []
  } else {
    ;[errors, warnings, rows] = check_change_set(files, author, cfg)

    for (const item of files) {
      const filePath = item.filename ?? ''
      if (item.status === 'removed') continue
      let data
      let fetch_error
      try {
        ;[data, fetch_error] = await fetch_file_bytes(repo, filePath, head_sha)
      } catch (exc) {
        errors.push(`${filePath}：读取文件内容失败 —— ${exc}`)
        continue
      }
      if (fetch_error) {
        errors.push(`${filePath}：${fetch_error}`)
        continue
      }
      let text
      try {
        text = utf8DecodeStrict(data)
      } catch (exc) {
        errors.push(`${filePath}：不是合法的 UTF-8 编码（${exc}）`)
        continue
      }

      const folder_owner = filePath.includes('/') ? filePath.split('/')[0] : author
      const [meta, file_errors, file_warnings] = validate_script_file(text, filePath, folder_owner, cfg)
      errors.push(...file_errors)
      warnings.push(...file_warnings)
      if (meta) {
        script_guids.push([filePath, meta.guid])
      }
    }

    // guid 查重：拿仓库里已提交的映射表比对（改名 / 移动自己的文件不算冲突）
    const own_paths = new Set()
    for (const item of files) {
      for (const key of ['filename', 'previous_filename']) {
        if (item[key]) own_paths.add(item[key])
      }
    }

    const base_ref = process.env.PR_BASE_REF || process.env.PR_BASE_SHA || ''
    let guid_map = null
    try {
      guid_map = await load_guid_map(repo, base_ref)
    } catch (exc) {
      println(`::warning::无法读取 guid 映射表，本次跳过 guid 查重：${exc}`)
    }
    if (guid_map === null && base_ref) {
      println(`::notice::${GUID_MAP_PATH} 尚不存在，本次跳过 guid 查重`)
    }
    errors.push(...check_guid_collisions(script_guids, own_paths, author, guid_map, cfg))
  }

  if (errors.length) {
    const report = build_report('❌ 未通过', rows, errors, warnings)
    println(report)
    write_step_summary(report)
    await post_comment_safely(repo, pr_number, report)
    return 1
  }

  const report = build_report('✅ 通过', rows, errors, warnings)
  println(report)
  write_step_summary(report)
  if (warnings.length) {
    await post_comment_safely(repo, pr_number, report + '\n校验通过，将自动 squash 合并。')
  }
  return 0
}

// --------------------------------------------------------------------------- //
// 本地子命令
// --------------------------------------------------------------------------- //

/** 读取文件并解码为 UTF-8，返回 [文本, 退出码]；读取或解码失败时文本为 None。 */
export function read_utf8(filePath) {
  let data
  try {
    data = fs.readFileSync(filePath)
  } catch (exc) {
    println(`${filePath}：无法读取 —— ${pyOsErrorString(exc)}`)
    return [null, 2]
  }
  try {
    return [utf8DecodeStrict(data), 0]
  } catch (exc) {
    println(`${filePath}：不是合法的 UTF-8 编码（${exc}）`)
    return [null, 1]
  }
}

/** 严格 UTF-8 解码（对应 Python 的 data.decode("utf-8")）。 */
function utf8DecodeStrict(data) {
  return new TextDecoder('utf-8', { fatal: true }).decode(data)
}

/** 把 Node 的 fs 错误近似翻译成 Python OSError 的字符串形式。 */
function pyOsErrorString(err) {
  const table = {
    ENOENT: [2, 'No such file or directory'],
    EACCES: [13, 'Permission denied'],
    EPERM: [1, 'Operation not permitted'],
    EISDIR: [21, 'Is a directory'],
    ENOTDIR: [20, 'Not a directory'],
  }
  const entry = table[err && err.code]
  if (entry) {
    const target = err.path != null ? `: '${err.path}'` : ''
    return `[Errno ${entry[0]}] ${entry[1]}${target}`
  }
  return err && err.message ? err.message : String(err)
}

/** 校验生成的 OnlineRepo.json。 */
export function cmd_check_json(paths, cfg) {
  let exit_code = 0
  for (const filePath of paths) {
    const [text, code] = read_utf8(filePath)
    if (text === null) {
      exit_code = code
      continue
    }
    const [errors, warnings] = validate_json_document(text, filePath, cfg)
    for (const item of warnings) {
      println(`⚠️  ${item}`)
    }
    for (const item of errors) {
      println(`❌ ${item}`)
    }
    if (errors.length) {
      exit_code = 1
    } else {
      println(`✅ ${filePath} 通过`)
    }
  }
  return exit_code
}

/** 本地校验时推断文件夹名（即 author 回退用的值）。 */
export function infer_owner(filePath) {
  const absolute = path.resolve(filePath)
  let rel = ''
  try {
    rel = relativePortable(absolute, REPO_ROOT)
  } catch {
    rel = ''
  }
  if (rel && !rel.startsWith('..')) {
    const parts = rel.split('/')
    if (parts.length >= 2) {
      return parts[0]
    }
  }
  return path.basename(path.dirname(absolute)) || 'unknown'
}

/** 对应 os.path.relpath：跨盘符时 Python 抛 ValueError，这里同样抛错。 */
function relativePortable(target, base) {
  if (process.platform === 'win32') {
    const targetRoot = path.parse(target).root.toLowerCase()
    const baseRoot = path.parse(base).root.toLowerCase()
    if (targetRoot !== baseRoot) throw new Error('path is on mount')
  }
  return path.relative(base, target).split(path.sep).join('/')
}

/** 校验本地 .cs 文件；owner 决定 author 缺失时回退成什么。 */
export function cmd_check_cs(paths, owner, cfg) {
  let exit_code = 0
  for (const filePath of paths) {
    const [text, code] = read_utf8(filePath)
    if (text === null) {
      exit_code = code
      continue
    }
    const [meta, errors, warnings] = validate_script_file(
      text, filePath, owner || infer_owner(filePath), cfg
    )
    for (const item of warnings) {
      println(`⚠️  ${item}`)
    }
    for (const item of errors) {
      println(`❌ ${item}`)
    }
    if (errors.length) {
      exit_code = 1
    } else {
      println(
        `✅ ${filePath} 通过：Name=${pyReprAny(meta.name)} Guid=${pyReprAny(meta.guid)} ` +
        `Version=${pyReprAny(meta.version)} Author=${pyReprAny(meta.author)} ` +
        `TerritoryIds=${pyReprAny(meta.territorys)}`
      )
    }
  }
  return exit_code
}

/** 校验路径规则。 */
export function cmd_path_check(author, paths, cfg) {
  let exit_code = 0
  for (const filePath of paths) {
    const errors = []
    check_path_rules(filePath, author, cfg, errors, '路径')
    for (const item of errors) {
      println(`❌ ${item}`)
    }
    if (errors.length) {
      exit_code = 1
    } else {
      println(`✅ ${filePath} 通过（作者 ${author}）`)
    }
  }
  return exit_code
}

/** 命令行入口：无子命令时进入 CI 审核模式。 */
export async function main(argv = process.argv.slice(2)) {
  let values
  let positionals
  try {
    ;({ values, positionals } = parseArgs({
      args: argv,
      options: {
        'check-cs': { type: 'string', multiple: true },
        'check-json': { type: 'string', multiple: true },
        owner: { type: 'string' },
        'path-check': { type: 'string' },
        path: { type: 'string', multiple: true },
      },
      allowPositionals: true,
      strict: false,
    }))
  } catch (exc) {
    process.stderr.write(`usage: pr_review.mjs [--check-cs FILE ...] [--check-json FILE ...]\npr_review.mjs: error: ${exc.message}\n`)
    return 2
  }

  // argparse 的 nargs='+' 允许把位置参数一并吃给 --check-cs / --check-json。
  const checkCs = values['check-cs'] ? [...values['check-cs'], ...positionals] : null
  const checkJson = values['check-json'] ? [...values['check-json'], ...positionals] : null
  const pathArgs = values.path ?? []

  const cfg = load_config()

  if (checkCs && checkCs.length) {
    return cmd_check_cs(checkCs, values.owner, cfg)
  }
  if (checkJson && checkJson.length) {
    return cmd_check_json(checkJson, cfg)
  }
  if (values['path-check']) {
    if (!pathArgs.length) {
      process.stderr.write('usage: pr_review.mjs [--path-check AUTHOR --path PATH ...]\npr_review.mjs: error: --path-check 需要至少一个 --path\n')
      return 2
    }
    return cmd_path_check(values['path-check'], pathArgs, cfg)
  }
  return run_ci()
}

// --------------------------------------------------------------------------- //
// Python json.loads 的等价实现（错误文案 / 行列号与 CPython json.decoder 一致）
// --------------------------------------------------------------------------- //

/** 对应 json.JSONDecodeError，带 msg / lineno / colno / pos。 */
export class PyJsonDecodeError extends Error {
  constructor(msg, doc, pos) {
    const lineno = countNewlines(doc, 0, pos) + 1
    const colno = pos - doc.lastIndexOf('\n', pos - 1)
    super(`${msg}: line ${lineno} column ${colno} (char ${pos})`)
    this.name = 'JSONDecodeError'
    this.msg = msg
    this.doc = doc
    this.pos = pos
    this.lineno = lineno
    this.colno = colno
  }
}

/** doc[start:end) 里 '\n' 的个数（对应 str.count）。 */
function countNewlines(doc, start, end) {
  let count = 0
  for (let i = start; i < end; i += 1) {
    if (doc[i] === '\n') count += 1
  }
  return count
}

const JSON_WS = ' \t\n\r'
const isJsonWs = (ch) => ch !== undefined && ch !== '' && JSON_WS.includes(ch)

/** 跳过 JSON 空白，返回新下标（对应 WHITESPACE.match(s, pos).end()）。 */
function jsonWs(doc, pos) {
  let i = pos
  while (i < doc.length && isJsonWs(doc[i])) i += 1
  return i
}

const JSON_NUMBER_RE = /^(-?(?:0|[1-9][0-9]*))(\.[0-9]+)?([eE][-+]?[0-9]+)?/
const JSON_HEX_RE = /^[0-9A-Fa-f]{4}/
const JSON_ESCAPES = { '"': '"', '\\': '\\', '/': '/', b: '\b', f: '\f', n: '\n', r: '\r', t: '\t' }

/** 对应 json.decoder._decode_uXXXX。 */
function decodeUXXXX(doc, pos) {
  const m = JSON_HEX_RE.exec(doc.slice(pos + 1))
  if (m) return parseInt(m[0], 16)
  throw new PyJsonDecodeError('Invalid \\uXXXX escape', doc, pos)
}

/** 对应 json.decoder.py_scanstring：返回 [值, 结束下标]。 */
function jsonScanString(doc, end) {
  const parts = []
  const begin = end - 1
  for (;;) {
    // STRINGCHUNK = (.*?)(["\\\x00-\x1f])，找到最近的分隔字符
    let j = end
    while (j < doc.length) {
      const ch = doc[j]
      if (ch === '"' || ch === '\\' || ch.charCodeAt(0) <= 0x1f) break
      j += 1
    }
    if (j >= doc.length) {
      throw new PyJsonDecodeError('Unterminated string starting at', doc, begin)
    }
    const content = doc.slice(end, j)
    if (content) parts.push(content)
    end = j + 1
    const terminator = doc[j]
    if (terminator === '"') break
    if (terminator !== '\\') {
      throw new PyJsonDecodeError(`Invalid control character ${pyRepr(terminator)} at`, doc, end)
    }
    if (end >= doc.length) {
      throw new PyJsonDecodeError('Unterminated string starting at', doc, begin)
    }
    const esc = doc[end]
    let char
    if (esc !== 'u') {
      if (!Object.hasOwn(JSON_ESCAPES, esc)) {
        throw new PyJsonDecodeError(`Invalid \\escape: ${pyRepr(esc)}`, doc, end)
      }
      char = JSON_ESCAPES[esc]
      end += 1
    } else {
      let uni = decodeUXXXX(doc, end)
      end += 5
      if (0xd800 <= uni && uni <= 0xdbff && doc.slice(end, end + 2) === '\\u') {
        const uni2 = decodeUXXXX(doc, end + 1)
        if (0xdc00 <= uni2 && uni2 <= 0xdfff) {
          uni = 0x10000 + (((uni - 0xd800) << 10) | (uni2 - 0xdc00))
          end += 6
        }
      }
      char = String.fromCodePoint(uni)
    }
    parts.push(char)
  }
  return [parts.join(''), end]
}

/** 对应 CPython 的 json/scanner.py 里的 _scan_once（不含 NaN/Infinity：json.loads 默认支持，这里保留）。 */
function jsonScanOnce(doc, idx) {
  if (idx >= doc.length) throw { stop: idx }
  const c = doc[idx]
  if (c === '"') return jsonScanString(doc, idx + 1)
  if (c === '{') return jsonParseObject(doc, idx + 1)
  if (c === '[') return jsonParseArray(doc, idx + 1)
  if (c === 'n' && doc.slice(idx, idx + 4) === 'null') return [null, idx + 4]
  if (c === 't' && doc.slice(idx, idx + 4) === 'true') return [true, idx + 4]
  if (c === 'f' && doc.slice(idx, idx + 5) === 'false') return [false, idx + 5]

  const m = JSON_NUMBER_RE.exec(doc.slice(idx))
  if (m) {
    const [, integer, frac, exp] = m
    // 带小数部分或指数的走 PyFloat，保持「小数」身份，别被当成整数放行
    const value =
      frac || exp
        ? new PyFloat(Number(integer + (frac || '') + (exp || '')))
        : BigInt(integer)
    return [value, idx + m[0].length]
  }
  if (c === 'N' && doc.slice(idx, idx + 3) === 'NaN') return [NaN, idx + 3]
  if (c === 'I' && doc.slice(idx, idx + 8) === 'Infinity') return [Infinity, idx + 8]
  if (c === '-' && doc.slice(idx, idx + 9) === '-Infinity') return [-Infinity, idx + 9]
  throw { stop: idx }
}

/** 对应 json.decoder.JSONObject。 */
function jsonParseObject(doc, end) {
  const obj = Object.create(null)
  let nextchar = end < doc.length ? doc[end] : ''
  if (nextchar !== '"') {
    if (isJsonWs(nextchar)) {
      end = jsonWs(doc, end)
      nextchar = end < doc.length ? doc[end] : ''
    }
    if (nextchar === '}') return [obj, end + 1]
    if (nextchar !== '"') {
      throw new PyJsonDecodeError('Expecting property name enclosed in double quotes', doc, end)
    }
  }
  end += 1
  for (;;) {
    let key
    ;[key, end] = jsonScanString(doc, end)
    if (doc[end] !== ':') {
      end = jsonWs(doc, end)
      if (doc[end] !== ':') {
        throw new PyJsonDecodeError("Expecting ':' delimiter", doc, end)
      }
    }
    end += 1
    if (end < doc.length && isJsonWs(doc[end])) {
      end += 1
      if (end < doc.length && isJsonWs(doc[end])) end = jsonWs(doc, end + 1)
    }
    let value
    try {
      ;[value, end] = jsonScanOnce(doc, end)
    } catch (err) {
      if (err && err.stop !== undefined) {
        throw new PyJsonDecodeError('Expecting value', doc, err.stop)
      }
      throw err
    }
    obj[key] = value
    let after
    if (end < doc.length) {
      after = doc[end]
      if (isJsonWs(after)) {
        end = jsonWs(doc, end + 1)
        after = end < doc.length ? doc[end] : ''
      }
    } else {
      after = ''
    }
    end += 1
    if (after === '}') break
    if (after !== ',') {
      throw new PyJsonDecodeError("Expecting ',' delimiter", doc, end - 1)
    }
    const commaIdx = end - 1
    end = jsonWs(doc, end)
    after = end < doc.length ? doc[end] : ''
    end += 1
    if (after !== '"') {
      if (after === '}') {
        throw new PyJsonDecodeError('Illegal trailing comma before end of object', doc, commaIdx)
      }
      throw new PyJsonDecodeError('Expecting property name enclosed in double quotes', doc, end - 1)
    }
  }
  return [obj, end]
}

/** 对应 json.decoder.JSONArray。 */
function jsonParseArray(doc, end) {
  const values = []
  let nextchar = end < doc.length ? doc[end] : ''
  if (isJsonWs(nextchar)) {
    end = jsonWs(doc, end + 1)
    nextchar = end < doc.length ? doc[end] : ''
  }
  if (nextchar === ']') return [values, end + 1]

  for (;;) {
    let value
    try {
      ;[value, end] = jsonScanOnce(doc, end)
    } catch (err) {
      if (err && err.stop !== undefined) {
        throw new PyJsonDecodeError('Expecting value', doc, err.stop)
      }
      throw err
    }
    values.push(value)
    nextchar = end < doc.length ? doc[end] : ''
    if (isJsonWs(nextchar)) {
      end = jsonWs(doc, end + 1)
      nextchar = end < doc.length ? doc[end] : ''
    }
    end += 1
    if (nextchar === ']') break
    if (nextchar !== ',') {
      throw new PyJsonDecodeError("Expecting ',' delimiter", doc, end - 1)
    }
    const commaIdx = end - 1
    try {
      if (isJsonWs(doc[end])) {
        end += 1
        if (isJsonWs(doc[end])) end = jsonWs(doc, end + 1)
      }
      nextchar = end < doc.length ? doc[end] : ''
    } catch {
      // 对应 Python 的 IndexError: pass
    }
    if (nextchar === ']') {
      throw new PyJsonDecodeError('Illegal trailing comma before end of array', doc, commaIdx)
    }
  }
  return [values, end]
}

/** 等价于 json.loads(text)（默认 strict=True）。 */
export function pyJsonLoads(text) {
  let end = jsonWs(text, 0)
  let obj
  try {
    ;[obj, end] = jsonScanOnce(text, end)
  } catch (err) {
    if (err && err.stop !== undefined) {
      throw new PyJsonDecodeError('Expecting value', text, err.stop)
    }
    throw err
  }
  end = jsonWs(text, end)
  if (end !== text.length) {
    throw new PyJsonDecodeError('Extra data', text, end)
  }
  return obj
}

// 直接运行本文件时进入 CLI（被 import 时不会执行）。
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().then((code) => process.exit(code))
}
