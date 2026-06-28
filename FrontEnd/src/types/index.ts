export interface AuthUser {
  userId: string
  username: string | null
  role: string | null
  employeeId: string | null
}

export interface TokenResponse {
  accessToken: string
  refreshToken: string
  expiresIn: number
  tokenType: string
}

export interface Floor {
  id: string
  name: string
  displayOrder: number
  seatCount: number
  mapStatus: string
}

export interface FloorMap {
  floorId: string
  status: string
  geoJsonUrl: string | null
  errorMessage: string | null
}

export interface AssignmentInfo {
  employeeId: string
  fullName: string
  avatarUrl: string | null
  department: string | null
}

export interface Seat {
  id: string
  floorId: string
  seatNumber: string
  x: number
  y: number
  assignment: AssignmentInfo | null
  isPresent: boolean | null
}

export interface SeatSummary {
  seatId: string
  floorId: string
  seatNumber: string
  x: number
  y: number
}

export interface Employee {
  id: string
  fullName: string
  department: string | null
  avatarUrl: string | null
  username: string | null
  isPresent: boolean
  seat: SeatSummary | null
}

export interface EmployeeSearchResult {
  employeeId: string
  fullName: string
  department: string | null
  avatarUrl: string | null
  isPresent: boolean
  seat: SeatSummary | null
}

export interface AssignmentResponse {
  seatId: string
  seatNumber: string
  floorId: string
  employeeId: string
  fullName: string
  assignedAt: string
}

export interface AttendanceStatus {
  employeeId: string
  isPresent: boolean
  lastCheckInAt: string | null
  lastCheckOutAt: string | null
  updatedAt: string
}
