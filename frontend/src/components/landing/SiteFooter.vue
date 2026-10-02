<script setup lang="ts">
import { site } from '../../content/site'
</script>

<template>
  <footer class="lp-footer">
    <div class="lp-wrap wrap-row">
      <span>© {{ new Date().getFullYear() }} {{ site.name }}. Todos los derechos reservados.</span>
      <span>Las respuestas del mecánico interactivo son orientativas y no sustituyen el diagnóstico de un profesional.</span>
    </div>
  </footer>
</template>
