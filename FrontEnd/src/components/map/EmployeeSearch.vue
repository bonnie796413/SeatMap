<template>
  <n-select
    v-model:value="selected"
    placeholder="搜尋員工..."
    filterable
    remote
    clearable
    :options="options"
    :loading="searching"
    @search="handleSearch"
    @update:value="handleSelect"
  />
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useMessage } from 'naive-ui'
import { employeesApi } from '@/api/employees'
import { useFloorsStore } from '@/stores/floors'
import type { EmployeeSearchResult } from '@/types'
import type L from 'leaflet'

const props = defineProps<{ map: (() => L.Map | null) | null }>()
const floorsStore = useFloorsStore()
const message = useMessage()

const selected = ref<string | null>(null)
const searching = ref(false)
const results = ref<EmployeeSearchResult[]>([])
const options = ref<{ label: string; value: string }[]>([])

let searchTimer: ReturnType<typeof setTimeout> | undefined

function handleSearch(query: string) {
  clearTimeout(searchTimer)
  if (!query.trim()) { options.value = []; return }
  searchTimer = setTimeout(async () => {
    searching.value = true
    try {
      const res = await employeesApi.search(query)
      results.value = res.data
      options.value = res.data.map((e) => ({
        label: `${e.fullName}${e.department ? ` · ${e.department}` : ''}`,
        value: e.employeeId,
      }))
    } finally {
      searching.value = false
    }
  }, 300)
}

async function handleSelect(id: string | null) {
  if (!id) return
  const emp = results.value.find((e) => e.employeeId === id)
  if (!emp) return

  if (!emp.seat) {
    message.warning(`${emp.fullName} 尚未指派座位`)
    return
  }

  // 切換樓層
  const floorIdx = floorsStore.floors.findIndex((f) => f.id === emp.seat!.floorId)
  if (floorIdx !== -1 && floorIdx !== floorsStore.currentIndex) {
    await floorsStore.selectFloor(floorIdx)
  }

  // 定位地圖
  const m = props.map?.()
  if (m) {
    m.setView([emp.seat.y, emp.seat.x], 3)
  }

  selected.value = null
}
</script>
