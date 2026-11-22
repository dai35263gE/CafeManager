import { useState } from 'react'
import { Alert, Button, Form, Input, InputNumber, Modal, Popconfirm, Select, Space, Table } from 'antd'
import { remove, send } from '../api/client'
import { useAction, useOptions, usePage } from '../api/hooks'
import type { Ingredient, MenuItem, Recipe } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { PageTitle, QueryError } from '../components/Common'
import { quantity } from '../utils/display'

interface RecipeForm { maMon: string; maNguyenLieu: string; soLuongSuDung: number }

export default function RecipesPage() {
  const { user } = useAuth()
  const canManage = user?.role === 'Manager'
  const recipes = usePage<Recipe>('/dinh-luong', undefined, canManage)
  const menu = useOptions<MenuItem>('/mon', undefined, canManage)
  const ingredients = useOptions<Ingredient>('/nguyen-lieu', undefined, canManage)
  const action = useAction()
  const [editing, setEditing] = useState<Recipe | null>()
  const [form] = Form.useForm<RecipeForm>()

  function open(recipe?: Recipe) {
    form.resetFields()
    if (recipe) form.setFieldsValue({
      maMon: recipe.maMon,
      maNguyenLieu: recipe.maNguyenLieu,
      soLuongSuDung: recipe.soLuongSuDung,
    })
    setEditing(recipe || null)
  }

  if (!canManage) return <Alert type="warning" showIcon message="Chỉ quản lý mới được cập nhật định lượng món." />
  return <Space direction="vertical" size="large" style={{ width: '100%' }}>
    <PageTitle title="Định lượng nguyên liệu" extra={<Button type="primary" onClick={() => open()}>Thêm định lượng</Button>} />
    <Alert type="info" showIcon message="Định lượng là lượng nguyên liệu tiêu hao cho một đơn vị món; hệ thống dùng dữ liệu này khi tự động trừ kho." />
    <QueryError error={menu.error} retry={menu.refetch} />
    <QueryError error={ingredients.error} retry={ingredients.refetch} />
    <QueryError error={recipes.query.error} retry={recipes.query.refetch} />
    <Input.Search maxLength={150} allowClear placeholder="Tìm tên món hoặc nguyên liệu" value={recipes.search} onChange={event => recipes.setSearch(event.target.value)} />
    <Table<Recipe> rowKey="maDinhLuong" loading={recipes.query.isFetching} dataSource={recipes.query.data?.items} pagination={recipes.pagination} scroll={{ x: 900 }} columns={[
      { title: 'Món', dataIndex: 'tenMon' },
      { title: 'Mã món', dataIndex: 'maMon' },
      { title: 'Nguyên liệu', dataIndex: 'tenNguyenLieu' },
      { title: 'Mã nguyên liệu', dataIndex: 'maNguyenLieu' },
      { title: 'Định lượng / món', dataIndex: 'soLuongSuDung', render: (value: number, row) => `${quantity(value)} ${row.donViTinh}` },
      { title: 'Thao tác', key: 'actions', render: (_: unknown, row: Recipe) => <Space>
        <Button onClick={() => open(row)}>Sửa</Button>
        <Popconfirm title="Xóa định lượng này?" onConfirm={() => action.run(() => remove(`/dinh-luong/${row.maDinhLuong}`), 'Đã xóa định lượng.')}>
          <Button danger disabled={action.busy}>Xóa</Button>
        </Popconfirm>
      </Space> },
    ]} />
    <Modal title={editing ? 'Sửa định lượng' : 'Thêm định lượng'} open={editing !== undefined} onCancel={() => setEditing(undefined)} onOk={() => form.submit()} confirmLoading={action.busy}>
      <Form<RecipeForm> form={form} layout="vertical" onFinish={async values => {
        const path = editing ? `/dinh-luong/${editing.maDinhLuong}` : '/dinh-luong'
        if (await action.run(() => send<Recipe>(editing ? 'put' : 'post', path, values))) {
          setEditing(undefined)
          form.resetFields()
        }
      }}>
        <Form.Item name="maMon" label="Món" rules={[{ required: true, message: 'Vui lòng chọn món.' }]}>
          <Select showSearch optionFilterProp="label" loading={menu.isLoading} options={(menu.data || []).map((item: MenuItem) => ({ value: item.id, label: `${item.name} (${item.id})` }))} />
        </Form.Item>
        <Form.Item name="maNguyenLieu" label="Nguyên liệu" rules={[{ required: true, message: 'Vui lòng chọn nguyên liệu.' }]}>
          <Select showSearch optionFilterProp="label" loading={ingredients.isLoading} options={(ingredients.data || []).map((item: Ingredient) => ({ value: item.id, label: `${item.name} (${item.unit})` }))} />
        </Form.Item>
        <Form.Item name="soLuongSuDung" label="Lượng cần dùng cho một món" rules={[
          { required: true, message: 'Vui lòng nhập định lượng.' },
          { type: 'number', min: 0.001, max: 9999999.999, message: 'Định lượng phải lớn hơn 0 và không vượt giới hạn.' },
        ]}>
          <InputNumber min={0.001} max={9999999.999} precision={3} style={{ width: '100%' }} />
        </Form.Item>
      </Form>
    </Modal>
  </Space>
}
