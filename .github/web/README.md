# 在线索引页面（.github/web）

KodakkuAssistScriptLibrary 的 GitHub Pages 展示页。Vue 3 + TypeScript + Vite。

放在 `.github/` 下有两个原因：一是它和 `merge_repos.mjs` 一样属于维护者自己的东西，不是贡献者目录；
二是 `merge_repos.mjs` 的扫描器会把仓库第一层所有不以 `.` / `_` 开头的目录当成贡献者目录递归进去找
`.cs`，放在点开头的目录里可以从结构上避开，不需要额外配置。

## 开发

```bash
cd .github/web
npm install
npm run dev
```

打开 http://localhost:5173/KodakkuAssistScriptLibrary/ —— `base` 设了子路径，少了这一段会 404。

`npm run dev` 之前会自动跑一次 `sync-index`，把仓库根目录已提交的 `OnlineRepo.json` 复制成
`public/index.json`，用来顶替线上由 CI 生成的索引。想用最新数据，先跑一遍：

```bash
node .github/scripts/merge_repos.mjs --out ../../_site --no-html
cp ../../_site/index.json public/index.json
```

## 构建与部署

`npm run build` 会清理并写入仓库根目录的 `_site/`，CI 里接着由
`merge_repos.mjs --out _site --no-html` 补上机器可读的 `index.json`，两者一起作为 Pages 产物上传。

**顺序不能反**：vite 会清空 `_site`，必须让它先跑，否则 `index.json` 会被删掉。也正因为这个
`emptyOutDir` 是显式打开的（`outDir` 在项目根之外，Vite 默认不允许清空）。

## 页面数据

页面从 `${base}index.json` 取数据，也就是 merge_repos 生成的合并索引。字段见 `src/types.ts`。
索引里没有贡献者字段，贡献者目录是从 `DownloadUrl` 反推出的（协议与 owner/repo/branch 之后的第一段）。

`index.json` 是插件侧也在用的对外格式，**改它之前先确认不会影响 KodakkuAssist 的解析**。
