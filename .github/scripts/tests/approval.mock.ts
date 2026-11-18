import http from 'node:http'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

import type { ChangedFile } from '../pr_review.ts'

const PR_REVIEW = path.resolve(import.meta.dirname, '../pr_review.ts')
const REVIEW_CONTEXT = 'KodakkuAssist/cross-author-review'

const GUID_MAP: Record<string, string> = {
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa': 'Other/seed.cs',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb': 'Alice/seed.cs',
    'cccccccc-cccc-cccc-cccc-cccccccccccc': 'Bob/seed.cs'
}
const GUID_MAP_B64 = Buffer.from(JSON.stringify(GUID_MAP), 'utf8').toString('base64')

const CS_TEMPLATE = `using KodakkuAssist.Script;

[ScriptType(name: "Mock Script", territorys: [1], guid: "GUID_PLACEHOLDER", version: "0.0.0.1", author: "Me")]
public class MockScript { }
`

/** 模拟 PR */
interface MockPR {
    state: string
    draft: boolean
    merged: boolean
    user: { login: string }
    head: { sha: string }
    base: { ref: string }
}

/** 模拟状态 */
interface MockStatus {
    context?: string
    state?: string
    description?: string
}

/** 模拟请求 */
interface MockRequest {
    method?: string
    url?: string
    body: string
}

/** 测试用例 */
interface MockCase {
    runner: () => Promise<number>
    files?: ChangedFile[]
    pr?: Partial<MockPR>
    statuses?: MockStatus[]
    mergeResult?: Record<string, unknown>
    mergeStatus?: number
    prAuthor?: string
    headSha?: string
    commentAuthor?: string
    commentBody?: string
    commentType?: string
    expectCode: number
    expectOutput?: Record<string, string | undefined>
    expectPatterns?: string[]
    forbidPatterns?: string[]
}

const DEFAULT_PR: MockPR = {
    state: 'open',
    draft: false,
    merged: false,
    user: { login: 'me' },
    head: { sha: 'sha1' },
    base: { ref: 'main' }
}

const OK_STATUS: MockStatus = {
    context: REVIEW_CONTEXT,
    state: 'success',
    description: 'mock reviewed'
}

let requests: MockRequest[] = []
let files: ChangedFile[] = []
let pr: MockPR = { ...DEFAULT_PR }
let statusesBySha: Record<string, MockStatus[]> = { sha1: [OK_STATUS] }
let mergeResult: Record<string, unknown> = { merged: true, sha: 'sha1' }
let mergeStatus = 200

/** 构造模拟脚本 */
function csForPath(filePath: string): string {
    const hash = Buffer.from(filePath).toString('hex').padEnd(32, '0').slice(0, 32)
    const guid =
        `${hash.slice(0, 8)}-${hash.slice(8, 12)}-${hash.slice(12, 16)}-` +
        `${hash.slice(16, 20)}-${hash.slice(20, 32)}`
    return CS_TEMPLATE.replace('GUID_PLACEHOLDER', guid)
}

/** 返回 JSON */
function send(res: http.ServerResponse, code: number, obj: unknown): void {
    res.writeHead(code, { 'content-type': 'application/json' })
    res.end(JSON.stringify(obj))
}

/** 启动模拟服务 */
function startServer(): Promise<http.Server> {
    return new Promise((resolve) => {
        // 模拟路由
        const server = http.createServer((req, res) => {
            let body = ''
            req.on('data', (chunk: Buffer) => {
                body += chunk.toString()
            })
            req.on('end', () => {
                requests.push({ method: req.method, url: req.url, body })
                const url = new URL(req.url ?? '/', 'http://localhost')

                if (req.method === 'GET' && url.pathname === '/repos/test/repo/pulls/1/files') {
                    const page = Number(url.searchParams.get('page') || '1')
                    const perPage = Number(url.searchParams.get('per_page') || '100')
                    const start = (page - 1) * perPage
                    return send(res, 200, files.slice(start, start + perPage))
                }
                if (req.method === 'GET' && url.pathname === '/repos/test/repo/pulls/1') {
                    return send(res, 200, pr)
                }
                if (
                    req.method === 'GET' &&
                    /\/repos\/test\/repo\/commits\/[^/]+\/statuses$/.test(url.pathname)
                ) {
                    const sha = url.pathname.split('/commits/')[1].split('/statuses')[0]
                    return send(res, 200, statusesBySha[sha] || [])
                }
                if (req.method === 'POST' && url.pathname === '/repos/test/repo/statuses/sha1') {
                    const data = JSON.parse(body || '{}') as MockStatus
                    statusesBySha.sha1 = [
                        { context: data.context, state: data.state, description: data.description },
                        ...(statusesBySha.sha1 || [])
                    ]
                    return send(res, 201, data)
                }
                if (req.method === 'PUT' && url.pathname === '/repos/test/repo/pulls/1/merge') {
                    return send(res, mergeStatus, mergeResult)
                }
                if (
                    req.method === 'GET' &&
                    url.pathname === '/repos/test/repo/contents/.github/index/guid-map.json'
                ) {
                    return send(res, 200, {
                        type: 'file',
                        size: Buffer.byteLength(JSON.stringify(GUID_MAP)),
                        encoding: 'base64',
                        content: GUID_MAP_B64
                    })
                }
                if (req.method === 'GET' && url.pathname.startsWith('/repos/test/repo/contents/')) {
                    const contentPath = decodeURIComponent(
                        url.pathname.replace('/repos/test/repo/contents/', '')
                    )
                    const content = csForPath(contentPath)
                    return send(res, 200, {
                        type: 'file',
                        size: Buffer.byteLength(content),
                        encoding: 'base64',
                        content: Buffer.from(content, 'utf8').toString('base64')
                    })
                }
                if (req.method === 'POST' && url.pathname === '/repos/test/repo/labels') {
                    return send(res, 201, { name: 'cross-author' })
                }
                if (req.method === 'POST' && url.pathname === '/repos/test/repo/issues/1/labels') {
                    return send(res, 200, [{ name: '跨作者修改' }])
                }
                if (
                    req.method === 'POST' &&
                    url.pathname === '/repos/test/repo/issues/1/comments'
                ) {
                    return send(res, 201, { id: 1 })
                }
                if (
                    req.method === 'DELETE' &&
                    url.pathname.startsWith('/repos/test/repo/issues/1/labels/')
                ) {
                    return send(res, 404, { message: 'Not Found' })
                }
                return send(res, 404, { message: 'Not Found' })
            })
        })
        server.listen(0, '127.0.0.1', () => resolve(server))
    })
}

const server = await startServer()
const address = server.address()
if (!address || typeof address === 'string') throw new Error('无法获取 mock 服务端口')
const port = address.port
const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'kasl-approval-mock-'))

Object.assign(process.env, {
    GITHUB_API_URL: `http://127.0.0.1:${port}`,
    GITHUB_TOKEN: 'mock-token',
    GITHUB_REPOSITORY: 'test/repo',
    PR_NUMBER: '1',
    GITHUB_OUTPUT: path.join(tempDir, 'output.txt'),
    GITHUB_STEP_SUMMARY: path.join(tempDir, 'summary.md'),
    COMMENT_TYPE: 'User'
})

const prReview = (await import(pathToFileURL(PR_REVIEW).href)) as typeof import('../pr_review.ts')

const failures: string[] = []

/** 读取输出变量 */
function readOutput(): Record<string, string> {
    const file = process.env.GITHUB_OUTPUT
    const raw = file && fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : ''
    return Object.fromEntries(
        raw
            .split(/\r?\n/)
            .filter(Boolean)
            .map((line) => {
                const index = line.indexOf('=')
                return [line.slice(0, index), line.slice(index + 1)]
            })
    )
}

const realStdoutWrite = process.stdout.write.bind(process.stdout)

/** 执行测试用例 */
async function runCase(name: string, options: MockCase): Promise<void> {
    files = options.files ?? []
    pr = { ...DEFAULT_PR, ...(options.pr ?? {}) }
    statusesBySha = {
        sha1: options.statuses ?? [OK_STATUS]
    }
    mergeResult = options.mergeResult ?? { merged: true, sha: 'sha1' }
    mergeStatus = options.mergeStatus ?? 200
    requests = []

    fs.writeFileSync(process.env.GITHUB_OUTPUT ?? '', '', 'utf8')
    fs.writeFileSync(process.env.GITHUB_STEP_SUMMARY ?? '', '', 'utf8')
    Object.assign(process.env, {
        PR_AUTHOR: options.prAuthor ?? 'me',
        PR_HEAD_SHA: options.headSha ?? 'sha1',
        PR_BASE_REF: 'main',
        COMMENT_AUTHOR: options.commentAuthor ?? '',
        COMMENT_BODY: options.commentBody ?? '',
        COMMENT_TYPE: options.commentType ?? 'User'
    })

    let captured = ''
    process.stdout.write = ((
        chunk: string | Uint8Array,
        encoding?: unknown,
        callback?: unknown
    ) => {
        captured += typeof chunk === 'string' ? chunk : Buffer.from(chunk).toString('utf8')
        if (typeof encoding === 'function') encoding()
        else if (typeof callback === 'function') callback()
        return true
    }) as typeof process.stdout.write
    let code: number
    try {
        code = await options.runner()
    } finally {
        process.stdout.write = realStdoutWrite
    }
    // 解析输出
    const output = readOutput()
    const combined = requests.map((item) => `${item.method} ${item.url}\n${item.body}`).join('\n')

    // 问题列表
    const problems: string[] = []
    if (code !== options.expectCode) problems.push(`exit ${code} != ${options.expectCode}`)
    for (const [key, value] of Object.entries(options.expectOutput ?? {})) {
        if (output[key] !== value) problems.push(`${key}=${output[key]} != ${value}`)
    }
    for (const pattern of options.expectPatterns ?? []) {
        if (!combined.includes(pattern)) problems.push(`missing: ${pattern}`)
    }
    for (const pattern of options.forbidPatterns ?? []) {
        if (combined.includes(pattern)) problems.push(`unexpected: ${pattern}`)
    }

    if (problems.length) {
        failures.push(name)
        console.log(`❌ ${name}`)
        console.log(problems.join('\n'))
        console.log('--- requests ---')
        console.log(combined)
        console.log('--- script output ---')
        console.log(captured)
    } else {
        console.log(`✅ ${name}`)
    }
}

await runCase('缺少 PR_HEAD_SHA：直接拒绝执行', {
    runner: prReview.run_ci,
    files: [{ status: 'added', filename: 'Other/a.cs' }],
    headSha: '',
    expectCode: 2,
    forbidPatterns: ['GET /repos/test/repo/pulls/1']
})

await runCase('跨作者修改：写审核状态、打标签、@ 所有者', {
    runner: prReview.run_ci,
    files: [{ status: 'added', filename: 'Other/a.cs' }],
    expectCode: 0,
    expectOutput: { auto_merge: 'false' },
    expectPatterns: [
        'POST /repos/test/repo/statuses/sha1',
        `"context":"${REVIEW_CONTEXT}"`,
        '"state":"success"',
        'POST /repos/test/repo/issues/1/labels',
        'POST /repos/test/repo/issues/1/comments',
        '@Other',
        '当前版本：'
    ]
})

await runCase('自己的文件夹：校验通过，自动合并', {
    runner: prReview.run_ci,
    files: [{ status: 'added', filename: 'me/a.cs' }],
    expectCode: 0,
    expectOutput: { auto_merge: 'true' },
    forbidPatterns: ['POST /repos/test/repo/statuses/sha1']
})

await runCase('跨作者内容错误：跳过内容下载并写 failure 状态', {
    runner: prReview.run_ci,
    files: [{ status: 'added', filename: 'Other/bad.txt' }],
    expectCode: 1,
    expectOutput: { auto_merge: 'false' },
    expectPatterns: [
        'POST /repos/test/repo/statuses/sha1',
        `"context":"${REVIEW_CONTEXT}"`,
        '"state":"failure"',
        'POST /repos/test/repo/issues/1/comments'
    ],
    forbidPatterns: ['GET /repos/test/repo/contents/Other/']
})

await runCase('同时改多个别人文件夹：直接不通过且不下载内容', {
    runner: prReview.run_ci,
    files: [
        { status: 'added', filename: 'Alice/a.cs' },
        { status: 'added', filename: 'Bob/b.cs' }
    ],
    expectCode: 1,
    expectOutput: { auto_merge: 'false' },
    expectPatterns: ['只能修改一个其他作者的文件夹', '"state":"failure"'],
    forbidPatterns: ['GET /repos/test/repo/contents/Alice/', 'GET /repos/test/repo/contents/Bob/']
})

await runCase('超过 100 个文件：不下载任何内容', {
    runner: prReview.run_ci,
    files: Array.from({ length: 101 }, (_, index) => ({
        status: 'added',
        filename: `Other/f${index}.cs`
    })),
    expectCode: 1,
    expectOutput: { auto_merge: 'false' },
    expectPatterns: ['超过上限'],
    forbidPatterns: ['GET /repos/test/repo/contents/']
})

await runCase('新文件夹不会被 @：只提醒仓库里已存在的作者', {
    runner: prReview.run_ci,
    files: [{ status: 'added', filename: 'Victim/a.cs' }],
    expectCode: 0,
    expectOutput: { auto_merge: 'false' },
    expectPatterns: ['POST /repos/test/repo/issues/1/comments', '文件夹所有者'],
    forbidPatterns: ['@Victim']
})

await runCase('审核期间作者推送了新提交：丢弃旧结果', {
    runner: prReview.run_ci,
    files: [{ status: 'added', filename: 'Other/a.cs' }],
    pr: { head: { sha: 'sha2' } },
    expectCode: 0,
    expectOutput: { auto_merge: 'false' },
    forbidPatterns: [
        'GET /repos/test/repo/contents/',
        'POST /repos/test/repo/statuses/sha1',
        'POST /repos/test/repo/issues/1/comments',
        'POST /repos/test/repo/issues/1/labels'
    ]
})

await runCase('审核期间 PR 已合并：不再补发结果', {
    runner: prReview.run_ci,
    files: [{ status: 'added', filename: 'Other/a.cs' }],
    pr: { state: 'closed', merged: true },
    expectCode: 0,
    expectOutput: { auto_merge: 'false' },
    forbidPatterns: [
        'GET /repos/test/repo/contents/',
        'POST /repos/test/repo/statuses/sha1',
        'POST /repos/test/repo/issues/1/comments',
        'POST /repos/test/repo/issues/1/labels'
    ]
})

await runCase('所有者 /approve：当前 head 已审核，自动合并', {
    runner: prReview.run_approval,
    files: [{ status: 'added', filename: 'Other/a.cs' }],
    commentAuthor: 'Other',
    commentBody: '/approve',
    expectCode: 0,
    expectOutput: { merged: 'true' },
    expectPatterns: [
        'GET /repos/test/repo/commits/sha1/statuses',
        'GET /repos/test/repo/contents/Other/a.cs',
        'PUT /repos/test/repo/pulls/1/merge',
        '"sha":"sha1"',
        'POST /repos/test/repo/issues/1/comments',
        '已 squash 合并'
    ]
})

await runCase('非所有者 /approve：不读状态、不下载内容、不合并', {
    runner: prReview.run_approval,
    files: [{ status: 'added', filename: 'Other/a.cs' }],
    commentAuthor: 'someone',
    commentBody: '/approve',
    expectCode: 0,
    expectOutput: { merged: undefined },
    forbidPatterns: [
        'GET /repos/test/repo/commits/sha1/statuses',
        'GET /repos/test/repo/contents/',
        'PUT /repos/test/repo/pulls/1/merge',
        'POST /repos/test/repo/issues/1/comments'
    ]
})

await runCase('当前 head 还没审核：拒绝批准并提示等待', {
    runner: prReview.run_approval,
    files: [{ status: 'added', filename: 'Other/a.cs' }],
    statuses: [],
    commentAuthor: 'Other',
    commentBody: '/approve',
    expectCode: 0,
    expectOutput: { merged: undefined },
    expectPatterns: ['还没有完成自动审核', 'POST /repos/test/repo/issues/1/comments'],
    forbidPatterns: ['GET /repos/test/repo/contents/', 'PUT /repos/test/repo/pulls/1/merge']
})

await runCase('作者在批准前推送了新提交：旧审核状态失效', {
    runner: prReview.run_approval,
    files: [{ status: 'added', filename: 'Other/a.cs' }],
    pr: { head: { sha: 'sha2' } },
    commentAuthor: 'Other',
    commentBody: '/approve',
    expectCode: 0,
    expectOutput: { merged: undefined },
    expectPatterns: ['还没有完成自动审核', 'POST /repos/test/repo/issues/1/comments'],
    forbidPatterns: ['GET /repos/test/repo/contents/', 'PUT /repos/test/repo/pulls/1/merge']
})

await runCase('当前 head 审核失败：拒绝批准', {
    runner: prReview.run_approval,
    files: [{ status: 'added', filename: 'Other/a.cs' }],
    statuses: [{ context: REVIEW_CONTEXT, state: 'failure' }],
    commentAuthor: 'Other',
    commentBody: '/approve',
    expectCode: 0,
    expectOutput: { merged: undefined },
    expectPatterns: ['自动审核未通过'],
    forbidPatterns: ['GET /repos/test/repo/contents/', 'PUT /repos/test/repo/pulls/1/merge']
})

await runCase('所有者批准但内容有错：不下载内容、写 failure、不合并', {
    runner: prReview.run_approval,
    files: [{ status: 'added', filename: 'Other/bad.txt' }],
    commentAuthor: 'Other',
    commentBody: '/approve',
    expectCode: 1,
    expectOutput: { merged: undefined },
    expectPatterns: [
        'POST /repos/test/repo/statuses/sha1',
        '"state":"failure"',
        'POST /repos/test/repo/issues/1/comments'
    ],
    forbidPatterns: ['GET /repos/test/repo/contents/', 'PUT /repos/test/repo/pulls/1/merge']
})

await runCase('所有者批准多个作者文件夹：不下载内容、不合并', {
    runner: prReview.run_approval,
    files: [
        { status: 'added', filename: 'Alice/a.cs' },
        { status: 'added', filename: 'Bob/b.cs' }
    ],
    commentAuthor: 'Alice',
    commentBody: '/approve',
    expectCode: 1,
    expectOutput: { merged: undefined },
    expectPatterns: ['只能修改一个其他作者的文件夹', '"state":"failure"'],
    forbidPatterns: ['GET /repos/test/repo/contents/', 'PUT /repos/test/repo/pulls/1/merge']
})

await runCase('自己的文件夹 /approve：无需批准、不下载内容', {
    runner: prReview.run_approval,
    files: [{ status: 'added', filename: 'me/a.cs' }],
    commentAuthor: 'me',
    commentBody: '/approve',
    expectCode: 0,
    expectOutput: { merged: undefined },
    forbidPatterns: [
        'GET /repos/test/repo/commits/sha1/statuses',
        'GET /repos/test/repo/contents/',
        'PUT /repos/test/repo/pulls/1/merge'
    ]
})

await runCase('普通回复：不触发', {
    runner: prReview.run_approval,
    files: [{ status: 'added', filename: 'Other/a.cs' }],
    commentAuthor: 'Other',
    commentBody: '我看看',
    expectCode: 0,
    expectOutput: { merged: undefined },
    forbidPatterns: ['GET /repos/test/repo/pulls/1\n']
})

await runCase('Bot 评论：不触发', {
    runner: prReview.run_approval,
    files: [{ status: 'added', filename: 'Other/a.cs' }],
    commentAuthor: 'github-actions[bot]',
    commentBody: '/approve',
    commentType: 'Bot',
    expectCode: 0,
    expectOutput: { merged: undefined },
    forbidPatterns: ['GET /repos/test/repo/pulls/1\n']
})

await runCase('draft PR：忽略', {
    runner: prReview.run_approval,
    files: [{ status: 'added', filename: 'Other/a.cs' }],
    pr: { draft: true },
    commentAuthor: 'Other',
    commentBody: '/approve',
    expectCode: 0,
    expectOutput: { merged: undefined },
    forbidPatterns: ['GET /repos/test/repo/pulls/1/files', 'PUT /repos/test/repo/pulls/1/merge']
})

await runCase('PR 作者自己 /approve：忽略', {
    runner: prReview.run_approval,
    files: [{ status: 'added', filename: 'me/a.cs' }],
    pr: { user: { login: 'Other' } },
    commentAuthor: 'Other',
    commentBody: '/approve',
    expectCode: 0,
    expectOutput: { merged: undefined },
    forbidPatterns: ['GET /repos/test/repo/pulls/1/files', 'PUT /repos/test/repo/pulls/1/merge']
})

await runCase('合并接口失败：留言提示', {
    runner: prReview.run_approval,
    files: [{ status: 'added', filename: 'Other/a.cs' }],
    commentAuthor: 'Other',
    commentBody: '/approve',
    mergeStatus: 409,
    mergeResult: { message: 'Head branch was modified' },
    expectCode: 1,
    expectOutput: { merged: undefined },
    expectPatterns: [
        'PUT /repos/test/repo/pulls/1/merge',
        'POST /repos/test/repo/issues/1/comments',
        '自动合并失败'
    ]
})

server.closeAllConnections?.()
server.close()
fs.rmSync(tempDir, { recursive: true, force: true })

if (failures.length) {
    console.log(`\n❌ 审批 mock 测试失败 ${failures.length} 项：${failures.join(', ')}`)
    process.exitCode = 1
} else {
    console.log('\n✅ 审批 mock 测试全部通过')
    process.exitCode = 0
}
