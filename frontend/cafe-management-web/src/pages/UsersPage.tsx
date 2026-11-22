import { Alert } from 'antd'
import { usePage } from '../api/hooks'
import type { User } from '../api/types'
import { PageTitle, QueryError } from '../components/Common'
import { dateTime } from '../utils/display'
import { Input, Table } from 'antd'

export default function UsersPage() {
  const list = usePage<User>('/tai-khoan')
  return <><PageTitle title="Tài khoản" />
    <Alert type="info" showIcon style={{ marginBottom: 16 }} message="Quản lý tài khoản chưa khả dụng: hợp đồng tạo/sửa mật khẩu và trạng thái chưa được cung cấp." />
    <Input.Search allowClear placeholder="Tìm tài khoản" onSearch={list.setSearch} style={{ maxWidth: 320, marginBottom: 16 }} />
    <QueryError error={list.query.error} retry={list.query.refetch} />
    <Table<User> rowKey="id" dataSource={list.query.data?.items} loading={list.query.isFetching} pagination={list.pagination} columns={[
      { title: 'Tên đăng nhập', dataIndex: 'username' }, { title: 'Họ tên', dataIndex: 'fullName' },
      { title: 'Mã nhân viên', dataIndex: 'employeeId' }, { title: 'Vai trò', dataIndex: 'role' },
      { title: 'Trạng thái', render: (_, row) => row.isLocked ? 'Đã khóa' : row.isActive ? 'Đang hoạt động' : 'Ngừng hoạt động' },
      { title: 'Đăng nhập gần nhất', dataIndex: 'lastLoginAt', render: dateTime },
    ]} />
  </>
}
