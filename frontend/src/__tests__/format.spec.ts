import { describe, expect, it } from 'vitest'
import { formatDate, formatMoney, isoDate, monthRange, signedAmount } from '@/utils/format'

describe('formatMoney', () => {
  it('formats positive and negative amounts with the currency symbol', () => {
    expect(formatMoney(1234.5, 'CAD')).toBe('$1,234.50')
    expect(formatMoney(-20, 'CAD')).toBe('−$20.00')
  })

  it('adds a plus sign for positive amounts when signed', () => {
    expect(formatMoney(15, 'USD', { signed: true })).toBe('+$15.00')
    expect(formatMoney(0, 'USD', { signed: true })).toBe('$0.00')
  })
})

describe('signedAmount', () => {
  it('treats income and incoming transfers as inflows', () => {
    expect(signedAmount('Income', 10)).toBe(10)
    expect(signedAmount('TransferIn', 10)).toBe(10)
    expect(signedAmount('Expense', 10)).toBe(-10)
    expect(signedAmount('TransferOut', 10)).toBe(-10)
  })
})

describe('dates', () => {
  it('formats ISO dates without timezone shifts', () => {
    expect(formatDate('2026-01-01')).toContain('Jan')
    expect(formatDate('2026-01-01')).toContain('1')
  })

  it('produces local ISO dates and month ranges', () => {
    expect(isoDate(new Date(2026, 1, 5))).toBe('2026-02-05')
    expect(monthRange(2028, 2)).toEqual({ from: '2028-02-01', to: '2028-02-29' })
  })
})
