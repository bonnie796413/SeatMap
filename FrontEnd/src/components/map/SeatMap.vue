<template>
  <div style="position: relative; width: 100%; height: 100%;">
    <!-- 地圖容器 -->
    <div ref="mapEl" style="width: 100%; height: 100%;"></div>

    <!-- 左上角樓層名稱 -->
    <div class="map-overlay map-overlay--top-left">
      <n-tag v-if="floorsStore.currentFloor" type="info">
        {{ floorsStore.currentFloor.name }}
      </n-tag>
      <n-empty v-else description="尚無樓層，請管理者先建立" size="small" />
    </div>

    <!-- 右側樓層切換箭頭 -->
    <div class="map-overlay map-overlay--right" v-if="floorsStore.floors.length > 1">
      <n-button circle @click="floorsStore.nextFloor()">▶</n-button>
    </div>

    <!-- 搜尋框 -->
    <div class="map-overlay map-overlay--top-right" style="width: 240px;">
      <EmployeeSearch :map="map" />
    </div>

    <!-- 重置視角 -->
    <div class="map-overlay map-overlay--bottom-right">
      <n-button size="small" @click="resetView">重置視角</n-button>
    </div>

    <!-- 底圖狀態提示 -->
    <div
      v-if="mapStatusMsg"
      class="map-overlay map-overlay--center"
    >
      <n-alert :type="mapStatusType">{{ mapStatusMsg }}</n-alert>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch, computed } from 'vue'
import L from 'leaflet'
import { useFloorsStore } from '@/stores/floors'
import { createSeatMarkers } from './seatMarkers'
import EmployeeSearch from './EmployeeSearch.vue'
import type { Seat } from '@/types'
import { API_ORIGIN } from '@/api/http'

const floorsStore = useFloorsStore()
const mapEl = ref<HTMLElement>()
let map: L.Map | null = null
let tileLayer: L.TileLayer | null = null
let seatLayer: L.LayerGroup | null = null
let pollInterval: number | undefined

const currentBounds = ref<L.LatLngBoundsExpression>([[0, 0], [1000, 1000]])

const mapStatusMsg = computed(() => {
  const status = floorsStore.currentMapMeta?.status
  if (!status || status === 'Ready' || status === 'None') return ''
  if (status === 'Processing') return '底圖轉檔中，請稍候...'
  if (status === 'Failed') return `底圖轉檔失敗：${floorsStore.currentMapMeta?.errorMessage ?? ''}`
  return ''
})

const mapStatusType = computed(() => {
  const status = floorsStore.currentMapMeta?.status
  if (status === 'Processing') return 'info'
  if (status === 'Failed') return 'error'
  return 'default'
})

function resetView() {
  map?.fitBounds(currentBounds.value as L.LatLngBoundsExpression)
}

function renderFloor() {
  if (!map) return

  // 移除舊圖層
  tileLayer?.remove()
  seatLayer?.remove()

  const meta = floorsStore.currentMapMeta
  if (meta?.status === 'Ready' && meta.tileUrlTemplate && meta.boundsJson) {
    const bounds = JSON.parse(meta.boundsJson) as L.LatLngBoundsExpression
    currentBounds.value = bounds

    tileLayer = L.tileLayer(
      `${API_ORIGIN}${meta.tileUrlTemplate}`,
      {
        minZoom: meta.minZoom,
        maxZoom: meta.maxZoom,
        tileSize: 256,
        noWrap: true,
        bounds,
      },
    )
    tileLayer.addTo(map)
    map.setMaxBounds(bounds)
    map.fitBounds(bounds)
  }

  // 繪製座位
  seatLayer = createSeatMarkers(floorsStore.currentSeats, handleSeatClick)
  seatLayer.addTo(map)
}

function handleSeatClick(_seat: Seat) {
  // popup 已由 createSeatMarkers 綁定
}

onMounted(async () => {
  if (!mapEl.value) return

  map = L.map(mapEl.value, {
    crs: L.CRS.Simple,
    minZoom: -2,
    maxZoom: 6,
  })

  await floorsStore.loadFloors()
  renderFloor()

  // 輪詢在場狀態
  pollInterval = window.setInterval(async () => {
    await floorsStore.refreshSeats()
    seatLayer?.remove()
    seatLayer = createSeatMarkers(floorsStore.currentSeats, handleSeatClick)
    if (map) seatLayer.addTo(map)
  }, 20000)

  // 視窗 resize
  window.addEventListener('resize', () => map?.invalidateSize())
})

onBeforeUnmount(() => {
  clearInterval(pollInterval)
  map?.remove()
  map = null
})

watch(
  () => [floorsStore.currentFloor?.id, floorsStore.currentMapMeta, floorsStore.currentSeats],
  renderFloor,
  { deep: true },
)

defineExpose({ map: () => map })
</script>

<style>
.map-overlay {
  position: absolute;
  z-index: 1000;
  pointer-events: auto;
}
.map-overlay--top-left  { top: 12px; left: 12px; }
.map-overlay--top-right { top: 12px; right: 60px; }
.map-overlay--right     { top: 50%; right: 12px; transform: translateY(-50%); }
.map-overlay--bottom-right { bottom: 28px; right: 12px; }
.map-overlay--center {
  top: 50%; left: 50%;
  transform: translate(-50%, -50%);
  min-width: 300px;
}

/* 座位標記樣式 */
.seat-marker {
  width: 32px; height: 32px;
  border-radius: 50%;
  display: flex; align-items: center; justify-content: center;
  font-size: 14px; font-weight: bold; color: white;
  border: 2px solid transparent;
  cursor: pointer;
  box-shadow: 0 1px 4px rgba(0,0,0,.4);
}
.seat-present  { background: #18a058; border-color: #0e7a3d; }
.seat-absent   { background: #909399; border-color: #707070; }
.seat-empty    {
  background: rgba(255,255,255,0.85);
  border: 2px dashed #ccc;
  color: #aaa;
}
.seat-initials { font-size: 13px; }
</style>
