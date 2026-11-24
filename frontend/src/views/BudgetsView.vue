<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { budgetsApi } from '@/api'
import { ApiError } from '@/api/http'
import type { Budget } from '@/api/types'
import AppIcon from '@/components/AppIcon.vue'
import BaseModal from '@/components/BaseModal.vue'
import BudgetProgress from '@/components/BudgetProgress.vue'
import EmptyState from '@/components/EmptyState.vue'
import StatCard from '@/components/StatCard.vue'
import { useAuthStore } from '@/stores/auth'
import { useCatalogStore } from '@/stores/catalog'
import { useToastStore } from '@/stores/toast'
import { formatMoney, monthLabel } from '@/utils/format'

const auth = useAuthStore()
const catalog = useCatalogStore()
const toast = useToastStore()

const now = new Date()
const period = reactive({ year: now.getFullYear(), month: now.getMonth() + 1 })
const budgets = ref<Budget[]>([])
const loading = ref(false)
const showForm = ref(false)
const error = ref<ApiError | null>(null)
const form = reactive({ categoryId: '', limit: '' })

const currency = computed(() => budgets.value[0]?.currency ?? auth.user?.baseCurrency ?? 'CAD')
const totals = computed(() => ({
  limit: budgets.value.reduce((sum, b) => sum + b.limit, 0),
  spent: budgets.value.reduce((sum, b) => sum + b.spent, 0),
}))

async function load() {
  loading.value = true
  try {
    budgets.value = await budgetsApi.list(period.year, period.month)
  } catch (err) {
    toast.error(err)
  } finally {
    loading.value = false
  }
}

function shiftMonth(delta: number) {
  const date = new Date(period.year, period.month - 1 + delta, 1)
  period.year = date.getFullYear()
  period.month = date.getMonth() + 1
  void load()
}

function openForm(budget?: Budget) {
  form.categoryId = budget?.categoryId ?? catalog.expenseCategories[0]?.id ?? ''
  form.limit = budget ? String(budget.limit) : ''
  error.value = null
  showForm.value = true
}

async function save() {
  error.value = null
  try {
    await budgetsApi.upsert({ categoryId: form.categoryId, year: period.year, month: period.month, limit: Number(form.limit) })
    toast.success('Budget saved')
    showForm.value = false
    await load()
  } catch (err) {
    if (err instanceof ApiError) error.value = err
    toast.error(err)
  }
}

async function copyPrevious() {
  try {
    const { copied } = await budgetsApi.copyPrevious(period.year, period.month)
    toast.success(copied ? `Copied ${copied} budget${copied === 1 ? '' : 's'} from last month` : 'Nothing new to copy')
    await load()
  } catch (err) {
    toast.error(err)
  }
}

async function remove(budget: Budget) {
  if (!window.confirm(`Remove the ${budget.categoryName} budget?`)) return
  try {
    await budgetsApi.remove(budget.id)
    await load()
  } catch (err) {
    toast.error(err)
  }
}

onMounted(async () => {
  await catalog.load()
  await load()
})
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div class="flex items-center gap-2">
        <button class="btn-secondary px-2" aria-label="Previous month" @click="shiftMonth(-1)"><AppIcon name="left" :size="16" /></button>
        <span class="min-w-40 text-center font-semibold text-slate-900">{{ monthLabel(period.year, period.month, 'long') }}</span>
        <button class="btn-secondary px-2" aria-label="Next month" @click="shiftMonth(1)"><AppIcon name="right" :size="16" /></button>
      </div>
      <div class="flex gap-2">
        <button class="btn-secondary" @click="copyPrevious">Copy last month</button>
        <button class="btn-primary" @click="openForm()"><AppIcon name="plus" :size="16" /> Set budget</button>
      </div>
    </div>

    <div v-if="budgets.length" class="grid gap-4 sm:grid-cols-3">
      <StatCard label="Budgeted" :value="formatMoney(totals.limit, currency)" />
      <StatCard label="Spent" :value="formatMoney(totals.spent, currency)" :tone="totals.spent > totals.limit ? 'negative' : 'neutral'" />
      <StatCard
        label="Remaining"
        :value="formatMoney(totals.limit - totals.spent, currency)"
        :tone="totals.limit - totals.spent < 0 ? 'negative' : 'positive'"
      />
    </div>

    <section v-if="budgets.length" class="card divide-y divide-slate-100">
      <div v-for="budget in budgets" :key="budget.id" class="flex items-center gap-4 p-5">
        <div class="flex-1"><BudgetProgress :budget="budget" /></div>
        <button class="btn-ghost" :aria-label="`Edit ${budget.categoryName} budget`" @click="openForm(budget)">
          <AppIcon name="pencil" :size="16" />
        </button>
        <button class="btn-ghost hover:text-rose-600" :aria-label="`Remove ${budget.categoryName} budget`" @click="remove(budget)">
          <AppIcon name="trash" :size="16" />
        </button>
      </div>
    </section>

    <EmptyState
      v-else-if="!loading"
      title="No budgets for this month"
      description="Set monthly limits for your expense categories, or copy last month's budgets."
    />

    <BaseModal v-if="showForm" title="Set budget" @close="showForm = false">
      <form class="space-y-4" novalidate @submit.prevent="save">
        <div>
          <label class="label" for="budget-category">Category</label>
          <select id="budget-category" v-model="form.categoryId" class="input">
            <option v-for="c in catalog.expenseCategories" :key="c.id" :value="c.id">{{ c.name }}</option>
          </select>
        </div>
        <div>
          <label class="label" for="budget-limit">Monthly limit ({{ auth.user?.baseCurrency }})</label>
          <input id="budget-limit" v-model="form.limit" class="input" type="number" min="0.01" step="0.01" required />
          <p v-if="error?.field('limit')" class="field-error">{{ error.field('limit') }}</p>
        </div>
        <div class="flex justify-end gap-2 pt-2">
          <button type="button" class="btn-secondary" @click="showForm = false">Cancel</button>
          <button type="submit" class="btn-primary">Save budget</button>
        </div>
      </form>
    </BaseModal>
  </div>
</template>
