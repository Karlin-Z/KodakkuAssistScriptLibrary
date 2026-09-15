<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import ScriptCard from './components/ScriptCard.vue'
import { formatDate } from './format'
import { loadIndex } from './index-data'
import { theme, type Theme } from './useTheme'
import type { ScriptRow, SortKey } from './types'

const SORT_KEYS: SortKey[] = ['updated', 'name', 'author', 'contributor']
const THEME_LABEL: Record<Theme, string> = {
  auto: '跟随系统',
  light: '浅色',
  dark: '深色',
}
const THEME_ORDER: Theme[] = ['auto', 'light', 'dark']

const collator = new Intl.Collator('zh-Hans-CN')

/** 站点基路径（以 / 结尾），部署在项目页子路径下时用来拼 index.json 的地址。 */
const baseUrl = import.meta.env.BASE_URL

const rows = ref<ScriptRow[]>([])
const loading = ref(true)
const error = ref('')

const query = ref('')
const contributor = ref('')
const sortKey = ref<SortKey>('updated')

const isSortKey = (value: string): value is SortKey => (SORT_KEYS as string[]).includes(value)

async function refresh(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    rows.value = await loadIndex()
  } catch (err) {
    error.value = err instanceof Error ? err.message : String(err)
    rows.value = []
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const contributors = computed(() => {
  const counts = new Map<string, number>()
  for (const row of rows.value) {
    counts.set(row.contributor, (counts.get(row.contributor) ?? 0) + 1)
  }
  return [...counts.entries()]
    .map(([name, count]) => ({ name, count }))
    .sort((a, b) => b.count - a.count || collator.compare(a.name, b.name))
})

const lastUpdated = computed(() =>
  rows.value.reduce((max, row) => Math.max(max, row.updatedAt), 0),
)

function haystack(row: ScriptRow): string {
  return [
    row.Name,
    row.Author,
    row.contributor,
    row.repoPath,
    row.Guid,
    row.Note,
    row.UpdateInfo,
    ...(row.TerritoryIds ?? []).map(String),
  ]
    .join('\n')
    .toLowerCase()
}

function comparator(key: SortKey) {
  switch (key) {
    case 'updated':
      return (a: ScriptRow, b: ScriptRow) =>
        b.updatedAt - a.updatedAt || collator.compare(a.Name, b.Name)
    case 'name':
      return (a: ScriptRow, b: ScriptRow) => collator.compare(a.Name, b.Name)
    case 'author':
      return (a: ScriptRow, b: ScriptRow) =>
        collator.compare(a.Author, b.Author) || collator.compare(a.Name, b.Name)
    case 'contributor':
      return (a: ScriptRow, b: ScriptRow) =>
        collator.compare(a.contributor, b.contributor) || collator.compare(a.Name, b.Name)
  }
}

const visible = computed(() => {
  const keyword = query.value.trim().toLowerCase()
  const filtered = rows.value.filter((row) => {
    if (contributor.value && row.contributor !== contributor.value) return false
    return !keyword || haystack(row).includes(keyword)
  })
  return filtered.sort(comparator(sortKey.value))
})

const filtering = computed(() => query.value.trim() !== '' || contributor.value !== '')

function resetFilters(): void {
  query.value = ''
  contributor.value = ''
}

function pickContributor(name: string): void {
  contributor.value = contributor.value === name ? '' : name
}

function onSortChange(event: Event): void {
  const value = (event.target as HTMLSelectElement).value
  if (isSortKey(value)) sortKey.value = value
}

function cycleTheme(): void {
  const next = (THEME_ORDER.indexOf(theme.value) + 1) % THEME_ORDER.length
  theme.value = THEME_ORDER[next] ?? 'auto'
}
</script>

<template>
  <div class="page">
    <header class="hero">
      <div>
        <h1 class="hero__title">KodakkuAssistScriptLibrary</h1>
        <p class="hero__sub">
          FF14 卫月「可达鸭」插件的官方在线脚本库，共
          <strong>{{ rows.length }}</strong> 个脚本，来自
          <strong>{{ contributors.length }}</strong> 位贡献者<template v-if="lastUpdated"
            >，最近更新 {{ formatDate(lastUpdated) }}</template
          >。
        </p>
      </div>
      <button
        class="btn btn--ghost"
        type="button"
        :title="`当前主题：${THEME_LABEL[theme]}，点击切换`"
        @click="cycleTheme"
      >
        {{ THEME_LABEL[theme] }}
      </button>
    </header>

    <div class="toolbar">
      <label class="field field--search">
        <span class="sr-only">搜索脚本</span>
        <svg
          class="field__icon"
          width="16"
          height="16"
          viewBox="0 0 16 16"
          fill="none"
          aria-hidden="true"
        >
          <circle cx="7" cy="7" r="4.5" stroke="currentColor" stroke-width="1.5" />
          <path d="M10.5 10.5 14 14" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
        </svg>
        <input v-model="query" type="search" placeholder="搜索脚本名、作者、备注、GUID…" />
      </label>

      <label class="field">
        <span class="sr-only">排序方式</span>
        <select :value="sortKey" @change="onSortChange">
          <option value="updated">按更新时间</option>
          <option value="name">按名称</option>
          <option value="author">按作者</option>
          <option value="contributor">按贡献者</option>
        </select>
      </label>
    </div>

    <div v-if="contributors.length" class="chips">
      <button
        class="chip"
        type="button"
        :class="{ 'chip--on': contributor === '' }"
        @click="contributor = ''"
      >
        全部 <span class="chip__count">{{ rows.length }}</span>
      </button>
      <button
        v-for="item in contributors"
        :key="item.name"
        class="chip"
        type="button"
        :class="{ 'chip--on': contributor === item.name }"
        @click="pickContributor(item.name)"
      >
        {{ item.name }} <span class="chip__count">{{ item.count }}</span>
      </button>
    </div>

    <p v-if="!loading && !error" class="result-line">
      <template v-if="filtering">
        筛选出 <strong>{{ visible.length }}</strong> / {{ rows.length }} 个脚本
        <button class="btn btn--ghost result-line__reset" type="button" @click="resetFilters">
          清除筛选
        </button>
      </template>
      <template v-else>共 {{ rows.length }} 个脚本</template>
    </p>

    <p v-if="loading" class="state">正在载入索引…</p>

    <div v-else-if="error" class="state state--error">
      <p>{{ error }}</p>
      <p class="state__hint">
        索引文件 <code>index.json</code> 没取到。本地开发请先执行
        <code>npm run sync-index</code>。
      </p>
      <button class="btn" type="button" @click="refresh">重试</button>
    </div>

    <p v-else-if="visible.length === 0" class="state">
      没有匹配的脚本<template v-if="filtering">，换个关键词试试</template>。
    </p>

    <div v-else class="list">
      <ScriptCard v-for="row in visible" :key="row.Guid" :row="row" />
    </div>

    <footer class="foot">
      <p>
        想收录自己的脚本？把 <code>.cs</code> 放进以你的 GitHub 用户名命名的目录，然后提 PR。
        详见
        <a
          href="https://github.com/Karlin-Z/KodakkuAssistScriptLibrary/blob/main/README.md"
          target="_blank"
          rel="noopener"
          >贡献说明</a
        >。
      </p>
      <p class="foot__muted">
        本页数据来自
        <a :href="`${baseUrl}index.json`" target="_blank" rel="noopener">index.json</a>。
      </p>
    </footer>
  </div>
</template>

<style scoped>
.page {
  max-width: 960px;
  margin: 0 auto;
  padding: 40px 20px 64px;
}

.hero {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 24px;
}

.hero__title {
  margin: 0 0 6px;
  font-size: 26px;
  font-weight: 700;
  letter-spacing: -0.01em;
}

.hero__sub {
  margin: 0;
  color: var(--text-muted);
}

.hero__sub strong {
  color: var(--text);
  font-variant-numeric: tabular-nums;
}

.toolbar {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
  margin-bottom: 12px;
}

.field--search {
  flex: 1 1 280px;
}

.chips {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-bottom: 20px;
}

.chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 5px 12px;
  background: var(--bg-elev);
  border: 1px solid var(--border);
  border-radius: 999px;
  cursor: pointer;
  font-size: 13px;
  transition: background 0.15s ease, border-color 0.15s ease, color 0.15s ease;
}

.chip:hover {
  background: var(--bg-subtle);
}

.chip--on {
  background: var(--accent-soft);
  border-color: color-mix(in srgb, var(--accent) 45%, transparent);
  color: var(--accent);
  font-weight: 600;
}

.chip__count {
  color: var(--text-muted);
  font-variant-numeric: tabular-nums;
}

.chip--on .chip__count {
  color: inherit;
  opacity: 0.75;
}

.result-line {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0 0 14px;
  color: var(--text-muted);
  font-size: 13px;
}

.result-line strong {
  color: var(--text);
  font-variant-numeric: tabular-nums;
}

.result-line__reset {
  height: 26px;
  padding: 0 8px;
  font-size: 13px;
}

.state {
  padding: 48px 0;
  color: var(--text-muted);
  text-align: center;
}

.state--error {
  color: var(--danger);
}

.state--error .btn {
  color: var(--text);
}

.state__hint {
  color: var(--text-muted);
  font-size: 13px;
}

.state code,
.foot code {
  padding: 1px 6px;
  background: var(--bg-subtle);
  border-radius: 4px;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 12px;
}

.list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.foot {
  margin-top: 40px;
  padding-top: 20px;
  border-top: 1px solid var(--border-soft);
  color: var(--text-muted);
  font-size: 13px;
}

.foot p {
  margin: 0 0 6px;
}

.foot__muted {
  font-size: 12px;
}

@media (max-width: 560px) {
  .page {
    padding: 24px 14px 48px;
  }

  .hero {
    flex-direction: column;
  }

  .hero__title {
    font-size: 21px;
  }
}
</style>
