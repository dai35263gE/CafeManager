import { afterEach, describe, expect, it, vi } from 'vitest'
import { App as AntApp } from 'antd'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import type { AxiosAdapter } from 'axios'
import { http } from '../api/client'
import KitchenPage from './KitchenPage'

vi.mock('../auth/AuthContext', () => ({ useAuth: () => ({ user: { id: 'barista-1', role: 'Barista' } }) }))
const original = http.defaults.adapter
afterEach(() => { http.defaults.adapter = original })

describe('kitchen queue defaults', () => {
  it('starts on waiting items and explains when the queue is empty', async () => {
    let requestedStatus: unknown
    const adapter: AxiosAdapter = async config => {
      if (config.url === '/don-hang/kitchen-queue') requestedStatus = config.params?.status
      const data = { items: [], totalCount: 0, page: 1, pageSize: 20 }
      return { config, status: 200, statusText: 'OK', headers: {}, data: { success: true, message: 'OK', data } }
    }
    http.defaults.adapter = adapter
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<AntApp><QueryClientProvider client={client}><KitchenPage /></QueryClientProvider></AntApp>)
    expect(await screen.findByText('Hiện chưa có món chờ pha chế')).toBeTruthy()
    expect(requestedStatus).toBe('Waiting')
  })
})
