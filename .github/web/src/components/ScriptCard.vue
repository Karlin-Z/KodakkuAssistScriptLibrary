<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import AppBadge from './AppBadge.vue'
import AppButton from './AppButton.vue'
import type { ScriptRow } from '../types'
import { useFormat } from '../useFormat'

/** 超过这个数量的地图标签折叠成 `+N`，避免个别脚本把一整行塞满。 */
const MAX_TERRITORIES = 8

const props = defineProps<{ row: ScriptRow }>()

const { t } = useI18n()
const { formatDate, formatRelative } = useFormat()

const territories = computed(() => props.row.TerritoryIds ?? [])
const shownTerritories = computed(() => territories.value.slice(0, MAX_TERRITORIES))
const hiddenTerritories = computed(() => Math.max(0, territories.value.length - MAX_TERRITORIES))

/** 折叠起来的那些地图 id 挂在 title 上，连接符按语言走（中文顿号、英文逗号）。 */
const hiddenTitle = computed(() => territories.value.join(t('common.listSeparator')))

const copied = ref(false)
let copyTimer: number | undefined

async function copyGuid(): Promise<void> {
  try {
    await navigator.clipboard.writeText(props.row.Guid)
  } catch {
    return
  }
  copied.value = true
  window.clearTimeout(copyTimer)
  copyTimer = window.setTimeout(() => {
    copied.value = false
  }, 1500)
}

const downloading = ref(false)

/**
 * raw 直链是跨域的，`<a download>` 在跨域时不生效（只会打开源码页），
 * 所以自己取回字节再触发保存，顺便用插件约定的 `Name_Author.cs` 命名。
 */
async function download(): Promise<void> {
  if (downloading.value) return
  downloading.value = true
  try {
    const response = await fetch(props.row.DownloadUrl)
    if (!response.ok) throw new Error(`HTTP ${response.status}`)
    const blobUrl = URL.createObjectURL(await response.blob())
    const link = document.createElement('a')
    link.href = blobUrl
    link.download = `${props.row.Name}_${props.row.Author}.cs`
    link.click()
    URL.revokeObjectURL(blobUrl)
  } catch {
    window.open(props.row.DownloadUrl, '_blank', 'noopener')
  } finally {
    downloading.value = false
  }
}
</script>

<template>
  <article
    class="flex flex-col gap-2.5 rounded-xl border border-line-soft bg-surface px-5 py-4.5 shadow-sm transition hover:-translate-y-px hover:border-line hover:shadow-md"
  >
    <header class="flex flex-wrap items-baseline gap-2.5">
      <h2 class="text-[17px] leading-[1.4] font-semibold wrap-anywhere">{{ row.Name }}</h2>
      <AppBadge>v{{ row.Version }}</AppBadge>
    </header>

    <p class="flex flex-wrap items-center gap-2 text-[13px] text-fg-muted">
      <span>{{ row.Author }}</span>
      <span aria-hidden="true">·</span>
      <span>{{ row.contributor }}</span>
      <span aria-hidden="true">·</span>
      <span :title="formatDate(row.updatedAt)">
        {{ t('card.updated', { time: formatRelative(row.updatedAt) }) }}
      </span>
    </p>

    <p class="flex flex-wrap gap-1.5">
      <template v-if="territories.length">
        <AppBadge v-for="id in shownTerritories" :key="id">{{ t('card.map', { id }) }}</AppBadge>
        <AppBadge v-if="hiddenTerritories" :title="hiddenTitle">+{{ hiddenTerritories }}</AppBadge>
      </template>
      <AppBadge v-else>{{ t('card.anyTerritory') }}</AppBadge>
    </p>

    <p v-if="row.Note" class="whitespace-pre-wrap text-fg wrap-anywhere">{{ row.Note }}</p>

    <div v-if="row.UpdateInfo" class="flex gap-2.5 rounded-lg bg-accent-soft px-3 py-2.5 text-sm">
      <span class="flex-none font-semibold text-accent">{{ t('card.updateNote') }}</span>
      <span>{{ row.UpdateInfo }}</span>
    </div>

    <footer
      class="mt-0.5 flex flex-wrap items-center justify-between gap-3 border-t border-line-soft pt-3"
    >
      <code class="min-w-0 font-mono text-xs text-fg-muted wrap-anywhere" :title="row.repoPath">{{
        row.repoPath
      }}</code>
      <div class="flex flex-none gap-2">
        <AppButton variant="ghost" size="sm" @click="copyGuid">
          {{ copied ? t('common.copied') : t('card.copyGuid') }}
        </AppButton>
        <AppButton size="sm" :disabled="downloading" @click="download">
          {{ downloading ? t('card.downloading') : t('card.download') }}
        </AppButton>
      </div>
    </footer>
  </article>
</template>
