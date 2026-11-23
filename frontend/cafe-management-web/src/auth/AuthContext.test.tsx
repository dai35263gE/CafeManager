import { describe, expect, it } from 'vitest'
import { App as AntApp } from 'antd'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import type { AxiosAdapter } from 'axios'
import { http } from '../api/client'
import { AuthProvider, useAuth } from './AuthContext'
import { ProtectedRoute, RoleGuard } from './Guards'
import LoginPage, { loginDestination } from '../pages/LoginPage'

function SessionView() {
  const { user, logout } = useAuth()
  return <><p>Phiên của {user?.fullName}</p><button onClick={logout}>Kết thúc phiên</button></>
}
function mount(path: string, adapter: AxiosAdapter) {
  http.defaults.adapter = adapter
  const cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<AntApp><QueryClientProvider client={cache}><MemoryRouter initialEntries={[path]}><AuthProvider><Routes>
    <Route path="/login" element={<LoginPage />} />
    <Route element={<ProtectedRoute />}><Route path="/orders" element={<SessionView />} />
      <Route path="/users" element={<RoleGuard roles={['Admin']}><p>Danh sách tài khoản</p></RoleGuard>} /></Route>
    <Route element={<ProtectedRoute />}><Route path="/reports" element={<RoleGuard roles={['Manager']}><p>Báo cáo doanh thu</p></RoleGuard>} /></Route>
  </Routes></AuthProvider></MemoryRouter></QueryClientProvider></AntApp>)
}
const waiter = { id: 'test-user', username: 'test', fullName: 'Người phục vụ', role: 'Waiter', employeeId: null, isActive: true, isLocked: false, lastLoginAt: null }
describe('Authentication and route permissions', () => {
  it('validates credentials, restores the protected destination, and clears the token on logout', async () => {
    const adapter: AxiosAdapter = async config => ({ config, headers: {}, status: 200, statusText: 'OK',
      data: { success: true, message: 'OK', data: { token: 'unit-test-token', expiresAt: '2099-01-01T00:00:00Z', user: waiter } },
    })
    mount('/orders', adapter)
    const actor = userEvent.setup()
    await screen.findByRole('button', { name: 'Đăng nhập' })
    await actor.click(screen.getByRole('button', { name: 'Đăng nhập' }))
    expect(await screen.findByText('Nhập tên đăng nhập.')).toBeTruthy()
    await actor.type(screen.getByRole('textbox', { name: 'Tên đăng nhập' }), 'test')
    await actor.type(screen.getByLabelText('Mật khẩu'), 'Password@123')
    await actor.click(screen.getByRole('button', { name: 'Đăng nhập' }))
    expect(await screen.findByText('Phiên của Người phục vụ')).toBeTruthy()
    expect(sessionStorage.getItem('cafe.accessToken')).toBe('unit-test-token')
    await actor.click(screen.getByRole('button', { name: 'Kết thúc phiên' }))
    await waitFor(() => expect(sessionStorage.getItem('cafe.accessToken')).toBeNull())
    expect(await screen.findByRole('button', { name: 'Đăng nhập' })).toBeTruthy()
  })
  it('denies direct Admin route access after restoring a valid Waiter profile', async () => {
    sessionStorage.setItem('cafe.accessToken', 'unit-test-token')
    mount('/users', async config => ({ config, headers: {}, status: 200, statusText: 'OK', data: { success: true, message: 'OK', data: waiter } }))
    expect(await screen.findByText('Không có quyền truy cập')).toBeTruthy()
    expect(screen.queryByText('Danh sách tài khoản')).toBeNull()
  })
  it('allows Admin to open a Manager-only route after restoring the session', async () => {
    sessionStorage.setItem('cafe.accessToken', 'unit-test-token')
    mount('/reports', async config => ({ config, headers: {}, status: 200, statusText: 'OK',
      data: { success: true, message: 'OK', data: { ...waiter, role: 'Admin' } } }))
    expect(await screen.findByText('Báo cáo doanh thu')).toBeTruthy()
    expect(screen.queryByText('Không có quyền truy cập')).toBeNull()
  })
  it('keeps a failed session verification explicit and retryable instead of fabricating a user', async () => {
    sessionStorage.setItem('cafe.accessToken', 'unit-test-token')
    mount('/orders', async () => { throw new Error('Network unavailable') })
    expect(await screen.findByText('Không xác minh được phiên đăng nhập')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Thử lại' })).toBeTruthy()
    expect(screen.queryByText('Phiên của Người phục vụ')).toBeNull()
  })
  it('does not accept external or login-loop return locations', () => {
    expect(loginDestination({ from: '//outside.example' })).toBe('/')
    expect(loginDestination({ from: '/login' })).toBe('/')
    expect(loginDestination({ from: '/orders?tableId=test' })).toBe('/orders?tableId=test')
  })
})
