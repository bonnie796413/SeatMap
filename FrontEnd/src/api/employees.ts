import http from './http'
import type { Employee, EmployeeSearchResult } from '@/types'

export const employeesApi = {
  list: () => http.get<Employee[]>('/employees'),
  search: (name: string) =>
    http.get<EmployeeSearchResult[]>('/employees/search', { params: { name } }),
  get: (id: string) => http.get<Employee>(`/employees/${id}`),
  create: (data: {
    fullName: string
    department?: string
    avatarUrl?: string
    username: string
    password: string
  }) => http.post<Employee>('/employees', data),
  update: (
    id: string,
    data: { fullName: string; department?: string; avatarUrl?: string },
  ) => http.put<Employee>(`/employees/${id}`, data),
  remove: (id: string) => http.delete(`/employees/${id}`),
}
