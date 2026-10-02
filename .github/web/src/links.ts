const OWNER_REPO = 'Karlin-Z/KodakkuAssistScriptLibrary'
const GITHUB = `https://github.com/${OWNER_REPO}`
const RAW = `https://raw.githubusercontent.com/${OWNER_REPO}/main`

/** 插件本体单独一个仓库发布，脚本库不在这里。 */
const PLUGIN_REPO = 'Karlin-Z/DalamudPlugins'

/** 站点基路径（以 / 结尾）；部署在项目页子路径下时，用来拼站内资源地址。 */
export const baseUrl = import.meta.env.BASE_URL

/** 站外链接集中在这里，改仓库地址或邀请链接时只动这一处。 */
export const links = {
  repo: GITHUB,
  readme: `${GITHUB}/blob/main/README.md`,
  guide: `${GITHUB}/blob/main/script-writing-guide.md`,
  sample: `${GITHUB}/blob/main/SimpleScript.cs`,
  /** 可达鸭的 Discord：脚本反馈与开发讨论都在这。 */
  discord: 'https://discord.gg/HwmmEBXXz3',
  /**
   * 卫月「在线插件库」的订阅地址。填进插件库设置里才能搜到并安装可达鸭本体，
   * 和下面的脚本库订阅地址不是一回事。
   */
  pluginLibrary: `https://raw.githubusercontent.com/${PLUGIN_REPO}/main/pluginmaster.json`,
  /**
   * 插件的在线库订阅地址。仓库根目录的 OnlineRepo.json 由 CI 生成并提交，
   * raw 直链永远是最新的一份，插件按它比对版本号。
   */
  subscription: `${RAW}/OnlineRepo.json`,
} as const

/** 站点上的同一份索引，可以直接在浏览器里打开看。 */
export const siteIndexPath = `${baseUrl}index.json`
