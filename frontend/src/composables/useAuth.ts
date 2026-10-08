import { computed, ref } from 'vue'
import { api, type RegisterResult, type User } from '../appApi'

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
  /**
   * Da de alta la cuenta. Normalmente NO inicia sesión: hay que confirmar el correo primero.
   * Solo entra directamente cuando el servidor no tiene configurado el envío de emails.
   */
  async function register(name: string, email: string, emailConfirm: string, phone: string, password: string): Promise<RegisterResult> {
    const result = await api.register(name, email, emailConfirm, phone, password)
    if (result.signedIn) user.value = await api.me()
    return result
  }

  /**
   * Cierra la sesión. Nunca propaga un error: aunque falle la llamada al servidor, la sesión local
   * se limpia igualmente para que la navegación posterior (volver al inicio) siempre ocurra.
   */
  async function logout() {
    try { await api.logout() } catch { /* la cookie caduca igualmente en el servidor */ }
    user.value = null
  }

  /** Refresca el usuario en memoria tras editar la ficha o los vehículos. */
  function setUser(u: User) { user.value = u }

  return { user, ready, isAdmin: computed(() => user.value?.role === 'admin'), load, login, register, logout, setUser }
}
