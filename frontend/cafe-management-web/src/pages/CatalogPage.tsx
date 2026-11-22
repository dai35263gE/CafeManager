import { useState } from 'react'
import { Alert, Button, Form, Image, Input, InputNumber, Modal, Select, Space, Table, Tabs } from 'antd'
import { send } from '../api/client'
import type { Category, MenuItem, MenuStatus } from '../api/types'
import { useAction, useOptions, usePage } from '../api/hooks'
import { useAuth } from '../auth/AuthContext'
import ErdCrud from '../components/ErdCrud'
import { PageTitle, QueryError, StatusTag } from '../components/Common'
import { money, options } from '../utils/display'

interface MenuForm {
  categoryId: string
  name: string
  description?: string | null
  imageUrl?: string | null
  sizes: { sizeId?: string; name: string; price: number }[]
  toppings: { toppingId?: string; name: string; price: number }[]
}

function Menu() {
  const { user } = useAuth()
  const manager = user?.role === 'Manager'
  const [statusFilter, setStatusFilter] = useState<MenuStatus | undefined>()
  const list = usePage<MenuItem>('/mon', { orderableOnly: !manager, status: statusFilter }, true)
  const categories = useOptions<Category>('/danh-muc', undefined, manager)
  const action = useAction()
  const [selected, setSelected] = useState<MenuItem | null>()
  const [statusTarget, setStatusTarget] = useState<MenuItem>()
  const [nextStatus, setNextStatus] = useState<MenuStatus>('Available')
  const [form] = Form.useForm<MenuForm>()

  const edit = (row: MenuItem | null) => {
    form.resetFields()
    form.setFieldsValue(row
      ? {
          categoryId: row.categoryId,
          name: row.name,
          description: row.description,
          imageUrl: row.imageUrl,
          sizes: row.sizes.map(size => ({ sizeId: size.sizeId, name: size.name, price: size.price })),
          toppings: row.toppings.map(topping => ({ toppingId: topping.id, name: topping.name, price: topping.price })),
        }
      : { sizes: [{ name: 'M', price: 0 }], toppings: [] })
    setSelected(row)
  }

  return <>
    <Space wrap style={{ marginBottom: 16 }}>
      <Input.Search allowClear placeholder="Tìm món" onSearch={list.setSearch} />
      {manager && <Select
        allowClear
        placeholder="Mọi trạng thái"
        style={{ width: 180 }}
        value={statusFilter}
        options={options(['Available', 'OutOfStock', 'Discontinued'])}
        onChange={value => { setStatusFilter(value); list.resetPage() }}
      />}
      <Button onClick={() => void list.query.refetch()}>Tải lại</Button>
      {manager && <Button type="primary" onClick={() => edit(null)}>Thêm món</Button>}
    </Space>
    <QueryError error={list.query.error} retry={list.query.refetch} />
    <QueryError error={categories.error} retry={categories.refetch} />
    <Table<MenuItem> rowKey="id" dataSource={list.query.data?.items} loading={list.query.isFetching} pagination={list.pagination} scroll={{ x: 1100 }} columns={[
      { title: 'Mã', dataIndex: 'id' },
      { title: 'Ảnh', dataIndex: 'imageUrl', render: (url: string | null) => url ? <Image src={url} alt="Ảnh món" width={52} height={52} style={{ objectFit: 'cover' }} /> : '—' },
      { title: 'Tên món', dataIndex: 'name' },
      { title: 'Danh mục', dataIndex: 'categoryName' },
      { title: 'Giá theo cỡ', render: (_, row) => row.sizes.map(x => `${x.name}: ${money(x.price)}`).join(', ') || money(row.basePrice) },
      { title: 'Topping', render: (_, row) => row.toppings.map(x => `${x.name} (${money(x.price)})`).join(', ') || '—' },
      { title: 'Trạng thái', dataIndex: 'status', render: value => <StatusTag value={value} /> },
      ...(manager ? [{ title: 'Thao tác', render: (_: unknown, row: MenuItem) => <Space>
        <Button onClick={() => edit(row)}>Sửa</Button>
        <Button onClick={() => { setStatusTarget(row); setNextStatus(row.status) }}>Trạng thái</Button>
      </Space> }] : []),
    ]} />
    <Modal title={selected ? 'Sửa món' : 'Thêm món'} open={selected !== undefined} width={760}
      onCancel={() => setSelected(undefined)} onOk={() => form.submit()} confirmLoading={action.busy} destroyOnHidden>
      {selected && <Alert type="info" showIcon style={{ marginBottom: 16 }}
        message="Giá đã lưu trong đơn hàng không thay đổi. Kích cỡ/topping đã dùng trong đơn không thể đổi tên hoặc xóa." />}
      <Form<MenuForm> form={form} layout="vertical" preserve={false} onFinish={async values => {
        const body = {
          categoryId: values.categoryId,
          name: values.name.trim(),
          description: values.description?.trim() || null,
          imageUrl: values.imageUrl?.trim() || null,
          status: selected?.status ?? 'Available',
          isActive: true,
          sizes: values.sizes.map(({ sizeId, ...size }) => ({ ...(sizeId ? { sizeId } : {}), ...size })),
          toppings: (values.toppings ?? []).map(({ toppingId, ...topping }) => ({ ...(toppingId ? { toppingId } : {}), ...topping })),
          ...(selected ? { rowVersion: selected.rowVersion } : {}),
        }
        const path = selected ? `/mon/${encodeURIComponent(selected.id)}` : '/mon'
        if (await action.run(() => send<MenuItem>(selected ? 'put' : 'post', path, body))) {
          setSelected(undefined)
        } else if (selected) {
          const refreshed = await list.query.refetch()
          const latest = refreshed.data?.items.find(item => item.id === selected.id)
          if (!latest || latest.rowVersion !== selected.rowVersion) setSelected(undefined)
        }
      }}>
        <Form.Item name="name" label="Tên món" rules={[{ required: true, whitespace: true, max: 100 }]}>
          <Input maxLength={100} />
        </Form.Item>
        <Form.Item name="categoryId" label="Danh mục" rules={[{ required: true }]}>
          <Select loading={categories.isLoading} options={categories.data?.map(x => ({ value: x.id, label: x.name }))} />
        </Form.Item>
        <Form.Item name="description" label="Mô tả"><Input.TextArea maxLength={2000} /></Form.Item>
        <Form.Item name="imageUrl" label="Đường dẫn hình ảnh" rules={[{ type: 'url', message: 'Hãy nhập URL hợp lệ.' }]}>
          <Input maxLength={255} />
        </Form.Item>
        <Form.Item label="Kích cỡ và giá">
          <Form.List name="sizes" rules={[{ validator: async (_, value: MenuForm['sizes']) =>
            value?.length ? Promise.resolve() : Promise.reject(new Error('Món phải có ít nhất một kích cỡ.')) }]}>
            {(fields, { add, remove: removeSize }, { errors }) => <>
              {fields.map(({ key, name, ...field }) => <Space key={key} align="baseline" style={{ display: 'flex', marginBottom: 8 }}>
                <Form.Item name={[name, 'sizeId']} {...field} hidden><Input /></Form.Item>
                <Form.Item name={[name, 'name']} rules={[{ required: true, whitespace: true, max: 20 }]}>
                  <Input placeholder="Tên kích cỡ" maxLength={20} />
                </Form.Item>
                <Form.Item name={[name, 'price']} rules={[{ required: true }, { type: 'number', min: 0, max: 999999999 }]}>
                  <InputNumber placeholder="Giá" min={0} max={999999999} precision={2} />
                </Form.Item>
                <Button danger onClick={() => removeSize(name)}>Xóa</Button>
              </Space>)}
              <Form.ErrorList errors={errors} />
              <Button onClick={() => add({ name: '', price: 0 })}>Thêm kích cỡ</Button>
            </>}
          </Form.List>
        </Form.Item>
        <Form.Item label="Topping">
          <Form.List name="toppings">
            {(fields, { add, remove: removeTopping }) => <>
              {fields.map(({ key, name, ...field }) => <Space key={key} align="baseline" style={{ display: 'flex', marginBottom: 8 }}>
                <Form.Item name={[name, 'toppingId']} {...field} hidden><Input /></Form.Item>
                <Form.Item name={[name, 'name']} rules={[{ required: true, whitespace: true, max: 100 }]}>
                  <Input placeholder="Tên topping" maxLength={100} />
                </Form.Item>
                <Form.Item name={[name, 'price']} rules={[{ required: true }, { type: 'number', min: 0, max: 999999999 }]}>
                  <InputNumber placeholder="Giá" min={0} max={999999999} precision={2} />
                </Form.Item>
                <Button danger onClick={() => removeTopping(name)}>Xóa</Button>
              </Space>)}
              <Button onClick={() => add({ name: '', price: 0 })}>Thêm topping</Button>
            </>}
          </Form.List>
        </Form.Item>
      </Form>
    </Modal>
    <Modal title="Cập nhật trạng thái món" open={!!statusTarget}
      onCancel={() => setStatusTarget(undefined)} onOk={async () => {
        if (!statusTarget) return
        if (await action.run(() => send<MenuItem>('patch', `/mon/${encodeURIComponent(statusTarget.id)}/status`, {
          status: nextStatus, rowVersion: statusTarget.rowVersion,
        }))) {
          setStatusTarget(undefined)
        } else {
          const refreshed = await list.query.refetch()
          const latest = refreshed.data?.items.find(item => item.id === statusTarget.id)
          if (!latest || latest.rowVersion !== statusTarget.rowVersion) setStatusTarget(undefined)
        }
      }} confirmLoading={action.busy} destroyOnHidden>
      <p>{statusTarget?.name}</p>
      <Select<MenuStatus> value={nextStatus} style={{ width: '100%' }} options={options(['Available', 'OutOfStock', 'Discontinued'])}
        onChange={setNextStatus} />
    </Modal>
  </>
}

export default function CatalogPage() {
  const { user } = useAuth()
  return <><PageTitle title="Thực đơn và danh mục" /><Tabs items={[
    { key: 'mon', label: 'Món', children: <Menu /> },
    { key: 'danh-muc', label: 'Danh mục', children: <ErdCrud<Category> title="Danh mục" path="/danh-muc" id="id" writable={user?.role === 'Manager'} fields={[
      { name: 'name', label: 'Tên danh mục', required: true, max: 100 },
    ]} /> },
  ]} />
  </>
}
