<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { accountsApi } from '@/api'
import { ApiError } from '@/api/http'
import { ACCOUNT_TYPES, CURRENCIES, type Account, type AccountType } from '@/api/types'
import AppIcon from '@/components/AppIcon.vue'
import BaseModal from '@/components/BaseModal.vue'
import EmptyState from '@/components/EmptyState.vue'
import { useAuthStore } from '@/stores/auth'
import { useCatalogStore } from '@/stores/catalog'
import { useToastStore } from '@/stores/toast'
import { formatMoney } from '@/utils/format'

const auth = useAuthStore()
const catalog = useCatalogStore()
const toast = useToastStore()

const showArchived = ref(false)
const editing = ref<Account | null>(null)
const showForm = ref(false)
const saving = ref(false)
const error = ref<ApiError | null>(null)
const form = reactive({ name: '', type: 'Checking' as AccountType, currency: 'CAD', openingBalance: '0' })

const typeLabel = (type: AccountType) => ACCOUNT_TYPES.find((t) => t.value === type)?.label ?? type

const groups = computed(() => {
  const visible = catalog.accounts.filter((a) => showArchived.value || !a.isArchived)
  const byCurrency = new Map<string, Account[]>()
  for (const account of visible) {
    byCurrency.set(account.currency, [...(byCurrency.get(account.currency) ?? []), account])
  }
  return [...byCurrency.entries()].map(([currency, accounts]) => ({
    currency,
    accounts,
    total: accounts.filter((a) => !a.isArchived).reduce((sum, a) => sum + a.balance, 0),
  }))
})

function openCreate() {
  editing.value = null
  Object.assign(form, { name: '', type: 'Checking', currency: auth.user?.baseCurrency ?? 'CAD', openingBalance: '0' })
  error.value = null
  showForm.value = true
}

function openEdit(account: Account) {
  editing.value = account
  Object.assign(form, { name: account.name, type: account.type, currency: account.currency, openingBalance: String(account.openingBalance) })
  error.value = null
  showForm.value = true
}

async function save() {
  saving.value = true
  error.value = null
  try {
    if (editing.value) {
      await accountsApi.update(editing.value.id, { name: form.name, type: form.type })
      toast.success('Account updated')
    } else {
      await accountsApi.create({ name: form.name, type: form.type, currency: form.currency, openingBalance: Number(form.openingBalance) })
      toast.success('Account created')
    }
    showForm.value = false
    await catalog.load(true)
  } catch (err) {
    if (err instanceof ApiError) error.value = err
    toast.error(err)
  } finally {
    saving.value = false
  }
}

async function toggleArchive(account: Account) {
  try {
    await (account.isArchived ? accountsApi.restore(account.id) : accountsApi.archive(account.id))
    toast.success(account.isArchived ? 'Account restored' : 'Account archived')
    await catalog.load(true)
  } catch (err) {
    toast.error(err)
  }
}

async function remove(account: Account) {
  if (!window.confirm(`Delete "${account.name}"? This cannot be undone.`)) return
  try {
    await accountsApi.remove(account.id)
    toast.success('Account deleted')
    await catalog.load(true)
  } catch (err) {
    toast.error(err)
  }
}

onMounted(() => catalog.load(true))
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <label class="flex items-center gap-2 text-sm text-slate-600">
        <input v-model="showArchived" type="checkbox" class="rounded border-slate-300" /> Show archived
      </label>
      <button class="btn-primary" @click="openCreate"><AppIcon name="plus" :size="16" /> Add account</button>
    </div>

    <EmptyState v-if="catalog.loaded && groups.length === 0" title="No accounts yet" description="Add your bank accounts, cards and wallets.">
      <button class="btn-primary" @click="openCreate">Add account</button>
    </EmptyState>

    <section v-for="group in groups" :key="group.currency">
      <div class="mb-3 flex items-baseline justify-between">
        <h2 class="font-semibold text-slate-900">{{ group.currency }} accounts</h2>
        <p class="text-sm text-slate-500">
          Net worth <span class="font-semibold text-slate-900">{{ formatMoney(group.total, group.currency) }}</span>
        </p>
      </div>
      <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <article v-for="account in group.accounts" :key="account.id" class="card p-5" :class="{ 'opacity-60': account.isArchived }">
          <div class="flex items-start justify-between gap-2">
            <div>
              <p class="font-medium text-slate-900">{{ account.name }}</p>
              <p class="text-xs text-slate-500">
                {{ typeLabel(account.type) }}<span v-if="account.isArchived"> · Archived</span>
              </p>
            </div>
            <div class="flex">
              <button class="btn-ghost" :aria-label="`Edit ${account.name}`" @click="openEdit(account)">
                <AppIcon name="pencil" :size="16" />
              </button>
              <button
                class="btn-ghost"
                :aria-label="account.isArchived ? `Restore ${account.name}` : `Archive ${account.name}`"
                :title="account.isArchived ? 'Restore' : 'Archive'"
                @click="toggleArchive(account)"
              >
                <AppIcon name="archive" :size="16" />
              </button>
              <button class="btn-ghost hover:text-rose-600" :aria-label="`Delete ${account.name}`" @click="remove(account)">
                <AppIcon name="trash" :size="16" />
              </button>
            </div>
          </div>
          <p class="mt-4 text-2xl font-semibold tabular-nums" :class="account.balance < 0 ? 'text-rose-600' : 'text-slate-900'">
            {{ formatMoney(account.balance, account.currency) }}
          </p>
          <p class="mt-1 text-xs text-slate-500">Opening balance {{ formatMoney(account.openingBalance, account.currency) }}</p>
        </article>
      </div>
    </section>

    <BaseModal v-if="showForm" :title="editing ? 'Edit account' : 'New account'" @close="showForm = false">
      <form class="space-y-4" novalidate @submit.prevent="save">
        <div>
          <label class="label" for="acc-name">Name</label>
          <input id="acc-name" v-model="form.name" class="input" maxlength="100" placeholder="e.g. Everyday Chequing" required />
          <p v-if="error?.field('name')" class="field-error">{{ error.field('name') }}</p>
        </div>
        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label class="label" for="acc-type">Type</label>
            <select id="acc-type" v-model="form.type" class="input">
              <option v-for="t in ACCOUNT_TYPES" :key="t.value" :value="t.value">{{ t.label }}</option>
            </select>
          </div>
          <div>
            <label class="label" for="acc-currency">Currency</label>
            <select id="acc-currency" v-model="form.currency" class="input" :disabled="!!editing">
              <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
            </select>
          </div>
        </div>
        <div v-if="!editing">
          <label class="label" for="acc-opening">Opening balance</label>
          <input id="acc-opening" v-model="form.openingBalance" class="input" type="number" step="0.01" />
          <p class="mt-1 text-xs text-slate-500">Use a negative value for money owed, e.g. a credit card balance.</p>
          <p v-if="error?.field('openingBalance')" class="field-error">{{ error.field('openingBalance') }}</p>
        </div>
        <div class="flex justify-end gap-2 pt-2">
          <button type="button" class="btn-secondary" @click="showForm = false">Cancel</button>
          <button type="submit" class="btn-primary" :disabled="saving">{{ saving ? 'Saving…' : 'Save' }}</button>
        </div>
      </form>
    </BaseModal>
  </div>
</template>
