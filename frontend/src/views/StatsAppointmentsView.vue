<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { api, errMsg, type AppointmentsPoint, type AppointmentsStats, type Granularity } from '../appApi'
import AnimatedNumber from '../components/AnimatedNumber.vue'
import { typeLabels, type ChartSeries, type ChartType } from '../components/chartTypes'
import Icon from '../components/Icon.vue'
import SegmentedControl from '../components/SegmentedControl.vue'
import StatsChart from '../components/StatsChart.vue'
import StatsNav from '../components/StatsNav.vue'
import { useAuth } from '../composables/useAuth'

const { isAdmin } = useAuth()

type Metric = 'created' | 'pending' | 'confirmed' | 'closed' | 'cancelled'
const metrics: { key: Metric; label: string; color: string }[] = [
  { key: 'created', label: 'Creadas', color: 'var(--st-created)' },
  { key: 'pending', label: 'Pendientes', color: 'var(--st-pending)' },
  { key: 'confirmed', label: 'Confirmadas', color: 'var(--st-confirmed)' },
  { key: 'closed', label: 'Cerradas', color: 'var(--st-closed)' },
  { key: 'cancelled', label: 'Canceladas', color: 'var(--st-cancelled)' }
]

const granularity = ref<Granularity>('month')
const type = ref<ChartType>('bar')
const periods = ref(12)
const category = ref('')
const data = ref<AppointmentsStats | null>(null)
const loading = ref(false)
const error = ref('')
const version = ref(0)
const on = reactive<Record<Metric, boolean>>({ created: true, pending: false, confirmed: true, closed: true, cancelled: true })

const granularities: { value: Granularity; label: string }[] = [
  { value: 'month', label: 'Meses' }, { value: 'quarter', label: 'Trimestres' }, { value: 'year', label: 'Años' }
]
const types = [typeLabels.bar, typeLabels.stacked, typeLabels.line, typeLabels.pie]
const periodOptions: Record<Granularity, { value: number; label: string }[]> = {
  month: [6, 12, 24, 36].map((v) => ({ value: v, label: `Últimos ${v} meses` })),
  quarter: [4, 8, 12].map((v) => ({ value: v, label: `Últimos ${v} trimestres` })),
  year: [3, 5, 10].map((v) => ({ value: v, label: `Últimos ${v} años` }))
}
const defaults: Record<Granularity, number> = { month: 12, quarter: 8, year: 5 }

let request = 0
async function load() {
  const mine = ++request
  loading.value = true
  error.value = ''
  try {
    const result = await api.statsAppointments(granularity.value, periods.value, category.value)
    if (mine !== request) return
    data.value = result
    version.value++
  } catch (e) {
    if (mine === request) error.value = errMsg(e)
  } finally {
    if (mine === request) loading.value = false
  }
}

watch(granularity, (g) => { periods.value = defaults[g] })
watch([granularity, periods, category], load, { immediate: true })

/** «Creadas» es la suma de todos los estados: apilada o en sectores se contaría dos veces, así que se omite. */
const skipsTotal = computed(() => type.value === 'stacked' || type.value === 'pie')
const active = computed(() => metrics.filter((m) => on[m.key] && !(skipsTotal.value && m.key === 'created')))

const labels = computed(() => data.value?.points.map((p) => p.label) ?? [])
const series = computed<ChartSeries[]>(() =>
  active.value.map((m) => ({ key: m.key, label: m.label, color: m.color, values: data.value?.points.map((p: AppointmentsPoint) => p[m.key]) ?? [] })))

function toggle(key: Metric) {
  const others = metrics.filter((m) => m.key !== key && on[m.key]).length
  if (on[key] && others === 0) return // al menos una serie visible
  on[key] = !on[key]
  version.value++
}

const totals = computed(() => data.value?.totals)
const cancelRate = computed(() => (totals.value?.created ? (totals.value.cancelled / totals.value.created) * 100 : 0))
const categoryName = computed(() => data.value?.categories.find((c) => c.id === category.value)?.name ?? 'Todas las categorías')
const animKey = computed(() => `${type.value}-${version.value}-${active.value.map((m) => m.key).join()}`)
</script>

<template>
  <section class="page stats-page">
    <div class="lp-wrap">
      <header class="page-head enter">
        <p class="eyebrow">{{ isAdmin ? 'Administración' : 'Gestión' }} · Estadísticas</p>
        <h1 class="page-title">Estadísticas de citas</h1>
        <p class="lp-lead">Citas creadas, confirmadas, cerradas y canceladas. Se agrupan por el periodo en que se solicitaron.</p>
      </header>

      <StatsNav />

      <div class="stat-row stats-kpis enter" style="--i: 2">
        <div class="stat kpi"><strong><AnimatedNumber :value="totals?.created ?? 0" /></strong><span>citas creadas</span></div>
        <div class="stat kpi"><strong><AnimatedNumber :value="totals?.confirmed ?? 0" /></strong><span>confirmadas</span></div>
        <div class="stat kpi"><strong><AnimatedNumber :value="totals?.closed ?? 0" /></strong><span>cerradas</span></div>
        <div class="stat kpi"><strong><AnimatedNumber :value="cancelRate" :decimals="1" suffix=" %" /></strong><span>de cancelación</span></div>
      </div>

      <div class="card stats-card enter" style="--i: 3">
        <div class="stats-controls">
          <SegmentedControl v-model="granularity" :options="granularities" label="Agrupar por" />
          <label class="stats-select">
            <span class="sr">Intervalo</span>
            <select v-model.number="periods"><option v-for="o in periodOptions[granularity]" :key="o.value" :value="o.value">{{ o.label }}</option></select>
          </label>
          <label class="stats-select">
            <span class="sr">Categoría de servicio</span>
            <select v-model="category">
              <option value="">Todas las categorías</option>
              <option v-for="c in data?.categories ?? []" :key="c.id" :value="c.id">{{ c.name }}</option>
            </select>
          </label>
          <SegmentedControl v-model="type" :options="types" label="Tipo de gráfica" class="push" />
        </div>

        <div class="legend" role="group" aria-label="Series visibles">
          <button v-for="m in metrics" :key="m.key" type="button" class="legend-chip" :class="{ off: !on[m.key] || (skipsTotal && m.key === 'created') }"
            :aria-pressed="on[m.key]" :disabled="skipsTotal && m.key === 'created'" :title="skipsTotal && m.key === 'created' ? 'Creadas es el total: no se muestra en esta gráfica' : ''"
            @click="toggle(m.key)">
            <i :style="{ background: m.color }" /><span>{{ m.label }}</span>
            <b v-if="totals"><AnimatedNumber :value="totals[m.key]" /></b>
            <Icon v-if="on[m.key] && !(skipsTotal && m.key === 'created')" name="check" :size="14" />
          </button>
        </div>

        <p v-if="error" class="form-error" role="alert">{{ error }}</p>
        <div class="chart-wrap" :class="{ busy: loading, 'is-pie': type === 'pie' }" :aria-busy="loading">
          <StatsChart v-if="data" :labels="labels" :series="series" :type="type" :anim-key="animKey" unit="citas" />
          <div v-else-if="loading" class="chart-skeleton" aria-hidden="true" />
        </div>
        <p class="stats-foot">{{ categoryName }}. Cada cita cuenta con su estado actual.</p>
      </div>
    </div>
  </section>
</template>
