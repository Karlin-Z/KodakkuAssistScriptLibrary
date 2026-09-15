import { ref, watchEffect } from 'vue'

export type Theme = 'auto' | 'light' | 'dark'

const STORAGE_KEY = 'kasl-theme'
const saved = localStorage.getItem(STORAGE_KEY)
const isTheme = (value: string | null): value is Theme =>
  value === 'auto' || value === 'light' || value === 'dark'

/** 主题状态；auto 表示跟随系统。 */
export const theme = ref<Theme>(isTheme(saved) ? saved : 'auto')

watchEffect(() => {
  localStorage.setItem(STORAGE_KEY, theme.value)
  const root = document.documentElement
  if (theme.value === 'auto') {
    root.removeAttribute('data-theme')
  } else {
    root.setAttribute('data-theme', theme.value)
  }
})
