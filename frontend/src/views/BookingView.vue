<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import { api, ApiError, errMsg, type BookResponse, type Slot } from '../appApi'
import Icon from '../components/Icon.vue'
import { useAuth } from '../composables/useAuth'
import { site } from '../content/site'
import { addDays, dayParts, fmtDate, fmtTime, todayIso, weekday } from '../format'

const route = useRoute()
const { user } = useAuth()

const services = site.services
const serviceId = ref(services.some((s) => s.icon === route.query.servicio) ? String(route.query.servicio) : '')
const date = ref('')
const slots = ref<Slot[]>([])
const slotsBusy = ref(false)
const slotsError = ref('')
const start = ref('')
const plate = ref('')
const vehicle = ref('')
const notes = ref('')
const busy = ref(false)
const error = ref('')
const done = ref<BookResponse | null>(null)

// Próximos días laborables (el taller cierra los domingos).
const days = computed(() => {
  const today = todayIso()
  const out: string[] = []
  for (let i = 0; i < 40 && out.length < 18; i++) {
    const d = addDays(today, i)
    if (weekday(d) !== 0) out.push(d)
  }
  return out
})

const service = computed(() => services.find((s) => s.icon === serviceId.value))
const valid = computed(() => !!serviceId.value && !!start.value && plate.value.trim().length >= 4 && vehicle.value.trim().length >= 2)
const hasFree = computed(() => slots.value.some((s) => s.available))

async function pickDate(d: string) {
  date.value = d
  start.value = ''
  slotsError.value = ''
  slotsBusy.value = true
  try {
    const result = await api.availability(d)
    if (date.value === d) slots.value = result.slots // descarta respuestas antiguas si el usuario cambió de día
  } catch (e) {
    if (date.value === d) { slots.value = []; slotsError.value = errMsg(e) }
  } finally {
    if (date.value === d) slotsBusy.value = false
  }
}

async function submit() {
  if (!valid.value || busy.value) return
  busy.value = true
  error.value = ''
  try {
    done.value = await api.book({ serviceId: serviceId.value, start: start.value, plate: plate.value, vehicle: vehicle.value, notes: notes.value })
  } catch (e) {
    error.value = errMsg(e)
    if (e instanceof ApiError && e.status === 409) { start.value = ''; await pickDate(date.value) } // el hueco se ocupó: recarga las horas
  } finally {
    busy.value = false
  }
}

function again() {
  done.value = null
  serviceId.value = ''; date.value = ''; slots.value = []; start.value = ''
  plate.value = ''; vehicle.value = ''; notes.value = ''; error.value = ''
}
</script>

<template>
  <section class="page">
    <div class="lp-wrap narrow">
      <header class="page-head enter">
        <p class="eyebrow">Cita online</p>
        <h1 class="page-title">Pide tu cita</h1>
        <p class="lp-lead">Elige el servicio, el día y la hora. El taller confirmará tu cita y te avisaremos por email.</p>
      </header>

      <!-- Sin sesión -->
      <div v-if="!user" class="card gate enter" style="--i: 1">
        <span class="gate-ico"><Icon name="user" :size="26" /></span>
        <div>
          <h2>Necesitas una cuenta para pedir cita</h2>
          <p>Así podrás ver tus citas, cancelarlas y recibirlas por email. Solo te llevará un minuto.</p>
        </div>
        <div class="row-gap">
          <RouterLink :to="{ path: '/acceso', query: { redirect: '/cita' } }" class="btn btn-primary">Iniciar sesión</RouterLink>
          <RouterLink :to="{ path: '/acceso', query: { redirect: '/cita', modo: 'registro' } }" class="btn btn-outline">Crear cuenta</RouterLink>
        </div>
      </div>

      <!-- Confirmación -->
      <div v-else-if="done" class="card done-card enter" role="status">
        <span class="done-mark"><Icon name="check" :size="28" /></span>
        <h2>¡Solicitud enviada!</h2>
        <p class="done-sub">El taller revisará tu cita y la confirmará en breve.</p>
        <dl class="detail">
          <div><dt>Servicio</dt><dd>{{ done.appointment.serviceName }}</dd></div>
          <div><dt>Fecha</dt><dd>{{ fmtDate(done.appointment.start) }}, {{ fmtTime(done.appointment.start) }}</dd></div>
          <div><dt>Vehículo</dt><dd>{{ done.appointment.vehicle }} · {{ done.appointment.plate }}</dd></div>
        </dl>
        <p class="note">
          {{ done.emailSent ? `Te hemos enviado un correo a ${user?.email}.` : 'No hemos podido enviarte el correo de confirmación, pero puedes ver la cita en tu área de cliente.' }}
        </p>
        <div class="row-gap">
          <RouterLink to="/mi-cuenta" class="btn btn-primary">Ver mis citas</RouterLink>
          <button type="button" class="btn btn-outline" @click="again">Pedir otra cita</button>
        </div>
      </div>

      <!-- Formulario -->
      <form v-else class="stack-lg" novalidate @submit.prevent="submit">
        <fieldset class="card step enter" style="--i: 1">
          <legend><span class="num">1</span>¿Qué necesita tu coche?</legend>
          <div class="choice-grid">
            <label v-for="s in services" :key="s.icon" class="choice">
              <input v-model="serviceId" type="radio" name="service" :value="s.icon" class="sr" />
              <span class="choice-ico"><Icon :name="s.icon" :size="22" /></span>
              <span class="choice-txt">{{ s.title }}</span>
            </label>
          </div>
        </fieldset>

        <fieldset class="card step enter" style="--i: 2" :disabled="!serviceId">
          <legend><span class="num">2</span>Elige día y hora</legend>
          <div class="day-strip" role="group" aria-label="Día">
            <button v-for="d in days" :key="d" type="button" class="day" :class="{ on: date === d }" :aria-pressed="date === d" @click="pickDate(d)">
              <span>{{ dayParts(d).wd }}</span><strong>{{ dayParts(d).d }}</strong><span>{{ dayParts(d).m }}</span>
            </button>
          </div>

          <p v-if="slotsError" class="form-error" role="alert">{{ slotsError }}</p>
          <p v-else-if="date && slotsBusy" class="muted">Buscando huecos…</p>
          <template v-else-if="date">
            <div v-if="slots.length" class="slot-grid" role="group" aria-label="Hora">
              <button v-for="s in slots" :key="s.start" type="button" class="slot" :class="{ on: start === s.start }"
                :disabled="!s.available" :aria-pressed="start === s.start" @click="start = s.start">
                {{ s.time }}
              </button>
            </div>
            <p v-if="!slots.length" class="muted">El taller está cerrado ese día.</p>
            <p v-else-if="!hasFree" class="muted">No quedan huecos ese día. Prueba con otro.</p>
          </template>
          <p v-else class="muted">Selecciona un día para ver las horas libres.</p>
        </fieldset>

        <fieldset class="card step enter" style="--i: 3" :disabled="!start">
          <legend><span class="num">3</span>Tu vehículo</legend>
          <div class="grid-2">
            <label class="field">Matrícula
              <input v-model="plate" type="text" autocomplete="off" autocapitalize="characters" maxlength="12" placeholder="1234ABC" />
            </label>
            <label class="field">Marca y modelo
              <input v-model="vehicle" type="text" autocomplete="off" maxlength="60" placeholder="Seat León 1.5 TSI" />
            </label>
          </div>
          <label class="field">Notas <span class="muted">(opcional)</span>
            <textarea v-model="notes" rows="3" maxlength="500" placeholder="Cuéntanos qué le pasa o qué quieres revisar" />
          </label>
        </fieldset>

        <div class="summary card enter" style="--i: 4">
          <p v-if="service && start" class="summary-line">
            <Icon name="calendar" :size="18" />
            <span><strong>{{ service.title }}</strong> · {{ fmtDate(start) }}, {{ fmtTime(start) }}</span>
          </p>
          <p v-else class="muted">Completa los pasos para confirmar tu solicitud.</p>
          <p v-if="error" class="form-error" role="alert">{{ error }}</p>
          <button class="primary" type="submit" :disabled="!valid || busy">{{ busy ? 'Enviando…' : 'Solicitar cita' }}</button>
          <p class="note">Recibirás un correo en {{ user?.email }} cuando el taller confirme la cita.</p>
        </div>
      </form>
    </div>
  </section>
</template>
