<script setup lang="ts">
import { computed } from 'vue'
import { Doughnut } from 'vue-chartjs'
import type { CategoryBreakdown } from '@/api/types'
import { moneyTooltip } from '@/components/charts'
import { formatMoney } from '@/utils/format'

const props = defineProps<{ items: CategoryBreakdown[]; currency: string }>()

const total = computed(() => props.items.reduce((sum, item) => sum + item.amount, 0))

const data = computed(() => ({
  labels: props.items.map((i) => i.name),
  datasets: [
    {
      data: props.items.map((i) => i.amount),
      backgroundColor: props.items.map((i) => i.color),
      borderWidth: 2,
      borderColor: '#ffffff',
    },
  ],
}))

const options = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  cutout: '70%',
  plugins: { legend: { display: false }, tooltip: moneyTooltip(props.currency) },
}))
</script>

<template>
  <div class="grid items-center gap-6 sm:grid-cols-[12rem_1fr]">
    <div class="relative mx-auto size-48">
      <Doughnut :data="data" :options="options" aria-label="Spending by category chart" />
      <div class="pointer-events-none absolute inset-0 grid place-items-center text-center">
        <div>
          <p class="text-xs text-slate-500">Total</p>
          <p class="text-lg font-semibold text-slate-900">{{ formatMoney(total, currency) }}</p>
        </div>
      </div>
    </div>
    <ul class="space-y-2">
      <li v-for="item in items.slice(0, 7)" :key="item.categoryId ?? 'none'" class="flex items-center gap-3 text-sm">
        <span class="size-2.5 shrink-0 rounded-full" :style="{ backgroundColor: item.color }" />
        <span class="flex-1 truncate text-slate-700">{{ item.name }}</span>
        <span class="text-xs text-slate-500">{{ item.percentage }}%</span>
        <span class="text-right font-medium text-slate-900 tabular-nums">{{ formatMoney(item.amount, currency) }}</span>
      </li>
    </ul>
  </div>
</template>
