<script setup lang="ts">
import SiteFooter from './components/landing/SiteFooter.vue'
import SiteHeader from './components/landing/SiteHeader.vue'
import ChatWidget from './components/ChatWidget.vue'
import { observeReveals, useReveal } from './composables/useReveal'

useReveal()
</script>

<template>
  <SiteHeader />
  <main>
    <!--
      Las páginas se cargan de forma diferida, así que sus elementos .reveal no existen todavía cuando se monta
      App.vue. Se observan en el montaje de cada página (que es justo cuando entran en el DOM); el watcher de
      useReveal cubre además los cambios de ruta que reutilizan el mismo componente.
    -->
    <RouterView v-slot="{ Component }">
      <component :is="Component" @vue:mounted="observeReveals" />
    </RouterView>
  </main>
  <SiteFooter />
  <ChatWidget />
</template>
