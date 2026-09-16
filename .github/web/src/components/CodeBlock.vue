<script setup lang="ts">
import AppIcon from './AppIcon.vue'
import { useCopy } from '../useCopy'

/** 代码片段：整体（含标题栏）就是一个可复制单元，省得用户手动划选。 */
const props = defineProps<{ title: string; code: string }>()

const { state, copy } = useCopy()

const LABEL = {
  idle: '复制',
  copied: '已复制',
  failed: '复制失败',
} as const
</script>

<template>
  <figure class="overflow-hidden rounded-xl border border-line-soft bg-code">
    <figcaption
      class="flex items-center justify-between gap-3 border-b border-line-soft bg-surface px-4 py-2"
    >
      <span class="flex items-center gap-2 text-[12.5px] text-fg-muted">
        <AppIcon name="code" />
        {{ title }}
      </span>
      <button
        class="flex cursor-pointer items-center gap-1.5 rounded-md px-2 py-1 text-[12.5px] text-fg-muted transition hover:bg-subtle hover:text-fg"
        type="button"
        @click="copy(props.code)"
      >
        <AppIcon :name="state === 'copied' ? 'check' : 'copy'" />
        {{ LABEL[state] }}
      </button>
    </figcaption>
    <pre
      class="overflow-x-auto overscroll-x-contain px-3.5 py-3.5 font-mono text-[12px] leading-[1.75] text-[#cfcfcf] sm:px-4 sm:text-[12.5px]"
    ><code>{{ code }}</code></pre>
  </figure>
</template>
