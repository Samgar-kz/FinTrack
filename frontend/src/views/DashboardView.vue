<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { budgetsApi, reportsApi, transactionsApi } from '@/api'
import type { Budget, CashFlowPoint, CategoryBreakdown, Summary, Transaction } from '@/api/types'
import BaseModal from '@/components/BaseModal.vue'
import BudgetProgress from '@/components/BudgetProgress.vue'
import CashFlowChart from '@/components/CashFlowChart.vue'
import CategoryDoughnut from '@/components/CategoryDoughnut.vue'
import EmptyState from '@/components/EmptyState.vue'
import AppIcon from '@/components/AppIcon.vue'
import StatCard from '@/components/StatCard.vue'
import TransactionForm from '@/components/TransactionForm.vue'
import TransactionTable from '@/components/TransactionTable.vue'
import { useAuthStore } from '@/stores/auth'
import { useCatalogStore } from '@/stores/catalog'
import { useToastStore } from '@/stores/toast'
import { formatMoney } from '@/utils/format'

const auth = useAuthStore()
const catalog = useCatalogStore()
const toast = useToastStore()

const summary = ref<Summary | null>(null)
const cashFlow = ref<CashFlowPoint[]>([])
const spending = ref<CategoryBreakdown[]>([])
const budgets = ref<Budget[]>([])
const recent = ref<Transaction[]>([])
const loading = ref(true)
const showForm = ref(false)

const currency = computed(() => auth.user?.baseCurrency ?? 'CAD')
const today = new Date()

async function load() {
  loading.value = true
  try {
    await catalog.load()
    const [s, flow, cats, b, tx] = await Promise.all([
      reportsApi.summary({}),
      reportsApi.cashFlow(12),
      reportsApi.categories({}, 'Expense'),
      budgetsApi.list(today.getFullYear(), today.getMonth() + 1),
      transactionsApi.search({ pageSize: 6 }),
    ])
    summary.value = s
    cashFlow.value = flow
    spending.value = cats
    budgets.value = b
    recent.value = tx.items
  } catch (err) {
    toast.error(err)
  } finally {
    loading.value = false
  }
}

async function onSaved() {
  showForm.value = false
  await load()
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <p class="text-sm text-slate-500">
          {{ today.toLocaleDateString('en-CA', { weekday: 'long', month: 'long', day: 'numeric' }) }}
        </p>
        <h2 class="text-2xl font-semibold text-slate-900">Hi {{ auth.user?.fullName.split(' ')[0] }} 👋</h2>
      </div>
      <button class="btn-primary" :disabled="catalog.activeAccounts.length === 0" @click="showForm = true">
        <AppIcon name="plus" :size="16" /> Add transaction
      </button>
    </div>

    <EmptyState
      v-if="!loading && catalog.accounts.length === 0"
      title="Add your first account"
      description="Create a chequing, savings or credit card account to start tracking transactions."
    >
      <RouterLink to="/accounts" class="btn-primary">Add account</RouterLink>
    </EmptyState>

    <template v-else>
      <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Total balance" :value="summary ? formatMoney(summary.totalBalance, currency) : '—'" :hint="`Active ${currency} accounts`" />
        <StatCard label="Income this month" :value="summary ? formatMoney(summary.income, currency) : '—'" tone="positive" />
        <StatCard label="Expenses this month" :value="summary ? formatMoney(summary.expenses, currency) : '—'" tone="negative" />
        <StatCard
          label="Savings rate"
          :value="summary ? `${summary.savingsRate}%` : '—'"
          :hint="summary ? `Net ${formatMoney(summary.net, currency, { signed: true })}` : undefined"
          :tone="summary && summary.net < 0 ? 'negative' : 'neutral'"
        />
      </div>

      <div class="grid gap-6 xl:grid-cols-3">
        <section class="card p-5 xl:col-span-2">
          <h3 class="mb-4 font-semibold text-slate-900">Cash flow · last 12 months</h3>
          <CashFlowChart :points="cashFlow" :currency="currency" />
        </section>
        <section class="card p-5">
          <div class="mb-4 flex items-center justify-between">
            <h3 class="font-semibold text-slate-900">Budgets this month</h3>
            <RouterLink to="/budgets" class="text-sm font-medium text-brand-600 hover:text-brand-700">Manage</RouterLink>
          </div>
          <div v-if="budgets.length" class="space-y-5">
            <BudgetProgress v-for="budget in budgets.slice(0, 5)" :key="budget.id" :budget="budget" />
          </div>
          <p v-else class="text-sm text-slate-500">No budgets set for this month yet.</p>
        </section>
      </div>

      <div class="grid gap-6 xl:grid-cols-5">
        <section class="card p-5 xl:col-span-2">
          <h3 class="mb-4 font-semibold text-slate-900">Spending by category</h3>
          <CategoryDoughnut v-if="spending.length" :items="spending" :currency="currency" />
          <p v-else class="text-sm text-slate-500">No expenses recorded this month.</p>
        </section>
        <section class="card overflow-hidden xl:col-span-3">
          <div class="flex items-center justify-between p-5 pb-3">
            <h3 class="font-semibold text-slate-900">Recent transactions</h3>
            <RouterLink to="/transactions" class="text-sm font-medium text-brand-600 hover:text-brand-700">View all</RouterLink>
          </div>
          <TransactionTable v-if="recent.length" :transactions="recent" compact />
          <p v-else class="px-5 pb-5 text-sm text-slate-500">No transactions yet.</p>
        </section>
      </div>
    </template>

    <BaseModal v-if="showForm" title="New transaction" @close="showForm = false">
      <TransactionForm @saved="onSaved" @cancel="showForm = false" />
    </BaseModal>
  </div>
</template>
