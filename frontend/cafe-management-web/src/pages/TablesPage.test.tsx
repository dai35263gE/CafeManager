import { afterEach, describe, expect, it, vi } from 'vitest'
import { App as AntApp } from 'antd'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { AxiosAdapter } from 'axios'
import { http } from '../api/client'
import type { Table as CafeTable } from '../api/types'
import TablesPage from './TablesPage'

vi.mock('../auth/AuthContext', () => ({ useAuth: () => ({ user: { role: 'Manager' } }) }))
const original = http.defaults.adapter
afterEach(() => { http.defaults.adapter = original })

function mount(initialTables?: CafeTable[]) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity } } })
  if (initialTables) client.setQueryData(['/ban', 'options', undefined], initialTables)
  return render(<AntApp><QueryClientProvider client={client}><TablesPage /></QueryClientProvider></AntApp>)
}

describe('table floor plan', () => {
  it('seats a guest without creating an order', async () => {
    const calls: { url?: string; method?: string; data?: unknown }[] = []
    let table: CafeTable = { id: 'B01', code: 'B01', area: 'Tầng 1', status: 'Available', note: null, rowVersion: 'AQIDBAUGBwg=', activeOrder: null }
    const adapter: AxiosAdapter = async config => {
      calls.push({ url: config.url, method: config.method, data: config.data })
      if (config.method === 'post') table = { ...table, status: 'InUse', rowVersion: 'AgMEBQYHCAk=' }
      const data = config.url?.split('?')[0].endsWith('/ban')
        ? { items: [table], totalCount: 1, page: 1, pageSize: 100 }
        : table
      return { config, status: 200, statusText: 'OK', headers: {}, data: { success: true, message: 'OK', data } }
    }
    http.defaults.adapter = adapter
    mount([table])
    const actor = userEvent.setup()
    await actor.click(await screen.findByRole('button', { name: 'Xem chi tiết' }))
    await actor.click(await screen.findByRole('button', { name: 'Xếp bàn' }))
    await actor.click(await screen.findByRole('button', { name: 'Xác nhận' }))
    await waitFor(() => expect(calls.some(call => call.method === 'post' && call.url === '/ban/B01/seat')).toBe(true))
    const seating = calls.find(call => call.url === '/ban/B01/seat')
    expect(JSON.parse(String(seating?.data))).toEqual({ rowVersion: 'AQIDBAUGBwg=' })
    expect(table).toMatchObject({ status: 'InUse', activeOrder: null })
  })

  it('creates a table through the manager CRUD form', async () => {
    const calls: { url?: string; method?: string; data?: unknown }[] = []
    const table = { id: 'B02', code: 'B02', area: 'Sân vườn', status: 'Available', note: null, rowVersion: 'AQIDBAUGBwg=', activeOrder: null }
    const adapter: AxiosAdapter = async config => {
      calls.push({ url: config.url, method: config.method, data: config.data })
      const data = config.method === 'get'
        ? { items: [], totalCount: 0, page: 1, pageSize: 100 }
        : table
      return { config, status: config.method === 'post' ? 201 : 200, statusText: 'OK', headers: {}, data: { success: true, message: 'OK', data } }
    }
    http.defaults.adapter = adapter
    mount([])
    const actor = userEvent.setup()
    await actor.click(await screen.findByRole('button', { name: 'Thêm bàn' }))
    await actor.type(screen.getByLabelText('Mã bàn'), 'B02')
    await actor.type(screen.getByLabelText('Khu vực'), 'Sân vườn')
    await actor.click(screen.getByRole('button', { name: 'OK' }))
    await waitFor(() => expect(calls.some(call => call.method === 'post' && call.url === '/ban')).toBe(true))
    const create = calls.find(call => call.method === 'post' && call.url === '/ban')
    expect(JSON.parse(String(create?.data))).toMatchObject({ code: 'B02', area: 'Sân vườn' })
  })

  it('merges multiple selected tables into the main table and selected order', async () => {
    const calls: { url?: string; method?: string; data?: unknown }[] = []
    const tables: CafeTable[] = [
      { id: 'B01', code: 'B01', area: 'Tầng 1', status: 'InUse', note: null, rowVersion: 'AQIDBAUGBwg=', activeOrder: { id: 'O01', code: 'DH01', status: 'Open', rowVersion: 'AgMEBQYHCAk=' } },
      { id: 'B02', code: 'B02', area: 'Tầng 1', status: 'InUse', note: null, rowVersion: 'AwQFBgcICQo=', activeOrder: { id: 'O02', code: 'DH02', status: 'Open', rowVersion: 'BAUGBwgJCgs=' } },
      { id: 'B03', code: 'B03', area: 'Tầng 1', status: 'Available', note: null, rowVersion: 'BQYHCAkKCww=', activeOrder: null },
    ]
    const mainOrder = tables[0].activeOrder!
    const sourceOrder = tables[1].activeOrder!
    const adapter: AxiosAdapter = async config => {
      calls.push({ url: config.url, method: config.method, data: config.data })
      const data = config.url?.split('?')[0].endsWith('/ban')
        ? { items: tables, totalCount: tables.length, page: 1, pageSize: 100 }
        : tables.find(table => config.url?.endsWith(`/ban/${table.id}`))
      return { config, status: 200, statusText: 'OK', headers: {}, data: { success: true, message: 'OK', data } }
    }
    http.defaults.adapter = adapter
    mount(tables)
    const actor = userEvent.setup()
    await actor.click(await screen.findByRole('button', { name: 'B01' }))
    await actor.click(await screen.findByRole('button', { name: 'Ghép bàn' }))
    const tableSelect = screen.getByLabelText('Các bàn cần ghép')
    fireEvent.mouseDown(tableSelect)
    fireEvent.click(await screen.findByText('B02 · Tầng 1 · Đang sử dụng'))
    fireEvent.mouseDown(tableSelect)
    fireEvent.click(await screen.findByText('B03 · Tầng 1 · Sẵn sàng'))
    fireEvent.mouseDown(screen.getByLabelText('Đơn đích'))
    fireEvent.click(await screen.findByText('DH02 · Nháp'))
    await actor.click(screen.getByRole('button', { name: 'OK' }))
    await waitFor(() => expect(calls.some(call => call.method === 'post' && call.url === '/ban/merge')).toBe(true))
    const request = JSON.parse(String(calls.find(call => call.method === 'post' && call.url === '/ban/merge')?.data))
    expect(request).toMatchObject({
      targetTableId: 'B01',
      targetTableRowVersion: tables[0].rowVersion,
      targetActiveOrderRowVersion: mainOrder.rowVersion,
      targetOrderId: 'O02',
      sources: [
        { tableId: 'B02', tableRowVersion: tables[1].rowVersion, activeOrderRowVersion: sourceOrder.rowVersion },
        { tableId: 'B03', tableRowVersion: tables[2].rowVersion, activeOrderRowVersion: null },
      ],
    })
  })
})
