<template>
  <div style="display: flex; justify-content: center; align-items: center; height: 100%;">
    <n-card title="登入" style="width: 360px;">
      <n-form @submit.prevent="handleLogin">
        <n-form-item label="帳號">
          <n-input v-model:value="form.username" placeholder="請輸入帳號" />
        </n-form-item>
        <n-form-item label="密碼">
          <n-input
            v-model:value="form.password"
            type="password"
            placeholder="請輸入密碼"
            show-password-on="click"
          />
        </n-form-item>
        <n-alert v-if="errorMsg" type="error" style="margin-bottom: 12px;">
          {{ errorMsg }}
        </n-alert>
        <n-button
          type="primary"
          attr-type="submit"
          :loading="loading"
          block
        >
          登入
        </n-button>
      </n-form>
    </n-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const loading = ref(false)
const errorMsg = ref('')
const form = ref({ username: '', password: '' })

async function handleLogin() {
  if (!form.value.username || !form.value.password) {
    errorMsg.value = '請輸入帳號與密碼'
    return
  }
  loading.value = true
  errorMsg.value = ''
  try {
    await auth.login(form.value.username, form.value.password)
    router.push('/')
  } catch (e: unknown) {
    errorMsg.value = (e instanceof Error ? e.message : null) ?? '登入失敗，請確認帳號密碼'
  } finally {
    loading.value = false
  }
}
</script>
