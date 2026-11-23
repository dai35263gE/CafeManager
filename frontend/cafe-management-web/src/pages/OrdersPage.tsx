import { useState } from 'react'
import { Alert, Button, Descriptions, Drawer, Form, Image, Input, InputNumber, Modal, Select, Space, Table, Typography } from 'antd'
import { remove, send } from '../api/client'
import type { MenuItem, NewOrderItem, Order, OrderItem, Table as CafeTable } from '../api/types'
import { useAction, useOptions, usePage, useResource } from '../api/hooks'
import { useAuth } from '../auth/AuthContext'
import { allowed, floor } from '../auth/permissions'
import { PageTitle, QueryError, StatusTag } from '../components/Common'
import { money, options } from '../utils/display'

interface OrderForm {
  type: 'DineIn' | 'TakeAway'
  tableId?: string
  menuItemId: string
  sizeId?: string
  quantity: number
  toppings: string[]
  note?: string
}

type OrderLineForm = Pick<OrderForm, 'menuItemId' | 'sizeId' | 'quantity' | 'toppings' | 'note'>
interface CancelForm { reason: string }

function toOrderItem(values: OrderLineForm): NewOrderItem {
  return {
    menuItemId: values.menuItemId,
    sizeId: values.sizeId || null,
    quantity: values.quantity,
    note: values.note?.trim() || undefined,
    toppings: (values.toppings || []).map(toppingId => ({ toppingId, quantity: 1 })),
  }
}

function startingPrice(item: MenuItem) {
  return item.sizes.length ? Math.min(...item.sizes.map(size => size.price)) : item.basePrice
}

export default function OrdersPage() {
  const { user } = useAuth()
  const writable = allowed(user?.role, floor)
  const page = usePage<Order>('/don-hang', undefined, true, 5000)
  const tables = useOptions<CafeTable>('/ban')
  const menu = useOptions<MenuItem>('/mon')
  const availableMenu = menu.data?.filter(item => item.status === 'Available') || []
  const availableTables = tables.data?.filter(table => table.status === 'Available' ||
    (table.status === 'InUse' && !table.activeOrder)) || []
  const menuOptions = [...new Set(availableMenu.map(item => item.categoryName))]
    .sort((a, b) => a.localeCompare(b, 'vi'))
    .map(category => ({
      label: category,
      options: availableMenu.filter(item => item.categoryName === category)
        .map(item => ({ value: item.id, label: `${item.name} · từ ${money(startingPrice(item))}` })),
    }))
  const [selected, setSelected] = useState<string>()
  const detail = useResource<Order>(`/don-hang/${encodeURIComponent(selected || '')}`, undefined, !!selected, 5000)
  const [creating, setCreating] = useState(false)
  const [adding, setAdding] = useState(false)
  const [editingItem, setEditingItem] = useState<OrderItem>()
  const [cancellingItem, setCancellingItem] = useState<OrderItem>()
  const [cancellingOrder, setCancellingOrder] = useState(false)
  const [form] = Form.useForm<OrderForm>()
  const [lineForm] = Form.useForm<OrderLineForm>()
  const [cancelItemForm] = Form.useForm<CancelForm>()
  const [cancelOrderForm] = Form.useForm<CancelForm>()
  const orderType = Form.useWatch('type', form)
  const createMenuId = Form.useWatch('menuItemId', form)
  const lineMenuId = Form.useWatch('menuItemId', lineForm)
  const createMenu = menu.data?.find(item => item.id === createMenuId)
  const lineMenu = menu.data?.find(item => item.id === lineMenuId)
  const action = useAction()
  const order = detail.data
  const mutable = order ? ['Open', 'Confirmed', 'Preparing'].includes(order.status) : false
  const activeItems = order?.items.filter(item => item.status !== 'Cancelled') || []
  const canComplete = !!order && ['Confirmed', 'Preparing'].includes(order.status) && activeItems.length > 0 &&
    activeItems.every(item => item.status === 'Served')
  const hasPreparedItems = !!order?.items.some(item =>
    ['Preparing', 'Completed', 'Served'].includes(item.status))
  const canCancelOrder = writable && mutable && (allowed(user?.role, ['Manager']) || !hasPreparedItems)

  function closeLine() {
    setAdding(false)
    setEditingItem(undefined)
    lineForm.resetFields()
  }

  async function submitLine(values: OrderLineForm) {
    if (!order) return
    const saved = await action.run(() => editingItem
      ? send<Order>('patch', `/don-hang/${encodeURIComponent(order.id)}/items/${encodeURIComponent(editingItem.id)}`, {
        quantity: values.quantity,
        note: values.note?.trim() || null,
        rowVersion: editingItem.rowVersion,
      })
      : send<Order>('post', `/don-hang/${encodeURIComponent(order.id)}/items`, {
        rowVersion: order.rowVersion,
        items: [toOrderItem(values)],
      }), editingItem ? 'Đã cập nhật món.' : 'Đã thêm món vào đơn.')
    if (saved) closeLine()
  }

  return <><PageTitle title="Đơn hàng" extra={<Space><Button onClick={() => void page.query.refetch()}>Tải lại</Button>
    {writable && <Button type="primary" onClick={() => {
      form.resetFields()
      form.setFieldsValue({ type: 'DineIn', quantity: 1, toppings: [] })
      setCreating(true)
    }}>Tạo đơn</Button>}</Space>} />
    <Input.Search allowClear placeholder="Tìm mã đơn" onSearch={page.setSearch} style={{ maxWidth: 320, marginBottom: 16 }} />
    <QueryError error={page.query.error} retry={page.query.refetch} />
    <Table<Order> rowKey="id" dataSource={page.query.data?.items} loading={page.query.isFetching} pagination={page.pagination} scroll={{ x: 800 }} columns={[
      { title: 'Mã đơn', dataIndex: 'code', render: (id: string) => <Button type="link" onClick={() => setSelected(id)}>{id}</Button> },
      { title: 'Bàn', dataIndex: 'tableCode', render: (value: string | null) => value || 'Mang đi' },
      { title: 'Loại', dataIndex: 'type', render: value => value === 'DineIn' ? 'Tại bàn' : 'Mang đi' },
      { title: 'Trạng thái', dataIndex: 'status', render: value => <StatusTag value={value} /> },
      { title: 'Tạm tính', dataIndex: 'subTotal', render: money },
      { title: 'Thời gian', dataIndex: 'createdAt', render: value => new Date(value).toLocaleString('vi-VN') },
    ]} />
    <Modal title="Tạo và xác nhận đơn" open={creating} onCancel={() => setCreating(false)} onOk={() => form.submit()}
      confirmLoading={action.busy} okButtonProps={{ disabled: availableMenu.length === 0 }} destroyOnHidden>
      <QueryError error={tables.error || menu.error} retry={() => { void tables.refetch(); void menu.refetch() }} />
      {availableMenu.length === 0 && <Alert type="warning" showIcon message="Hiện không có món khả dụng để tạo đơn." />}
      <Form<OrderForm> form={form} layout="vertical" preserve={false}
        onValuesChange={values => { if ('menuItemId' in values) form.setFieldsValue({ sizeId: undefined, toppings: [] }) }}
        onFinish={async values => {
          const created = await action.run(async () => {
            const createdOrder = await send<Order>('post', '/don-hang', {
              type: values.type,
              tableId: values.type === 'DineIn' ? values.tableId : null,
              confirm: true,
              items: [toOrderItem(values)],
            })
            setSelected(createdOrder.id)
          }, 'Đã tạo và xác nhận đơn.')
          if (created) setCreating(false)
        }}>
        <Form.Item name="type" label="Loại đơn" rules={[{ required: true }]}>
          <Select options={options(['DineIn', 'TakeAway'])} />
        </Form.Item>
        {orderType !== 'TakeAway' && <>
          {availableTables.length === 0 && <Alert type="info" showIcon message="Không có bàn trống; có thể chọn đơn mang đi." />}
          <Form.Item name="tableId" label="Bàn" rules={[{ required: true, message: 'Chọn bàn.' }]}>
            <Select showSearch optionFilterProp="label" options={availableTables.map(table => ({
              value: table.id, label: `${table.code} · ${table.area}`,
            }))} />
          </Form.Item>
        </>}
        <Form.Item name="menuItemId" label="Món" rules={[{ required: true, message: 'Chọn món.' }]}>
          <Select showSearch optionFilterProp="label" options={menuOptions} />
        </Form.Item>
        {createMenu?.imageUrl && <Image src={createMenu.imageUrl} alt={createMenu.name} width={120} style={{ marginBottom: 12 }} />}
        <Form.Item name="sizeId" label="Kích cỡ" rules={[{ required: !!createMenu?.sizes.length, message: 'Chọn kích cỡ.' }]}>
          <Select allowClear options={createMenu?.sizes.map(size => ({ value: size.sizeId, label: `${size.name} · ${money(size.price)}` }))} />
        </Form.Item>
        <Form.Item name="toppings" label="Topping">
          <Select mode="multiple" options={createMenu?.toppings.map(topping => ({ value: topping.id, label: `${topping.name} · ${money(topping.price)}` }))} />
        </Form.Item>
        <Form.Item name="quantity" label="Số lượng" rules={[{ required: true, type: 'integer', min: 1, max: 100 }]}>
          <InputNumber min={1} max={100} precision={0} />
        </Form.Item>
        <Form.Item name="note" label="Ghi chú món" rules={[{ max: 2000 }]}>
          <Input.TextArea maxLength={2000} rows={2} />
        </Form.Item>
      </Form>
    </Modal>
    <Drawer title={`Đơn ${selected || ''}`} open={!!selected} onClose={() => { setSelected(undefined); closeLine() }}
      width={1000} loading={detail.isLoading} extra={<Button onClick={() => void detail.refetch()}>Tải mới nhất</Button>}>
      <QueryError error={detail.error} retry={detail.refetch} />
      {order && <><Descriptions items={[
        { key: 'table', label: 'Bàn', children: order.tableCode || 'Mang đi' },
        { key: 'createdBy', label: 'Nhân viên', children: order.createdByUserId },
        { key: 'status', label: 'Trạng thái', children: <StatusTag value={order.status} /> },
        { key: 'total', label: 'Tạm tính', children: money(order.subTotal) },
      ]} />
        {writable && mutable && <Space wrap style={{ margin: '16px 0' }}>
          {order.status === 'Open' && <Button type="primary" disabled={activeItems.length === 0} loading={action.busy}
            onClick={() => void action.run(
              () => send<Order>('patch', `/don-hang/${encodeURIComponent(order.id)}/status`, {
                status: 'Confirmed', rowVersion: order.rowVersion,
              }), 'Đã xác nhận đơn và chuyển đến bộ phận pha chế.',
            )}>Xác nhận đơn</Button>}
          <Button onClick={() => {
            lineForm.resetFields()
            lineForm.setFieldsValue({ quantity: 1, toppings: [] })
            setAdding(true)
          }}>Thêm món</Button>
          {canCancelOrder && <Button danger onClick={() => {
            cancelOrderForm.resetFields()
            setCancellingOrder(true)
          }}>Hủy đơn</Button>}
        </Space>}
        {order.status === 'Open' && activeItems.length === 0 &&
          <Alert type="warning" showIcon message="BR02: Đơn cần có ít nhất một món trước khi xác nhận." />}
        {canComplete && <Alert style={{ margin: '16px 0' }} type="success" showIcon
          message="Tất cả món đã phục vụ. Nhân viên cần xác nhận hoàn tất đơn." />}
        {canComplete && writable && <Button type="primary" loading={action.busy} style={{ marginBottom: 16 }}
          onClick={() => void action.run(
            () => send<Order>('patch', `/don-hang/${encodeURIComponent(order.id)}/status`, {
              status: 'Served', rowVersion: order.rowVersion,
            }), 'Đã xác nhận hoàn tất đơn.',
          )}>Xác nhận hoàn tất đơn</Button>}
        <Table<OrderItem> rowKey="id" dataSource={order.items} pagination={false} scroll={{ x: 1000 }} columns={[
          { title: 'Món', render: (_, row) => <><strong>{row.menuItemName}</strong>{row.sizeName && ` · ${row.sizeName}`}</> },
          { title: 'Topping', render: (_, row) => row.toppings.map(topping => `${topping.name} × ${topping.quantity}`).join(', ') || '—' },
          { title: 'Số lượng', dataIndex: 'quantity' },
          { title: 'Đơn giá', dataIndex: 'unitPrice', render: money },
          { title: 'Thành tiền', dataIndex: 'lineTotal', render: money },
          { title: 'Ghi chú', dataIndex: 'note', render: value => value || '—' },
          { title: 'Trạng thái', dataIndex: 'status', render: value => <StatusTag value={value} /> },
          { title: 'Thao tác', render: (_, row) => <Space>
            {writable && mutable && row.status === 'Waiting' && <Button size="small" onClick={() => {
              lineForm.resetFields()
              lineForm.setFieldsValue({ menuItemId: row.menuItemId, quantity: row.quantity, note: row.note || '', toppings: [] })
              setEditingItem(row)
            }}>Sửa</Button>}
            {writable && mutable && row.status !== 'Cancelled' &&
              (order.status === 'Open' || activeItems.length > 1) &&
              (allowed(user?.role, ['Manager']) || row.status === 'Waiting') && <Button size="small" danger onClick={() => {
                cancelItemForm.resetFields()
                setCancellingItem(row)
              }}>Hủy món</Button>}
          </Space> },
        ]} />
      </>}
    </Drawer>
    <Modal title={editingItem ? 'Cập nhật món' : 'Thêm món vào đơn'} open={adding || !!editingItem}
      onCancel={closeLine} onOk={() => lineForm.submit()} confirmLoading={action.busy} destroyOnHidden>
      <QueryError error={menu.error} retry={menu.refetch} />
      {!editingItem && availableMenu.length === 0 && <Alert type="warning" showIcon message="Hiện không có món khả dụng để thêm vào đơn." />}
      <Form<OrderLineForm> form={lineForm} layout="vertical" preserve={false}
        onValuesChange={values => { if ('menuItemId' in values) lineForm.setFieldsValue({ sizeId: undefined, toppings: [] }) }}
        onFinish={submitLine}>
        {editingItem
          ? <Form.Item label="Món"><Typography.Text>{editingItem.menuItemName} {editingItem.sizeName || ''}</Typography.Text></Form.Item>
          : <Form.Item name="menuItemId" label="Món" rules={[{ required: true, message: 'Chọn món.' }]}>
            <Select showSearch optionFilterProp="label" options={menuOptions} />
          </Form.Item>}
        {!editingItem && lineMenu?.imageUrl && <Image src={lineMenu.imageUrl} alt={lineMenu.name} width={120} style={{ marginBottom: 12 }} />}
        {!editingItem && <Form.Item name="sizeId" label="Kích cỡ" rules={[{ required: !!lineMenu?.sizes.length, message: 'Chọn kích cỡ.' }]}>
          <Select allowClear options={lineMenu?.sizes.map(size => ({ value: size.sizeId, label: `${size.name} · ${money(size.price)}` }))} />
        </Form.Item>}
        {!editingItem && <Form.Item name="toppings" label="Topping">
          <Select mode="multiple" options={lineMenu?.toppings.map(topping => ({ value: topping.id, label: `${topping.name} · ${money(topping.price)}` }))} />
        </Form.Item>}
        <Form.Item name="quantity" label="Số lượng" rules={[{ required: true, type: 'integer', min: 1, max: 100 }]}>
          <InputNumber min={1} max={100} precision={0} />
        </Form.Item>
        <Form.Item name="note" label="Ghi chú món" rules={[{ max: 2000 }]}>
          <Input.TextArea maxLength={2000} rows={2} />
        </Form.Item>
      </Form>
    </Modal>
    <Modal title={`Hủy món ${cancellingItem?.menuItemName || ''}`} open={!!cancellingItem}
      onCancel={() => setCancellingItem(undefined)} onOk={() => cancelItemForm.submit()} confirmLoading={action.busy} destroyOnHidden>
      <Form<CancelForm> form={cancelItemForm} layout="vertical" onFinish={async values => {
        if (!order || !cancellingItem) return
        const cancelled = await action.run(() => remove(
          `/don-hang/${encodeURIComponent(order.id)}/items/${encodeURIComponent(cancellingItem.id)}`,
          undefined, { rowVersion: cancellingItem.rowVersion, reason: values.reason.trim() },
        ), 'Đã hủy món.')
        if (cancelled) setCancellingItem(undefined)
      }}>
        <Form.Item name="reason" label="Lý do hủy" rules={[{ required: true, whitespace: true, max: 1000 }]}>
          <Input.TextArea maxLength={1000} rows={3} />
        </Form.Item>
      </Form>
    </Modal>
    <Modal title={`Hủy đơn ${order?.code || ''}`} open={cancellingOrder}
      onCancel={() => setCancellingOrder(false)} onOk={() => cancelOrderForm.submit()} confirmLoading={action.busy} destroyOnHidden>
      <Form<CancelForm> form={cancelOrderForm} layout="vertical" onFinish={async values => {
        if (!order) return
        const cancelled = await action.run(() => remove(
          `/don-hang/${encodeURIComponent(order.id)}`,
          undefined, { rowVersion: order.rowVersion, reason: values.reason.trim() },
        ), 'Đã hủy đơn.')
        if (cancelled) setCancellingOrder(false)
      }}>
        <Form.Item name="reason" label="Lý do hủy đơn" rules={[{ required: true, whitespace: true, max: 1000 }]}>
          <Input.TextArea maxLength={1000} rows={3} />
        </Form.Item>
      </Form>
    </Modal>
  </>
}
