import { useState } from 'react'
import dayjs from 'dayjs'
import type { Dayjs } from 'dayjs'
import { Alert, Button, DatePicker, Form, Input, InputNumber, Modal, Popconfirm, Select, Space, Switch, Table, Tag, Typography } from 'antd'
import { remove, send } from '../api/client'
import { useAction, useOptions, usePage, useResource } from '../api/hooks'
import type { Ingredient, InventoryMovement, InventoryTransaction, InventoryType, Page } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { allowed, stock, stockWrite } from '../auth/permissions'
import { PageTitle, QueryError } from '../components/Common'
import { dateTime, label, options, quantity } from '../utils/display'

interface IngredientForm { code: string; name: string; unit: string; minQuantity: number; expiryDate?: Dayjs | null }
interface MovementForm { type: InventoryType; quantity: number; reasonCategory?: string; reason?: string }
const lossReasonLabels: Record<string, string> = {
  Expired: 'Hết hạn', Spoiled: 'Hỏng/mốc', PreparationError: 'Lỗi pha chế', Other: 'Khác',
}
function Warnings({ row }: { row: Ingredient }) {
  return <Space wrap size={4}>{row.isOutOfStock && <Tag color="red">Hết hàng · cần nhập gấp</Tag>}{row.isLowStock && !row.isOutOfStock && <Tag color="orange">Sắp hết · bằng/dưới mức tối thiểu</Tag>}{row.isExpired && <Tag color="red">Đã hết hạn</Tag>}{row.isExpiringSoon && <Tag color="gold">Hết hạn trong 7 ngày</Tag>}</Space>
}
function History({ ingredient, close }: { ingredient: Ingredient; close: () => void }) {
  const history = usePage<InventoryTransaction>(`/nguyen-lieu/${ingredient.id}/history`)
  return <Modal title={`Lịch sử kho · ${ingredient.code} (${ingredient.unit})`} open onCancel={close} footer={<Button onClick={close}>Đóng</Button>} width={1000}>
    <Alert type="info" message="Nhật ký bất biến; kiểm kê ghi số đếm tuyệt đối, điều chỉnh ghi chênh lệch có dấu. Không sửa / xóa giao dịch." />
    <Input.Search maxLength={150} placeholder="Tìm lý do" value={history.search} onChange={e => history.setSearch(e.target.value)} allowClear style={{ marginBlock: 16 }} />
    <QueryError error={history.query.error} retry={history.query.refetch} />
    <Table<InventoryTransaction> rowKey="id" size="small" loading={history.query.isFetching} dataSource={history.query.data?.items} pagination={history.pagination} scroll={{ x: 900 }} columns={[
      { title: 'Thời gian', dataIndex: 'createdAt', render: dateTime },
      { title: 'Loại', dataIndex: 'type', render: label },
      { title: 'Số lượng ghi nhận', dataIndex: 'quantity', render: quantity },
      { title: 'Trước', dataIndex: 'previousQuantity', render: quantity },
      { title: 'Sau', dataIndex: 'resultingQuantity', render: quantity },
      { title: 'Lý do', dataIndex: 'reason' }, { title: 'ID người thực hiện', dataIndex: 'performedByUserId' },
    ]} />
  </Modal>
}
export default function InventoryPage() {
  const { user } = useAuth()
  const canRead = allowed(user?.role, stock)
  const canWrite = allowed(user?.role, stockWrite)
  const [warningsOnly, setWarningsOnly] = useState(false)
  const list = usePage<Ingredient>(warningsOnly ? '/nguyen-lieu/warnings' : '/nguyen-lieu', undefined, canRead, 15000)
  const stockWarnings = useOptions<Ingredient>('/nguyen-lieu/warnings', undefined, canRead, 15000)
  const [editing, setEditing] = useState<Ingredient | null>()
  const [historyIngredient, setHistoryIngredient] = useState<Ingredient>()
  const [moving, setMoving] = useState<Ingredient>()
  const [form] = Form.useForm<IngredientForm>()
  const [movementForm] = Form.useForm<MovementForm>()
  const movementType = Form.useWatch('type', movementForm)
  const movementQuantity = Form.useWatch('quantity', movementForm)
  const editHistory = useResource<Page<InventoryTransaction>>(`/nguyen-lieu/${editing?.id}/history`, { page: 1, pageSize: 1 }, canWrite && !!editing)
  const currentIngredient = useResource<Ingredient>(`/nguyen-lieu/${moving?.id}`, undefined, canWrite && !!moving)
  const action = useAction()
  const unitLocked = !!editing && (editHistory.isPending || editHistory.isError || (editHistory.data?.totalCount || 0) > 0)
  const losesStock = movementType === 'Export' ||
    (movementType === 'Adjust' && typeof movementQuantity === 'number' && movementQuantity < 0) ||
    (movementType === 'Stocktake' && typeof movementQuantity === 'number' &&
      movementQuantity < (currentIngredient.data?.quantity || 0))
  const outOfStock = stockWarnings.data?.filter(item => item.isOutOfStock) || []
  function edit(row: Ingredient | null) {
    form.resetFields()
    form.setFieldsValue(row ? { ...row, expiryDate: row.expiryDate ? dayjs(row.expiryDate) : null } : { minQuantity: 0 })
    setEditing(row)
  }
  if (!canRead) return <Alert type="warning" showIcon message="Bạn không có quyền xem kho." />
  return <Space direction="vertical" size="large" style={{ width: '100%' }}>
    <PageTitle title="Nguyên liệu & kho" extra={canWrite && <Button type="primary" onClick={() => edit(null)}>Thêm nguyên liệu</Button>} />
    <Alert type="info" showIcon message="Hạn sử dụng áp dụng chung cho nguyên liệu; hệ thống hiện không theo dõi hạn dùng hoặc giá nhập theo từng lô." />
    {outOfStock.length > 0 && <Alert type="error" showIcon message={`Cảnh báo khẩn: ${outOfStock.length} nguyên liệu đã hết hàng (${outOfStock.slice(0, 5).map(item => item.name).join(', ')}${outOfStock.length > 5 ? ', …' : ''}).`} />}
    <QueryError error={stockWarnings.error} retry={stockWarnings.refetch} />
    <Space wrap>
      <Input.Search maxLength={150} placeholder="Tìm mã / tên" value={list.search} onChange={e => list.setSearch(e.target.value)} allowClear />
      <Space><Switch checked={warningsOnly} onChange={value => { setWarningsOnly(value); list.resetPage() }} /><span>Chỉ cảnh báo</span></Space>
    </Space>
    <QueryError error={list.query.error} retry={list.query.refetch} />
    <Table<Ingredient> rowKey="id" loading={list.query.isFetching} dataSource={list.query.data?.items} pagination={list.pagination} scroll={{ x: 1100 }} columns={[
      { title: 'Mã', dataIndex: 'code' }, { title: 'Tên', dataIndex: 'name' }, { title: 'Đơn vị', dataIndex: 'unit' },
      { title: 'Tồn', dataIndex: 'quantity', render: quantity }, { title: 'Tối thiểu', dataIndex: 'minQuantity', render: quantity },
      { title: 'Hạn dùng (UTC)', dataIndex: 'expiryDate', render: (value: string | null) => value || 'Không có' },
      { title: 'Cảnh báo', key: 'warnings', render: (_: unknown, row) => <Warnings row={row} /> },
      ...(canWrite ? [{ title: 'Thao tác', key: 'actions', render: (_: unknown, row: Ingredient) => <Space wrap>
        <Button onClick={() => edit(row)}>Sửa</Button><Button onClick={() => setHistoryIngredient(row)}>Lịch sử</Button>
        <Button onClick={() => { movementForm.resetFields(); movementForm.setFieldsValue({ type: 'Import' }); setMoving(row) }}>Giao dịch kho</Button>
        <Popconfirm title="Xóa nguyên liệu chưa có tồn, lịch sử kho hoặc công thức? Hệ thống sẽ từ chối nếu có dữ liệu liên quan." onConfirm={() => action.run(() => remove(`/nguyen-lieu/${row.id}`, { rowVersion: row.rowVersion }), 'Đã xóa nguyên liệu.')}><Button danger disabled={row.quantity !== 0 || action.busy}>Xóa</Button></Popconfirm>
      </Space> }] : []),
    ]} />
    {canWrite && <Modal title={editing ? `Sửa ${editing.code}` : 'Thêm nguyên liệu'} open={editing !== undefined} onCancel={() => setEditing(undefined)} onOk={() => form.submit()} confirmLoading={action.busy} okButtonProps={{ disabled: !!editing && (editHistory.isPending || editHistory.isError) }}>
      {!editing && <Alert type="info" message="Tạo nguyên liệu với tồn bằng 0. Nhập kho để ghi nhận tồn đầu kỳ và lịch sử." />}
      <QueryError error={editHistory.error} retry={editHistory.refetch} />
      <Form<IngredientForm> form={form} layout="vertical" onFinish={async values => {
        if (!canWrite) return
        const data = { ...values, code: values.code.trim(), name: values.name.trim(), unit: values.unit.trim(), expiryDate: values.expiryDate?.format('YYYY-MM-DD') || null, rowVersion: editing?.rowVersion }
        if (await action.run(() => send<Ingredient>(editing ? 'put' : 'post', editing ? `/nguyen-lieu/${editing.id}` : '/nguyen-lieu', data))) setEditing(undefined)
      }}>
        <Form.Item name="code" label="Mã" rules={[{ required: true, whitespace: true, max: 10 }]}><Input maxLength={10} /></Form.Item>
        <Form.Item name="name" label="Tên" rules={[{ required: true, whitespace: true, max: 100 }]}><Input maxLength={100} /></Form.Item>
        <Form.Item name="unit" label="Đơn vị" help={unitLocked ? 'Không đổi đơn vị sau khi có lịch sử kho.' : undefined} rules={[{ required: true, whitespace: true, max: 20 }]}><Input maxLength={20} disabled={unitLocked} /></Form.Item>
        <Form.Item name="minQuantity" label="Tồn tối thiểu" rules={[{ required: true }, { type: 'number', min: 0, max: 9999999.999 }]}><InputNumber min={0} max={9999999.999} precision={3} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="expiryDate" label="Hạn sử dụng chung (ngày UTC)" rules={[{ required: true, message: 'Vui lòng chọn hạn sử dụng.' }]}><DatePicker style={{ width: '100%' }} /></Form.Item>
      </Form>
    </Modal>}
    {canWrite && <Modal title={`Giao dịch kho · ${moving?.name || ''}`} open={!!moving} onCancel={() => setMoving(undefined)} onOk={() => movementForm.submit()} confirmLoading={action.busy} okButtonProps={{ disabled: !currentIngredient.data || currentIngredient.isError }}>
      <QueryError error={currentIngredient.error} retry={currentIngredient.refetch} />
      {currentIngredient.data && <Typography.Paragraph>Tồn hiện tại: <strong>{quantity(currentIngredient.data.quantity)} {currentIngredient.data.unit}</strong></Typography.Paragraph>}
      <Form<MovementForm> form={movementForm} layout="vertical" onFinish={async values => {
        if (!canWrite || !currentIngredient.data) return
        const ingredient = currentIngredient.data
        const detail = values.reason?.trim() || ''
        const reason = losesStock
          ? `${lossReasonLabels[values.reasonCategory || '']}${detail ? `: ${detail}` : ''}`
          : detail
        if (await action.run(() => send<InventoryMovement>('post', `/nguyen-lieu/${ingredient.id}/${values.type.toLowerCase()}`, { quantity: values.quantity, reason, rowVersion: ingredient.rowVersion }), 'Đã ghi giao dịch kho.')) setMoving(undefined)
      }}>
        <Form.Item name="type" label="Loại giao dịch" rules={[{ required: true }]}><Select options={options(['Import', 'Export', 'Adjust', 'Stocktake'])} onChange={() => { movementForm.setFieldsValue({ quantity: undefined, reasonCategory: undefined, reason: undefined }) }} /></Form.Item>
        <Alert type="info" message={movementType === 'Adjust' ? 'Nhập chênh lệch có dấu (+ tăng, − giảm), khác 0.' : movementType === 'Stocktake' ? 'Nhập số lượng kiểm đếm thực tế tuyệt đối, không âm (có thể bằng 0).' : 'Nhập số lượng dương. Xuất kho không vượt tồn hiện tại.'} />
        <Form.Item name="quantity" label={movementType === 'Stocktake' ? 'Số lượng đếm thực tế' : movementType === 'Adjust' ? 'Chênh lệch' : 'Số lượng'} dependencies={['type']} rules={[{ required: true }, { validator: (_, value: number | null) => {
          if (typeof value !== 'number' || !Number.isFinite(value) || Math.abs(value) > 9999999.999) return Promise.reject(new Error('Nhập số lượng hợp lệ trong giới hạn ±9999999.999.'))
          if ((movementType === 'Stocktake' && value < 0) || (movementType === 'Adjust' && value === 0) || ((movementType === 'Import' || movementType === 'Export') && value <= 0)) return Promise.reject(new Error('Số lượng không hợp lệ cho loại giao dịch.'))
          const result = movementType === 'Export' ? (currentIngredient.data?.quantity || 0) - value : movementType === 'Adjust' ? (currentIngredient.data?.quantity || 0) + value : value
          return result < 0 ? Promise.reject(new Error('Tồn sau giao dịch không được âm.')) : Promise.resolve()
        } }]}><InputNumber min={movementType === 'Adjust' ? -9999999.999 : 0} max={9999999.999} precision={3} style={{ width: '100%' }} /></Form.Item>
        {losesStock && <Form.Item name="reasonCategory" label="Nguyên nhân hao hụt" rules={[{ required: true, message: 'Vui lòng chọn nguyên nhân.' }]}><Select options={[
          { value: 'Expired', label: 'Hết hạn sử dụng' }, { value: 'Spoiled', label: 'Hỏng / mốc' },
          { value: 'PreparationError', label: 'Lỗi pha chế' }, { value: 'Other', label: 'Khác' },
        ]} /></Form.Item>}
        <Form.Item name="reason" label={losesStock ? 'Mô tả chi tiết (bắt buộc nếu chọn Khác)' : 'Lý do / chứng từ'} dependencies={['reasonCategory']} rules={[{ validator: (_, value: string | undefined) => {
          const category = movementForm.getFieldValue('reasonCategory')
          const detail = value?.trim() || ''
          if ((!losesStock || category === 'Other') && !detail) return Promise.reject(new Error('Vui lòng nhập lý do.'))
          const reasonLength = detail.length + (losesStock
            ? (lossReasonLabels[category] || '').length + (detail ? 2 : 0)
            : 0)
          if (reasonLength > 1000) return Promise.reject(new Error('Lý do không được vượt quá 1000 ký tự.'))
          return Promise.resolve()
        } }]}><Input.TextArea maxLength={1000} rows={3} showCount /></Form.Item>
      </Form>
    </Modal>}
    {canWrite && historyIngredient && <History ingredient={historyIngredient} close={() => setHistoryIngredient(undefined)} />}
  </Space>
}
