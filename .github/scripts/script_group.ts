import { extractPartialTypes, hasScriptTypeAttribute } from './csharp_meta.ts'

/** 参与分组的文件 */
export interface GroupFile {
    rel: string
    text: string
    partialTypes: string[]
    hasScriptType: boolean
}

/** 主文件与部分文件 */
export interface GroupedScript {
    main: GroupFile
    parts: GroupFile[]
}

/** 分组结果 */
export interface GroupResult {
    groups: GroupedScript[]
    /** 有 partial 但没有主文件 */
    orphans: GroupFile[]
    /** 多处 ScriptType */
    conflicts: GroupFile[]
    /** 普通非法文件 */
    none: GroupFile[]
}

/** 分析单个文件 */
export function analyzeFile(rel: string, text: string): GroupFile {
    return {
        rel,
        text,
        partialTypes: extractPartialTypes(text).names,
        hasScriptType: hasScriptTypeAttribute(text)
    }
}

/** 按同名 partial 聚合 */
export function groupFiles(files: GroupFile[]): GroupResult {
    const parent = files.map((_, index) => index)
    const find = (index: number): number =>
        parent[index] === index ? index : (parent[index] = find(parent[index]))
    const union = (a: number, b: number): void => {
        const rootA = find(a)
        const rootB = find(b)
        if (rootA !== rootB) parent[rootB] = rootA
    }

    const typeIndex = new Map<string, number[]>()
    files.forEach((file, index) => {
        for (const typeName of file.partialTypes) {
            const list = typeIndex.get(typeName) ?? []
            list.push(index)
            typeIndex.set(typeName, list)
        }
    })
    for (const indexes of typeIndex.values())
        for (let i = 1; i < indexes.length; i += 1) union(indexes[0], indexes[i])

    const buckets = new Map<number, GroupFile[]>()
    files.forEach((file, index) => {
        const root = find(index)
        const bucket = buckets.get(root) ?? []
        bucket.push(file)
        buckets.set(root, bucket)
    })

    const groups: GroupedScript[] = []
    const orphans: GroupFile[] = []
    const conflicts: GroupFile[] = []
    const none: GroupFile[] = []
    const compare = (a: GroupFile, b: GroupFile): number =>
        a.rel < b.rel ? -1 : a.rel > b.rel ? 1 : 0

    for (const bucket of buckets.values()) {
        const mains = bucket.filter((file) => file.hasScriptType)
        if (mains.length === 1) {
            const main = mains[0]
            groups.push({ main, parts: bucket.filter((file) => file !== main).sort(compare) })
        } else if (mains.length === 0) {
            if (bucket.some((file) => file.partialTypes.length > 0)) orphans.push(...bucket)
            else none.push(...bucket)
        } else {
            conflicts.push(...bucket)
        }
    }

    groups.sort((a, b) => compare(a.main, b.main))
    orphans.sort(compare)
    conflicts.sort(compare)
    none.sort(compare)
    return { groups, orphans, conflicts, none }
}
