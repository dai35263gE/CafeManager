import { useMemo, useState } from 'react'
import { Alert, Button, Card, Col, Descriptions, Drawer, Empty, Form, Input, Modal, Popconfirm, Row, Select, Space, Tag, Typography } from 'antd'
import { remove, send } from '../api/client'
import type { Table as CafeTable, TableStatus } from '../api/types'
import { useAction, useOptions, useResource } from '../api/hooks'
import { useAuth } from '../auth/AuthContext'
import { allowed, floor } from '../auth/permissions'
import { PageTitle, QueryError, StatusTag } from '../components/Common'
import { label, options } from '../utils/display'

interface TableForm { code: string; area: string; note?: string }
interface TargetForm { targetTableId: string }
interface MergeForm { sourceTableIds: string[]; targetOrderId?: string }

const statusColor: Record<TableStatus, string> = {
  Available: 'green',
  InUse: 'blue',
  NeedsCleaning: 'orange',
  Disabled: 'default',
}

export default function TablesPage() {
  const { user } = useAuth()
  const canRead = allowed(user?.role, floor)
  const canManage = allowed(user?.role, ['Manager'])
  const tableQuery = useOptions<CafeTable>('/ban', undefined, canRead)
  const tables = tableQuery.data || []
  const [selectedId, setSelectedId] = useState<string>()
  const selectedQuery = useResource<CafeTable>(`/ban/${encodeURIComponent(selectedId || '')}`, undefined, !!selectedId)
  const selected = selectedQuery.data || tables.find(table => table.id === selectedId)
  const [status, setStatus] = useState<TableStatus>()
  const [area, setArea] = useState<string>()
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<CafeTable | null | undefined>()
  const [transferring, setTransferring] = useState(false)
  const [merging, setMerging] = useState(false)
  const [mergeSourceIds, setMergeSourceIds] = useState<string[]>([])
  const [tableForm] = Form.useForm<TableForm>()
  const [transferForm] = Form.useForm<TargetForm>()
  const [mergeForm] = Form.useForm<MergeForm>()
  const action = useAction()
  const mergeSourceTables = tables.filter(table => mergeSourceIds.includes(table.id))
  const mergeOrderCandidates = [selected, ...mergeSourceTables]
    .flatMap(table => table?.activeOrder ? [table.activeOrder] : [])
    .filter((order, index, orders) => orders.findIndex(candidate => candidate.id === order.id) === index)

  const visibleTables = useMemo(() => tables.filter(table =>
    (!status || table.status === status) &&
    (!area || table.area === area) &&
    (!search.trim() || `${table.code} ${table.area}`.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase())),
  ), [tables, status, area, search])
  const groups = useMemo(() => visibleTables.reduce<Record<string, CafeTable[]>>((result, table) => {
    if (!result[table.area]) result[table.area] = []
    result[table.area].push(table)
    return result
  }, {}), [visibleTables])
  const areas = [...new Set(tables.map(table => table.area))].sort((a, b) => a.localeCompare(b, 'vi'))

  function editTable(table: CafeTable | null) {
    tableForm.resetFields()
    tableForm.setFieldsValue(table
      ? { code: table.code, area: table.area, note: table.note || '' }
      : { code: '', area: '', note: '' })
    setEditing(table)
  }

  async function changeStatus(table: CafeTable, next: TableStatus) {
    return action.run(
      () => send<CafeTable>('patch', `/ban/${encodeURIComponent(table.id)}/status`, {
        status: next,
        rowVersion: table.rowVersion,
      }),
      next === 'Available' ? 'Bàn đã sẵn sàng sử dụng.' : 'Đã cập nhật trạng thái bàn.',
    )
  }

  if (!canRead) return <Alert type="warning" showIcon message="Bạn không có quyền xem sơ đồ bàn." />

  return <Space direction="vertical" size="large" style={{ width: '100%' }}>
    <PageTitle title="Quản lý bàn" extra={<Space>
      <Button onClick={() => { void tableQuery.refetch(); if (selectedId) void selectedQuery.refetch() }}>Tải lại</Button>
      {canManage && <Button type="primary" onClick={() => editTable(null)}>Thêm bàn</Button>}
    </Space>} />
    <Alert type="info" showIcon message="Xếp bàn chỉ ghi nhận khách đã ngồi; tạo đơn và chọn món được thực hiện riêng trong chức năng Đơn hàng." />
    <Space wrap>
      <Input.Search allowClear placeholder="Tìm mã bàn / khu vực" value={search} onChange={event => setSearch(event.target.value)} style={{ width: 260 }} />
      <Select allowClear placeholder="Tất cả trạng thái" value={status} onChange={(value: TableStatus | undefined) => setStatus(value)} options={options(['Available', 'InUse', 'NeedsCleaning', 'Disabled'])} style={{ width: 200 }} />
      <Select allowClear placeholder="Tất cả khu vực" value={area} onChange={(value: string | undefined) => setArea(value)} options={areas.map(value => ({ value, label: value }))} style={{ width: 200 }} />
    </Space>
    <QueryError error={tableQuery.error} retry={tableQuery.refetch} />
    {tableQuery.isLoading ? <Card loading /> : !visibleTables.length
      ? <Empty description={tables.length ? 'Không có bàn khớp bộ lọc.' : 'Chưa có bàn.'} />
      : Object.entries(groups).sort(([a], [b]) => a.localeCompare(b, 'vi')).map(([zone, rows]) =>
        <section key={zone}>
          <Typography.Title level={4}>{zone}</Typography.Title>
          <Row gutter={[12, 12]}>
            {rows.map(table => <Col key={table.id} xs={24} sm={12} md={8} lg={6} xl={4}>
              <Card size="small" title={<Space>
                <Button type="link" style={{ padding: 0 }} onClick={() => setSelectedId(table.id)}>{table.code}</Button>
                <Tag color={statusColor[table.status]}>{label(table.status)}</Tag>
              </Space>}>
                <Typography.Text type="secondary">Đơn đang liên kết: </Typography.Text>
                {table.activeOrder
                  ? <Button type="link" onClick={() => setSelectedId(table.id)}>{table.activeOrder.code}</Button>
                  : <Typography.Text>Chưa có</Typography.Text>}
                <div><Button type="link" onClick={() => setSelectedId(table.id)}>Xem chi tiết</Button></div>
              </Card>
            </Col>)}
          </Row>
        </section>,
      )}

    <Drawer title={`Chi tiết bàn ${selected?.code || ''}`} open={!!selectedId} width={480}
      onClose={() => setSelectedId(undefined)} extra={<Button onClick={() => { void selectedQuery.refetch(); void tableQuery.refetch() }}>Tải mới nhất</Button>}>
      <QueryError error={selectedQuery.error} retry={selectedQuery.refetch} />
      {selected && <>
        <Descriptions column={1} bordered size="small" items={[
          { key: 'code', label: 'Mã bàn', children: selected.code },
          { key: 'area', label: 'Khu vực', children: selected.area },
          { key: 'status', label: 'Trạng thái', children: <StatusTag value={selected.status} /> },
          { key: 'note', label: 'Ghi chú', children: selected.note || '—' },
          { key: 'order', label: 'Đơn đang liên kết', children: selected.activeOrder
            ? <Space><Typography.Text>{selected.activeOrder.code}</Typography.Text><StatusTag value={selected.activeOrder.status} /></Space>
            : 'Chưa có đơn đang hoạt động' },
        ]} />
        <Space wrap style={{ marginTop: 16 }}>
          {selected.status === 'Available' && <Popconfirm title={`Xác nhận xếp khách vào bàn ${selected.code}?`}
            description="Thao tác này chỉ cập nhật trạng thái bàn, không tạo đơn hàng."
            okText="Xác nhận" cancelText="Hủy" onConfirm={() => void action.run(
              () => send<CafeTable>('post', `/ban/${encodeURIComponent(selected.id)}/seat`, { rowVersion: selected.rowVersion }),
              'Đã xếp bàn. Chưa tạo đơn hàng.',
            )}>
            <Button type="primary" loading={action.busy}>Xếp bàn</Button>
          </Popconfirm>}
          {selected.status === 'InUse' && !selected.activeOrder && <Button loading={action.busy} onClick={() => void changeStatus(selected, 'NeedsCleaning')}>Cần dọn vệ sinh</Button>}
          {selected.status === 'NeedsCleaning' && <Button type="primary" loading={action.busy} onClick={() => void changeStatus(selected, 'Available')}>Đã dọn xong</Button>}
          {selected.status === 'InUse' && selected.activeOrder && <Button onClick={() => { transferForm.resetFields(); setTransferring(true) }}>Chuyển bàn</Button>}
          {selected.status === 'Available' || selected.status === 'InUse'
            ? <Button onClick={() => { mergeForm.resetFields(); setMergeSourceIds([]); setMerging(true) }}>Ghép bàn</Button>
            : null}
          {canManage && <Button onClick={() => editTable(selected)}>Sửa thông tin</Button>}
          {canManage && <Popconfirm title="Ngừng sử dụng bàn này? Lịch sử liên kết đơn vẫn được giữ." okText="Ngừng sử dụng" cancelText="Hủy"
            disabled={selected.status === 'InUse' || !!selected.activeOrder || selected.status === 'Disabled'}
            onConfirm={() => void action.run(
              () => remove(`/ban/${encodeURIComponent(selected.id)}`, { rowVersion: selected.rowVersion }),
              'Đã ngừng sử dụng bàn.',
            )}>
            <Button danger disabled={selected.status === 'InUse' || !!selected.activeOrder || selected.status === 'Disabled' || action.busy}>Ngừng sử dụng</Button>
          </Popconfirm>}
        </Space>
        {selected.status === 'InUse' && selected.activeOrder && <Alert style={{ marginTop: 16 }} type="warning" showIcon message="Bàn còn đơn đang hoạt động; cần hoàn tất hoặc xử lý đơn trước khi vệ sinh hay ngừng sử dụng." />}
      </>}
    </Drawer>

    {canManage && <Modal title={editing ? `Sửa bàn ${editing.code}` : 'Thêm bàn'} open={editing !== undefined}
      onCancel={() => setEditing(undefined)} onOk={() => tableForm.submit()} confirmLoading={action.busy} destroyOnHidden>
      <Form<TableForm> form={tableForm} layout="vertical" onFinish={async values => {
        const payload = {
          code: values.code.trim(),
          area: values.area.trim(),
          note: values.note?.trim() || null,
          rowVersion: editing?.rowVersion,
        }
        const saved = await action.run(
          () => send<CafeTable>(editing ? 'put' : 'post', editing ? `/ban/${encodeURIComponent(editing.id)}` : '/ban', payload),
          editing ? 'Đã cập nhật thông tin bàn.' : 'Đã thêm bàn.',
        )
        if (saved) setEditing(undefined)
      }}>
        <Form.Item name="code" label="Mã bàn" rules={[{ required: true, whitespace: true, max: 10 }]}>
          <Input maxLength={10} disabled={!!editing} />
        </Form.Item>
        <Form.Item name="area" label="Khu vực" rules={[{ required: true, whitespace: true, max: 50 }]}>
          <Input maxLength={50} />
        </Form.Item>
        <Form.Item name="note" label="Ghi chú" rules={[{ max: 255 }]}>
          <Input.TextArea maxLength={255} rows={3} />
        </Form.Item>
      </Form>
    </Modal>}

    {selected?.status === 'InUse' && selected.activeOrder && <Modal title={`Chuyển bàn ${selected.code}`}
      open={transferring} onCancel={() => setTransferring(false)} onOk={() => transferForm.submit()} confirmLoading={action.busy} destroyOnHidden>
      <Form<TargetForm> form={transferForm} layout="vertical" onFinish={async values => {
        const target = tables.find(table => table.id === values.targetTableId)
        const activeOrder = selected.activeOrder
        if (!target || !activeOrder) return
        const moved = await action.run(() => send<unknown>('post', '/ban/transfer', {
          sourceTableId: selected.id,
          targetTableId: target.id,
          orderRowVersion: activeOrder.rowVersion,
          sourceTableRowVersion: selected.rowVersion,
          targetTableRowVersion: target.rowVersion,
        }), 'Đã chuyển bàn và đơn đang liên kết.')
        if (moved) setTransferring(false)
      }}>
        <Form.Item name="targetTableId" label="Bàn đích" rules={[{ required: true }]}>
          <Select options={tables.filter(table => table.status === 'Available' && table.id !== selected.id)
            .map(table => ({ value: table.id, label: `${table.code} · ${table.area}` }))} />
        </Form.Item>
      </Form>
    </Modal>}

    {selected && (selected.status === 'Available' || selected.status === 'InUse') && <Modal title={`Ghép bàn vào ${selected.code}`}
      open={merging} onCancel={() => { setMerging(false); setMergeSourceIds([]) }} onOk={() => mergeForm.submit()} confirmLoading={action.busy} destroyOnHidden>
      <Form<MergeForm> form={mergeForm} layout="vertical" onFinish={async values => {
        const sources = tables.filter(table => values.sourceTableIds.includes(table.id))
        if (sources.length !== values.sourceTableIds.length || !sources.length) return
        const selectedOrder = mergeOrderCandidates.find(order => order.id === values.targetOrderId)
        const merged = await action.run(() => send<unknown>('post', '/ban/merge', {
          targetTableId: selected.id,
          targetTableRowVersion: selected.rowVersion,
          targetActiveOrderRowVersion: selected.activeOrder?.rowVersion || null,
          sources: sources.map(table => ({
            tableId: table.id,
            tableRowVersion: table.rowVersion,
            activeOrderRowVersion: table.activeOrder?.rowVersion || null,
          })),
          targetOrderId: selectedOrder?.id || null,
          targetOrderRowVersion: selectedOrder?.rowVersion || null,
        }), 'Đã ghép bàn và đồng bộ đơn đang hoạt động.')
        if (merged) { setMerging(false); setMergeSourceIds([]) }
      }}>
        <Form.Item name="sourceTableIds" label="Các bàn cần ghép" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn ít nhất một bàn.' }]}>
          <Select mode="multiple" maxCount={19} options={tables.filter(table =>
            table.id !== selected.id && (table.status === 'Available' || table.status === 'InUse'),
          ).map(table => ({ value: table.id, label: `${table.code} · ${table.area} · ${label(table.status)}` }))}
          onChange={(ids: string[]) => { setMergeSourceIds(ids); mergeForm.setFieldValue('targetOrderId', undefined) }} />
        </Form.Item>
        {mergeOrderCandidates.length > 1 && <Form.Item name="targetOrderId" label="Đơn đích" rules={[{ required: true, message: 'Chọn đơn đích để ghép các đơn đang hoạt động.' }]}>
          <Select options={mergeOrderCandidates.map(order => ({ value: order.id, label: `${order.code} · ${label(order.status)}` }))} />
        </Form.Item>}
        {mergeOrderCandidates.length > 1 && <Alert type="info" showIcon message="Các đơn còn lại trên bàn đã chọn sẽ được ghép vào đơn đích." />}
      </Form>
    </Modal>}
  </Space>
}
