import { onBeforeUnmount, ref, watch } from 'vue'

const reduced = () => typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches

/** Número que «sube» hasta su valor cada vez que cambia el origen (sin movimiento si el usuario lo ha desactivado). */
export function useCountUp(source: () => number, decimals = 0, ms = 900) {
  const shown = ref(0)
  let raf = 0
  const round = (n: number) => Number(n.toFixed(decimals))

  watch(source, (to) => {
    cancelAnimationFrame(raf)
    const from = shown.value
    if (reduced() || from === to) { shown.value = round(to); return }
    const t0 = performance.now()
    const step = (t: number) => {
      const k = Math.min(1, (t - t0) / ms)
      shown.value = round(from + (to - from) * (1 - Math.pow(1 - k, 3)))
      if (k < 1) raf = requestAnimationFrame(step)
    }
    raf = requestAnimationFrame(step)
  }, { immediate: true })

  onBeforeUnmount(() => cancelAnimationFrame(raf))
  return shown
}
