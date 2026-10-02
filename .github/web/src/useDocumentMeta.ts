import { watchEffect } from 'vue'
import { useI18n } from 'vue-i18n'

/**
 * 标题、简介和 `<html lang>` 跟着语言一起换。
 *
 * index.html 里写死的是简体中文，那份只在脚本跑起来之前算数 ——
 * 首屏本来就是空 div，用户看不到错的文案，标签页标题会闪一下而已。
 * 搜索引擎抓到的也是简体中文那份（静态托管没有按语言分路由，这是已知取舍）。
 */
export function useDocumentMeta(): void {
  const { t, locale } = useI18n()

  watchEffect(() => {
    document.documentElement.lang = locale.value
    document.title = t('meta.title')
    document.querySelector('meta[name="description"]')?.setAttribute('content', t('meta.description'))
  })
}
