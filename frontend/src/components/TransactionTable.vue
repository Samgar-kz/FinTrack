<script setup lang="ts">
import AppIcon from '@/components/AppIcon.vue'
import type { Transaction } from '@/api/types'
import { formatDate, formatMoney, signedAmount } from '@/utils/format'

defineProps<{ transactions: Transaction[]; compact?: boolean }>()
const emit = defineEmits<{ edit: [transaction: Transaction]; remove: [transaction: Transaction] }>()
</script>

<template>
  <div class="overflow-x-auto">
    <table class="min-w-full divide-y divide-slate-200">
      <thead class="bg-slate-50">
        <tr>
          <th class="table-head">Date</th>
          <th class="table-head">Description</th>
          <th class="table-head">Category</th>
          <th v-if="!compact" class="table-head">Account</th>
          <th class="table-head text-right">Amount</th>
          <th v-if="!compact" class="table-head"><span class="sr-only">Actions</span></th>
        </tr>
      </thead>
      <tbody class="divide-y divide-slate-100 bg-white">
        <tr v-for="t in transactions" :key="t.id" class="hover:bg-slate-50">
          <td class="table-cell text-slate-500">{{ formatDate(t.date) }}</td>
          <td class="table-cell max-w-xs truncate font-medium text-slate-900" :title="t.notes ?? undefined">
            {{ t.description }}
          </td>
          <td class="table-cell">
            <span v-if="t.transferId" class="inline-flex items-center gap-1.5 rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-600">
              <AppIcon name="transfer" :size="12" /> Transfer
            </span>
            <span v-else-if="t.categoryName" class="inline-flex items-center gap-1.5 text-slate-700">
              <span class="size-2 rounded-full" :style="{ backgroundColor: t.categoryColor ?? '#94a3b8' }" />
              {{ t.categoryName }}
            </span>
            <span v-else class="text-slate-400">Uncategorised</span>
          </td>
          <td v-if="!compact" class="table-cell text-slate-600">{{ t.accountName }}</td>
          <td
            class="table-cell text-right font-semibold tabular-nums"
            :class="signedAmount(t.type, t.amount) > 0 ? 'text-emerald-600' : 'text-slate-900'"
          >
            {{ formatMoney(signedAmount(t.type, t.amount), t.currency, { signed: true }) }}
          </td>
          <td v-if="!compact" class="table-cell text-right">
            <button v-if="!t.transferId" class="btn-ghost" :aria-label="`Edit ${t.description}`" @click="emit('edit', t)">
              <AppIcon name="pencil" :size="16" />
            </button>
            <button class="btn-ghost hover:text-rose-600" :aria-label="`Delete ${t.description}`" @click="emit('remove', t)">
              <AppIcon name="trash" :size="16" />
            </button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
