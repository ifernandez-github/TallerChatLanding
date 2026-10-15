<script setup lang="ts">
import Icon from './Icon.vue'

const items = [
  { to: '/admin', label: 'Agenda y usuarios', icon: 'calendar' },
  { to: '/admin/estadisticas/clientes', label: 'Nuevos clientes', icon: 'users' },
  { to: '/admin/estadisticas/citas', label: 'Estadísticas de citas', icon: 'chart' }
]
</script>

<template>
  <nav class="stats-nav enter" aria-label="Secciones de gestión">
    <RouterLink v-for="i in items" :key="i.to" :to="i.to" exact-active-class="on"><Icon :name="i.icon" :size="16" />{{ i.label }}</RouterLink>
  </nav>
</template>
