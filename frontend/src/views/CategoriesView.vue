<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { categoriesApi } from '@/api'
import { ApiError } from '@/api/http'
import type { Category, CategoryKind } from '@/api/types'
import AppIcon from '@/components/AppIcon.vue'
import BaseModal from '@/components/BaseModal.vue'
import { useCatalogStore } from '@/stores/catalog'
import { useToastStore } from '@/stores/toast'

const catalog = useCatalogStore()
const toast = useToastStore()

const editing = ref<Category | null>(null)
const showForm = ref(false)
const error = ref<ApiError | null>(null)
const form = reactive({ name: '', kind: 'Expense' as CategoryKind, color: '#6366F1' })

const columns: { kind: CategoryKind; title: string }[] = [
  { kind: 'Expense', title: 'Expense categories' },
  { kind: 'Income', title: 'Income categories' },
]

function openForm(kind: CategoryKind, category?: Category) {
  editing.value = category ?? null
  Object.assign(form, { name: category?.name ?? '', kind, color: category?.color ?? '#6366F1' })
  error.value = null
  showForm.value = true
}

async function save() {
  error.value = null
  try {
    if (editing.value) {
      await categoriesApi.update(editing.value.id, { name: form.name, color: form.color.toUpperCase() })
    } else {
      await categoriesApi.create({ name: form.name, kind: form.kind, color: form.color.toUpperCase() })
    }
    toast.success('Category saved')
    showForm.value = false
    await catalog.load(true)
  } catch (err) {
    if (err instanceof ApiError) error.value = err
    toast.error(err)
  }
}

async function remove(category: Category) {
  if (!window.confirm(`Delete "${category.name}"? Its transactions will become uncategorised.`)) return
  try {
    await categoriesApi.remove(category.id)
    toast.success('Category deleted')
    await catalog.load(true)
  } catch (err) {
    toast.error(err)
  }
}

onMounted(() => catalog.load(true))
</script>

<template>
  <div class="grid gap-6 lg:grid-cols-2">
    <section v-for="column in columns" :key="column.kind" class="card">
      <div class="flex items-center justify-between border-b border-slate-100 p-5">
        <h2 class="font-semibold text-slate-900">{{ column.title }}</h2>
        <button class="btn-secondary" @click="openForm(column.kind)"><AppIcon name="plus" :size="16" /> Add</button>
      </div>
      <ul class="divide-y divide-slate-100">
        <li
          v-for="category in column.kind === 'Expense' ? catalog.expenseCategories : catalog.incomeCategories"
          :key="category.id"
          class="flex items-center gap-3 px-5 py-3"
        >
          <span class="size-3 rounded-full" :style="{ backgroundColor: category.color }" />
          <span class="flex-1 text-sm font-medium text-slate-800">{{ category.name }}</span>
          <button class="btn-ghost" :aria-label="`Edit ${category.name}`" @click="openForm(category.kind, category)">
            <AppIcon name="pencil" :size="16" />
          </button>
          <button class="btn-ghost hover:text-rose-600" :aria-label="`Delete ${category.name}`" @click="remove(category)">
            <AppIcon name="trash" :size="16" />
          </button>
        </li>
      </ul>
    </section>

    <BaseModal v-if="showForm" :title="editing ? 'Edit category' : `New ${form.kind.toLowerCase()} category`" @close="showForm = false">
      <form class="space-y-4" novalidate @submit.prevent="save">
        <div>
          <label class="label" for="cat-name">Name</label>
          <input id="cat-name" v-model="form.name" class="input" maxlength="50" required />
          <p v-if="error?.field('name')" class="field-error">{{ error.field('name') }}</p>
        </div>
        <div>
          <label class="label" for="cat-color">Colour</label>
          <div class="flex items-center gap-3">
            <input id="cat-color" v-model="form.color" type="color" class="h-10 w-14 cursor-pointer rounded border border-slate-300" />
            <span class="font-mono text-sm text-slate-600">{{ form.color.toUpperCase() }}</span>
          </div>
        </div>
        <div class="flex justify-end gap-2 pt-2">
          <button type="button" class="btn-secondary" @click="showForm = false">Cancel</button>
          <button type="submit" class="btn-primary">Save</button>
        </div>
      </form>
    </BaseModal>
  </div>
</template>
