import { ref } from 'vue'

// Estado compartido: cualquier parte de la web puede abrir el mecánico interactivo.
const open = ref(false)

export function useChat() {
  return {
    open,
    openChat: () => { open.value = true },
    close: () => { open.value = false },
    toggle: () => { open.value = !open.value }
  }
}
