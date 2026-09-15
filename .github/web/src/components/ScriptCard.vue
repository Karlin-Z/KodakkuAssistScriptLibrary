<script setup lang="ts">
import { computed, ref } from 'vue'
import { formatDate, formatRelative } from '../format'
import type { ScriptRow } from '../types'

/** 超过这个数量的地图标签折叠成 `+N`，避免个别脚本把一整行塞满。 */
const MAX_TERRITORIES = 8

const props = defineProps<{ row: ScriptRow }>()

const territories = computed(() => props.row.TerritoryIds ?? [])
const shownTerritories = computed(() => territories.value.slice(0, MAX_TERRITORIES))
const hiddenTerritories = computed(() => Math.max(0, territories.value.length - MAX_TERRITORIES))

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
  <article class="card">
    <header class="card__head">
      <h2 class="card__name">{{ row.Name }}</h2>
      <span class="badge">v{{ row.Version }}</span>
    </header>

    <p class="card__meta">
      <span>{{ row.Author }}</span>
      <span aria-hidden="true">·</span>
      <span>{{ row.contributor }}</span>
      <span aria-hidden="true">·</span>
      <span :title="formatDate(row.updatedAt)">更新于 {{ formatRelative(row.updatedAt) }}</span>
    </p>

    <p class="card__territories">
      <template v-if="territories.length">
        <span v-for="id in shownTerritories" :key="id" class="badge">地图 {{ id }}</span>
        <span v-if="hiddenTerritories" class="badge" :title="territories.join('、')">
          +{{ hiddenTerritories }}
        </span>
      </template>
      <span v-else class="badge">不限地图</span>
    </p>

    <p v-if="row.Note" class="card__note">{{ row.Note }}</p>

    <div v-if="row.UpdateInfo" class="card__update">
      <span class="card__update-label">更新说明</span>
      <span>{{ row.UpdateInfo }}</span>
    </div>

    <footer class="card__foot">
      <code class="card__path" :title="row.repoPath">{{ row.repoPath }}</code>
      <div class="card__actions">
        <button class="btn btn--ghost" type="button" @click="copyGuid">
          {{ copied ? '已复制' : '复制 GUID' }}
        </button>
        <button class="btn" type="button" :disabled="downloading" @click="download">
          {{ downloading ? '下载中…' : '下载 .cs' }}
        </button>
      </div>
    </footer>
  </article>
</template>

<style scoped>
.card {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 18px 20px;
  background: var(--bg-elev);
  border: 1px solid var(--border-soft);
  border-radius: var(--radius);
  box-shadow: var(--shadow-sm);
  transition: border-color 0.15s ease, box-shadow 0.15s ease, transform 0.15s ease;
}

.card:hover {
  border-color: var(--border);
  box-shadow: var(--shadow-md);
  transform: translateY(-1px);
}

.card__head {
  display: flex;
  align-items: baseline;
  gap: 10px;
  flex-wrap: wrap;
}

.card__name {
  margin: 0;
  font-size: 17px;
  font-weight: 600;
  line-height: 1.4;
  overflow-wrap: anywhere;
}

.card__meta {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  margin: 0;
  color: var(--text-muted);
  font-size: 13px;
}

.card__territories {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
  margin: 0;
}

.card__note {
  margin: 0;
  color: var(--text);
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}

.card__update {
  display: flex;
  gap: 10px;
  padding: 10px 12px;
  background: var(--accent-soft);
  border-radius: var(--radius-sm);
  font-size: 14px;
}

.card__update-label {
  flex: none;
  color: var(--accent);
  font-weight: 600;
}

.card__foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
  margin-top: 2px;
  padding-top: 12px;
  border-top: 1px solid var(--border-soft);
}

.card__path {
  min-width: 0;
  color: var(--text-muted);
  font-family: ui-monospace, SFMono-Regular, 'Cascadia Code', Consolas, monospace;
  font-size: 12px;
  overflow-wrap: anywhere;
}

.card__actions {
  display: flex;
  gap: 8px;
  flex: none;
}

.card__actions .btn {
  height: 32px;
  padding: 0 12px;
  font-size: 13px;
}

.card__actions .btn:disabled {
  cursor: progress;
  opacity: 0.6;
}
</style>
