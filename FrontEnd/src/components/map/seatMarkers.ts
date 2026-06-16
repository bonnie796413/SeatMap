import L from 'leaflet'
import type { Seat } from '@/types'

function avatarInitials(name: string): string {
  return name.charAt(0).toUpperCase()
}

function buildIcon(seat: Seat): L.DivIcon {
  if (!seat.assignment) {
    // 未指派空座位
    return L.divIcon({
      className: '',
      html: `<div class="seat-marker seat-empty" title="${seat.seatNumber}">
               <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="24" height="24">
                 <path fill="currentColor" d="M4 18v1a1 1 0 0 0 1 1h1a1 1 0 0 0 1-1v-1h10v1a1 1 0 0 0 1 1h1a1 1 0 0 0 1-1v-1h.5a1.5 1.5 0 0 0 0-3H20V9a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v6H3.5a1.5 1.5 0 0 0 0 3H4Z"/>
               </svg>
             </div>`,
      iconSize: [32, 32],
      iconAnchor: [16, 16],
    })
  }

  const isPresent = seat.isPresent === true
  const colorClass = isPresent ? 'seat-present' : 'seat-absent'

  let inner: string
  if (seat.assignment.avatarUrl) {
    inner = `<img src="${seat.assignment.avatarUrl}" alt="${seat.assignment.fullName}" style="width:28px;height:28px;border-radius:50%;object-fit:cover;" />`
  } else {
    inner = `<span class="seat-initials">${avatarInitials(seat.assignment.fullName)}</span>`
  }

  return L.divIcon({
    className: '',
    html: `<div class="seat-marker ${colorClass}" title="${seat.assignment.fullName}">${inner}</div>`,
    iconSize: [32, 32],
    iconAnchor: [16, 16],
  })
}

export function createSeatMarkers(
  seats: Seat[],
  onClickSeat: (seat: Seat) => void,
): L.LayerGroup {
  const group = L.layerGroup()
  for (const seat of seats) {
    const marker = L.marker([seat.y, seat.x], { icon: buildIcon(seat) })
    const popupContent = seat.assignment
      ? `<strong>${seat.assignment.fullName}</strong><br/>部門：${seat.assignment.department ?? '—'}<br/>座位：${seat.seatNumber}`
      : `<strong>空座位</strong><br/>座位號：${seat.seatNumber}`

    marker.bindPopup(popupContent)
    if (seat.assignment) {
      marker.bindTooltip(seat.assignment.fullName)
    }
    marker.on('click', () => onClickSeat(seat))
    group.addLayer(marker)
  }
  return group
}
