<script setup lang="ts">
/**
 * 带序号的步骤列表，编号用主题色点出来。
 * 每一步后面可以再插一段自定义内容（比如订阅地址框）：用 `#extra-0`、`#extra-1`… 对应第几步。
 */
defineProps<{ steps: { title: string; body: string }[] }>()
</script>

<template>
  <ol class="flex flex-col gap-6">
    <li v-for="(step, index) in steps" :key="step.title" class="flex gap-3.5">
      <span
        class="mt-px flex size-7 flex-none items-center justify-center rounded-full border border-accent/30 bg-accent-soft text-[13px] font-semibold text-accent tabular-nums"
        aria-hidden="true"
        >{{ index + 1 }}</span
      >
      <div class="min-w-0 flex-1">
        <h3 class="text-[15px] font-semibold">{{ step.title }}</h3>
        <p class="mt-1 text-[13px] text-fg-muted">{{ step.body }}</p>
        <slot :name="`extra-${index}`" />
      </div>
    </li>
  </ol>
</template>
