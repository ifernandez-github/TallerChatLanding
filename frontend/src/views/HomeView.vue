<script setup lang="ts">
import AboutSection from '../components/landing/AboutSection.vue'
import ContactSection from '../components/landing/ContactSection.vue'
import HeroSection from '../components/landing/HeroSection.vue'
import ServicesSection from '../components/landing/ServicesSection.vue'
import Icon from '../components/Icon.vue'

const trust = [
  { icon: 'shield', strong: 'Garantía de 24 meses', text: 'en mano de obra y recambios' },
  { icon: 'check-list', strong: 'Presupuesto cerrado', text: 'antes de tocar nada' },
  { icon: 'clock', strong: 'Cita en 24 horas', text: 'reservable online' },
  { icon: 'bolt', strong: 'Híbridos y eléctricos', text: 'técnicos de alta tensión' }
]
</script>

<template>
  <HeroSection />

  <div class="trust-strip">
    <div class="lp-wrap trust-in">
      <div v-for="t in trust" :key="t.strong" class="trust-item">
        <Icon :name="t.icon" :size="22" />
        <span><strong>{{ t.strong }}</strong> {{ t.text }}</span>
      </div>
    </div>
  </div>

  <AboutSection />
  <ServicesSection />

  <section class="lp-section book-band">
    <div class="lp-wrap">
      <div class="cta-band reveal">
        <div>
          <h3>Reserva tu cita online</h3>
          <p>Elige servicio, día y hora. Te enviamos la confirmación por email y la ves en tu área de cliente.</p>
        </div>
        <RouterLink to="/cita" class="btn btn-amber"><Icon name="calendar" :size="18" />Pedir cita</RouterLink>
      </div>
    </div>
  </section>

  <ContactSection />
</template>
