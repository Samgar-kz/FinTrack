<script setup lang="ts">
defineProps<{ title: string; subtitle: string }>()
</script>

<template>
  <div class="grid min-h-screen lg:grid-cols-2">
    <section class="hidden flex-col justify-between bg-slate-900 p-12 text-white lg:flex">
      <div class="flex items-center gap-2">
        <span class="grid size-9 place-items-center rounded-lg bg-brand-500 text-lg font-bold">F</span>
        <span class="text-xl font-semibold">FinTrack</span>
      </div>
      <div class="max-w-md">
        <h2 class="text-3xl font-semibold leading-tight">Every account, budget and transaction in one place.</h2>
        <p class="mt-4 text-slate-400">
          Track spending across accounts and currencies, set monthly budgets and see where your money goes.
        </p>
        <ul class="mt-8 space-y-3 text-sm text-slate-300">
          <li>✓ Multi-account and multi-currency balances</li>
          <li>✓ Transfers that keep both accounts in sync</li>
          <li>✓ Monthly budgets with live progress</li>
          <li>✓ Cash-flow and category reports, CSV export</li>
        </ul>
      </div>
      <p class="text-xs text-slate-500">ASP.NET Core · PostgreSQL · Vue 3 · TypeScript</p>
    </section>

    <section class="flex items-center justify-center p-6">
      <div class="w-full max-w-sm">
        <h1 class="text-2xl font-semibold text-slate-900">{{ title }}</h1>
        <p class="mt-1 text-sm text-slate-500">{{ subtitle }}</p>
        <div class="mt-8">
          <slot />
        </div>
      </div>
    </section>
  </div>
</template>
