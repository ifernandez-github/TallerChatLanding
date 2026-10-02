<script setup lang="ts">
import AboutSection from './components/landing/AboutSection.vue'
import ContactSection from './components/landing/ContactSection.vue'
import HeroSection from './components/landing/HeroSection.vue'
import ServicesSection from './components/landing/ServicesSection.vue'
import SiteFooter from './components/landing/SiteFooter.vue'
import SiteHeader from './components/landing/SiteHeader.vue'
import ChatWidget from './components/ChatWidget.vue'
import { useReveal } from './composables/useReveal'

useReveal()
</script>

<template>
  <SiteHeader />
  <main>
    <HeroSection />
    <AboutSection />
    <ServicesSection />
    <ContactSection />
  </main>
  <SiteFooter />
  <ChatWidget />
</template>
