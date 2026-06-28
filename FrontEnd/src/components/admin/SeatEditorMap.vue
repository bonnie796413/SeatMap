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
          <n-input v-model:value="newSeatNumber" placeholder="例：A-101" @keydown.enter="handleAddSeat" />
        </n-form-item>
      </n-form>
      <template #action>
        <n-button @click="showAddModal = false">取消</n-button>
        <n-button type="primary" @click="handleAddSeat">確認</n-button>
      </template>
    </n-modal>

    <!-- 指派員工 Modal -->
    <n-modal v-model:show="showAssignModal" preset="dialog" title="指派員工">
      <n-select v-model:value="assignEmployeeId" filterable remote clearable :options="employeeOptions"
        :loading="searchingEmployee" placeholder="搜尋員工姓名..." @search="searchEmployee" />
      <template #action>
        <n-button @click="showAssignModal = false">取消</n-button>
        <n-button type="primary" @click="handleAssign">指派</n-button>
      </template>
    </n-modal>

    <!-- 重新命名座位 Modal -->
    <n-modal v-model:show="showRenameModal" preset="dialog" title="重新命名座位">
      <n-form>
        <n-form-item label="座位編號">
          <n-input v-model:value="renameSeatNumber" placeholder="例：A-101" @keydown.enter="handleRename" />
        </n-form-item>
      </n-form>
      <template #action>
        <n-button @click="showRenameModal = false">取消</n-button>
        <n-button type="primary" @click="handleRename">確認</n-button>
      </template>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch } from 'vue'
import L from 'leaflet'
import { useMessage } from 'naive-ui'
import { seatsApi } from '@/api/seats'
import { assignmentsApi } from '@/api/assignments'
import { employeesApi } from '@/api/employees'
import type { FloorMap, Seat } from '@/types'
import { loadBaseLayer } from '@/components/map/baseLayer'
import { emptySeatIcon } from '@/components/map/seatMarkers'

interface DraftAssignment {
  employeeId: string
  fullName: string
  department: string | null
}
interface DraftSeat {
  id: string // 真實 id，或新增座位的暫時 id（temp-N）
  isNew: boolean
  seatNumber: string
  x: number
  y: number
  assignment: DraftAssignment | null
}

const props = defineProps<{ floorId: string; mapMeta: FloorMap | null; editable?: boolean }>()
const message = useMessage()

const mapEl = ref<HTMLElement>()
let map: L.Map | null = null
let baseLayer: L.GeoJSON | null = null
let seatLayer: L.LayerGroup | null = null

// 原始快照（用於 diff）與工作草稿
const originalSeats = ref<Seat[]>([])
const draftSeats = ref<DraftSeat[]>([])
const hasChanges = ref(false)
const saving = ref(false)
let tempCounter = 0

// 新增座位
const showAddModal = ref(false)
const newSeatNumber = ref('')
const pendingLatLng = ref<L.LatLng | null>(null)

// 改名
const showRenameModal = ref(false)
const renameSeatId = ref<string | null>(null)
const renameSeatNumber = ref('')

// 指派
const showAssignModal = ref(false)
const assignSeatId = ref<string | null>(null)
const assignEmployeeId = ref<string | null>(null)
const employeeOptions = ref<{ label: string; value: string }[]>([])
const employeeCache = ref<DraftAssignment[]>([])
const searchingEmployee = ref(false)

function toDraft(s: Seat): DraftSeat {
  return {
    id: s.id,
    isNew: false,
    seatNumber: s.seatNumber,
    x: s.x,
    y: s.y,
    assignment: s.assignment
      ? { employeeId: s.assignment.employeeId, fullName: s.assignment.fullName, department: s.assignment.department }
      : null,
  }
}

async function loadSeats() {
  const res = await seatsApi.listByFloor(props.floorId)
  originalSeats.value = res.data
  draftSeats.value = res.data.map(toDraft)
  hasChanges.value = false
  renderSeats()
}

function findDraft(id: string) {
  return draftSeats.value.find((s) => s.id === id)
}

function renderSeats() {
  if (!map) return
  seatLayer?.remove()
  seatLayer = L.layerGroup()

  for (const seat of draftSeats.value) {
    const marker = L.marker([seat.y, seat.x], {
      draggable: !!props.editable,
      title: seat.seatNumber,
      icon: emptySeatIcon(seat.seatNumber),
    })

    const assignedLabel = seat.assignment
      ? `<br/><small>指派：${seat.assignment.fullName}</small>`
      : '<br/><small>未指派</small>'

    // 僅在編輯模式顯示操作按鈕；唯讀時點擊只看座位資訊
    const actions = props.editable
      ? `<div class="seat-popup__actions">
        <button class="seat-popup-btn" onclick="window._seatEdit('${seat.id}')">改名</button>
        <button class="seat-popup-btn" onclick="window._seatAssign('${seat.id}')">指派</button>
        ${seat.assignment ? `<button class="seat-popup-btn" onclick="window._seatUnassign('${seat.id}')">解除指派</button>` : ''}
        <button class="seat-popup-btn seat-popup-btn--danger" onclick="window._seatDelete('${seat.id}')">刪除</button>
      </div>`
      : ''

    marker.bindPopup(`<strong>${seat.seatNumber}</strong>${assignedLabel}${actions}`)

    // 拖曳移動 → 只更新草稿座標
    marker.on('dragend', (e: L.DragEndEvent) => {
      const latlng = (e as unknown as { target: L.Marker }).target.getLatLng()
      const d = findDraft(seat.id)
      if (d) {
        d.x = latlng.lng
        d.y = latlng.lat
        hasChanges.value = true
      }
    })

    seatLayer.addLayer(marker)
  }

  map && seatLayer.addTo(map)

  // 全域回呼（供 popup 按鈕使用）
  const w = window as unknown as Record<string, unknown>
  w._seatEdit = (id: string) => openRename(id)
  w._seatAssign = (id: string) => openAssign(id)
  w._seatUnassign = (id: string) => unassignSeat(id)
  w._seatDelete = (id: string) => deleteSeat(id)
}

// ── 草稿操作（皆不呼叫 API，按「儲存」才提交） ─────────────────────────────

function handleAddSeat() {
  const num = newSeatNumber.value.trim()
  if (!num || !pendingLatLng.value) return
  if (draftSeats.value.some((s) => s.seatNumber === num)) {
    message.warning(`座位編號 ${num} 已存在`)
    return
  }
  draftSeats.value.push({
    id: `temp-${++tempCounter}`,
    isNew: true,
    seatNumber: num,
    x: pendingLatLng.value.lng,
    y: pendingLatLng.value.lat,
    assignment: null,
  })
  hasChanges.value = true
  showAddModal.value = false
  newSeatNumber.value = ''
  renderSeats()
}

function openRename(id: string) {
  const d = findDraft(id)
  if (!d) return
  renameSeatId.value = id
  renameSeatNumber.value = d.seatNumber
  showRenameModal.value = true
}

function handleRename() {
  const id = renameSeatId.value
  const newName = renameSeatNumber.value.trim()
  if (!id || !newName) return
  const d = findDraft(id)
  if (!d) return
  if (newName !== d.seatNumber && draftSeats.value.some((s) => s.id !== id && s.seatNumber === newName)) {
    message.warning(`座位編號 ${newName} 已存在`)
    return
  }
  d.seatNumber = newName
  hasChanges.value = true
  showRenameModal.value = false
  map?.closePopup()
  renderSeats()
}

function deleteSeat(id: string) {
  draftSeats.value = draftSeats.value.filter((s) => s.id !== id)
  hasChanges.value = true
  map?.closePopup()
  renderSeats()
}

function openAssign(id: string) {
  assignSeatId.value = id
  assignEmployeeId.value = null
  employeeOptions.value = []
  showAssignModal.value = true
}

async function searchEmployee(query: string) {
  if (!query.trim()) return
  searchingEmployee.value = true
  try {
    const res = await employeesApi.search(query)
    employeeCache.value = res.data.map((e) => ({
      employeeId: e.employeeId,
      fullName: e.fullName,
      department: e.department,
    }))
    employeeOptions.value = res.data.map((e) => ({
      label: `${e.fullName}${e.department ? ` · ${e.department}` : ''}`,
      value: e.employeeId,
    }))
  } finally {
    searchingEmployee.value = false
  }
}

function handleAssign() {
  if (!assignSeatId.value || !assignEmployeeId.value) return
  const emp = employeeCache.value.find((e) => e.employeeId === assignEmployeeId.value)
  if (!emp) return
  // 同一員工不可同時指派到兩個座位（提交時會違反唯一鍵）
  const dup = draftSeats.value.find(
    (s) => s.id !== assignSeatId.value && s.assignment?.employeeId === emp.employeeId,
  )
  if (dup) {
    message.warning(`${emp.fullName} 已指派給座位 ${dup.seatNumber}，請先解除`)
    return
  }
  const d = findDraft(assignSeatId.value)
  if (!d) return
  d.assignment = { ...emp }
  hasChanges.value = true
  showAssignModal.value = false
  map?.closePopup()
  renderSeats()
}

function unassignSeat(id: string) {
  const d = findDraft(id)
  if (!d) return
  d.assignment = null
  hasChanges.value = true
  map?.closePopup()
  renderSeats()
}

// ── 批次儲存 / 取消 ───────────────────────────────────────────────────────

async function save() {
  if (saving.value) return
  saving.value = true
  try {
    const original = originalSeats.value
    const draft = draftSeats.value
    const tempIdMap = new Map<string, string>()
    const draftRealIds = new Set(draft.filter((d) => !d.isNew).map((d) => d.id))

    // 1. 刪除（original 有、draft 沒有）；cascade 會一併移除其指派
    for (const o of original) {
      if (!draftRealIds.has(o.id)) {
        await seatsApi.remove(o.id)
      }
    }

    // 2. 新增（temp 座位）→ 取得真實 id
    for (const d of draft) {
      if (!d.isNew) continue
      const res = await seatsApi.create({
        floorId: props.floorId,
        seatNumber: d.seatNumber,
        x: d.x,
        y: d.y,
      })
      tempIdMap.set(d.id, res.data.id)
    }

    // 3. 更新既有座位（編號 / 座標變動）
    for (const d of draft) {
      if (d.isNew) continue
      const o = original.find((s) => s.id === d.id)
      if (o && (o.seatNumber !== d.seatNumber || o.x !== d.x || o.y !== d.y)) {
        await seatsApi.update(d.id, { seatNumber: d.seatNumber, x: d.x, y: d.y })
      }
    }

    // 4a. 先解除指派（原本有、現在沒有或換人），避免員工唯一鍵衝突
    for (const o of original) {
      if (!draftRealIds.has(o.id)) continue // 已刪除，cascade 處理過
      const d = draft.find((s) => s.id === o.id)
      const origEmp = o.assignment?.employeeId ?? null
      const draftEmp = d?.assignment?.employeeId ?? null
      if (origEmp && origEmp !== draftEmp) {
        await assignmentsApi.unassignBySeat(o.id)
      }
    }

    // 4b. 再指派（新增或換人）
    for (const d of draft) {
      const draftEmp = d.assignment?.employeeId ?? null
      if (!draftEmp) continue
      const realId = d.isNew ? tempIdMap.get(d.id) : d.id
      if (!realId) continue
      const origEmp = d.isNew ? null : (original.find((s) => s.id === d.id)?.assignment?.employeeId ?? null)
      if (draftEmp !== origEmp) {
        await assignmentsApi.assign(realId, draftEmp)
      }
    }

    message.success('座位配置已儲存')
    await loadSeats()
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : null) ?? '儲存失敗，已重新載入目前狀態')
    await loadSeats()
  } finally {
    saving.value = false
  }
}

async function cancel() {
  await loadSeats()
  message.info('已取消變更，回復至上次儲存的狀態')
}

// ── 地圖 ────────────────────────────────────────────────────────────────

function initMap() {
  if (!mapEl.value || map) return

  map = L.map(mapEl.value, {
    crs: L.CRS.Simple,
    minZoom: -2,
    maxZoom: 6,
    attributionControl: false,
  })

  map.on('click', (e: L.LeafletMouseEvent) => {
    if (!props.editable) return
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
watch(() => props.editable, renderSeats)

// 對外開放：供父層功能列觸發批次儲存 / 取消，並回報是否有未儲存變更
defineExpose({ save, cancel, hasChanges })
</script>

<style scoped>
.editor-placeholder {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(255, 250, 235, 0.85);
  z-index: 999;
}
</style>
