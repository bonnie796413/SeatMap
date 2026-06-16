<template>
  <div>
    <n-upload
      accept=".dxf"
      :max="1"
      :custom-request="handleUpload"
      :show-file-list="false"
    >
      <n-upload-dragger>
        <p>點擊或拖曳 .dxf 檔案至此上傳底圖</p>
      </n-upload-dragger>
    </n-upload>

    <div style="margin-top: 12px;">
      <template v-if="status === 'Processing'">
        <n-spin size="small" /> 轉檔中，請稍候...
      </template>
      <n-alert v-else-if="status === 'Failed'" type="error">
        轉檔失敗：{{ errorMsg }}
      </n-alert>
      <n-alert v-else-if="status === 'Ready'" type="success">
        底圖已就緒，可在下方地圖預覽
      </n-alert>
    </div>

    <div v-if="mapMeta?.status === 'Ready'" style="margin-top: 8px; display: flex; gap: 8px;">
      <n-button size="small" type="error" @click="handleDeleteMap">移除底圖</n-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import type { UploadCustomRequestOptions } from 'naive-ui'
import { useMessage } from 'naive-ui'
import { floorsApi } from '@/api/floors'
import type { FloorMap } from '@/types'

const props = defineProps<{ floorId: string }>()
const emit = defineEmits<{ mapReady: [meta: FloorMap] }>()

const message = useMessage()
const mapMeta = ref<FloorMap | null>(null)
const status = ref<string>('None')
const errorMsg = ref<string | null>(null)

let pollTimer: ReturnType<typeof setTimeout> | undefined

async function loadStatus() {
  const res = await floorsApi.getMap(props.floorId)
  mapMeta.value = res.data
  status.value = res.data.status
  errorMsg.value = res.data.errorMessage
  if (res.data.status === 'Ready') emit('mapReady', res.data)
}

async function handleUpload({ file, onFinish, onError }: UploadCustomRequestOptions) {
  try {
    await floorsApi.uploadMap(props.floorId, file.file as File)
    status.value = 'Processing'
    message.info('已上傳，轉檔中...')
    startPolling()
    onFinish()
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : null) ?? '上傳失敗')
    onError()
  }
}

function startPolling() {
  clearTimeout(pollTimer)
  pollTimer = setTimeout(async () => {
    await loadStatus()
    if (status.value === 'Processing') startPolling()
  }, 3000)
}

async function handleDeleteMap() {
  try {
    await floorsApi.deleteMap(props.floorId)
    status.value = 'None'
    mapMeta.value = null
    message.success('底圖已移除')
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : null) ?? '移除失敗')
  }
}

onMounted(loadStatus)
</script>
