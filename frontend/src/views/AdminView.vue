<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import {
  api, errMsg, type AdminAppointment, type AdminUser, type CalendarDay, type DeletionSummary, type Status,
  type Summary, type VehicleInput
} from '../appApi'
import ConfirmDialog from '../components/ConfirmDialog.vue'
import Icon from '../components/Icon.vue'
import VehicleManager from '../components/VehicleManager.vue'
import { useAuth } from '../composables/useAuth'
import { addMonths, dayNumber, fmtDate, fmtTime, monthLabel, monthOf, statusLabels, todayIso, weekdayMon } from '../format'

const { user } = useAuth()
const route = useRoute()

const tab = ref<'agenda' | 'users'>('agenda')
const month = ref(monthOf(todayIso()))
const date = ref(todayIso())
const onlyPending = ref(false)
const calendar = ref<CalendarDay[]>([])
const calBusy = ref(false)
const items = ref<AdminAppointment[]>([])
const users = ref<AdminUser[]>([])
const summary = ref<Summary | null>(null)
const loading = ref(false)
const error = ref('')
const notice = ref('')
const acting = ref('')
/** Cita a la que apunta un enlace recibido por correo (?fecha=&cita=): se resalta al llegar a su agenda. */
const highlightId = ref('')

const dow = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom']
const statusText: Record<string, string> = { free: 'Libre', partial: 'Parcial', full: 'Completo', closed: 'Cerrado' }
/** Huecos vacíos antes del día 1 para que el mes empiece en la columna correcta. */
const pad = computed(() => (calendar.value.length ? weekdayMon(calendar.value[0].date) : 0))

const actions = (a: AdminAppointment): { to: Status; label: string; danger?: boolean; solid?: boolean }[] =>
  a.status === 'pending'
    ? [{ to: 'confirmed', label: 'Confirmar', solid: true }, { to: 'cancelled', label: 'Cancelar', danger: true }]
    : a.status === 'confirmed'
      ? [{ to: 'completed', label: 'Completar' }, { to: 'cancelled', label: 'Cancelar', danger: true }]
      : []

async function loadSummary() {
  try { summary.value = await api.adminSummary() } catch { /* el resumen es opcional */ }
}

async function loadCalendar() {
  calBusy.value = true
  try {
    calendar.value = (await api.adminCalendar(month.value)).days
  } catch (e) {
    error.value = errMsg(e)
  } finally {
    calBusy.value = false
  }
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

function pickDay(day: CalendarDay) {
  if (day.closed) return
  onlyPending.value = false
  highlightId.value = ''
  date.value = day.date
}

function shiftMonth(n: number) { month.value = addMonths(month.value, n) }

function goToday() {
  month.value = monthOf(todayIso())
  date.value = todayIso()
  onlyPending.value = false
}

async function setStatus(a: AdminAppointment, to: Status) {
  acting.value = a.id
  error.value = ''
  notice.value = ''
  try {
    const res = await api.adminSetStatus(a.id, to)
    if (to === 'confirmed' || to === 'cancelled') {
      notice.value = res.emailSent
        ? `Se ha avisado por email a ${a.userName}.`
        : 'Estado actualizado (no se envió email: SMTP no configurado o cliente de ejemplo).'
    }
    await Promise.all([loadAgenda(), loadSummary(), loadCalendar()])
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

// ---- Baja de una cuenta ----
const delUser = ref<AdminUser | null>(null)
const delOpening = ref('')
const delBusy = ref(false)
const delError = ref('')
/** Se cuenta en el momento de abrir el aviso: la tabla puede llevar rato cargada. */
const delPreview = ref<DeletionSummary | null>(null)

/** Lo que desaparece con la cuenta, con el número de citas que todavía no han pasado bien visible. */
const delItems = computed(() => {
  const p = delPreview.value
  if (!p) return []
  const list: string[] = []
  list.push(p.vehicles
    ? (p.vehicles === 1 ? '1 vehículo de su ficha' : `${p.vehicles} vehículos de su ficha`)
    : 'Su ficha (no tiene vehículos guardados)')
  if (p.appointments) {
    const citas = p.appointments === 1 ? '1 cita' : `${p.appointments} citas`
    list.push(p.upcoming
      ? `${citas}, de las que ${p.upcoming} todavía no ha${p.upcoming === 1 ? '' : 'n'} pasado`
      : `${citas} y su historial`)
  }
  list.push('Sus datos de contacto y su dirección')
  return list
})

async function askDelete(u: AdminUser) {
  if (delOpening.value) return
  delOpening.value = u.id
  delError.value = ''
  error.value = ''
  notice.value = ''
  delPreview.value = null
  try {
    delPreview.value = await api.adminDeletionPreview(u.id)
    delUser.value = u
  } catch (e) {
    error.value = errMsg(e)
  } finally {
    delOpening.value = ''
  }
}

async function confirmDelete(confirmEmail: string) {
  const target = delUser.value
  if (!target) return
  delBusy.value = true
  delError.value = ''
  try {
    await api.adminDeleteUser(target.id, confirmEmail)
    users.value = users.value.filter((u) => u.id !== target.id)
    delUser.value = null
    notice.value = `La cuenta de ${target.name} se ha eliminado con todos sus datos.`
    // Las citas de esa persona ya no están: la ocupación y el resumen cambian.
    await Promise.all([loadSummary(), loadCalendar()])
  } catch (e) {
    delError.value = errMsg(e)
  } finally {
    delBusy.value = false
  }
}

// ---- Ficha de cliente ----
const dialog = ref<HTMLDialogElement | null>(null)
const editing = ref<AdminUser | null>(null)
const form = reactive({ name: '', email: '', phone: '', street: '', postalCode: '', city: '', province: '', role: 'client' as 'client' | 'admin' })
const formBusy = ref(false)
const formMsg = ref<{ ok: boolean; text: string } | null>(null)

function openUser(u: AdminUser) {
  editing.value = u
  formMsg.value = null
  form.name = u.name; form.email = u.email; form.phone = u.phone ?? ''
  form.street = u.address?.street ?? ''; form.postalCode = u.address?.postalCode ?? ''
  form.city = u.address?.city ?? ''; form.province = u.address?.province ?? ''
  form.role = u.role
  dialog.value?.showModal()
}

function closeUser() {
  dialog.value?.close()
  editing.value = null
}

/** Refleja en la tabla los cambios hechos en el diálogo, sin recargar toda la lista. */
function syncUser(updated: { name: string; email: string; phone?: string | null; role: 'client' | 'admin'; address?: AdminUser['address']; vehicles: AdminUser['vehicles'] }) {
  if (!editing.value) return
  Object.assign(editing.value, {
    name: updated.name, email: updated.email, phone: updated.phone,
    role: updated.role, address: updated.address, vehicles: updated.vehicles
  })
}

async function saveUser() {
  if (!editing.value) return
  formBusy.value = true
  formMsg.value = null
  try {
    const updated = await api.adminUpdateUser(editing.value.id, {
      name: form.name, email: form.email, phone: form.phone,
      address: { street: form.street, postalCode: form.postalCode, city: form.city, province: form.province },
      role: form.role
    })
    syncUser(updated)
    formMsg.value = { ok: true, text: 'Ficha guardada.' }
  } catch (e) {
    formMsg.value = { ok: false, text: errMsg(e) }
  } finally {
    formBusy.value = false
  }
}

const addVehicle = async (v: VehicleInput) => syncUser(await api.adminAddVehicle(editing.value!.id, v))
const updateVehicle = async (id: string, v: VehicleInput) => syncUser(await api.adminUpdateVehicle(editing.value!.id, id, v))
const removeVehicle = async (id: string) => syncUser(await api.adminDeleteVehicle(editing.value!.id, id))

watch(month, loadCalendar)
watch([date, onlyPending], loadAgenda)
watch(tab, (t) => { notice.value = ''; if (t === 'users') loadUsers(); else { loadAgenda(); loadCalendar() } })

onMounted(async () => {
  // Enlace de un correo de aviso: abre directamente el día de esa cita y la resalta al llegar.
  // La fecha llega de una URL, no del backend: antes de usarla se comprueba el formato.
  const qDate = route.query.fecha
  const qAppt = route.query.cita
  if (typeof qDate === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(qDate)) {
    date.value = qDate
    month.value = monthOf(qDate)
  }
  if (typeof qAppt === 'string' && qAppt) highlightId.value = qAppt

  await Promise.all([loadAgenda(), loadCalendar(), loadSummary()])

  if (highlightId.value) {
    await nextTick()
    document.getElementById(`appt-${highlightId.value}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
  }
})
</script>

<template>
  <section class="page">
    <div class="lp-wrap">
      <header class="page-head enter">
        <p class="eyebrow">Administración</p>
        <h1 class="page-title">Agenda y clientes</h1>
        <p class="lp-lead">Consulta la ocupación de un vistazo, confirma las solicitudes y gestiona las fichas de tus clientes.</p>
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
          <Icon name="users" :size="16" />Clientes
        </button>
      </div>

      <p v-if="error" class="form-error" role="alert">{{ error }}</p>
      <p v-if="notice" class="ok-note" role="status">{{ notice }}</p>

      <!-- Agenda -->
      <template v-if="tab === 'agenda'">
        <div class="card cal-card enter" style="--i: 2">
          <div class="cal-head">
            <h2 class="cal-month">{{ monthLabel(month) }}</h2>
            <div class="date-nav">
              <button type="button" class="icon-btn" aria-label="Mes anterior" @click="shiftMonth(-1)">
                <Icon name="arrow" :size="18" style="transform: rotate(180deg)" />
              </button>
              <button type="button" class="chip-btn" @click="goToday">Hoy</button>
              <button type="button" class="icon-btn" aria-label="Mes siguiente" @click="shiftMonth(1)"><Icon name="arrow" :size="18" /></button>
            </div>
          </div>

          <div class="cal-grid" role="grid" :aria-busy="calBusy">
            <div v-for="d in dow" :key="d" class="cal-dow">{{ d }}</div>
            <div v-for="n in pad" :key="`pad-${n}`" class="cal-day pad" aria-hidden="true" />
            <button v-for="day in calendar" :key="day.date" type="button"
              class="cal-day" :class="[day.status, { past: day.past, on: day.date === date && !onlyPending }]"
              :disabled="day.closed" :aria-pressed="day.date === date && !onlyPending"
              :aria-label="`${day.date}: ${day.closed ? 'cerrado' : `${day.booked} de ${day.capacity} huecos ocupados`}`"
              @click="pickDay(day)">
              <span class="num-day">{{ dayNumber(day.date) }}</span>
              <span class="cal-count">
                {{ day.closed ? 'Cerrado' : day.booked === 0 ? 'Sin citas' : `${day.booked} cita${day.booked === 1 ? '' : 's'}` }}
              </span>
              <span v-if="day.pending" class="cal-count">{{ day.pending }} sin confirmar</span>
              <span class="cal-tag">{{ statusText[day.status] }}</span>
            </button>
          </div>

          <div class="cal-legend">
            <span><i class="free" />Libre</span>
            <span><i class="partial" />Parcial</span>
            <span><i class="full" />Completo</span>
            <span><i class="closed" />Cerrado</span>
          </div>
        </div>

        <div class="toolbar">
          <h2 class="block-title" style="margin: 0">
            {{ onlyPending ? 'Solicitudes pendientes' : fmtDate(`${date}T12:00:00Z`) }}
          </h2>
          <label class="check"><input v-model="onlyPending" type="checkbox" />Solo pendientes (todas las fechas)</label>
        </div>

        <p v-if="loading" class="muted">Cargando…</p>
        <div v-else-if="!items.length" class="card empty-card">
          <Icon name="calendar" :size="26" />
          <p>{{ onlyPending ? 'No hay solicitudes pendientes.' : 'No hay citas ese día.' }}</p>
        </div>
        <ul v-else class="appt-list">
          <li v-for="a in items" :id="`appt-${a.id}`" :key="a.id" class="appt card admin-appt"
            :class="{ highlight: a.id === highlightId }">
            <div class="appt-date">
              <strong>{{ onlyPending ? fmtDate(a.start) : `${fmtTime(a.start)} h` }}</strong>
              <span>{{ onlyPending ? `${fmtTime(a.start)} h` : `Elevador ${a.bay}` }}</span>
            </div>
            <div class="appt-body">
              <p class="appt-svc">{{ a.serviceName }}</p>
              <p class="appt-meta"><Icon name="car" :size="15" />{{ a.vehicle }} · {{ a.plate }}</p>
              <p class="appt-meta">
                <Icon name="user" :size="15" />{{ a.userName }}
                <template v-if="a.userPhone"> · {{ a.userPhone }}</template>
                · <a :href="`mailto:${a.userEmail}`">{{ a.userEmail }}</a>
              </p>
              <p v-if="a.notes" class="appt-notes">{{ a.notes }}</p>
            </div>
            <div class="appt-side">
              <span :class="['pill', a.status]">{{ statusLabels[a.status] }}</span>
              <div class="act-row">
                <button v-for="act in actions(a)" :key="act.to" type="button" class="mini"
                  :class="{ danger: act.danger, solid: act.solid }" :disabled="acting === a.id" @click="setStatus(a, act.to)">
                  {{ act.label }}
                </button>
              </div>
            </div>
          </li>
        </ul>
      </template>

      <!-- Clientes -->
      <template v-else>
        <p v-if="loading" class="muted">Cargando…</p>
        <div v-else class="table-wrap card">
          <table>
            <thead>
              <tr><th>Nombre</th><th>Correo</th><th>Teléfono</th><th>Localidad</th><th class="num-col">Vehículos</th><th class="num-col">Citas</th><th>Rol</th><th>Acciones</th></tr>
            </thead>
            <tbody>
              <tr v-for="u in users" :key="u.id" :class="{ off: !u.active }">
                <td data-label="Nombre">{{ u.name }}</td>
                <td data-label="Correo">{{ u.email }}</td>
                <td data-label="Teléfono">{{ u.phone || '—' }}</td>
                <td data-label="Localidad">{{ u.address?.city || '—' }}</td>
                <td data-label="Vehículos" class="num-col">{{ u.vehicles.length }}</td>
                <td data-label="Citas" class="num-col">{{ u.appointments }}</td>
                <td data-label="Rol">
                  <div class="act-row">
                    <span :class="['pill', u.role === 'admin' ? 'confirmed' : 'completed']">{{ u.role === 'admin' ? 'Administrador' : 'Cliente' }}</span>
                    <span v-if="!u.emailVerified" class="pill pending" title="Todavía no ha pulsado el enlace de confirmación">Sin confirmar</span>
                  </div>
                </td>
                <td data-label="Acciones">
                  <div class="act-row">
                    <button type="button" class="mini" :aria-label="`Abrir la ficha de ${u.name}`" @click="openUser(u)">
                      <Icon name="edit" :size="14" />Ficha
                    </button>
                    <template v-if="u.id !== user?.id">
                      <button type="button" class="mini" :class="{ danger: u.active }" :disabled="acting === u.id"
                        :aria-label="`${u.active ? 'Desactivar' : 'Reactivar'} la cuenta de ${u.name}`"
                        @click="toggleActive(u)">
                        {{ u.active ? 'Desactivar' : 'Reactivar' }}
                      </button>
                      <button type="button" class="mini danger" :disabled="delOpening === u.id"
                        :aria-label="`Eliminar la cuenta de ${u.name}`" @click="askDelete(u)">
                        <Icon name="trash" :size="14" />Eliminar
                      </button>
                    </template>
                    <span v-else class="muted">Tú</span>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>

      <!-- Ficha completa del cliente -->
      <dialog ref="dialog" class="sheet wide" @cancel.prevent="closeUser" @close="editing = null">
        <div v-if="editing" class="sheet-scroll">
          <form @submit.prevent="saveUser">
            <div class="sheet-head">
              <h2>Ficha de {{ editing.name }}</h2>
              <button type="button" class="icon-btn" aria-label="Cerrar" @click="closeUser"><Icon name="close" /></button>
            </div>

            <label class="field">Nombre
              <input v-model="form.name" type="text" maxlength="80" required />
            </label>
            <div class="grid-2">
              <label class="field">Correo electrónico
                <input v-model="form.email" type="email" maxlength="254" required />
              </label>
              <label class="field">Teléfono
                <input v-model="form.phone" type="tel" maxlength="20" />
              </label>
            </div>
            <label class="field">Calle y número
              <input v-model="form.street" type="text" maxlength="120" />
            </label>
            <div class="grid-3">
              <label class="field">Código postal
                <input v-model="form.postalCode" type="text" inputmode="numeric" maxlength="10" />
              </label>
              <label class="field">Localidad
                <input v-model="form.city" type="text" maxlength="80" />
              </label>
              <label class="field">Provincia
                <input v-model="form.province" type="text" maxlength="80" />
              </label>
            </div>
            <label class="field">Rol
              <select v-model="form.role" :disabled="editing.id === user?.id">
                <option value="client">Cliente</option>
                <option value="admin">Administrador</option>
              </select>
            </label>

            <p v-if="formMsg" :class="formMsg.ok ? 'ok-note' : 'form-error'" role="status">{{ formMsg.text }}</p>
            <button class="primary" type="submit" :disabled="formBusy">{{ formBusy ? 'Guardando…' : 'Guardar ficha' }}</button>
          </form>

          <VehicleManager :vehicles="editing.vehicles" :max="10"
            :add="addVehicle" :update="updateVehicle" :remove="removeVehicle" />
        </div>
      </dialog>

      <!-- Baja de una cuenta de cliente -->
      <ConfirmDialog :open="!!delUser" :title="`Eliminar la cuenta de ${delUser?.name ?? ''}`"
        :intro="`Se borrará definitivamente la cuenta de ${delUser?.email ?? ''} y todo lo que cuelga de ella:`"
        :items="delItems" require-label="Escribe el correo de la cuenta para confirmar"
        :expected="delUser?.email"
        require-hint="Lo pedimos para que un clic en la fila equivocada no borre a quien no toca."
        confirm-label="Eliminar la cuenta" :busy="delBusy" :error="delError"
        @confirm="confirmDelete" @close="delUser = null" />
    </div>
  </section>
</template>
