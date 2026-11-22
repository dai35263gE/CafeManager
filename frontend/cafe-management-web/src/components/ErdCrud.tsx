import { useState } from 'react'
import { Button, Form, Input, Modal, Popconfirm, Select, Space, Table } from 'antd'
import { remove, send } from '../api/client'
import { useAction, usePage } from '../api/hooks'
import { PageTitle, QueryError } from './Common'

export interface Field { name: string; label: string; required?: boolean; max?: number; values?: { value: string; label: string }[]; multiline?: boolean }
export default function ErdCrud<T extends object>({ title, path, id, fields, writable }: {
  title: string; path: string; id: keyof T & string; fields: Field[]; writable: boolean
}) {
  const list = usePage<T>(path)
  const [selected, setSelected] = useState<T | null>()
  const [form] = Form.useForm()
  const action = useAction()
  const keyOf = (row: T) => String(row[id])
  const edit = (row: T | null) => { form.resetFields(); if (row) form.setFieldsValue(row); setSelected(row) }
  return <><PageTitle title={title} extra={<Space><Button onClick={() => void list.query.refetch()}>Tải lại</Button>{writable && <Button type="primary" onClick={() => edit(null)}>Thêm</Button>}</Space>} />
    <Input.Search placeholder="Tìm kiếm" allowClear onSearch={list.setSearch} style={{ maxWidth: 320, marginBottom: 16 }} />
    <QueryError error={list.query.error} retry={list.query.refetch} />
    <Table<T> rowKey={keyOf} dataSource={list.query.data?.items} loading={list.query.isFetching} pagination={list.pagination} scroll={{ x: 700 }} columns={[
      ...fields.map(field => ({ title: field.label, dataIndex: field.name })),
      ...(writable ? [{ title: 'Thao tác', key: 'action', render: (_: unknown, row: T) => <Space>
        <Button onClick={() => edit(row)}>Sửa</Button>
        <Popconfirm title="Xác nhận xóa?" onConfirm={() => action.run(() => remove(`${path}/${encodeURIComponent(keyOf(row))}`))}><Button danger disabled={action.busy}>Xóa</Button></Popconfirm>
      </Space> }] : []),
    ]} />
    <Modal title={`${selected ? 'Sửa' : 'Thêm'} ${title.toLowerCase()}`} open={selected !== undefined} onCancel={() => setSelected(undefined)} onOk={() => form.submit()} confirmLoading={action.busy} destroyOnHidden>
      <Form form={form} layout="vertical" preserve={false} onFinish={async values => {
        const url = selected ? `${path}/${encodeURIComponent(keyOf(selected))}` : path
        if (await action.run(() => send(selected ? 'put' : 'post', url, values))) {
          setSelected(undefined)
        } else if (selected) {
          const refreshed = await list.query.refetch()
          if (refreshed.data && !refreshed.data.items.some(row => keyOf(row) === keyOf(selected)))
            setSelected(undefined)
        }
      }}>
        {fields.map(field => <Form.Item key={field.name} name={field.name} label={field.label} rules={[{ required: field.required, message: 'Vui lòng nhập thông tin.' }, ...(field.max ? [{ max: field.max }] : [])]}>
          {field.values ? <Select options={field.values} /> : field.multiline ? <Input.TextArea maxLength={field.max} /> : <Input maxLength={field.max} disabled={field.name === id && !!selected} />}
        </Form.Item>)}
      </Form>
    </Modal>
  </>
}
