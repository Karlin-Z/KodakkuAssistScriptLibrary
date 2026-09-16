<script setup lang="ts">
import SiteFooter from './components/SiteFooter.vue'
import SiteHeader from './components/SiteHeader.vue'
import ContributeSection from './sections/ContributeSection.vue'
import ExamplesSection from './sections/ExamplesSection.vue'
import HomeSection from './sections/HomeSection.vue'
import StartSection from './sections/StartSection.vue'
import { useActiveSection } from './sections'

// 板块依次排在一页里，靠滚动切换；导航高亮由 useActiveSection 跟着滚动更新。
useActiveSection()
</script>

<template>
  <div id="top">
    <SiteHeader />
    <main>
      <HomeSection />
      <ExamplesSection />
      <StartSection />
      <ContributeSection />
    </main>
    <SiteFooter />
  </div>
</template>
