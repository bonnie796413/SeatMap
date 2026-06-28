<template>
  <n-button
    :type="attendanceStore.isPresent ? 'default' : 'primary'"
    :loading="loading"
    size="small"
    strong
    @click="handleClick"
  >
    {{ attendanceStore.isPresent ? '下班打卡' : '上班打卡' }}
  </n-button>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useMessage } from 'naive-ui'
import { useAttendanceStore } from '@/stores/attendance'

const attendanceStore = useAttendanceStore()
const message = useMessage()
const loading = ref(false)

onMounted(() => attendanceStore.loadStatus())

async function handleClick() {
  loading.value = true
  try {
    if (attendanceStore.isPresent) {
      await attendanceStore.checkOut()
      message.success('下班打卡成功')
    } else {
      await attendanceStore.checkIn()
      message.success('上班打卡成功')
    }
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : null) ?? '打卡失敗')
  } finally {
    loading.value = false
  }
}
</script>
