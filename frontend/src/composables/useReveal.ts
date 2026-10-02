import { onMounted } from 'vue'

// Anima la aparición de los elementos con clase .reveal al entrar en pantalla.
export function useReveal() {
  onMounted(() => {
    const els = document.querySelectorAll<HTMLElement>('.reveal')
    if (!('IntersectionObserver' in window) || matchMedia('(prefers-reduced-motion: reduce)').matches) {
      els.forEach((e) => e.classList.add('in'))
      return
    }
    const io = new IntersectionObserver(
      (entries) => entries.forEach((en) => {
        if (en.isIntersecting) { en.target.classList.add('in'); io.unobserve(en.target) }
      }),
      { threshold: 0.15 }
    )
    els.forEach((e) => io.observe(e))
  })
}
