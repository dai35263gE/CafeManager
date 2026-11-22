import { useState } from 'react'
import { Alert, Button, Descriptions, Form, Input, Modal, Popconfirm, Select, Space, Table, Tag, Tooltip } from 'antd'
import { send } from '../api/client'
import { useAction, usePage, useResource } from '../api/hooks'
import type { Employee, EmployeeStatus, User } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { PageTitle, QueryError } from '../components/Common'
import { label, options } from '../utils/display'

interface EmployeeForm {
  fullName: string; position: string; phone?: string; email?: string; status: EmployeeStatus
}
interface AccountForm { username: string; password: string; role: string }

function accountStatus(employee: Employee) {
  if (!employee.userId) return 'Chưa có tài khoản'
  if (employee.accountIsActive) return 'Đang hoạt động'
  if (employee.accountIsLocked) return 'Đã khóa'
  return 'Đã vô hiệu'
}

export default function EmployeesPage() {
  const { user } = useAuth()
  const writable = user?.role === 'Manager'
  const [status, setStatus] = useState<EmployeeStatus>()
  const [position, setPosition] = useState('')
  const list = usePage<Employee>('/nhan-vien', { status, position }, true)
  const [details, setDetails] = useState<Employee>()
  const [editing, setEditing] = useState<Employee | null>()
  const [accountEmployee, setAccountEmployee] = useState<Employee>()
  const [form] = Form.useForm<EmployeeForm>()
  const [accountForm] = Form.useForm<AccountForm>()
  const detail = useResource<Employee>(`/nhan-vien/${details?.id}`, undefined, !!details)
  const action = useAction()

  function edit(employee?: Employee) {
    form.resetFields()
    if (employee) form.setFieldsValue({
      fullName: employee.fullName,
      position: employee.position,
      phone: employee.phone || undefined,
      email: employee.email || undefined,
      status: employee.status,
    })
    setEditing(employee || null)
  }

  async function saveProfile(values: EmployeeForm) {
    if (editing) {
      const request = { ...values, code: editing.code }
      if (editing.status === 'Working' && values.status === 'Resigned' && editing.accountIsActive) {
        Modal.confirm({
          title: 'Xác nhận cho nhân viên nghỉ việc',
          content: 'Tài khoản của nhân viên này sẽ bị khóa. Bạn có muốn tiếp tục?',
          okText: 'Đồng ý',
          cancelText: 'Hủy',
          onOk: async () => {
            if (await action.run(() => send<Employee>('put', `/nhan-vien/${editing.id}`, request), 'Đã cập nhật hồ sơ nhân viên.'))
              setEditing(undefined)
          },
          onCancel: () => form.setFieldValue('status', editing.status),
        })
        return
      }
      if (await action.run(() => send<Employee>('put', `/nhan-vien/${editing.id}`, request), 'Đã cập nhật hồ sơ nhân viên.'))
        setEditing(undefined)
      return
    }

    const request = { fullName: values.fullName, position: values.position, phone: values.phone, email: values.email }
    if (await action.run(() => send<Employee>('post', '/nhan-vien', request), 'Đã thêm nhân viên.'))
      setEditing(undefined)
  }

  async function createAccount(values: AccountForm) {
    if (!accountEmployee) return
    if (await action.run(() =>
      send<User>('post', `/nhan-vien/${accountEmployee.id}/account`, values),
      'Đã tạo tài khoản và gán vai trò.')) {
      setAccountEmployee(undefined)
      accountForm.resetFields()
    }
  }

  async function toggleAccount(employee: Employee, locked: boolean) {
    if (!employee.userId) return
    await action.run(() =>
      send<User>('patch', `/nhan-vien/${employee.id}/account/lock`, { locked }),
      locked ? 'Đã khóa tài khoản.' : 'Đã mở khóa tài khoản.')
  }

  if (!user || (user.role !== 'Manager' && user.role !== 'Admin'))
    return <Alert type="warning" showIcon message="Bạn không có quyền xem nhân viên." />

  return <Space direction="vertical" size="large" style={{ width: '100%' }}>
    <PageTitle title="Quản lý nhân viên" extra={writable && <Button type="primary" onClick={() => edit()}>Thêm nhân viên</Button>} />
    <Space wrap>
      <Input.Search allowClear placeholder="Tìm mã nhân viên hoặc họ tên" value={list.search} onChange={event => list.setSearch(event.target.value)} style={{ width: 280 }} />
      <Input allowClear placeholder="Lọc theo vị trí" value={position} onChange={event => { setPosition(event.target.value); list.resetPage() }} style={{ width: 200 }} />
      <Select allowClear placeholder="Tất cả trạng thái" value={status} options={options(['Working', 'Resigned'])}
        onChange={(value: EmployeeStatus | undefined) => { setStatus(value); list.resetPage() }} style={{ width: 180 }} />
    </Space>
    <QueryError error={list.query.error} retry={list.query.refetch} />
    <Table<Employee> rowKey="id" dataSource={list.query.data?.items} loading={list.query.isFetching}
      pagination={list.pagination} locale={{ emptyText: 'Không tìm thấy nhân viên phù hợp' }} scroll={{ x: 1100 }} columns={[
        { title: 'Mã nhân viên', dataIndex: 'code' },
        { title: 'Họ tên', dataIndex: 'fullName' },
        { title: 'Vị trí', dataIndex: 'position' },
        { title: 'Điện thoại', dataIndex: 'phone', render: (value: string | null) => value || '—' },
        { title: 'Email', dataIndex: 'email', render: (value: string | null) => value || '—' },
        { title: 'Trạng thái làm việc', dataIndex: 'status', render: (value: EmployeeStatus) =>
          <Tag color={value === 'Working' ? 'green' : 'default'}>{label(value)}</Tag> },
        { title: 'Tài khoản', render: (_: unknown, row: Employee) => row.userId
          ? `${row.userId} · ${accountStatus(row)}` : 'Chưa có' },
        { title: 'Thao tác', key: 'actions', render: (_: unknown, row: Employee) => <Space wrap>
          <Button onClick={() => setDetails(row)}>Hồ sơ</Button>
          {writable && <>
            <Button onClick={() => edit(row)}>Sửa</Button>
            {row.status === 'Working' && !row.userId && <Button onClick={() => {
              accountForm.resetFields()
              accountForm.setFieldsValue({ role: 'Waiter' })
              setAccountEmployee(row)
            }}>Tạo tài khoản</Button>}
            {row.status === 'Working' && row.userId && <>
              {row.accountIsActive
                ? <Tooltip title={row.code === user.employeeId ? 'Không thể khóa tài khoản đang đăng nhập' : undefined}>
                  <Popconfirm title="Xác nhận khóa tài khoản?" onConfirm={() => toggleAccount(row, true)}>
                    <Button disabled={row.code === user.employeeId || action.busy}>Khóa tài khoản</Button>
                  </Popconfirm>
                </Tooltip>
                : <Popconfirm title="Xác nhận mở khóa tài khoản?" onConfirm={() => toggleAccount(row, false)}>
                  <Button disabled={action.busy}>Mở khóa</Button>
                </Popconfirm>}
            </>}
          </>}
        </Space> },
      ]} />

    <Modal title={editing ? `Sửa nhân viên · ${editing.code}` : 'Thêm nhân viên'} open={editing !== undefined}
      onCancel={() => setEditing(undefined)} onOk={() => form.submit()} confirmLoading={action.busy} destroyOnHidden>
      {!editing && <Alert type="info" showIcon message="Mã nhân viên được hệ thống tự sinh; nhân viên mới bắt đầu ở trạng thái Đang làm." />}
      <Form<EmployeeForm> form={form} layout="vertical" preserve={false} onFinish={saveProfile}>
        <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, whitespace: true, max: 150 }]}>
          <Input maxLength={150} />
        </Form.Item>
        <Form.Item name="position" label="Vị trí làm việc" rules={[{ required: true, whitespace: true, max: 100 }]}>
          <Input maxLength={100} />
        </Form.Item>
        <Form.Item name="phone" label="Số điện thoại" rules={[{ max: 30 }, { pattern: /^[0-9+(). -]*$/, message: 'Số điện thoại không hợp lệ.' }]}>
          <Input maxLength={30} />
        </Form.Item>
        <Form.Item name="email" label="Email" rules={[{ type: 'email', max: 254 }]}>
          <Input maxLength={254} />
        </Form.Item>
        {editing && <Form.Item name="status" label="Trạng thái làm việc" rules={[{ required: true }]}>
          <Select options={options(['Working', 'Resigned'])} />
        </Form.Item>}
      </Form>
    </Modal>

    <Modal title={`Tạo tài khoản · ${accountEmployee?.fullName || ''}`} open={!!accountEmployee}
      onCancel={() => setAccountEmployee(undefined)} onOk={() => accountForm.submit()} confirmLoading={action.busy} destroyOnHidden>
      <Form<AccountForm> form={accountForm} layout="vertical" preserve={false} onFinish={createAccount}>
        <Form.Item name="username" label="Tên đăng nhập" rules={[{ required: true, whitespace: true, max: 100 }]}>
          <Input maxLength={100} autoComplete="username" />
        </Form.Item>
        <Form.Item name="password" label="Mật khẩu ban đầu" rules={[{ required: true, min: 8, max: 128, message: 'Mật khẩu phải có ít nhất 8 ký tự.' }]}>
          <Input.Password maxLength={128} autoComplete="new-password" />
        </Form.Item>
        <Form.Item name="role" label="Vai trò" rules={[{ required: true }]}>
          <Select options={options(['Waiter', 'Barista', 'Cashier', 'Warehouse', 'Manager'])} />
        </Form.Item>
      </Form>
    </Modal>

    <Modal title="Hồ sơ nhân viên" open={!!details} onCancel={() => setDetails(undefined)}
      footer={<Button onClick={() => setDetails(undefined)}>Đóng</Button>}>
      <QueryError error={detail.error} retry={detail.refetch} />
      {detail.data && <Descriptions column={1} bordered size="small">
        <Descriptions.Item label="Mã nhân viên">{detail.data.code}</Descriptions.Item>
        <Descriptions.Item label="Họ tên">{detail.data.fullName}</Descriptions.Item>
        <Descriptions.Item label="Vị trí">{detail.data.position}</Descriptions.Item>
        <Descriptions.Item label="Điện thoại">{detail.data.phone || '—'}</Descriptions.Item>
        <Descriptions.Item label="Email">{detail.data.email || '—'}</Descriptions.Item>
        <Descriptions.Item label="Trạng thái">{label(detail.data.status)}</Descriptions.Item>
        <Descriptions.Item label="Tài khoản">{accountStatus(detail.data)}</Descriptions.Item>
      </Descriptions>}
    </Modal>
  </Space>
}
