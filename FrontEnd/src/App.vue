<template>
  <n-config-provider>
    <n-message-provider>
      <n-dialog-provider>
        <n-layout style="height: 100vh">
          <n-layout-header
            bordered
            style="padding: 0 48px; height: 52px; display: flex; align-items: center; gap: 12px;"
          >
            <n-text strong style="font-size: 18px;">座位表系統</n-text>
            <div style="flex: 1" />
            <template v-if="auth.isAuthenticated">
              <CheckInButton v-if="auth.user?.employeeId" />
              <RouterLink
                v-if="auth.isAdmin"
                :to="{ name: 'admin' }"
                custom
                v-slot="{ href, navigate }"
              >
                <n-button
                  text
                  tag="a"
                  :href="href"
                  style="font-size: 14px;"
                  @click="navigate"
                >
                  管理後台
                </n-button>
              </RouterLink>
              
              <n-text>{{ auth.user?.username }}</n-text>
              <n-button size="small" @click="handleLogout">登出</n-button>
            </template>
          </n-layout-header>
          <n-layout-content style="height: calc(100vh - 52px);">
            <RouterView />
          </n-layout-content>
        </n-layout>
      </n-dialog-provider>
    </n-message-provider>
  </n-config-provider>
</template>

<script setup lang="ts">
import { RouterView } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'
import CheckInButton from '@/components/CheckInButton.vue'

const auth = useAuthStore()
const router = useRouter()

function handleLogout() {
  auth.logout()
  router.push('/login')
}
</script>
