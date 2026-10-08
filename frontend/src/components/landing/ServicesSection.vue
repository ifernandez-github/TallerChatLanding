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
        <p class="eyebrow">Servicios</p>
        <h2 class="lp-h2">Todo lo que tu coche necesita</h2>
        <p class="lp-lead">De la revisión periódica a la avería más rebuscada. Entra en cada servicio y te contamos por qué importa y cada cuánto toca.</p>
      </div>

      <div class="svc-grid">
        <RouterLink v-for="(s, i) in site.services" :key="s.icon" :to="`/servicios/${s.icon}`"
          class="svc-card reveal" :style="{ '--d': `${(i % 4) * 60}ms` }">
          <img :src="s.image" :alt="s.title" class="svc-photo" loading="lazy" />
          <div class="svc-body">
            <span class="svc-ico"><Icon :name="s.icon" :size="22" /></span>
            <h3>{{ s.title }}</h3>
            <p>{{ s.text }}</p>
            <span class="svc-link">Ver servicio <Icon name="arrow" :size="16" /></span>
          </div>
        </RouterLink>
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
