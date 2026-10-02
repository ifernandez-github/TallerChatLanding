<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { site } from '../../content/site'
import { useChat } from '../../composables/useChat'
import Icon from '../Icon.vue'
import HeroScene from './HeroScene.vue'

const el = ref<HTMLElement | null>(null)
const { openChat } = useChat()
let raf = 0

// --py = desplazamiento de scroll; las capas del hero lo usan para el parallax.
function onScroll() {
  if (raf) return
  raf = requestAnimationFrame(() => {
    raf = 0
    el.value?.style.setProperty('--py', String(Math.min(window.scrollY, window.innerHeight * 1.2)))
  })
}
onMounted(() => {
  if (matchMedia('(prefers-reduced-motion: reduce)').matches) return
  window.addEventListener('scroll', onScroll, { passive: true })
  onScroll()
})
onBeforeUnmount(() => window.removeEventListener('scroll', onScroll))
</script>

<template>
  <section id="inicio" ref="el" class="lp-hero" aria-label="Inicio">
    <div class="screen">
      <img v-if="site.heroImage" :src="site.heroImage" alt="" class="hero-photo" />
      <HeroScene v-else :sign="site.name.toUpperCase()" />
      <div class="scanlines" aria-hidden="true" />
      <div class="vignette" aria-hidden="true" />
    </div>

    <div class="hero-copy lp-wrap">
      <h1>Mecánica de precisión. <em>Diagnóstico honesto.</em></h1>
      <p>{{ site.intro }}</p>
      <div class="hero-cta">
        <a href="#contacto" class="btn btn-primary">Pedir cita <Icon name="arrow" :size="18" /></a>
        <button type="button" class="btn btn-ghost" @click="openChat"><Icon name="chat" :size="18" />Hablar con el mecánico interactivo</button>
      </div>
      <ul class="hero-points">
        <li v-for="p in site.heroPoints" :key="p"><Icon name="check" :size="16" />{{ p }}</li>
      </ul>
    </div>

    <a href="#quienes-somos" class="scroll-cue" aria-label="Ir a la siguiente sección"><Icon name="chevron" :size="22" /></a>
  </section>
</template>
