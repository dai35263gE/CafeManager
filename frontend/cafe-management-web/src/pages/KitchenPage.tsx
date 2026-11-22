import { useEffect, useRef, useState } from 'react'
import { Alert, Button, Descriptions, Drawer, Form, Input, Modal, Select, Space, Table, Tabs, Tag } from 'antd'
import { useAction, useOptions, usePage } from '../api/hooks'
import { send } from '../api/client'
import type { Ingredient, ItemStatus, KitchenItem, KitchenNotice, MenuItem } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { allowed, floor, kitchen } from '../auth/permissions'
import { PageTitle, QueryError, StatusTag } from '../components/Common'
import { dateTime, options } from '../utils/display'
interface ShortageFields { menuItemId?: string; ingredientId?: string; reason?: string }
interface ReopenFields { reason: string }
export default function KitchenPage() {
  const { user } = useAuth()
  const [status, setStatus] = useState<ItemStatus | undefined>('Waiting')
  const canPrepare = allowed(user?.role, kitchen)
  const canServe = allowed(user?.role, floor)
  const canViewQueue = canPrepare || canServe
  const queue = usePage<KitchenItem>('/don-hang/kitchen-queue', { status }, canViewQueue, 5000)
  const notices = usePage<KitchenNotice>('/don-hang/kitchen-notices', undefined, true, 5000)
  const seenItemIds = useRef<Set<string> | null>(null)
  const [newItemIds, setNewItemIds] = useState<Set<string>>(new Set())
  const [selectedItem, setSelectedItem] = useState<KitchenItem | null>(null)
  const [reporting, setReporting] = useState(false)
  const [reopening, setReopening] = useState<KitchenItem | null>(null)
  const menu = useOptions<MenuItem>('/mon', { orderableOnly: false }, reporting && canPrepare)
  const ingredients = useOptions<Ingredient>('/nguyen-lieu', { active: true }, reporting && canPrepare)
  const [form] = Form.useForm<ShortageFields>()
  const [reopenForm] = Form.useForm<ReopenFields>()
  const action = useAction()
  useEffect(() => {
    const rows = queue.query.data?.items
    if (!rows) return
    const visibleIds = new Set(rows.map(row => row.item.id))
    if (!seenItemIds.current) {
      seenItemIds.current = visibleIds
      return
    }
    const added = rows.filter(row => !seenItemIds.current?.has(row.item.id))
    if (added.length) setNewItemIds(current => new Set([...current, ...added.map(row => row.item.id)]))
    seenItemIds.current = new Set([...seenItemIds.current, ...visibleIds])
  }, [queue.query.data])
  function transition(row: KitchenItem, next: ItemStatus) {
    void action.run(() => send('patch', `/don-hang/${row.orderId}/items/${row.item.id}/status`, { status: next, rowVersion: row.item.rowVersion }))
  }
  return <><PageTitle title={canViewQueue ? 'Pha chế & phục vụ' : 'Thông báo đơn hàng'} extra={<>{canPrepare && <Button type="primary" onClick={() => { form.resetFields(); setReporting(true) }}>Báo thiếu nguyên liệu</Button>}
    <Button onClick={() => { if (canViewQueue) void queue.query.refetch(); void notices.query.refetch() }}>Tải lại</Button></>} />
    {canViewQueue && <Alert type="info" showIcon message="Tự cập nhật mỗi 5 giây. Người nhận món hoặc Quản lý hoàn thành pha chế; Phục vụ xác nhận giao món." />}
    <Tabs items={[...(canViewQueue ? [{ key: 'queue', label: 'Hàng đợi pha chế', children: <>
      <div className="toolbar"><Input.Search placeholder="Tìm đơn / món" allowClear onSearch={queue.setSearch} />
        <Select aria-label="Trạng thái pha chế" allowClear placeholder="Tất cả trạng thái" style={{ width: 200 }} value={status} options={options(['Waiting', 'Preparing', 'Completed', 'Cancelled'])} onChange={(value: ItemStatus | undefined) => { setStatus(value); queue.resetPage() }} /></div>
      <QueryError error={queue.query.error} retry={queue.query.refetch} /><Table<KitchenItem> rowKey={row => row.item.id} dataSource={queue.query.data?.items} loading={queue.query.isFetching} pagination={queue.pagination} scroll={{ x: 900 }} columns={[
        { title: 'Đơn / bàn', render: (_, row) => `${row.orderCode} · ${row.tableCode || 'Mang đi'}` },
        { title: 'Món', render: (_, row) => <><Button type="link" onClick={() => setSelectedItem(row)}><strong>{row.item.menuItemName} {row.item.sizeName} × {row.item.quantity}</strong></Button>
          {newItemIds.has(row.item.id) && <Tag color="gold">Mới</Tag>}
          {row.item.status === 'Cancelled' && <Alert type="error" showIcon message="Món đã bị hủy" />}
          <div>{row.item.toppings.length ? row.item.toppings.map(topping => `${topping.name} × ${topping.quantity}/ly`).join(', ') : 'Topping: Không có'}</div>
          <div>Ghi chú: {row.item.note || 'Không có'}</div></> },
        { title: 'Trạng thái', render: (_, row) => <><StatusTag value={row.item.status} />{row.item.status === 'Preparing' && row.item.preparedByUserId && row.item.preparedByUserId !== user?.id && <Tag color="orange">Đã có người nhận</Tag>}</> },
        { title: 'Người nhận', render: (_, row) => row.item.preparedByUserId === user?.id ? 'Bạn' : row.item.preparedByUserId ? 'Pha chế khác' : 'Chưa nhận' },
        { title: 'Thời gian', dataIndex: 'createdAt', render: dateTime },
        { title: 'Thao tác', render: (_, row) => <Space>
          {canPrepare && row.item.status === 'Waiting' && <Button type="primary" loading={action.busy} onClick={() => transition(row, 'Preparing')}>Nhận pha chế</Button>}
          {canPrepare && row.item.status === 'Preparing' && (user?.role === 'Manager' || row.item.preparedByUserId === user?.id) && <Button loading={action.busy} onClick={() => transition(row, 'Completed')}>Hoàn thành</Button>}
          {canServe && row.item.status === 'Completed' && <Button type="primary" loading={action.busy} onClick={() => transition(row, 'Served')}>Đã phục vụ</Button>}
          {user?.role === 'Manager' && row.item.status === 'Cancelled' && ['Open', 'Confirmed', 'Preparing'].includes(row.orderStatus) && <Button loading={action.busy} onClick={() => { reopenForm.resetFields(); setReopening(row) }}>Mở lại món</Button>}
        </Space> },
      ]} locale={{ emptyText: status === 'Waiting' ? 'Hiện chưa có món chờ pha chế' : 'Không có món phù hợp.' }} />
    </> }] : []), { key: 'notices', label: 'Thông báo', children: <><QueryError error={notices.query.error} retry={notices.query.refetch} />
      <Input.Search placeholder="Tìm thông báo" onSearch={notices.setSearch} style={{ maxWidth: 320, marginBottom: 16 }} allowClear />
      <Table<KitchenNotice> rowKey="id" dataSource={notices.query.data?.items} pagination={notices.pagination} loading={notices.query.isFetching} columns={[
        { title: 'Nội dung', dataIndex: 'description' },
        { title: 'Đã xem', render: (_, row) => row.isRead ? <Tag>Đã xem</Tag> : <Tag color="blue">Chưa xem</Tag> },
        { title: 'Thời gian', dataIndex: 'createdAt', render: dateTime },
        { title: 'Thao tác', render: (_, row) => !row.isRead && <Button loading={action.busy} onClick={() => void action.run(
          () => send('patch', `/don-hang/kitchen-notices/${row.id}/read`), 'Đã xác nhận đã xem thông báo.',
        )}>Đã xem</Button> },
      ]} /></> }]} />
    <Drawer title="Chi tiết món pha chế" open={selectedItem !== null} onClose={() => setSelectedItem(null)} width={480}>
      {selectedItem && <Descriptions column={1} bordered>
        <Descriptions.Item label="Đơn">{selectedItem.orderCode}</Descriptions.Item>
        <Descriptions.Item label="Bàn / loại đơn">{selectedItem.tableCode || 'Mang đi'}</Descriptions.Item>
        <Descriptions.Item label="Món">{selectedItem.item.menuItemName}</Descriptions.Item>
        <Descriptions.Item label="Kích cỡ">{selectedItem.item.sizeName || 'Không có'}</Descriptions.Item>
        <Descriptions.Item label="Số lượng">{selectedItem.item.quantity}</Descriptions.Item>
        <Descriptions.Item label="Topping">{selectedItem.item.toppings.length ? selectedItem.item.toppings.map(topping => `${topping.name} × ${topping.quantity}`).join(', ') : 'Không có'}</Descriptions.Item>
        <Descriptions.Item label="Ghi chú">{selectedItem.item.note || 'Không có'}</Descriptions.Item>
        <Descriptions.Item label="Trạng thái"><StatusTag value={selectedItem.item.status} /></Descriptions.Item>
        <Descriptions.Item label="Thời gian gọi">{dateTime(selectedItem.createdAt)}</Descriptions.Item>
      </Descriptions>}
    </Drawer>
    <Modal title="Báo thiếu nguyên liệu / món" open={reporting} onCancel={() => setReporting(false)} onOk={() => form.submit()} confirmLoading={action.busy}>
      <QueryError error={menu.error || ingredients.error} retry={() => { void menu.refetch(); void ingredients.refetch() }} />
      <Alert type="warning" showIcon message="Chọn món sẽ chuyển món sang Hết hàng. Báo thiếu không tự sửa tồn nguyên liệu." />
      <Form form={form} layout="vertical" onFinish={async values => {
        if (await action.run(() => send('post', '/don-hang/shortage-reports', { ...values, menuItemId: values.menuItemId || null, ingredientId: values.ingredientId || null }))) setReporting(false)
      }}>
        <Form.Item name="menuItemId" label="Món thiếu" dependencies={['ingredientId']} rules={[({ getFieldValue }) => ({
          validator: (_, value: string | undefined) => value || getFieldValue('ingredientId') ? Promise.resolve() : Promise.reject(new Error('Chọn ít nhất một món hoặc nguyên liệu.')),
        })]}><Select allowClear showSearch optionFilterProp="label" options={menu.data?.filter(item => item.isActive && item.status !== 'Discontinued').map(item => ({ value: item.id, label: item.name }))} /></Form.Item>
        <Form.Item name="ingredientId" label="Nguyên liệu" dependencies={['menuItemId']}><Select allowClear showSearch optionFilterProp="label" options={ingredients.data?.map(item => ({ value: item.id, label: `${item.name} (${item.unit})` }))} /></Form.Item>
        <Form.Item name="reason" label="Lý do (không bắt buộc)" rules={[{ max: 1000 }]}><Input.TextArea maxLength={1000} /></Form.Item>
      </Form>
    </Modal>
    <Modal title="Mở lại món đã hủy" open={reopening !== null} onCancel={() => setReopening(null)} onOk={() => reopenForm.submit()} confirmLoading={action.busy}>
      <Form form={reopenForm} layout="vertical" onFinish={async values => {
        if (!reopening) return
        if (await action.run(() => send('patch', `/don-hang/${reopening.orderId}/items/${reopening.item.id}/reopen`, {
          rowVersion: reopening.item.rowVersion, reason: values.reason,
        }), 'Đã mở lại món và gửi thông báo cho pha chế.')) setReopening(null)
      }}>
        <Form.Item name="reason" label="Lý do mở lại" rules={[{ required: true, whitespace: true, max: 1000 }]}>
          <Input.TextArea maxLength={1000} />
        </Form.Item>
      </Form>
    </Modal>
  </>
}
