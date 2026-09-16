<script setup lang="ts">
import CopyField from '../components/CopyField.vue'
import SectionHead from '../components/SectionHead.vue'
import StepList from '../components/StepList.vue'
import { links } from '../links'
import { PAGE_PAD } from '../ui'

const STEPS = [
  {
    title: '安装插件',
    body: '在卫月的「在线插件库设置」里添加插件库订阅地址，然后搜索 KodakkuAssist 并启用。',
  },
  {
    title: '认证插件',
    body: '跟随插件内的提示，在 Discord 中完成认证并激活插件。',
  },
  {
    title: '启用脚本',
    body: '下载并启用你需要的副本绘制脚本。',
  },
]
</script>

<template>
  <section id="start" class="mt-24 mb-50 max-[620px]:mt-16" :class="PAGE_PAD">
    <SectionHead
      eyebrow="开始"
      title="三步就能用起来"
      lead="从装插件到开始绘制 AOE，只需简单三步"
    />

    <!-- 分栏与「贡献」板块保持一致（都是对分），左右两栏才能和那边对齐 -->
    <div class="grid grid-cols-1 gap-10 lg:grid-cols-2 lg:items-start">
      <StepList :steps="STEPS" />

      <!--
        两条订阅地址，别搞混：
        插件库（DalamudPlugins 仓库）用来在卫月里装插件本体；在线库（本脚本库）是插件内的脚本索引。
      -->
      <div class="flex flex-col gap-3">
        <CopyField label="插件库订阅地址" :value="links.pluginLibrary" />
      </div>
    </div>
  </section>
</template>
