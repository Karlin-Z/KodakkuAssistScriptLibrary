/** 与 merge_repos.py 生成的 index.json 一一对应；值为空的字段在 JSON 里直接被省略。 */
export interface ScriptEntry {
  Name: string
  Guid: string
  Version: string
  Author: string
  DownloadUrl: string
  Note?: string
  UpdateInfo?: string
  UpdateTime?: string
  TerritoryIds?: number[]
}

/** 页面内部使用的结构：把原始字段补全成必填，省得模板里到处判空。 */
export interface ScriptRow extends ScriptEntry {
  /** 贡献者目录名，从 DownloadUrl 反推 */
  contributor: string
  /** 仓库内相对路径，如 `VeeverSW/07-DawnTrail/x.cs` */
  repoPath: string
  /** 文件名，如 `x.cs` */
  fileName: string
  /** UpdateTime 的时间戳；解析不出来时为 0 */
  updatedAt: number
}

/** 排序方式；取值与界面下拉框一一对应。 */
export type SortKey = 'updated' | 'name' | 'author' | 'contributor'
