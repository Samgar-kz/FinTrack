<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { transactionsApi } from '@/api'
import { ApiError } from '@/api/http'
import type { Transaction } from '@/api/types'
import { useCatalogStore } from '@/stores/catalog'
import { useToastStore } from '@/stores/toast'
import { isoDate } from '@/utils/format'

const props = defineProps<{ transaction?: Transaction | null; initialMode?: 'transaction' | 'transfer' }>()
const emit = defineEmits<{ saved: []; cancel: [] }>()

const catalog = useCatalogStore()
const toast = useToastStore()

const isEdit = computed(() => !!props.transaction)
const mode = ref<'transaction' | 'transfer'>(props.initialMode ?? 'transaction')
const saving = ref(false)
const error = ref<ApiError | null>(null)

const form = reactive({
  type: (props.transaction?.type === 'Income' ? 'Income' : 'Expense') as 'Income' | 'Expense',
  accountId: props.transaction?.accountId ?? catalog.activeAccounts[0]?.id ?? '',
  toAccountId: catalog.activeAccounts[1]?.id ?? '',
  amount: props.transaction ? String(props.transaction.amount) : '',
  date: props.transaction?.date ?? isoDate(),
  description: props.transaction?.description ?? '',
  categoryId: props.transaction?.categoryId ?? '',
  notes: props.transaction?.notes ?? '',
})

const categories = computed(() => (form.type === 'Income' ? catalog.incomeCategories : catalog.expenseCategories))

watch(
  () => form.type,
  () => {
    if (!categories.value.some((c) => c.id === form.categoryId)) form.categoryId = ''
  },
)

const fieldError = (name: string) => error.value?.field(name)

async function submit() {
  saving.value = true
  error.value = null
  try {
    const amount = Number(form.amount)
    if (mode.value === 'transfer') {
      await transactionsApi.transfer({
        fromAccountId: form.accountId,
        toAccountId: form.toAccountId,
        amount,
        date: form.date,
        description: form.description,
      })
      toast.success('Transfer recorded')
    } else {
      const body = {
        accountId: form.accountId,
        type: form.type,
        amount,
        date: form.date,
        description: form.description,
        categoryId: form.categoryId || null,
        notes: form.notes || null,
      }
      if (props.transaction) {
        await transactionsApi.update(props.transaction.id, body)
        toast.success('Transaction updated')
      } else {
        await transactionsApi.create(body)
        toast.success('Transaction added')
      }
    }
    await catalog.load(true)
    emit('saved')
  } catch (err) {
    if (err instanceof ApiError) error.value = err
    toast.error(err)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <form class="space-y-4" novalidate @submit.prevent="submit">
    <div v-if="!isEdit" class="grid grid-cols-2 gap-1 rounded-lg bg-slate-100 p-1 text-sm font-medium">
      <button
        type="button"
        class="rounded-md py-1.5"
        :class="mode === 'transaction' ? 'bg-white shadow-sm' : 'text-slate-500'"
        @click="mode = 'transaction'"
      >
        Income / expense
      </button>
      <button
        type="button"
        class="rounded-md py-1.5"
        :class="mode === 'transfer' ? 'bg-white shadow-sm' : 'text-slate-500'"
        @click="mode = 'transfer'"
      >
        Transfer
      </button>
    </div>

    <div v-if="mode === 'transaction'" class="grid grid-cols-2 gap-3">
      <label
        v-for="option in ['Expense', 'Income'] as const"
        :key="option"
        class="flex cursor-pointer items-center justify-center rounded-lg border px-3 py-2 text-sm font-medium"
        :class="
          form.type === option
            ? option === 'Income'
              ? 'border-emerald-500 bg-emerald-50 text-emerald-700'
              : 'border-rose-500 bg-rose-50 text-rose-700'
            : 'border-slate-300 text-slate-600'
        "
      >
        <input v-model="form.type" type="radio" class="sr-only" :value="option" />
        {{ option }}
      </label>
    </div>

    <div class="grid gap-4 sm:grid-cols-2">
      <div>
        <label class="label" for="tx-account">{{ mode === 'transfer' ? 'From account' : 'Account' }}</label>
        <select id="tx-account" v-model="form.accountId" class="input" required>
          <option v-for="account in catalog.activeAccounts" :key="account.id" :value="account.id">
            {{ account.name }} ({{ account.currency }})
          </option>
        </select>
        <p v-if="fieldError('accountId') || fieldError('fromAccountId')" class="field-error">
          {{ fieldError('accountId') ?? fieldError('fromAccountId') }}
        </p>
      </div>

      <div v-if="mode === 'transfer'">
        <label class="label" for="tx-to">To account</label>
        <select id="tx-to" v-model="form.toAccountId" class="input" required>
          <option v-for="account in catalog.activeAccounts" :key="account.id" :value="account.id">
            {{ account.name }} ({{ account.currency }})
          </option>
        </select>
        <p v-if="fieldError('toAccountId')" class="field-error">{{ fieldError('toAccountId') }}</p>
      </div>

      <div v-else>
        <label class="label" for="tx-category">Category</label>
        <select id="tx-category" v-model="form.categoryId" class="input">
          <option value="">Uncategorised</option>
          <option v-for="category in categories" :key="category.id" :value="category.id">{{ category.name }}</option>
        </select>
      </div>

      <div>
        <label class="label" for="tx-amount">Amount</label>
        <input id="tx-amount" v-model="form.amount" class="input" type="number" min="0.01" step="0.01" inputmode="decimal" required />
        <p v-if="fieldError('amount')" class="field-error">{{ fieldError('amount') }}</p>
      </div>

      <div>
        <label class="label" for="tx-date">Date</label>
        <input id="tx-date" v-model="form.date" class="input" type="date" required />
        <p v-if="fieldError('date')" class="field-error">{{ fieldError('date') }}</p>
      </div>
    </div>

    <div>
      <label class="label" for="tx-description">Description</label>
      <input id="tx-description" v-model="form.description" class="input" maxlength="200" required placeholder="e.g. Grocery store" />
      <p v-if="fieldError('description')" class="field-error">{{ fieldError('description') }}</p>
    </div>

    <div v-if="mode === 'transaction'">
      <label class="label" for="tx-notes">Notes <span class="font-normal text-slate-400">(optional)</span></label>
      <textarea id="tx-notes" v-model="form.notes" class="input" rows="2" maxlength="1000" />
    </div>

    <div class="flex justify-end gap-2 pt-2">
      <button type="button" class="btn-secondary" @click="emit('cancel')">Cancel</button>
      <button type="submit" class="btn-primary" :disabled="saving">
        {{ saving ? 'Saving…' : isEdit ? 'Save changes' : mode === 'transfer' ? 'Record transfer' : 'Add transaction' }}
      </button>
    </div>
  </form>
</template>
