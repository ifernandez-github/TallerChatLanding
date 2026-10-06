import { nextTick, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'

let io: IntersectionObserver | null = null

/**
 * Anima la aparición de los elementos con clase .reveal al entrar en pantalla.
 * Es idempotente: solo observa los que aún no se han mostrado, así que se puede llamar
 * tantas veces como haga falta (al montar cada página, al cambiar de ruta...).
 */
export function observeReveals() {
  const els = document.querySelectorAll<HTMLElement>('.reveal:not(.in)')
  if (!els.length) return
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

export function useReveal() {
  const route = useRoute()
  // Red de seguridad: una página ya cargada que cambia solo de parámetro no se vuelve a montar.
  onMounted(() => nextTick(observeReveals))
  watch(() => route.fullPath, () => nextTick(observeReveals), { flush: 'post' })
}
