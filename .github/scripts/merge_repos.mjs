/**
 * 扫描所有贡献者文件夹里的 .cs，提取 [ScriptType(...)]，生成合并后的索引。
 *
 * 产物（可分别开关）：
 *   --out DIR          站点目录：DIR/index.json（合并索引）+ DIR/index.html（说明页）；
 *                      加 --no-html 则只写 index.json，说明页交给前端
 *   --root-json PATH   仓库根目录的总索引，例如 OnlineRepo.json
 *   --guid-map PATH    guid -> 源文件 映射表，供 PR 审核阶段查重
 *
 * 合并规则：
 *   - 只扫描仓库第一层里不以 . 或 _ 开头的目录（即贡献者文件夹），进入文件夹后递归全部子目录
 *   - 每个 .cs 用 prReview.validateScriptFile 校验（与 PR 审核同一套规则）
 *   - DownloadUrl 自动填成本仓库该 .cs 的 raw 直链
 *   - UpdateTime 取该 .cs 的最后提交时间（UTC），未提交时退回文件修改时间
 *   - 值为空的字段（Note / UpdateInfo / UpdateTime / TerritoryIds 等）直接省略
 *   - 按「文件夹名 -> 文件路径」排序处理，保证输出稳定、可复现
 *   - 按 Guid（忽略大小写）去重：先出现的生效，冲突列入报告
 *
 * 零依赖，只用 Node 内置模块。由 merge_repos.py 移植而来（Python 版已随迁移移除），
 * 生成的 JSON 是提交进仓库的产物：字段顺序、缩进、空值省略规则都算对外契约，不能变。
 */

import fs from 'node:fs'
import path from 'node:path'
import process from 'node:process'
import { execFileSync } from 'node:child_process'
import { parseArgs } from 'node:util'
import { fileURLToPath } from 'node:url'

import * as prReview from './pr_review.mjs'

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url))
export const REPO_ROOT_DEFAULT = path.resolve(SCRIPT_DIR, '../..')

/** 输出时统一字段顺序，让合并结果整齐、diff 友好 */
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

/** 任何层级都不参与合并的目录名 */
const EXCLUDED_DIRS = new Set(['.git', 'node_modules'])

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

// --------------------------------------------------------------------------- //
// 收集与合并
// --------------------------------------------------------------------------- //

/**
 * 返回 [[owner, relPath]]，按 owner/路径 排序。
 *
 * 第一层目录必须像 GitHub 用户名（跳过 . / _ 开头，以及在 ignoreDirs 里的）；
 * 进入贡献者文件夹后**递归全部子目录**，不再按名字过滤——只要在用户目录下，
 * 任意深度的 .cs 都会被收录。
 */
export function discoverFiles(repoRoot, ignoreDirs) {
  const ignored = new Set([...(ignoreDirs ?? []).map(String), ...EXCLUDED_DIRS])
  const found = []
  for (const name of fs.readdirSync(repoRoot).sort()) {
    const full = path.join(repoRoot, name)
    let isDir = false
    try {
      isDir = fs.statSync(full).isDirectory()
    } catch {
      isDir = false
    }
    if (!isDir) continue
    if (name.startsWith('.') || name.startsWith('_') || ignored.has(name)) continue
    for (const rel of walkCsFiles(full, ignored)) {
      found.push([name, path.relative(repoRoot, rel).split(path.sep).join('/')])
    }
  }
  return found
}

/**
 * 递归收集目录下的 .cs，返回绝对路径。
 *
 * 顺序要跟 Python 的 os.walk(topdown) 一致：**先当前目录的文件，再按名字序进子目录**。
 * 反过来的话，同一贡献者既有顶层脚本又有子目录时，guid 去重的先后会变，索引内容就不同了。
 */
function walkCsFiles(dir, ignored) {
  const out = []
  const entries = fs.readdirSync(dir, { withFileTypes: true })
  const dirs = entries
    .filter((entry) => entry.isDirectory() && !ignored.has(entry.name))
    .map((entry) => entry.name)
    .sort()
  const files = entries
    .filter((entry) => !entry.isDirectory())
    .map((entry) => entry.name)
    .sort()

  for (const name of files) {
    if (name.toLowerCase().endsWith('.cs')) out.push(path.join(dir, name))
  }
  for (const name of dirs) out.push(...walkCsFiles(path.join(dir, name), ignored))
  return out
}

/**
 * 复刻 Python 的 urllib.parse.quote()。
 *
 * 不能直接用 encodeURIComponent：后者不转义 ! * ' ( )，而 Python 在 safe='' 时会转义，
 * 而这两个字符在 SAFE 集合里的差异会直接改变提交进仓库的 DownloadUrl。
 */
function pyQuote(value, safe = '') {
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

/** 该 .cs 在本仓库的 raw 直链，作为索引里的 DownloadUrl。 */
export function downloadUrl(repo, branch, relPath) {
  return `https://raw.githubusercontent.com/${repo}/${pyQuote(branch, '')}/${pyQuote(relPath, '/')}`
}

/** 该文件最后一次提交的 Unix 时间戳；取不到（非 git 仓库 / 未提交）时返回 null。 */
function commitEpoch(repoRoot, relPath) {
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

/**
 * 索引里的 UpdateTime：优先取该文件的最后提交时间，取不到时退回文件修改时间。
 *
 * 时间统一转成 UTC ISO8601（`2026-09-12T05:45:00Z`），保证同一份内容在
 * 不同机器 / 多次生成下结果一致，避免「无改动也提交」。
 */
export function lastUpdateTime(repoRoot, relPath) {
  let epoch = commitEpoch(repoRoot, relPath)
  if (epoch === null) {
    try {
      epoch = Math.floor(fs.statSync(path.join(repoRoot, relPath)).mtimeMs / 1000)
    } catch {
      return ''
    }
  }
  // toISOString 带毫秒（.000Z），Python 的 strftime 不带，去掉才一致
  return new Date(epoch * 1000).toISOString().replace(/\.\d{3}Z$/, 'Z')
}

/**
 * 把 validateScriptFile 的 meta 组装成 OnlineScriptInfo 字段。
 *
 * 字段顺序固定；值为空（空串 / 空数组）的字段直接省略，插件端会回退到默认值。
 * `Repo` 由插件在订阅时用当前订阅地址覆盖，所以永远不写。
 */
export function buildEntry(meta, repo, branch, relPath, updateTime) {
  const entry = {
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
  const out = {}
  for (const key of CANONICAL_FIELD_ORDER) {
    if (!(key in entry)) continue
    const value = entry[key]
    // 对应 Python 的 `entry[key] not in ("", [], None)`；注意 0 要保留
    if (value === '' || value === null || value === undefined) continue
    if (Array.isArray(value) && value.length === 0) continue
    out[key] = value
  }
  return out
}

/**
 * 读取并校验所有脚本，返回 {records, skipped, warnings, fileCounts}。
 *
 * records: [[owner, relPath, entry]]；skipped: [[relPath, [错误]]]
 */
export function collect(repoRoot, cfg, repo, branch) {
  const records = []
  const skipped = []
  const warnings = []
  // 无原型对象：guid / 目录名可以是 "constructor" 这类字面量，普通对象会被原型链误命中
  const fileCounts = Object.create(null)

  for (const [owner, rel] of discoverFiles(repoRoot, cfg.ignore_dirs)) {
    fileCounts[owner] = (fileCounts[owner] ?? 0) + 1

    let raw
    try {
      raw = fs.readFileSync(path.join(repoRoot, rel))
    } catch (error) {
      skipped.push([rel, [`无法读取：${error.message}`]])
      continue
    }

    let text
    try {
      text = new TextDecoder('utf-8', { fatal: true }).decode(raw)
    } catch {
      skipped.push([rel, [`不是合法的 UTF-8 编码：${describeUtf8Error(raw)}`]])
      continue
    }

    const { meta, errors, warnings: fileWarnings } = prReview.validateScriptFile(text, rel, owner, cfg)
    warnings.push(...fileWarnings)
    if (errors.length) {
      skipped.push([rel, errors])
      continue
    }

    const updateTime = lastUpdateTime(repoRoot, rel)
    records.push([owner, rel, buildEntry(meta, repo, branch, rel, updateTime)])
  }

  return { records, skipped, warnings, fileCounts }
}

/**
 * 尽量复刻 Python 抛 UnicodeDecodeError 时的措辞，让面向贡献者的提示保持稳定。
 *
 * Python 的原文形如 `'utf-8' codec can't decode byte 0xff in position 0: invalid start byte`。
 * 只覆盖最常见的「非法起始字节」，其他情况退回通用说明。
 */
function describeUtf8Error(raw) {
  for (let i = 0; i < raw.length; i += 1) {
    const byte = raw[i]
    if (byte < 0x80) continue
    if (byte >= 0xc2 && byte <= 0xf4) continue
    return `'utf-8' codec can't decode byte 0x${byte.toString(16).padStart(2, '0')} in position ${i}: invalid start byte`
  }
  return '输入不是合法的 UTF-8 序列'
}

/**
 * 按 Guid（忽略大小写）去重，先出现的生效。
 *
 * 返回 {merged, conflicts, ownerCounts, guidMap}；
 * guidMap 是 {小写 guid: 生效的源文件路径}，会提交到仓库供 PR 审核查重。
 */
export function mergeRecords(records) {
  const merged = []
  const conflicts = []
  // 同上：无原型对象，避免 guid 取值撞上 Object.prototype 的成员名
  const ownerCounts = Object.create(null)
  const guidMap = Object.create(null)
  const seen = Object.create(null)

  for (const [owner, rel, entry] of records) {
    const guid = String(entry.Guid ?? '').trim()
    const key = guid.toLowerCase()
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

// --------------------------------------------------------------------------- //
// 输出
// --------------------------------------------------------------------------- //

/** 贡献者维度的 HTML 表格。 */
export function renderTable(ownerCounts, fileCounts) {
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

/** 冲突与跳过文件的 HTML 列表；都没有则返回空串。 */
export function renderProblems(conflicts, skipped) {
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

/** 站点说明页。 */
export function renderHtml(merged, ownerCounts, fileCounts, conflicts, skipped) {
  return INDEX_HTML_TEMPLATE.replaceAll('{ENTRY_COUNT}', String(merged.length))
    .replaceAll('{OWNER_COUNT}', String(Object.keys(ownerCounts).length))
    .replaceAll('{TABLE}', renderTable(ownerCounts, fileCounts))
    .replaceAll('{PROBLEMS}', renderProblems(conflicts, skipped))
    .replaceAll('{GENERATED_AT}', formatUtcMinute(new Date()))
}

/** 与 Python 的 strftime("%Y-%m-%d %H:%M") 一致（UTC）。 */
function formatUtcMinute(date) {
  const pad = (value) => String(value).padStart(2, '0')
  return (
    `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())} ` +
    `${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())}`
  )
}

/** 写文本文件，自动创建父目录，统一 LF 换行。 */
export function writeText(filePath, text) {
  const parent = path.dirname(path.resolve(filePath))
  if (parent) fs.mkdirSync(parent, { recursive: true })
  fs.writeFileSync(filePath, text, 'utf8')
}

/**
 * dest: {site, root, guidMap}，空值表示不输出。
 *
 * html 为空串表示站点目录只写 index.json——说明页由 .github/web 的前端产出。
 */
export function writeOutputs(dest, payload, guidMap, html) {
  if (dest.site) {
    fs.mkdirSync(dest.site, { recursive: true })
    writeText(path.join(dest.site, 'index.json'), `${payload}\n`)
    if (html) writeText(path.join(dest.site, 'index.html'), `${html}\n`)
  }
  if (dest.root) writeText(dest.root, `${payload}\n`)
  if (dest.guidMap) {
    // guid -> 源文件，供 PR 审核阶段查重（不进公开索引）
    writeText(dest.guidMap, `${canonicalJson(guidMap)}\n`)
  }
}

/** 对应 Python 的 json.dumps(..., ensure_ascii=False, indent=2, sort_keys=True)。 */
function canonicalJson(value) {
  const sortValue = (input) => {
    if (Array.isArray(input)) return input.map(sortValue)
    if (input && typeof input === 'object') {
      const out = {}
      for (const key of Object.keys(input).sort()) out[key] = sortValue(input[key])
      return out
    }
    return input
  }
  return JSON.stringify(sortValue(value), null, 2)
}

/**
 * 把生成结果写到 stdout 与 Actions 摘要。
 *
 * stats: [merged, fileCounts, ownerCounts, conflicts, skipped, warnings]
 */
export function emitReport(stats, dest, noHtml = false) {
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
      // 摘要写不进去不影响主流程
    }
  }
  return report
}

// --------------------------------------------------------------------------- //
// 入口
// --------------------------------------------------------------------------- //

/** 跑一条 git 命令并返回 stdout（已 strip）；失败返回空串。 */
function git(args) {
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

/** 确定 owner/repo：显式参数 -> GITHUB_REPOSITORY -> origin 远端地址。 */
export function resolveRepo(explicit) {
  if (explicit) return explicit
  if (process.env.GITHUB_REPOSITORY) return process.env.GITHUB_REPOSITORY
  const url = git(['config', '--get', 'remote.origin.url'])
  const match = /github\.com[:/]([^/]+)\/(.+?)(?:\.git)?$/.exec(url)
  return match ? `${match[1]}/${match[2]}` : ''
}

/** 确定分支名：显式参数 -> GITHUB_REF_NAME -> 当前分支 -> main。 */
export function resolveBranch(explicit) {
  if (explicit) return explicit
  if (process.env.GITHUB_REF_NAME) return process.env.GITHUB_REF_NAME
  const branch = git(['rev-parse', '--abbrev-ref', 'HEAD'])
  return branch || 'main'
}

/** 生成合并索引并写出。返回进程退出码。 */
export function build(repoRoot, cfg, repo, branch, options = {}) {
  const {
    outDir = '',
    rootJsonPath = '',
    guidMapPath = '',
    rootJsonMode = false,
    forbidEmpty = false,
    strict = false,
    noHtml = false,
  } = options

  const { records, skipped, warnings, fileCounts } = collect(repoRoot, cfg, repo, branch)
  const { merged, conflicts, ownerCounts, guidMap } = mergeRecords(records)

  if (!merged.length && forbidEmpty) {
    console.log('::error::没有收录到任何脚本，拒绝生成空索引（--forbid-empty）')
    return 1
  }

  // 自检：合并结果本身必须仍然是合规的 OnlineRepo 文档
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
  // --no-html：站点目录只留 index.json，index.html 交给 .github/web 的前端
  // --root-json-mode：让站点根路径直接返回 JSON（插件不检查 Content-Type）
  let html
  if (noHtml) html = ''
  else if (rootJsonMode) html = payload
  else html = renderHtml(merged, ownerCounts, fileCounts, conflicts, skipped)

  writeOutputs(dest, payload, guidMap, html)
  emitReport([merged, fileCounts, ownerCounts, conflicts, skipped, warnings], dest, noHtml)

  if (strict && (conflicts.length || skipped.length)) return 1
  return 0
}

/** 命令行入口。 */
export function main(argv = process.argv.slice(2)) {
  let args
  try {
    ;({ values: args } = parseArgs({
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
    }))
  } catch (error) {
    console.error(`::error::${error.message}`)
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

  const asAbsolute = (value) => (path.isAbsolute(value) ? value : path.join(repoRoot, value))
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

// 只在直接执行时跑 main，被 import 时不跑（对应 Python 的 __main__ 判断）
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = main()
}
