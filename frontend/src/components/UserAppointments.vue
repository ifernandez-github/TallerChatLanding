<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { api, errMsg, type Appointment, type ServiceInfo, type Slot, type Vehicle } from '../appApi'
import { fmtDate, fmtTime, statusLabels, todayIso } from '../format'

// Citas de una cuenta ajena, para el personal: historial, nueva cita a su nombre y cancelación.
const props = defineProps<{ userId: string; vehicles: Vehicle[] }>()
const emit = defineEmits<{ changed: [] }>()

const items = ref<Appointment[]>([])
const services = ref<ServiceInfo[]>([])
const slots = ref<Slot[]>([])
const loading = ref(false)
const busy = ref(false)
const cancelling = ref('')
const error = ref('')
const notice = ref('')
const adding = ref(false)

const form = reactive({ serviceId: '', date: todayIso(), start: '', vehicleId: '', plate: '', vehicle: '', notes: '' })
const manual = computed(() => !form.vehicleId)
const freeSlots = computed(() => slots.value.filter((s) => s.available))

async function load() {
  loading.value = true
  error.value = ''
  try { items.value = await api.adminUserAppointments(props.userId) } catch (e) { error.value = errMsg(e) } finally { loading.value = false }
}

async function loadSlots() {
  form.start = ''
  slots.value = []
  if (!form.date) return
  try { slots.value = (await api.availability(form.date)).slots } catch (e) { error.value = errMsg(e) }
}

async function openForm() {
  adding.value = true
  notice.value = ''
  form.vehicleId = props.vehicles[0]?.id ?? ''
  if (!services.value.length) {
    try { services.value = await api.services() } catch (e) { error.value = errMsg(e) }
  }
  form.serviceId ||= services.value[0]?.id ?? ''
  await loadSlots()
}

async function book() {
  busy.value = true
  error.value = ''
  notice.value = ''
  try {
    const res = await api.adminBookForUser(props.userId, {
      serviceId: form.serviceId, start: form.start, notes: form.notes,
      ...(form.vehicleId ? { vehicleId: form.vehicleId } : { plate: form.plate, vehicle: form.vehicle })
    })
    notice.value = res.emailSent ? 'Cita creada. Se ha avisado al cliente por email.' : 'Cita creada.'
    adding.value = false
    form.notes = ''; form.plate = ''; form.vehicle = ''
    await load()
    emit('changed')
  } catch (e) {
    error.value = errMsg(e)
  } finally {
    busy.value = false
  }
}

async function cancel(a: Appointment) {
  cancelling.value = a.id
  error.value = ''
  notice.value = ''
  try {
    await api.adminSetStatus(a.id, 'cancelled')
    await load()
    emit('changed')
  } catch (e) {
    error.value = errMsg(e)
  } finally {
    cancelling.value = ''
  }
}

watch(() => form.date, loadSlots)
watch(() => props.userId, () => { adding.value = false; notice.value = ''; load() })
onMounted(load)
</script>

<template>
  <div class="user-appts">
    <div class="block-head">
      <h2>Citas</h2>
      <button v-if="!adding" type="button" class="mini solid" @click="openForm">Nueva cita</button>
    </div>

    <p v-if="error" class="form-error" role="alert">{{ error }}</p>
    <p v-if="notice" class="ok-note" role="status">{{ notice }}</p>

    <form v-if="adding" @submit.prevent="book">
      <label class="field">Servicio
        <select v-model="form.serviceId" required>
          <option v-for="s in services" :key="s.id" :value="s.id">{{ s.name }}</option>
        </select>
      </label>
      <div class="grid-2">
        <label class="field">Día
          <input v-model="form.date" type="date" :min="todayIso()" required />
        </label>
        <label class="field">Hora
          <select v-model="form.start" required>
            <option value="" disabled>{{ freeSlots.length ? 'Elige una hora' : 'Sin huecos ese día' }}</option>
            <option v-for="s in freeSlots" :key="s.start" :value="s.start">{{ s.time }}</option>
          </select>
        </label>
      </div>
      <label class="field">Vehículo
        <select v-model="form.vehicleId">
          <option v-for="v in vehicles" :key="v.id" :value="v.id">{{ v.make }} {{ v.model }} · {{ v.plate }}</option>
          <option value="">Otro vehículo (se guardará en la ficha)</option>
        </select>
      </label>
      <div v-if="manual" class="grid-2">
        <label class="field">Marca y modelo
          <input v-model="form.vehicle" type="text" maxlength="60" required />
        </label>
        <label class="field">Matrícula
          <input v-model="form.plate" type="text" maxlength="12" required />
        </label>
      </div>
      <label class="field">Notas
        <textarea v-model="form.notes" maxlength="500" rows="2" />
      </label>
      <div class="act-row">
        <button class="primary" type="submit" :disabled="busy || !form.start">{{ busy ? 'Creando…' : 'Crear cita' }}</button>
        <button type="button" class="mini" @click="adding = false">Cancelar</button>
      </div>
    </form>

    <p v-if="loading" class="muted">Cargando…</p>
    <p v-else-if="!items.length" class="muted">Esta cuenta no tiene citas.</p>
    <ul v-else class="appt-list">
      <li v-for="a in items" :key="a.id" class="appt card">
        <div class="appt-body">
          <p class="appt-svc">{{ a.serviceName }}</p>
          <p class="appt-meta">{{ fmtDate(a.start) }} · {{ fmtTime(a.start) }} h</p>
          <p class="appt-meta">{{ a.vehicle }} · {{ a.plate }}</p>
          <p v-if="a.notes" class="appt-notes">{{ a.notes }}</p>
        </div>
        <div class="appt-side">
          <span :class="['pill', a.status]">{{ statusLabels[a.status] }}</span>
          <button v-if="a.canCancel" type="button" class="mini danger" :disabled="cancelling === a.id" @click="cancel(a)">
            Cancelar
          </button>
        </div>
      </li>
    </ul>
  </div>
</template>
