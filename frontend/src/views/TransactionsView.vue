<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { transactionsApi } from '@/api'
import type { PagedResult, Transaction, TransactionQuery } from '@/api/types'
import AppIcon from '@/components/AppIcon.vue'
import BaseModal from '@/components/BaseModal.vue'
import EmptyState from '@/components/EmptyState.vue'
import PaginationBar from '@/components/PaginationBar.vue'
import TransactionForm from '@/components/TransactionForm.vue'
import TransactionTable from '@/components/TransactionTable.vue'
import { useCatalogStore } from '@/stores/catalog'
import { useToastStore } from '@/stores/toast'

const catalog = useCatalogStore()
const toast = useToastStore()

const filters = reactive<TransactionQuery>({ search: '', sort: 'DateDesc', page: 1, pageSize: 20 })
const result = ref<PagedResult<Transaction> | null>(null)
const loading = ref(false)
const editing = ref<Transaction | null>(null)
const formMode = ref<'transaction' | 'transfer' | null>(null)
let searchTimer: ReturnType<typeof setTimeout> | undefined

async function load() {
  loading.value = true
  try {
    result.value = await transactionsApi.search(filters)
  } catch (err) {
    toast.error(err)
  } finally {
    loading.value = false
  }
}

watch(
  () => [filters.accountId, filters.categoryId, filters.type, filters.from, filters.to, filters.sort],
  () => {
    filters.page = 1
    void load()
  },
)

watch(
  () => filters.search,
  () => {
    clearTimeout(searchTimer)
    searchTimer = setTimeout(() => {
      filters.page = 1
      void load()
    }, 300)
  },
)

function changePage(page: number) {
  filters.page = page
  void load()
}

function openCreate(mode: 'transaction' | 'transfer') {
  editing.value = null
  formMode.value = mode
}

function openEdit(transaction: Transaction) {
  editing.value = transaction
  formMode.value = 'transaction'
}

function closeForm() {
  formMode.value = null
  editing.value = null
}

async function onSaved() {
  closeForm()
  await load()
}

async function remove(transaction: Transaction) {
  const message = transaction.transferId
    ? 'Delete this transfer? Both sides of the transfer will be removed.'
    : `Delete "${transaction.description}"?`
  if (!window.confirm(message)) return
  try {
    await transactionsApi.remove(transaction.id)
    toast.success('Transaction deleted')
    await Promise.all([load(), catalog.load(true)])
  } catch (err) {
    toast.error(err)
  }
}

async function exportCsv() {
  try {
    await transactionsApi.exportCsv(filters)
  } catch (err) {
    toast.error(err)
  }
}

function resetFilters() {
  Object.assign(filters, { search: '', accountId: undefined, categoryId: undefined, type: undefined, from: undefined, to: undefined })
}

onMounted(async () => {
  await catalog.load()
  await load()
})
</script>

<template>
  <div class="space-y-4">
    <div class="flex flex-wrap items-center justify-end gap-2">
      <button class="btn-secondary" @click="exportCsv"><AppIcon name="download" :size="16" /> Export CSV</button>
      <button class="btn-secondary" :disabled="catalog.activeAccounts.length < 2" @click="openCreate('transfer')">
        <AppIcon name="transfer" :size="16" /> Transfer
      </button>
      <button class="btn-primary" :disabled="catalog.activeAccounts.length === 0" @click="openCreate('transaction')">
        <AppIcon name="plus" :size="16" /> Add transaction
      </button>
    </div>

    <div class="card grid gap-3 p-4 sm:grid-cols-2 lg:grid-cols-6">
      <input v-model="filters.search" class="input lg:col-span-2" type="search" placeholder="Search description or notes" aria-label="Search" />
      <select v-model="filters.accountId" class="input" aria-label="Account">
        <option :value="undefined">All accounts</option>
        <option v-for="a in catalog.accounts" :key="a.id" :value="a.id">{{ a.name }}</option>
      </select>
      <select v-model="filters.categoryId" class="input" aria-label="Category">
        <option :value="undefined">All categories</option>
        <option v-for="c in catalog.categories" :key="c.id" :value="c.id">{{ c.name }} ({{ c.kind }})</option>
      </select>
      <select v-model="filters.type" class="input" aria-label="Type">
        <option :value="undefined">All types</option>
        <option value="Income">Income</option>
        <option value="Expense">Expense</option>
        <option value="TransferIn">Transfer in</option>
        <option value="TransferOut">Transfer out</option>
      </select>
      <select v-model="filters.sort" class="input" aria-label="Sort">
        <option value="DateDesc">Newest first</option>
        <option value="DateAsc">Oldest first</option>
        <option value="AmountDesc">Largest amount</option>
        <option value="AmountAsc">Smallest amount</option>
      </select>
      <div class="flex items-center gap-2 lg:col-span-2">
        <input v-model="filters.from" class="input" type="date" aria-label="From date" />
        <span class="text-slate-400">–</span>
        <input v-model="filters.to" class="input" type="date" aria-label="To date" />
      </div>
      <button class="btn-ghost justify-self-start" @click="resetFilters">Clear filters</button>
    </div>

    <section class="card overflow-hidden">
      <TransactionTable v-if="result?.items.length" :transactions="result.items" @edit="openEdit" @remove="remove" />
      <div v-else-if="!loading" class="p-6">
        <EmptyState title="No transactions found" description="Try changing the filters or add a new transaction." />
      </div>
      <div v-else class="p-6 text-sm text-slate-500">Loading…</div>
      <PaginationBar
        v-if="result && result.totalCount > 0"
        :page="result.page"
        :total-pages="result.totalPages"
        :total-count="result.totalCount"
        @change="changePage"
      />
    </section>

    <BaseModal
      v-if="formMode"
      :title="editing ? 'Edit transaction' : formMode === 'transfer' ? 'New transfer' : 'New transaction'"
      @close="closeForm"
    >
      <TransactionForm :transaction="editing" :initial-mode="formMode" @saved="onSaved" @cancel="closeForm" />
    </BaseModal>
  </div>
</template>
