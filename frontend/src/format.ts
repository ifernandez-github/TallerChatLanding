// Fechas y textos de la agenda. Todo se muestra en la zona horaria del taller, no en la del navegador.
const TZ = 'Europe/Madrid'

export const statusLabels: Record<string, string> = {
  pending: 'Pendiente de confirmar',
  confirmed: 'Confirmada',
  completed: 'Completada',
  cancelled: 'Cancelada'
}

/** Combustibles: el valor es el que guarda el backend (Fuels.cs). */
export const fuels = [
  { value: 'gasolina', label: 'Gasolina' },
  { value: 'diesel', label: 'Diésel' },
  { value: 'hibrido', label: 'Híbrido' },
  { value: 'hibrido-enchufable', label: 'Híbrido enchufable' },
  { value: 'electrico', label: 'Eléctrico' },
  { value: 'glp-gnc', label: 'GLP / GNC' },
  { value: 'otro', label: 'Otro' }
]
export const fuelLabel = (v?: string | null) => fuels.find((f) => f.value === v)?.label ?? ''

const capitalize = (s: string) => s.charAt(0).toUpperCase() + s.slice(1)

export function fmtDate(iso: string): string {
  return capitalize(new Intl.DateTimeFormat('es-ES', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', timeZone: TZ }).format(new Date(iso)))
}

export function fmtDay(iso: string): string {
  return capitalize(new Intl.DateTimeFormat('es-ES', { weekday: 'short', day: 'numeric', month: 'short', timeZone: TZ }).format(new Date(iso)))
}

export function fmtTime(iso: string): string {
  return new Intl.DateTimeFormat('es-ES', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone: TZ }).format(new Date(iso))
}

export const fmtKm = (km?: number | null) => (km == null ? '' : `${new Intl.NumberFormat('es-ES').format(km)} km`)

/** Fecha de hoy en Madrid (AAAA-MM-DD). */
export function todayIso(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: TZ, year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date())
}

export function addDays(iso: string, n: number): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d + n)).toISOString().slice(0, 10)
}

/** Mes AAAA-MM desplazado n meses. */
export function addMonths(month: string, n: number): string {
  const [y, m] = month.split('-').map(Number)
  const d = new Date(Date.UTC(y, m - 1 + n, 1))
  return `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`
}

export const monthOf = (iso: string) => iso.slice(0, 7)

/** "Octubre de 2026" a partir de AAAA-MM. */
export function monthLabel(month: string): string {
  const [y, m] = month.split('-').map(Number)
  return capitalize(new Intl.DateTimeFormat('es-ES', { month: 'long', year: 'numeric', timeZone: 'UTC' })
    .format(new Date(Date.UTC(y, m - 1, 1))))
}

/** 0 = domingo … 6 = sábado, para una fecha AAAA-MM-DD. */
export function weekday(iso: string): number {
  return new Date(`${iso}T00:00:00Z`).getUTCDay()
}

/** Lunes = 0 … domingo = 6, que es como se ordena el calendario en España. */
export const weekdayMon = (iso: string) => (weekday(iso) + 6) % 7

export const dayNumber = (iso: string) => Number(iso.slice(8, 10))

/** Etiquetas cortas de un día AAAA-MM-DD para los selectores de fecha. */
export function dayParts(iso: string): { wd: string; d: string; m: string } {
  const date = new Date(`${iso}T00:00:00Z`)
  const f = (o: Intl.DateTimeFormatOptions) => new Intl.DateTimeFormat('es-ES', { ...o, timeZone: 'UTC' }).format(date)
  return { wd: f({ weekday: 'short' }).replace('.', ''), d: f({ day: 'numeric' }), m: f({ month: 'short' }).replace('.', '') }
}
