import { Card, Col, Row, Statistic, Typography } from 'antd'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { allowed, operations } from '../auth/permissions'
import { useOptions } from '../api/hooks'
import type { Order, Table, User } from '../api/types'
import { navigation } from '../components/Shell'
import { PageTitle, QueryError } from '../components/Common'

export default function DashboardPage() {
  const { user } = useAuth()
  const enabled = allowed(user?.role, operations)
  const tables = useOptions<Table>('/ban', undefined, enabled)
  const orders = useOptions<Order>('/don-hang', undefined, enabled)
  const accounts = useOptions<User>('/tai-khoan', undefined, user?.role === 'Admin')
  const summary = [
    ...(enabled ? [
      { title: 'Bàn sẵn sàng', value: tables.data?.filter(t => t.status === 'Available').length },
      { title: 'Bàn đang sử dụng', value: tables.data?.filter(t => t.status === 'InUse').length },
      { title: 'Đơn đang hoạt động', value: orders.data?.filter(o => !['Paid', 'Cancelled'].includes(o.status)).length },
    ] : []),
    ...(user?.role === 'Admin' ? [{ title: 'Tài khoản hệ thống', value: accounts.data?.length }] : []),
  ]
  return <><PageTitle title={`Xin chào, ${user?.fullName}`} />
    <QueryError error={tables.error || orders.error || accounts.error} retry={() => { if (enabled) { void tables.refetch(); void orders.refetch() }; if (user?.role === 'Admin') void accounts.refetch() }} />
    <Row gutter={[16, 16]}>{summary.map(item => <Col xs={24} sm={12} lg={8} key={item.title}><Card loading={item.value === undefined}><Statistic title={item.title} value={item.value ?? '—'} /></Card></Col>)}</Row>
    <Typography.Title level={4}>Chức năng của bạn</Typography.Title>
    <Row gutter={[16, 16]}>{navigation.filter(item => item.path !== '/' && (!item.roles || allowed(user?.role, item.roles))).map(item =>
      <Col xs={24} sm={12} lg={8} key={item.path}><Link to={item.path}><Card hoverable>{item.title} →</Card></Link></Col>)}</Row>
  </>
}
