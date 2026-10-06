import { onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'

let io: IntersectionObserver | null = null

// Anima la aparición de los elementos con clase .reveal al entrar en pantalla.
export function observeReveals() {
  const els = document.querySelectorAll<HTMLElement>('.reveal:not(.in)')
  if (!('IntersectionObserver' in window) || matchMedia('(prefers-reduced-motion: reduce)').matches) {
    els.forEach((e) => e.classList.add('in'))
    return
  }
  io ??= new IntersectionObserver(
    (entries) => entries.forEach((en) => {
      if (en.isIntersecting) { en.target.classList.add('in'); io?.unobserve(en.target) }
    }),
    { threshold: 0.15 }
  )
  els.forEach((e) => io!.observe(e))
}

// Con router, cada página trae sus propios .reveal: se vuelven a observar tras cada cambio de ruta.
export function useReveal() {
  const route = useRoute()
  onMounted(observeReveals)
  watch(() => route.fullPath, observeReveals, { flush: 'post' })
}
