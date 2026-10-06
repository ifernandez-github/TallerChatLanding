import { createApp } from 'vue'
import App from './App.vue'
import { router } from './router'
import './style.css'
import './chat.css'
import './landing.css'
import './app.css'

createApp(App).use(router).mount('#app')
