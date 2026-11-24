import type { TransactionType } from '@/api/types'

const moneyFormatters = new Map<string, Intl.NumberFormat>()

export function formatMoney(amount: number, currency: string, options: { signed?: boolean } = {}): string {
  let formatter = moneyFormatters.get(currency)
  if (!formatter) {
    formatter = new Intl.NumberFormat('en-CA', { style: 'currency', currency, currencyDisplay: 'narrowSymbol' })
    moneyFormatters.set(currency, formatter)
  }
  const formatted = formatter.format(Math.abs(amount))
  if (amount < 0) return `−${formatted}`
  return options.signed && amount > 0 ? `+${formatted}` : formatted
}

export function signedAmount(type: TransactionType, amount: number): number {
  return type === 'Income' || type === 'TransferIn' ? amount : -amount
}

/** Formats an ISO date (yyyy-MM-dd) without timezone shifts. */
export function formatDate(isoDate: string): string {
  const [year, month, day] = isoDate.split('-').map(Number)
  return new Date(year!, month! - 1, day).toLocaleDateString('en-CA', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })
}

export function monthLabel(year: number, month: number, style: 'short' | 'long' = 'short'): string {
  return new Date(year, month - 1, 1).toLocaleDateString('en-CA', {
    month: style,
    year: style === 'long' ? 'numeric' : '2-digit',
  })
}

/** Local date as yyyy-MM-dd. */
export function isoDate(date: Date = new Date()): string {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

export function monthRange(year: number, month: number): { from: string; to: string } {
  return { from: isoDate(new Date(year, month - 1, 1)), to: isoDate(new Date(year, month, 0)) }
}
