<script setup lang="ts">
import { computed } from 'vue'
import { buttonClass, type ButtonSize, type ButtonVariant } from '../ui'

/**
 * `size` 管高度 / 内边距 / 字号，`variant` 管边框与配色；两者都在 ui.ts 里定义，
 * 与 AppLink 共用。其余属性（`title`、`disabled`、`@click`…）直接落到根元素 <button> 上。
 */
const props = withDefaults(
  defineProps<{ variant?: ButtonVariant; size?: ButtonSize }>(),
  { variant: 'solid', size: 'md' },
)

const classes = computed(() => buttonClass(props.variant, props.size))
</script>

<template>
  <button class="disabled:cursor-progress" :class="classes" type="button">
    <slot />
  </button>
</template>
