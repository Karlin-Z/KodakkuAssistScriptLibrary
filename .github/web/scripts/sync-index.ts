import { copyFile, mkdir } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const source = resolve(here, '../../../OnlineRepo.json')
const target = resolve(here, '../public/index.json')

// 同步本地索引
await mkdir(dirname(target), { recursive: true })
await copyFile(source, target)

console.log(`已同步索引：${source}\n         -> ${target}`)
