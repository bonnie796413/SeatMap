import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { attendanceApi } from '@/api/attendance'

export const useAttendanceStore = defineStore('attendance', () => {
  const status = ref<{ isPresent: boolean } | null>(null)
  const isPresent = computed(() => status.value?.isPresent ?? false)

  async function loadStatus() {
    try {
      const res = await attendanceApi.me()
      status.value = res.data
    } catch {
      // 無對應員工時忽略
    }
  }

  async function checkIn() {
    const res = await attendanceApi.checkIn()
    status.value = res.data
  }

  async function checkOut() {
    const res = await attendanceApi.checkOut()
    status.value = res.data
  }

  return { status, isPresent, loadStatus, checkIn, checkOut }
})
