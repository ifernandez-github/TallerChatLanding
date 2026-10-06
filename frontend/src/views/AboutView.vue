<script setup lang="ts">
import AboutSection from '../components/landing/AboutSection.vue'
import Icon from '../components/Icon.vue'
</script>

<template>
  <div class="sub-page">
    <AboutSection />
    <section class="lp-section alt">
      <div class="lp-wrap">
        <div class="cta-band reveal">
          <div>
            <h3>Conoce el taller en persona</h3>
            <p>Pide cita y te enseñamos la avería, la pieza y el presupuesto antes de empezar.</p>
          </div>
          <RouterLink to="/cita" class="btn btn-amber"><Icon name="calendar" :size="18" />Pedir cita</RouterLink>
        </div>
      </div>
    </section>
  </div>
</template>
