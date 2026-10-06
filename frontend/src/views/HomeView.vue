<script setup lang="ts">
import AboutSection from '../components/landing/AboutSection.vue'
import ContactSection from '../components/landing/ContactSection.vue'
import HeroSection from '../components/landing/HeroSection.vue'
import ServicesSection from '../components/landing/ServicesSection.vue'
import Icon from '../components/Icon.vue'
</script>

<template>
  <HeroSection />
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
