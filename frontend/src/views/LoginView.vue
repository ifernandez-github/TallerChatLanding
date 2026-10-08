<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, ApiError, ERR_NOT_VERIFIED, errMsg } from '../appApi'
import { getFeatures } from '../api'
import Icon from '../components/Icon.vue'
import { useAuth } from '../composables/useAuth'

const route = useRoute()
const router = useRouter()
const { login, register } = useAuth()

type Mode = 'login' | 'register' | 'forgot'
type Panel = 'registered' | 'forgot-sent'

const mode = ref<Mode>(route.query.modo === 'registro' ? 'register' : 'login')
const panel = ref<Panel | null>(null)

const name = ref('')
const email = ref('')
const emailConfirm = ref('')
const phone = ref('')
const password = ref('')
const busy = ref(false)
const error = ref('')
const notVerified = ref(false)
const resendMsg = ref('')
const resendBusy = ref(false)
const emailEnabled = ref(true)

// Solo se redirige a rutas internas (evita redirecciones abiertas a otros sitios).
const target = computed(() => {
  const r = route.query.redirect
  return typeof r === 'string' && r.startsWith('/') && !r.startsWith('//') ? r : null
})
const emailsMatch = computed(() => !emailConfirm.value || emailConfirm.value.trim().toLowerCase() === email.value.trim().toLowerCase())

function switchTo(next: Mode) {
  mode.value = next
  error.value = ''
  notVerified.value = false
  resendMsg.value = ''
}

async function submit() {
  if (busy.value) return
  busy.value = true
  error.value = ''
  notVerified.value = false
  resendMsg.value = ''
  try {
    if (mode.value === 'login') {
      await login(email.value, password.value)
      await router.replace(target.value ?? '/mi-cuenta')
    } else if (mode.value === 'register') {
      if (!emailsMatch.value) { error.value = 'Los dos correos no coinciden.'; return }
      const result = await register(name.value, email.value, emailConfirm.value, phone.value, password.value)
      if (result.signedIn) await router.replace(target.value ?? '/mi-cuenta')
      else panel.value = 'registered'
    } else {
      await api.forgotPassword(email.value)
      panel.value = 'forgot-sent'
    }
  } catch (e) {
    error.value = errMsg(e)
    if (e instanceof ApiError && e.code === ERR_NOT_VERIFIED) notVerified.value = true
  } finally {
    busy.value = false
  }
}

async function resend() {
  resendBusy.value = true
  resendMsg.value = ''
  try {
    await api.resendVerification(email.value)
    resendMsg.value = 'Si esa dirección tiene una cuenta sin confirmar, le acabamos de enviar un enlace nuevo.'
  } catch (e) {
    resendMsg.value = errMsg(e)
  } finally {
    resendBusy.value = false
  }
}

onMounted(async () => { emailEnabled.value = (await getFeatures()).email })
</script>

<template>
  <section class="page">
    <div class="lp-wrap auth-wrap">
      <div class="auth-card enter">
        <!-- Cuenta creada: falta confirmar el correo -->
        <div v-if="panel === 'registered'" class="auth-state">
          <span class="state-ico"><Icon name="mail" :size="26" /></span>
          <h2>Revisa tu correo</h2>
          <p>Hemos enviado un enlace de confirmación a <strong>{{ email }}</strong>. Pulsa en él y ya podrás entrar.</p>
          <p class="note">¿No lo ves? Mira en la carpeta de spam. El enlace caduca en 48 horas.</p>
          <p v-if="resendMsg" class="ok-note" role="status">{{ resendMsg }}</p>
          <div class="row-gap">
            <button type="button" class="btn btn-outline" :disabled="resendBusy" @click="resend">
              {{ resendBusy ? 'Enviando…' : 'Reenviar el correo' }}
            </button>
            <button type="button" class="btn btn-primary" @click="panel = null; switchTo('login')">Ir a iniciar sesión</button>
          </div>
        </div>

        <!-- Enlace de contraseña enviado -->
        <div v-else-if="panel === 'forgot-sent'" class="auth-state">
          <span class="state-ico"><Icon name="mail" :size="26" /></span>
          <h2>Mira tu bandeja de entrada</h2>
          <p>Si <strong>{{ email }}</strong> tiene una cuenta, le hemos enviado un enlace para elegir una contraseña nueva.</p>
          <p class="note">El enlace caduca en una hora y solo se puede usar una vez.</p>
          <button type="button" class="btn btn-primary" @click="panel = null; switchTo('login')">Volver</button>
        </div>

        <!-- Formularios -->
        <template v-else>
          <div v-if="mode !== 'forgot'" class="tabs" role="tablist" aria-label="Acceso">
            <button type="button" role="tab" :aria-selected="mode === 'login'" :class="{ on: mode === 'login' }" @click="switchTo('login')">Entrar</button>
            <button type="button" role="tab" :aria-selected="mode === 'register'" :class="{ on: mode === 'register' }" @click="switchTo('register')">Crear cuenta</button>
          </div>

          <h1 class="auth-title">
            {{ mode === 'login' ? 'Accede a tus citas' : mode === 'register' ? 'Crea tu cuenta' : '¿Has olvidado la contraseña?' }}
          </h1>
          <p class="auth-sub">
            {{ mode === 'login' ? 'Consulta y gestiona tus citas del taller.'
              : mode === 'register' ? 'Con una cuenta puedes pedir cita, verla en tu área y recibirla por email.'
              : 'Escribe tu correo y te enviaremos un enlace para elegir una nueva.' }}
          </p>

          <form class="stack" novalidate @submit.prevent="submit">
            <label v-if="mode === 'register'" class="field">Nombre
              <input v-model="name" type="text" autocomplete="name" required maxlength="80" />
            </label>

            <label class="field">Correo electrónico
              <input v-model="email" type="email" autocomplete="email" inputmode="email" required maxlength="254" />
            </label>

            <template v-if="mode === 'register'">
              <label class="field">Repite el correo electrónico
                <input v-model="emailConfirm" type="email" autocomplete="off" inputmode="email" required maxlength="254"
                  spellcheck="false" @paste.prevent />
              </label>
              <p v-if="!emailsMatch" class="form-error" role="alert">Los dos correos no coinciden.</p>
              <label class="field">Teléfono <span class="muted">(opcional)</span>
                <input v-model="phone" type="tel" autocomplete="tel" maxlength="20" />
              </label>
            </template>

            <label v-if="mode !== 'forgot'" class="field">Contraseña
              <input v-model="password" type="password" :autocomplete="mode === 'login' ? 'current-password' : 'new-password'" required maxlength="100" />
            </label>
            <p v-if="mode === 'register'" class="note">Mínimo 10 caracteres, con letras y números.</p>

            <p v-if="error" class="form-error" role="alert">{{ error }}</p>

            <!-- La cuenta existe pero no ha confirmado el correo -->
            <template v-if="notVerified">
              <button type="button" class="btn btn-outline" :disabled="resendBusy" @click="resend">
                <Icon name="mail" :size="17" />{{ resendBusy ? 'Enviando…' : 'Reenviar el correo de confirmación' }}
              </button>
              <p v-if="resendMsg" class="ok-note" role="status">{{ resendMsg }}</p>
            </template>

            <button class="primary" type="submit" :disabled="busy || (mode === 'register' && !emailsMatch)">
              {{ busy ? 'Un momento…' : mode === 'login' ? 'Entrar' : mode === 'register' ? 'Crear cuenta' : 'Enviarme el enlace' }}
            </button>

            <div class="auth-foot">
              <button v-if="mode === 'login' && emailEnabled" type="button" class="link" @click="switchTo('forgot')">
                ¿Has olvidado la contraseña?
              </button>
              <button v-if="mode === 'forgot'" type="button" class="link" @click="switchTo('login')">Volver a iniciar sesión</button>
            </div>
          </form>
        </template>
      </div>
    </div>
  </section>
</template>
