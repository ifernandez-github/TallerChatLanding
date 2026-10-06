<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api, errMsg, type Appointment } from '../appApi'
import { getFeatures } from '../api'
import Icon from '../components/Icon.vue'
import { useAuth } from '../composables/useAuth'
import { fmtDate, fmtTime, statusLabels } from '../format'

const router = useRouter()
const { user, logout } = useAuth()

const items = ref<Appointment[]>([])
const loading = ref(true)
const error = ref('')
const confirmId = ref('')
const cancelling = ref('')
const emailEnabled = ref(false)
const emailBusy = ref(false)
const emailMsg = ref<{ ok: boolean; text: string } | null>(null)

const upcoming = computed(() =>
  items.value.filter((a) => (a.status === 'pending' || a.status === 'confirmed') && new Date(a.start) > new Date())
    .sort((a, b) => a.start.localeCompare(b.start)))
const history = computed(() => {
  const ids = new Set(upcoming.value.map((a) => a.id))
  return items.value.filter((a) => !ids.has(a.id)).sort((a, b) => b.start.localeCompare(a.start))
})

async function load() {
  loading.value = true
  error.value = ''
  try { items.value = await api.myAppointments() } catch (e) { error.value = errMsg(e) } finally { loading.value = false }
}

async function cancel(a: Appointment) {
  cancelling.value = a.id
  error.value = ''
  try {
    const res = await api.cancel(a.id)
    items.value = items.value.map((x) => (x.id === a.id ? res.appointment : x))
    confirmId.value = ''
  } catch (e) {
    error.value = errMsg(e)
    await load()
  } finally {
    cancelling.value = ''
  }
}

async function emailMine() {
  emailBusy.value = true
  emailMsg.value = null
  try {
    await api.emailMine()
    emailMsg.value = { ok: true, text: `Te hemos enviado tus próximas citas a ${user.value?.email}.` }
  } catch (e) {
    emailMsg.value = { ok: false, text: errMsg(e) }
  } finally {
    emailBusy.value = false
  }
}

async function signOut() {
  await logout()
  await router.push('/')
}

onMounted(async () => {
  emailEnabled.value = (await getFeatures()).email
  await load()
})
</script>

<template>
  <section class="page">
    <div class="lp-wrap narrow">
      <header class="page-head head-row enter">
        <div>
          <p class="eyebrow">Área de cliente</p>
          <h1 class="page-title">Hola, {{ user?.name.split(' ')[0] }}</h1>
          <p class="lp-lead">Aquí ves tus citas, puedes cancelarlas y recibirlas por email.</p>
        </div>
        <div class="row-gap">
          <RouterLink to="/cita" class="btn btn-primary"><Icon name="calendar" :size="18" />Nueva cita</RouterLink>
          <button type="button" class="btn btn-outline" @click="signOut"><Icon name="logout" :size="18" />Salir</button>
        </div>
      </header>

      <p v-if="error" class="form-error" role="alert">{{ error }}</p>

      <div class="block-head enter" style="--i: 1">
        <h2>Próximas citas</h2>
        <button v-if="emailEnabled && upcoming.length" type="button" class="chip-btn" :disabled="emailBusy" @click="emailMine">
          <Icon name="mail" :size="16" />{{ emailBusy ? 'Enviando…' : 'Enviármelas por email' }}
        </button>
      </div>
      <p v-if="emailMsg" :class="emailMsg.ok ? 'ok-note' : 'form-error'" role="status">{{ emailMsg.text }}</p>

      <p v-if="loading" class="muted">Cargando tus citas…</p>
      <div v-else-if="!upcoming.length" class="card empty-card enter" style="--i: 2">
        <Icon name="calendar" :size="28" />
        <p>No tienes citas próximas.</p>
        <RouterLink to="/cita" class="btn btn-primary">Pedir cita</RouterLink>
      </div>
      <ul v-else class="appt-list">
        <li v-for="(a, i) in upcoming" :key="a.id" class="appt card enter" :style="{ '--i': i + 2 }">
          <div class="appt-date">
            <strong>{{ fmtDate(a.start) }}</strong>
            <span>{{ fmtTime(a.start) }} h</span>
          </div>
          <div class="appt-body">
            <p class="appt-svc">{{ a.serviceName }}</p>
            <p class="appt-meta"><Icon name="car" :size="15" />{{ a.vehicle }} · {{ a.plate }}</p>
            <p v-if="a.notes" class="appt-notes">{{ a.notes }}</p>
          </div>
          <div class="appt-side">
            <span :class="['pill', a.status]">{{ statusLabels[a.status] }}</span>
            <template v-if="a.canCancel">
              <button v-if="confirmId !== a.id" type="button" class="link danger" @click="confirmId = a.id">Cancelar cita</button>
              <span v-else class="confirm-row">
                ¿Seguro?
                <button type="button" class="link danger" :disabled="cancelling === a.id" @click="cancel(a)">Sí, cancelar</button>
                <button type="button" class="link" @click="confirmId = ''">No</button>
              </span>
            </template>
          </div>
        </li>
      </ul>

      <details v-if="history.length" class="history enter" style="--i: 3">
        <summary><span>Historial</span><span class="count">{{ history.length }}</span></summary>
        <ul class="hist-list">
          <li v-for="a in history" :key="a.id">
            <div>
              <strong>{{ a.serviceName }}</strong>
              <span class="muted">{{ fmtDate(a.start) }}, {{ fmtTime(a.start) }} · {{ a.plate }}</span>
            </div>
            <span :class="['pill', a.status]">{{ statusLabels[a.status] }}</span>
          </li>
        </ul>
      </details>

      <div class="card profile enter" style="--i: 4">
        <h2>Mis datos</h2>
        <dl class="detail">
          <div><dt>Nombre</dt><dd>{{ user?.name }}</dd></div>
          <div><dt>Correo</dt><dd>{{ user?.email }}</dd></div>
          <div v-if="user?.phone"><dt>Teléfono</dt><dd>{{ user.phone }}</dd></div>
        </dl>
      </div>
    </div>
  </section>
</template>
