import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import process from 'node:process'
import { fileURLToPath } from 'node:url'

import * as csharpMeta from '../csharp_meta.ts'
import * as mergeRepos from '../merge_repos.ts'
import * as prReview from '../pr_review.ts'
import type { ReviewConfig, ForeignChange } from '../pr_review.ts'
import type { OnlineScriptEntry } from '../merge_repos.ts'

const HERE = path.dirname(fileURLToPath(import.meta.url))

/** 深度比较 */
export function deepEqual(a: unknown, b: unknown): boolean {
    if (a === b) return true
    if (Array.isArray(a) || Array.isArray(b)) {
        if (!Array.isArray(a) || !Array.isArray(b) || a.length !== b.length) return false
        return a.every((item, index) => deepEqual(item, b[index]))
    }
    if (a && b && typeof a === 'object' && typeof b === 'object') {
        const recA = a as Record<string, unknown>
        const recB = b as Record<string, unknown>
        const keysA = Object.keys(recA)
        const keysB = Object.keys(recB)
        if (keysA.length !== keysB.length) return false
        return keysA.every((key) => Object.hasOwn(recB, key) && deepEqual(recA[key], recB[key]))
    }
    return false
}

/** 转详情文本 */
function show(value: unknown): string {
    if (typeof value === 'string') return value
    try {
        return JSON.stringify(value)
    } catch {
        return Object.prototype.toString.call(value)
    }
}

/** 建临时目录 */
const makeTempDir = (prefix: string): string => fs.mkdtempSync(path.join(os.tmpdir(), prefix))

/** 删除目录 */
const removeDir = (dir: string): void => fs.rmSync(dir, { recursive: true, force: true })

/** 读取 JSON */
const readJson = <T = unknown>(file: string): T => JSON.parse(fs.readFileSync(file, 'utf8')) as T

/** PR 审核自测 */
export function runPrReviewChecks(): string[] {
    // 失败列表
    const failures: string[] = []
    /** 断言 */
    const expect = (name: string, condition: unknown, detail: unknown = ''): void => {
        if (condition) {
            console.log(`✅ ${name}`)
        } else {
            failures.push(name)
            console.log(`❌ ${name} ${detail}`)
        }
    }

    // 默认配置
    const cfg = prReview.defaultConfig()

    const GUID = '8010d865-7d6d-4c23-92e0-f4b0120e18ac'
    const goodCs =
        `[ScriptType(name: "M1s绘图", territorys: [1226], guid: "${GUID}", ` +
        'version: "0.0.0.9", author: "Karlin")]\npublic class M1s { }\n'

    let { meta, errors, warnings } = prReview.validateScriptFile(
        goodCs,
        'Karlin-Z/M1s.cs',
        'Karlin-Z',
        cfg
    )
    expect('合法 .cs 通过', !errors.length && meta !== null, show(errors))
    expect(
        '字段提取正确',
        meta &&
            meta.name === 'M1s绘图' &&
            meta.guid === GUID &&
            meta.version === '0.0.0.9' &&
            meta.author === 'Karlin' &&
            deepEqual(meta.territorys, [1226]),
        show(meta)
    )
    expect('合法 .cs 无提醒', !warnings.length, show(warnings))

    const noAuthor = goodCs.replace(', author: "Karlin"', '')
    ;({ meta, errors } = prReview.validateScriptFile(noAuthor, 'publisher/A.cs', 'publisher', cfg))
    expect('缺 author 时用文件夹名', !errors.length && meta?.author === 'publisher', show(errors))

    const unknownAuthor = goodCs.replace('"Karlin"', '"Unknown"')
    ;({ meta, errors, warnings } = prReview.validateScriptFile(
        unknownAuthor,
        'publisher/A.cs',
        'publisher',
        cfg
    ))
    expect(
        'author=Unknown 时用文件夹名',
        !errors.length && meta?.author === 'publisher',
        show(errors)
    )
    expect(
        'author 回退有提醒',
        warnings.some((w) => w.includes('文件夹名')),
        show(warnings)
    )

    ;({ errors } = prReview.validateScriptFile('class X {}', 'a/A.cs', 'a', cfg))
    expect('没有特性被拒绝', errors.length > 0)

    const two = goodCs + goodCs
    ;({ errors } = prReview.validateScriptFile(two, 'a/A.cs', 'a', cfg))
    expect(
        '两处特性被拒绝',
        errors.some((e) => e.includes('只能有一处')),
        show(errors)
    )

    const commented = '// ' + goodCs.replace('\n', ' ') + '\n' + goodCs
    ;({ meta, errors } = prReview.validateScriptFile(commented, 'a/A.cs', 'a', cfg))
    expect('注释里的特性不计数', !errors.length && meta !== null, show(errors))

    const tricky =
        goodCs +
        'class Q {\n' +
        "    char a = '\\'';\n" +
        "    char b = ',';\n" +
        '    void F() { Log("[ScriptType(guid: \\"x\\")]"); }\n' +
        '    string p = @"C:\\\\path, x";\n' +
        '}\n'
    ;({ meta, errors } = prReview.validateScriptFile(tricky, 'a/A.cs', 'a', cfg))
    expect('字符字面量 / 字符串不干扰解析', !errors.length && meta !== null, show(errors))

    const q3 = '"'.repeat(3)
    const interpolated =
        `[ScriptType(name: "N", guid: "${GUID}", version: "0.0.0.9", ` +
        'author: "A", territorys: [1226], note: NoteStr, updateInfo: UpdateInfo)]\n' +
        'public class S {\n' +
        '    const string NoteStr =\n' +
        `    $${q3}\n` +
        '    v{Version} 说明\n' +
        `    ${q3};\n` +
        '    const string Version = "1.2.3";\n' +
        '    const string UpdateStr = $"v{Version} 更新";\n' +
        '    const string UpdateInfo = UpdateStr;\n' +
        '}\n'
    ;({ meta, errors, warnings } = prReview.validateScriptFile(interpolated, 'a/A.cs', 'a', cfg))
    expect(
        '插值 const / const 引用 const 被识别且不报警',
        !errors.length && !warnings.length,
        show(errors) + show(warnings)
    )
    expect(
        '插值里的 {const} 被替换成实际值',
        meta !== null && meta.note === 'v1.2.3 说明' && meta.update_info === 'v1.2.3 更新',
        show(meta)
    )

    const unresolved =
        `[ScriptType(name: "N", guid: "${GUID}", version: "0.0.0.9", ` +
        'author: "A", territorys: [1226], note: $"v{Missing} 说明")]\npublic class S { }\n'
    ;({ meta, errors, warnings } = prReview.validateScriptFile(unresolved, 'a/A.cs', 'a', cfg))
    expect(
        '解不出来的插值原样保留，不报错',
        !errors.length && !warnings.length && meta?.note === 'v{Missing} 说明',
        show(meta) + show(errors) + show(warnings)
    )

    const doubleDollar =
        `[ScriptType(name: "N", guid: "${GUID}", version: "0.0.0.9", ` +
        'author: "A", territorys: [1226], note: Two)]\n' +
        'public class S {\n' +
        '    const string Version = "1.2.3";\n' +
        '    const string Two = $$' +
        q3 +
        '\n' +
        '    v{{Version}} 价格 {100}\n' +
        `    ${q3};\n` +
        '}\n'
    ;({ meta, errors, warnings } = prReview.validateScriptFile(doubleDollar, 'a/A.cs', 'a', cfg))
    expect(
        '两个 $ 时只有双花括号才是插值',
        !errors.length && !warnings.length && meta?.note === 'v1.2.3 价格 {100}',
        show(meta) + show(errors) + show(warnings)
    )

    const concat =
        `[ScriptType(name: "N", guid: "${GUID}", version: "0.0.0.9", ` +
        'author: "A", territorys: [1226], note: "a" + $"b{Version}")]\n' +
        'public class S { const string Version = "9"; }\n'
    ;({ meta, errors, warnings } = prReview.validateScriptFile(concat, 'a/A.cs', 'a', cfg))
    expect(
        '属性里内联写 + 拼接字面量也能还原',
        !errors.length && !warnings.length && meta?.note === 'ab9',
        show(meta) + show(errors) + show(warnings)
    )

    const noName = goodCs.replace('name: "M1s绘图", ', '')
    ;({ meta, errors, warnings } = prReview.validateScriptFile(noName, 'a/A.cs', 'a', cfg))
    expect(
        '缺 name 用默认值并提醒',
        !errors.length &&
            meta?.name === 'Default Script' &&
            warnings.some((w) => w.includes('name'))
    )

    const badCases: Array<[string, string, string]> = [
        ['guid 为空', goodCs.replace(GUID, ''), 'guid 不能为空'],
        ['version 非法', goodCs.replace('"0.0.0.9"', '"v1.0"'), '版本号'],
        ['territorys 为负数', goodCs.replace('[1226]', '[-1]'), 'uint 范围'],
        ['territorys 超 uint', goodCs.replace('[1226]', `[${prReview.UINT_MAX + 1}]`), 'uint 范围'],
        ['territorys 是字符串数组', goodCs.replace('[1226]', '["1226"]'), 'uint 数组']
    ]
    for (const [title, bad, needle] of badCases) {
        ;({ errors } = prReview.validateScriptFile(bad, 'a/A.cs', 'a', cfg))
        expect(
            `${title} 被拒绝`,
            errors.some((e) => e.includes(needle)),
            show(errors)
        )
    }

    const traversal = goodCs.replace('"M1s绘图"', '"../../pwn"')
    ;({ errors } = prReview.validateScriptFile(traversal, 'a/A.cs', 'a', cfg))
    expect(
        'name 含路径穿越被拒绝',
        errors.some((e) => e.includes('文件名')),
        show(errors)
    )

    const reserved = goodCs.replace('"Karlin"', '"CON"')
    ;({ errors } = prReview.validateScriptFile(reserved, 'a/A.cs', 'a', cfg))
    expect(
        'author 为 Windows 保留名被拒绝',
        errors.some((e) => e.includes('保留')),
        show(errors)
    )

    const badGuid = goodCs.replace(GUID, 'd99c7e91-9b56-432d-a3a8-49a8586915b7e2a')
    ;({ errors, warnings } = prReview.validateScriptFile(badGuid, 'a/A.cs', 'a', cfg))
    expect(
        '非标准 guid 仅提醒',
        !errors.length && warnings.some((w) => w.includes('UUID')),
        show(errors)
    )

    const doc = JSON.stringify([
        {
            Name: 'M1s绘图',
            Guid: GUID,
            Version: '0.0.0.9',
            Author: 'Karlin',
            Repo: '',
            DownloadUrl: 'https://raw.githubusercontent.com/a/b/main/c.cs',
            Note: '',
            UpdateInfo: '',
            TerritoryIds: [1226]
        }
    ])
    ;({ errors, warnings } = prReview.validateJsonDocument(doc, 'OnlineRepo.json', cfg))
    expect('生成的 json 通过自检', !errors.length && !warnings.length, show(errors))
    ;({ errors } = prReview.validateJsonDocument('{}', 'OnlineRepo.json', cfg))
    expect('生成的 json 顶层非数组被拒绝', errors.length > 0)

    /** 路径校验结果 */
    const pathErrors = (filePath: string, author: string, config?: ReviewConfig): string[] => {
        const collected: string[] = []
        prReview.checkPathRules(filePath, author, config ?? cfg, collected, '路径')
        return collected
    }

    /** 路径审核结果 */
    const pathReview = (
        filePath: string,
        author: string,
        config?: ReviewConfig
    ): { errors: string[]; foreign: ForeignChange[] } => {
        const collected: string[] = []
        const foreign: ForeignChange[] = []
        prReview.checkPathRules(filePath, author, config ?? cfg, collected, '路径', foreign)
        return { errors: collected, foreign }
    }

    expect('正确文件夹通过', !pathErrors('Karlin-Z/M1s.cs', 'Karlin-Z').length)
    expect('大小写不同通过', !pathErrors('karlin-z/M1s.cs', 'Karlin-Z').length)
    expect('子目录通过', !pathErrors('Karlin-Z/sub/M1s.cs', 'Karlin-Z').length)
    expect('改别人文件夹被拒绝（严格模式）', pathErrors('Other/M1s.cs', 'Karlin-Z').length > 0)
    expect(
        '改别人文件夹在审核模式转为待审核',
        !pathReview('Other/M1s.cs', 'Karlin-Z').errors.length &&
            pathReview('Other/M1s.cs', 'Karlin-Z').foreign.length === 1
    )
    expect('根目录文件被拒绝', pathErrors('M1s.cs', 'Karlin-Z').length > 0)
    expect('非 cs 文件被拒绝', pathErrors('Karlin-Z/data.json', 'Karlin-Z').length > 0)
    expect('路径穿越被拒绝', pathErrors('Karlin-Z/../Other/a.cs', 'Karlin-Z').length > 0)
    expect('绝对路径被拒绝', pathErrors('/Karlin-Z/a.cs', 'Karlin-Z').length > 0)

    const strictSub = { ...cfg, allow_subfolders: false }
    expect('禁止子目录时被拒绝', pathErrors('Karlin-Z/sub/a.cs', 'Karlin-Z', strictSub).length > 0)

    const caseSensitive = { ...cfg, username_case_insensitive: false }
    expect('区分大小写时被拒绝', pathErrors('karlin-z/a.cs', 'Karlin-Z', caseSensitive).length > 0)

    const changeFiles: prReview.ChangedFile[] = [
        { status: 'added', filename: 'Karlin-Z/M1s.cs' },
        { status: 'removed', filename: 'Karlin-Z/M2s.cs' }
    ]
    let changeResult = prReview.checkChangeSet(changeFiles, 'Karlin-Z', cfg)
    expect(
        '改动集合校验通过',
        !changeResult.errors.length && changeResult.rows.length === 2,
        show(changeResult.errors)
    )

    const renameOut = [
        { status: 'renamed', filename: 'Karlin-Z/a.cs', previous_filename: 'Other/a.cs' }
    ]
    changeResult = prReview.checkChangeSet(renameOut, 'Karlin-Z', cfg)
    expect(
        '从别人文件夹重命名来源不算错误、转为待审核',
        !changeResult.errors.length &&
            changeResult.foreign.some((item) => item.path === 'Other/a.cs'),
        show(changeResult)
    )

    changeResult = prReview.checkChangeSet(
        [
            { status: 'modified', filename: 'Karlin-Z/a.cs' },
            { status: 'modified', filename: 'Other/b.cs' }
        ],
        'Karlin-Z',
        cfg
    )
    expect(
        '同时改自己和别人文件夹时待审核',
        !changeResult.errors.length && changeResult.foreign.length === 1,
        show(changeResult)
    )

    changeResult = prReview.checkChangeSet(
        [
            { status: 'modified', filename: 'Alice/a.cs' },
            { status: 'modified', filename: 'Bob/b.cs' }
        ],
        'Karlin-Z',
        cfg
    )
    expect(
        '同时改多个别人的文件夹直接不通过',
        changeResult.errors.some((item) => item.includes('只能修改一个其他作者的文件夹')),
        show(changeResult.errors)
    )
    expect(
        '别人的文件夹按大小写去重',
        deepEqual(prReview.foreign_folders([{ folder: 'alice' }, { folder: 'Alice' }]), ['alice'])
    )
    expect(
        '文件夹整体作为一个所有者',
        deepEqual(prReview.foreign_owners([{ folder: 'Alice' }]), ['Alice'])
    )
    expect(
        '带 & 的文件夹不拆成多个所有者',
        deepEqual(prReview.foreign_owners([{ folder: 'alice & bob' }]), [])
    )
    expect(
        '非法用户名不生成 @ 提醒',
        deepEqual(prReview.foreign_owners([{ folder: 'evil\n@all' }]), [])
    )

    changeResult = prReview.checkChangeSet(
        [{ status: 'added', filename: 'README.md' }],
        'Karlin-Z',
        cfg
    )
    expect('改 README 被拒绝', changeResult.errors.length > 0)

    changeResult = prReview.checkChangeSet(
        [{ status: 'added', filename: '.github/scripts/x.cs' }],
        'Karlin-Z',
        cfg
    )
    expect(
        '改 .github 仍被拒绝，不进入待审核',
        changeResult.errors.length > 0 && !changeResult.foreign.length,
        show(changeResult)
    )

    expect('批准命令精确匹配', prReview.is_approval_comment('/approve', cfg))
    expect('批准命令忽略大小写与首尾空白', prReview.is_approval_comment('  /APPROVE  ', cfg))
    expect('中文批准命令', prReview.is_approval_comment('/同意合并', cfg))
    expect('带附加文字不触发', !prReview.is_approval_comment('/approve 我觉得可以', cfg))
    expect('普通回复不触发', !prReview.is_approval_comment('我看看', cfg))
    const customApprove = { ...cfg, approve_commands: ['/lgtm'] }
    expect(
        '自定义批准命令生效',
        prReview.is_approval_comment('/lgtm', customApprove) &&
            !prReview.is_approval_comment('/approve', customApprove)
    )

    const evilReport = prReview.describe(
        [['新增', 'Other/a\n::set-output name=auto_merge::true.cs']],
        ['文件 Other/a\n::error::x.cs：bad'],
        ['路径 `x` | y'],
        [{ path: 'Other/a\n::set-output.cs', folder: 'Other`@evil' }]
    )
    expect(
        '报告不会生成伪造的工作流命令',
        !evilReport
            .split('\n')
            .some((line) => line.startsWith('::set-output') || line.startsWith('::error')),
        evilReport
    )
    expect(
        '报告转义反引号',
        !evilReport.includes('`@evil') && !evilReport.includes('`x`'),
        evilReport
    )

    const bypassCfg = { ...cfg, maintainers: ['Karlin-Z'] }
    changeResult = prReview.checkChangeSet(
        [{ status: 'added', filename: 'README.md' }],
        'Karlin-Z',
        bypassCfg
    )
    expect('维护者白名单可跳过路径限制', !changeResult.errors.length, show(changeResult.errors))

    changeResult = prReview.checkChangeSet(
        [{ status: 'added', filename: 'README.md' }],
        'Karlin-Z',
        { ...cfg, maintainers: ['karlin-z'] }
    )
    expect('维护者白名单大小写不敏感', !changeResult.errors.length, show(changeResult.errors))

    const guidMap = { [GUID]: 'alice/A.cs' }
    let errs = prReview.checkGuidCollisions([['bob/B.cs', GUID]], new Set(), 'bob', guidMap, cfg)
    expect(
        'guid 被别人占用被拒绝',
        errs.some((e) => e.includes('已被 alice/A.cs 使用')),
        show(errs)
    )

    errs = prReview.checkGuidCollisions(
        [['alice/B.cs', GUID]],
        new Set(['alice/B.cs']),
        'alice',
        guidMap,
        cfg
    )
    expect(
        '自己文件夹里 guid 重复被拒绝',
        errs.some((e) => e.includes('你自己文件夹')),
        show(errs)
    )

    errs = prReview.checkGuidCollisions(
        [['alice/sub/A.cs', GUID]],
        new Set(['alice/A.cs', 'alice/sub/A.cs']),
        'alice',
        guidMap,
        cfg
    )
    expect('改名 / 移动自己的文件不算冲突', !errs.length, show(errs))

    errs = prReview.checkGuidCollisions(
        [
            ['alice/A.cs', GUID],
            ['alice/B.cs', GUID]
        ],
        new Set(),
        'alice',
        {},
        cfg
    )
    expect(
        '同一个 PR 内 guid 重复被拒绝',
        errs.some((e) => e.includes('本次 PR')),
        show(errs)
    )

    errs = prReview.checkGuidCollisions(
        [['alice/A.cs', 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee']],
        new Set(),
        'alice',
        {},
        cfg
    )
    expect('拿不到映射表时不误报', !errs.length, show(errs))

    const ignoreCfg = { ...cfg, ignore_dirs: ['skipme'] }
    changeResult = prReview.checkChangeSet(
        [{ status: 'added', filename: 'skipme/a.cs' }],
        'skipme',
        ignoreCfg
    )
    expect(
        '作者文件夹在 ignore_dirs 里被拒绝',
        changeResult.errors.some((e) => e.includes('ignore_dirs')),
        show(changeResult.errors)
    )

    let projErrors: string[] = []
    prReview.checkPathRules('alice/alice.csproj', 'alice', cfg, projErrors, '路径')
    expect('作者目录同名 csproj 通过', !projErrors.length, show(projErrors))

    projErrors = []
    prReview.checkPathRules('alice/other.csproj', 'alice', cfg, projErrors, '路径')
    expect('不同名 csproj 被拒绝', projErrors.length > 0, show(projErrors))

    projErrors = []
    prReview.checkPathRules('alice/sub/alice.csproj', 'alice', cfg, projErrors, '路径')
    expect('子目录 csproj 被拒绝', projErrors.length > 0, show(projErrors))

    expect(
        '项目文件模板通过',
        !prReview.validateProjectFile(prReview.PROJECT_TEMPLATE, 'alice/alice.csproj').length
    )
    expect(
        '修改过的项目文件被拒绝',
        prReview.validateProjectFile(
            prReview.PROJECT_TEMPLATE.replace(
                'KodakkuScripts.$(MSBuildProjectName)',
                'KodakkuScripts.Hacked'
            ),
            'alice/alice.csproj'
        ).length > 0
    )

    const probe = prReview.defaultConfig()
    probe.maintainers.push('__probe__')
    expect('default_config 返回深拷贝', deepEqual(prReview.DEFAULT_CONFIG.maintainers, []))

    const multiMain =
        `namespace MultiNs;\n[ScriptType(guid: "${GUID}", name: "Multi", version: "0.0.0.1", ` +
        'author: "A")]\npublic partial class Multi { }\n'
    const multiPart = 'namespace MultiNs;\npublic partial class Multi { public void A() { } }\n'

    let dirResult = prReview.validateScriptDirectory(
        [
            { rel_path: 'alice/Multi/Main.cs', text: multiMain },
            { rel_path: 'alice/Multi/P1.cs', text: multiPart }
        ],
        cfg
    )
    expect(
        '目录校验识别多文件组',
        dirResult.groups.length === 1 &&
            dirResult.groups[0].parts.length === 1 &&
            !dirResult.errors.length,
        show(dirResult)
    )
    expect(
        '多文件组主文件信息正确',
        dirResult.groups[0]?.meta?.name === 'Multi',
        show(dirResult.groups[0]?.meta)
    )

    dirResult = prReview.validateScriptDirectory(
        [{ rel_path: 'alice/Orphan/Part.cs', text: multiPart }],
        cfg
    )
    expect(
        '孤立 partial 被拒绝',
        dirResult.errors.some((e) => e.includes('缺少同目录的主文件')),
        show(dirResult.errors)
    )

    dirResult = prReview.validateScriptDirectory(
        [
            { rel_path: 'alice/Both/Main.cs', text: multiMain },
            { rel_path: 'alice/Both/Other.cs', text: multiMain }
        ],
        cfg
    )
    expect(
        '同一 partial 多处 ScriptType 被拒绝',
        dirResult.errors.some((e) => e.includes('只能保留一处')),
        show(dirResult.errors)
    )

    dirResult = prReview.validateScriptDirectory(
        [
            { rel_path: 'alice/Multi/Main.cs', text: multiMain },
            { rel_path: 'alice/Multi/P1.cs', text: multiPart },
            { rel_path: 'alice/Other/Broken.cs', text: 'class X { }' }
        ],
        cfg,
        new Set(['alice/Multi/P1.cs'])
    )
    expect(
        '只报告与改动文件相关的错误',
        !dirResult.errors.some((e) => e.includes('Broken.cs')) && dirResult.groups.length === 1,
        show(dirResult.errors)
    )

    const goodDoc = JSON.stringify([
        {
            Name: 'Multi',
            Guid: GUID,
            Version: '0.0.0.1',
            Author: 'A',
            DownloadUrl: 'https://example.com/a.cs',
            Parts: ['https://example.com/b.cs']
        }
    ])
    let docResult = prReview.validateJsonDocument(goodDoc, 'x.json', cfg)
    expect('合法的 Parts 通过', !docResult.errors.length, show(docResult.errors))

    const badDoc = JSON.stringify([
        {
            Name: 'Multi',
            Guid: GUID,
            Version: '0.0.0.1',
            Author: 'A',
            DownloadUrl: 'https://example.com/a.cs',
            Parts: ['https://example.com/a.cs', 'https://example.com/a.cs']
        }
    ])
    docResult = prReview.validateJsonDocument(badDoc, 'x.json', cfg)
    expect(
        'Parts 重复或与主地址相同被拒绝',
        docResult.errors.filter((e) => e.includes('Parts')).length >= 2,
        show(docResult.errors)
    )

    const nullDoc = JSON.stringify([
        {
            Name: 'Multi',
            Guid: GUID,
            Version: '0.0.0.1',
            Author: 'A',
            DownloadUrl: 'https://example.com/a.cs',
            Parts: null
        }
    ])
    docResult = prReview.validateJsonDocument(nullDoc, 'x.json', cfg)
    expect(
        'Parts 为 null 被拒绝',
        docResult.errors.some((e) => e.includes('Parts')),
        show(docResult.errors)
    )

    return failures
}

/** 构造测试脚本 */
function cs(
    guid: string,
    name = '测试脚本',
    author: string | null = null,
    version = '0.0.1',
    territorys: string | null = '[1226]'
): string {
    const parts = [`name: "${name}"`, `guid: "${guid}"`, `version: "${version}"`]
    if (author !== null) parts.push(`author: "${author}"`)
    if (territorys !== null) parts.push(`territorys: ${territorys}`)
    return '[ScriptType(' + parts.join(', ') + ')]\npublic class S { }\n'
}

/** 索引合并自测 */
export function runMergeReposChecks(): string[] {
    const failures: string[] = []
    /** 断言 */
    const expect = (name: string, condition: unknown, detail: unknown = ''): void => {
        if (condition) {
            console.log(`✅ ${name}`)
        } else {
            failures.push(name)
            console.log(`❌ ${name} ${detail}`)
        }
    }

    const cfg = prReview.defaultConfig()

    /** 静默执行 build */
    const quietBuild = (...args: Parameters<typeof mergeRepos.build>): number => {
        const original = process.stdout.write
        process.stdout.write = (() => true) as typeof process.stdout.write
        try {
            return mergeRepos.build(...args)
        } finally {
            process.stdout.write = original
        }
    }

    const GUID_A1 = '8010d865-7d6d-4c23-92e0-f4b0120e18ac'
    const GUID_A2 = 'd99c7e91-9b56-432d-a3a8-49a8586915b7e2a'
    const GUID_A3 = 'e7f7c69b-cc82-4b74-b1ea-2f3f0eecb2e2'
    const GUID_A4 = '37ea4922-dee4-b998-f23f-e2a1cd1b1bcd'
    const GUID_B2 = 'a4e14eff-0aea-a4b6-d8c3-47644a3e9e9a'

    // 临时目录
    const workdir = makeTempDir('kasl_merge_')
    try {
        for (const rel of [
            'alice/sub',
            'alice/_draft',
            'alice/skipme',
            'bob',
            '.github/scripts',
            '_site'
        ]) {
            fs.mkdirSync(path.join(workdir, rel), { recursive: true })
        }

        /** 写测试文件 */
        const write = (rel: string, content: string): void => {
            fs.writeFileSync(path.join(workdir, rel), content, 'utf8')
        }

        write('alice/A1.cs', cs(GUID_A1, 'A1', 'Alice'))
        write('alice/sub/A2.cs', cs(GUID_A2, 'A2', null))
        write('alice/_draft/A3.cs', cs(GUID_A3, 'A3', 'Alice'))
        write('alice/skipme/A4.cs', cs(GUID_A4, 'A4', 'Alice'))
        write('bob/B1.cs', cs(GUID_A1, 'B1', 'Bob'))
        write('bob/B2.cs', cs(GUID_B2, 'B2', 'Bob', '0.0.1', '[1226, 1228]'))
        write('alice/Bad.cs', 'public class NoAttribute { }\n')
        write('.github/scripts/x.cs', cs(GUID_A1, 'ignored'))
        write('_site/y.cs', cs(GUID_A1, 'ignored'))
        write('OnlineRepo.json', '[]')

        let collected = mergeRepos.collect(workdir, cfg, 'owner/repo', 'main')
        let { records, skipped, fileCounts } = collected
        const rels = records.map(([, rel]) => rel)
        expect(
            '忽略 . 与 _ 开头的第一层目录',
            rels.every((r) => !r.includes('_site') && !r.includes('.github'))
        )
        expect('递归收集子目录', rels.includes('alice/sub/A2.cs'))
        expect('子目录里 _ 开头的目录也收录', rels.includes('alice/_draft/A3.cs'), show(rels))
        expect(
            '根目录 json 不参与扫描',
            rels.every((r) => r !== 'OnlineRepo.json')
        )
        expect('收集到 6 个有效文件', records.length === 6, show(records.length))
        expect(
            '无特性的文件被跳过',
            skipped.some(([rel]) => rel === 'alice/Bad.cs'),
            show(skipped)
        )
        expect('按贡献者统计文件数', deepEqual(fileCounts, { alice: 5, bob: 2 }), show(fileCounts))

        const byName: Record<string, OnlineScriptEntry> = {}
        for (const [, , entry] of records) byName[entry.Name] = entry
        expect('author 缺失时用文件夹名', byName.A2.Author === 'alice', byName.A2.Author)
        expect(
            'DownloadUrl 自动生成',
            byName.A2.DownloadUrl ===
                'https://raw.githubusercontent.com/owner/repo/main/alice/sub/A2.cs',
            byName.A2.DownloadUrl
        )
        expect(
            '深层子目录 DownloadUrl 正确',
            byName.A3.DownloadUrl.endsWith('/alice/_draft/A3.cs'),
            byName.A3.DownloadUrl
        )

        const keysA1 = Object.keys(byName.A1)
        expect(
            '字段顺序规范且省略空值',
            deepEqual(
                keysA1,
                mergeRepos.CANONICAL_FIELD_ORDER.filter((k) => keysA1.includes(k))
            ) &&
                keysA1.every((k) => {
                    const value = (byName.A1 as unknown as Record<string, unknown>)[k]
                    return (
                        value !== '' && value !== null && !(Array.isArray(value) && !value.length)
                    )
                }),
            show(keysA1)
        )
        expect(
            'Repo / Note / UpdateInfo 为空时被省略',
            ['Repo', 'Note', 'UpdateInfo'].every((k) => !(k in byName.A1)),
            show(byName.A1)
        )
        expect(
            'TerritoryIds 正确',
            deepEqual(byName.B2.TerritoryIds, [1226, 1228]),
            show(byName.B2.TerritoryIds)
        )

        let mergedResult = mergeRepos.mergeRecords(records)
        let { merged, conflicts, ownerCounts, guidMap } = mergedResult
        expect('Guid 去重（忽略大小写）', merged.length === 5, show(merged.length))
        expect(
            '冲突被记录且先出现的胜出',
            conflicts.length === 1 &&
                merged.some((e) => e.Name === 'A1') &&
                merged.every((e) => e.Name !== 'B1'),
            show(conflicts)
        )
        expect(
            '按贡献者统计收录数',
            deepEqual(ownerCounts, { alice: 4, bob: 1 }),
            show(ownerCounts)
        )
        expect(
            'guid_map 指向生效文件',
            guidMap[GUID_A1.toLowerCase()] === 'alice/A1.cs',
            show(guidMap)
        )

        const skipCfg = { ...cfg, ignore_dirs: ['skipme'] }
        collected = mergeRepos.collect(workdir, skipCfg, 'owner/repo', 'main')
        const rels2 = collected.records.map(([, rel]) => rel)
        expect('ignore_dirs 排除深层目录', !rels2.includes('alice/skipme/A4.cs'), show(rels2))
        expect(
            'ignore_dirs 生效后数量正确',
            collected.records.length === 5,
            show(collected.records.length)
        )

        const outDir = path.join(workdir, '_out')
        const rootOut = path.join(workdir, '_generated', 'OnlineRepo.json')
        const mapOut = path.join(workdir, '_generated', 'guid-map.json')
        const code = quietBuild(workdir, cfg, 'owner/repo', 'main', {
            outDir,
            rootJsonPath: rootOut,
            guidMapPath: mapOut
        })
        expect('build 正常退出', code === 0, show(code))
        const written = readJson<OnlineScriptEntry[]>(path.join(outDir, 'index.json'))
        expect('站点 index.json 条目正确', written.length === 5, show(written.length))
        expect('index.html 已生成', fs.existsSync(path.join(outDir, 'index.html')))
        const rootWritten = readJson(rootOut)
        expect('根目录 json 内容一致', deepEqual(rootWritten, written))
        const writtenMap = readJson<Record<string, string>>(mapOut)
        expect(
            'guid-map 文件已写出且内容正确',
            writtenMap[GUID_B2.toLowerCase()] === 'bob/B2.cs',
            show(writtenMap)
        )

        const again = path.join(workdir, '_out2')
        quietBuild(workdir, cfg, 'owner/repo', 'main', { outDir: again })
        expect(
            '重复生成结果一致',
            fs.readFileSync(path.join(again, 'index.json'), 'utf8') ===
                fs.readFileSync(path.join(outDir, 'index.json'), 'utf8')
        )

        const onlyRoot = path.join(workdir, '_root_only.json')
        quietBuild(workdir, cfg, 'owner/repo', 'main', { rootJsonPath: onlyRoot })
        expect('--no-site 只写根目录 json', fs.existsSync(onlyRoot))

        const multi = makeTempDir('kasl_multi_')
        try {
            fs.mkdirSync(path.join(multi, 'alice', 'Multi'), { recursive: true })
            fs.mkdirSync(path.join(multi, 'alice', 'Orphan'), { recursive: true })
            fs.mkdirSync(path.join(multi, 'bob', 'Two'), { recursive: true })
            /** 写测试文件 */
            const writeMulti = (rel: string, content: string): void => {
                fs.writeFileSync(path.join(multi, rel), content, 'utf8')
            }

            writeMulti(
                'alice/Multi/Main.cs',
                `namespace MultiNs;\n[ScriptType(guid: "${GUID_A1}", name: "Multi", version: "0.0.0.1", author: "Alice")]\npartial public class Multi { }\n`
            )
            writeMulti(
                'alice/Multi/P1.cs',
                'namespace MultiNs;\npublic partial class Multi { public void A() { } }\n'
            )
            writeMulti(
                'alice/Multi/P2.cs',
                'namespace MultiNs;\npublic partial class Multi { public void B() { } }\n'
            )
            writeMulti(
                'alice/Orphan/Part.cs',
                'namespace MultiNs;\npublic partial class Orphan { }\n'
            )

            writeMulti(
                'bob/Two/A.cs',
                `namespace TwoNs;\n[ScriptType(guid: "${GUID_A4}", name: "TwoA", version: "0.0.0.1", author: "Bob")]\npublic partial class TwoA { }\n`
            )
            writeMulti(
                'bob/Two/A.Part.cs',
                'namespace TwoNs;\npublic partial class TwoA { public void A() { } }\n'
            )
            writeMulti(
                'bob/Two/B.cs',
                `namespace TwoNs;\n[ScriptType(guid: "${GUID_B2}", name: "TwoB", version: "0.0.0.1", author: "Bob")]\npublic partial class TwoB { }\n`
            )
            writeMulti(
                'bob/Two/B.Part.cs',
                'namespace TwoNs;\npublic partial class TwoB { public void B() { } }\n'
            )

            const multiCollected = mergeRepos.collect(multi, cfg, 'owner/repo', 'main')
            const multiRels = multiCollected.records.map(([, rel]) => rel)
            expect(
                '多文件脚本只生成一条记录',
                multiRels.filter((rel) => rel === 'alice/Multi/Main.cs').length === 1 &&
                    multiRels.filter((rel) => rel.startsWith('alice/Multi/')).length === 1,
                show(multiRels)
            )
            const multiParts = multiCollected.records.find(
                ([, rel]) => rel === 'alice/Multi/Main.cs'
            )?.[2].Parts
            expect(
                '多文件脚本 Parts 正确',
                multiParts?.length === 2 &&
                    multiParts.every((url) => url.includes('/alice/Multi/')),
                show(multiParts)
            )
            expect(
                '孤立 partial 被跳过',
                multiCollected.skipped.some(
                    ([rel, errs]) =>
                        rel === 'alice/Orphan/Part.cs' &&
                        errs.some((e) => e.includes('缺少同目录的主文件'))
                ),
                show(multiCollected.skipped)
            )
            expect(
                '同目录两个独立 partial 组分别成条',
                multiRels.filter((rel) => rel.startsWith('bob/Two/')).length === 2,
                show(multiRels)
            )

            const multiJson = path.join(multi, 'out.json')
            quietBuild(multi, cfg, 'owner/repo', 'main', { rootJsonPath: multiJson })
            const multiDoc = readJson<OnlineScriptEntry[]>(multiJson)
            expect(
                '清单写出 Parts',
                multiDoc.find((entry) => entry.Name === 'Multi')?.Parts?.length === 2,
                show(multiDoc)
            )
        } finally {
            removeDir(multi)
        }

        write('alice/极佐拉加 绘图.cs', cs(GUID_A2, '中文名', 'Alice'))
        collected = mergeRepos.collect(workdir, cfg, 'owner/repo', 'main')
        const cn = collected.records
            .map(([, , entry]) => entry)
            .filter((e) => e.DownloadUrl.includes('%'))
        expect(
            '中文路径被百分号编码',
            cn.length > 0 && cn[0].DownloadUrl.includes('%') && !cn[0].DownloadUrl.includes(' '),
            cn.length ? cn[0].DownloadUrl : 'not found'
        )

        expect(
            'strict 模式在跳过时报错',
            quietBuild(workdir, cfg, 'owner/repo', 'main', {
                outDir: path.join(workdir, '_out3'),
                strict: true
            }) === 1
        )

        const first = makeTempDir('kasl_ignore_')
        try {
            fs.mkdirSync(path.join(first, 'examples'), { recursive: true })
            fs.mkdirSync(path.join(first, 'alice'), { recursive: true })
            fs.writeFileSync(path.join(first, 'examples', 't.cs'), cs(GUID_A1, 'template'), 'utf8')
            fs.writeFileSync(path.join(first, 'alice', 'a.cs'), cs(GUID_A2, 'real'), 'utf8')
            const exampleCfg = { ...cfg, ignore_dirs: ['examples'] }
            const got = mergeRepos.collect(first, exampleCfg, 'owner/repo', 'main')
            expect(
                'ignore_dirs 排除第一层目录',
                deepEqual(
                    got.records.map(([, rel]) => rel),
                    ['alice/a.cs']
                ),
                show(got.records)
            )
        } finally {
            removeDir(first)
        }

        const emptyDir = makeTempDir('kasl_empty_')
        try {
            expect(
                '空仓库默认允许生成并告警',
                quietBuild(emptyDir, cfg, 'owner/repo', 'main', {
                    outDir: path.join(emptyDir, 'out')
                }) === 0
            )
            expect(
                '--forbid-empty 时拒绝生成',
                quietBuild(emptyDir, cfg, 'owner/repo', 'main', {
                    outDir: path.join(emptyDir, 'out2'),
                    forbidEmpty: true
                }) === 1
            )
        } finally {
            removeDir(emptyDir)
        }
    } finally {
        removeDir(workdir)
    }

    return failures
}

/** 解析器自测 */
export function runCsharpMetaChecks(): string[] {
    const failures: string[] = []
    /** 断言 */
    const expect = (name: string, condition: unknown, detail: unknown = ''): void => {
        if (condition) {
            console.log(`✅ ${name}`)
        } else {
            failures.push(name)
            console.log(`❌ ${name} ${detail}`)
        }
    }

    const casesDir = path.join(HERE, 'cases')
    // 读取快照
    const snapshot = readJson<Record<string, unknown>>(path.join(HERE, 'cases.snapshot.json'))

    for (const name of Object.keys(snapshot).sort()) {
        const file = path.join(casesDir, name)
        if (!fs.existsSync(file)) {
            expect(name, false, '用例文件缺失')
            continue
        }
        const { params, errors } = csharpMeta.extractScriptType(fs.readFileSync(file, 'utf8'))
        expect(
            name,
            deepEqual({ params, errors }, snapshot[name]),
            `实际 ${show({ params, errors })}`
        )
    }

    expect(
        '提取 partial 类型全名',
        deepEqual(
            csharpMeta
                .extractPartialTypes(
                    'namespace A.B;\npublic partial class X { }\ninternal partial record class Y { }\n'
                )
                .names.sort(),
            ['A.B.X', 'A.B.Y']
        )
    )
    expect(
        '修饰符顺序不影响 partial 识别',
        deepEqual(
            csharpMeta
                .extractPartialTypes(
                    'namespace N;\npartial public class A { }\npartial sealed class B { }\n' +
                        'partial ref struct C { }\npartial readonly record struct D { }\n'
                )
                .names.sort(),
            ['N.A', 'N.B', 'N.C', 'N.D']
        )
    )
    expect(
        '嵌套类型与 namespace 拼全名',
        deepEqual(
            csharpMeta
                .extractPartialTypes(
                    'namespace A {\n  namespace B {\n    class Outer {\n      partial class Inner { }\n    }\n  }\n}\n'
                )
                .names.sort(),
            ['A.B.Outer.Inner']
        )
    )
    expect(
        '注释和字符串里的 partial 不参与',
        deepEqual(
            csharpMeta.extractPartialTypes(
                '// partial class X\nstring s = "partial class Y";\n/* partial class Z */\nnamespace N;\npartial class Real { }\n'
            ).names,
            ['N.Real']
        )
    )
    expect(
        '识别 ScriptType 特性',
        csharpMeta.hasScriptTypeAttribute(
            'namespace N;\n[ScriptType(guid: "x")]\npartial class X { }\n'
        ) && !csharpMeta.hasScriptTypeAttribute('namespace N;\npartial class X { }\n')
    )
    expect(
        'ScriptType 括号写坏时仍算找到',
        csharpMeta.hasScriptTypeAttribute(
            'namespace N;\n[ScriptType(guid: "x"]\npartial class X { }\n'
        )
    )

    return failures
}

/** 自测入口 */
export function main(): number {
    const failures: string[] = []
    const runners: Array<[string, () => string[]]> = [
        ['pr_review', runPrReviewChecks],
        ['merge_repos', runMergeReposChecks],
        ['csharp_meta', runCsharpMetaChecks]
    ]
    for (const [title, runner] of runners) {
        console.log(`=== ${title} ===`)
        const failed = runner()
        failures.push(...failed)
        console.log(`--- ${title}：${failed.length ? `失败 ${failed.length} 项` : '全部通过'}`)
        console.log()
    }

    if (failures.length) {
        console.log(`❌ 自测失败 ${failures.length} 项：${failures.join(', ')}`)
        return 1
    }
    console.log('✅ 全部自测通过')
    return 0
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    process.exitCode = main()
}
