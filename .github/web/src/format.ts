const MINUTE = 60_000
const HOUR = 60 * MINUTE
const DAY = 24 * HOUR

const dateFormatter = new Intl.DateTimeFormat('zh-CN', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

/** `2026-09-12T05:46:17Z` -> 时间戳；缺失或不合法时返回 0。 */
export function parseTime(value: string | undefined): number {
  if (!value) return 0
  const ts = Date.parse(value)
  return Number.isNaN(ts) ? 0 : ts
}

export function formatDate(ts: number): string {
  return ts ? dateFormatter.format(new Date(ts)) : '未知'
}

/** 相对时间，超过 30 天就退回具体日期，避免出现「243 天前」这种没信息量的说法。 */
export function formatRelative(ts: number): string {
  if (!ts) return '未知'
  const diff = Date.now() - ts
  if (diff < 0) return formatDate(ts)
  if (diff < MINUTE) return '刚刚'
  if (diff < HOUR) return `${Math.floor(diff / MINUTE)} 分钟前`
  if (diff < DAY) return `${Math.floor(diff / HOUR)} 小时前`
  if (diff < 30 * DAY) return `${Math.floor(diff / DAY)} 天前`
  return formatDate(ts)
}
