<script setup lang="ts">
import { computed } from 'vue'
import type { Budget } from '@/api/types'
import { formatMoney } from '@/utils/format'

const props = defineProps<{ budget: Budget }>()

const status = computed(() => {
  if (props.budget.percentUsed > 100) return 'over'
  if (props.budget.percentUsed >= 85) return 'warning'
  return 'ok'
})

const barWidth = computed(() => `${Math.min(props.budget.percentUsed, 100)}%`)
</script>

<template>
  <div>
    <div class="mb-1.5 flex items-center justify-between gap-3 text-sm">
      <span class="flex items-center gap-2 font-medium text-slate-800">
        <span class="size-2.5 rounded-full" :style="{ backgroundColor: budget.categoryColor }" />
        {{ budget.categoryName }}
      </span>
      <span class="text-slate-500">
        <span class="font-medium text-slate-900">{{ formatMoney(budget.spent, budget.currency) }}</span>
        of {{ formatMoney(budget.limit, budget.currency) }}
      </span>
    </div>
    <div
      class="h-2 overflow-hidden rounded-full bg-slate-100"
      role="progressbar"
      :aria-valuenow="budget.percentUsed"
      aria-valuemin="0"
      aria-valuemax="100"
      :aria-label="`${budget.categoryName} budget used`"
    >
      <div
        class="h-full rounded-full transition-all"
        :class="{ 'bg-emerald-500': status === 'ok', 'bg-amber-500': status === 'warning', 'bg-rose-500': status === 'over' }"
        :style="{ width: barWidth }"
        data-testid="bar"
      />
    </div>
    <p class="mt-1 text-xs" :class="status === 'over' ? 'text-rose-600' : 'text-slate-500'" data-testid="remaining">
      <template v-if="status === 'over'">Over by {{ formatMoney(-budget.remaining, budget.currency) }}</template>
      <template v-else>{{ formatMoney(budget.remaining, budget.currency) }} left · {{ budget.percentUsed }}% used</template>
    </p>
  </div>
</template>
