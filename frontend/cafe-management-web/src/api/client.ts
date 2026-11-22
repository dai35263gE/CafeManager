import axios from 'axios'
import type { Envelope, Page } from './types'

export class ApiError extends Error {
  readonly status: number
  readonly errors?: Record<string, string[]> | null
  constructor(message: string, status: number, errors?: Record<string, string[]> | null) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
  }
}

export type Params = Record<string, string | number | boolean | null | undefined>
let token: string | null = null
let onUnauthorized: (() => void) | undefined
export function setAccessToken(value: string | null) { token = value }
export function registerUnauthorized(handler?: () => void) { onUnauthorized = handler }
export const http = axios.create({ baseURL: import.meta.env.VITE_API_BASE_URL || '/api', timeout: 20000 })
http.interceptors.request.use(config => {
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})
http.interceptors.response.use(response => response, (error: unknown) => {
  if (axios.isAxiosError<Envelope<unknown>>(error)) {
    if (error.response?.status === 401 && token && error.config?.headers.Authorization === `Bearer ${token}`) onUnauthorized?.()
    return Promise.reject(new ApiError(
      error.response?.data?.message || 'Không kết nối được API. Kiểm tra backend và mạng.',
      error.response?.status || 0, error.response?.data?.errors,
    ))
  }
  return Promise.reject(error)
})

export function unwrap<T>(response: Envelope<T>, status: number): T {
  if (!response.success || response.data === null || response.data === undefined)
    throw new ApiError(response.message || 'API trả dữ liệu không hợp lệ.', status, response.errors)
  return response.data
}
export async function get<T>(path: string, params?: Params, signal?: AbortSignal): Promise<T> {
  const response = await http.get<Envelope<T>>(path, { params, signal })
  return unwrap(response.data, response.status)
}
export async function send<T>(method: 'post' | 'put' | 'patch', path: string, data?: unknown): Promise<T> {
  const response = await http.request<Envelope<T>>({ method, url: path, data })
  return unwrap(response.data, response.status)
}
export async function remove(path: string, params?: Params, data?: unknown): Promise<void> {
  await http.delete(path, { params, data })
}
export async function all<T>(path: string, params?: Params, signal?: AbortSignal): Promise<T[]> {
  const rows: T[] = []
  for (let page = 1; ; page++) {
    const result = await get<Page<T>>(path, { ...params, page, pageSize: 100 }, signal)
    rows.push(...result.items)
    if (rows.length >= result.totalCount) return rows
    if (!result.items.length) throw new ApiError('Danh sách thay đổi trong lúc tải. Vui lòng tải lại.', 409)
  }
}
export function errorText(error: unknown): string {
  if (error instanceof ApiError) {
    const details = Object.entries(error.errors || {})
      .flatMap(([field, messages]) => [...new Set(messages)].map(detail =>
        detail === error.message ? field : `${field}: ${detail}`))
      .join('; ')
    return `${error.message}${details ? ` (${details})` : ''}`
  }
  return error instanceof Error ? error.message : 'Có lỗi xảy ra. Vui lòng thử lại.'
}
