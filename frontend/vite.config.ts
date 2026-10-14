import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'

// En desarrollo, /api se redirige a la Minimal API (sin CORS). El destino se puede cambiar con VITE_API_PROXY
// (p. ej. http://api:8080 en Docker Compose); por defecto, la API local.
// En producción, el build se copia a wwwroot y la API sirve la web.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, '.', '')
  return {
    plugins: [vue()],
    server: { port: 5173, proxy: { '/api': env.VITE_API_PROXY || 'http://localhost:5080' } },
    build: { outDir: '../src/TallerChat.Api/wwwroot', emptyOutDir: true }
  }
})
