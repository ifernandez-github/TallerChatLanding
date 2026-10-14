import { createRouter, createWebHistory } from 'vue-router'
import { site } from './content/site'
import { useAuth } from './composables/useAuth'

declare module 'vue-router' {
  interface RouteMeta { title?: string; auth?: boolean; staff?: boolean }
}

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: () => import('./views/HomeView.vue'), meta: { title: 'Mecánica de precisión, diagnóstico honesto' } },
    { path: '/servicios', component: () => import('./views/ServicesView.vue'), meta: { title: 'Servicios' } },
    { path: '/servicios/:id', component: () => import('./views/ServiceDetailView.vue'), meta: { title: 'Servicios' } },
    { path: '/nosotros', component: () => import('./views/AboutView.vue'), meta: { title: 'Quiénes somos' } },
    { path: '/contacto', component: () => import('./views/ContactView.vue'), meta: { title: 'Contacto' } },
    { path: '/cita', component: () => import('./views/BookingView.vue'), meta: { title: 'Pedir cita' } },
    { path: '/acceso', component: () => import('./views/LoginView.vue'), meta: { title: 'Acceso' } },
    { path: '/verificar', component: () => import('./views/VerifyEmailView.vue'), meta: { title: 'Confirmar cuenta' } },
    { path: '/restablecer', component: () => import('./views/ResetPasswordView.vue'), meta: { title: 'Nueva contraseña' } },
    { path: '/mi-cuenta', component: () => import('./views/AccountView.vue'), meta: { title: 'Mis citas', auth: true } },
    { path: '/admin', component: () => import('./views/AdminView.vue'), meta: { title: 'Administración', auth: true, staff: true } },
    { path: '/:pathMatch(.*)*', component: () => import('./views/NotFoundView.vue'), meta: { title: 'Página no encontrada' } }
  ],
  scrollBehavior(to, from, saved) {
    if (saved) return saved
    if (to.hash) return { el: to.hash, top: 72, behavior: 'smooth' }
    // Entre páginas se vuelve arriba al instante; el movimiento suave queda para los anclajes.
    return { top: 0, behavior: 'instant' }
  }
})

router.beforeEach(async (to) => {
  const { user, load } = useAuth()
  await load()
  if (to.meta.auth && !user.value) return { path: '/acceso', query: { redirect: to.fullPath } }
  // El panel de gestión es para administradores y gestores; lo que cada uno puede tocar lo decide la API.
  if (to.meta.staff && user.value?.role !== 'admin' && user.value?.role !== 'manager') return { path: '/' }
  // /verificar y /restablecer se abren desde un enlace del correo: deben funcionar siempre.
  if (to.path === '/acceso' && user.value) return { path: user.value.role === 'client' ? '/mi-cuenta' : '/admin' }
})

router.afterEach((to) => {
  document.title = to.meta.title ? `${to.meta.title} · ${site.name}` : site.name
})
