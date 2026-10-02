import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// En desarrollo, /api se redirige a la Minimal API (sin CORS).
// En producción, el build se copia a wwwroot y la API sirve la web.
export default defineConfig({
  plugins: [vue()],
  server: { port: 5173, proxy: { '/api': 'http://localhost:5080' } },
  build: { outDir: '../src/TallerChat.Api/wwwroot', emptyOutDir: true }
})
