import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { intlLocale } from './i18n'

const MINUTE = 60_000
const HOUR = 60 * MINUTE
const DAY = 24 * HOUR

/** 超过这个跨度就退回具体日期，避免出现「243 天前」这种没信息量的说法。 */
const RELATIVE_LIMIT = 30 * DAY

/**
 * 日期展示跟着当前语言走。日期格式交给 Intl（各语言的年月日顺序不同），
 * 相对时间交给文案（各语言的量词与复数规则不同，英文就在词条里写单复数两式）。
 *
 * `t` 与 `locale` 都是响应式的，所以在模板或 computed 里调用这两个函数，
 * 切换语言后会跟着重新求值 —— 不需要额外通知模板。
 */
export function useFormat() {
  const { t, locale } = useI18n()

  const dateFormatter = computed(
    () =>
      new Intl.DateTimeFormat(intlLocale(locale.value), {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
      }),
  )

  function formatDate(ts: number): string {
    return ts ? dateFormatter.value.format(new Date(ts)) : t('common.unknown')
  }

  function formatRelative(ts: number): string {
    if (!ts) return t('common.unknown')
    const diff = Date.now() - ts
    // 时间戳在将来（时钟偏差、数据写错）时按日期显示，别算出「-3 小时前」
    if (diff < 0) return formatDate(ts)
    if (diff < MINUTE) return t('time.justNow')
    if (diff < HOUR) {
      const minutes = Math.floor(diff / MINUTE)
      return t('time.minutesAgo', { n: minutes }, minutes)
    }
    if (diff < DAY) {
      const hours = Math.floor(diff / HOUR)
      return t('time.hoursAgo', { n: hours }, hours)
    }
    if (diff < RELATIVE_LIMIT) {
      const days = Math.floor(diff / DAY)
      return t('time.daysAgo', { n: days }, days)
    }
    return formatDate(ts)
  }

  return { formatDate, formatRelative }
}
