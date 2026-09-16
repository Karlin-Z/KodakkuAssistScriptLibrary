<script setup lang="ts">
import AppIcon from '../components/AppIcon.vue'
import AppLink from '../components/AppLink.vue'
import SectionHead from '../components/SectionHead.vue'
import StepList from '../components/StepList.vue'
import { links } from '../links'
import { PAGE_PAD } from '../ui'

const STEPS = [
  {
    title: '建目录',
    body: 'Fork 本仓库，在根目录新建以你的 GitHub 用户名命名的文件夹，把 .cs 脚本文件放进去。',
  },
  {
    title: '提 PR',
    body: '只允许修改自己目录中的文件，修改完成后提交 PR。',
  },
  {
    title: '等合并',
    body: '自动审核校验 guid、版本号、文件名与目录归属，通过即合并并重建索引。',
  },
]

const RESOURCES = [
  {
    name: 'script-writing-guide.md',
    href: links.guide,
    desc: '完整的脚本编写指南，写脚本前先看这份。',
  },
  {
    name: 'SimpleScript.cs',
    href: links.sample,
    desc: '官方示例模板，复制下来改掉 guid 就能开工。',
  },
  {
    name: 'OnlineRepo.json',
    href: links.subscription,
    desc: '插件官方订阅用的在线库索引。',
  }
]
</script>

<template>
  <section id="contribute" class="mt-24 max-[620px]:mt-16 mb-100" :class="PAGE_PAD">
    <SectionHead
      eyebrow="贡献"
      title="分享你的脚本"
      lead="可达鸭支持你提交自己的脚本，方便其他玩家共享使用，步骤很简单"
    />

    <div class="mb-12 flex flex-wrap items-center gap-3">
      <AppLink :href="links.repo" variant="primary" size="lg" external>
        <AppIcon name="external" />
        GitHub 仓库
      </AppLink>
      <AppLink :href="links.readme" variant="solid" size="lg" external>
        <AppIcon name="book" />
        贡献说明
      </AppLink>
      <span class="text-[13px] text-fg-muted">Fork → 提交 PR → 自动审核通过后合并</span>
    </div>

    <div class="grid grid-cols-1 gap-10 lg:grid-cols-2">
      <div>
        <h3 class="mb-5 text-[15px] font-semibold">提交流程</h3>
        <StepList :steps="STEPS" />
      </div>
      <div>
        <h3 class="mb-5 text-[15px] font-semibold">开发资源</h3>
        <div class="flex flex-col gap-2">
          <a
            v-for="item in RESOURCES"
            :key="item.name"
            class="group flex items-center gap-3 rounded-lg border border-line-soft bg-surface px-4 py-3 transition hover:border-line"
            :href="item.href"
            target="_blank"
            rel="noopener"
          >
            <span class="min-w-0 flex-1">
              <span class="block font-mono text-[13px] font-medium">{{ item.name }}</span>
              <span class="mt-0.5 block text-[12.5px] text-fg-muted">{{ item.desc }}</span>
            </span>
            <AppIcon class="text-fg-muted transition group-hover:text-accent" name="external" />
          </a>
        </div>
      </div>
    </div>
  </section>
</template>
