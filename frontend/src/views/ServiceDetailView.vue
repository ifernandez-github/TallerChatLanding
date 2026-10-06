<script setup lang="ts">
import { computed, watchEffect } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Icon from '../components/Icon.vue'
import { useChat } from '../composables/useChat'
import { findService, site } from '../content/site'

const route = useRoute()
const router = useRouter()
const { openChat } = useChat()

const service = computed(() => findService(route.params.id as string))
const others = computed(() => site.services.filter((s) => s.icon !== route.params.id).slice(0, 4))

// El título depende del parámetro, así que se actualiza también al saltar de un servicio a otro.
watchEffect(() => {
  if (!service.value) { router.replace('/servicios'); return }
  document.title = `${service.value.title} · ${site.name}`
})
</script>

<template>
  <article v-if="service" class="svc-page">
    <!-- La foto ya lleva rotulado el nombre del servicio: no se superpone ningún texto encima. -->
    <div class="svc-hero">
      <img :src="service.image" :alt="service.title" />
    </div>

    <div class="lp-wrap narrow">
      <header class="svc-intro card enter">
        <nav class="crumbs" aria-label="Migas de pan">
          <RouterLink to="/servicios">Servicios</RouterLink>
          <Icon name="arrow" :size="13" />
          <span>{{ service.title }}</span>
        </nav>
        <h1 class="page-title">{{ service.title }}</h1>
        <p class="lp-lead">{{ service.lead }}</p>
        <div class="row-gap">
          <RouterLink :to="{ path: '/cita', query: { servicio: service.icon } }" class="btn btn-primary btn-lg">
            <Icon name="calendar" :size="18" />Pedir cita
          </RouterLink>
          <button type="button" class="btn btn-outline btn-lg" @click="openChat">
            <Icon name="chat" :size="18" />Consultar al mecánico
          </button>
        </div>
      </header>

      <section class="svc-block enter" style="--i: 1">
        <h2 class="block-title">Por qué importa</h2>
        <div class="why-grid">
          <div v-for="w in service.why" :key="w.title" class="why-card">
            <span class="why-ico"><Icon :name="service.icon" :size="20" /></span>
            <h3>{{ w.title }}</h3>
            <p>{{ w.text }}</p>
          </div>
        </div>
      </section>

      <section v-if="service.periodicity.length" class="svc-block enter" style="--i: 2">
        <h2 class="block-title">Cada cuánto</h2>
        <dl class="period-list card">
          <div v-for="p in service.periodicity" :key="p.label">
            <dt>{{ p.label }}</dt>
            <dd>{{ p.value }}</dd>
          </div>
        </dl>
        <p class="note">Los intervalos son orientativos: siempre tiene prioridad el plan de mantenimiento de tu fabricante.</p>
      </section>

      <aside class="note-card enter" style="--i: 3">
        <span class="note-ico"><Icon name="shield" :size="20" /></span>
        <p>{{ service.note }}</p>
      </aside>

      <div class="cta-band enter" style="--i: 4">
        <div>
          <h3>¿Te lo revisamos?</h3>
          <p>Reserva en un minuto, elige el hueco que mejor te venga y recibe la confirmación por email.</p>
        </div>
        <RouterLink :to="{ path: '/cita', query: { servicio: service.icon } }" class="btn btn-amber">
          <Icon name="calendar" :size="18" />Pedir cita
        </RouterLink>
      </div>

      <section class="svc-block enter" style="--i: 5">
        <h2 class="block-title">Otros servicios</h2>
        <div class="more-grid">
          <RouterLink v-for="s in others" :key="s.icon" :to="`/servicios/${s.icon}`" class="more-card">
            <span class="more-ico"><Icon :name="s.icon" :size="20" /></span>
            <span class="more-name">{{ s.title }}</span>
            <Icon name="arrow" :size="16" />
          </RouterLink>
        </div>
      </section>
    </div>
  </article>
</template>
