<script setup lang="ts">
import { reactive, ref } from 'vue'
import { authApi } from '@/api'
import { ApiError } from '@/api/http'
import { CURRENCIES } from '@/api/types'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toast'

const auth = useAuthStore()
const toast = useToastStore()

const form = reactive({ fullName: auth.user?.fullName ?? '', baseCurrency: auth.user?.baseCurrency ?? 'CAD' })
const error = ref<ApiError | null>(null)
const saving = ref(false)

async function save() {
  saving.value = true
  error.value = null
  try {
    auth.setUser(await authApi.updateProfile(form))
    toast.success('Profile updated')
  } catch (err) {
    if (err instanceof ApiError) error.value = err
    toast.error(err)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <section class="card max-w-xl p-6">
    <h2 class="font-semibold text-slate-900">Profile</h2>
    <p class="mt-1 text-sm text-slate-500">Signed in as {{ auth.user?.email }}</p>
    <form class="mt-6 space-y-4" novalidate @submit.prevent="save">
      <div>
        <label class="label" for="name">Full name</label>
        <input id="name" v-model="form.fullName" class="input" maxlength="100" />
        <p v-if="error?.field('fullName')" class="field-error">{{ error.field('fullName') }}</p>
      </div>
      <div>
        <label class="label" for="currency">Base currency</label>
        <select id="currency" v-model="form.baseCurrency" class="input">
          <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
        </select>
        <p class="mt-1 text-xs text-slate-500">Used for the dashboard, reports and new budgets.</p>
      </div>
      <button class="btn-primary" type="submit" :disabled="saving">{{ saving ? 'Saving…' : 'Save changes' }}</button>
    </form>
  </section>
</template>
