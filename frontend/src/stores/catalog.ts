import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { accountsApi, categoriesApi } from '@/api'
import type { Account, Category } from '@/api/types'

/** Accounts and categories, shared by forms and filters across views. */
export const useCatalogStore = defineStore('catalog', () => {
  const accounts = ref<Account[]>([])
  const categories = ref<Category[]>([])
  const loaded = ref(false)

  const activeAccounts = computed(() => accounts.value.filter((a) => !a.isArchived))
  const expenseCategories = computed(() => categories.value.filter((c) => c.kind === 'Expense'))
  const incomeCategories = computed(() => categories.value.filter((c) => c.kind === 'Income'))

  async function load(force = false) {
    if (loaded.value && !force) return
    const [accountList, categoryList] = await Promise.all([accountsApi.list(true), categoriesApi.list()])
    accounts.value = accountList
    categories.value = categoryList
    loaded.value = true
  }

  function reset() {
    accounts.value = []
    categories.value = []
    loaded.value = false
  }

  return { accounts, categories, loaded, activeAccounts, expenseCategories, incomeCategories, load, reset }
})
