<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import CopyField from '../components/CopyField.vue'
import SectionHead from '../components/SectionHead.vue'
import StepList from '../components/StepList.vue'
import { links } from '../links'
import { PAGE_PAD } from '../ui'

const { t } = useI18n()

const STEPS = computed(() => [
  { title: t('start.steps.install.title'), body: t('start.steps.install.body') },
  { title: t('start.steps.verify.title'), body: t('start.steps.verify.body') },
  { title: t('start.steps.enable.title'), body: t('start.steps.enable.body') },
])
</script>

<template>
  <section id="start" class="mt-24 mb-50 max-[620px]:mt-16" :class="PAGE_PAD">
    <SectionHead
      :eyebrow="t('start.eyebrow')"
      :title="t('start.title')"
      :lead="t('start.lead')"
    />

    <!-- 分栏与「贡献」板块保持一致（都是对分），左右两栏才能和那边对齐 -->
    <div class="grid grid-cols-1 gap-10 lg:grid-cols-2 lg:items-start">
      <StepList :steps="STEPS" />

      <!--
        两条订阅地址，别搞混：
        插件库（DalamudPlugins 仓库）用来在卫月里装插件本体；在线库（本脚本库）是插件内的脚本索引。
        这里只列插件库那条 —— 脚本库的地址由插件自己带，用户不需要手抄。
      -->
      <div class="flex flex-col gap-3">
        <CopyField :label="t('start.pluginLibrary')" :value="links.pluginLibrary" />
      </div>
    </div>
  </section>
</template>
