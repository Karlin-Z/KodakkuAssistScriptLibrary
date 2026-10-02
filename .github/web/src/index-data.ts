import { i18n } from './i18n'
import type { ScriptEntry, ScriptRow } from './types'

/** 站点根目录的合并索引，由 merge_repos.mjs 生成。 */
const INDEX_URL = `${import.meta.env.BASE_URL}index.json`

/** `2026-09-12T05:46:17Z` -> 时间戳；缺失或不合法时返回 0。 */
function parseTime(value: string | undefined): number {
  if (!value) return 0
  const ts = Date.parse(value)
  return Number.isNaN(ts) ? 0 : ts
}

/**
 * 从 raw 直链反推贡献者目录与仓库内路径。
 *
 * 形如 `https://raw.githubusercontent.com/<owner>/<repo>/<branch>/<贡献者>/<文件…>`，
 * 去掉协议与前三段后，余下第一段就是贡献者目录。这里假定分支名不含斜杠（本仓库是 main）。
 */
function splitRepoPath(rawUrl: string): { contributor: string; repoPath: string } {
  // 这里在组件之外，拿不到 useI18n，兜底文案走全局实例（取当前语言）
  const unknown = i18n.global.t('common.unknown')
  let segments: string[]
  try {
    segments = new URL(rawUrl).pathname.split('/').filter(Boolean)
  } catch {
    return { contributor: unknown, repoPath: '' }
  }
  if (segments.length < 5) return { contributor: unknown, repoPath: '' }
  return { contributor: segments[3] ?? unknown, repoPath: segments.slice(4).join('/') }
}

function toRow(entry: ScriptEntry): ScriptRow {
  const { contributor, repoPath } = splitRepoPath(entry.DownloadUrl)
  return {
    ...entry,
    contributor,
    repoPath,
    fileName: repoPath.split('/').pop() ?? '',
    updatedAt: parseTime(entry.UpdateTime),
  }
}

/** 拉取索引；失败时抛出可直接展示给用户的错误。 */
export async function loadIndex(signal?: AbortSignal): Promise<ScriptRow[]> {
  const response = await fetch(INDEX_URL, { signal, cache: 'no-cache' })
  if (!response.ok) {
    throw new Error(i18n.global.t('errors.indexHttp', { status: response.status }))
  }
  const payload: unknown = await response.json()
  if (!Array.isArray(payload)) {
    throw new Error(i18n.global.t('errors.indexShape'))
  }
  return (payload as ScriptEntry[]).map(toRow)
}
