import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { floorsApi } from '@/api/floors'
import type { Floor, FloorMap, Seat } from '@/types'
import { seatsApi } from '@/api/seats'

export const useFloorsStore = defineStore('floors', () => {
  const floors = ref<Floor[]>([])
  const currentIndex = ref(0)
  const currentMapMeta = ref<FloorMap | null>(null)
  const currentSeats = ref<Seat[]>([])

  const currentFloor = computed(() => floors.value[currentIndex.value] ?? null)

  async function loadFloors() {
    const res = await floorsApi.list()
    floors.value = res.data
    if (floors.value.length > 0) {
      await selectFloor(0)
    }
  }

  async function selectFloor(index: number) {
    currentIndex.value = index
    const floor = floors.value[index]
    if (!floor) return

    const [mapRes, seatsRes] = await Promise.all([
      floorsApi.getMap(floor.id),
      seatsApi.listByFloor(floor.id),
    ])
    currentMapMeta.value = mapRes.data
    currentSeats.value = seatsRes.data
  }

  async function nextFloor() {
    if (floors.value.length === 0) return
    const next = (currentIndex.value + 1) % floors.value.length
    await selectFloor(next)
  }

  async function prevFloor() {
    if (floors.value.length === 0) return
    const prev = (currentIndex.value - 1 + floors.value.length) % floors.value.length
    await selectFloor(prev)
  }

  async function refreshSeats() {
    const floor = currentFloor.value
    if (!floor) return
    const res = await seatsApi.listByFloor(floor.id)
    currentSeats.value = res.data
  }

  return {
    floors, currentIndex, currentFloor, currentMapMeta, currentSeats,
    loadFloors, selectFloor, nextFloor, prevFloor, refreshSeats,
  }
})
