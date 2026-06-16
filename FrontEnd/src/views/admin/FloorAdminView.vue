<template>
  <div>
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <n-text strong style="font-size: 16px;">樓層管理</n-text>
      <n-button type="primary" @click="showCreateModal = true">新增樓層</n-button>
    </div>

    <n-data-table :columns="columns" :data="floors" :loading="loading" />

    <!-- 新增樓層 Modal -->
    <n-modal v-model:show="showCreateModal" preset="dialog" title="新增樓層">
      <n-form>
        <n-form-item label="樓層名稱">
          <n-input v-model:value="newFloorName" placeholder="例：3F 研發部" />
        </n-form-item>
      </n-form>
      <template #action>
        <n-button @click="showCreateModal = false">取消</n-button>
        <n-button type="primary" :loading="saving" @click="handleCreate">確認</n-button>
      </template>
    </n-modal>

    <!-- 重新命名 Modal -->
    <n-modal v-model:show="showRenameModal" preset="dialog" title="重新命名">
      <n-form>
        <n-form-item label="樓層名稱">
          <n-input v-model:value="renameValue" placeholder="輸入新名稱" />
        </n-form-item>
      </n-form>
      <template #action>
        <n-button @click="showRenameModal = false">取消</n-button>
        <n-button type="primary" :loading="saving" @click="handleRename">確認</n-button>
      </template>
    </n-modal>

    <!-- 底圖 / 座位編輯區 -->
    <div v-if="selectedFloor" style="margin-top: 24px;">
      <n-divider>{{ selectedFloor.name }} — 底圖與座位設定</n-divider>
      <FloorMapUploader :floor-id="selectedFloor.id" @map-ready="handleMapReady" />
      <div style="margin-top: 16px; height: 500px;">
        <SeatEditorMap :floor-id="selectedFloor.id" :map-meta="currentMapMeta" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, h } from 'vue'
import { NButton, NTag, useMessage, useDialog } from 'naive-ui'
import { floorsApi } from '@/api/floors'
import type { Floor, FloorMap } from '@/types'
import FloorMapUploader from '@/components/admin/FloorMapUploader.vue'
import SeatEditorMap from '@/components/admin/SeatEditorMap.vue'

const message = useMessage()
const dialog = useDialog()

const floors = ref<Floor[]>([])
const loading = ref(false)
const saving = ref(false)

const showCreateModal = ref(false)
const newFloorName = ref('')

const showRenameModal = ref(false)
const renameTarget = ref<Floor | null>(null)
const renameValue = ref('')

const selectedFloor = ref<Floor | null>(null)
const currentMapMeta = ref<FloorMap | null>(null)

const columns = [
  { title: '名稱', key: 'name' },
  { title: '座位數', key: 'seatCount' },
  {
    title: '底圖狀態',
    key: 'mapStatus',
    render: (row: Floor) => {
      const typeMap: Record<string, 'success' | 'warning' | 'error' | 'default'> = {
        Ready: 'success', Processing: 'warning', Failed: 'error', None: 'default',
      }
      return h(NTag, { type: typeMap[row.mapStatus] ?? 'default' }, { default: () => row.mapStatus })
    },
  },
  {
    title: '操作',
    key: 'actions',
    render: (row: Floor) =>
      h('div', { style: 'display:flex;gap:8px;' }, [
        h(NButton, { size: 'small', onClick: () => openRename(row) }, { default: () => '重新命名' }),
        h(NButton, { size: 'small', onClick: () => { selectedFloor.value = row; loadMapMeta(row.id) } }, { default: () => '編輯' }),
        h(NButton, { size: 'small', type: 'error', onClick: () => handleDelete(row) }, { default: () => '刪除' }),
      ]),
  },
]

async function load() {
  loading.value = true
  try {
    const res = await floorsApi.list()
    floors.value = res.data
  } finally {
    loading.value = false
  }
}

async function handleCreate() {
  if (!newFloorName.value.trim()) { message.error('名稱不可為空'); return }
  saving.value = true
  try {
    await floorsApi.create(newFloorName.value.trim())
    message.success('樓層建立成功')
    showCreateModal.value = false
    newFloorName.value = ''
    await load()
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : null) ?? '建立失敗')
  } finally {
    saving.value = false
  }
}

function openRename(floor: Floor) {
  renameTarget.value = floor
  renameValue.value = floor.name
  showRenameModal.value = true
}

async function handleRename() {
  if (!renameTarget.value || !renameValue.value.trim()) return
  saving.value = true
  try {
    await floorsApi.rename(renameTarget.value.id, renameValue.value.trim())
    message.success('重新命名成功')
    showRenameModal.value = false
    await load()
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : null) ?? '失敗')
  } finally {
    saving.value = false
  }
}

function handleDelete(floor: Floor) {
  dialog.warning({
    title: '確認刪除',
    content: `刪除「${floor.name}」將一併移除其所有座位（${floor.seatCount} 個）與指派關係，此操作不可復原。確定刪除？`,
    positiveText: '確認刪除',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await floorsApi.remove(floor.id)
        message.success('已刪除')
        if (selectedFloor.value?.id === floor.id) selectedFloor.value = null
        await load()
      } catch (e: unknown) {
        message.error((e instanceof Error ? e.message : null) ?? '刪除失敗')
      }
    },
  })
}

async function loadMapMeta(floorId: string) {
  try {
    const res = await floorsApi.getMap(floorId)
    currentMapMeta.value = res.data
  } catch {
    currentMapMeta.value = null
  }
}

function handleMapReady(meta: FloorMap) {
  currentMapMeta.value = meta
}

onMounted(load)
</script>
