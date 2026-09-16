<script setup lang="ts">
import { links } from '../links'
import { NAV, TOP_ID } from '../sections'

const YEAR = new Date().getFullYear()

const RESOURCES = [
  { href: links.discord, label: 'Discord' },
  { href: links.repo, label: 'GitHub 仓库' },
  { href: links.guide, label: '脚本说明书' }
]
</script>

<template>
  <footer class="mt-20 border-t border-line-soft">
    <div class="mx-auto max-w-[1080px] px-5 py-10 max-[620px]:px-4">
      <div class="flex flex-wrap items-start justify-between gap-10">
        <p class="max-w-[400px] text-[13px] text-fg-muted">
          <strong class="mb-1.5 block font-semibold text-fg">可达鸭 KodakkuAssist</strong>
          FF14「卫月」战斗机制辅助插件的官方在线脚本库。本库与插件均为玩家社区作品，与
          Square Enix 无关；使用第三方插件存在风险，请自行判断。
        </p>

        <div class="flex flex-wrap gap-x-14 gap-y-6">
          <nav>
            <p class="mb-2.5 text-[12.5px] font-medium text-fg">板块</p>
            <ul class="flex flex-col gap-2 text-[13px]">
              <li>
                <a class="text-fg-muted transition hover:text-accent-bright" :href="`#${TOP_ID}`">
                  主页
                </a>
              </li>
              <li v-for="item in NAV" :key="item.id">
                <a class="text-fg-muted transition hover:text-accent-bright" :href="`#${item.id}`">
                  {{ item.label }}
                </a>
              </li>
            </ul>
          </nav>
          <div>
            <p class="mb-2.5 text-[12.5px] font-medium text-fg">资源</p>
            <ul class="flex flex-col gap-2 text-[13px]">
              <li v-for="item in RESOURCES" :key="item.href">
                <a
                  class="text-fg-muted transition hover:text-accent-bright"
                  :href="item.href"
                  target="_blank"
                  rel="noopener"
                  >{{ item.label }}</a
                >
              </li>
            </ul>
          </div>
        </div>
      </div>

      <p class="mt-8 border-t border-line-soft pt-5 text-[12px] text-fg-muted">
        © {{ YEAR }} 可达鸭 KodakkuAssist
      </p>
    </div>
  </footer>
</template>
