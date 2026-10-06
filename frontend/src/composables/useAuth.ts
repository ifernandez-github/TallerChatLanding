import { computed, ref } from 'vue'
import { api, type User } from '../appApi'

// Estado compartido de la sesión.
const user = ref<User | null>(null)
const ready = ref(false)
let loading: Promise<void> | null = null

export function useAuth() {
  /** Consulta la sesión una sola vez (las siguientes llamadas reutilizan el resultado). */
  function load(): Promise<void> {
    loading ??= api.me()
      .then((u) => { user.value = u })
      .catch(() => { user.value = null })
      .finally(() => { ready.value = true })
    return loading
  }

  async function login(email: string, password: string) { user.value = await api.login(email, password) }
  async function register(name: string, email: string, phone: string, password: string) {
    user.value = await api.register(name, email, phone, password)
  }
  async function logout() {
    try { await api.logout() } finally { user.value = null }
  }

  return { user, ready, isAdmin: computed(() => user.value?.role === 'admin'), load, login, register, logout }
}
