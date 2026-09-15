/**
 * 内置自测：校验 pr_review 与 merge_repos 的核心规则。
 *
 * 从两个生产脚本里拆出来的——它们会在 CI 里无人值守地审核并合并 PR，还托管着会被
 * 插件编译执行的脚本，改完之后必须能快速确认行为没变。这里不联网、不碰真实仓库，
 * 只在临时目录里造样本。
 *
 *     node .github/scripts/tests/selftest.mjs
 *
 * 本文件由 selftest.py 移植而来（Python 版已随迁移移除）：断言集合与输出格式保持一致，
 * 另外补了一组 csharp_meta 的边界用例。
 */

import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import process from 'node:process'
import { fileURLToPath } from 'node:url'

import * as csharpMeta from '../csharp_meta.mjs'
import * as mergeRepos from '../merge_repos.mjs'
import * as prReview from '../pr_review.mjs'

const HERE = path.dirname(fileURLToPath(import.meta.url))

/** 结构性比较，忽略对象原型差异（Python 的 == 语义）。 */
export function deepEqual(a, b) {
  if (a === b) return true
  if (Array.isArray(a) || Array.isArray(b)) {
    if (!Array.isArray(a) || !Array.isArray(b) || a.length !== b.length) return false
    return a.every((item, index) => deepEqual(item, b[index]))
  }
  if (a && b && typeof a === 'object' && typeof b === 'object') {
    const keysA = Object.keys(a)
    const keysB = Object.keys(b)
    if (keysA.length !== keysB.length) return false
    return keysA.every((key) => Object.hasOwn(b, key) && deepEqual(a[key], b[key]))
  }
  return false
}

/** 详情字符串：对象走 JSON，避免无原型对象（Object.create(null)）没法被 show()。 */
function show(value) {
  if (typeof value === 'string') return value
  try {
    return JSON.stringify(value)
  } catch {
    return Object.prototype.toString.call(value)
  }
}

/** 造一个临时目录，用后由调用方删除。 */
const makeTempDir = (prefix) => fs.mkdtempSync(path.join(os.tmpdir(), prefix))

/** 递归删除，对应 Python 的 shutil.rmtree(..., ignore_errors=True)。 */
const removeDir = (dir) => fs.rmSync(dir, { recursive: true, force: true })

const readJson = (file) => JSON.parse(fs.readFileSync(file, 'utf8'))

// --------------------------------------------------------------------------- //
// pr_review：路径规则、.cs 校验、guid 查重、生成结果自检
// --------------------------------------------------------------------------- //

/** 跑 pr_review 的断言，返回失败项名称。 */
export function runPrReviewChecks() {
  const failures = []
  const expect = (name, condition, detail = '') => {
    if (condition) {
      console.log(`✅ ${name}`)
    } else {
      failures.push(name)
      console.log(`❌ ${name} ${detail}`)
    }
  }

  const cfg = prReview.defaultConfig()

  const GUID = '8010d865-7d6d-4c23-92e0-f4b0120e18ac'
  const goodCs =
    `[ScriptType(name: "M1s绘图", territorys: [1226], guid: "${GUID}", ` +
    'version: "0.0.0.9", author: "Karlin")]\npublic class M1s { }\n'

  let { meta, errors, warnings } = prReview.validateScriptFile(goodCs, 'Karlin-Z/M1s.cs', 'Karlin-Z', cfg)
  expect('合法 .cs 通过', !errors.length && meta !== null, show(errors))
  expect(
    '字段提取正确',
    meta &&
      meta.name === 'M1s绘图' &&
      meta.guid === GUID &&
      meta.version === '0.0.0.9' &&
      meta.author === 'Karlin' &&
      deepEqual(meta.territorys, [1226]),
    show(meta),
  )
  expect('合法 .cs 无提醒', !warnings.length, show(warnings))

  // author 回退到文件夹名
  const noAuthor = goodCs.replace(', author: "Karlin"', '')
  ;({ meta, errors } = prReview.validateScriptFile(noAuthor, 'publisher/A.cs', 'publisher', cfg))
  expect('缺 author 时用文件夹名', !errors.length && meta.author === 'publisher', show(errors))

  const unknownAuthor = goodCs.replace('"Karlin"', '"Unknown"')
  ;({ meta, errors, warnings } = prReview.validateScriptFile(
    unknownAuthor,
    'publisher/A.cs',
    'publisher',
    cfg,
  ))
  expect('author=Unknown 时用文件夹名', !errors.length && meta.author === 'publisher', show(errors))
  expect(
    'author 回退有提醒',
    warnings.some((w) => w.includes('文件夹名')),
    show(warnings),
  )

  // 结构性错误
  ;({ errors } = prReview.validateScriptFile('class X {}', 'a/A.cs', 'a', cfg))
  expect('没有特性被拒绝', errors.length > 0)

  const two = goodCs + goodCs
  ;({ errors } = prReview.validateScriptFile(two, 'a/A.cs', 'a', cfg))
  expect('两处特性被拒绝', errors.some((e) => e.includes('只能有一处')), show(errors))

  const commented = '// ' + goodCs.replace('\n', ' ') + '\n' + goodCs
  ;({ meta, errors } = prReview.validateScriptFile(commented, 'a/A.cs', 'a', cfg))
  expect('注释里的特性不计数', !errors.length && meta !== null, show(errors))

  // 词法器边界：字符字面量、字符串里的假特性、逐字字符串都必须被正确跳过（跳过失败会死循环）
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

  // 插值字符串：真实的 note / updateInfo 常写成插值原始字符串，不能因为含插值就整段丢掉
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
    show(errors) + show(warnings),
  )
  expect(
    '插值里的 {const} 被替换成实际值',
    meta !== null && meta.note === 'v1.2.3 说明' && meta.update_info === 'v1.2.3 更新',
    show(meta),
  )

  const unresolved =
    `[ScriptType(name: "N", guid: "${GUID}", version: "0.0.0.9", ` +
    'author: "A", territorys: [1226], note: $"v{Missing} 说明")]\npublic class S { }\n'
  ;({ meta, errors, warnings } = prReview.validateScriptFile(unresolved, 'a/A.cs', 'a', cfg))
  expect(
    '解不出来的插值原样保留，不报错',
    !errors.length && !warnings.length && meta.note === 'v{Missing} 说明',
    show(meta) + show(errors) + show(warnings),
  )

  const doubleDollar =
    `[ScriptType(name: "N", guid: "${GUID}", version: "0.0.0.9", ` +
    'author: "A", territorys: [1226], note: Two)]\n' +
    'public class S {\n' +
    '    const string Version = "1.2.3";\n' +
    '    const string Two = $$' + q3 + '\n' +
    '    v{{Version}} 价格 {100}\n' +
    `    ${q3};\n` +
    '}\n'
  ;({ meta, errors, warnings } = prReview.validateScriptFile(doubleDollar, 'a/A.cs', 'a', cfg))
  expect(
    '两个 $ 时只有双花括号才是插值',
    !errors.length && !warnings.length && meta.note === 'v1.2.3 价格 {100}',
    show(meta) + show(errors) + show(warnings),
  )

  const concat =
    `[ScriptType(name: "N", guid: "${GUID}", version: "0.0.0.9", ` +
    'author: "A", territorys: [1226], note: "a" + $"b{Version}")]\n' +
    'public class S { const string Version = "9"; }\n'
  ;({ meta, errors, warnings } = prReview.validateScriptFile(concat, 'a/A.cs', 'a', cfg))
  expect(
    '属性里内联写 + 拼接字面量也能还原',
    !errors.length && !warnings.length && meta.note === 'ab9',
    show(meta) + show(errors) + show(warnings),
  )

  const noName = goodCs.replace('name: "M1s绘图", ', '')
  ;({ meta, errors, warnings } = prReview.validateScriptFile(noName, 'a/A.cs', 'a', cfg))
  expect(
    '缺 name 用默认值并提醒',
    !errors.length && meta.name === 'Default Script' && warnings.some((w) => w.includes('name')),
  )

  // 字段值错误
  const badCases = [
    ['guid 为空', goodCs.replace(GUID, ''), 'guid 不能为空'],
    ['version 非法', goodCs.replace('"0.0.0.9"', '"v1.0"'), '版本号'],
    ['territorys 为负数', goodCs.replace('[1226]', '[-1]'), 'uint 范围'],
    ['territorys 超 uint', goodCs.replace('[1226]', `[${prReview.UINT_MAX + 1}]`), 'uint 范围'],
    ['territorys 是字符串数组', goodCs.replace('[1226]', '["1226"]'), 'uint 数组'],
  ]
  for (const [title, bad, needle] of badCases) {
    ;({ errors } = prReview.validateScriptFile(bad, 'a/A.cs', 'a', cfg))
    expect(`${title} 被拒绝`, errors.some((e) => e.includes(needle)), show(errors))
  }

  const traversal = goodCs.replace('"M1s绘图"', '"../../pwn"')
  ;({ errors } = prReview.validateScriptFile(traversal, 'a/A.cs', 'a', cfg))
  expect('name 含路径穿越被拒绝', errors.some((e) => e.includes('文件名')), show(errors))

  const reserved = goodCs.replace('"Karlin"', '"CON"')
  ;({ errors } = prReview.validateScriptFile(reserved, 'a/A.cs', 'a', cfg))
  expect('author 为 Windows 保留名被拒绝', errors.some((e) => e.includes('保留')), show(errors))

  const badGuid = goodCs.replace(GUID, 'd99c7e91-9b56-432d-a3a8-49a8586915b7e2a')
  ;({ errors, warnings } = prReview.validateScriptFile(badGuid, 'a/A.cs', 'a', cfg))
  expect(
    '非标准 guid 仅提醒',
    !errors.length && warnings.some((w) => w.includes('UUID')),
    show(errors),
  )

  // 生成的 json 自检
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
      TerritoryIds: [1226],
    },
  ])
  ;({ errors, warnings } = prReview.validateJsonDocument(doc, 'OnlineRepo.json', cfg))
  expect('生成的 json 通过自检', !errors.length && !warnings.length, show(errors))
  ;({ errors } = prReview.validateJsonDocument('{}', 'OnlineRepo.json', cfg))
  expect('生成的 json 顶层非数组被拒绝', errors.length > 0)

  // 路径规则
  const pathErrors = (filePath, author, config) => {
    const collected = []
    prReview.checkPathRules(filePath, author, config ?? cfg, collected, '路径')
    return collected
  }

  expect('正确文件夹通过', !pathErrors('Karlin-Z/M1s.cs', 'Karlin-Z').length)
  expect('大小写不同通过', !pathErrors('karlin-z/M1s.cs', 'Karlin-Z').length)
  expect('子目录通过', !pathErrors('Karlin-Z/sub/M1s.cs', 'Karlin-Z').length)
  expect('改别人文件夹被拒绝', pathErrors('Other/M1s.cs', 'Karlin-Z').length > 0)
  expect('根目录文件被拒绝', pathErrors('M1s.cs', 'Karlin-Z').length > 0)
  expect('非 cs 文件被拒绝', pathErrors('Karlin-Z/data.json', 'Karlin-Z').length > 0)
  expect('路径穿越被拒绝', pathErrors('Karlin-Z/../Other/a.cs', 'Karlin-Z').length > 0)
  expect('绝对路径被拒绝', pathErrors('/Karlin-Z/a.cs', 'Karlin-Z').length > 0)

  const strictSub = { ...cfg, allow_subfolders: false }
  expect('禁止子目录时被拒绝', pathErrors('Karlin-Z/sub/a.cs', 'Karlin-Z', strictSub).length > 0)

  const caseSensitive = { ...cfg, username_case_insensitive: false }
  expect('区分大小写时被拒绝', pathErrors('karlin-z/a.cs', 'Karlin-Z', caseSensitive).length > 0)

  const changeFiles = [
    { status: 'added', filename: 'Karlin-Z/M1s.cs' },
    { status: 'removed', filename: 'Karlin-Z/M2s.cs' },
  ]
  let changeResult = prReview.checkChangeSet(changeFiles, 'Karlin-Z', cfg)
  expect('改动集合校验通过', !changeResult.errors.length && changeResult.rows.length === 2, show(changeResult.errors))

  const renameOut = [
    { status: 'renamed', filename: 'Karlin-Z/a.cs', previous_filename: 'Other/a.cs' },
  ]
  changeResult = prReview.checkChangeSet(renameOut, 'Karlin-Z', cfg)
  expect('从别人文件夹重命名过来被拒绝', changeResult.errors.length > 0)

  changeResult = prReview.checkChangeSet([{ status: 'added', filename: 'README.md' }], 'Karlin-Z', cfg)
  expect('改 README 被拒绝', changeResult.errors.length > 0)

  const bypassCfg = { ...cfg, maintainers: ['Karlin-Z'] }
  changeResult = prReview.checkChangeSet(
    [{ status: 'added', filename: 'README.md' }],
    'Karlin-Z',
    bypassCfg,
  )
  expect('维护者白名单可跳过路径限制', !changeResult.errors.length, show(changeResult.errors))

  // guid 查重
  const guidMap = { [GUID]: 'alice/A.cs' }
  let errs = prReview.checkGuidCollisions([['bob/B.cs', GUID]], new Set(), 'bob', guidMap, cfg)
  expect('guid 被别人占用被拒绝', errs.some((e) => e.includes('已被 alice/A.cs 使用')), show(errs))

  errs = prReview.checkGuidCollisions(
    [['alice/B.cs', GUID]],
    new Set(['alice/B.cs']),
    'alice',
    guidMap,
    cfg,
  )
  expect('自己文件夹里 guid 重复被拒绝', errs.some((e) => e.includes('你自己文件夹')), show(errs))

  errs = prReview.checkGuidCollisions(
    [['alice/sub/A.cs', GUID]],
    new Set(['alice/A.cs', 'alice/sub/A.cs']),
    'alice',
    guidMap,
    cfg,
  )
  expect('改名 / 移动自己的文件不算冲突', !errs.length, show(errs))

  errs = prReview.checkGuidCollisions(
    [
      ['alice/A.cs', GUID],
      ['alice/B.cs', GUID],
    ],
    new Set(),
    'alice',
    {},
    cfg,
  )
  expect('同一个 PR 内 guid 重复被拒绝', errs.some((e) => e.includes('本次 PR')), show(errs))

  errs = prReview.checkGuidCollisions(
    [['alice/A.cs', 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee']],
    new Set(),
    'alice',
    {},
    cfg,
  )
  expect('拿不到映射表时不误报', !errs.length, show(errs))

  // ignore_dirs 里的文件夹
  const ignoreCfg = { ...cfg, ignore_dirs: ['skipme'] }
  changeResult = prReview.checkChangeSet([{ status: 'added', filename: 'skipme/a.cs' }], 'skipme', ignoreCfg)
  expect(
    '作者文件夹在 ignore_dirs 里被拒绝',
    changeResult.errors.some((e) => e.includes('ignore_dirs')),
    show(changeResult.errors),
  )

  // 回归测试：配置拷贝必须是深拷贝
  const probe = prReview.defaultConfig()
  probe.maintainers.push('__probe__')
  expect('default_config 返回深拷贝', deepEqual(prReview.DEFAULT_CONFIG.maintainers, []))

  return failures
}

// --------------------------------------------------------------------------- //
// merge_repos：扫描范围、DownloadUrl、去重、ignore_dirs、产物
// --------------------------------------------------------------------------- //

/** 自测用：拼一个最小可解析的 .cs 内容。 */
function cs(guid, name = '测试脚本', author = null, version = '0.0.1', territorys = '[1226]') {
  const parts = [`name: "${name}"`, `guid: "${guid}"`, `version: "${version}"`]
  if (author !== null) parts.push(`author: "${author}"`)
  if (territorys !== null) parts.push(`territorys: ${territorys}`)
  return '[ScriptType(' + parts.join(', ') + ')]\npublic class S { }\n'
}

/** 跑 merge_repos 的断言，返回失败项名称。 */
export function runMergeReposChecks() {
  const failures = []
  const expect = (name, condition, detail = '') => {
    if (condition) {
      console.log(`✅ ${name}`)
    } else {
      failures.push(name)
      console.log(`❌ ${name} ${detail}`)
    }
  }

  const cfg = prReview.defaultConfig()

  /** build 会往 stdout 写报告，自测里屏蔽掉。 */
  const quietBuild = (...args) => {
    const original = process.stdout.write
    process.stdout.write = () => true
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

  const workdir = makeTempDir('kasl_merge_')
  try {
    for (const rel of ['alice/sub', 'alice/_draft', 'alice/skipme', 'bob', '.github/scripts', '_site']) {
      fs.mkdirSync(path.join(workdir, rel), { recursive: true })
    }

    const write = (rel, content) => {
      fs.writeFileSync(path.join(workdir, rel), content, 'utf8')
    }

    write('alice/A1.cs', cs(GUID_A1, 'A1', 'Alice'))
    write('alice/sub/A2.cs', cs(GUID_A2, 'A2', null)) // 作者回退到文件夹名
    write('alice/_draft/A3.cs', cs(GUID_A3, 'A3', 'Alice')) // 子目录里 _ 开头也要收录
    write('alice/skipme/A4.cs', cs(GUID_A4, 'A4', 'Alice')) // 供 ignore_dirs 测试
    write('bob/B1.cs', cs(GUID_A1, 'B1', 'Bob')) // 与 alice 冲突
    write('bob/B2.cs', cs(GUID_B2, 'B2', 'Bob', '0.0.1', '[1226, 1228]'))
    write('alice/Bad.cs', 'public class NoAttribute { }\n') // 无特性 -> 跳过
    write('.github/scripts/x.cs', cs(GUID_A1, 'ignored'))
    write('_site/y.cs', cs(GUID_A1, 'ignored'))
    write('OnlineRepo.json', '[]') // 根目录产物不参与扫描

    let collected = mergeRepos.collect(workdir, cfg, 'owner/repo', 'main')
    let { records, skipped, fileCounts } = collected
    const rels = records.map(([, rel]) => rel)
    expect('忽略 . 与 _ 开头的第一层目录', rels.every((r) => !r.includes('_site') && !r.includes('.github')))
    expect('递归收集子目录', rels.includes('alice/sub/A2.cs'))
    expect('子目录里 _ 开头的目录也收录', rels.includes('alice/_draft/A3.cs'), show(rels))
    expect('根目录 json 不参与扫描', rels.every((r) => r !== 'OnlineRepo.json'))
    expect('收集到 6 个有效文件', records.length === 6, show(records.length))
    expect('无特性的文件被跳过', skipped.some(([rel]) => rel === 'alice/Bad.cs'), show(skipped))
    expect('按贡献者统计文件数', deepEqual(fileCounts, { alice: 5, bob: 2 }), show(fileCounts))

    const byName = {}
    for (const [, , entry] of records) byName[entry.Name] = entry
    expect('author 缺失时用文件夹名', byName.A2.Author === 'alice', byName.A2.Author)
    expect(
      'DownloadUrl 自动生成',
      byName.A2.DownloadUrl === 'https://raw.githubusercontent.com/owner/repo/main/alice/sub/A2.cs',
      byName.A2.DownloadUrl,
    )
    expect('深层子目录 DownloadUrl 正确', byName.A3.DownloadUrl.endsWith('/alice/_draft/A3.cs'), byName.A3.DownloadUrl)

    const keysA1 = Object.keys(byName.A1)
    expect(
      '字段顺序规范且省略空值',
      deepEqual(
        keysA1,
        mergeRepos.CANONICAL_FIELD_ORDER.filter((k) => keysA1.includes(k)),
      ) &&
        keysA1.every((k) => {
          const value = byName.A1[k]
          return value !== '' && value !== null && !(Array.isArray(value) && !value.length)
        }),
      show(keysA1),
    )
    expect(
      'Repo / Note / UpdateInfo 为空时被省略',
      ['Repo', 'Note', 'UpdateInfo'].every((k) => !(k in byName.A1)),
      show(byName.A1),
    )
    expect('TerritoryIds 正确', deepEqual(byName.B2.TerritoryIds, [1226, 1228]), show(byName.B2.TerritoryIds))

    let mergedResult = mergeRepos.mergeRecords(records)
    let { merged, conflicts, ownerCounts, guidMap } = mergedResult
    expect('Guid 去重（忽略大小写）', merged.length === 5, show(merged.length))
    expect(
      '冲突被记录且先出现的胜出',
      conflicts.length === 1 &&
        merged.some((e) => e.Name === 'A1') &&
        merged.every((e) => e.Name !== 'B1'),
      show(conflicts),
    )
    expect('按贡献者统计收录数', deepEqual(ownerCounts, { alice: 4, bob: 1 }), show(ownerCounts))
    expect('guid_map 指向生效文件', guidMap[GUID_A1.toLowerCase()] === 'alice/A1.cs', show(guidMap))

    // ignore_dirs：任意深度匹配
    const skipCfg = { ...cfg, ignore_dirs: ['skipme'] }
    collected = mergeRepos.collect(workdir, skipCfg, 'owner/repo', 'main')
    const rels2 = collected.records.map(([, rel]) => rel)
    expect('ignore_dirs 排除深层目录', !rels2.includes('alice/skipme/A4.cs'), show(rels2))
    expect('ignore_dirs 生效后数量正确', collected.records.length === 5, show(collected.records.length))

    const outDir = path.join(workdir, '_out')
    const rootOut = path.join(workdir, '_generated', 'OnlineRepo.json')
    const mapOut = path.join(workdir, '_generated', 'guid-map.json')
    const code = quietBuild(workdir, cfg, 'owner/repo', 'main', {
      outDir,
      rootJsonPath: rootOut,
      guidMapPath: mapOut,
    })
    expect('build 正常退出', code === 0, show(code))
    const written = readJson(path.join(outDir, 'index.json'))
    expect('站点 index.json 条目正确', written.length === 5, show(written.length))
    expect('index.html 已生成', fs.existsSync(path.join(outDir, 'index.html')))
    const rootWritten = readJson(rootOut)
    expect('根目录 json 内容一致', deepEqual(rootWritten, written))
    const writtenMap = readJson(mapOut)
    expect('guid-map 文件已写出且内容正确', writtenMap[GUID_B2.toLowerCase()] === 'bob/B2.cs', show(writtenMap))

    // 稳定可复现：再生成一次内容应完全相同（决定「无变化就不提交」是否成立）
    const again = path.join(workdir, '_out2')
    quietBuild(workdir, cfg, 'owner/repo', 'main', { outDir: again })
    expect(
      '重复生成结果一致',
      fs.readFileSync(path.join(again, 'index.json'), 'utf8') ===
        fs.readFileSync(path.join(outDir, 'index.json'), 'utf8'),
    )

    const onlyRoot = path.join(workdir, '_root_only.json')
    quietBuild(workdir, cfg, 'owner/repo', 'main', { rootJsonPath: onlyRoot })
    expect('--no-site 只写根目录 json', fs.existsSync(onlyRoot))

    // 中文与空格路径需要 URL 编码
    write('alice/极佐拉加 绘图.cs', cs(GUID_A2, '中文名', 'Alice'))
    collected = mergeRepos.collect(workdir, cfg, 'owner/repo', 'main')
    const cn = collected.records.map(([, , entry]) => entry).filter((e) => e.DownloadUrl.includes('%'))
    expect(
      '中文路径被百分号编码',
      cn.length > 0 && cn[0].DownloadUrl.includes('%') && !cn[0].DownloadUrl.includes(' '),
      cn.length ? cn[0].DownloadUrl : 'not found',
    )

    expect(
      'strict 模式在跳过时报错',
      quietBuild(workdir, cfg, 'owner/repo', 'main', {
        outDir: path.join(workdir, '_out3'),
        strict: true,
      }) === 1,
    )

    // 第一层目录被 ignore_dirs 排除（例如放模板的 examples/）
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
          ['alice/a.cs'],
        ),
        show(got.records),
      )
    } finally {
      removeDir(first)
    }

    const emptyDir = makeTempDir('kasl_empty_')
    try {
      expect(
        '空仓库默认允许生成并告警',
        quietBuild(emptyDir, cfg, 'owner/repo', 'main', { outDir: path.join(emptyDir, 'out') }) === 0,
      )
      expect(
        '--forbid-empty 时拒绝生成',
        quietBuild(emptyDir, cfg, 'owner/repo', 'main', {
          outDir: path.join(emptyDir, 'out2'),
          forbidEmpty: true,
        }) === 1,
      )
    } finally {
      removeDir(emptyDir)
    }
  } finally {
    removeDir(workdir)
  }

  return failures
}

// --------------------------------------------------------------------------- //
// csharp_meta：真实脚本之外的边界用例
// --------------------------------------------------------------------------- //

/**
 * 用 tests/cases/*.cs 与快照比对，锁住 C# 解析的边界行为。
 *
 * 上面两套检查只覆盖了插值原始字符串等少数几例，而 csharp_meta 真正难的地方——注释里的
 * 假特性、双美元原始插值、const 链与环引用、解析不了的插值原样保留、字符串里含 : [] ,——
 * 都在这里。快照是迁移到 Node 时生成的，当时那份实现已与 Python 版逐字节对照过 96 个文件，
 * 记录的即是已验证的行为；改动 csharp_meta 后跑一遍就能立刻发现回归。
 */
export function runCsharpMetaChecks() {
  const failures = []
  const expect = (name, condition, detail = '') => {
    if (condition) {
      console.log(`✅ ${name}`)
    } else {
      failures.push(name)
      console.log(`❌ ${name} ${detail}`)
    }
  }

  const casesDir = path.join(HERE, 'cases')
  const snapshot = readJson(path.join(HERE, 'cases.snapshot.json'))

  for (const name of Object.keys(snapshot).sort()) {
    const file = path.join(casesDir, name)
    if (!fs.existsSync(file)) {
      expect(name, false, '用例文件缺失')
      continue
    }
    const { params, errors } = csharpMeta.extractScriptType(fs.readFileSync(file, 'utf8'))
    expect(name, deepEqual({ params, errors }, snapshot[name]), `实际 ${show({ params, errors })}`)
  }

  return failures
}

/** 依次跑三套检查，返回进程退出码。 */
export function main() {
  const failures = []
  for (const [title, runner] of [
    ['pr_review', runPrReviewChecks],
    ['merge_repos', runMergeReposChecks],
    ['csharp_meta', runCsharpMetaChecks],
  ]) {
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
