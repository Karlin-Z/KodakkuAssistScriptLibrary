import { i18n } from './i18n'
import type { ScriptEntry, ScriptRow } from './types'

/** 合并索引地址 */
const INDEX_URL = `${import.meta.env.BASE_URL}index.json`

/** 解析时间戳 */
function parseTime(value: string | undefined): number {
  if (!value) return 0
  const ts = Date.parse(value)
  return Number.isNaN(ts) ? 0 : ts
}

/** 拆分仓库路径 */
function splitRepoPath(rawUrl: string): { contributor: string; repoPath: string } {
  // 兜底文案
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

/** 转为行数据 */
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

/** 拉取索引 */
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
