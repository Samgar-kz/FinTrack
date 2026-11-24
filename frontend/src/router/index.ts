import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
    admin?: boolean
    title?: string
  }
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/login', name: 'login', component: () => import('@/views/LoginView.vue'), meta: { public: true, title: 'Sign in' } },
    { path: '/register', name: 'register', component: () => import('@/views/RegisterView.vue'), meta: { public: true, title: 'Create account' } },
    { path: '/', name: 'dashboard', component: () => import('@/views/DashboardView.vue'), meta: { title: 'Dashboard' } },
    { path: '/transactions', name: 'transactions', component: () => import('@/views/TransactionsView.vue'), meta: { title: 'Transactions' } },
    { path: '/accounts', name: 'accounts', component: () => import('@/views/AccountsView.vue'), meta: { title: 'Accounts' } },
    { path: '/budgets', name: 'budgets', component: () => import('@/views/BudgetsView.vue'), meta: { title: 'Budgets' } },
    { path: '/reports', name: 'reports', component: () => import('@/views/ReportsView.vue'), meta: { title: 'Reports' } },
    { path: '/categories', name: 'categories', component: () => import('@/views/CategoriesView.vue'), meta: { title: 'Categories' } },
    { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue'), meta: { title: 'Settings' } },
    { path: '/admin', name: 'admin', component: () => import('@/views/AdminView.vue'), meta: { admin: true, title: 'Admin' } },
    { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('@/views/NotFoundView.vue'), meta: { public: true, title: 'Not found' } },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  await auth.restore()

  if (!to.meta.public && !auth.isAuthenticated) {
    return { name: 'login', query: to.fullPath !== '/' ? { redirect: to.fullPath } : {} }
  }
  if ((to.name === 'login' || to.name === 'register') && auth.isAuthenticated) {
    return { name: 'dashboard' }
  }
  if (to.meta.admin && !auth.isAdmin) {
    return { name: 'dashboard' }
  }
})

router.afterEach((to) => {
  document.title = to.meta.title ? `${to.meta.title} · FinTrack` : 'FinTrack'
})

export default router
