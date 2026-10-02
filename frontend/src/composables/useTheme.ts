import { ref, watchEffect } from 'vue'

type Theme = 'light' | 'dark'

const media = matchMedia('(prefers-color-scheme: dark)')
const theme = ref<Theme>(localStorage.getItem('theme') === 'dark' || (!localStorage.getItem('theme') && media.matches) ? 'dark' : 'light')

// Mientras el usuario no elija tema manualmente, sigue el del sistema.
media.addEventListener('change', (e) => {
  if (!localStorage.getItem('theme')) theme.value = e.matches ? 'dark' : 'light'
})
watchEffect(() => { document.documentElement.dataset.theme = theme.value })

export function useTheme() {
  function toggle() {
    theme.value = theme.value === 'dark' ? 'light' : 'dark'
    localStorage.setItem('theme', theme.value)
  }
  return { theme, toggle }
}
