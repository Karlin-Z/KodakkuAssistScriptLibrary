<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { links } from '../links'
import { NAV, TOP_ID } from '../sections'

const { t } = useI18n()

const YEAR = new Date().getFullYear()

const RESOURCES = computed(() => [
  { href: links.discord, label: t('footer.discord') },
  { href: links.repo, label: t('footer.repo') },
  { href: links.guide, label: t('footer.guide') },
])
</script>

<template>
  <footer class="mt-20 border-t border-line-soft">
    <div class="mx-auto max-w-[1080px] px-5 py-10 max-[620px]:px-4">
      <div class="flex flex-wrap items-start justify-between gap-10">
        <p class="max-w-[400px] text-[13px] text-fg-muted">
          <strong class="mb-1.5 block font-semibold text-fg">{{ t('footer.brand') }}</strong>
          {{ t('footer.about') }}
        </p>

        <div class="flex flex-wrap gap-x-14 gap-y-6">
          <nav>
            <p class="mb-2.5 text-[12.5px] font-medium text-fg">{{ t('footer.sections') }}</p>
            <ul class="flex flex-col gap-2 text-[13px]">
              <li>
                <a class="text-fg-muted transition hover:text-accent-bright" :href="`#${TOP_ID}`">
                  {{ t('nav.home') }}
                </a>
              </li>
              <li v-for="item in NAV" :key="item.id">
                <a class="text-fg-muted transition hover:text-accent-bright" :href="`#${item.id}`">
                  {{ t(`nav.${item.id}`) }}
                </a>
              </li>
            </ul>
          </nav>
          <div>
            <p class="mb-2.5 text-[12.5px] font-medium text-fg">{{ t('footer.resources') }}</p>
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
        {{ t('footer.copyright', { year: YEAR }) }}
      </p>
    </div>
  </footer>
</template>
