import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BudgetProgress from '@/components/BudgetProgress.vue'
import type { Budget } from '@/api/types'

const budget = (spent: number, limit = 200): Budget => ({
  id: '1',
  categoryId: 'c',
  categoryName: 'Groceries',
  categoryColor: '#22C55E',
  year: 2026,
  month: 11,
  currency: 'CAD',
  limit,
  spent,
  remaining: limit - spent,
  percentUsed: Math.round((spent / limit) * 1000) / 10,
})

describe('BudgetProgress', () => {
  it('shows remaining amount while under budget', () => {
    const wrapper = mount(BudgetProgress, { props: { budget: budget(50) } })

    expect(wrapper.get('[data-testid="remaining"]').text()).toContain('$150.00 left')
    expect(wrapper.get('[data-testid="bar"]').classes()).toContain('bg-emerald-500')
  })

  it('warns when the budget is nearly used', () => {
    const wrapper = mount(BudgetProgress, { props: { budget: budget(180) } })

    expect(wrapper.get('[data-testid="bar"]').classes()).toContain('bg-amber-500')
  })

  it('caps the bar and shows the overspend', () => {
    const wrapper = mount(BudgetProgress, { props: { budget: budget(250) } })

    expect(wrapper.get('[data-testid="remaining"]').text()).toContain('Over by $50.00')
    expect(wrapper.get('[data-testid="bar"]').attributes('style')).toContain('width: 100%')
    expect(wrapper.get('[data-testid="bar"]').classes()).toContain('bg-rose-500')
  })
})
