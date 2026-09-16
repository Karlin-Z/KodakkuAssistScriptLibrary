import { ref } from 'vue'

export type CopyState = 'idle' | 'copied' | 'failed'

const HOLD_MS = 2000

/**
 * 复制到剪贴板，并把结果保留两秒给按钮显示。
 * clipboard API 在非 HTTPS 或没授权的环境会直接抛错，这种情况退回「复制失败」，
 * 让用户手动选中文本，不静默失败。
 */
export function useCopy() {
  const state = ref<CopyState>('idle')
  let timer: number | undefined

  async function copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text)
      state.value = 'copied'
    } catch {
      state.value = 'failed'
    }
    window.clearTimeout(timer)
    timer = window.setTimeout(() => {
      state.value = 'idle'
    }, HOLD_MS)
  }

  return { state, copy }
}
