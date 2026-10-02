<script setup lang="ts">
import { computed } from 'vue'
import { buttonClass, type ButtonSize, type ButtonVariant } from '../ui'

/** 与 AppButton 同一套外观，只是根元素换成 <a>：站内锚点用 external=false，站外链接另开标签页。 */
const props = withDefaults(
  defineProps<{
    href: string
    variant?: ButtonVariant
    size?: ButtonSize
    external?: boolean
  }>(),
  { variant: 'solid', size: 'md', external: false },
)

const classes = computed(() => buttonClass(props.variant, props.size))
</script>

<template>
  <a
    :href="href"
    :class="classes"
    :target="external ? '_blank' : undefined"
    :rel="external ? 'noopener' : undefined"
  >
    <slot />
  </a>
</template>
