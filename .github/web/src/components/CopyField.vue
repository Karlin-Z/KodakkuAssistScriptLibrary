<script setup lang="ts">
import AppButton from './AppButton.vue'
import AppIcon from './AppIcon.vue'
import { useCopy } from '../useCopy'

/** 一条要用户抄进插件里的地址：可点复制，也可以直接划选。 */
const props = defineProps<{ label: string; value: string; hint?: string }>()

const { state, copy } = useCopy()

const LABEL = {
  idle: '复制地址',
  copied: '已复制',
  failed: '复制失败',
} as const
</script>

<template>
  <div class="rounded-xl border border-line-soft bg-surface p-4">
    <div class="mb-2.5 flex items-center justify-between gap-3">
      <span class="text-[13px] font-medium">{{ label }}</span>
      <AppButton size="sm" @click="copy(props.value)">
        <AppIcon :name="state === 'copied' ? 'check' : 'copy'" />
        {{ LABEL[state] }}
      </AppButton>
    </div>
    <code
      class="block rounded-lg border border-line-soft bg-code px-3 py-2 font-mono text-[12.5px] text-fg-muted select-all break-all"
      >{{ value }}</code
    >
    <p v-if="props.hint" class="mt-2.5 text-[12.5px] text-fg-muted">{{ hint }}</p>
  </div>
</template>
