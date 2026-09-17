<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { I18nT, useI18n } from 'vue-i18n'
import logoUrl from '../../assets/logo.svg'
import AppIcon from '../components/AppIcon.vue'
import AppLink from '../components/AppLink.vue'
import FeatureCard from '../components/FeatureCard.vue'
import type { IconName } from '../icons'
import { links } from '../links'
import { loadStats, type LibraryStats } from '../site-stats'
import { PAGE_PAD } from '../ui'

const { t } = useI18n()

/** 图标固定在代码里，文案跟着语言走，所以整个数组用 computed 包起来。 */
const FEATURES = computed<{ icon: IconName; title: string; body: string }[]>(() => [
  { icon: 'shapes', title: t('features.aoe.title'), body: t('features.aoe.body') },
  { icon: 'broadcast', title: t('features.tts.title'), body: t('features.tts.body') },
  { icon: 'bolt', title: t('features.customize.title'), body: t('features.customize.body') },
  { icon: 'gift', title: t('features.free.title'), body: t('features.free.body') },
])

/** 统计还没到时的占位符，免得四个格子先塌下去再撑开。 */
const PLACEHOLDER = '—'

const stats = ref<LibraryStats | null>(null)
const statsFailed = ref(false)

onMounted(async () => {
  try {
    stats.value = await loadStats()
  } catch {
    // 统计数字只是点缀：索引没取到就当没有这块，不打扰用户。
    statsFailed.value = true
  }
})

const statItems = computed(() => [
  // 用户数是外部给的概数，不来自索引，所以只有它没有「还没取到」的占位
  { label: t('stats.users'), value: t('stats.usersValue') },
  { label: t('stats.scripts'), value: stats.value ? String(stats.value.scripts) : PLACEHOLDER },
  { label: t('stats.contributors'), value: stats.value ? String(stats.value.contributors) : PLACEHOLDER },
  { label: t('stats.territories'), value: stats.value ? String(stats.value.territories) : PLACEHOLDER },
])
</script>

<template>
  <!--
    首屏占满一屏，整块内容垂直居中：标题区 -> 功能卡片 -> 脚本信息。
    - 减 56px：顶栏是 sticky 但仍占文档流一行（h-14），不减底部会掉到屏幕外。
    - 用 my-auto 而不是 justify-center 居中：内容比一屏高时（手机上）auto 外边距会归零，
      不会像 justify-center 那样把顶部内容裁掉。
    - isolate：给板块建独立层叠上下文。标题后面那张放大的图形用 -z-10 才压得住内容，
      否则绝对定位的图形会盖在徽标和卡片上面。
  -->
  <section
    class="relative isolate flex min-h-[calc(100svh-56px)] flex-col overflow-hidden pt-8 pb-8 max-[620px]:pt-6 max-[620px]:pb-5"
  >
    <!-- 主题色氛围光：整页唯一的渐变，给纯色背景一点纵深 -->
    <div
      class="pointer-events-none absolute inset-x-0 top-0 -z-20 h-200 bg-[radial-gradient(46%_58%_at_50%_0%,rgba(226,164,0,0.18),transparent_72%)]"
      aria-hidden="true"
    ></div>

    <div class="relative my-auto flex w-full flex-col" :class="PAGE_PAD">
      <!-- 标题区：整体居中 -->
      <div class="flex flex-col items-center text-center">
        <img
          :src="logoUrl"
          :alt="t('footer.brand')"
          class="h-25 w-auto mb-5 select-none"
          draggable="false"
          @dragstart.prevent
        />

        <div class="relative mt-5">
          <!-- 装饰用的地面危险区示意：放大后居中垫在标题文字后面，压低不透明度当水印 -->
          <svg
            class="pointer-events-none absolute top-1/2 left-1/2 -z-10 w-75 -translate-x-1/2 -translate-y-1/2 text-accent opacity-25 sm:w-105 lg:w-130"
            viewBox="0 0 200 200"
            fill="none"
            aria-hidden="true"
          >
            <circle
              cx="100"
              cy="100"
              r="92"
              stroke="currentColor"
              stroke-width="1"
              stroke-dasharray="5 7"
            />
            <circle cx="100" cy="100" r="62" stroke="currentColor" stroke-width="1" />
            <path
              d="M100 38a62 62 0 0 1 53.7 31"
              stroke="currentColor"
              stroke-width="5"
              stroke-linecap="round"
            />
          </svg>

          <!--
            主标题里那个带主题色的短语位置各语言不同（中文在中间、日文在句首动作前），
            所以整句交给译文，只把 {accent} 这一处插槽留出来上色，而不是把句子拆成前后两段。
          -->
          <h1
            class="relative max-w-220 text-[30px] leading-[1.2] font-bold tracking-[-0.02em] text-balance sm:text-[40px] lg:text-[50px]"
          >
            <I18nT keypath="hero.title">
              <template #accent>
                <span class="text-accent whitespace-nowrap">{{ t('hero.accent') }}</span>
              </template>
            </I18nT>
          </h1>
        </div>

        <p class="relative mt-5 max-w-155 text-pretty text-fg-muted sm:text-[16.5px]">
          {{ t('hero.lead') }}
        </p>

        <div class="relative mt-7 flex flex-wrap justify-center gap-3">
          <AppLink href="#start" variant="primary" size="lg">{{ t('hero.getStarted') }}</AppLink>
          <AppLink :href="links.discord" variant="solid" size="lg" external>
            <AppIcon name="chat" />
            Discord
          </AppLink>
        </div>
      </div>

      <!-- 功能：紧接标题区，不再单列一节标题，把一屏留给卡片 -->
      <h2 class="sr-only">{{ t('hero.featuresLabel') }}</h2>
      <div class="mt-40 grid gap-3.5 max-[620px]:mt-8 sm:grid-cols-2 lg:grid-cols-4">
        <FeatureCard
          v-for="feature in FEATURES"
          :key="feature.icon"
          :icon="feature.icon"
          :title="feature.title"
          :body="feature.body"
        />
      </div>

      <!-- 脚本信息：在线库的规模，贴在屏幕底部；数据取自站上的 index.json -->
      <dl
        v-if="!statsFailed"
        class="mt-6 grid grid-cols-2 gap-px overflow-hidden rounded-xl border border-line-soft bg-line-soft transition hover:border-line-bright hover:shadow-glow sm:grid-cols-4"
      >
        <div v-for="item in statItems" :key="item.label" class="bg-surface px-4 py-3.5">
          <dt class="text-[12.5px] text-fg-muted">{{ item.label }}</dt>
          <dd class="mt-1.5 text-[22px] leading-none font-semibold tabular-nums">
            {{ item.value }}
          </dd>
        </div>
      </dl>
    </div>
  </section>
</template>
