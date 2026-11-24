<script setup lang="ts">
import { computed } from 'vue'
import { Bar } from 'vue-chartjs'
import type { CashFlowPoint } from '@/api/types'
import { EXPENSE_COLOR, INCOME_COLOR, moneyTooltip } from '@/components/charts'
import { formatMoney, monthLabel } from '@/utils/format'

const props = defineProps<{ points: CashFlowPoint[]; currency: string }>()

const data = computed(() => ({
  labels: props.points.map((p) => monthLabel(p.year, p.month)),
  datasets: [
    { label: 'Income', data: props.points.map((p) => p.income), backgroundColor: INCOME_COLOR, borderRadius: 4 },
    { label: 'Expenses', data: props.points.map((p) => p.expenses), backgroundColor: EXPENSE_COLOR, borderRadius: 4 },
  ],
}))

const options = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { position: 'bottom' as const, labels: { usePointStyle: true, boxWidth: 8 } },
    tooltip: moneyTooltip(props.currency),
  },
  scales: {
    x: { grid: { display: false } },
    y: {
      beginAtZero: true,
      grid: { color: '#f1f5f9' },
      ticks: { callback: (value: string | number) => formatMoney(Number(value), props.currency) },
    },
  },
}))
</script>

<template>
  <div class="h-72">
    <Bar :data="data" :options="options" aria-label="Monthly income and expenses chart" />
  </div>
</template>
