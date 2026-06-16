import http from './http'
import type { Seat } from '@/types'

export const seatsApi = {
  listByFloor: (floorId: string) => http.get<Seat[]>(`/floors/${floorId}/seats`),
  get: (id: string) => http.get<Seat>(`/seats/${id}`),
  create: (data: { floorId: string; seatNumber: string; x: number; y: number }) =>
    http.post<Seat>('/seats', data),
  update: (id: string, data: { seatNumber: string; x: number; y: number }) =>
    http.put<Seat>(`/seats/${id}`, data),
  remove: (id: string) => http.delete(`/seats/${id}`),
}
