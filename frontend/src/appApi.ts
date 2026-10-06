// Cliente de la API de usuarios y citas. La sesión viaja en una cookie HttpOnly del mismo origen.
export type Status = 'pending' | 'confirmed' | 'completed' | 'cancelled'

export interface User { id: string; name: string; email: string; phone?: string | null; role: 'client' | 'admin' }
export interface Slot { start: string; time: string; free: number; available: boolean }
export interface Availability { date: string; closed: boolean; slots: Slot[] }
export interface Appointment {
  id: string; serviceId: string; serviceName: string; start: string; plate: string; vehicle: string
  notes?: string | null; status: Status; canCancel: boolean
}
export interface BookResponse { appointment: Appointment; emailSent: boolean }
export interface BookInput { serviceId: string; start: string; plate: string; vehicle: string; notes: string }
export interface AdminAppointment {
  id: string; userName: string; userEmail: string; userPhone?: string | null; serviceId: string; serviceName: string
  start: string; bay: number; plate: string; vehicle: string; notes?: string | null; status: Status
}
export interface AdminUser {
  id: string; name: string; email: string; phone?: string | null; role: 'client' | 'admin'
  active: boolean; createdAt: string; appointments: number
}
export interface Summary { pending: number; today: number; users: number }

export class ApiError extends Error {
  constructor(public status: number, message: string) { super(message) }
}

export const errMsg = (e: unknown) => (e instanceof Error ? e.message : 'Algo salió mal. Inténtalo de nuevo.')

async function call<T>(method: string, url: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
    credentials: 'same-origin'
  })
  if (!res.ok) {
    const data = await res.json().catch(() => null)
    const fallback =
      res.status === 401 ? 'Inicia sesión para continuar.'
      : res.status === 403 ? 'No tienes permiso para hacer esto.'
      : res.status === 429 ? 'Demasiadas solicitudes. Inténtalo de nuevo más tarde.'
      : 'No se pudo completar la operación. Inténtalo de nuevo.'
    throw new ApiError(res.status, data?.error ?? fallback)
  }
  return res.status === 204 ? (undefined as T) : res.json()
}

export const api = {
  // Sesión
  async me(): Promise<User | null> {
    try { return await call<User>('GET', '/api/auth/me') } catch (e) {
      if (e instanceof ApiError && e.status === 401) return null
      throw e
    }
  },
  login: (email: string, password: string) => call<User>('POST', '/api/auth/login', { email, password }),
  register: (name: string, email: string, phone: string, password: string) =>
    call<User>('POST', '/api/auth/register', { name, email, phone, password }),
  logout: () => call<void>('POST', '/api/auth/logout'),

  // Citas del cliente
  availability: (date: string) => call<Availability>('GET', `/api/appointments/availability?date=${encodeURIComponent(date)}`),
  myAppointments: () => call<Appointment[]>('GET', '/api/appointments/mine'),
  book: (input: BookInput) => call<BookResponse>('POST', '/api/appointments', input),
  cancel: (id: string) => call<BookResponse>('POST', `/api/appointments/${id}/cancel`),
  emailMine: () => call<{ sent: boolean }>('POST', '/api/appointments/mine/email'),

  // Administración
  adminSummary: () => call<Summary>('GET', '/api/admin/summary'),
  adminAppointments: (q: { date?: string; pending?: boolean }) =>
    call<AdminAppointment[]>('GET', q.pending ? '/api/admin/appointments?pending=true' : `/api/admin/appointments?date=${q.date}`),
  adminSetStatus: (id: string, status: Status) => call<BookResponse>('POST', `/api/admin/appointments/${id}/status`, { status }),
  adminUsers: () => call<AdminUser[]>('GET', '/api/admin/users'),
  adminSetActive: (id: string, active: boolean) => call<{ id: string; active: boolean }>('POST', `/api/admin/users/${id}/active`, { active })
}
