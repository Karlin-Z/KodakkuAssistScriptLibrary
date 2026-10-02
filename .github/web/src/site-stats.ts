import { loadIndex } from './index-data'

/** 首页要展示的在线库概况。 */
export interface LibraryStats {
  /** 收录的脚本数 */
  scripts: number
  /** 贡献者目录数 */
  contributors: number
  /** 所有脚本声明过的地图 ID 并集大小 */
  territories: number
}

/**
 * 汇总在线库概况。拿不到索引时直接抛出，由调用方决定是显示占位还是整块隐藏
 * ——统计数字是锦上添花，不该让首页因此报错。
 */
export async function loadStats(signal?: AbortSignal): Promise<LibraryStats> {
  const rows = await loadIndex(signal)

  const contributors = new Set<string>()
  const territories = new Set<number>()

  for (const row of rows) {
    contributors.add(row.contributor)
    for (const id of row.TerritoryIds ?? []) territories.add(id)
  }

  return {
    scripts: rows.length,
    contributors: contributors.size,
    territories: territories.size,
  }
}
