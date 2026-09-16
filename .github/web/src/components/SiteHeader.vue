<script setup lang="ts">
import logoUrl from '../../assets/logo.svg'
import AppIcon from './AppIcon.vue'
import AppLink from './AppLink.vue'
import { links } from '../links'
import { activeSection, NAV, TOP_ID } from '../sections'
</script>

<template>
  <header class="sticky top-0 z-20 border-b border-line-soft bg-canvas/80 backdrop-blur-md">
    <div
      class="mx-auto flex h-14 max-w-[1080px] items-center gap-4 px-5 max-[620px]:gap-3 max-[620px]:px-4"
    >
      <!-- 图标就是主页：点它滚回页面顶端 -->
      <a class="flex flex-none items-center" :href="`#${TOP_ID}`" aria-label="回到主页顶部">
        <img :src="logoUrl" alt="可达鸭 KodakkuAssist" class="h-6 w-auto" />
      </a>

      <!-- 两侧各一个弹性空隙，把三个板块链接顶到整条栏的正中；窄屏收回，导航跟在图标后面 -->
      <span class="flex-1 max-[620px]:hidden" aria-hidden="true"></span>

      <nav class="flex items-center gap-5 text-[13px] max-[620px]:gap-3.5" aria-label="板块导航">
        <a
          v-for="item in NAV"
          :key="item.id"
          class="relative transition"
          :class="activeSection === item.id ? 'text-fg' : 'text-fg-muted hover:text-fg'"
          :href="`#${item.id}`"
          :aria-current="activeSection === item.id ? 'true' : undefined"
        >
          {{ item.label }}
          <!-- 下划线贴住行盒底边；这里不能加 py，否则行盒被撑高、下划线会被推下去像下标 -->
          <span
            v-if="activeSection === item.id"
            class="absolute inset-x-0 bottom-0 h-0.5 rounded-full bg-accent"
            aria-hidden="true"
          ></span>
        </a>
      </nav>

      <span class="flex-1 max-[620px]:hidden" aria-hidden="true"></span>

      <AppLink
        class="max-[620px]:hidden"
        :href="links.discord"
        variant="ghost"
        size="sm"
        external
      >
        <AppIcon name="chat" />
        Discord
      </AppLink>
    </div>
  </header>
</template>
