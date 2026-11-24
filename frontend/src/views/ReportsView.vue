<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { reportsApi } from '@/api'
import { CURRENCIES, type CashFlowPoint, type CategoryBreakdown, type Summary } from '@/api/types'
import CashFlowChart from '@/components/CashFlowChart.vue'
import CategoryDoughnut from '@/components/CategoryDoughnut.vue'
import StatCard from '@/components/StatCard.vue'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toast'
import { formatMoney, isoDate } from '@/utils/format'

const auth = useAuthStore()
const toast = useToastStore()

type Preset = 'this-month' | 'last-month' | 'last-3' | 'ytd' | 'last-12'
const presets: { value: Preset; label: string }[] = [
  { value: 'this-month', label: 'This month' },
  { value: 'last-month', label: 'Last month' },
  { value: 'last-3', label: 'Last 3 months' },
  { value: 'ytd', label: 'Year to date' },
  { value: 'last-12', label: 'Last 12 months' },
]

const preset = ref<Preset>('this-month')
const currency = ref(auth.user?.baseCurrency ?? 'CAD')
const summary = ref<Summary | null>(null)
const expenses = ref<CategoryBreakdown[]>([])
const income = ref<CategoryBreakdown[]>([])
const cashFlow = ref<CashFlowPoint[]>([])

const range = computed(() => {
  const now = new Date()
  const y = now.getFullYear()
  const m = now.getMonth()
  switch (preset.value) {
    case 'last-month':
      return { from: isoDate(new Date(y, m - 1, 1)), to: isoDate(new Date(y, m, 0)) }
    case 'last-3':
      return { from: isoDate(new Date(y, m - 2, 1)), to: isoDate(new Date(y, m + 1, 0)) }
    case 'ytd':
      return { from: isoDate(new Date(y, 0, 1)), to: isoDate(now) }
    case 'last-12':
      return { from: isoDate(new Date(y, m - 11, 1)), to: isoDate(new Date(y, m + 1, 0)) }
    default:
      return { from: isoDate(new Date(y, m, 1)), to: isoDate(new Date(y, m + 1, 0)) }
  }
})

async function load() {
  const params = { ...range.value, currency: currency.value }
  try {
    const [s, e, i, flow] = await Promise.all([
      reportsApi.summary(params),
      reportsApi.categories(params, 'Expense'),
      reportsApi.categories(params, 'Income'),
      reportsApi.cashFlow(12, currency.value),
    ])
    summary.value = s
    expenses.value = e
    income.value = i
    cashFlow.value = flow
  } catch (err) {
    toast.error(err)
  }
}

watch([preset, currency], load)
onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div class="flex flex-wrap gap-1 rounded-lg bg-white p-1 shadow-sm ring-1 ring-slate-200">
        <button
          v-for="p in presets"
          :key="p.value"
          class="rounded-md px-3 py-1.5 text-sm font-medium"
          :class="preset === p.value ? 'bg-brand-600 text-white' : 'text-slate-600 hover:bg-slate-100'"
          @click="preset = p.value"
        >
          {{ p.label }}
        </button>
      </div>
      <select v-model="currency" class="input w-28" aria-label="Currency">
        <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
      </select>
    </div>

    <div v-if="summary" class="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <StatCard label="Income" :value="formatMoney(summary.income, currency)" tone="positive" />
      <StatCard label="Expenses" :value="formatMoney(summary.expenses, currency)" tone="negative" />
      <StatCard label="Net" :value="formatMoney(summary.net, currency, { signed: true })" :tone="summary.net < 0 ? 'negative' : 'positive'" />
      <StatCard label="Savings rate" :value="`${summary.savingsRate}%`" :hint="`${summary.transactionCount} transactions`" />
    </div>

    <div class="grid gap-6 xl:grid-cols-2">
      <section class="card p-5">
        <h3 class="mb-4 font-semibold text-slate-900">Expenses by category</h3>
        <CategoryDoughnut v-if="expenses.length" :items="expenses" :currency="currency" />
        <p v-else class="text-sm text-slate-500">No expenses in this period.</p>
      </section>
      <section class="card p-5">
        <h3 class="mb-4 font-semibold text-slate-900">Income by category</h3>
        <CategoryDoughnut v-if="income.length" :items="income" :currency="currency" />
        <p v-else class="text-sm text-slate-500">No income in this period.</p>
      </section>
    </div>

    <section class="card p-5">
      <h3 class="mb-4 font-semibold text-slate-900">Cash flow · last 12 months ({{ currency }})</h3>
      <CashFlowChart :points="cashFlow" :currency="currency" />
    </section>
  </div>
</template>
