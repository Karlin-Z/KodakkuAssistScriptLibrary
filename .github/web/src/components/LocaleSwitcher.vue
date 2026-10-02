<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import AppIcon from './AppIcon.vue'
import { LOCALES, setLocale, type LocaleCode } from '../i18n'

/**
 * 语言切换。做成折叠菜单而不是原生 <select>：原生下拉的弹层样式由系统决定，
 * 和整站的深色卡片差太远。代价是收起逻辑要自己管：点空白处、按 Esc 都要能关。
 *
 * 窄屏只留地球图标（语言名和缩写都收起来），顶栏一行才放得下。
 */
const { t, locale } = useI18n()

const open = ref(false)
const root = ref<HTMLElement | null>(null)
const trigger = ref<HTMLButtonElement | null>(null)

/** 当前语言：`locale` 一定能命中 LOCALES，找不到时退回第一项兜底。 */
const current = computed(() => LOCALES.find((item) => item.code === locale.value) ?? LOCALES[0])

function select(code: LocaleCode): void {
  setLocale(code)
  open.value = false
  // 菜单项随 v-if 一起卸载，不把焦点还回去的话它会掉到 body 上（键盘用户就找不到位置了）
  trigger.value?.focus()
}

function onPointerDown(event: PointerEvent): void {
  if (open.value && root.value && !root.value.contains(event.target as Node)) open.value = false
}

/** Esc 收起并把焦点还给按钮，否则焦点会留在已卸载的菜单项上。 */
function onKeydown(event: KeyboardEvent): void {
  if (event.key !== 'Escape' || !open.value) return
  open.value = false
  trigger.value?.focus()
}

onMounted(() => {
  document.addEventListener('pointerdown', onPointerDown)
  document.addEventListener('keydown', onKeydown)
})

onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', onPointerDown)
  document.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <div ref="root" class="relative flex-none">
    <button
      ref="trigger"
      class="flex h-8 cursor-pointer items-center gap-1.5 rounded-lg border border-transparent px-2 text-fg-muted transition hover:bg-subtle hover:text-fg max-[620px]:px-1.5"
      type="button"
      :aria-label="t('locale.label')"
      aria-haspopup="true"
      :aria-expanded="open"
      @click="open = !open"
    >
      <AppIcon name="globe" />
      <span class="text-[13px] max-[620px]:hidden">{{ current.short }}</span>
      <AppIcon class="text-[10px] transition-transform" :class="open && 'rotate-180'" name="chevron" />
    </button>

    <Transition
      enter-active-class="transition duration-150 ease-out"
      enter-from-class="-translate-y-1 opacity-0"
      leave-active-class="transition duration-100 ease-in"
      leave-to-class="-translate-y-1 opacity-0"
    >
      <ul
        v-if="open"
        class="absolute top-[calc(100%+6px)] right-0 z-30 min-w-[9.5rem] rounded-xl border border-line-soft bg-surface p-1 shadow-md"
        :aria-label="t('locale.label')"
      >
        <li v-for="item in LOCALES" :key="item.code">
          <button
            class="flex w-full cursor-pointer items-center gap-2 rounded-lg px-2.5 py-1.5 text-left text-[13px] whitespace-nowrap transition"
            :class="
              item.code === locale ? 'text-fg' : 'text-fg-muted hover:bg-subtle hover:text-fg'
            "
            type="button"
            :aria-current="item.code === locale ? 'true' : undefined"
            @click="select(item.code)"
          >
            <span class="flex-1">{{ item.label }}</span>
            <AppIcon v-if="item.code === locale" class="text-accent" name="check" />
          </button>
        </li>
      </ul>
    </Transition>
  </div>
</template>
