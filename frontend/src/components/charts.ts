import {
  ArcElement,
  BarElement,
  CategoryScale,
  Chart as ChartJS,
  Legend,
  LinearScale,
  Tooltip,
  type TooltipItem,
} from 'chart.js'
import { formatMoney } from '@/utils/format'

ChartJS.register(ArcElement, BarElement, CategoryScale, LinearScale, Tooltip, Legend)
ChartJS.defaults.font.family = 'Inter, ui-sans-serif, system-ui, sans-serif'
ChartJS.defaults.color = '#64748b'

export const INCOME_COLOR = '#10b981'
export const EXPENSE_COLOR = '#f43f5e'

export function moneyTooltip(currency: string) {
  return {
    callbacks: {
      label: (item: TooltipItem<'bar' | 'doughnut'>) =>
        `${item.dataset.label ?? item.label}: ${formatMoney(Number(item.raw), currency)}`,
    },
  }
}
