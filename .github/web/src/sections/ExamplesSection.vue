<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import SectionHead from '../components/SectionHead.vue'
import { PAGE_PAD } from '../ui'
import scriptWindow from '../../assets/script-window.webp'

const { t } = useI18n()

/**
 * 界面截图。图片按原始尺寸写进 width/height，让浏览器先占好位，
 * 加载完不会把下面的内容顶下去。想加图就往数组里追加一条；
 * 图上没有文字说明，所以描述写在 alt 里，读屏和搜索引擎拿得到（跟着语言走）。
 */
const SHOTS = computed(() => [
  {
    src: scriptWindow,
    width: 1686,
    height: 1054,
    alt: t('examples.shotAlt'),
  },
])
</script>

<template>
  <section id="examples" class="mt-24 max-[620px]:mt-16 mb-50" :class="PAGE_PAD">
    <SectionHead :eyebrow="t('examples.eyebrow')" :title="t('examples.title')" />

    <div class="flex flex-col gap-8 p-6">
      <!-- 整块就是图片本身；点开可以在新标签看原图 -->
      <a
        v-for="shot in SHOTS"
        :key="shot.src"
        class="block overflow-hidden rounded-[22px] border border-line-soft bg-code transition hover:border-line-bright shadow-glow"
        :href="shot.src"
        target="_blank"
        rel="noopener"
      >
        <img
          class="h-auto w-full"
          :src="shot.src"
          :width="shot.width"
          :height="shot.height"
          :alt="shot.alt"
        />
      </a>
    </div>
  </section>
</template>
