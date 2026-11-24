import { del, get, post, put, request, toQuery } from './http'
import type {
  Account,
  AccountType,
  AdminUser,
  AuthResponse,
  Budget,
  CashFlowPoint,
  Category,
  CategoryBreakdown,
  CategoryKind,
  CreateTransferRequest,
  PagedResult,
  PlatformStats,
  SaveTransactionRequest,
  Summary,
  Transaction,
  TransactionQuery,
  User,
} from './types'

export const authApi = {
  login: (email: string, password: string) => post<AuthResponse>('/auth/login', { email, password }),
  register: (body: { email: string; password: string; fullName: string; baseCurrency: string }) =>
    post<AuthResponse>('/auth/register', body),
  refresh: () => post<AuthResponse>('/auth/refresh'),
  logout: () => post<void>('/auth/logout'),
  me: () => get<User>('/auth/me'),
  updateProfile: (body: { fullName: string; baseCurrency: string }) => put<User>('/auth/me', body),
}

export const accountsApi = {
  list: (includeArchived = false) => get<Account[]>(`/accounts${toQuery({ includeArchived })}`),
  create: (body: { name: string; type: AccountType; currency: string; openingBalance: number }) =>
    post<Account>('/accounts', body),
  update: (id: string, body: { name: string; type: AccountType }) => put<Account>(`/accounts/${id}`, body),
  archive: (id: string) => post<Account>(`/accounts/${id}/archive`),
  restore: (id: string) => post<Account>(`/accounts/${id}/restore`),
  remove: (id: string) => del(`/accounts/${id}`),
}

export const categoriesApi = {
  list: (kind?: CategoryKind) => get<Category[]>(`/categories${toQuery({ kind })}`),
  create: (body: { name: string; kind: CategoryKind; color: string }) => post<Category>('/categories', body),
  update: (id: string, body: { name: string; color: string }) => put<Category>(`/categories/${id}`, body),
  remove: (id: string) => del(`/categories/${id}`),
}

export const transactionsApi = {
  search: (query: TransactionQuery) => get<PagedResult<Transaction>>(`/transactions${toQuery(query)}`),
  create: (body: SaveTransactionRequest) => post<Transaction>('/transactions', body),
  update: (id: string, body: SaveTransactionRequest) => put<Transaction>(`/transactions/${id}`, body),
  remove: (id: string) => del(`/transactions/${id}`),
  transfer: (body: CreateTransferRequest) => post<unknown>('/transfers', body),
  /** Downloads the filtered transactions as a CSV file. */
  async exportCsv(query: TransactionQuery) {
    const response = await request(`/transactions/export${toQuery({ ...query, page: undefined, pageSize: undefined })}`)
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `fintrack-transactions-${new Date().toISOString().slice(0, 10)}.csv`
    link.click()
    URL.revokeObjectURL(url)
  },
}

export const budgetsApi = {
  list: (year: number, month: number) => get<Budget[]>(`/budgets${toQuery({ year, month })}`),
  upsert: (body: { categoryId: string; year: number; month: number; limit: number }) => put<Budget>('/budgets', body),
  copyPrevious: (year: number, month: number) =>
    post<{ copied: number }>(`/budgets/copy-previous${toQuery({ year, month })}`),
  remove: (id: string) => del(`/budgets/${id}`),
}

export interface ReportRange {
  from?: string
  to?: string
  currency?: string
}

export const reportsApi = {
  summary: (range: ReportRange) => get<Summary>(`/reports/summary${toQuery(range)}`),
  categories: (range: ReportRange, kind: CategoryKind) =>
    get<CategoryBreakdown[]>(`/reports/categories${toQuery({ ...range, kind })}`),
  cashFlow: (months: number, currency?: string) =>
    get<CashFlowPoint[]>(`/reports/cash-flow${toQuery({ months, currency })}`),
}

export const adminApi = {
  stats: () => get<PlatformStats>('/admin/stats'),
  users: (search?: string, page = 1) => get<PagedResult<AdminUser>>(`/admin/users${toQuery({ search, page })}`),
}
