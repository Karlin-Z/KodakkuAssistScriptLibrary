/** 索引条目 */
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

/** 页面行结构 */
export interface ScriptRow extends ScriptEntry {
    /** 贡献者目录 */
    contributor: string
    /** 仓库相对路径 */
    repoPath: string
    /** 文件名 */
    fileName: string
    /** 修改时间戳 */
    updatedAt: number
}

/** 排序方式 */
export type SortKey = 'updated' | 'name' | 'author' | 'contributor'
