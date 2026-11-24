<script setup lang="ts">
import AppIcon from '@/components/AppIcon.vue'

defineProps<{ page: number; totalPages: number; totalCount: number }>()
const emit = defineEmits<{ change: [page: number] }>()
</script>

<template>
  <div class="flex items-center justify-between border-t border-slate-200 px-4 py-3 text-sm text-slate-500">
    <span>{{ totalCount }} {{ totalCount === 1 ? 'result' : 'results' }}</span>
    <div class="flex items-center gap-2">
      <button class="btn-ghost" :disabled="page <= 1" aria-label="Previous page" @click="emit('change', page - 1)">
        <AppIcon name="left" :size="16" />
      </button>
      <span>Page {{ page }} of {{ Math.max(totalPages, 1) }}</span>
      <button class="btn-ghost" :disabled="page >= totalPages" aria-label="Next page" @click="emit('change', page + 1)">
        <AppIcon name="right" :size="16" />
      </button>
    </div>
  </div>
</template>
