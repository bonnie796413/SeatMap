import http from './http'
import type { AuthUser, TokenResponse } from '@/types'

export const authApi = {
  login: (username: string, password: string) =>
    http.post<TokenResponse>('/auth/login', { username, password }),

  me: () => http.get<AuthUser>('/auth/me'),

  refresh: (refreshToken: string) =>
    http.post<TokenResponse>('/identity/refresh', { refreshToken }),

  logout: () => http.post('/identity/logout').catch(() => {}),
}
