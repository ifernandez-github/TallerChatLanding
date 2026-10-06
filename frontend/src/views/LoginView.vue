<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { errMsg } from '../appApi'
import { useAuth } from '../composables/useAuth'

const route = useRoute()
const router = useRouter()
const { login, register } = useAuth()

const mode = ref<'login' | 'register'>(route.query.modo === 'registro' ? 'register' : 'login')
const name = ref('')
const email = ref('')
const phone = ref('')
const password = ref('')
const busy = ref(false)
const error = ref('')

// Solo se redirige a rutas internas (evita redirecciones abiertas a otros sitios).
const target = computed(() => {
  const r = route.query.redirect
  return typeof r === 'string' && r.startsWith('/') && !r.startsWith('//') ? r : null
})

async function submit() {
  if (busy.value) return
  busy.value = true
  error.value = ''
  try {
    if (mode.value === 'login') await login(email.value, password.value)
    else await register(name.value, email.value, phone.value, password.value)
    await router.replace(target.value ?? '/mi-cuenta')
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
        <div class="tabs" role="tablist" aria-label="Acceso">
          <button type="button" role="tab" :aria-selected="mode === 'login'" :class="{ on: mode === 'login' }" @click="mode = 'login'; error = ''">Entrar</button>
          <button type="button" role="tab" :aria-selected="mode === 'register'" :class="{ on: mode === 'register' }" @click="mode = 'register'; error = ''">Crear cuenta</button>
        </div>

        <h1 class="auth-title">{{ mode === 'login' ? 'Accede a tus citas' : 'Crea tu cuenta' }}</h1>
        <p class="auth-sub">
          {{ mode === 'login' ? 'Consulta y gestiona tus citas del taller.' : 'Con una cuenta puedes pedir cita, verla en tu área y recibirla por email.' }}
        </p>

        <form class="stack" novalidate @submit.prevent="submit">
          <label v-if="mode === 'register'" class="field">Nombre
            <input v-model="name" type="text" autocomplete="name" required maxlength="80" />
          </label>
          <label class="field">Correo electrónico
            <input v-model="email" type="email" autocomplete="email" inputmode="email" required maxlength="254" />
          </label>
          <label v-if="mode === 'register'" class="field">Teléfono <span class="muted">(opcional)</span>
            <input v-model="phone" type="tel" autocomplete="tel" maxlength="20" />
          </label>
          <label class="field">Contraseña
            <input v-model="password" type="password" :autocomplete="mode === 'login' ? 'current-password' : 'new-password'" required maxlength="100" />
          </label>
          <p v-if="mode === 'register'" class="note">Mínimo 10 caracteres, con letras y números.</p>

          <p v-if="error" class="form-error" role="alert">{{ error }}</p>
          <button class="primary" type="submit" :disabled="busy">{{ busy ? 'Un momento…' : mode === 'login' ? 'Entrar' : 'Crear cuenta' }}</button>
        </form>
      </div>
    </div>
  </section>
</template>
