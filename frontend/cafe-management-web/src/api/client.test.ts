import { afterEach, describe, expect, it, vi } from 'vitest'
import { AxiosError } from 'axios'
import type { AxiosAdapter } from 'axios'
import { all, get, http, registerUnauthorized, remove, setAccessToken, unwrap } from './client'
const original = http.defaults.adapter
afterEach(() => { http.defaults.adapter = original; setAccessToken(null); registerUnauthorized() })
describe('HTTP client and concurrency contracts', () => {
  it('sends the token and preserves DELETE body and encoded query parameters', async () => {
    setAccessToken('demo-test-token')
    const captured = vi.fn()
    const adapter: AxiosAdapter = async config => {
      captured(config.headers.Authorization, config.params, config.data)
      return { config, data: '', status: 204, statusText: 'No Content', headers: {} }
    }
    http.defaults.adapter = adapter
    await remove('/ban/test', { rowVersion: '+/=' }, { reason: 'test' })
    expect(captured).toHaveBeenCalledWith('Bearer demo-test-token', { rowVersion: '+/=' }, JSON.stringify({ reason: 'test' }))
  })
  it('loads options past the first 100 rows instead of silently truncating', async () => {
    const adapter: AxiosAdapter = async config => ({
      config, status: 200, statusText: 'OK', headers: {},
      data: { success: true, message: 'OK', data: { items: config.params.page === 1 ? Array.from({ length: 100 }, (_, id) => ({ id })) : [{ id: 100 }], totalCount: 101, page: config.params.page, pageSize: 100 } },
    })
    http.defaults.adapter = adapter
    expect(await all<{ id: number }>('/mon')).toHaveLength(101)
  })
  it('rejects a success-shaped envelope without actual data', () => {
    expect(() => unwrap({ success: true, message: 'Missing data', data: null }, 200)).toThrow('Missing data')
  })
  it('invalidates the current session on 401, but not a newer session after a stale response', async () => {
    const logout = vi.fn()
    registerUnauthorized(logout)
    setAccessToken('old')
    let replace = true
    const adapter: AxiosAdapter = async config => {
      if (replace) setAccessToken('new')
      throw new AxiosError('Unauthorized', 'ERR_BAD_REQUEST', config, undefined, {
        config, status: 401, statusText: 'Unauthorized', headers: {}, data: { success: false, message: 'Invalid token', data: null },
      })
    }
    http.defaults.adapter = adapter
    await expect(get('/auth/me')).rejects.toThrow('Invalid token')
    expect(logout).not.toHaveBeenCalled()
    replace = false
    await expect(get('/auth/me')).rejects.toThrow('Invalid token')
    expect(logout).toHaveBeenCalledTimes(1)
  })
})
