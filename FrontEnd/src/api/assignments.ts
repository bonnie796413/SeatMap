import http from './http'
import type { AssignmentResponse } from '@/types'

export const assignmentsApi = {
  assign: (seatId: string, employeeId: string) =>
    http.post<AssignmentResponse>('/assignments', { seatId, employeeId }),
  unassignBySeat: (seatId: string) => http.delete(`/seats/${seatId}/assignment`),
  unassignByEmployee: (employeeId: string) =>
    http.delete(`/employees/${employeeId}/assignment`),
}
