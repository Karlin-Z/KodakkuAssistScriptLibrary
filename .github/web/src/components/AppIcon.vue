<script setup lang="ts">
import { computed } from 'vue'
import { ICON_PATHS, type IconName } from '../icons'

/**
 * 图标尺寸跟着字号走（1.15em），所以父级只要给字号就能整体缩放，
 * 也不会和父级传进来的 size-* 打架。
 */
const props = defineProps<{ name: IconName }>()

const paths = computed(() => ICON_PATHS[props.name])
</script>

<template>
  <svg
    class="size-[1.15em] shrink-0"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.7"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <path v-for="(d, index) in paths" :key="index" :d="d" />
  </svg>
</template>
