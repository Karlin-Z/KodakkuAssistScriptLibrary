import fs from 'node:fs'
import path from 'node:path'
import process from 'node:process'
import { parseArgs } from 'node:util'
import { fileURLToPath, pathToFileURL } from 'node:url'

import { extractScriptType, pyRepr } from './csharp_meta.ts'
import type { DecodedValue, ScriptTypeParams } from './csharp_meta.ts'

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.dirname(path.dirname(SCRIPT_DIR))

const CONFIG_PATH = path.join(SCRIPT_DIR, 'pr_review_rules.json')

export const ALLOWED_FIELDS = new Set([
  'Name', 'Guid', 'Version', 'Author', 'Repo',
  'DownloadUrl', 'Note', 'UpdateInfo', 'UpdateTime', 'TerritoryIds',
])
const REQUIRED_STRING_FIELDS = ['Name', 'Guid', 'Version', 'Author', 'DownloadUrl']
const OPTIONAL_STRING_FIELDS = ['Repo', 'Note', 'UpdateInfo', 'UpdateTime']
const NON_EMPTY_FIELDS = ['Name', 'Guid', 'Version', 'Author']

const VERSION_RE = /^[\p{Nd}]+(\.[\p{Nd}]+){0,3}(-[0-9A-Za-z.\-]+)?(\+[0-9A-Za-z.\-]+)?$/u
const UUID_RE = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/
const URL_RE = /^https?:\/\/[^\s]+$/
const UPDATE_TIME_RE = /^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$/

export const UINT_MAX = 0xFFFFFFFF

const INVALID_FILENAME_CHARS = new Set('<>:"/\\|?*'.split(''))
const WINDOWS_RESERVED_NAMES = new Set([
  'CON', 'PRN', 'AUX', 'NUL',
  ...Array.from({ length: 9 }, (_, i) => `COM${i + 1}`),
  ...Array.from({ length: 9 }, (_, i) => `LPT${i + 1}`),
])

const AUTHOR_DEFAULT = 'Unknown'
const NAME_DEFAULT = 'Default Script'
const VERSION_DEFAULT = '0.0.0.1'

export const GUID_MAP_PATH = '.github/index/guid-map.json'

export const REVIEW_STATUS_CONTEXT = 'KodakkuAssist/cross-author-review'

const MAX_FILE_BYTES = 2 * 1024 * 1024

/** API 地址 */
const API_URL = process.env.GITHUB_API_URL ?? 'https://api.github.com'

/** GitHub 用户名 */
const GITHUB_LOGIN_RE = /^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$/

/** 审核配置 */
export interface ReviewConfig {
  /** 用户名忽略大小写 */
  username_case_insensitive: boolean;
  /** 允许子目录 */
  allow_subfolders: boolean;
  /** 允许扩展名 */
  allowed_extensions: string[];
  /** 单 PR 文件上限 */
  max_files_per_pr: number;
  /** 忽略目录 */
  ignore_dirs: string[];
  /** 维护者白名单 */
  maintainers: string[];
  /** 跨作者标签 */
  foreign_folder_labels: string[];
  /** 批准命令 */
  approve_commands: string[];
  /** 未知字段策略 */
  unknown_fields: 'warn' | 'error' | 'ignore';
}

/** 脚本信息 */
export interface ScriptMeta {
  guid: string;
  name: string;
  version: string;
  author: string;
  note: string;
  update_info: string;
  territorys: number[];
}

/** 校验结果 */
export interface ValidationResult {
  meta: ScriptMeta | null;
  errors: string[];
  warnings: string[];
}

/** 改动文件 */
export interface ChangedFile {
  filename?: string;
  status?: string;
  previous_filename?: string | null;
  [key: string]: unknown;
}

/** 跨作者改动 */
export interface ForeignChange {
  path: string;
  folder: string;
  author?: string;
  ctx?: string;
}

/** guid 映射 */
export type GuidMap = Record<string, string> | Map<string, string>;

/** JSON 值 */
export type JsonValue =
  | null
  | boolean
  | number
  | string
  | bigint
  | PyFloat
  | JsonValue[]
  | { [key: string]: JsonValue };

/** 内容响应 */
export interface GitHubContentInfo {
  type?: string;
  size?: number;
  encoding?: string;
  content?: string;
}

/** PR 响应 */
export interface GitHubPullRequest {
  state?: string;
  merged?: boolean;
  draft?: boolean;
  user?: { login?: string };
  head?: { sha?: string };
  base?: { ref?: string };
}

/** 状态响应 */
export interface GitHubStatus {
  context?: string;
  state?: string;
}

/** 合并响应 */
export interface GitHubMergeResult {
  merged?: boolean;
}

/** 审核结果 */
export interface ReviewOutcome {
  files: ChangedFile[] | null;
  errors: string[];
  warnings: string[];
  rows: Array<[string, string]>;
  foreign: ForeignChange[];
  known_folders: Set<string>;
  api_error: string | null;
}

export const DEFAULT_CONFIG: ReviewConfig = {
  username_case_insensitive: true,
  allow_subfolders: true,
  allowed_extensions: ['.cs'],
  max_files_per_pr: 100,
  ignore_dirs: [],
  maintainers: [],
  foreign_folder_labels: ['跨作者修改'],
  approve_commands: ['/approve', '/同意合并'],
  unknown_fields: 'warn',
}

const STATUS_LABEL: Record<string, string> = {
  added: '新增',
  modified: '修改',
  removed: '删除',
  renamed: '重命名',
  copied: '复制',
  changed: '变更',
  unchanged: '未变',
}

/** 输出一行 */
function println(text: unknown): void {
  process.stdout.write(String(text) + '\n')
}

/** Python 空白 */
function isPySpace(code: number): boolean {
  return (
    (code >= 0x09 && code <= 0x0d) ||
    (code >= 0x1c && code <= 0x1f) ||
    code === 0x20 || code === 0x85 || code === 0xa0 || code === 0x1680 ||
    (code >= 0x2000 && code <= 0x200a) ||
    code === 0x2028 || code === 0x2029 || code === 0x202f || code === 0x205f ||
    code === 0x3000
  )
}

/** Python 去空白 */
function pyStrip(text: string): string {
  const chars = Array.from(text)
  let start = 0
  let end = chars.length
  while (start < end && isPySpace(chars[start].codePointAt(0) ?? 0)) start += 1
  while (end > start && isPySpace(chars[end - 1].codePointAt(0) ?? 0)) end -= 1
  return chars.slice(start, end).join('')
}

/** 模拟 repr */
function pyReprAny(value: unknown): string {
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

/** 浮点 repr */
export function pyFloatRepr(value: number): string {
  if (Number.isNaN(value)) return 'nan'
  if (value === Infinity) return 'inf'
  if (value === -Infinity) return '-inf'
  if (Number.isInteger(value)) {
    return Object.is(value, -0) ? '-0.0' : `${value}.0`
  }
  return String(value)
}

/** 小数标记 */
export class PyFloat extends Number {}

/** 类型中文名 */
export function type_name(value: unknown): string {
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

/** 默认配置 */
export function default_config(): ReviewConfig {
  return structuredClone(DEFAULT_CONFIG)
}

/** 默认配置 */
export function defaultConfig(): ReviewConfig {
  return default_config()
}

/** 读取配置 */
export function load_config(): ReviewConfig {
  const cfg = default_config()
  // 读取文件
  let raw: string
  try {
    raw = fs.readFileSync(CONFIG_PATH, 'utf8')
  } catch (err) {
    if (err && (err as NodeJS.ErrnoException).code === 'ENOENT') return cfg
    println(`::warning::无法读取审核规则配置 ${CONFIG_PATH}: ${pyOsErrorString(err)}`)
    return cfg
  }
  try {
    const user_cfg = pyJsonLoads(raw)
    // 合并配置
    if (isPlainObject(user_cfg)) Object.assign(cfg, user_cfg as Partial<ReviewConfig>)
  } catch (err) {
    println(`::warning::无法读取审核规则配置 ${CONFIG_PATH}: ${err}`)
  }
  return cfg
}

/** 读取配置 */
export function loadConfig(): ReviewConfig {
  return load_config()
}

/** 规范化用户名 */
export function normalize_login(login: string | null | undefined, case_insensitive: boolean): string {
  const text = pyStrip(login || '')
  return case_insensitive ? text.toLowerCase() : text
}

/** 去尾部空格点 */
function rstripSpaceDot(text: string): string {
  let end = text.length
  while (end > 0 && (text[end - 1] === ' ' || text[end - 1] === '.')) end -= 1
  return text.slice(0, end)
}

/** 校验文件名 */
export function check_filename_component(value: string, label: string, errors: string[]): void {
  if (Array.from(value).some((ch) => (ch.codePointAt(0) ?? 0) < 32)) {
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

/** 校验路径规则 */
export function check_path_rules(
  filePath: unknown,
  author: string,
  cfg: ReviewConfig,
  errors: string[],
  ctx: string,
  foreign: ForeignChange[] | null = null,
): void {
  if (typeof filePath !== 'string' || !filePath) {
    errors.push(`${ctx}：路径为空或非法`)
    return
  }

  // 路径格式
  if (filePath.startsWith('/') || filePath.includes('\\')) {
    errors.push(`${ctx}：${pyReprAny(filePath)} 不是合法的仓库相对路径`)
    return
  }

  // 路径片段
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
  if (folder.startsWith('.') || folder.startsWith('_')) {
    errors.push(`${ctx}：${pyReprAny(folder)}/ 不是贡献者文件夹，不能修改`)
  } else {
    const same = normalize_login(folder, cfg.username_case_insensitive) ===
      normalize_login(author, cfg.username_case_insensitive)
    if (!same) {
      if (Array.isArray(foreign)) {
        foreign.push({ path: filePath, folder, author, ctx })
      } else {
        errors.push(
          `${ctx}：第一层文件夹 ${pyReprAny(folder)} 与 PR 作者 ${pyReprAny(author)} 不一致；` +
          `你只能修改自己的文件夹 ${author}/`
        )
      }
    }
  }

  // 子目录限制
  if (!(cfg.allow_subfolders ?? true) && parts.length > 2) {
    errors.push(`${ctx}：不允许在 ${folder}/ 下创建子文件夹（${pyReprAny(filePath)}）`)
  }

  // 扩展名限制
  const extensions = (cfg.allowed_extensions ?? ['.cs']).map((e) => String(e).toLowerCase())
  const filename = parts[parts.length - 1]
  const filenameLower = filename.toLowerCase()
  if (!extensions.some((ext) => filenameLower.endsWith(ext))) {
    errors.push(
      `${ctx}：只允许 ${extensions.join(' / ')} 文件，${pyReprAny(filename)} 不合法`
    )
  }
}

/** 校验路径规则 */
export function checkPathRules(
  filePath: string,
  author: string,
  cfg: ReviewConfig,
  collected: string[],
  label: string,
  foreign: ForeignChange[] | null = null,
): void {
  check_path_rules(filePath, author, cfg, collected, label, foreign)
}

/** 取参数值 */
function getParam(params: ScriptTypeParams, key: string): DecodedValue | [null, null] {
  return Object.hasOwn(params, key) ? params[key] : [null, null]
}

/** 校验 .cs 文件 */
export function validate_script_file(
  text: string,
  rel_path: string,
  folder_owner: string,
  cfg: ReviewConfig,
): [ScriptMeta | null, string[], string[]] {
  const errors: string[] = []
  const warnings: string[] = []

  /** 记录错误 */
  const err = (msg: string): number => errors.push(`${rel_path}：${msg}`)
  /** 记录提醒 */
  const warn = (msg: string): number => warnings.push(`${rel_path}：${msg}`)

  // 解析特性
  const { params, errors: parse_errors } = extractScriptType(text)
  if (parse_errors.length) {
    for (const item of parse_errors) err(item)
    return [null, errors, warnings]
  }

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

  // guid 参数
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

  // name 参数
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

  // version 参数
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

  // author 参数
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

  // 地图参数
  const territorys: number[] = []
  {
    const [kind, value] = getParam(params, 'territorys')
    if (kind === null) {
      warn('没有写 territorys，该脚本不会按地图过滤')
    } else if (kind === 'null') {
      warn('territorys 写成了 null，等同于空数组')
    } else if (kind !== 'array') {
      err(`territorys 必须是 uint 数组字面量，实际是${type_name(value)}`)
    } else {
      const items = value as readonly number[]
      items.forEach((tid, pos) => {
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

  // note 参数
  let note = ''
  {
    const [kind, value] = getParam(params, 'note')
    if (kind !== null && kind !== 'string') {
      err(`note 必须是字符串，实际是${type_name(value)}`)
    } else if (kind === 'string') {
      note = String(value)
    }
  }

  // 更新参数
  let update_info = ''
  {
    const [kind, value] = getParam(params, 'updateInfo')
    if (kind !== null && kind !== 'string') {
      err(`updateInfo 必须是字符串，实际是${type_name(value)}`)
    } else if (kind === 'string') {
      update_info = String(value)
    }
  }

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

/** 校验 .cs 文件 */
export function validateScriptFile(
  text: string,
  relPath: string,
  owner: string,
  cfg: ReviewConfig,
): ValidationResult {
  const [meta, errors, warnings] = validate_script_file(text, relPath, owner, cfg)
  return { meta, errors, warnings }
}

/** 判断普通对象 */
function isPlainObject(value: unknown): value is Record<string, JsonValue> {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
}

/** 校验索引文档 */
export function validate_json_document(
  text: string,
  jsonPath: string,
  cfg: ReviewConfig,
): [string[], string[]] {
  const errors: string[] = []
  const warnings: string[] = []

  if (text.startsWith('\ufeff')) {
    warnings.push(`${jsonPath}：文件带有 UTF-8 BOM，已忽略`)
    text = text.slice(1)
  }

  if (!pyStrip(text)) {
    errors.push(`${jsonPath}：文件内容为空`)
    return [errors, warnings]
  }

  let data: JsonValue
  try {
    // 解析 JSON
    data = pyJsonLoads(text)
  } catch (exc) {
    const error = exc as PyJsonDecodeError
    errors.push(
      `${jsonPath}：JSON 语法错误 —— ${error.msg}（第 ${error.lineno} 行第 ${error.colno} 列）`
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

  // 去重表
  const guid_seen = new Map()
  const name_seen = new Map()
  data.forEach((entry, idx) => {
    validate_entry(idx, entry, jsonPath, cfg, errors, warnings, guid_seen, name_seen)
  })
  return [errors, warnings]
}

/** 校验索引文档 */
export function validateJsonDocument(
  text: string,
  filename: string,
  cfg: ReviewConfig,
): { errors: string[]; warnings: string[] } {
  const [errors, warnings] = validate_json_document(text, filename, cfg)
  return { errors, warnings }
}

/** 校验单个条目 */
export function validate_entry(
  idx: number,
  entry: JsonValue,
  jsonPath: string,
  cfg: ReviewConfig,
  errors: string[],
  warnings: string[],
  guid_seen: Map<string, number>,
  name_seen: Map<string, number>,
): void {
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

  const territory_ids = entry.TerritoryIds
  if (territory_ids !== null && territory_ids !== undefined && !Array.isArray(territory_ids)) {
    errors.push(
      `${label}：TerritoryIds 必须是整数数组，实际是${type_name(territory_ids)}`
    )
  } else if (Array.isArray(territory_ids)) {
    territory_ids.forEach((tid, pos) => {
      const integer = typeof tid === 'bigint' || (typeof tid === 'number' && Number.isInteger(tid))
      if (!integer) {
        errors.push(`${label}：TerritoryIds[${pos}] 必须是整数，实际是${type_name(tid)}`)
      } else if (!(0 <= (tid as number | bigint) && (tid as number | bigint) <= UINT_MAX)) {
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

/** 构造请求头 */
function _api_headers(accept: string): Record<string, string> {
  const token = process.env.GITHUB_TOKEN || process.env.GH_TOKEN || ''
  const headers: Record<string, string> = {
    Accept: accept,
    'X-GitHub-Api-Version': '2022-11-28',
    'User-Agent': 'kasl-pr-reviewer',
  }
  if (token) {
    headers.Authorization = `Bearer ${token}`
  }
  return headers
}

/** URL 编码 */
function pyQuote(text: string, safe = ''): string {
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

/** HTTP 错误 */
class HttpError extends Error {
  code: number

  /** 构造函数 */
  constructor(status: number, statusText: string) {
    super(`HTTP Error ${status}: ${statusText}`)
    this.name = 'HTTPError'
    this.code = status
  }
}

/** 请求 JSON */
async function api_json<T = JsonValue>(api_path: string): Promise<T | null> {
  const response = await fetch(`${API_URL}${api_path}`, {
    headers: _api_headers('application/vnd.github+json'),
    signal: AbortSignal.timeout(30000),
  })
  if (!response.ok) throw new HttpError(response.status, response.statusText)
  const body = Buffer.from(await response.arrayBuffer())
  return body.length ? (pyJsonLoads(body.toString('utf8')) as T) : null
}

/** 发送 API 请求 */
async function api_send<T = JsonValue>(
  method: string,
  api_path: string,
  body?: unknown,
): Promise<T | null> {
  const headers = _api_headers('application/vnd.github+json')
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const response = await fetch(`${API_URL}${api_path}`, {
    method,
    body: body === undefined ? undefined : Buffer.from(JSON.stringify(body), 'utf8'),
    headers,
    signal: AbortSignal.timeout(30000),
  })
  if (!response.ok) throw new HttpError(response.status, response.statusText)
  const bytes = Buffer.from(await response.arrayBuffer())
  return bytes.length ? (pyJsonLoads(bytes.toString('utf8')) as T) : null
}

/** 请求原始内容 */
async function api_raw(api_path: string): Promise<Buffer> {
  const response = await fetch(`${API_URL}${api_path}`, {
    headers: _api_headers('application/vnd.github.raw'),
    signal: AbortSignal.timeout(30000),
  })
  if (!response.ok) throw new HttpError(response.status, response.statusText)
  return Buffer.from(await response.arrayBuffer())
}

/** 拉取改动文件 */
export async function list_changed_files(
  repo: string,
  pr_number: string | number,
  maxFiles: number | null = null,
): Promise<ChangedFile[]> {
  const limit = maxFiles === null ? null : Number(maxFiles)
  const files: ChangedFile[] = []
  let page = 1
  for (;;) {
    const batch = await api_json<ChangedFile[]>(
      `/repos/${repo}/pulls/${pr_number}/files?per_page=100&page=${page}`,
    )
    if (!batch || !Array.isArray(batch)) break
    files.push(...batch)
    if (batch.length < 100) break
    if (limit !== null && files.length > limit) {
      files.length = limit + 1
      break
    }
    page += 1
  }
  return files
}

/** Base64 解码 */
function pyB64Decode(text: string): Buffer {
  const cleaned = String(text).replace(/[^A-Za-z0-9+/=]/g, '')
  if (cleaned.length % 4 !== 0 || /=/.test(cleaned.slice(0, -2)) || (cleaned.includes('=') && !/=+$/.test(cleaned))) {
    throw new Error('Invalid base64-encoded string')
  }
  return Buffer.from(cleaned, 'base64')
}

/** 读取文件内容 */
export async function fetch_file_bytes(
  repo: string,
  filePath: string,
  ref: string,
): Promise<[Buffer | null, string | null]> {
  const quoted = pyQuote(filePath, '/')
  const ref_q = pyQuote(ref, '')
  const info = await api_json<GitHubContentInfo>(`/repos/${repo}/contents/${quoted}?ref=${ref_q}`)
  if (!info || typeof info !== 'object' || Array.isArray(info) || info.type !== 'file') {
    const kind = info && typeof info === 'object' && !Array.isArray(info) ? info.type : type_name(info)
    return [null, `只允许普通文件（不允许符号链接 / 子模块 / 目录），实际类型为 ${kind}`]
  }
  // 大小限制
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
  // 大文件走 raw
  return [await api_raw(`/repos/${repo}/contents/${quoted}?ref=${ref_q}`), null]
}

/** 读取 guid 映射 */
export async function load_guid_map(
  repo: string,
  ref: string,
): Promise<Record<string, string> | null> {
  if (!ref) return null
  const quoted = pyQuote(GUID_MAP_PATH, '/')
  let info: GitHubContentInfo | null
  try {
    info = await api_json<GitHubContentInfo>(`/repos/${repo}/contents/${quoted}?ref=${pyQuote(ref, '')}`)
  } catch (exc) {
    if (exc instanceof HttpError && exc.code === 404) return null
    throw exc
  }
  if (!info || typeof info !== 'object' || Array.isArray(info) || !info.content) return null
  let data: JsonValue
  try {
    data = pyJsonLoads(pyB64Decode(info.content).toString('utf8'))
  } catch {
    return null
  }
  if (!isPlainObject(data)) return null
  const out: Record<string, string> = {}
  for (const [key, value] of Object.entries(data)) {
    out[String(key).toLowerCase()] = String(value)
  }
  return out
}

/** 判断映射为空 */
function mappingIsEmpty(mapping: unknown): boolean {
  if (!mapping) return true
  if (mapping instanceof Map) return mapping.size === 0
  if (Array.isArray(mapping)) return mapping.length === 0
  if (typeof mapping === 'object') return Object.keys(mapping).length === 0
  return true
}

/** 读取映射值 */
function mappingGet(mapping: GuidMap | null | undefined, key: string): string | undefined {
  if (!mapping) return undefined
  if (mapping instanceof Map) return mapping.get(key)
  return Object.hasOwn(mapping, key) ? mapping[key] : undefined
}

/** 判断路径存在 */
function ownPathsHas(own_paths: unknown, candidate: string): boolean {
  if (!own_paths) return false
  if (own_paths instanceof Set) return own_paths.has(candidate)
  if (Array.isArray(own_paths)) return own_paths.includes(candidate)
  if (typeof own_paths === 'object') return Object.hasOwn(own_paths, candidate)
  return false
}

/** 检查 guid 冲突 */
export function check_guid_collisions(
  script_guids: Array<[string, string]>,
  own_paths: unknown,
  author: string,
  guid_map: GuidMap | null,
  cfg: ReviewConfig,
): string[] {
  const errors: string[] = []

  const seen = new Map<string, string>()
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

/** 检查 guid 冲突 */
export function checkGuidCollisions(
  entries: Array<[string, string]>,
  renamedFiles: unknown,
  author: string,
  guidMap: GuidMap | null,
  cfg: ReviewConfig,
): string[] {
  return check_guid_collisions(entries, renamedFiles, author, guidMap, cfg)
}

/** 校验改动集合 */
export function check_change_set(
  files: ChangedFile[],
  author: string,
  cfg: ReviewConfig,
): [string[], string[], Array<[string, string]>, ForeignChange[]] {
  const errors: string[] = []
  const warnings: string[] = []
  const rows: Array<[string, string]> = []
  const foreign: ForeignChange[] = []

  const max_files = cfg.max_files_per_pr ?? 100
  if (files.length > max_files) {
    errors.push(`本 PR 改动了 ${files.length} 个文件，超过上限 ${max_files} 个`)
  }

  const ignore_dirs = new Set((cfg.ignore_dirs || []).map((d) => normalize_login(String(d), true)))
  if (ignore_dirs.has(normalize_login(author, true))) {
    errors.push(
      `你的文件夹 ${author}/ 在 ignore_dirs 排除名单里，不会被收录；请联系维护者`
    )
  }

  const bypass = (cfg.maintainers || []).some(
    (login) => normalize_login(String(login), true) === normalize_login(author, true),
  )
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
      check_path_rules(filePath, author, cfg, errors, `文件 ${filePath}`, foreign)
    }

    if ((status === 'renamed' || status === 'copied') && previous) {
      const prefix = status === 'renamed' ? '重命名前' : '复制来源'
      rows.push([`${label}（来源）`, previous])
      if (!bypass) {
        check_path_rules(previous, author, cfg, errors, `文件 ${previous}（${prefix}路径）`, foreign)
      }
    }
  }

  const foreign_dirs = foreign_folders(foreign)
  if (foreign_dirs.length > 1) {
    errors.push(
      `本 PR 同时修改了多个其他作者的文件夹（${foreign_dirs.map((folder) => `${folder}/`).join('、')}）；` +
      '一个 PR 只能修改一个其他作者的文件夹，请拆成多个 PR'
    )
  }

  return [errors, warnings, rows, foreign]
}

/** 校验改动集合 */
export function checkChangeSet(
  files: ChangedFile[],
  author: string,
  cfg: ReviewConfig,
): { errors: string[]; warnings: string[]; rows: Array<[string, string]>; foreign: ForeignChange[] } {
  const [errors, warnings, rows, foreign] = check_change_set(files, author, cfg)
  return { errors, warnings, rows, foreign }
}

/** 跨作者文件夹 */
export function foreign_folders(
  foreign: ReadonlyArray<{ folder?: string }> | null | undefined,
): string[] {
  const folders: string[] = []
  const seen = new Set<string>()
  for (const item of foreign || []) {
    const folder = String(item.folder || '')
    if (!folder) continue
    const key = normalize_login(folder, true)
    if (!seen.has(key)) {
      seen.add(key)
      folders.push(folder)
    }
  }
  return folders
}

/** 跨作者所有者 */
export function foreign_owners(
  foreign: ReadonlyArray<{ folder?: string }> | null | undefined,
): string[] {
  const owners: string[] = []
  const seen = new Set<string>()
  for (const item of foreign || []) {
    for (const part of String(item.folder || '').split(/[&＆]/)) {
      const owner = part.trim()
      if (!GITHUB_LOGIN_RE.test(owner)) continue
      const key = normalize_login(owner, true)
      if (!seen.has(key)) {
        seen.add(key)
        owners.push(owner)
      }
    }
  }
  return owners
}

/** 跨作者去重 */
function unique_foreign(
  foreign: ReadonlyArray<ForeignChange> | null | undefined,
): ForeignChange[] {
  const seen = new Set<string>()
  const out: ForeignChange[] = []
  for (const item of foreign || []) {
    if (seen.has(item.path)) continue
    seen.add(item.path)
    out.push(item)
  }
  return out
}

/** 净化文本 */
function safe_text(value: unknown): string {
  return String(value ?? '')
    .replace(/[\u0000-\u001f\u007f\u2028\u2029]+/g, ' ')
    .replace(/`/g, "'")
    .replace(/\|/g, '\\|')
    .replace(/::/g, ': :')
}

/** 行内代码 */
function safe_code(value: unknown): string {
  return `\`${safe_text(value)}\``
}

/** 净化日志 */
function safe_stdout(text: unknown): string {
  return String(text).replace(/^::/gm, '\\::').replace(/^##\[/gm, '\\##[')
}

/** 生成报告正文 */
export function describe(
  files: ReadonlyArray<readonly [string, string]>,
  errors: string[],
  warnings: string[],
  foreign: ReadonlyArray<ForeignChange> = [],
): string {
  const lines = ['### 改动清单', '', '| 状态 | 文件 |', '| --- | --- |']
  for (const [label, filePath] of files) {
    lines.push(`| ${safe_text(label)} | ${safe_code(filePath)} |`)
  }
  lines.push('')

  if (errors.length) {
    lines.push('### ❌ 需要修复')
    lines.push('')
    errors.forEach((item, index) => {
      lines.push(`${index + 1}. ${safe_text(item)}`)
    })
    lines.push('')
  }

  if (warnings.length) {
    lines.push('### ⚠️ 提醒（不阻塞）')
    lines.push('')
    for (const item of warnings) {
      lines.push(`- ${safe_text(item)}`)
    }
    lines.push('')
  }

  const foreign_items = unique_foreign(foreign)
  if (foreign_items.length) {
    lines.push('### 🔀 跨作者修改')
    lines.push('')
    lines.push('以下文件位于其他作者的文件夹，本 PR 不会自动合并：')
    lines.push('')
    for (const item of foreign_items) {
      lines.push(`- ${safe_code(item.path)}（属于 ${safe_code(item.folder)}）`)
    }
    lines.push('')
  }

  if (!errors.length && !warnings.length && !foreign_items.length) {
    lines.push('未发现问题。')
    lines.push('')
  }

  lines.push(
    '> 规则：修改自己文件夹的 PR 校验通过后自动合并；修改别人文件夹的 PR 需要文件夹所有者或维护者确认。' +
    '只能新增 / 修改 / 删除 `.cs`，每个 `.cs` 必须包含且只包含一处 `[ScriptType(...)]`。详见 `README.md`。'
  )
  return lines.join('\n')
}

/** 生成 PR 评论 */
export function build_report(
  title: string,
  rows: ReadonlyArray<readonly [string, string]>,
  errors: string[],
  warnings: string[],
  foreign: ReadonlyArray<ForeignChange> = [],
): string {
  return [`## 🤖 PR 自动审核：${title}`, '', describe(rows, errors, warnings, foreign)].join('\n')
}

/** 写运行摘要 */
export function write_step_summary(markdown: string): void {
  const summary_path = process.env.GITHUB_STEP_SUMMARY
  if (summary_path) {
    try {
      fs.appendFileSync(summary_path, markdown + '\n', 'utf8')
    } catch {
      // 忽略
    }
  }
}

/** 发表评论 */
export async function post_comment(
  repo: string,
  pr_number: string | number,
  body: string,
): Promise<void> {
  const response = await fetch(`${API_URL}/repos/${repo}/issues/${pr_number}/comments`, {
    method: 'POST',
    body: Buffer.from(JSON.stringify({ body }), 'utf8'),
    headers: _api_headers('application/vnd.github+json'),
    signal: AbortSignal.timeout(30000),
  })
  if (!response.ok) throw new HttpError(response.status, response.statusText)
  await response.arrayBuffer()
}

/** 安全发表评论 */
export async function post_comment_safely(
  repo: string,
  pr_number: string | number,
  body: string,
): Promise<void> {
  try {
    await post_comment(repo, pr_number, body)
  } catch (exc) {
    println(`::warning::无法在 PR 下留言：${exc}`)
  }
}

/** 写输出变量 */
export function set_output(name: string, value: unknown): boolean {
  const output_path = process.env.GITHUB_OUTPUT
  if (!output_path) return false
  try {
    fs.appendFileSync(output_path, `${name}=${String(value)}\n`, 'utf8')
    return true
  } catch (exc) {
    println(`::warning::无法写入 GitHub Actions 输出 ${name}：${exc}`)
    return false
  }
}

/** 跨作者标签 */
function foreign_labels(cfg: ReviewConfig): string[] {
  const value = cfg.foreign_folder_labels ?? []
  const list = Array.isArray(value) ? value : [value]
  return list.map((item) => String(item).trim()).filter(Boolean)
}

/** 批准命令列表 */
function approval_command_list(cfg: ReviewConfig): string[] {
  const value = cfg.approve_commands ?? []
  const list = Array.isArray(value) ? value : [value]
  return list.map((item) => String(item).trim()).filter(Boolean)
}

/** 批准命令集合 */
function approval_commands(cfg: ReviewConfig): Set<string> {
  return new Set(approval_command_list(cfg).map((item) => item.toLowerCase()))
}

/** 主批准命令 */
function primary_approval_command(cfg: ReviewConfig): string {
  return approval_command_list(cfg)[0] || '/approve'
}

/** 判断批准命令 */
export function is_approval_comment(body: unknown, cfg: ReviewConfig): boolean {
  return approval_commands(cfg).has(String(body ?? '').trim().toLowerCase())
}

/** 确保标签存在 */
async function ensure_label(repo: string, name: string): Promise<void> {
  try {
    await api_send('POST', `/repos/${repo}/labels`, {
      name,
      color: 'fbca04',
      description: '修改了其他作者的文件夹，需要人工审核',
    })
  } catch (exc) {
    if (!(exc instanceof HttpError) || exc.code !== 422) throw exc
  }
}

/** 安全添加标签 */
export async function add_labels_safely(
  repo: string,
  pr_number: string | number,
  labels: ReadonlyArray<unknown> | string,
): Promise<void> {
  const names = (Array.isArray(labels) ? labels : []).map((item) => String(item).trim()).filter(Boolean)
  if (!names.length) return
  try {
    for (const name of names) await ensure_label(repo, name)
    await api_send('POST', `/repos/${repo}/issues/${pr_number}/labels`, { labels: names })
  } catch (exc) {
    println(`::warning::无法给 PR 添加标签 ${names.join(', ')}：${exc}`)
  }
}

/** 安全移除标签 */
export async function remove_labels_safely(
  repo: string,
  pr_number: string | number,
  labels: ReadonlyArray<unknown> | string,
): Promise<void> {
  const names = (Array.isArray(labels) ? labels : []).map((item) => String(item).trim()).filter(Boolean)
  for (const name of names) {
    try {
      await api_send('DELETE', `/repos/${repo}/issues/${pr_number}/labels/${pyQuote(name)}`)
    } catch (exc) {
      if (!(exc instanceof HttpError) || exc.code !== 404) {
        println(`::warning::无法移除标签 ${name}：${exc}`)
      }
    }
  }
}

/** 写审核状态 */
async function set_review_status(
  repo: string,
  sha: string,
  state: string,
  description: string,
): Promise<void> {
  if (!sha) return
  try {
    await api_send('POST', `/repos/${repo}/statuses/${sha}`, {
      state,
      context: REVIEW_STATUS_CONTEXT,
      description: String(description).slice(0, 140),
    })
  } catch (exc) {
    println(`::warning::无法写入跨作者审核状态：${exc}`)
  }
}

/** 读取审核状态 */
async function reviewed_status(
  repo: string,
  sha: string,
): Promise<'success' | 'failed' | 'not_ready' | 'unknown'> {
  try {
    // 查询状态
    const statuses = await api_json<GitHubStatus[]>(
      `/repos/${repo}/commits/${sha}/statuses?per_page=100`,
    )
    if (!Array.isArray(statuses)) return 'not_ready'
    const latest = statuses.find((item) => item && item.context === REVIEW_STATUS_CONTEXT)
    if (!latest) return 'not_ready'
    if (latest.state === 'success') return 'success'
    if (latest.state === 'failure' || latest.state === 'error') return 'failed'
    return 'not_ready'
  } catch (exc) {
    println(`::warning::无法读取跨作者审核状态：${exc}`)
    return 'unknown'
  }
}

/** 判断 PR 可合并 */
async function pull_request_is_open(
  repo: string,
  pr_number: string | number,
  expectedSha: string | null = null,
): Promise<boolean> {
  try {
    const pr = await api_json<GitHubPullRequest>(`/repos/${repo}/pulls/${pr_number}`)
    if (!pr || typeof pr !== 'object' || Array.isArray(pr) || pr.state !== 'open' || pr.merged) {
      return false
    }
    if (expectedSha && pr.head && pr.head.sha !== expectedSha) return false
    return true
  } catch (exc) {
    println(`::warning::无法确认 PR 状态：${exc}`)
    return true
  }
}

/** 审核 PR */
async function review_pull_request(
  repo: string,
  pr_number: string | number,
  author: string,
  head_sha: string,
  base_ref: string,
  cfg: ReviewConfig,
  files: ChangedFile[] | null = null,
): Promise<ReviewOutcome> {
  if (files === null) {
    try {
      files = await list_changed_files(repo, pr_number, Number(cfg.max_files_per_pr ?? 100) + 1)
    } catch (exc) {
      return {
        files: null,
        errors: [],
        warnings: [],
        rows: [],
        foreign: [],
        known_folders: new Set(),
        api_error: `无法读取 PR 改动（GitHub API 错误）：${exc}`,
      }
    }
  }

  // 收集 guid
  const script_guids: Array<[string, string]> = []
  const known_folders = new Set<string>()
  let errors: string[]
  let warnings: string[]
  let rows: Array<[string, string]>
  let foreign: ForeignChange[] = []
  if (!files.length) {
    errors = ['该 PR 没有包含任何文件改动']
    warnings = []
    rows = []
    foreign = []
  } else {
    ;[errors, warnings, rows, foreign] = check_change_set(files, author, cfg)

    if (errors.length) {
      warnings.push('存在路径层面的错误，本次跳过 .cs 内容校验；修复后重新提交会重新校验。')
      return { files, errors, warnings, rows, foreign, known_folders, api_error: null }
    }

    for (const item of files) {
      const filePath = item.filename ?? ''
      if (item.status === 'removed') continue
      let data
      let fetch_error
      try {
        // 下载内容
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
        text = utf8DecodeStrict(data as Buffer)
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

    // 查重准备
    const own_paths = new Set()
    for (const item of files) {
      for (const key of ['filename', 'previous_filename']) {
        if (item[key]) own_paths.add(item[key])
      }
    }

    let guid_map = null
    try {
      guid_map = await load_guid_map(repo, base_ref)
    } catch (exc) {
      println(`::warning::无法读取 guid 映射表，本次跳过 guid 查重：${exc}`)
    }
    if (guid_map === null && base_ref) {
      println(`::notice::${GUID_MAP_PATH} 尚不存在，本次跳过 guid 查重`)
    }
    for (const mapped of Object.values(guid_map || {})) {
      if (typeof mapped === 'string' && mapped.includes('/')) {
        known_folders.add(normalize_login(mapped.split('/')[0], true))
      }
    }
    errors.push(...check_guid_collisions(script_guids, own_paths, author, guid_map, cfg))
  }

  return { files, errors, warnings, rows, foreign, known_folders, api_error: null }
}

/** CI 入口 */
export async function run_ci(): Promise<number> {
  const repo = process.env.GITHUB_REPOSITORY ?? ''
  const pr_number = process.env.PR_NUMBER ?? ''
  const author = process.env.PR_AUTHOR ?? ''
  const head_sha = process.env.PR_HEAD_SHA ?? ''

  // 环境变量
  const missing = [
    ['GITHUB_REPOSITORY', repo], ['PR_NUMBER', pr_number], ['PR_AUTHOR', author],
    ['PR_HEAD_SHA', head_sha],
  ].filter(([, value]) => !value).map(([key]) => key)
  if (missing.length) {
    println(`::error::缺少环境变量：${missing.join(', ')}`)
    return 2
  }

  const cfg = load_config()
  const base_ref = process.env.PR_BASE_REF || process.env.PR_BASE_SHA || ''

  if (!(await pull_request_is_open(repo, pr_number, head_sha))) {
    println(`::notice::PR #${pr_number} 已关闭、已合并或已有新提交，跳过审核`)
    set_output('auto_merge', 'false')
    return 0
  }

  // 执行审核
  const review = await review_pull_request(repo, pr_number, author, head_sha, base_ref, cfg)
  if (review.api_error) {
    const message = review.api_error
    println(`::error::${message}`)
    write_step_summary(`## 🤖 PR 自动审核：无法执行\n\n${message}\n`)
    try {
      await post_comment(repo, pr_number, `## 🤖 PR 自动审核：无法执行\n\n${message}\n\n未做合并，请维护者手动检查。`)
    } catch {
      // 忽略
    }
    return 2
  }

  const { rows, errors, warnings, foreign, known_folders } = review

  if (!(await pull_request_is_open(repo, pr_number, head_sha))) {
    println(`::notice::PR #${pr_number} 已关闭、已合并或已有新提交，跳过本次审核结果`)
    set_output('auto_merge', 'false')
    return 0
  }

  if (errors.length) {
    if (foreign.length) {
      await set_review_status(repo, head_sha, 'failure', '内容校验未通过，请修复后重新提交')
    }
    let report = build_report('❌ 未通过', rows, errors, warnings, foreign)
    if (foreign.length) {
      const command = primary_approval_command(cfg)
      report +=
        `\n\n此外，本 PR 修改了其他作者的文件夹；修复上述问题后仍需文件夹所有者回复 \`${command}\` ` +
        '或维护者确认，不会自动合并。'
    }
    println(safe_stdout(report))
    write_step_summary(report)
    await post_comment_safely(repo, pr_number, report)
    set_output('auto_merge', 'false')
    return 1
  }

  if (foreign.length) {
    const mentionable = foreign.filter(
      (item) => known_folders.has(normalize_login(item.folder, true)),
    )
    const owners = foreign_owners(mentionable)
    const who = owners.length ? owners.map((owner) => `@${owner}`).join(' ') : '文件夹所有者'
    const command = primary_approval_command(cfg)
    const short_sha = head_sha.slice(0, 7)
    await set_review_status(repo, head_sha, 'success', `内容校验通过，等待所有者批准（${short_sha}）`)
    let report = build_report('⏳ 待人工审核', rows, errors, warnings, foreign)
    report +=
      `\n\n当前版本：\`${short_sha}\`\n\n` +
      `脚本内容校验已通过。请 ${who} 回复 \`${command}\` 确认，或由维护者手动合并。` +
      '本 PR 需要所有者确认后才会合并。'
    println(safe_stdout(report))
    write_step_summary(report)
    await add_labels_safely(repo, pr_number, foreign_labels(cfg))
    await post_comment_safely(repo, pr_number, report)
    set_output('auto_merge', 'false')
    return 0
  }

  const report = build_report('✅ 通过', rows, errors, warnings)
  println(safe_stdout(report))
  write_step_summary(report)
  await remove_labels_safely(repo, pr_number, foreign_labels(cfg))
  set_output('auto_merge', 'true')
  if (warnings.length) {
    await post_comment_safely(repo, pr_number, report + '\n校验通过，将自动 squash 合并。')
  }
  return 0
}

/** 批准入口 */
export async function run_approval(): Promise<number> {
  const repo = process.env.GITHUB_REPOSITORY ?? ''
  const pr_number = process.env.PR_NUMBER ?? ''
  const comment_author = process.env.COMMENT_AUTHOR ?? ''
  const comment_body = process.env.COMMENT_BODY ?? ''
  const comment_type = process.env.COMMENT_TYPE ?? ''

  const missing = [
    ['GITHUB_REPOSITORY', repo], ['PR_NUMBER', pr_number], ['COMMENT_AUTHOR', comment_author],
  ].filter(([, value]) => !value).map(([key]) => key)
  if (missing.length) {
    println(`::error::缺少环境变量：${missing.join(', ')}`)
    return 2
  }

  if (comment_type === 'Bot' || comment_author.toLowerCase().endsWith('[bot]')) return 0

  const cfg = load_config()
  if (!is_approval_comment(comment_body, cfg)) return 0

  let pr: GitHubPullRequest | null
  try {
    pr = await api_json<GitHubPullRequest>(`/repos/${repo}/pulls/${pr_number}`)
  } catch (exc) {
    println(`::warning::无法读取 PR #${pr_number}：${exc}`)
    return 2
  }
  if (!pr || typeof pr !== 'object' || Array.isArray(pr)) {
    println(`::warning::PR #${pr_number} 返回内容不是对象，跳过`)
    return 2
  }
  if (pr.state !== 'open' || pr.merged) {
    println(`::notice::PR #${pr_number} 已关闭或已合并，忽略批准命令`)
    return 0
  }
  if (pr.draft) {
    println(`::notice::PR #${pr_number} 仍是 draft，忽略批准命令`)
    return 0
  }

  const author = pr.user && pr.user.login ? String(pr.user.login) : ''
  const head_sha = pr.head && pr.head.sha ? String(pr.head.sha) : ''
  const base_ref = pr.base && pr.base.ref ? String(pr.base.ref) : ''
  if (!author || !head_sha) {
    println(`::warning::PR #${pr_number} 缺少作者或 head SHA，无法审核`)
    return 2
  }
  if (normalize_login(author, true) === normalize_login(comment_author, true)) {
    println('::notice::PR 作者本人不能批准跨作者修改')
    return 0
  }

  let files
  try {
    files = await list_changed_files(repo, pr_number, Number(cfg.max_files_per_pr ?? 100) + 1)
  } catch (exc) {
    const message = `无法读取 PR 改动（GitHub API 错误）：${exc}`
    println(`::error::${message}`)
    await post_comment_safely(repo, pr_number, `## 🤖 所有者批准：无法执行\n\n${message}\n`)
    return 2
  }

  // 所有者预检
  const [, , , pre_foreign] = check_change_set(files, author, cfg)
  if (!pre_foreign.length) {
    println(`::notice::PR #${pr_number} 没有修改别人的文件夹，不需要所有者批准`)
    return 0
  }
  const folders = foreign_folders(pre_foreign)
  const owner_key = normalize_login(comment_author, true)
  const owns_some = folders.some((folder) => normalize_login(folder, true) === owner_key)
  if (!owns_some) {
    println(`::notice::${comment_author} 不是被修改文件夹的所有者，忽略批准命令`)
    return 0
  }

  // 审核状态
  const status = await reviewed_status(repo, head_sha)
  if (status !== 'success') {
    const short_sha = head_sha.slice(0, 7)
    let message
    if (status === 'unknown') {
      message = '暂时无法确认当前版本的自动审核状态，请稍后重试或联系维护者。'
    } else if (status === 'failed') {
      message = '当前版本的自动审核未通过，请先修复问题并等待重新审核。'
    } else {
      message =
        '当前版本还没有完成自动审核，或代码在审核后已更新；请等待机器人重新审核后再回复批准命令。'
    }
    await post_comment_safely(
      repo,
      pr_number,
      `## 🤖 所有者批准\n\n当前版本：\`${short_sha}\`\n\n${message}`,
    )
    return 0
  }

  const review = await review_pull_request(repo, pr_number, author, head_sha, base_ref, cfg, files)
  if (review.api_error) {
    const message = review.api_error
    println(`::error::${message}`)
    await post_comment_safely(repo, pr_number, `## 🤖 所有者批准：无法执行\n\n${message}\n`)
    return 2
  }

  const { rows, errors, warnings, foreign } = review
  if (!foreign.length) {
    println(`::notice::PR #${pr_number} 没有修改别人的文件夹，不需要所有者批准`)
    return 0
  }

  if (errors.length) {
    await set_review_status(repo, head_sha, 'failure', '内容校验未通过，请修复后重新提交')
    let report = build_report('❌ 未通过', rows, errors, warnings, foreign)
    report += '\n\n所有者批准不会绕过校验，请先修复上述问题。'
    println(safe_stdout(report))
    write_step_summary(report)
    await post_comment_safely(repo, pr_number, report)
    return 1
  }

  if (!(await pull_request_is_open(repo, pr_number, head_sha))) {
    await post_comment_safely(
      repo,
      pr_number,
      '## 🤖 所有者批准\n\n审核期间 PR 已更新或关闭，请等待机器人重新审核后再回复批准命令。',
    )
    return 0
  }

  let merge_result
  try {
    // 合并 PR
    merge_result = await api_send('PUT', `/repos/${repo}/pulls/${pr_number}/merge`, {
      merge_method: 'squash',
      sha: head_sha,
    })
  } catch (exc) {
    const message =
      `自动合并失败：${exc}。可能是 PR 在审核后又有更新、存在合并冲突，或分支保护不允许。`
    println(`::warning::${message}`)
    await post_comment_safely(
      repo,
      pr_number,
      `## 🤖 所有者批准\n\n${message}\n\n请重新确认后再次回复批准命令，或由维护者手动合并。`,
    )
    return 1
  }

  if (!isPlainObject(merge_result) || merge_result.merged !== true) {
    const message = '自动合并失败：GitHub 未返回 merged=true，请维护者手动检查。'
    println(`::warning::${message}`)
    await post_comment_safely(repo, pr_number, `## 🤖 所有者批准\n\n${message}\n`)
    return 1
  }

  await remove_labels_safely(repo, pr_number, foreign_labels(cfg))
  await post_comment_safely(
    repo,
    pr_number,
    `## 🤖 所有者批准\n\n✅ @${comment_author} 已确认，PR 已 squash 合并。`,
  )
  set_output('merged', 'true')
  return 0
}

/** 读取 UTF-8 */
export function read_utf8(filePath: string): [string | null, number] {
  let data: Buffer
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

/** 严格解码 */
function utf8DecodeStrict(data: Uint8Array): string {
  return new TextDecoder('utf-8', { fatal: true }).decode(data)
}

/** 格式化文件错误 */
function pyOsErrorString(err: unknown): string {
  const table: Record<string, [number, string]> = {
    ENOENT: [2, 'No such file or directory'],
    EACCES: [13, 'Permission denied'],
    EPERM: [1, 'Operation not permitted'],
    EISDIR: [21, 'Is a directory'],
    ENOTDIR: [20, 'Not a directory'],
  }
  const nodeError = (err ?? {}) as { code?: string; path?: string; message?: string }
  const entry = table[nodeError.code ?? '']
  if (entry) {
    const target = nodeError.path != null ? `: '${nodeError.path}'` : ''
    return `[Errno ${entry[0]}] ${entry[1]}${target}`
  }
  return nodeError.message ? nodeError.message : String(err)
}

/** 校验 JSON 命令 */
export function cmd_check_json(paths: string[], cfg: ReviewConfig): number {
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

/** 推断文件夹 */
export function infer_owner(filePath: string): string {
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

/** 可移植相对路径 */
function relativePortable(target: string, base: string): string {
  if (process.platform === 'win32') {
    const targetRoot = path.parse(target).root.toLowerCase()
    const baseRoot = path.parse(base).root.toLowerCase()
    if (targetRoot !== baseRoot) throw new Error('path is on mount')
  }
  return path.relative(base, target).split(path.sep).join('/')
}

/** 校验 .cs 命令 */
export function cmd_check_cs(
  paths: string[],
  owner: string | undefined,
  cfg: ReviewConfig,
): number {
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
        `✅ ${filePath} 通过：Name=${pyReprAny(meta!.name)} Guid=${pyReprAny(meta!.guid)} ` +
        `Version=${pyReprAny(meta!.version)} Author=${pyReprAny(meta!.author)} ` +
        `TerritoryIds=${pyReprAny(meta!.territorys)}`
      )
    }
  }
  return exit_code
}

/** 校验路径命令 */
export function cmd_path_check(
  author: string,
  paths: string[],
  cfg: ReviewConfig,
): number {
  let exit_code = 0
  for (const filePath of paths) {
    const errors: string[] = []
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

/** 命令行入口 */
export async function main(argv = process.argv.slice(2)): Promise<number> {
  let values: {
    'check-cs'?: string[]
    'check-json'?: string[]
    owner?: string
    'path-check'?: string
    path?: string[]
    'approve-comment'?: boolean
  }
  let positionals: string[]
  try {
    const parsed = parseArgs({
      args: argv,
      options: {
        'check-cs': { type: 'string', multiple: true },
        'check-json': { type: 'string', multiple: true },
        owner: { type: 'string' },
        'path-check': { type: 'string' },
        path: { type: 'string', multiple: true },
        'approve-comment': { type: 'boolean' },
      },
      allowPositionals: true,
      strict: false,
    })
    values = parsed.values as unknown as typeof values
    positionals = parsed.positionals
  } catch (exc) {
    const message = exc instanceof Error ? exc.message : String(exc)
    process.stderr.write(`usage: pr_review.ts [--check-cs FILE ...] [--check-json FILE ...]\npr_review.ts: error: ${message}\n`)
    return 2
  }

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
      process.stderr.write('usage: pr_review.ts [--path-check AUTHOR --path PATH ...]\npr_review.ts: error: --path-check 需要至少一个 --path\n')
      return 2
    }
    return cmd_path_check(values['path-check'], pathArgs, cfg)
  }
  if (values['approve-comment']) {
    return run_approval()
  }
  return run_ci()
}

/** JSON 错误 */
export class PyJsonDecodeError extends Error {
  msg: string
  doc: string
  pos: number
  lineno: number
  colno: number

  /** 构造函数 */
  constructor(msg: string, doc: string, pos: number) {
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

/** 统计换行 */
function countNewlines(doc: string, start: number, end: number): number {
  let count = 0
  for (let i = start; i < end; i += 1) {
    if (doc[i] === '\n') count += 1
  }
  return count
}

const JSON_WS = ' \t\n\r'
/** JSON 空白判断 */
const isJsonWs = (ch: string | undefined): boolean => ch !== undefined && ch !== '' && JSON_WS.includes(ch)

/** 跳过 JSON 空白 */
function jsonWs(doc: string, pos: number): number {
  let i = pos
  while (i < doc.length && isJsonWs(doc[i])) i += 1
  return i
}

const JSON_NUMBER_RE = /^(-?(?:0|[1-9][0-9]*))(\.[0-9]+)?([eE][-+]?[0-9]+)?/
const JSON_HEX_RE = /^[0-9A-Fa-f]{4}/
const JSON_ESCAPES: Record<string, string> = { '"': '"', '\\': '\\', '/': '/', b: '\b', f: '\f', n: '\n', r: '\r', t: '\t' }

/** 判断中止信号 */
function isStopSignal(err: unknown): err is { stop: number } {
  if (typeof err !== 'object' || err === null) return false
  return typeof (err as { stop?: unknown }).stop === 'number'
}

/** 解码 \uXXXX */
function decodeUXXXX(doc: string, pos: number): number {
  const m = JSON_HEX_RE.exec(doc.slice(pos + 1))
  if (m) return parseInt(m[0], 16)
  throw new PyJsonDecodeError('Invalid \\uXXXX escape', doc, pos)
}

/** 扫描字符串 */
function jsonScanString(doc: string, end: number): [string, number] {
  const parts: string[] = []
  const begin = end - 1
  for (;;) {
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

/** 扫描单个值 */
function jsonScanOnce(doc: string, idx: number): [JsonValue, number] {
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

/** 解析 JSON 对象 */
function jsonParseObject(doc: string, end: number): [Record<string, JsonValue>, number] {
  const obj: Record<string, JsonValue> = Object.create(null)
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
      if (isStopSignal(err)) {
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

/** 解析 JSON 数组 */
function jsonParseArray(doc: string, end: number): [JsonValue[], number] {
  const values: JsonValue[] = []
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
      if (isStopSignal(err)) {
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
      // 忽略
    }
    if (nextchar === ']') {
      throw new PyJsonDecodeError('Illegal trailing comma before end of array', doc, commaIdx)
    }
  }
  return [values, end]
}

/** 解析 JSON */
export function pyJsonLoads(text: string): JsonValue {
  // 跳过空白
  let end = jsonWs(text, 0)
  let obj: JsonValue
  try {
    ;[obj, end] = jsonScanOnce(text, end)
  } catch (err) {
    if (isStopSignal(err)) {
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

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().then((code) => process.exit(code))
}
