<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import { api, errMsg } from '../appApi'
import Icon from '../components/Icon.vue'

const route = useRoute()
const token = computed(() => (typeof route.query.token === 'string' ? route.query.token : ''))

const password = ref('')
const repeat = ref('')
const busy = ref(false)
const error = ref('')
const done = ref(false)

const match = computed(() => !repeat.value || repeat.value === password.value)
const strong = computed(() => password.value.length >= 10 && /[a-zA-Z]/.test(password.value) && /\d/.test(password.value))

async function submit() {
  if (busy.value || !strong.value || !match.value) return
  busy.value = true
  error.value = ''
  try {
    await api.resetPassword(token.value, password.value)
    done.value = true
  } catch (e) {
    error.value = errMsg(e)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <section class="page">
    <div class="lp-wrap auth-wrap">
      <div class="auth-card enter">
        <div v-if="done" class="auth-state">
          <span class="done-mark"><Icon name="check" :size="28" /></span>
          <h2>Contraseña cambiada</h2>
          <p>Ya puedes entrar con tu contraseña nueva. Por seguridad hemos cerrado las sesiones que tuvieras abiertas en otros dispositivos.</p>
          <RouterLink to="/acceso" class="btn btn-primary">Iniciar sesión</RouterLink>
        </div>

        <div v-else-if="!token" class="auth-state">
          <span class="state-ico bad"><Icon name="close" :size="26" /></span>
          <h2>Enlace incompleto</h2>
          <p>Ábrelo directamente desde el correo que te enviamos, sin copiarlo a trozos.</p>
          <RouterLink to="/acceso" class="btn btn-primary">Volver</RouterLink>
        </div>

        <template v-else>
          <h1 class="auth-title">Elige una contraseña nueva</h1>
          <p class="auth-sub">Este enlace solo se puede usar una vez.</p>

          <form class="stack" novalidate @submit.prevent="submit">
            <label class="field">Contraseña nueva
              <input v-model="password" type="password" autocomplete="new-password" required maxlength="100" />
            </label>
            <label class="field">Repite la contraseña
              <input v-model="repeat" type="password" autocomplete="new-password" required maxlength="100" />
            </label>
            <p v-if="!match" class="form-error" role="alert">Las dos contraseñas no coinciden.</p>
            <p class="note">Mínimo 10 caracteres, con letras y números.</p>

            <p v-if="error" class="form-error" role="alert">{{ error }}</p>
            <button class="primary" type="submit" :disabled="busy || !strong || !match">
              {{ busy ? 'Guardando…' : 'Guardar contraseña' }}
            </button>
            <RouterLink to="/acceso" class="link">Volver a la pantalla de acceso</RouterLink>
          </form>
        </template>
      </div>
    </div>
  </section>
</template>
