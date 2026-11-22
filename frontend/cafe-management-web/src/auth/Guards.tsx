import { Button, Result, Spin } from 'antd'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'
import type { Role } from '../api/types'
import { useAuth } from './AuthContext'
import { allowed } from './permissions'
export function ProtectedRoute() {
  const { user, loading, error, retry, logout } = useAuth()
  const location = useLocation()
  if (loading) return <div className="loading-screen"><Spin size="large" /></div>
  if (error) return <Result status="error" title="Không xác minh được phiên đăng nhập" subTitle={error}
    extra={[<Button key="retry" onClick={retry}>Thử lại</Button>, <Button key="logout" onClick={logout}>Đăng nhập lại</Button>]} />
  return user ? <Outlet /> : <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
}
export function RoleGuard({ roles, children }: { roles: readonly Role[]; children?: ReactNode }) {
  const { user } = useAuth()
  return allowed(user?.role, roles) ? children || <Outlet /> : <Result status="403" title="Không có quyền truy cập" subTitle="Tài khoản không được phép sử dụng chức năng này." />
}
