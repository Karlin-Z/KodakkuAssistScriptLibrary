<script setup lang="ts">
import AppIcon from './AppIcon.vue'
import type { IconName } from '../icons'

defineProps<{ icon: IconName; title: string; body: string }>()
</script>

<template>
  <article
    class="flex flex-col gap-2.5 rounded-xl border border-line-soft bg-surface bg-[linear-gradient(180deg,rgba(255,255,255,0.04),transparent_60%)] p-4 transition hover:border-line-bright hover:shadow-glow"
  >
    <span
      class="flex size-8 items-center justify-center rounded-lg border border-accent/25 bg-accent-soft text-[15px] text-accent"
    >
      <AppIcon :name="icon" />
    </span>
    <h3 class="text-[15px] font-semibold">{{ title }}</h3>
    <p class="text-[13px] text-fg-muted">{{ body }}</p>
  </article>
</template>
