<script setup lang="ts">
import { site } from '../../content/site'
import { useChat } from '../../composables/useChat'
import Icon from '../Icon.vue'

const { openChat } = useChat()
</script>

<template>
  <section id="servicios" class="lp-section alt">
    <div class="lp-wrap">
      <div class="svc-head reveal">
        <h2 class="lp-h2">Servicios</h2>
        <p class="lp-lead">Todo lo que tu coche necesita, de la revisión periódica a la avería más rebuscada.</p>
      </div>

      <div class="svc-grid">
        <article v-for="(s, i) in site.services" :key="s.title" class="svc-card reveal" :style="{ '--d': `${(i % 4) * 80}ms` }">
          <div class="svc-ico"><Icon :name="s.icon" :size="26" /></div>
          <h3>{{ s.title }}</h3>
          <p>{{ s.text }}</p>
        </article>
      </div>

      <div class="cta-band reveal">
        <div>
          <h3>¿No sabes qué le pasa a tu coche?</h3>
          <p>Cuéntaselo al mecánico interactivo: te orienta con la información de nuestra base de conocimiento.</p>
        </div>
        <button type="button" class="btn btn-amber" @click="openChat"><Icon name="chat" :size="18" />Preguntar ahora</button>
      </div>
    </div>
  </section>
</template>
