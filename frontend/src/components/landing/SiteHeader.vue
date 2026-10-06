<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { site } from '../../content/site'
import { useAuth } from '../../composables/useAuth'
import { useTheme } from '../../composables/useTheme'
import Icon from '../Icon.vue'

const { theme, toggle } = useTheme()
const { user, isAdmin } = useAuth()
const route = useRoute()
const scrolled = ref(false)
const menu = ref(false)

const links = computed(() => [
  { to: '/servicios', label: 'Servicios' },
  { to: '/nosotros', label: 'Quiénes somos' },
  { to: '/contacto', label: 'Contacto' },
  ...(isAdmin.value ? [{ to: '/admin', label: 'Administración' }] : [])
])
const account = computed(() => (user.value ? { to: '/mi-cuenta', label: 'Mis citas' } : { to: '/acceso', label: 'Acceder' }))

const onScroll = () => { scrolled.value = window.scrollY > 16 }
onMounted(() => { onScroll(); window.addEventListener('scroll', onScroll, { passive: true }) })
onBeforeUnmount(() => window.removeEventListener('scroll', onScroll))
watch(() => route.fullPath, () => { menu.value = false })
</script>

<template>
  <header :class="['lp-nav', { scrolled: scrolled || menu }]">
    <div class="lp-wrap nav-in">
      <RouterLink to="/" class="brand">
        <span class="logo-mark"><Icon name="wrench" :size="20" /></span>
        <span class="brand-text">{{ site.brand.first }}<em>&nbsp;{{ site.brand.second }}</em></span>
      </RouterLink>
      <nav id="main-nav" :class="{ show: menu }" aria-label="Principal">
        <RouterLink v-for="l in links" :key="l.to" :to="l.to">{{ l.label }}</RouterLink>
        <RouterLink :to="account.to" class="nav-account-link">{{ account.label }}</RouterLink>
      </nav>
      <div class="nav-actions">
        <button type="button" class="icon-btn nav-btn" :aria-label="theme === 'dark' ? 'Cambiar a tema claro' : 'Cambiar a tema oscuro'" @click="toggle">
          <Icon :name="theme === 'dark' ? 'sun' : 'moon'" />
        </button>
        <RouterLink :to="account.to" class="icon-btn nav-btn nav-account" :aria-label="account.label" :title="account.label">
          <Icon name="user" />
        </RouterLink>
        <RouterLink to="/cita" class="btn btn-primary nav-cta">Pedir cita</RouterLink>
        <button type="button" class="icon-btn nav-btn burger" :aria-expanded="menu" aria-controls="main-nav" aria-label="Menú" @click="menu = !menu">
          <Icon :name="menu ? 'close' : 'menu'" />
        </button>
      </div>
    </div>
  </header>
</template>
