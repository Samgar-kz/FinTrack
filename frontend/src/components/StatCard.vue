<script setup lang="ts">
defineProps<{
  label: string
  value: string
  hint?: string
  tone?: 'neutral' | 'positive' | 'negative'
}>()
</script>

<template>
  <div class="card p-5">
    <p class="text-sm font-medium text-slate-500">{{ label }}</p>
    <p
      class="mt-2 text-2xl font-semibold tracking-tight"
      :class="{
        'text-slate-900': !tone || tone === 'neutral',
        'text-emerald-600': tone === 'positive',
        'text-rose-600': tone === 'negative',
      }"
    >
      {{ value }}
    </p>
    <p v-if="hint" class="mt-1 text-xs text-slate-500">{{ hint }}</p>
  </div>
</template>
