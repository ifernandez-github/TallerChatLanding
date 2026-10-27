<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { api, errMsg, type ClientsStats, type Granularity } from '../appApi'
import AnimatedNumber from '../components/AnimatedNumber.vue'
import { typeLabels, type ChartSeries, type ChartType } from '../components/chartTypes'
import SegmentedControl from '../components/SegmentedControl.vue'
import StatsChart from '../components/StatsChart.vue'
import StatsNav from '../components/StatsNav.vue'
import { useAuth } from '../composables/useAuth'

const { isAdmin } = useAuth()

const granularity = ref<Granularity>('month')
const type = ref<ChartType>('bar')
const periods = ref(12)
const data = ref<ClientsStats | null>(null)
const loading = ref(false)
const error = ref('')
const version = ref(0)

const granularities: { value: Granularity; label: string }[] = [
  { value: 'month', label: 'Meses' }, { value: 'quarter', label: 'Trimestres' }, { value: 'year', label: 'Años' }
]
const types = [typeLabels.bar, typeLabels.line, typeLabels.pie]
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
    const result = await api.statsClients(granularity.value, periods.value)
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
watch([granularity, periods], load, { immediate: true })

const labels = computed(() => data.value?.points.map((p) => p.label) ?? [])
const series = computed<ChartSeries[]>(() => [{ key: 'clients', label: 'Clientes nuevos', color: 'var(--st-created)', values: data.value?.points.map((p) => p.count) ?? [] }])

const counts = computed(() => data.value?.points.map((p) => p.count) ?? [])
const best = computed(() => {
  const max = Math.max(0, ...counts.value)
  return max > 0 ? data.value!.points.find((p) => p.count === max)! : null
})
const average = computed(() => (counts.value.length ? (data.value?.total ?? 0) / counts.value.length : 0))
const last = computed(() => counts.value[counts.value.length - 1] ?? 0)
const prev = computed(() => counts.value[counts.value.length - 2] ?? 0)
const variation = computed(() => (prev.value > 0 ? ((last.value - prev.value) / prev.value) * 100 : null))
const unitName = computed(() => ({ month: 'este mes', quarter: 'este trimestre', year: 'este año' })[granularity.value])
const animKey = computed(() => `${type.value}-${version.value}`)
</script>

<template>
  <section class="page stats-page">
    <div class="lp-wrap">
      <header class="page-head enter">
        <p class="eyebrow">{{ isAdmin ? 'Administración' : 'Gestión' }} · Estadísticas</p>
        <h1 class="page-title">Nuevos clientes</h1>
        <p class="lp-lead">Cuántas personas se registran en el taller y cómo evoluciona el ritmo de altas.</p>
      </header>

      <StatsNav />

      <div class="stat-row stats-kpis enter" style="--i: 2">
        <div class="stat kpi"><strong><AnimatedNumber :value="data?.total ?? 0" /></strong><span>altas en el periodo</span></div>
        <div class="stat kpi"><strong><AnimatedNumber :value="last" /></strong><span>{{ unitName }}</span></div>
        <div class="stat kpi">
          <strong><AnimatedNumber :value="variation ?? 0" :decimals="0" suffix=" %" signed /></strong>
          <span>{{ variation === null ? 'sin periodo anterior con altas' : 'frente al periodo anterior' }}</span>
        </div>
        <div class="stat kpi"><strong><AnimatedNumber :value="average" :decimals="1" /></strong><span>de media por periodo</span></div>
      </div>

      <div class="card stats-card enter" style="--i: 3">
        <div class="stats-controls">
          <SegmentedControl v-model="granularity" :options="granularities" label="Agrupar por" />
          <label class="stats-select">
            <span class="sr">Intervalo</span>
            <select v-model.number="periods"><option v-for="o in periodOptions[granularity]" :key="o.value" :value="o.value">{{ o.label }}</option></select>
          </label>
          <SegmentedControl v-model="type" :options="types" label="Tipo de gráfica" class="push" />
        </div>

        <p v-if="error" class="form-error" role="alert">{{ error }}</p>
        <div class="chart-wrap" :class="{ busy: loading, 'is-pie': type === 'pie' }" :aria-busy="loading">
          <StatsChart v-if="data" :labels="labels" :series="series" :type="type" :anim-key="animKey" unit="clientes nuevos" />
          <div v-else-if="loading" class="chart-skeleton" aria-hidden="true" />
        </div>
        <p v-if="best" class="stats-foot">Mejor periodo: <strong>{{ best.label }}</strong> con {{ best.count }} {{ best.count === 1 ? 'alta' : 'altas' }}.</p>
      </div>
    </div>
  </section>
</template>
