<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { adminApi } from '@/api'
import type { AdminUser, PagedResult, PlatformStats } from '@/api/types'
import PaginationBar from '@/components/PaginationBar.vue'
import StatCard from '@/components/StatCard.vue'
import { useToastStore } from '@/stores/toast'

const toast = useToastStore()
const stats = ref<PlatformStats | null>(null)
const users = ref<PagedResult<AdminUser> | null>(null)
const search = ref('')
const page = ref(1)
let timer: ReturnType<typeof setTimeout> | undefined

async function loadUsers() {
  try {
    users.value = await adminApi.users(search.value || undefined, page.value)
  } catch (err) {
    toast.error(err)
  }
}

watch(search, () => {
  clearTimeout(timer)
  timer = setTimeout(() => {
    page.value = 1
    void loadUsers()
  }, 300)
})

function changePage(next: number) {
  page.value = next
  void loadUsers()
}

const date = (value: string | null) => (value ? new Date(value).toLocaleDateString('en-CA') : '—')

onMounted(async () => {
  try {
    stats.value = await adminApi.stats()
  } catch (err) {
    toast.error(err)
  }
  await loadUsers()
})
</script>

<template>
  <div class="space-y-6">
    <div v-if="stats" class="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <StatCard label="Users" :value="String(stats.users)" />
      <StatCard label="Active (30 days)" :value="String(stats.activeUsersLast30Days)" />
      <StatCard label="Accounts" :value="String(stats.accounts)" />
      <StatCard label="Transactions" :value="String(stats.transactions)" />
    </div>

    <section class="card overflow-hidden">
      <div class="flex flex-wrap items-center justify-between gap-3 p-5">
        <h2 class="font-semibold text-slate-900">Users</h2>
        <input v-model="search" class="input max-w-xs" type="search" placeholder="Search name or email" aria-label="Search users" />
      </div>
      <div class="overflow-x-auto">
        <table class="min-w-full divide-y divide-slate-200">
          <thead class="bg-slate-50">
            <tr>
              <th class="table-head">Name</th>
              <th class="table-head">Email</th>
              <th class="table-head">Role</th>
              <th class="table-head">Joined</th>
              <th class="table-head">Last sign-in</th>
              <th class="table-head text-right">Accounts</th>
              <th class="table-head text-right">Transactions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            <tr v-for="user in users?.items" :key="user.id">
              <td class="table-cell font-medium text-slate-900">{{ user.fullName }}</td>
              <td class="table-cell text-slate-600">{{ user.email }}</td>
              <td class="table-cell">
                <span
                  class="rounded-full px-2 py-0.5 text-xs font-medium"
                  :class="user.role === 'Admin' ? 'bg-brand-100 text-brand-700' : 'bg-slate-100 text-slate-600'"
                >
                  {{ user.role }}
                </span>
              </td>
              <td class="table-cell text-slate-500">{{ date(user.createdAt) }}</td>
              <td class="table-cell text-slate-500">{{ date(user.lastLoginAt) }}</td>
              <td class="table-cell text-right tabular-nums">{{ user.accountCount }}</td>
              <td class="table-cell text-right tabular-nums">{{ user.transactionCount }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <PaginationBar v-if="users" :page="users.page" :total-pages="users.totalPages" :total-count="users.totalCount" @change="changePage" />
    </section>
  </div>
</template>
