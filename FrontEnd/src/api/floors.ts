import http from './http'
import type { Floor, FloorMap } from '@/types'

export const floorsApi = {
  list: () => http.get<Floor[]>('/floors'),
  get: (id: string) => http.get<Floor>(`/floors/${id}`),
  create: (name: string) => http.post<Floor>('/floors', { name }),
  rename: (id: string, name: string) => http.put<Floor>(`/floors/${id}`, { name }),
  reorder: (orderedFloorIds: string[]) =>
    http.put<Floor[]>('/floors/reorder', { orderedFloorIds }),
  remove: (id: string) => http.delete(`/floors/${id}`),
  getMap: (floorId: string) => http.get<FloorMap>(`/floors/${floorId}/map`),
  uploadMap: (floorId: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return http.post(`/floors/${floorId}/map`, form)
  },
  deleteMap: (floorId: string) => http.delete(`/floors/${floorId}/map`),
}
