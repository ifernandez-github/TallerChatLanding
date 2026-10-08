<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { api, errMsg } from '../appApi'
import Icon from '../components/Icon.vue'

const route = useRoute()
const state = ref<'checking' | 'ok' | 'error'>('checking')
const error = ref('')
const email = ref('')
const resendEmail = ref('')
const resendBusy = ref(false)
const resendMsg = ref('')

async function resend() {
  resendBusy.value = true
  resendMsg.value = ''
  try {
    await api.resendVerification(resendEmail.value)
    resendMsg.value = 'Si esa dirección tiene una cuenta sin confirmar, le acabamos de enviar un enlace nuevo.'
  } catch (e) {
    resendMsg.value = errMsg(e)
  } finally {
    resendBusy.value = false
  }
}

onMounted(async () => {
  const token = route.query.token
  if (typeof token !== 'string' || !token) {
    state.value = 'error'
    error.value = 'Este enlace está incompleto. Ábrelo directamente desde el correo que te enviamos.'
    return
  }
  try {
    const result = await api.verifyEmail(token)
    email.value = result.email
    state.value = 'ok'
  } catch (e) {
    error.value = errMsg(e)
    state.value = 'error'
  }
})
</script>

<template>
  <section class="page">
    <div class="lp-wrap auth-wrap">
      <div class="auth-card enter">
        <div v-if="state === 'checking'" class="auth-state">
          <span class="state-ico"><Icon name="clock" :size="26" /></span>
          <h2>Comprobando el enlace…</h2>
        </div>

        <div v-else-if="state === 'ok'" class="auth-state">
          <span class="done-mark"><Icon name="check" :size="28" /></span>
          <h2>¡Cuenta confirmada!</h2>
          <p>Ya puedes iniciar sesión con <strong>{{ email }}</strong> y pedir tu primera cita.</p>
          <RouterLink to="/acceso" class="btn btn-primary">Iniciar sesión</RouterLink>
        </div>

        <div v-else class="auth-state">
          <span class="state-ico bad"><Icon name="close" :size="26" /></span>
          <h2>No hemos podido confirmar la cuenta</h2>
          <p>{{ error }}</p>
          <form class="stack" style="width: 100%; text-align: left" novalidate @submit.prevent="resend">
            <label class="field">Pide un enlace nuevo
              <input v-model="resendEmail" type="email" inputmode="email" required maxlength="254" placeholder="tu@correo.com" />
            </label>
            <p v-if="resendMsg" class="ok-note" role="status">{{ resendMsg }}</p>
            <button class="primary" type="submit" :disabled="resendBusy">{{ resendBusy ? 'Enviando…' : 'Enviarme otro enlace' }}</button>
          </form>
          <RouterLink to="/acceso" class="link">Volver a la pantalla de acceso</RouterLink>
        </div>
      </div>
    </div>
  </section>
</template>
