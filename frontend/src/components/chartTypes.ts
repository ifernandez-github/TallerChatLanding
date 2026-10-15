export type ChartType = 'bar' | 'stacked' | 'line' | 'pie'

/** Una serie de la gráfica: un valor por etiqueta del eje X. El color es una variable CSS (`var(--st-…)`). */
export interface ChartSeries { key: string; label: string; color: string; values: number[] }

export interface TypeOption { value: ChartType; label: string; icon: string }

export const typeLabels: Record<ChartType, TypeOption> = {
  bar: { value: 'bar', label: 'Barras', icon: 'bars' },
  stacked: { value: 'stacked', label: 'Apiladas', icon: 'stack' },
  line: { value: 'line', label: 'Línea', icon: 'trend' },
  pie: { value: 'pie', label: 'Circular', icon: 'donut' }
}
