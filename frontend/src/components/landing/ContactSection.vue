<script setup lang="ts">
import { ref } from 'vue'
import { site } from '../../content/site'
import Icon from '../Icon.vue'

const c = site.contact
const loaded = ref(false)
const coords = `${c.address.lat},${c.address.lng}`
const embed = `https://maps.google.com/maps?q=${coords}&z=16&output=embed`
const route = `https://www.google.com/maps/search/?api=1&query=${coords}`
</script>

<template>
  <section id="contacto" class="lp-section">
    <div class="lp-wrap contact">
      <div class="reveal">
        <h2 class="lp-h2">Contacto</h2>
        <p class="lp-lead">Llámanos, escríbenos o pásate por el taller. Te atendemos con cita o sin ella.</p>

        <ul class="info-list">
          <li class="info"><span class="ico"><Icon name="pin" /></span><div><strong>Dirección</strong>{{ c.address.line }}<br />{{ c.address.city }}</div></li>
          <li class="info"><span class="ico"><Icon name="phone" /></span><div><strong>Teléfono</strong><a :href="c.phoneHref">{{ c.phone }}</a></div></li>
          <li class="info"><span class="ico"><Icon name="mail" /></span><div><strong>Email</strong><a :href="`mailto:${c.email}`">{{ c.email }}</a></div></li>
          <li class="info">
            <span class="ico"><Icon name="clock" /></span>
            <div>
              <strong>Horario</strong>
              <dl class="hours"><template v-for="h in c.hours" :key="h.days"><dt>{{ h.days }}</dt><dd>{{ h.time }}</dd></template></dl>
            </div>
          </li>
        </ul>

        <div class="contact-cta">
          <a :href="c.phoneHref" class="btn btn-primary"><Icon name="phone" :size="18" />Llamar</a>
          <a :href="route" class="btn btn-outline" target="_blank" rel="noopener"><Icon name="pin" :size="18" />Cómo llegar</a>
        </div>
      </div>

      <div class="map-card reveal" style="--d: 120ms">
        <iframe v-if="loaded" :src="embed" title="Ubicación del taller en Google Maps" loading="lazy" referrerpolicy="no-referrer-when-downgrade" allowfullscreen />
        <div v-else class="map-ph">
          <span class="pin"><Icon name="pin" :size="28" /></span>
          <strong>{{ c.address.line }}, {{ c.address.city }}</strong>
          <button type="button" class="btn btn-primary" @click="loaded = true">Cargar mapa interactivo</button>
          <p>Al cargar el mapa se conectará con Google Maps, que puede usar cookies.</p>
        </div>
      </div>
    </div>
  </section>
</template>
