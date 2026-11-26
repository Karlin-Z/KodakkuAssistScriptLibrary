# AGENT.md

本文件面向 AI agent（辅助写脚本、发 PR）。只列规范与环境入口，不是教程；具体写法与流程细节见下。

## 文档分工

- README.md：贡献流程与审核规则总纲（审核细节看这里）
- script-writing-guide.md：脚本 API 与写法指南（具体怎么写脚本看这里）
- AGENT.md：本文件，agent 的规则与环境入口

## 仓库定位

- 脚本由 KodakkuAssist 插件运行时编译分发，本库只做静态校验、不构建。
- csproj 仅服务本地 IDE 开发，不是构建入口。

## 仓库结构与文件放置

- 顶层一级目录 = 脚本作者的 GitHub 用户名（不区分大小写）。
- 每个作者目录根下必须有一个与目录同名的 `<用户名>.csproj`，内容全仓库统一，直接复制模板改名，禁止修改内容：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>KodakkuScripts.$(MSBuildProjectName)</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$(MSBuildProjectDirectory)\**\*.cs" Exclude="bin\**;obj\**" />
  </ItemGroup>
</Project>
```

- 子目录结构随意（按资料片/副本分目录均可），审核只约束第一层。
- 脚本 .cs 文件名自由（可中文/括号/空格）；元数据全部写在 .cs 顶部的 `[ScriptType]` 特性里，没有额外清单文件。

## 硬性规范（违反即拒/阻塞）

### 单文件脚本

- 第一层路径必须 = PR 作者的用户名；只允许 .cs 和与作者目录同名的 .csproj。
- 主文件恰好一处 `[ScriptType]`；part 文件不许写 ScriptType；同一 partial 类出现两处 ScriptType 拒绝；孤立 part（无主文件）拒绝。
- guid 非空且全局唯一，与任何已有脚本及同 PR 其它文件不冲突；重命名自己的文件时 guid 不变则放行。建议标准 UUID。
- 更新脚本时 guid 不变、递增 version（按 NuGetVersion 解析需合法）并更新 updateInfo。
- territorys 为 0~4294967295 的 uint 数组。
- name/author 必须能安全用作文件名（插件按 Name_Author.cs 保存）。
- UTF-8 编码；禁符号链接/子模块；单文件 ≤2MB；PR ≤100 文件。

### 多文件 partial 脚本

- 所有文件放同一目录、同一命名空间、同一个 public partial class；跨目录不会合并。
- 只有主文件写 `[ScriptType]`；version/guid/updateInfo 只写主文件。
- 每个 part 文件的 using 必须写全。
- part 命名惯例如 `主文件.P1.cs`（不强制，按同目录同名 partial 类聚合）。

`[ScriptType]` 参数速览（详见 guide）：guid（必填）、name、territorys、version、author、note、updateInfo；note/updateInfo 可引用本文件 const。

## 禁改与不入库

- 禁止提交（会被直接拒绝）：OnlineRepo.json、.github/index/guid-map.json（CI 自动生成）、.github/ 目录、根目录其它文件。
- 不入库（gitignore）：local.props、bin/、obj/、_site/、node_modules/。
- 入库：作者目录的 .cs、同名 csproj、文档、.github。

## 开发环境创建

1. 装 .NET 10 SDK。
2. 装可达鸭（KodakkuAssist）插件与 Dalamud。
3. 用 VS Code（C# Dev Kit）或 Rider 打开仓库根目录，即得类型提示/跳转/错误检查。

- Directory.Build.props 已配好公共配置（net10.0-windows、LangVersion 12、AllowUnsafeBlocks、Nullable disable、UseWindowsForms），KodakkuAssist.dll 按以下顺序探测：
  1. local.props 的 KaPluginDir
  2. `%APPDATA%\XIVLauncher(CN)\installedPlugins\KodakkuAssist\*\`
  3. 同级仓库 `..\KodakkuAssist\KodakkuAssist\bin\Debug\net10.0-windows\`
  4. 本目录 refs\
- Dalamud 从 `%APPDATA%\XIVLauncher(CN)\addon\Hooks\dev\` 探测；都找不到会报 MSBuild 错误。
- 本机路径写在 local.props（不入库），示例：

```xml
<Project>
  <PropertyGroup>
    <KaPluginDir>D:\game\FF14\卫月\Plugins\KodakkuAssist\KodakkuAssist\bin\Debug\net10.0-windows\</KaPluginDir>
  </PropertyGroup>
</Project>
```

- 同目录放 KodakkuAssist.xml 可获得 XML 注释智能提示。
- 正常分发不需要构建，脚本由插件运行时编译；csproj 只为 IDE 智能提示。

## 游戏内调试与提交前自检

插件内置 MCP（默认 `http://127.0.0.1:18778/mcp`，插件设置→IPC 勾选启用），常用工具：

- compile_script：用插件同一编译器编译
- lint_script：静态查 guid 冲突/签名/async void 等静默失效
- new_script_template：生成带新 guid 的骨架
- reload_script：热重载
- get_event_schema、get_script_log 等

提交前自检：

- 至少本地 compile/lint 一次，或游戏内加载一次。
- 校验命令：`node .github/scripts/pr_review.ts --owner <用户名> --check-cs <文件>.cs`
