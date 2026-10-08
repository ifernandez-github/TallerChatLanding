<script setup lang="ts">
import { site } from '../../content/site'
import Icon from '../Icon.vue'
import AboutArt from './AboutArt.vue'

const [main, ...rest] = site.stats
// Mosaico del taller: si no hay foto única para "Quiénes somos", se usan cuatro fotos reales de servicio.
const mosaic = ['scan', 'brake', 'oil', 'wheel']
  .map((id) => site.services.find((s) => s.icon === id)?.image)
  .filter((src): src is string => !!src)
</script>

<template>
  <section id="quienes-somos" class="lp-section">
    <div class="lp-wrap about">
      <div class="about-art reveal">
        <div v-if="site.aboutImage" class="about-card">
          <img :src="site.aboutImage" alt="El equipo del taller" class="about-photo" />
        </div>
        <div v-else-if="mosaic.length === 4" class="about-mosaic">
          <img v-for="(src, i) in mosaic" :key="src" :src="src" loading="lazy"
            :alt="`Instalaciones del taller ${i + 1} de 4`" />
        </div>
        <div v-else class="about-card"><AboutArt /></div>
        <div class="badge-float"><strong>{{ main.value }}</strong><span>{{ main.label }}</span></div>
      </div>

      <div class="reveal" style="--d: 120ms">
        <p class="eyebrow">Quiénes somos</p>
        <h2 class="lp-h2">{{ site.about.lead }}</h2>
        <p v-for="p in site.about.paragraphs" :key="p">{{ p }}</p>
        <ul class="values">
          <li v-for="v in site.values" :key="v.title">
            <span class="tick"><Icon name="check" :size="18" /></span>
            <div><strong>{{ v.title }}</strong><span>{{ v.text }}</span></div>
          </li>
        </ul>
        <dl class="stats">
          <div v-for="s in rest" :key="s.label"><dd>{{ s.value }}</dd><dt>{{ s.label }}</dt></div>
        </dl>
      </div>
    </div>
  </section>
</template>
