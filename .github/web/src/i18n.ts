import { createI18n } from 'vue-i18n'
import en from './locales/en'
import ja from './locales/ja'
import zhCN from './locales/zh-CN'
import zhTW from './locales/zh-TW'

/**
 * 站点支持的语言。`intl` 交给 Intl.DateTimeFormat —— 语言的写作习惯和地区不是一回事
 * （zh 用 2026/09/12、en 用 09/12/2026），所以由这里指定而不是直接拿 locale 当地区码。
 * `short` 是切换按钮在窄屏上显示的缩写。
 */
export const LOCALES = [
  { code: 'zh-CN', label: '简体中文', short: '简', intl: 'zh-CN' },
  { code: 'zh-TW', label: '繁體中文', short: '繁', intl: 'zh-TW' },
  { code: 'en', label: 'English', short: 'EN', intl: 'en-US' },
  { code: 'ja', label: '日本語', short: 'JA', intl: 'ja-JP' },
] as const

export type LocaleCode = (typeof LOCALES)[number]['code']

/** 源语言：文案缺项时的兜底，也是 URL 上不写 ?lang= 的那一种。 */
const DEFAULT_LOCALE: LocaleCode = 'zh-CN'

const STORAGE_KEY = 'kodakku-locale'

function isLocaleCode(value: unknown): value is LocaleCode {
  return LOCALES.some((item) => item.code === value)
}

/** 语言的地区码，给 Intl 用；找不到时退回默认语言的地区码。 */
export function intlLocale(code: string): string {
  return LOCALES.find((item) => item.code === code)?.intl ?? DEFAULT_LOCALE
}

/**
 * localStorage 在隐私模式、禁用 Cookie 时会直接抛错，读写都得兜住：
 * 最坏情况是记不住语言偏好，不该让整页挂掉。
 */
function readStoredLocale(): string | null {
  try {
    return window.localStorage.getItem(STORAGE_KEY)
  } catch {
    return null
  }
}

function storeLocale(code: LocaleCode): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, code)
  } catch {
    // 存不下就算了，本次访问仍然按选中的语言显示
  }
}

/**
 * 初始语言：URL 上的 `?lang=`（分享链接能带上语言）> 上次的选择 > 浏览器语言。
 * 浏览器语言只做前缀匹配，中文再按地区分简繁：只有明确的港台澳门才给繁体，
 * `zh`、`zh-Hans`、`zh-SG` 这些都按简体。
 */
function detectLocale(): LocaleCode {
  const fromUrl = new URLSearchParams(window.location.search).get('lang')
  if (isLocaleCode(fromUrl)) return fromUrl

  const stored = readStoredLocale()
  if (isLocaleCode(stored)) return stored

  for (const tag of navigator.languages ?? [navigator.language]) {
    const lower = tag.toLowerCase()
    if (lower.startsWith('zh')) return /hant|tw|hk|mo/.test(lower) ? 'zh-TW' : 'zh-CN'
    if (lower.startsWith('ja')) return 'ja'
    if (lower.startsWith('en')) return 'en'
  }
  return DEFAULT_LOCALE
}

export const i18n = createI18n({
  // 组合式 API：模板里用 `t()` 而不是 `$t()`，语言切换才能被响应式追踪。
  legacy: false,
  locale: detectLocale(),
  fallbackLocale: DEFAULT_LOCALE,
  messages: { 'zh-CN': zhCN, 'zh-TW': zhTW, en, ja },
})

/**
 * 切换语言：写回 i18n、记进 localStorage，并把 `?lang=` 同步到地址栏
 * （默认语言不带参数，保持首页地址干净）。用 replaceState 而不是 pushState，
 * 免得切一次语言就往历史里塞一条。
 */
export function setLocale(code: LocaleCode): void {
  i18n.global.locale.value = code
  storeLocale(code)

  const url = new URL(window.location.href)
  if (code === DEFAULT_LOCALE) url.searchParams.delete('lang')
  else url.searchParams.set('lang', code)
  window.history.replaceState(null, '', url)
}
