import http from './http'
import type { AttendanceStatus } from '@/types'

export const attendanceApi = {
  checkIn: () => http.post<AttendanceStatus>('/attendance/check-in'),
  checkOut: () => http.post<AttendanceStatus>('/attendance/check-out'),
  me: () => http.get<AttendanceStatus>('/attendance/me'),
}
