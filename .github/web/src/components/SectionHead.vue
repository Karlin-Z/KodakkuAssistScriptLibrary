<script setup lang="ts">
/** 三个板块共用的标题排布：小标签 + 主标题 + 一句说明。 */
defineProps<{ eyebrow: string; title: string; lead?: string }>()
</script>

<template>
  <div class="mb-8 max-w-[680px]">
    <p class="mb-3 flex items-center gap-2.5 text-[12.5px] font-medium tracking-[0.2em] text-accent">
      <span class="h-px w-7 flex-none bg-accent/40"></span>{{ eyebrow }}
    </p>
    <h2 class="text-[24px] font-bold tracking-[-0.01em] max-[620px]:text-[21px]">{{ title }}</h2>
    <p v-if="lead" class="mt-3 text-fg-muted">{{ lead }}</p>
  </div>
</template>
