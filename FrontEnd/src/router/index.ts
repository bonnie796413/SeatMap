import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
    },
    {
      path: '/',
      name: 'map',
      component: () => import('@/views/MapView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/admin',
      name: 'admin',
      redirect: { name: 'admin-floors' },
      component: () => import('@/views/admin/AdminView.vue'),
      meta: { requiresAuth: true, requiresAdmin: true },
      children: [
        {
          path: 'floors',
          name: 'admin-floors',
          component: () => import('@/views/admin/FloorAdminView.vue'),
        },
        {
          path: 'employees',
          name: 'admin-employees',
          component: () => import('@/views/admin/EmployeeAdminView.vue'),
        },
      ],
    },
    {
      path: '/:pathMatch(.*)*',
      redirect: '/',
    },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()

  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return { name: 'login' }
  }
  if (to.name === 'login' && auth.isAuthenticated) {
    return { name: 'map' }
  }
  if (to.meta.requiresAdmin && !auth.isAdmin) {
    return { name: 'map' }
  }
})

export default router
