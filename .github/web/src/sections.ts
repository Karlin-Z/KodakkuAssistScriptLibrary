import { onMounted, onUnmounted, ref } from 'vue'

/**
 * 板块锚点。整站是一页从上往下滚，板块顺序就是这里的顺序。
 * 主页（`#top`）不进菜单 —— 顶栏图标点的就是它。
 *
 * 这里只留 id：名字是文案，跟着语言走，用的时候取 `nav.<id>`。
 */
export const NAV = [{ id: 'examples' }, { id: 'start' }, { id: 'contribute' }] as const

export type SectionId = (typeof NAV)[number]['id']

/** 页面顶端（主页）的锚点，顶栏图标用它回去。 */
export const TOP_ID = 'top'

/** 当前停在哪个板块；还在主页范围（没滚到任何板块）时为 null。 */
export const activeSection = ref<SectionId | null>(null)

/**
 * 滚动高亮。逐个比较板块顶部与各自的「锚点落地线」，取最后一个已经越过的板块 ——
 * 比 IntersectionObserver 好懂，而且往回滚到顶时会正确地清空（也就是回到主页）。
 *
 * 落地线取元素自己的 scroll-margin-top（见 style.css 里的 `[id]` 规则），不另写常量：
 * 点导航后板块顶部正好停在那个位置，判定线必须和它一样大，「刚落地」的那一节才算当前板块。
 * 之前这里写死成头部高度，比 scroll-margin-top 小，高亮就会整体差一节。
 */
export function useActiveSection(): void {
  const margins: Partial<Record<SectionId, number>> = {}

  /** 留一点容差，免得亚像素布局下「刚好落地」的那一节被判在门外。 */
  const TOLERANCE = 4

  function update(): void {
    let current: SectionId | null = null
    for (const item of NAV) {
      const el = document.getElementById(item.id)
      if (!el) continue
      if (el.getBoundingClientRect().top <= (margins[item.id] ?? 0) + TOLERANCE) current = item.id
    }
    activeSection.value = current
  }

  function measure(): void {
    for (const item of NAV) {
      const el = document.getElementById(item.id)
      // scroll-margin-top 只在断点变化时可能不同，量一次缓存起来，滚动回调里就不用反复取计算样式
      margins[item.id] = el ? Number.parseFloat(getComputedStyle(el).scrollMarginTop) || 0 : 0
    }
  }

  function onResize(): void {
    measure()
    update()
  }

  onMounted(() => {
    measure()
    update()
    window.addEventListener('scroll', update, { passive: true })
    window.addEventListener('resize', onResize)
  })

  onUnmounted(() => {
    window.removeEventListener('scroll', update)
    window.removeEventListener('resize', onResize)
  })
}
