<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useCountUp } from '../composables/useCountUp'
import type { ChartSeries, ChartType } from './chartTypes'

// Gráfica SVG propia (sin librerías): barras, barras apiladas, línea con área y circular (donut).
// Cada cambio de `animKey` (tipo, periodo, filtro...) vuelve a montar el dibujo y repite la animación de entrada.
const props = defineProps<{ labels: string[]; series: ChartSeries[]; type: ChartType; animKey: string; unit: string }>()

const uid = `ch${Math.random().toString(36).slice(2, 8)}`

const W = 760, H = 360, L = 46, R = 16, T = 18, B = 46
const plotW = W - L - R, plotH = H - T - B
const n = computed(() => props.labels.length)
const nf = new Intl.NumberFormat('es-ES')

const hover = ref<number | null>(null)
watch(() => props.animKey, () => { hover.value = null })

// ---------- Escala ----------
function niceStep(x: number) {
  if (x <= 1) return 1
  const e = Math.pow(10, Math.floor(Math.log10(x)))
  const f = x / e
  return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10) * e
}
const maxValue = computed(() => {
  if (props.type === 'stacked') return Math.max(0, ...props.labels.map((_, i) => props.series.reduce((s, x) => s + (x.values[i] ?? 0), 0)))
  return Math.max(0, ...props.series.flatMap((s) => s.values))
})
const step = computed(() => niceStep(Math.max(maxValue.value, 4) / 4))
const top = computed(() => Math.ceil(Math.max(maxValue.value, 1) / step.value) * step.value)
const ticks = computed(() => Array.from({ length: Math.round(top.value / step.value) + 1 }, (_, k) => k * step.value))
const y = (v: number) => T + plotH - (v / top.value) * plotH
const band = computed(() => plotW / Math.max(n.value, 1))
const cx = (i: number) => L + band.value * (i + 0.5)
const base = T + plotH

const labelEvery = computed(() => Math.max(1, Math.ceil(n.value / Math.floor(plotW / 62))))
const empty = computed(() => props.series.every((s) => s.values.every((v) => !v)))
const cartesian = computed(() => props.type !== 'pie')

// ---------- Barras ----------
interface Bar { id: string; i: number; x: number; y: number; w: number; h: number; color: string; d: number; key: string }
const bars = computed<Bar[]>(() => {
  if (props.type !== 'bar' && props.type !== 'stacked') return []
  const out: Bar[] = []
  const m = props.series.length
  props.labels.forEach((_, i) => {
    if (props.type === 'stacked') {
      const w = Math.max(4, Math.min(band.value * 0.62, 64))
      let acc = 0
      props.series.forEach((s, j) => {
        const v = s.values[i] ?? 0
        if (!v) return
        out.push({ id: `${i}-${j}`, i, x: cx(i) - w / 2, y: y(acc + v), w, h: y(acc) - y(acc + v), color: s.color, d: i * 40 + j * 70, key: s.key })
        acc += v
      })
    } else {
      const inner = 4
      const group = Math.min(band.value * 0.74, 22 * m + 14 * (m - 1) + 8)
      const w = Math.max(3, Math.min(34, (group - (m - 1) * inner) / m))
      const total = w * m + (m - 1) * inner
      props.series.forEach((s, j) => {
        const v = s.values[i] ?? 0
        if (!v) return
        out.push({ id: `${i}-${j}`, i, x: cx(i) - total / 2 + j * (w + inner), y: y(v), w, h: base - y(v), color: s.color, d: i * 40 + j * 30, key: s.key })
      })
    }
  })
  return out
})

/** Totales encima de las barras, solo cuando caben y no son ruido (una serie, o apiladas). */
const totals = computed(() => {
  if (n.value > 14 || (props.type !== 'stacked' && !(props.type === 'bar' && props.series.length === 1))) return []
  return props.labels.map((_, i) => {
    const t = props.series.reduce((s, x) => s + (x.values[i] ?? 0), 0)
    return { i, t, x: cx(i), y: y(t) - 7 }
  }).filter((p) => p.t > 0)
})

// ---------- Línea y área ----------
interface Pt { x: number; y: number; v: number }
function smooth(pts: Pt[]): string {
  if (!pts.length) return ''
  let d = `M${pts[0].x.toFixed(1)},${pts[0].y.toFixed(1)}`
  const clamp = (v: number) => Math.min(base, Math.max(T, v))
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[i - 1] ?? pts[i], p1 = pts[i], p2 = pts[i + 1], p3 = pts[i + 2] ?? p2
    d += `C${(p1.x + (p2.x - p0.x) / 6).toFixed(1)},${clamp(p1.y + (p2.y - p0.y) / 6).toFixed(1)} ${(p2.x - (p3.x - p1.x) / 6).toFixed(1)},${clamp(p2.y - (p3.y - p1.y) / 6).toFixed(1)} ${p2.x.toFixed(1)},${p2.y.toFixed(1)}`
  }
  return d
}
const lines = computed(() => props.type !== 'line' ? [] : props.series.map((s, j) => {
  const pts = props.labels.map((_, i) => ({ x: cx(i), y: y(s.values[i] ?? 0), v: s.values[i] ?? 0 }))
  const path = smooth(pts)
  const area = pts.length > 1 ? `${path}L${pts[pts.length - 1].x.toFixed(1)},${base}L${pts[0].x.toFixed(1)},${base}Z` : ''
  return { key: s.key, color: s.color, pts, path, area, j }
}))

// ---------- Circular ----------
const R_DONUT = 108, SW = 46, CX = W / 2, CY = H / 2
const C = 2 * Math.PI * R_DONUT
interface Slice { key: string; label: string; value: number; color: string; len: number; rest: number; off: number; d: number; pct: number }
const slices = computed<Slice[]>(() => {
  if (props.type !== 'pie') return []
  // Con una sola serie (nuevos clientes) las porciones son los periodos; con varias, las series.
  const items = props.series.length === 1
    ? props.labels.map((l, i) => ({ key: `p${i}`, label: l, value: props.series[0].values[i] ?? 0, color: `hsl(${(172 + i * (320 / Math.max(n.value, 1))) % 360} 62% 52%)` }))
    : props.series.map((s) => ({ key: s.key, label: s.label, value: s.values.reduce((a, b) => a + b, 0), color: s.color }))
  const total = items.reduce((a, b) => a + b.value, 0)
  if (!total) return []
  const live = items.filter((x) => x.value > 0).length
  const gap = live > 1 ? 3 : 0
  let acc = 0
  return items.filter((x) => x.value > 0).map((x, k) => {
    const full = (x.value / total) * C
    const s = { ...x, len: Math.max(full - gap, 0.5), rest: C, off: -acc, d: k * 90, pct: (x.value / total) * 100 }
    acc += full
    return s
  })
})
const hoverSlice = ref<number | null>(null)
const pieTotal = computed(() => slices.value.reduce((a, b) => a + b.value, 0))
const centerValue = useCountUp(() => (hoverSlice.value !== null ? slices.value[hoverSlice.value]?.value ?? 0 : pieTotal.value))
watch(() => props.animKey, () => { hoverSlice.value = null })

// ---------- Tooltip ----------
const tip = computed(() => {
  const i = hover.value
  if (i === null || !cartesian.value) return null
  const rows = props.series.map((s) => ({ key: s.key, label: s.label, color: s.color, v: s.values[i] ?? 0 }))
  return { label: props.labels[i], rows, left: Math.min(86, Math.max(14, (cx(i) / W) * 100)) }
})

const summary = computed(() => `Gráfica de ${typeName.value}: ${props.series.map((s) => `${s.label} ${nf.format(s.values.reduce((a, b) => a + b, 0))} en total`).join(', ')}.`)
const typeName = computed(() => ({ bar: 'barras', stacked: 'barras apiladas', line: 'líneas', pie: 'sectores' })[props.type])
</script>

<template>
  <div class="chart" :class="{ hovering: hover !== null || hoverSlice !== null }">
    <svg :viewBox="`0 0 ${W} ${H}`" role="img" :aria-label="summary" preserveAspectRatio="xMidYMid meet">
      <defs>
        <linearGradient v-for="s in series" :key="s.key" :id="`${uid}-bar-${s.key}`" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" :style="{ stopColor: s.color }" stop-opacity="1" />
          <stop offset="100%" :style="{ stopColor: s.color }" stop-opacity=".62" />
        </linearGradient>
        <linearGradient v-for="s in series" :key="`a${s.key}`" :id="`${uid}-area-${s.key}`" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" :style="{ stopColor: s.color }" stop-opacity=".34" />
          <stop offset="100%" :style="{ stopColor: s.color }" stop-opacity="0" />
        </linearGradient>
      </defs>

      <!-- Rejilla y eje Y -->
      <g v-if="cartesian" class="grid" aria-hidden="true">
        <g v-for="t in ticks" :key="t">
          <line :x1="L" :x2="W - R" :y1="y(t)" :y2="y(t)" />
          <text :x="L - 10" :y="y(t) + 4" text-anchor="end">{{ nf.format(t) }}</text>
        </g>
        <text v-for="(l, i) in labels" v-show="i % labelEvery === 0" :key="l"
          :x="cx(i)" :y="H - 18" text-anchor="middle" class="xl" :class="{ hot: hover === i }">{{ l }}</text>
      </g>

      <g :key="animKey">
        <!-- Barras -->
        <g v-if="bars.length">
          <rect v-for="b in bars" :key="b.id" class="bar" :class="{ hot: hover === b.i }" :x="b.x" :y="b.y" :width="b.w" :height="Math.max(b.h, 1)"
            rx="4" :fill="`url(#${uid}-bar-${b.key})`" :style="{ '--d': `${b.d}ms` }" />
          <text v-for="t in totals" :key="t.i" class="val" :x="t.x" :y="t.y" text-anchor="middle" :style="{ '--d': `${t.i * 40 + 500}ms` }">{{ nf.format(t.t) }}</text>
        </g>

        <!-- Líneas con área -->
        <g v-for="l in lines" :key="l.key">
          <path v-if="l.area" class="area" :d="l.area" :fill="`url(#${uid}-area-${l.key})`" :style="{ '--d': `${200 + l.j * 120}ms` }" />
          <path class="line" :d="l.path" pathLength="1" fill="none" :stroke="l.color" :style="{ '--d': `${l.j * 120}ms` }" />
          <circle v-for="(p, i) in l.pts" :key="i" class="dot" :class="{ hot: hover === i }" :cx="p.x" :cy="p.y" :r="hover === i ? 6 : n > 24 ? 2.5 : 4"
            :fill="l.color" :style="{ '--d': `${600 + i * 30 + l.j * 120}ms` }" />
        </g>

        <!-- Circular -->
        <g v-if="type === 'pie'">
          <circle :cx="CX" :cy="CY" :r="R_DONUT" fill="none" class="track" :stroke-width="SW" />
          <g :transform="`rotate(-90 ${CX} ${CY})`">
            <circle v-for="(s, k) in slices" :key="s.key" class="seg" :class="{ hot: hoverSlice === k }" :cx="CX" :cy="CY" :r="R_DONUT" fill="none"
              :stroke="s.color" :stroke-width="hoverSlice === k ? SW + 10 : SW" :style="{ '--len': s.len, '--rest': s.rest, '--c': C, '--off': s.off, '--d': `${s.d}ms` }"
              @pointerenter="hoverSlice = k" @pointerleave="hoverSlice = null" />
          </g>
          <text :x="CX" :y="CY + 6" text-anchor="middle" class="center-n">{{ nf.format(centerValue) }}</text>
          <text :x="CX" :y="CY + 30" text-anchor="middle" class="center-l">
            {{ hoverSlice !== null && slices[hoverSlice] ? `${slices[hoverSlice].label} · ${slices[hoverSlice].pct.toFixed(0)} %` : unit }}
          </text>
        </g>
      </g>

      <!-- Zonas sensibles al ratón/táctil -->
      <g v-if="cartesian && !empty">
        <line v-if="hover !== null" class="guide" :x1="cx(hover)" :x2="cx(hover)" :y1="T" :y2="base" />
        <rect v-for="(_, i) in labels" :key="i" class="hit" :x="L + band * i" :y="T" :width="band" :height="plotH"
          @pointerenter="hover = i" @pointermove="hover = i" @pointerleave="hover = null" />
      </g>
    </svg>

    <p v-if="empty" class="chart-empty">Sin datos en este periodo</p>

    <div v-if="tip" class="tip" :style="{ left: `${tip.left}%` }" role="status">
      <strong>{{ tip.label }}</strong>
      <span v-for="r in tip.rows" :key="r.key"><i :style="{ background: r.color }" />{{ r.label }}<b>{{ nf.format(r.v) }}</b></span>
    </div>

    <!-- Lo mismo en tabla, para lectores de pantalla -->
    <table class="sr">
      <caption>{{ summary }}</caption>
      <thead><tr><th>Periodo</th><th v-for="s in series" :key="s.key">{{ s.label }}</th></tr></thead>
      <tbody><tr v-for="(l, i) in labels" :key="l"><th>{{ l }}</th><td v-for="s in series" :key="s.key">{{ s.values[i] ?? 0 }}</td></tr></tbody>
    </table>
  </div>
</template>
