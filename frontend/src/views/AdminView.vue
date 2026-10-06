<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import {
  api, errMsg, type AdminAppointment, type AdminUser, type Status, type Summary
} from '../appApi'
import Icon from '../components/Icon.vue'
import { useAuth } from '../composables/useAuth'
import { addDays, fmtDate, fmtTime, statusLabels, todayIso } from '../format'

const { user } = useAuth()

const tab = ref<'agenda' | 'users'>('agenda')
const date = ref(todayIso())
const onlyPending = ref(false)
const items = ref<AdminAppointment[]>([])
const users = ref<AdminUser[]>([])
const summary = ref<Summary | null>(null)
const loading = ref(false)
const error = ref('')
const notice = ref('')
const acting = ref('')

const actions = (a: AdminAppointment): { to: Status; label: string; danger?: boolean }[] =>
  a.status === 'pending'
    ? [{ to: 'confirmed', label: 'Confirmar' }, { to: 'cancelled', label: 'Cancelar', danger: true }]
    : a.status === 'confirmed'
      ? [{ to: 'completed', label: 'Completar' }, { to: 'cancelled', label: 'Cancelar', danger: true }]
      : []

async function loadSummary() {
  try { summary.value = await api.adminSummary() } catch { /* el resumen es opcional */ }
}

async function loadAgenda() {
  loading.value = true
  error.value = ''
  try {
    items.value = await api.adminAppointments(onlyPending.value ? { pending: true } : { date: date.value })
  } catch (e) {
    error.value = errMsg(e)
  } finally {
    loading.value = false
  }
}

async function loadUsers() {
  loading.value = true
  error.value = ''
  try { users.value = await api.adminUsers() } catch (e) { error.value = errMsg(e) } finally { loading.value = false }
}

async function setStatus(a: AdminAppointment, to: Status) {
  acting.value = a.id
  error.value = ''
  notice.value = ''
  try {
    const res = await api.adminSetStatus(a.id, to)
    if (to === 'confirmed' || to === 'cancelled') {
      notice.value = res.emailSent ? `Se ha avisado por email a ${a.userName}.` : 'Estado actualizado (no se envió email: SMTP no configurado o cliente de ejemplo).'
    }
    await Promise.all([loadAgenda(), loadSummary()])
  } catch (e) {
    error.value = errMsg(e)
  } finally {
    acting.value = ''
  }
}

async function toggleActive(u: AdminUser) {
  acting.value = u.id
  error.value = ''
  try {
    await api.adminSetActive(u.id, !u.active)
    u.active = !u.active
  } catch (e) {
    error.value = errMsg(e)
  } finally {
    acting.value = ''
  }
}

const shift = (n: number) => { onlyPending.value = false; date.value = addDays(date.value, n) }

watch([date, onlyPending], loadAgenda)
watch(tab, (t) => { notice.value = ''; if (t === 'users') loadUsers(); else loadAgenda() })
onMounted(() => { loadAgenda(); loadSummary() })
</script>

<template>
  <section class="page">
    <div class="lp-wrap">
      <header class="page-head enter">
        <p class="eyebrow">Administración</p>
        <h1 class="page-title">Agenda y clientes</h1>
        <p class="lp-lead">Confirma las solicitudes, sigue el día del taller y gestiona las cuentas de clientes.</p>
      </header>

      <div class="stat-row enter" style="--i: 1">
        <div class="stat"><strong>{{ summary?.pending ?? '–' }}</strong><span>pendientes de confirmar</span></div>
        <div class="stat"><strong>{{ summary?.today ?? '–' }}</strong><span>citas hoy</span></div>
        <div class="stat"><strong>{{ summary?.users ?? '–' }}</strong><span>clientes</span></div>
      </div>

      <div class="tabs wide" role="tablist" aria-label="Secciones">
        <button type="button" role="tab" :aria-selected="tab === 'agenda'" :class="{ on: tab === 'agenda' }" @click="tab = 'agenda'">
          <Icon name="calendar" :size="16" />Agenda
        </button>
        <button type="button" role="tab" :aria-selected="tab === 'users'" :class="{ on: tab === 'users' }" @click="tab = 'users'">
          <Icon name="users" :size="16" />Usuarios
        </button>
      </div>

      <p v-if="error" class="form-error" role="alert">{{ error }}</p>
      <p v-if="notice" class="ok-note" role="status">{{ notice }}</p>

      <!-- Agenda -->
      <template v-if="tab === 'agenda'">
        <div class="toolbar">
          <div class="date-nav">
            <button type="button" class="icon-btn" aria-label="Día anterior" @click="shift(-1)"><Icon name="arrow" :size="18" style="transform: rotate(180deg)" /></button>
            <input v-model="date" type="date" aria-label="Fecha" @change="onlyPending = false" />
            <button type="button" class="icon-btn" aria-label="Día siguiente" @click="shift(1)"><Icon name="arrow" :size="18" /></button>
            <button type="button" class="chip-btn" @click="shift(0); date = todayIso()">Hoy</button>
          </div>
          <label class="check"><input v-model="onlyPending" type="checkbox" />Solo pendientes (todas las fechas)</label>
        </div>

        <p v-if="loading" class="muted">Cargando…</p>
        <div v-else-if="!items.length" class="card empty-card">
          <Icon name="calendar" :size="26" />
          <p>{{ onlyPending ? 'No hay solicitudes pendientes.' : 'No hay citas ese día.' }}</p>
        </div>
        <ul v-else class="appt-list">
          <li v-for="a in items" :key="a.id" class="appt card admin-appt">
            <div class="appt-date">
              <strong>{{ onlyPending ? fmtDate(a.start) : `${fmtTime(a.start)} h` }}</strong>
              <span>{{ onlyPending ? `${fmtTime(a.start)} h` : `Elevador ${a.bay}` }}</span>
            </div>
            <div class="appt-body">
              <p class="appt-svc">{{ a.serviceName }}</p>
              <p class="appt-meta"><Icon name="car" :size="15" />{{ a.vehicle }} · {{ a.plate }}</p>
              <p class="appt-meta"><Icon name="user" :size="15" />{{ a.userName }}<template v-if="a.userPhone"> · {{ a.userPhone }}</template> · <a :href="`mailto:${a.userEmail}`">{{ a.userEmail }}</a></p>
              <p v-if="a.notes" class="appt-notes">{{ a.notes }}</p>
            </div>
            <div class="appt-side">
              <span :class="['pill', a.status]">{{ statusLabels[a.status] }}</span>
              <div class="act-row">
                <button v-for="act in actions(a)" :key="act.to" type="button" class="mini" :class="{ danger: act.danger }"
                  :disabled="acting === a.id" @click="setStatus(a, act.to)">{{ act.label }}</button>
              </div>
            </div>
          </li>
        </ul>
      </template>

      <!-- Usuarios -->
      <template v-else>
        <p v-if="loading" class="muted">Cargando…</p>
        <div v-else class="table-wrap card">
          <table>
            <thead><tr><th>Nombre</th><th>Correo</th><th>Teléfono</th><th>Rol</th><th class="num-col">Citas</th><th>Cuenta</th></tr></thead>
            <tbody>
              <tr v-for="u in users" :key="u.id" :class="{ off: !u.active }">
                <td data-label="Nombre">{{ u.name }}</td>
                <td data-label="Correo">{{ u.email }}</td>
                <td data-label="Teléfono">{{ u.phone || '—' }}</td>
                <td data-label="Rol"><span :class="['pill', u.role === 'admin' ? 'confirmed' : 'completed']">{{ u.role === 'admin' ? 'Administrador' : 'Cliente' }}</span></td>
                <td data-label="Citas" class="num-col">{{ u.appointments }}</td>
                <td data-label="Cuenta">
                  <button v-if="u.id !== user?.id" type="button" class="mini" :class="{ danger: u.active }" :disabled="acting === u.id" @click="toggleActive(u)">
                    {{ u.active ? 'Desactivar' : 'Reactivar' }}
                  </button>
                  <span v-else class="muted">Tú</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </div>
  </section>
</template>
