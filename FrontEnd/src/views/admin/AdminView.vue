<template>
  <div style="padding: 16px;">
    <n-tabs v-model:value="activeTab" type="line" animated @update:value="handleTabChange">
      <n-tab-pane name="floors" tab="樓層管理">
        <RouterView v-if="activeTab === 'floors'" />
      </n-tab-pane>
      <n-tab-pane name="employees" tab="員工管理">
        <RouterView v-if="activeTab === 'employees'" />
      </n-tab-pane>
    </n-tabs>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRoute, useRouter, RouterView } from 'vue-router'

const route = useRoute()
const router = useRouter()
const activeTab = ref(route.name === 'admin-employees' ? 'employees' : 'floors')

watch(
  () => route.name,
  (name) => {
    activeTab.value = name === 'admin-employees' ? 'employees' : 'floors'
  },
)

function handleTabChange(tab: string) {
  router.push({ name: tab === 'employees' ? 'admin-employees' : 'admin-floors' })
}
</script>
