import fs from 'node:fs'
import path from 'node:path'
import process from 'node:process'
import { execFileSync } from 'node:child_process'
import { parseArgs } from 'node:util'
import { fileURLToPath } from 'node:url'

import * as prReview from './pr_review.ts'
import type { ReviewConfig, ScriptMeta } from './pr_review.ts'

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url))
export const REPO_ROOT_DEFAULT = path.resolve(SCRIPT_DIR, '../..')

/** 字段顺序 */
export const CANONICAL_FIELD_ORDER = [
  'Name',
  'Guid',
  'Version',
  'Author',
  'DownloadUrl',
  'Note',
  'UpdateInfo',
  'UpdateTime',
  'TerritoryIds',
]

/** 排除目录 */
const EXCLUDED_DIRS = new Set(['.git', 'node_modules'])

/** 索引条目 */
export interface OnlineScriptEntry {
  Name: string;
  Guid: string;
  Version: string;
  Author: string;
  DownloadUrl: string;
  Repo?: string;
  Note?: string;
  UpdateInfo?: string;
  UpdateTime?: string;
  TerritoryIds?: number[];
}

/** Guid 冲突 */
export type Conflict = [guid: string, winner: string, loser: string];

/** 跳过文件 */
export type SkippedFile = [relPath: string, errors: string[]];

/** 贡献者计数 */
export type OwnerCounts = Record<string, number>;

/** guid 映射 */
export type GuidMap = Record<string, string>;

/** 收集结果 */
export interface CollectResult {
  records: Array<[owner: string, relPath: string, entry: OnlineScriptEntry]>;
  skipped: SkippedFile[];
  warnings: string[];
  fileCounts: OwnerCounts;
}

/** 合并结果 */
export interface MergeResult {
  merged: OnlineScriptEntry[];
  conflicts: Conflict[];
  ownerCounts: OwnerCounts;
  guidMap: GuidMap;
}

/** 构建选项 */
export interface BuildOptions {
  outDir?: string;
  rootJsonPath?: string;
  guidMapPath?: string;
  rootJsonMode?: boolean;
  forbidEmpty?: boolean;
  strict?: boolean;
  noHtml?: boolean;
}

/** 输出目标 */
export interface OutputDest {
  site: string;
  root: string;
  guidMap: string;
}

const INDEX_HTML_TEMPLATE = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>KodakkuAssistScriptLibrary 索引</title>
<style>
  body { font-family: -apple-system, "Segoe UI", "Microsoft YaHei", sans-serif;
         max-width: 860px; margin: 40px auto; padding: 0 16px; line-height: 1.6; color: #24292f; }
  code { background: #f3f4f6; padding: 2px 6px; border-radius: 4px; }
  table { border-collapse: collapse; width: 100%; margin: 16px 0; }
  th, td { border: 1px solid #d0d7de; padding: 6px 10px; text-align: left; }
  th { background: #f6f8fa; }
  .warn { color: #9a6700; }
  .muted { color: #57606a; font-size: 0.9em; }
</style>
</head>
<body>
<h1>KodakkuAssistScriptLibrary 索引</h1>
<p>共 <strong>{ENTRY_COUNT}</strong> 个脚本，来自 <strong>{OWNER_COUNT}</strong> 位贡献者。</p>
{TABLE}
{PROBLEMS}
<p class="muted"><a href="index.json">index.json</a> · 最近生成：{GENERATED_AT}（UTC）</p>
</body>
</html>
`

/** 查找脚本文件 */
export function discoverFiles(
  repoRoot: string,
  ignoreDirs: string[] | null | undefined,
): Array<[string, string]> {
  const ignored = new Set([...(ignoreDirs ?? []).map(String), ...EXCLUDED_DIRS])
  const found: Array<[string, string]> = []
  // 遍历首层目录
  for (const name of fs.readdirSync(repoRoot).sort()) {
    const full = path.join(repoRoot, name)
    let isDir = false
    try {
      isDir = fs.statSync(full).isDirectory()
    } catch {
      isDir = false
    }
    if (!isDir) continue
    // 跳过隐藏目录
    if (name.startsWith('.') || name.startsWith('_') || ignored.has(name)) continue
    for (const rel of walkCsFiles(full, ignored)) {
      found.push([name, path.relative(repoRoot, rel).split(path.sep).join('/')])
    }
  }
  return found
}

/** 递归收集 .cs */
function walkCsFiles(dir: string, ignored: Set<string>): string[] {
  const out: string[] = []
  const entries = fs.readdirSync(dir, { withFileTypes: true })
  const dirs = entries
    .filter((entry) => entry.isDirectory() && !ignored.has(entry.name))
    .map((entry) => entry.name)
    .sort()
  const files = entries
    .filter((entry) => !entry.isDirectory())
    .map((entry) => entry.name)
    .sort()

  // 先收集文件
  for (const name of files) {
    if (name.toLowerCase().endsWith('.cs')) out.push(path.join(dir, name))
  }
  // 再递归目录
  for (const name of dirs) out.push(...walkCsFiles(path.join(dir, name), ignored))
  return out
}

/** URL 编码 */
function pyQuote(value: string, safe = ''): string {
  const unreserved = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_.-~'
  let out = ''
  for (const ch of value) {
    if (unreserved.includes(ch) || safe.includes(ch)) {
      out += ch
      continue
    }
    for (const byte of Buffer.from(ch, 'utf8')) {
      out += `%${byte.toString(16).toUpperCase().padStart(2, '0')}`
    }
  }
  return out
}

/** 生成下载链接 */
export function downloadUrl(repo: string, branch: string, relPath: string): string {
  return `https://raw.githubusercontent.com/${repo}/${pyQuote(branch, '')}/${pyQuote(relPath, '/')}`
}

/** 提交时间戳 */
function commitEpoch(repoRoot: string, relPath: string): number | null {
  try {
    const stdout = execFileSync('git', ['-C', repoRoot, 'log', '-1', '--format=%ct', '--', relPath], {
      encoding: 'utf8',
      timeout: 10_000,
      stdio: ['ignore', 'pipe', 'ignore'],
    })
    const stamp = stdout.trim()
    return /^\d+$/.test(stamp) ? Number(stamp) : null
  } catch {
    return null
  }
}

/** 计算更新时间 */
export function lastUpdateTime(repoRoot: string, relPath: string): string {
  let epoch = commitEpoch(repoRoot, relPath)
  if (epoch === null) {
    try {
      epoch = Math.floor(fs.statSync(path.join(repoRoot, relPath)).mtimeMs / 1000)
    } catch {
      return ''
    }
  }
  return new Date(epoch * 1000).toISOString().replace(/\.\d{3}Z$/, 'Z')
}

/** 组装索引条目 */
export function buildEntry(
  meta: ScriptMeta,
  repo: string,
  branch: string,
  relPath: string,
  updateTime: string,
): OnlineScriptEntry {
  const entry: Record<string, unknown> = {
    Name: meta.name,
    Guid: meta.guid,
    Version: meta.version,
    Author: meta.author,
    DownloadUrl: downloadUrl(repo, branch, relPath),
    Note: meta.note,
    UpdateInfo: meta.update_info,
    UpdateTime: updateTime,
    TerritoryIds: meta.territorys,
  }
  const out: Record<string, unknown> = {}
  for (const key of CANONICAL_FIELD_ORDER) {
    if (!(key in entry)) continue
    const value = entry[key]
    // 省略空字段
    if (value === '' || value === null || value === undefined) continue
    if (Array.isArray(value) && value.length === 0) continue
    out[key] = value
  }
  return out as unknown as OnlineScriptEntry
}

/** 收集并校验 */
export function collect(
  repoRoot: string,
  cfg: ReviewConfig,
  repo: string,
  branch: string,
): CollectResult {
  const records: Array<[string, string, OnlineScriptEntry]> = []
  const skipped: SkippedFile[] = []
  const warnings: string[] = []
  const fileCounts: OwnerCounts = Object.create(null)

  for (const [owner, rel] of discoverFiles(repoRoot, cfg.ignore_dirs)) {
    fileCounts[owner] = (fileCounts[owner] ?? 0) + 1

    let raw: Buffer
    try {
      raw = fs.readFileSync(path.join(repoRoot, rel))
    // 读取失败
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error)
      skipped.push([rel, [`无法读取：${message}`]])
      continue
    }

    let text: string
    try {
      text = new TextDecoder('utf-8', { fatal: true }).decode(raw)
    } catch {
      skipped.push([rel, [`不是合法的 UTF-8 编码：${describeUtf8Error(raw)}`]])
      continue
    }

    // 校验脚本
    const { meta, errors, warnings: fileWarnings } = prReview.validateScriptFile(text, rel, owner, cfg)
    warnings.push(...fileWarnings)
    // 跳过不合规
    if (errors.length || meta === null) {
      skipped.push([rel, errors.length ? errors : ['无法解析出脚本信息']])
      continue
    }

    const updateTime = lastUpdateTime(repoRoot, rel)
    records.push([owner, rel, buildEntry(meta, repo, branch, rel, updateTime)])
  }

  return { records, skipped, warnings, fileCounts }
}

/** 描述编码错误 */
function describeUtf8Error(raw: Buffer): string {
  for (let i = 0; i < raw.length; i += 1) {
    const byte = raw[i]
    if (byte < 0x80) continue
    if (byte >= 0xc2 && byte <= 0xf4) continue
    return `'utf-8' codec can't decode byte 0x${byte.toString(16).padStart(2, '0')} in position ${i}: invalid start byte`
  }
  return '输入不是合法的 UTF-8 序列'
}

/** 合并去重 */
export function mergeRecords(
  records: Array<[string, string, OnlineScriptEntry]>,
): MergeResult {
  const merged: OnlineScriptEntry[] = []
  const conflicts: Conflict[] = []
  const ownerCounts: OwnerCounts = Object.create(null)
  const guidMap: GuidMap = Object.create(null)
  const seen: GuidMap = Object.create(null)

  for (const [owner, rel, entry] of records) {
    const guid = String(entry.Guid ?? '').trim()
    const key = guid.toLowerCase()
    // guid 去重
    if (key in seen) {
      conflicts.push([guid, seen[key], rel])
      continue
    }
    seen[key] = rel
    guidMap[key] = rel
    ownerCounts[owner] = (ownerCounts[owner] ?? 0) + 1
    merged.push(entry)
  }

  return { merged, conflicts, ownerCounts, guidMap }
}

/** 渲染贡献者表 */
export function renderTable(ownerCounts: OwnerCounts, fileCounts: OwnerCounts): string {
  const rows = ['<table>', '<tr><th>贡献者</th><th>脚本文件</th><th>收录脚本</th></tr>']
  for (const owner of Object.keys(ownerCounts).sort()) {
    rows.push(
      `<tr><td>${owner}</td><td>${fileCounts[owner] ?? 0}</td>` +
        `<td>${ownerCounts[owner]}</td></tr>`,
    )
  }
  rows.push('</table>')
  return rows.join('\n')
}

/** 渲染问题列表 */
export function renderProblems(conflicts: Conflict[], skipped: SkippedFile[]): string {
  if (!conflicts.length && !skipped.length) return ''
  const parts = ['<h2 class="warn">需要注意</h2>', '<ul>']
  for (const [guid, winner, loser] of conflicts) {
    parts.push(
      `<li class="warn">Guid <code>${guid}</code> 重复：<code>${loser}</code> ` +
        `与 <code>${winner}</code> 冲突，已保留前者。</li>`,
    )
  }
  for (const [rel, errors] of skipped) {
    parts.push(`<li class="warn"><code>${rel}</code> 未通过校验，已跳过：${errors[0]}</li>`)
  }
  parts.push('</ul>')
  return parts.join('\n')
}

/** 渲染说明页 */
export function renderHtml(
  merged: OnlineScriptEntry[],
  ownerCounts: OwnerCounts,
  fileCounts: OwnerCounts,
  conflicts: Conflict[],
  skipped: SkippedFile[],
): string {
  return INDEX_HTML_TEMPLATE.replaceAll('{ENTRY_COUNT}', String(merged.length))
    .replaceAll('{OWNER_COUNT}', String(Object.keys(ownerCounts).length))
    .replaceAll('{TABLE}', renderTable(ownerCounts, fileCounts))
    .replaceAll('{PROBLEMS}', renderProblems(conflicts, skipped))
    .replaceAll('{GENERATED_AT}', formatUtcMinute(new Date()))
}

/** 格式化 UTC 时间 */
function formatUtcMinute(date: Date): string {
  /** 补零 */
  const pad = (value: number): string => String(value).padStart(2, '0')
  return (
    `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())} ` +
    `${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())}`
  )
}

/** 写文本文件 */
export function writeText(filePath: string, text: string): void {
  const parent = path.dirname(path.resolve(filePath))
  if (parent) fs.mkdirSync(parent, { recursive: true })
  fs.writeFileSync(filePath, text, 'utf8')
}

/** 写出产物 */
export function writeOutputs(
  dest: OutputDest,
  payload: string,
  guidMap: GuidMap,
  html: string,
): void {
  if (dest.site) {
    fs.mkdirSync(dest.site, { recursive: true })
    writeText(path.join(dest.site, 'index.json'), `${payload}\n`)
    if (html) writeText(path.join(dest.site, 'index.html'), `${html}\n`)
  }
  if (dest.root) writeText(dest.root, `${payload}\n`)
  if (dest.guidMap) {
    writeText(dest.guidMap, `${canonicalJson(guidMap)}\n`)
  }
}

/** 稳定序列化 */
function canonicalJson(value: unknown): string {
  /** 递归排序键 */
  const sortValue = (input: unknown): unknown => {
    if (Array.isArray(input)) return input.map(sortValue)
    if (input && typeof input === 'object') {
      const out: Record<string, unknown> = {}
      // 排序对象键
      const record = input as Record<string, unknown>
      for (const key of Object.keys(input).sort()) out[key] = sortValue(record[key])
      return out
    }
    return input
  }
  return JSON.stringify(sortValue(value), null, 2)
}

/** 报告统计 */
export type ReportStats = [
  merged: OnlineScriptEntry[],
  fileCounts: OwnerCounts,
  ownerCounts: OwnerCounts,
  conflicts: Conflict[],
  skipped: SkippedFile[],
  warnings: string[],
];

/** 输出报告 */
export function emitReport(
  stats: ReportStats,
  dest: OutputDest,
  noHtml = false,
): string {
  const [merged, fileCounts, ownerCounts, conflicts, skipped, warnings] = stats
  const lines = ['## 📦 索引生成结果', '']
  lines.push(`- 收录脚本：**${merged.length}**`)
  lines.push(`- 贡献者：**${Object.keys(fileCounts).length}**`)
  lines.push(`- 扫描文件：**${Object.values(fileCounts).reduce((sum, n) => sum + n, 0)}** 个 .cs`)
  lines.push('')

  if (conflicts.length) {
    lines.push(`### ⚠️ Guid 冲突（${conflicts.length}）`)
    lines.push('')
    for (const [guid, winner, loser] of conflicts) {
      lines.push(`- \`${guid}\`：\`${loser}\` 与 \`${winner}\` 冲突，已保留 \`${winner}\``)
      console.log(`::warning::Guid 冲突：${loser} 与 ${winner} 重复，已保留 ${winner}`)
    }
    lines.push('')
  }

  if (skipped.length) {
    lines.push(`### ❌ 未通过校验被跳过（${skipped.length}）`)
    lines.push('')
    for (const [rel, errors] of skipped) {
      lines.push(`- \`${rel}\`：${errors[0]}（共 ${errors.length} 个问题）`)
      console.log(`::warning::跳过不合规文件 ${rel}：${errors[0]}`)
    }
    lines.push('')
  }

  if (warnings.length) {
    lines.push(`### 提醒（${warnings.length}）`)
    lines.push('')
    for (const item of warnings) lines.push(`- ${item}`)
    lines.push('')
  }

  if (!merged.length) {
    lines.push('> ⚠️ 当前没有任何脚本被收录，生成的是空索引。')
    lines.push('')
    console.log('::warning::没有收录到任何脚本，生成的是空索引')
  } else if (!conflicts.length && !skipped.length && !warnings.length) {
    lines.push('没有发现问题。')
    lines.push('')
  }

  const outputs = []
  if (dest.site) {
    const contents = noHtml ? 'index.json' : 'index.json + index.html'
    outputs.push(`\`${dest.site}\`（${contents}）`)
  }
  if (dest.root) outputs.push(`\`${dest.root}\``)
  if (dest.guidMap) outputs.push(`\`${dest.guidMap}\``)
  lines.push(`输出：${outputs.length ? outputs.join('、') : '没有指定输出'}`)

  const report = lines.join('\n')
  console.log(report)

  const summaryPath = process.env.GITHUB_STEP_SUMMARY
  if (summaryPath) {
    try {
      fs.appendFileSync(summaryPath, `${report}\n`, 'utf8')
    } catch {
      // 忽略
    }
  }
  return report
}

/** 执行 git 命令 */
function git(args: string[]): string {
  try {
    return execFileSync('git', ['-C', REPO_ROOT_DEFAULT, ...args], {
      encoding: 'utf8',
      timeout: 10_000,
      stdio: ['ignore', 'pipe', 'ignore'],
    }).trim()
  } catch {
    return ''
  }
}

/** 解析仓库名 */
export function resolveRepo(explicit?: string): string {
  if (explicit) return explicit
  if (process.env.GITHUB_REPOSITORY) return process.env.GITHUB_REPOSITORY
  const url = git(['config', '--get', 'remote.origin.url'])
  const match = /github\.com[:/]([^/]+)\/(.+?)(?:\.git)?$/.exec(url)
  return match ? `${match[1]}/${match[2]}` : ''
}

/** 解析分支名 */
export function resolveBranch(explicit?: string): string {
  if (explicit) return explicit
  if (process.env.GITHUB_REF_NAME) return process.env.GITHUB_REF_NAME
  const branch = git(['rev-parse', '--abbrev-ref', 'HEAD'])
  return branch || 'main'
}

/** 生成索引 */
export function build(
  repoRoot: string,
  cfg: ReviewConfig,
  repo: string,
  branch: string,
  options: BuildOptions = {},
): number {
  const {
    outDir = '',
    rootJsonPath = '',
    guidMapPath = '',
    rootJsonMode = false,
    forbidEmpty = false,
    strict = false,
    noHtml = false,
  } = options

  // 收集数据
  const { records, skipped, warnings, fileCounts } = collect(repoRoot, cfg, repo, branch)
  const { merged, conflicts, ownerCounts, guidMap } = mergeRecords(records)

  if (!merged.length && forbidEmpty) {
    console.log('::error::没有收录到任何脚本，拒绝生成空索引（--forbid-empty）')
    return 1
  }

  // 自检结果
  const { errors: checkErrors } = prReview.validateJsonDocument(
    JSON.stringify(merged),
    'OnlineRepo.json',
    cfg,
  )
  if (checkErrors.length) {
    console.log('::error::合并结果自身不合规，说明合并逻辑有 bug：')
    for (const item of checkErrors) console.log(`  - ${item}`)
    return 2
  }

  const dest = { site: outDir, root: rootJsonPath, guidMap: guidMapPath }
  const payload = JSON.stringify(merged, null, 2)
  let html
  if (noHtml) html = ''
  else if (rootJsonMode) html = payload
  else html = renderHtml(merged, ownerCounts, fileCounts, conflicts, skipped)

  writeOutputs(dest, payload, guidMap, html)
  emitReport([merged, fileCounts, ownerCounts, conflicts, skipped, warnings], dest, noHtml)

  if (strict && (conflicts.length || skipped.length)) return 1
  return 0
}

/** 命令行入口 */
export function main(argv = process.argv.slice(2)): number {
  // 命令行参数
  let args: {
    'repo-root': string
    out: string
    'no-site': boolean
    'root-json': string
    'guid-map': string
    'root-json-mode': boolean
    'no-html': boolean
    repo: string
    branch: string
    'forbid-empty': boolean
    strict: boolean
  }
  try {
    const parsed = parseArgs({
      args: argv,
      options: {
        'repo-root': { type: 'string', default: REPO_ROOT_DEFAULT },
        out: { type: 'string', default: '_site' },
        'no-site': { type: 'boolean', default: false },
        'root-json': { type: 'string', default: '' },
        'guid-map': { type: 'string', default: '' },
        'root-json-mode': { type: 'boolean', default: false },
        'no-html': { type: 'boolean', default: false },
        repo: { type: 'string', default: '' },
        branch: { type: 'string', default: '' },
        'forbid-empty': { type: 'boolean', default: false },
        strict: { type: 'boolean', default: false },
      },
    })
    args = parsed.values as typeof args
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error)
    console.error(`::error::${message}`)
    return 2
  }

  const repoRoot = path.resolve(args['repo-root'])
  const cfg = prReview.loadConfig()
  const repo = resolveRepo(args.repo)
  const branch = resolveBranch(args.branch)

  if (!repo) {
    console.log('::error::无法确定仓库地址（owner/repo），请用 --repo 指定或用 GITHUB_REPOSITORY')
    return 2
  }

  const outDir = args['no-site']
    ? ''
    : path.isAbsolute(args.out)
      ? args.out
      : path.join(repoRoot, args.out)

  /** 转为绝对路径 */
  const asAbsolute = (value: string): string =>
    path.isAbsolute(value) ? value : path.join(repoRoot, value)
  const rootJsonPath = args['root-json'] ? asAbsolute(args['root-json']) : ''
  const guidMapPath = args['guid-map'] ? asAbsolute(args['guid-map']) : ''

  if (!outDir && !rootJsonPath && !guidMapPath) {
    console.log('::error::--no-site 且未指定 --root-json / --guid-map 时没有任何输出')
    return 2
  }

  return build(repoRoot, cfg, repo, branch, {
    outDir,
    rootJsonPath,
    guidMapPath,
    rootJsonMode: args['root-json-mode'],
    forbidEmpty: args['forbid-empty'],
    strict: args.strict,
    noHtml: args['no-html'],
  })
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = main()
}
