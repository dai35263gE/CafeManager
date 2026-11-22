import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { App } from 'antd'
import { all, ApiError, errorText, get } from './client'
import type { Params } from './client'
import type { Page } from './types'

export function useResource<T>(path: string, params?: Params, enabled = true, poll?: number) {
  return useQuery({ queryKey: [path, params], queryFn: ({ signal }) => get<T>(path, params, signal), enabled, refetchInterval: poll })
}
export function useOptions<T>(path: string, params?: Params, enabled = true, poll?: number) {
  return useQuery({ queryKey: [path, 'options', params], queryFn: ({ signal }) => all<T>(path, params, signal), enabled, refetchInterval: poll })
}
export function usePage<T>(path: string, filters?: Params, enabled = true, poll?: number) {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [search, setSearchValue] = useState('')
  const query = useResource<Page<T>>(path, { ...filters, page, pageSize, search }, enabled, poll)
  return {
    query, search, setSearch: (value: string) => { setSearchValue(value); setPage(1) },
    resetPage: () => setPage(1),
    pagination: { current: page, pageSize, total: query.data?.totalCount || 0, showSizeChanger: true, pageSizeOptions: [10, 20, 50, 100],
      onChange: (next: number, size: number) => { setPage(next); setPageSize(size) } },
  }
}
export function useAction() {
  const [busy, setBusy] = useState(false)
  const { message } = App.useApp()
  const cache = useQueryClient()
  async function run(task: () => Promise<unknown>, success = 'Thao tác thành công.'): Promise<boolean> {
    setBusy(true)
    try {
      await task()
      await cache.invalidateQueries()
      message.success(success)
      return true
    } catch (error) {
      message.error(errorText(error), 7)
      if (error instanceof ApiError && (error.status === 404 || error.status === 409))
        await cache.invalidateQueries()
      return false
    } finally { setBusy(false) }
  }
  return { run, busy }
}
