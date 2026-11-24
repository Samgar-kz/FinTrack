// Types mirroring the FinTrack API contracts (enums are serialised as strings).

export type UserRole = 'User' | 'Admin'
export type AccountType = 'Checking' | 'Savings' | 'CreditCard' | 'Cash' | 'Investment'
export type CategoryKind = 'Expense' | 'Income'
export type TransactionType = 'Income' | 'Expense' | 'TransferIn' | 'TransferOut'
export type TransactionSort = 'DateDesc' | 'DateAsc' | 'AmountDesc' | 'AmountAsc'

export const CURRENCIES = ['CAD', 'USD', 'EUR', 'GBP', 'KZT'] as const
export type Currency = (typeof CURRENCIES)[number]

export const ACCOUNT_TYPES: { value: AccountType; label: string }[] = [
  { value: 'Checking', label: 'Chequing' },
  { value: 'Savings', label: 'Savings' },
  { value: 'CreditCard', label: 'Credit card' },
  { value: 'Cash', label: 'Cash' },
  { value: 'Investment', label: 'Investment' },
]

export interface User {
  id: string
  email: string
  fullName: string
  role: UserRole
  baseCurrency: string
}

export interface AuthResponse {
  accessToken: string
  expiresAt: string
  user: User
}

export interface Account {
  id: string
  name: string
  type: AccountType
  currency: string
  openingBalance: number
  balance: number
  isArchived: boolean
  createdAt: string
}

export interface Category {
  id: string
  name: string
  kind: CategoryKind
  color: string
}

export interface Transaction {
  id: string
  accountId: string
  accountName: string
  currency: string
  categoryId: string | null
  categoryName: string | null
  categoryColor: string | null
  type: TransactionType
  amount: number
  date: string
  description: string
  notes: string | null
  transferId: string | null
  createdAt: string
}

export interface TransactionQuery {
  from?: string
  to?: string
  accountId?: string
  categoryId?: string
  type?: TransactionType
  search?: string
  sort?: TransactionSort
  page?: number
  pageSize?: number
}

export interface SaveTransactionRequest {
  accountId: string
  type: 'Income' | 'Expense'
  amount: number
  date: string
  description: string
  categoryId: string | null
  notes: string | null
}

export interface CreateTransferRequest {
  fromAccountId: string
  toAccountId: string
  amount: number
  date: string
  description: string
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface Budget {
  id: string
  categoryId: string
  categoryName: string
  categoryColor: string
  year: number
  month: number
  currency: string
  limit: number
  spent: number
  remaining: number
  percentUsed: number
}

export interface Summary {
  currency: string
  from: string
  to: string
  income: number
  expenses: number
  net: number
  savingsRate: number
  totalBalance: number
  transactionCount: number
}

export interface CategoryBreakdown {
  categoryId: string | null
  name: string
  color: string
  amount: number
  percentage: number
  transactionCount: number
}

export interface CashFlowPoint {
  year: number
  month: number
  income: number
  expenses: number
  net: number
}

export interface AdminUser {
  id: string
  email: string
  fullName: string
  role: UserRole
  createdAt: string
  lastLoginAt: string | null
  accountCount: number
  transactionCount: number
}

export interface PlatformStats {
  users: number
  activeUsersLast30Days: number
  accounts: number
  transactions: number
}
