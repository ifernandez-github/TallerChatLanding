import { createRouter, createWebHistory } from 'vue-router'
import { site } from './content/site'
import { useAuth } from './composables/useAuth'

declare module 'vue-router' {
  interface RouteMeta { title?: string; auth?: boolean; admin?: boolean }
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
    { path: '/admin', component: () => import('./views/AdminView.vue'), meta: { title: 'Administración', auth: true, admin: true } },
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
  if (to.meta.admin && user.value?.role !== 'admin') return { path: '/' }
  // /verificar y /restablecer se abren desde un enlace del correo: deben funcionar siempre.
  if (to.path === '/acceso' && user.value) return { path: user.value.role === 'admin' ? '/admin' : '/mi-cuenta' }
})

router.afterEach((to) => {
  document.title = to.meta.title ? `${to.meta.title} · ${site.name}` : site.name
})
