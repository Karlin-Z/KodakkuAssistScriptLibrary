// 本地开发时站点根目录那份 index.json 还不存在（它由 CI 里的 merge_repos.mjs 生成），
// 这里先用仓库根目录已提交的 OnlineRepo.json 顶替 —— 两者内容一致，且不需要 Python
// 与 git 历史，npm run dev / build 开箱即用。
//
// CI 里也会先跑一次，但产物随后会被 workflow 中真正的 merge_repos --no-html 覆盖，
// 所以这里同步到的是不是最新并不影响线上结果。
import { copyFile, mkdir } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const source = resolve(here, '../../../OnlineRepo.json')
const target = resolve(here, '../public/index.json')

await mkdir(dirname(target), { recursive: true })
await copyFile(source, target)

console.log(`已同步索引：${source}\n         -> ${target}`)
