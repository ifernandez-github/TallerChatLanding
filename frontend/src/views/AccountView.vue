<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { api, errMsg, type Appointment, type DeletionSummary, type VehicleInput } from '../appApi'
import { getFeatures } from '../api'
import ConfirmDialog from '../components/ConfirmDialog.vue'
import Icon from '../components/Icon.vue'
import VehicleManager from '../components/VehicleManager.vue'
import { useAuth } from '../composables/useAuth'
import { fmtDate, fmtTime, statusLabels } from '../format'

const router = useRouter()
const { user, logout, setUser } = useAuth()

type Tab = 'citas' | 'vehiculos' | 'datos'
const tab = ref<Tab>('citas')

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

// ---- Datos personales ----
const profile = reactive({ name: '', phone: '', street: '', postalCode: '', city: '', province: '' })
const profileBusy = ref(false)
const profileMsg = ref<{ ok: boolean; text: string } | null>(null)

function fillProfile() {
  profile.name = user.value?.name ?? ''
  profile.phone = user.value?.phone ?? ''
  profile.street = user.value?.address?.street ?? ''
  profile.postalCode = user.value?.address?.postalCode ?? ''
  profile.city = user.value?.address?.city ?? ''
  profile.province = user.value?.address?.province ?? ''
}
watch(user, fillProfile, { immediate: true })

async function saveProfile() {
  profileBusy.value = true
  profileMsg.value = null
  try {
    const updated = await api.updateProfile({
      name: profile.name,
      phone: profile.phone,
      address: { street: profile.street, postalCode: profile.postalCode, city: profile.city, province: profile.province }
    })
    setUser(updated)
    profileMsg.value = { ok: true, text: 'Datos guardados.' }
  } catch (e) {
    profileMsg.value = { ok: false, text: errMsg(e) }
  } finally {
    profileBusy.value = false
  }
}

// ---- Vehículos ----
const addVehicle = async (v: VehicleInput) => setUser(await api.addVehicle(v))
const updateVehicle = async (id: string, v: VehicleInput) => setUser(await api.updateVehicle(id, v))
const removeVehicle = async (id: string) => setUser(await api.deleteVehicle(id))

// ---- Citas ----
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

// ---- Baja de la cuenta ----
const delOpen = ref(false)
const delOpening = ref(false)
const delBusy = ref(false)
const delError = ref('')
/** Fallo al preparar el aviso: se enseña dentro de la propia zona de riesgo, no arriba en el formulario. */
const delZoneError = ref('')
const delPreview = ref<DeletionSummary | null>(null)
/** Una vez borrada no hay a dónde volver: la vista pasa a mostrar la despedida. */
const deleted = ref<DeletionSummary | null>(null)
const doneCard = ref<HTMLElement | null>(null)

/** Lo que se va a perder, en frases que entienda el cliente. */
const delItems = computed(() => {
  const p = delPreview.value
  if (!p) return []
  const list: string[] = []
  if (p.vehicles) list.push(p.vehicles === 1 ? 'Tu vehículo guardado' : `Tus ${p.vehicles} vehículos guardados`)
  if (p.appointments) {
    const citas = p.appointments === 1 ? 'Tu cita' : `Tus ${p.appointments} citas`
    list.push(p.upcoming ? `${citas}, incluidas ${p.upcoming} sin pasar todavía` : `${citas} y su historial`)
  }
  list.push('Tu nombre, teléfono y dirección')
  return list
})

async function openDelete() {
  if (delOpening.value) return
  delOpening.value = true
  delError.value = ''
  delZoneError.value = ''
  delPreview.value = null
  try {
    delPreview.value = await api.deletionPreview()
    delOpen.value = true
  } catch (e) {
    delZoneError.value = errMsg(e)
  } finally {
    delOpening.value = false
  }
}

async function confirmDelete(password: string) {
  delBusy.value = true
  delError.value = ''
  try {
    const summary = await api.deleteAccount(password)
    delOpen.value = false
    deleted.value = summary
    // El servidor ya ha cerrado la sesión; esto limpia la copia que la web tiene en memoria.
    await logout()
    // Al desaparecer el botón que abrió el diálogo, el foco se iría al body y un lector de pantalla
    // no anunciaría nada. Se lleva a la despedida, que además es region viva.
    await nextTick()
    doneCard.value?.focus()
  } catch (e) {
    delError.value = errMsg(e)
  } finally {
    delBusy.value = false
  }
}

/** Salir siempre devuelve a la portada, aunque falle la llamada de cierre de sesión. */
async function signOut() {
  // Primero la navegación: si se limpiara la sesión antes, esta vista se quedaría un instante
  // sin usuario y enseñaría la pestaña equivocada (y con ella la zona de borrado).
  await router.replace('/')
  await logout()
}

onMounted(async () => {
  emailEnabled.value = (await getFeatures()).email
  await load()
})
</script>

<template>
  <section class="page">
    <div class="lp-wrap narrow">
      <!-- Cuenta eliminada: ya no hay nada que gestionar aquí -->
      <div v-if="deleted" ref="doneCard" class="card empty-card enter" role="status" tabindex="-1">
        <Icon name="check" :size="30" />
        <h1 class="page-title" style="font-size: 1.6rem">Tu cuenta se ha eliminado</h1>
        <p class="muted">
          Hemos borrado tus datos{{ deleted.vehicles ? `, tus ${deleted.vehicles} vehículos` : '' }}{{
            deleted.appointments ? ` y tus ${deleted.appointments} citas` : '' }}. No guardamos ninguna copia.
          Si algún día vuelves a necesitarnos, puedes crear una cuenta nueva.
        </p>
        <RouterLink to="/" class="btn btn-primary">Volver a la portada</RouterLink>
      </div>

      <template v-else>
      <header class="page-head head-row enter">
        <div>
          <p class="eyebrow">Área de cliente</p>
          <h1 class="page-title">Hola, {{ user?.name.split(' ')[0] }}</h1>
          <p class="lp-lead">Gestiona tus citas, tus vehículos y tus datos de contacto.</p>
        </div>
        <div class="row-gap">
          <RouterLink to="/cita" class="btn btn-primary"><Icon name="calendar" :size="18" />Nueva cita</RouterLink>
          <button type="button" class="btn btn-outline" @click="signOut"><Icon name="logout" :size="18" />Salir</button>
        </div>
      </header>

      <div class="tabs wide" style="--cols: 3" role="tablist" aria-label="Secciones">
        <button type="button" role="tab" :aria-selected="tab === 'citas'" :class="{ on: tab === 'citas' }" @click="tab = 'citas'">
          <Icon name="calendar" :size="16" />Citas
        </button>
        <button type="button" role="tab" :aria-selected="tab === 'vehiculos'" :class="{ on: tab === 'vehiculos' }" @click="tab = 'vehiculos'">
          <Icon name="car" :size="16" />Vehículos
        </button>
        <button type="button" role="tab" :aria-selected="tab === 'datos'" :class="{ on: tab === 'datos' }" @click="tab = 'datos'">
          <Icon name="user" :size="16" />Mis datos
        </button>
      </div>

      <!-- Citas -->
      <template v-if="tab === 'citas'">
        <p v-if="error" class="form-error" role="alert">{{ error }}</p>

        <div class="block-head">
          <h2>Próximas citas</h2>
          <button v-if="emailEnabled && upcoming.length" type="button" class="chip-btn" :disabled="emailBusy" @click="emailMine">
            <Icon name="mail" :size="16" />{{ emailBusy ? 'Enviando…' : 'Enviármelas por email' }}
          </button>
        </div>
        <p v-if="emailMsg" :class="emailMsg.ok ? 'ok-note' : 'form-error'" role="status">{{ emailMsg.text }}</p>

        <p v-if="loading" class="muted">Cargando tus citas…</p>
        <div v-else-if="!upcoming.length" class="card empty-card">
          <Icon name="calendar" :size="28" />
          <p>No tienes citas próximas.</p>
          <RouterLink to="/cita" class="btn btn-primary">Pedir cita</RouterLink>
        </div>
        <ul v-else class="appt-list">
          <li v-for="(a, i) in upcoming" :key="a.id" class="appt card enter" :style="{ '--i': i + 1 }">
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

        <details v-if="history.length" class="history">
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
      </template>

      <!-- Vehículos -->
      <template v-else-if="tab === 'vehiculos'">
        <VehicleManager v-if="user" :vehicles="user.vehicles" :max="10"
          :add="addVehicle" :update="updateVehicle" :remove="removeVehicle" />
      </template>

      <!-- Mis datos -->
      <template v-else>
      <form class="card stack enter" novalidate @submit.prevent="saveProfile">
        <h2 class="block-title">Mis datos</h2>
        <label class="field">Nombre
          <input v-model="profile.name" type="text" autocomplete="name" maxlength="80" required />
        </label>
        <div class="grid-2">
          <label class="field">Correo electrónico
            <input :value="user?.email" type="email" disabled />
          </label>
          <label class="field">Teléfono
            <input v-model="profile.phone" type="tel" autocomplete="tel" maxlength="20" placeholder="600 000 000" />
          </label>
        </div>
        <p class="note">El correo identifica tu cuenta y no se puede cambiar desde aquí. Si lo necesitas, pídenoslo en el taller.</p>

        <h3 class="block-title" style="font-size: 1.05rem">Dirección</h3>
        <label class="field">Calle y número
          <input v-model="profile.street" type="text" autocomplete="street-address" maxlength="120" placeholder="Calle del Motor 24, 2º B" />
        </label>
        <div class="grid-3">
          <label class="field">Código postal
            <input v-model="profile.postalCode" type="text" inputmode="numeric" autocomplete="postal-code" maxlength="10" placeholder="28045" />
          </label>
          <label class="field">Localidad
            <input v-model="profile.city" type="text" autocomplete="address-level2" maxlength="80" placeholder="Madrid" />
          </label>
          <label class="field">Provincia
            <input v-model="profile.province" type="text" autocomplete="address-level1" maxlength="80" placeholder="Madrid" />
          </label>
        </div>

        <p v-if="profileMsg" :class="profileMsg.ok ? 'ok-note' : 'form-error'" role="status">{{ profileMsg.text }}</p>
        <button class="primary" type="submit" :disabled="profileBusy">{{ profileBusy ? 'Guardando…' : 'Guardar cambios' }}</button>
      </form>

      <!-- Baja de la cuenta -->
      <section class="danger-zone enter" aria-labelledby="baja">
        <h2 id="baja"><Icon name="trash" :size="17" />Eliminar mi cuenta</h2>
        <p>
          Se borran para siempre tus datos, tus vehículos y todas tus citas, incluidas las que aún no han pasado.
          No es lo mismo que cerrar sesión y no se puede deshacer.
        </p>
        <p v-if="delZoneError" class="form-error" role="alert">{{ delZoneError }}</p>
        <button type="button" class="primary danger-btn" :disabled="delOpening" @click="openDelete">
          {{ delOpening ? 'Preparando…' : 'Eliminar mi cuenta' }}
        </button>
      </section>
      </template>
      </template>

      <ConfirmDialog v-if="!deleted" :open="delOpen" title="Eliminar tu cuenta"
        :intro="`Vamos a borrar definitivamente la cuenta de ${user?.email ?? ''}. Esto incluye:`"
        :items="delItems" require-label="Escribe tu contraseña para confirmar" require-password
        require-hint="Te la pedimos para que nadie pueda borrar tu cuenta desde una sesión que hayas dejado abierta."
        confirm-label="Eliminar mi cuenta" :busy="delBusy" :error="delError"
        @confirm="confirmDelete" @close="delOpen = false" />
    </div>
  </section>
</template>
