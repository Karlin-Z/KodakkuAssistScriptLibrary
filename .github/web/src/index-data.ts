import { parseTime } from './format'
import type { ScriptEntry, ScriptRow } from './types'

/** 站点根目录的合并索引，由 merge_repos.mjs 生成。 */
const INDEX_URL = `${import.meta.env.BASE_URL}index.json`

/**
 * 从 raw 直链反推贡献者目录与仓库内路径。
 *
 * 形如 `https://raw.githubusercontent.com/<owner>/<repo>/<branch>/<贡献者>/<文件…>`，
 * 去掉协议与前三段后，余下第一段就是贡献者目录。这里假定分支名不含斜杠（本仓库是 main）。
 */
function splitRepoPath(rawUrl: string): { contributor: string; repoPath: string } {
  let segments: string[]
  try {
    segments = new URL(rawUrl).pathname.split('/').filter(Boolean)
  } catch {
    return { contributor: '未知', repoPath: '' }
  }
  if (segments.length < 5) return { contributor: '未知', repoPath: '' }
  return { contributor: segments[3] ?? '未知', repoPath: segments.slice(4).join('/') }
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
    throw new Error(`索引请求失败（HTTP ${response.status}）`)
  }
  const payload: unknown = await response.json()
  if (!Array.isArray(payload)) {
    throw new Error('索引格式不正确：顶层不是数组')
  }
  return (payload as ScriptEntry[]).map(toRow)
}
