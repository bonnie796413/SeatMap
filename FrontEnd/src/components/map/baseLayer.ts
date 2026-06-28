import L from 'leaflet'
import { API_ORIGIN } from '@/api/http'

function escapeHtml(s: string): string {
  return s.replace(/[&<>"']/g, (c) =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c] ?? c,
  )
}

/**
 * 載入樓層 GeoJSON 底圖並建立 Leaflet 向量圖層。
 * DXF 平面座標 [x, y] 直接對應 CRS.Simple 的 latLng(y, x)，不需翻轉 Y。
 * 線條（LINE/LWPOLYLINE）以細線繪製；文字（TEXT/MTEXT）以標籤顯示。
 */
export async function loadBaseLayer(geoJsonUrl: string): Promise<L.GeoJSON> {
  const res = await fetch(`${API_ORIGIN}${geoJsonUrl}`)
  if (!res.ok) throw new Error(`載入底圖失敗（${res.status}）`)
  const data = await res.json()

  return L.geoJSON(data as GeoJSON.GeoJsonObject, {
    interactive: false,
    coordsToLatLng: (coords) => L.latLng(coords[1], coords[0]),
    style: () => ({ color: '#555', weight: 1, opacity: 0.85 }),
    pointToLayer: (feature, latlng) => {
      const text = feature.properties?.Text ?? feature.properties?.text
      if (text != null && String(text).trim() !== '') {
        return L.marker(latlng, {
          interactive: false,
          icon: L.divIcon({
            className: 'base-label',
            html: `<span style="font-size:10px;color:#555;white-space:nowrap;">${escapeHtml(String(text))}</span>`,
          }),
        })
      }
      return L.circleMarker(latlng, {
        radius: 1.5,
        color: '#999',
        weight: 1,
        interactive: false,
      })
    },
  })
}
