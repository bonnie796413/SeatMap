<template>
  <div>
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
      <n-text strong style="font-size: 16px;">員工管理</n-text>
      <n-button type="primary" @click="openCreateModal">新增員工</n-button>
    </div>

    <n-data-table :columns="columns" :data="employees" :loading="loading" />

    <!-- 新增/編輯 Modal -->
    <n-modal v-model:show="showModal" preset="dialog" :title="editTarget ? '編輯員工' : '新增員工'">
      <n-form :model="form" :rules="rules" ref="formRef">
        <n-form-item label="姓名" path="fullName">
          <n-input v-model:value="form.fullName" placeholder="員工姓名" />
        </n-form-item>
        <n-form-item label="部門" path="department">
          <n-input v-model:value="form.department" placeholder="（選填）" />
        </n-form-item>
        <n-form-item label="頭像 URL" path="avatarUrl">
          <n-input v-model:value="form.avatarUrl" placeholder="（選填）" />
        </n-form-item>
        <template v-if="!editTarget">
          <n-form-item label="帳號" path="username">
            <n-input v-model:value="form.username" placeholder="登入帳號" />
          </n-form-item>
          <n-form-item label="密碼" path="password">
            <n-input v-model:value="form.password" type="password" placeholder="至少8碼，英數組合" />
          </n-form-item>
        </template>
      </n-form>
      <template #action>
        <n-button @click="showModal = false">取消</n-button>
        <n-button type="primary" :loading="saving" @click="handleSave">確認</n-button>
      </template>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, h } from 'vue'
import { NButton, NAvatar, NTag, useMessage, useDialog } from 'naive-ui'
import { employeesApi } from '@/api/employees'
import type { Employee } from '@/types'

const message = useMessage()
const dialog = useDialog()

const employees = ref<Employee[]>([])
const loading = ref(false)
const saving = ref(false)
const showModal = ref(false)
const editTarget = ref<Employee | null>(null)
const formRef = ref()

const form = ref({
  fullName: '',
  department: '',
  avatarUrl: '',
  username: '',
  password: '',
})

const rules = {
  fullName: [{ required: true, message: '姓名必填', trigger: 'blur' }],
  username: [{ required: true, message: '帳號必填', trigger: 'blur' }],
  password: [
    { required: true, message: '密碼必填', trigger: 'blur' },
    { min: 8, message: '至少8碼', trigger: 'blur' },
  ],
}

const columns = [
  {
    title: '頭像',
    key: 'avatar',
    width: 60,
    render: (row: Employee) =>
      h(NAvatar, {
        src: row.avatarUrl ?? undefined,
        round: true,
        size: 'small',
      }, { default: () => row.fullName.charAt(0) }),
  },
  { title: '姓名', key: 'fullName' },
  { title: '部門', key: 'department', render: (row: Employee) => row.department ?? '—' },
  { title: '帳號', key: 'username', render: (row: Employee) => row.username ?? '—' },
  {
    title: '在場',
    key: 'isPresent',
    render: (row: Employee) =>
      h(NTag, { type: row.isPresent ? 'success' : 'default', size: 'small' }, {
        default: () => row.isPresent ? '在場' : '不在場',
      }),
  },
  {
    title: '座位',
    key: 'seat',
    render: (row: Employee) => row.seat ? `${row.seat.seatNumber}` : '—',
  },
  {
    title: '操作',
    key: 'actions',
    render: (row: Employee) =>
      h('div', { style: 'display:flex;gap:8px;' }, [
        h(NButton, { size: 'small', onClick: () => openEditModal(row) }, { default: () => '編輯' }),
        h(NButton, { size: 'small', type: 'error', onClick: () => handleDelete(row) }, { default: () => '刪除' }),
      ]),
  },
]

async function load() {
  loading.value = true
  try {
    const res = await employeesApi.list()
    employees.value = res.data
  } finally {
    loading.value = false
  }
}

function openCreateModal() {
  editTarget.value = null
  form.value = { fullName: '', department: '', avatarUrl: '', username: '', password: '' }
  showModal.value = true
}

function openEditModal(emp: Employee) {
  editTarget.value = emp
  form.value = {
    fullName: emp.fullName,
    department: emp.department ?? '',
    avatarUrl: emp.avatarUrl ?? '',
    username: '',
    password: '',
  }
  showModal.value = true
}

async function handleSave() {
  saving.value = true
  try {
    if (editTarget.value) {
      await employeesApi.update(editTarget.value.id, {
        fullName: form.value.fullName,
        department: form.value.department || undefined,
        avatarUrl: form.value.avatarUrl || undefined,
      })
      message.success('更新成功')
    } else {
      await employeesApi.create({
        fullName: form.value.fullName,
        department: form.value.department || undefined,
        avatarUrl: form.value.avatarUrl || undefined,
        username: form.value.username,
        password: form.value.password,
      })
      message.success('員工已新增，帳號可立即登入')
    }
    showModal.value = false
    await load()
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : null) ?? '操作失敗')
  } finally {
    saving.value = false
  }
}

function handleDelete(emp: Employee) {
  dialog.warning({
    title: '確認刪除員工',
    content: `刪除「${emp.fullName}」將一併移除其登入帳號、座位指派與打卡狀態，確定？`,
    positiveText: '確認刪除',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await employeesApi.remove(emp.id)
        message.success('已刪除')
        await load()
      } catch (e: unknown) {
        message.error((e instanceof Error ? e.message : null) ?? '刪除失敗')
      }
    },
  })
}

onMounted(load)
</script>
