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
      <FloorMapUploader
        ref="uploaderRef"
        :floor-id="selectedFloor.id"
        @map-ready="handleMapReady"
        @map-removed="handleMapRemoved"
      />
      <div style="margin-top: 16px; height: 500px;">
        <SeatEditorMap ref="editorRef" :floor-id="selectedFloor.id" :map-meta="currentMapMeta" :editable="editing" />
      </div>

      <!-- 功能列：AI 分析座位（左） / 移除底圖 · 儲存（右） -->
      <div class="editor-actions">
        <button class="ai-analyze-btn" @click="handleAiAnalyze">
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor"><path d="M11 3l1.6 4.4L17 9l-4.4 1.6L11 15l-1.6-4.4L5 9l4.4-1.6z"/><path d="M18 13l.9 2.4L21.3 16l-2.4.9L18 19.3l-.9-2.4L14.7 16l2.4-.9z"/></svg>
          AI 分析座位
        </button>
        <div style="display: flex; gap: 10px;">
          <n-button type="error" :disabled="currentMapMeta?.status !== 'Ready'" @click="handleRemoveMap">移除底圖</n-button>
          <n-button
            v-if="!editing"
            type="primary"
            :disabled="currentMapMeta?.status !== 'Ready'"
            @click="startEditing"
          >安排座位</n-button>
          <template v-else>
            <n-button :disabled="savingSeats" @click="handleCancel">取消</n-button>
            <n-button type="primary" :loading="savingSeats" @click="handleSave">儲存</n-button>
          </template>
        </div>
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
const uploaderRef = ref<InstanceType<typeof FloorMapUploader> | null>(null)
const editorRef = ref<InstanceType<typeof SeatEditorMap> | null>(null)
const savingSeats = ref(false)
const editing = ref(false)

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
      const labelMap: Record<string, string> = {
        Ready: '已就緒', Processing: '轉檔中', Failed: '解析失敗', None: '尚未上傳', Pending: '等待中',
      }
      return h(NTag, { type: typeMap[row.mapStatus] ?? 'default' }, { default: () => labelMap[row.mapStatus] ?? row.mapStatus })
    },
  },
  {
    title: '操作',
    key: 'actions',
    render: (row: Floor) =>
      h('div', { style: 'display:flex;gap:8px;' }, [
        h(NButton, { size: 'small', onClick: () => openRename(row) }, { default: () => '重新命名' }),
        h(NButton, { size: 'small', onClick: () => { selectedFloor.value = row; loadMapMeta(row.id); editing.value = false } }, { default: () => '編輯' }),
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

// 由 FloorMapUploader 觸發移除底圖；移除後同步狀態並刷新樓層清單
function handleMapRemoved() {
  currentMapMeta.value = null
  load()
}

function handleRemoveMap() {
  uploaderRef.value?.removeMap()
}

// 進入座位安排（編輯）模式
function startEditing() {
  editing.value = true
}

// 批次儲存座位草稿（新增 / 移動 / 改名 / 刪除 / 指派一次提交）
async function handleSave() {
  savingSeats.value = true
  try {
    await editorRef.value?.save()
    await load() // 同步更新樓層清單的座位數
    editing.value = false
  } finally {
    savingSeats.value = false
  }
}

// 取消草稿，回復至上次儲存的狀態並退出編輯模式
function handleCancel() {
  editorRef.value?.cancel()
  editing.value = false
}

function handleAiAnalyze() {
  message.info('AI 座位分析功能開發中，敬請期待')
}

onMounted(load)
</script>
