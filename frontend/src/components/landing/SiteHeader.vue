<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { site } from '../../content/site'
import { useTheme } from '../../composables/useTheme'
import Icon from '../Icon.vue'

const { theme, toggle } = useTheme()
const scrolled = ref(false)
const menu = ref(false)
const links = [
  { href: '#quienes-somos', label: 'Quiénes somos' },
  { href: '#servicios', label: 'Servicios' },
  { href: '#contacto', label: 'Contacto' }
]
const onScroll = () => { scrolled.value = window.scrollY > 40 }
onMounted(() => { onScroll(); window.addEventListener('scroll', onScroll, { passive: true }) })
onBeforeUnmount(() => window.removeEventListener('scroll', onScroll))
</script>

<template>
  <header :class="['lp-nav', { scrolled, open: menu }]">
    <div class="lp-wrap nav-in">
      <a href="#inicio" class="brand"><span class="logo-mark"><Icon name="wrench" :size="20" /></span>{{ site.name }}</a>
      <nav id="main-nav" :class="{ show: menu }" aria-label="Principal">
        <a v-for="l in links" :key="l.href" :href="l.href" @click="menu = false">{{ l.label }}</a>
      </nav>
      <div class="nav-actions">
        <button type="button" class="icon-btn nav-btn" :aria-label="theme === 'dark' ? 'Cambiar a tema claro' : 'Cambiar a tema oscuro'" @click="toggle">
          <Icon :name="theme === 'dark' ? 'sun' : 'moon'" />
        </button>
        <a href="#contacto" class="btn btn-primary nav-cta">Pedir cita</a>
        <button type="button" class="icon-btn nav-btn burger" :aria-expanded="menu" aria-controls="main-nav" aria-label="Menú" @click="menu = !menu">
          <Icon :name="menu ? 'close' : 'menu'" />
        </button>
      </div>
    </div>
  </header>
</template>
