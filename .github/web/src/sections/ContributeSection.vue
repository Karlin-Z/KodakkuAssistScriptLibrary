<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import AppIcon from '../components/AppIcon.vue'
import AppLink from '../components/AppLink.vue'
import SectionHead from '../components/SectionHead.vue'
import StepList from '../components/StepList.vue'
import { links } from '../links'
import { PAGE_PAD } from '../ui'

const { t } = useI18n()

const STEPS = computed(() => [
  { title: t('contribute.steps.folder.title'), body: t('contribute.steps.folder.body') },
  { title: t('contribute.steps.pr.title'), body: t('contribute.steps.pr.body') },
  { title: t('contribute.steps.merge.title'), body: t('contribute.steps.merge.body') },
])

/** 文件名是仓库里真实的名字，不翻译；只有说明跟着语言走。 */
const RESOURCES = computed(() => [
  { name: 'script-writing-guide.md', href: links.guide, desc: t('contribute.files.guide') },
  { name: 'SimpleScript.cs', href: links.sample, desc: t('contribute.files.sample') },
  { name: 'OnlineRepo.json', href: links.subscription, desc: t('contribute.files.index') },
])
</script>

<template>
  <section id="contribute" class="mt-24 max-[620px]:mt-16 mb-100" :class="PAGE_PAD">
    <SectionHead
      :eyebrow="t('contribute.eyebrow')"
      :title="t('contribute.title')"
      :lead="t('contribute.lead')"
    />

    <div class="mb-12 flex flex-wrap items-center gap-3">
      <AppLink :href="links.repo" variant="primary" size="lg" external>
        <AppIcon name="external" />
        {{ t('contribute.repo') }}
      </AppLink>
      <AppLink :href="links.readme" variant="solid" size="lg" external>
        <AppIcon name="book" />
        {{ t('contribute.guide') }}
      </AppLink>
      <span class="text-[13px] text-fg-muted">{{ t('contribute.flow') }}</span>
    </div>

    <div class="grid grid-cols-1 gap-10 lg:grid-cols-2">
      <div>
        <h3 class="mb-5 text-[15px] font-semibold">{{ t('contribute.process') }}</h3>
        <StepList :steps="STEPS" />
      </div>
      <div>
        <h3 class="mb-5 text-[15px] font-semibold">{{ t('contribute.resources') }}</h3>
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
