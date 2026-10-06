<script setup lang="ts">
import SiteFooter from './components/landing/SiteFooter.vue'
import SiteHeader from './components/landing/SiteHeader.vue'
import ChatWidget from './components/ChatWidget.vue'
import { useReveal } from './composables/useReveal'

useReveal()
</script>

<template>
  <SiteHeader />
  <main>
    <RouterView />
  </main>
  <SiteFooter />
  <ChatWidget />
</template>
