import { afterEach, describe, expect, it, vi } from 'vitest'
import { App as AntApp } from 'antd'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import type { AxiosAdapter } from 'axios'
import { ApiError, errorText, http } from '../api/client'
import CatalogPage from './CatalogPage'

const auth = vi.hoisted(() => ({ role: 'Manager' }))
vi.mock('../auth/AuthContext', () => ({ useAuth: () => ({ user: { role: auth.role } }) }))
const original = http.defaults.adapter
afterEach(() => { http.defaults.adapter = original; auth.role = 'Manager' })

describe('catalog validation errors', () => {
  it('identifies the field that needs correction', () => {
    expect(errorText(new ApiError('Validation failed.', 400, { 'Sizes.Price': ['Price is invalid.'] })))
      .toBe('Validation failed. (Sizes.Price: Price is invalid.)')
  })
})

describe('menu API contract', () => {
  it.each(['Manager', 'Admin'])('renders the full menu and management controls for %s', async role => {
    auth.role = role
    const dish = { id: 'MON-001', categoryId: 'D01', categoryName: 'Cà phê', name: 'Cà phê sữa',
      description: null, imageUrl: null, basePrice: 25000, status: 'Available', isActive: true,
      sizes: [{ sizeId: 'KC01', name: 'Nhỏ', price: 25000, isActive: true }],
      toppings: [{ id: 'TP01', name: 'Kem', price: 5000, isActive: true }], rowVersion: 'AQIDBAUGBwg=' }
    let orderableOnly: unknown
    const adapter: AxiosAdapter = async config => {
      if (config.url === '/mon') orderableOnly = config.params?.orderableOnly
      const items = config.url === '/mon' ? [dish] : [{ id: 'D01', name: 'Cà phê', description: null, isActive: true }]
      const data = { items, totalCount: items.length, page: 1, pageSize: 100 }
      return { config, status: 200, statusText: 'OK', headers: {}, data: { success: true, message: 'OK', data } }
    }
    http.defaults.adapter = adapter
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<AntApp><QueryClientProvider client={client}><CatalogPage /></QueryClientProvider></AntApp>)
    await screen.findByText('MON-001')
    expect(screen.getByText('Cà phê sữa')).toBeTruthy()
    expect(screen.getByText('Cà phê')).toBeTruthy()
    expect(screen.getByText(/Nhỏ:/)).toBeTruthy()
    expect(screen.getByText(/Kem/)).toBeTruthy()
    expect(orderableOnly).toBe(false)
    expect(screen.getByRole('button', { name: 'Thêm món' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Sửa' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Trạng thái' })).toBeTruthy()
  })
})
