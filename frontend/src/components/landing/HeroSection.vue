<script setup lang="ts">
import { site } from '../../content/site'
import { useChat } from '../../composables/useChat'
import Icon from '../Icon.vue'
import HeroScene from './HeroScene.vue'

const { openChat } = useChat()
</script>

<template>
  <section id="inicio" class="hero" aria-label="Inicio">
    <div class="lp-wrap hero-grid">
      <div class="hero-copy">
        <p class="eyebrow">{{ site.eyebrow }}</p>
        <h1 class="wordmark">{{ site.brand.first }} <em>{{ site.brand.second }}</em></h1>
        <p class="hero-tagline">Mecánica de precisión. <em>Diagnóstico honesto.</em></p>
        <p class="hero-lead">{{ site.intro }}</p>
        <div class="hero-cta">
          <RouterLink to="/cita" class="btn btn-primary btn-lg">Pedir cita <Icon name="arrow" :size="18" /></RouterLink>
          <button type="button" class="btn btn-ghost btn-lg" @click="openChat">
            <Icon name="chat" :size="18" />Mecánico interactivo
          </button>
        </div>
        <ul class="hero-points">
          <li v-for="p in site.heroPoints" :key="p"><Icon name="check" :size="16" />{{ p }}</li>
        </ul>
      </div>

      <div class="hero-media">
        <img v-if="site.heroImage" :src="site.heroImage" class="hero-photo"
          alt="Dos técnicos del taller revisando los bajos de un coche sobre el elevador" />
        <div v-else class="hero-scene"><HeroScene :sign="site.name.toUpperCase()" /></div>
        <div class="hero-badge">
          <strong>{{ site.heroBadge.value }}</strong>
          <span>{{ site.heroBadge.label }}</span>
        </div>
      </div>
    </div>
  </section>
</template>
