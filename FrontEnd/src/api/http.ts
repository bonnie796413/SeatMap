import axios from 'axios'
import { useAuthStore } from '@/stores/auth'
import router from '@/router'

const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
})

// API_ORIGIN: 去除 /api 後綴，供 Tile URL 使用
export const API_ORIGIN = import.meta.env.VITE_API_BASE_URL.replace(/\/api$/, '')

http.interceptors.request.use((config) => {
  const auth = useAuthStore()
  if (auth.accessToken) {
    config.headers.Authorization = `Bearer ${auth.accessToken}`
  }
  return config
})

http.interceptors.response.use(
  (res) => res,
  async (err) => {
    const auth = useAuthStore()
    const original = err.config

    if (err.response?.status === 401 && !original._retry && auth.refreshToken) {
      original._retry = true
      try {
        await auth.refresh()
        original.headers.Authorization = `Bearer ${auth.accessToken}`
        return http(original)
      } catch {
        auth.logout()
        router.push('/login')
      }
    }

    const detail = err.response?.data?.detail ?? err.response?.data?.title ?? err.message
    return Promise.reject(new Error(detail))
  },
)

export default http
