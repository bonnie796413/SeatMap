<template>
  <div>
    <n-upload
      accept=".dxf"
      :max="1"
      :custom-request="handleUpload"
      :show-file-list="false"
      :disabled="uploading"
    >
      <n-upload-dragger>
        <p v-if="uploading"><n-spin size="small" /> 轉檔中，請稍候...</p>
        <p v-else>點擊或拖曳 .dxf 檔案至此上傳底圖</p>
      </n-upload-dragger>
    </n-upload>

    <div style="margin-top: 12px;">
      <n-alert v-if="status === 'Failed'" type="error">
        底圖解析失敗：{{ errorMsg }}
      </n-alert>
      <n-alert v-else-if="status === 'Ready'" type="success">
        底圖已就緒，可在下方地圖預覽
      </n-alert>
    </div>
    <!-- 「移除底圖」按鈕已移至下方功能列（FloorAdminView），由父層透過 removeMap() 觸發 -->
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import type { UploadCustomRequestOptions } from 'naive-ui'
import { useMessage } from 'naive-ui'
import { floorsApi } from '@/api/floors'
import type { FloorMap } from '@/types'

const props = defineProps<{ floorId: string }>()
const emit = defineEmits<{ mapReady: [meta: FloorMap]; mapRemoved: [] }>()

const message = useMessage()
const mapMeta = ref<FloorMap | null>(null)
const status = ref<string>('None')
const errorMsg = ref<string | null>(null)
const uploading = ref(false)

async function loadStatus() {
  const res = await floorsApi.getMap(props.floorId)
  mapMeta.value = res.data
  status.value = res.data.status
  errorMsg.value = res.data.errorMessage
  if (res.data.status === 'Ready') emit('mapReady', res.data)
}

// 同步上傳：POST 直接回傳轉檔結果（Ready / 失敗則拋例外）
async function handleUpload({ file, onFinish, onError }: UploadCustomRequestOptions) {
  uploading.value = true
  try {
    const res = await floorsApi.uploadMap(props.floorId, file.file as File)
    mapMeta.value = res.data
    status.value = res.data.status
    errorMsg.value = res.data.errorMessage
    message.success('底圖轉檔完成')
    emit('mapReady', res.data)
    onFinish()
  } catch (e: unknown) {
    status.value = 'Failed'
    const msg = (e instanceof Error ? e.message : null) ?? '上傳失敗'
    errorMsg.value = msg
    message.error(msg)
    onError()
  } finally {
    uploading.value = false
  }
}

async function handleDeleteMap() {
  try {
    await floorsApi.deleteMap(props.floorId)
    status.value = 'None'
    mapMeta.value = null
    errorMsg.value = null
    message.success('底圖已移除')
    emit('mapRemoved')
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : null) ?? '移除失敗')
  }
}

onMounted(loadStatus)

// 對外開放：讓父層的功能列觸發移除底圖
defineExpose({ removeMap: handleDeleteMap, status })
</script>
