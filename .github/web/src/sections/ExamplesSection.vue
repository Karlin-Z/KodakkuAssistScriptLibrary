<script setup lang="ts">
import SectionHead from '../components/SectionHead.vue'
import { PAGE_PAD } from '../ui'
import scriptWindow from '../../assets/script-window.webp'

/**
 * 界面截图。图片按原始尺寸写进 width/height，让浏览器先占好位，
 * 加载完不会把下面的内容顶下去。想加图就往数组里追加一条；
 * 图上没有文字说明，所以描述写在 alt 里，读屏和搜索引擎拿得到。
 */
const SHOTS: { src: string; width: number; height: number; alt: string }[] = [
  {
    src: scriptWindow,
    width: 1686,
    height: 1054,
    alt: '可达鸭的脚本页：官方 / 收藏 / 在线 / 本地四个来源的脚本列表，可按名称、作者、副本搜索筛选，右侧是选中脚本的简介、用户设置与方法设置。',
  },
]
</script>

<template>
  <section id="examples" class="mt-24 max-[620px]:mt-16 mb-50" :class="PAGE_PAD">
    <SectionHead
      eyebrow="示例"
      title="插件长什么样"
    />

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
