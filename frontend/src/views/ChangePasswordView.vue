<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api, errMsg } from '../appApi'
import { useAuth } from '../composables/useAuth'

const router = useRouter()
const { user, setUser, logout } = useAuth()

const current = ref('')
const password = ref('')
const repeat = ref('')
const busy = ref(false)
const error = ref('')

const match = computed(() => !repeat.value || repeat.value === password.value)
const strong = computed(() => password.value.length >= 10 && /[a-zA-Z]/.test(password.value) && /\d/.test(password.value))

async function submit() {
  if (busy.value || !strong.value || !match.value || !repeat.value) return
  busy.value = true
  error.value = ''
  try {
    setUser(await api.changePassword(current.value, password.value))
    await router.replace(user.value?.role === 'client' ? '/mi-cuenta' : '/admin')
  } catch (e) {
    error.value = errMsg(e)
  } finally {
    busy.value = false
  }
}

async function leave() {
  await logout()
  await router.replace('/')
}
</script>

<template>
  <section class="page">
    <div class="lp-wrap auth-wrap">
      <div class="auth-card enter">
        <h1 class="auth-title">Elige tu contraseña</h1>
        <p class="auth-sub">
          Has entrado con la contraseña provisional que te dio el taller. Cámbiala por una tuya para poder seguir.
        </p>

        <form class="stack" novalidate @submit.prevent="submit">
          <label class="field">Contraseña provisional
            <input v-model="current" type="password" autocomplete="current-password" required maxlength="100" />
          </label>
          <label class="field">Contraseña nueva
            <input v-model="password" type="password" autocomplete="new-password" required maxlength="100" />
          </label>
          <label class="field">Repite la contraseña nueva
            <input v-model="repeat" type="password" autocomplete="new-password" required maxlength="100" />
          </label>
          <p v-if="!match" class="form-error" role="alert">Las dos contraseñas no coinciden.</p>
          <p class="note">Mínimo 10 caracteres, con letras y números.</p>

          <p v-if="error" class="form-error" role="alert">{{ error }}</p>
          <button class="primary" type="submit" :disabled="busy || !current || !strong || !match || !repeat">
            {{ busy ? 'Guardando…' : 'Guardar contraseña' }}
          </button>
          <button type="button" class="link" @click="leave">Salir</button>
        </form>
      </div>
    </div>
  </section>
</template>
