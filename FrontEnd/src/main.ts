import './assets/main.css'
import 'leaflet/dist/leaflet.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'
import naive from 'naive-ui'

import App from './App.vue'
import router from './router'
import { useAuthStore } from './stores/auth'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(router)
app.use(naive)

// 啟動時還原登入狀態
const auth = useAuthStore()
if (auth.accessToken) {
  auth.fetchMe().catch(() => {
    auth.refresh().catch(() => auth.logout())
  })
}

app.mount('#app')
