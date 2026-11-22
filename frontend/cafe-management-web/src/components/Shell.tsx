import { useState } from 'react'
import { Button, Dropdown, Layout, Menu, Space, Typography } from 'antd'
import { MenuOutlined } from '@ant-design/icons'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { allowed, billing, operations, stock, stockWrite } from '../auth/permissions'
import type { Role } from '../api/types'
import { label } from '../utils/display'
export const navigation: { path: string; title: string; roles?: Role[] }[] = [
  { path: '/', title: 'Tổng quan' }, { path: '/tables', title: 'Quản lý bàn', roles: operations },
  { path: '/catalog', title: 'Thực đơn', roles: operations }, { path: '/orders', title: 'Đơn hàng', roles: operations },
  { path: '/kitchen', title: 'Pha chế & phục vụ', roles: ['Manager', 'Waiter', 'Barista', 'Cashier'] },
  { path: '/invoices', title: 'Thanh toán & hóa đơn', roles: billing }, { path: '/promotions', title: 'Khuyến mãi', roles: billing },
  { path: '/inventory', title: 'Nguyên liệu & kho', roles: stock }, { path: '/recipes', title: 'Định lượng món', roles: ['Manager'] },
  { path: '/employees', title: 'Nhân viên', roles: ['Manager', 'Admin'] },
  { path: '/reports', title: 'Báo cáo', roles: stockWrite }, { path: '/users', title: 'Tài khoản & phân quyền', roles: ['Admin'] },
]
export default function Shell() {
  const { user, logout } = useAuth()
  const [collapsed, setCollapsed] = useState(false)
  const location = useLocation()
  const navigate = useNavigate()
  const items = navigation.filter(item => !item.roles || allowed(user?.role, item.roles))
  return <Layout className="app-shell">
    <Layout.Sider width={240} breakpoint="lg" collapsedWidth={0} collapsed={collapsed} onCollapse={setCollapsed} trigger={null}>
      <div className="brand">☕ Café Management</div>
      <Menu theme="dark" mode="inline" selectedKeys={[location.pathname]} items={items.map(item => ({ key: item.path, label: <Link to={item.path}>{item.title}</Link> }))} />
    </Layout.Sider>
    <Layout><Layout.Header className="topbar">
      <Space><Button aria-label="Đóng mở menu" icon={<MenuOutlined />} onClick={() => setCollapsed(!collapsed)} /><Typography.Text>{label(user?.role || '')}</Typography.Text></Space>
      <Space><Dropdown menu={{ items: [{ key: 'password', label: 'Đổi mật khẩu' }, { key: 'logout', label: 'Đăng xuất', danger: true }],
        onClick: ({ key }) => { if (key === 'logout') logout(); else navigate('/password') } }}>
        <Button>{user?.fullName}</Button>
      </Dropdown></Space>
    </Layout.Header><Layout.Content className="app-content"><Outlet /></Layout.Content></Layout>
  </Layout>
}
