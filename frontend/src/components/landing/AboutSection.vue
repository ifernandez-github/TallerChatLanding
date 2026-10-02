<script setup lang="ts">
import { site } from '../../content/site'
import Icon from '../Icon.vue'
import AboutArt from './AboutArt.vue'

const [main, ...rest] = site.stats
</script>

<template>
  <section id="quienes-somos" class="lp-section">
    <div class="lp-wrap about">
      <div class="about-art reveal">
        <div class="about-card">
          <img v-if="site.aboutImage" :src="site.aboutImage" alt="El equipo del taller" class="about-photo" />
          <AboutArt v-else />
        </div>
        <div class="badge-float"><strong>{{ main.value }}</strong><span>{{ main.label }}</span></div>
      </div>

      <div class="reveal" style="--d: 120ms">
        <h2 class="lp-h2">Quiénes somos</h2>
        <p class="lp-lead">{{ site.about.lead }}</p>
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
