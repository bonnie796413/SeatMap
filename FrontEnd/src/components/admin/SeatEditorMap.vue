<template>
  <div style="position: relative; width: 100%; height: 100%;">
    <div ref="mapEl" style="width: 100%; height: 100%;"></div>
    <div v-if="!mapMeta || mapMeta.status !== 'Ready'" class="editor-placeholder">
      <n-empty description="底圖尚未就緒，上傳 DXF 後才能在此新增座位" />
    </div>

    <!-- 新增座位 Modal -->
    <n-modal v-model:show="showAddModal" preset="dialog" title="新增座位">
      <n-form>
        <n-form-item label="座位編號">
          <n-input v-model:value="newSeatNumber" placeholder="例：A-101" />
        </n-form-item>
      </n-form>
      <template #action>
        <n-button @click="showAddModal = false">取消</n-button>
        <n-button type="primary" :loading="saving" @click="handleAddSeat">確認</n-button>
      </template>
    </n-modal>

    <!-- 指派員工 Modal -->
    <n-modal v-model:show="showAssignModal" preset="dialog" title="指派員工">
      <n-select
        v-model:value="assignEmployeeId"
        filterable
        remote
        clearable
        :options="employeeOptions"
        :loading="searchingEmployee"
        placeholder="搜尋員工姓名..."
        @search="searchEmployee"
      />
      <template #action>
        <n-button @click="showAssignModal = false">取消</n-button>
        <n-button type="primary" :loading="saving" @click="handleAssign">指派</n-button>
      </template>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch } from 'vue'
import L from 'leaflet'
import { useMessage, useDialog } from 'naive-ui'
import { seatsApi } from '@/api/seats'
import { assignmentsApi } from '@/api/assignments'
import { employeesApi } from '@/api/employees'
import type { FloorMap, Seat, Employee } from '@/types'
import { loadBaseLayer } from '@/components/map/baseLayer'

const props = defineProps<{ floorId: string; mapMeta: FloorMap | null }>()
const message = useMessage()
const dialog = useDialog()

const mapEl = ref<HTMLElement>()
let map: L.Map | null = null
let baseLayer: L.GeoJSON | null = null
let seatLayer: L.LayerGroup | null = null

const seats = ref<Seat[]>([])
const showAddModal = ref(false)
const newSeatNumber = ref('')
const pendingLatLng = ref<L.LatLng | null>(null)
const saving = ref(false)

const showAssignModal = ref(false)
const assignSeatId = ref<string | null>(null)
const assignEmployeeId = ref<string | null>(null)
const employeeOptions = ref<{ label: string; value: string }[]>([])
const employeeCache = ref<Employee[]>([])
const searchingEmployee = ref(false)

async function loadSeats() {
  const res = await seatsApi.listByFloor(props.floorId)
  seats.value = res.data
  renderSeats()
}

function renderSeats() {
  if (!map) return
  seatLayer?.remove()
  seatLayer = L.layerGroup()

  for (const seat of seats.value) {
    const marker = L.marker([seat.y, seat.x], {
      draggable: true,
      title: seat.seatNumber,
    })

    const assignedLabel = seat.assignment
      ? `<br/><small>指派：${seat.assignment.fullName}</small>`
      : '<br/><small>未指派</small>'

    marker.bindPopup(`
      <strong>${seat.seatNumber}</strong>${assignedLabel}
      <div style="display:flex;gap:4px;margin-top:8px;flex-wrap:wrap;">
        <button onclick="window._seatEdit('${seat.id}','${seat.seatNumber}')">改名</button>
        <button onclick="window._seatAssign('${seat.id}')">指派</button>
        ${seat.assignment ? `<button onclick="window._seatUnassign('${seat.id}')">解除指派</button>` : ''}
        <button onclick="window._seatDelete('${seat.id}','${seat.seatNumber}',${!!seat.assignment},'${seat.assignment?.fullName ?? ''}')">刪除</button>
      </div>
    `)

    marker.on('dragend', async (e: L.DragEndEvent) => {
      const latlng = (e as unknown as { target: L.Marker }).target.getLatLng()
      try {
        await seatsApi.update(seat.id, { seatNumber: seat.seatNumber, x: latlng.lng, y: latlng.lat })
        message.success('座位已移動')
        await loadSeats()
      } catch (err: unknown) {
        message.error((err instanceof Error ? err.message : null) ?? '移動失敗')
      }
    })

    seatLayer.addLayer(marker)
  }

  map && seatLayer.addTo(map)

  // 全域回呼（供 popup 按鈕使用）
  ;(window as unknown as Record<string, unknown>)._seatEdit = (id: string, name: string) => promptEditSeat(id, name)
  ;(window as unknown as Record<string, unknown>)._seatAssign = (id: string) => openAssign(id)
  ;(window as unknown as Record<string, unknown>)._seatUnassign = (id: string) => unassignSeat(id)
  ;(window as unknown as Record<string, unknown>)._seatDelete = (id: string, num: string, hasAssign: boolean, empName: string) =>
    confirmDeleteSeat(id, num, hasAssign, empName)
}

function promptEditSeat(id: string, currentName: string) {
  const newName = prompt('輸入新座位編號', currentName)
  if (!newName || newName === currentName) return
  const seat = seats.value.find((s) => s.id === id)
  if (!seat) return
  seatsApi.update(id, { seatNumber: newName, x: seat.x, y: seat.y })
    .then(() => { message.success('已更新'); loadSeats() })
    .catch((e: unknown) => message.error((e instanceof Error ? e.message : null) ?? '更新失敗'))
}

function confirmDeleteSeat(id: string, num: string, hasAssign: boolean, empName: string) {
  const content = hasAssign
    ? `座位 ${num} 已指派給 ${empName}，刪除將一併解除指派，確定？`
    : `確定刪除座位 ${num}？`
  dialog.warning({
    title: '確認刪除座位',
    content,
    positiveText: '確認刪除',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await seatsApi.remove(id)
        message.success('已刪除')
        await loadSeats()
      } catch (e: unknown) {
        message.error((e instanceof Error ? e.message : null) ?? '刪除失敗')
      }
    },
  })
}

function openAssign(seatId: string) {
  assignSeatId.value = seatId
  assignEmployeeId.value = null
  showAssignModal.value = true
}

async function searchEmployee(query: string) {
  if (!query.trim()) return
  searchingEmployee.value = true
  try {
    const res = await employeesApi.search(query)
    employeeCache.value = res.data as unknown as Employee[]
    employeeOptions.value = res.data.map((e) => ({
      label: `${e.fullName}${e.department ? ` · ${e.department}` : ''}`,
      value: e.employeeId,
    }))
  } finally {
    searchingEmployee.value = false
  }
}

async function handleAssign() {
  if (!assignSeatId.value || !assignEmployeeId.value) return
  saving.value = true
  try {
    await assignmentsApi.assign(assignSeatId.value, assignEmployeeId.value)
    message.success('指派成功')
    showAssignModal.value = false
    await loadSeats()
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : null) ?? '指派失敗')
  } finally {
    saving.value = false
  }
}

async function unassignSeat(seatId: string) {
  try {
    await assignmentsApi.unassignBySeat(seatId)
    message.success('已解除指派')
    await loadSeats()
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : null) ?? '解除失敗')
  }
}

async function handleAddSeat() {
  if (!newSeatNumber.value.trim() || !pendingLatLng.value) return
  saving.value = true
  try {
    await seatsApi.create({
      floorId: props.floorId,
      seatNumber: newSeatNumber.value.trim(),
      x: pendingLatLng.value.lng,
      y: pendingLatLng.value.lat,
    })
    message.success('座位已新增')
    showAddModal.value = false
    newSeatNumber.value = ''
    await loadSeats()
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : null) ?? '新增失敗')
  } finally {
    saving.value = false
  }
}

function initMap() {
  if (!mapEl.value || map) return

  map = L.map(mapEl.value, {
    crs: L.CRS.Simple,
    minZoom: -2,
    maxZoom: 6,
  })

  map.on('click', (e: L.LeafletMouseEvent) => {
    if (!props.mapMeta || props.mapMeta.status !== 'Ready') return
    pendingLatLng.value = e.latlng
    newSeatNumber.value = ''
    showAddModal.value = true
  })

  updateBaseLayer()
  loadSeats()
}

async function updateBaseLayer() {
  if (!map) return
  baseLayer?.remove()
  baseLayer = null

  const meta = props.mapMeta
  if (meta?.status === 'Ready' && meta.geoJsonUrl) {
    try {
      baseLayer = await loadBaseLayer(meta.geoJsonUrl)
      baseLayer.addTo(map)
      const b = baseLayer.getBounds()
      if (b.isValid()) map.fitBounds(b)
    } catch (e) {
      console.error('底圖載入失敗', e)
    }
  }
}

onMounted(initMap)
onBeforeUnmount(() => { map?.remove(); map = null })

watch(() => props.mapMeta, updateBaseLayer, { deep: true })
watch(() => props.floorId, loadSeats)
</script>

<style scoped>
.editor-placeholder {
  position: absolute; inset: 0;
  display: flex; align-items: center; justify-content: center;
  background: rgba(255,255,255,0.8);
  z-index: 999;
}
</style>
