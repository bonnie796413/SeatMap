<template>
  <n-config-provider :theme-overrides="themeOverrides">
    <n-message-provider>
      <n-dialog-provider>
        <n-layout style="height: 100vh">
          <n-layout-header class="app-header">
            <RouterLink to="/" class="brand" style="text-decoration: none; color: inherit; cursor: pointer;">
              <div class="brand-mark">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="currentColor" d="M4 18v1a1 1 0 0 0 1 1h1a1 1 0 0 0 1-1v-1h10v1a1 1 0 0 0 1 1h1a1 1 0 0 0 1-1v-1h.5a1.5 1.5 0 0 0 0-3H20V9a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v6H3.5a1.5 1.5 0 0 0 0 3H4Z"/></svg>
              </div>
              <span class="brand-title">座位表系統</span>
            </RouterLink>

            <div class="app-header__spacer"></div>

            <template v-if="auth.isAuthenticated">
              <div
                v-if="auth.user?.employeeId"
                class="presence-pill"
                :class="{ 'presence-pill--on': attendance.isPresent }"
              >
                <span class="presence-dot"></span>
                {{ attendance.isPresent ? '在場' : '不在場' }}
              </div>

              <CheckInButton v-if="auth.user?.employeeId" />

              <span v-if="auth.isAdmin" class="header-divider">|</span>
              <RouterLink
                v-if="auth.isAdmin"
                :to="{ name: 'admin' }"
                custom
                v-slot="{ href, navigate }"
              >
                <n-button text tag="a" :href="href" style="font-size: 14px;" @click="navigate">
                  管理後台
                </n-button>
              </RouterLink>

              <div class="user-chip">
                <div class="user-avatar">{{ userInitial }}</div>
                <n-text>{{ auth.user?.username }}</n-text>
              </div>

              <n-button size="small" @click="handleLogout">登出</n-button>
            </template>
          </n-layout-header>

          <n-layout-content class="surface-cream" style="height: calc(100vh - 60px);">
            <RouterView />
          </n-layout-content>
        </n-layout>
      </n-dialog-provider>
    </n-message-provider>
  </n-config-provider>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { RouterView, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useAttendanceStore } from '@/stores/attendance'
import { themeOverrides } from '@/assets/theme'
import CheckInButton from '@/components/CheckInButton.vue'

const auth = useAuthStore()
const attendance = useAttendanceStore()
const router = useRouter()

const userInitial = computed(() => auth.user?.username?.charAt(0).toUpperCase() ?? '?')

function handleLogout() {
  auth.logout()
  router.push('/login')
}
</script>
