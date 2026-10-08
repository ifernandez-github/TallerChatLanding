// Cliente de la API de usuarios y citas. La sesión viaja en una cookie HttpOnly del mismo origen.
export type Status = 'pending' | 'confirmed' | 'completed' | 'cancelled'
export type DayStatus = 'free' | 'partial' | 'full' | 'closed'

export interface Address { street?: string | null; postalCode?: string | null; city?: string | null; province?: string | null }
export interface Vehicle {
  id: string; make: string; model: string; plate: string
  year?: number | null; km?: number | null; fuel?: string | null; vin?: string | null
}
export interface VehicleInput {
  make: string; model: string; plate: string
  year?: number | null; km?: number | null; fuel?: string | null; vin?: string | null
}
export interface User {
  id: string; name: string; email: string; phone?: string | null; role: 'client' | 'admin'
  address?: Address | null; vehicles: Vehicle[]
}
export interface ProfileInput { name: string; phone: string; address: Address }

export interface Slot { start: string; time: string; free: number; available: boolean }
export interface Availability { date: string; closed: boolean; slots: Slot[] }
export interface FirstSlot { found: boolean; date?: string | null; start?: string | null; time?: string | null }
export interface CalendarDay {
  date: string; closed: boolean; past: boolean; booked: number; capacity: number; pending: number; status: DayStatus
}
export interface Calendar { month: string; days: CalendarDay[] }

export interface Appointment {
  id: string; serviceId: string; serviceName: string; start: string; plate: string; vehicle: string
  notes?: string | null; status: Status; canCancel: boolean
}
export interface BookResponse { appointment: Appointment; emailSent: boolean }
export interface BookInput { serviceId: string; start: string; vehicleId?: string | null; plate?: string; vehicle?: string; notes: string }
export interface AdminAppointment {
  id: string; userId: string; userName: string; userEmail: string; userPhone?: string | null
  serviceId: string; serviceName: string; start: string; bay: number; plate: string; vehicle: string
  notes?: string | null; status: Status
}
export interface AdminUser {
  id: string; name: string; email: string; phone?: string | null; role: 'client' | 'admin'
  active: boolean; emailVerified: boolean; createdAt: string; appointments: number
  address?: Address | null; vehicles: Vehicle[]
}
export interface AdminUserInput { name: string; email: string; phone: string; address: Address; role?: 'client' | 'admin' }
export interface Summary { pending: number; today: number; users: number }

/** Lo que desaparece al borrar una cuenta. Se consulta antes de confirmar, para poder avisar con números reales. */
export interface DeletionSummary { vehicles: number; appointments: number; upcoming: number }

/** Códigos que devuelve el backend para los casos que necesitan una acción concreta en la web. */
export const ERR_NOT_VERIFIED = 'email_not_verified'
export const ERR_BAD_TOKEN = 'invalid_token'

export interface RegisterResult { registered: boolean; emailSent: boolean; signedIn: boolean; email: string }

export class ApiError extends Error {
  constructor(public status: number, message: string, public code: string | null = null) { super(message) }
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
    throw new ApiError(res.status, data?.error ?? fallback, data?.code ?? null)
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
  register: (name: string, email: string, emailConfirm: string, phone: string, password: string) =>
    call<RegisterResult>('POST', '/api/auth/register', { name, email, emailConfirm, phone, password }),
  logout: () => call<void>('POST', '/api/auth/logout'),

  // Confirmación de la cuenta y contraseña olvidada
  verifyEmail: (token: string) => call<{ verified: boolean; email: string }>('POST', '/api/auth/verify', { token }),
  resendVerification: (email: string) => call<{ sent: boolean }>('POST', '/api/auth/resend', { email }),
  forgotPassword: (email: string) => call<{ sent: boolean }>('POST', '/api/auth/forgot', { email }),
  resetPassword: (token: string, password: string) => call<{ reset: boolean }>('POST', '/api/auth/reset', { token, password }),

  // Ficha del cliente: datos y vehículos
  updateProfile: (input: ProfileInput) => call<User>('PUT', '/api/profile', input),
  addVehicle: (v: VehicleInput) => call<User>('POST', '/api/profile/vehicles', v),
  updateVehicle: (id: string, v: VehicleInput) => call<User>('PUT', `/api/profile/vehicles/${id}`, v),
  deleteVehicle: (id: string) => call<User>('DELETE', `/api/profile/vehicles/${id}`),

  // Baja de la propia cuenta
  deletionPreview: () => call<DeletionSummary>('GET', '/api/profile/deletion'),
  deleteAccount: (password: string) => call<DeletionSummary>('POST', '/api/profile/delete', { password }),

  // Citas del cliente
  availability: (date: string) => call<Availability>('GET', `/api/appointments/availability?date=${encodeURIComponent(date)}`),
  firstAvailable: () => call<FirstSlot>('GET', '/api/appointments/first-available'),
  myAppointments: () => call<Appointment[]>('GET', '/api/appointments/mine'),
  book: (input: BookInput) => call<BookResponse>('POST', '/api/appointments', input),
  cancel: (id: string) => call<BookResponse>('POST', `/api/appointments/${id}/cancel`),
  emailMine: () => call<{ sent: boolean }>('POST', '/api/appointments/mine/email'),

  // Administración
  adminSummary: () => call<Summary>('GET', '/api/admin/summary'),
  adminCalendar: (month: string) => call<Calendar>('GET', `/api/admin/calendar?month=${encodeURIComponent(month)}`),
  adminAppointments: (q: { date?: string; pending?: boolean }) =>
    call<AdminAppointment[]>('GET', q.pending ? '/api/admin/appointments?pending=true' : `/api/admin/appointments?date=${q.date}`),
  adminSetStatus: (id: string, status: Status) => call<BookResponse>('POST', `/api/admin/appointments/${id}/status`, { status }),
  adminUsers: () => call<AdminUser[]>('GET', '/api/admin/users'),
  adminUpdateUser: (id: string, input: AdminUserInput) => call<User>('PUT', `/api/admin/users/${id}`, input),
  adminSetActive: (id: string, active: boolean) => call<{ id: string; active: boolean }>('POST', `/api/admin/users/${id}/active`, { active }),
  adminDeletionPreview: (id: string) => call<DeletionSummary>('GET', `/api/admin/users/${id}/deletion`),
  adminDeleteUser: (id: string, confirmEmail: string) =>
    call<DeletionSummary>('POST', `/api/admin/users/${id}/delete`, { confirmEmail }),
  adminAddVehicle: (userId: string, v: VehicleInput) => call<User>('POST', `/api/admin/users/${userId}/vehicles`, v),
  adminUpdateVehicle: (userId: string, id: string, v: VehicleInput) => call<User>('PUT', `/api/admin/users/${userId}/vehicles/${id}`, v),
  adminDeleteVehicle: (userId: string, id: string) => call<User>('DELETE', `/api/admin/users/${userId}/vehicles/${id}`)
}
