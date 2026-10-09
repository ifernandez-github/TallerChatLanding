<script setup lang="ts">
import { site } from '../../content/site'
</script>

<template>
  <footer class="lp-footer">
    <div class="lp-wrap foot-grid">
      <div>
        <strong class="foot-brand">{{ site.brand.first }}<em>&nbsp;{{ site.brand.second }}</em></strong>
        <p class="foot-note">{{ site.contact.address.line }}, {{ site.contact.address.city }}</p>
        <p class="foot-note">{{ site.contact.phone }} · {{ site.contact.email }}</p>
      </div>
      <nav class="foot-nav" aria-label="Pie de página">
        <RouterLink to="/servicios">Servicios</RouterLink>
        <RouterLink to="/nosotros">Quiénes somos</RouterLink>
        <RouterLink to="/contacto">Contacto</RouterLink>
        <RouterLink to="/cita">Pedir cita</RouterLink>
        <RouterLink to="/mi-cuenta">Mis citas</RouterLink>
        <a href="/privacidad.html">Política de privacidad</a>
        <a href="/terminos.html">Términos del servicio</a>
      </nav>
    </div>
    <div class="lp-wrap foot-legal">
      <span>© {{ new Date().getFullYear() }} {{ site.name }}. Todos los derechos reservados.</span>
      <span>Las respuestas del mecánico interactivo son orientativas y no sustituyen el diagnóstico de un profesional.</span>
    </div>
  </footer>
</template>
