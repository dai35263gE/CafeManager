import { useEffect, useRef, useState } from 'react'
import dayjs from 'dayjs'
import type { Dayjs } from 'dayjs'
import { Alert, Button, DatePicker, Descriptions, Form, Input, InputNumber, Modal, Popconfirm, Select, Space, Table, Typography } from 'antd'
import { remove, send } from '../api/client'
import { useAction, useOptions, usePage, useResource } from '../api/hooks'
import type { Invoice, Order, Promotion, PromotionPreview, PromotionType, PromotionUsageSummary } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { allowed, billing } from '../auth/permissions'
import { PageTitle, QueryError, StatusTag } from '../components/Common'
import { dateTime, money, options } from '../utils/display'

interface PromotionForm { code: string; name: string; startAt: Dayjs; endAt: Dayjs; type: PromotionType; discountValue: number; minOrderAmount: number }
interface PreviewForm { orderId: string; code: string }
export default function PromotionsPage() {
  const { user } = useAuth()
  const canRead = allowed(user?.role, billing)
  const manager = allowed(user?.role, ['Manager'])
  const [status, setStatus] = useState<string>()
  const list = usePage<Promotion>('/khuyen-mai', { status }, canRead)
  const [editing, setEditing] = useState<Promotion | null>()
  const [termsLocked, setTermsLocked] = useState(false)
  const [historyPromotion, setHistoryPromotion] = useState<Promotion>()
  const [form] = Form.useForm<PromotionForm>()
  const type = Form.useWatch('type', form)
  const action = useAction()
  const historyEnabled = canRead && manager && !!historyPromotion
  const usage = useResource<PromotionUsageSummary>(
    `/khuyen-mai/${historyPromotion?.id ?? ''}/history/summary`, undefined, historyEnabled)
  const history = usePage<Invoice>(
    `/khuyen-mai/${historyPromotion?.id ?? ''}/history`, undefined, historyEnabled)
  const [previewOpen, setPreviewOpen] = useState(false)
  const [previewForm] = Form.useForm<PreviewForm>()
  const selectedOrder = Form.useWatch('orderId', previewForm)
  const selectedCode = Form.useWatch('code', previewForm)
  const orders = useOptions<Order>('/don-hang', undefined, canRead && previewOpen)
  const [preview, setPreview] = useState<PromotionPreview>()
  const [previewError, setPreviewError] = useState<unknown>()
  const [previewBusy, setPreviewBusy] = useState(false)
  const generation = useRef(0)
  useEffect(() => { generation.current++; setPreview(undefined); setPreviewError(undefined); setPreviewBusy(false) }, [selectedOrder, selectedCode, previewOpen])
  function openEdit(row: Promotion | null) {
    form.resetFields()
    form.setFieldsValue(row ? {
      code: row.code, name: row.name, startAt: dayjs(row.startAt.slice(0, 10)), endAt: dayjs(row.endAt.slice(0, 10)),
      type: row.type, discountValue: row.discountValue, minOrderAmount: row.minOrderAmount,
    } : { type: 'Percentage', minOrderAmount: 0, discountValue: 10 })
    setTermsLocked(!!row && (row.effectiveStatus === 'Ended' || Date.now() >= Date.parse(row.startAt)))
    setEditing(row)
  }
  if (!canRead) return <Alert type="warning" showIcon message="Bạn không có quyền xem khuyến mãi." />
  return <Space direction="vertical" size="large" style={{ width: '100%' }}>
    <PageTitle title="Khuyến mãi" extra={<><Button onClick={() => { previewForm.resetFields(); setPreviewOpen(true) }}>Kiểm tra mã với đơn</Button>{manager && <Button type="primary" onClick={() => openEdit(null)}>Tạo khuyến mãi</Button>}</>} />
    <Space wrap><Input.Search maxLength={150} allowClear placeholder="Tìm mã / tên" value={list.search} onChange={e => list.setSearch(e.target.value)} />
      <Select style={{ width: 220 }} allowClear placeholder="Tất cả trạng thái" options={[
        { value: 'Active', label: 'Đang / sắp diễn ra' },
        { value: 'Inactive', label: 'Tạm ngưng' },
        { value: 'Expired', label: 'Đã kết thúc' },
      ]} onChange={(value: string | undefined) => { setStatus(value); list.resetPage() }} /></Space>
    <Alert type="info" showIcon message="Khuyến mãi áp dụng cho mọi đơn đủ điều kiện; hiện chưa có phạm vi phân nhóm khách hàng hoặc đơn hàng." />
    <QueryError error={list.query.error} retry={list.query.refetch} />
    <Table<Promotion> rowKey="id" dataSource={list.query.data?.items} loading={list.query.isFetching} pagination={list.pagination} scroll={{ x: 1100 }} expandable={{ expandedRowRender: row => <Descriptions column={{ xs: 1, sm: 2 }} items={[
      { key: 'min', label: 'Giá trị đơn tối thiểu', children: money(row.minOrderAmount) },
      { key: 'scope', label: 'Phạm vi', children: 'Mọi đơn đủ điều kiện' },
    ]} /> }} columns={[
      { title: 'Mã', dataIndex: 'code' }, { title: 'Tên', dataIndex: 'name' },
      { title: 'Bắt đầu', dataIndex: 'startAt', render: (value: string) => new Date(value).toLocaleDateString('vi-VN', { timeZone: 'UTC' }) },
      { title: 'Kết thúc', dataIndex: 'endAt', render: (value: string) => new Date(value).toLocaleDateString('vi-VN', { timeZone: 'UTC' }) },
      { title: 'Mức giảm', key: 'discount', render: (_: unknown, row) => row.type === 'Percentage' ? `${row.discountValue}%` : money(row.discountValue) },
      { title: 'Trạng thái', dataIndex: 'effectiveStatus', render: (value: string) => <StatusTag value={value} /> },
      { title: 'Thao tác', key: 'action', render: (_: unknown, row) => <Space wrap>
        <Button onClick={() => { previewForm.setFieldsValue({ code: row.code, orderId: undefined }); setPreviewOpen(true) }}>Kiểm tra</Button>
        {manager && <>
          <Button onClick={() => setHistoryPromotion(row)}>Lịch sử sử dụng</Button>
          <Button onClick={() => openEdit(row)}>Sửa</Button>
          {row.status === 'Active' && <Popconfirm title="Tạm ngưng mã khuyến mãi?" description="Mã sẽ không áp dụng cho đơn mới; lịch sử hóa đơn được giữ nguyên." onConfirm={() => action.run(() => remove(`/khuyen-mai/${row.id}`), 'Đã tạm ngưng khuyến mãi.')}><Button danger disabled={action.busy}>Tạm ngưng</Button></Popconfirm>}
          {row.status === 'Inactive' && row.effectiveStatus !== 'Ended' &&
            <Popconfirm title="Kích hoạt lại chương trình?" onConfirm={() => action.run(() => send<Promotion>('patch', `/khuyen-mai/${row.id}/resume`), 'Đã kích hoạt lại khuyến mãi.')}><Button disabled={action.busy}>Kích hoạt lại</Button></Popconfirm>}
          {(row.status === 'Active' || row.status === 'Inactive') && row.effectiveStatus !== 'Ended' &&
            <Popconfirm title="Kết thúc chương trình ngay?" description="Mã sẽ không thể áp dụng cho đơn mới." onConfirm={() => action.run(() => send<Promotion>('patch', `/khuyen-mai/${row.id}/end`), 'Đã kết thúc khuyến mãi.')}><Button danger disabled={action.busy}>Kết thúc</Button></Popconfirm>}
        </>}
      </Space> },
    ]} />
    <Modal title={editing ? 'Sửa khuyến mãi' : 'Tạo khuyến mãi'} open={editing !== undefined} width={650} onCancel={() => setEditing(undefined)} onOk={() => form.submit()} confirmLoading={action.busy}>
      <Form<PromotionForm> form={form} layout="vertical" onFinish={async values => {
        if (!manager) return
        const data = {
          ...values, code: values.code.trim().toUpperCase(), name: values.name.trim(),
          startAt: `${values.startAt.format('YYYY-MM-DD')}T00:00:00Z`,
          endAt: `${values.endAt.format('YYYY-MM-DD')}T23:59:59Z`,
        }
        if (await action.run(() => send<Promotion>(editing ? 'put' : 'post', editing ? `/khuyen-mai/${editing.id}` : '/khuyen-mai', data))) setEditing(undefined)
      }}>
        <Form.Item name="code" label="Mã" normalize={(value?: string) => value?.toUpperCase() ?? ''} rules={[{ required: true, max: 10, pattern: /^[A-Z0-9_-]+$/, message: 'Mã gồm chữ, số, _ hoặc -, tối đa 10 ký tự.' }]}><Input maxLength={10} disabled={!!editing} /></Form.Item>
        <Form.Item name="name" label="Tên" rules={[{ required: true, whitespace: true, max: 100 }]}><Input maxLength={100} /></Form.Item>
        {termsLocked && <Alert type="warning" showIcon message="Chương trình đã đến ngày bắt đầu. Chỉ được đổi tên; thời gian, loại/mức giảm và giá trị đơn tối thiểu đã khóa." />}
        <Space wrap align="start">
          <Form.Item name="startAt" label="Ngày bắt đầu (UTC)" rules={[{ required: true }]}><DatePicker disabled={termsLocked} /></Form.Item>
          <Form.Item name="endAt" label="Ngày kết thúc (UTC)" dependencies={['startAt']} rules={[{ required: true }, { validator: (_, value: Dayjs | undefined) => !value || !form.getFieldValue('startAt') || value.isAfter(form.getFieldValue('startAt'), 'day') ? Promise.resolve() : Promise.reject(new Error('Ngày kết thúc phải sau ngày bắt đầu.')) }]}><DatePicker disabled={termsLocked} /></Form.Item>
        </Space>
        <Form.Item name="type" label="Loại" rules={[{ required: true }]}><Select disabled={termsLocked} options={options(['Percentage', 'FixedAmount'])} /></Form.Item>
        <Form.Item name="discountValue" label={type === 'Percentage' ? 'Giảm (%)' : 'Giảm (VND)'} dependencies={['type']} rules={[{ required: true }, { validator: (_, value: number | undefined) => value !== undefined && value > 0 && value <= (type === 'Percentage' ? 100 : 999999999) ? Promise.resolve() : Promise.reject(new Error('Mức giảm phải dương; phần trăm không vượt 100.')) }]}><InputNumber disabled={termsLocked} min={0.01} max={type === 'Percentage' ? 100 : 999999999} precision={2} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="minOrderAmount" label="Giá trị đơn tối thiểu (VND)" rules={[{ required: true }, { type: 'number', min: 0, max: 999999999 }]}><InputNumber disabled={termsLocked} min={0} max={999999999} precision={2} style={{ width: '100%' }} /></Form.Item>
        <Alert type="info" message="Điều kiện hiện hỗ trợ thời gian và giá trị đơn tối thiểu. Mã áp dụng cho mọi đơn; hệ thống chưa lưu mức giảm tối đa hoặc mô tả điều kiện riêng." />
      </Form>
    </Modal>
    <Modal title={`Lịch sử sử dụng ${historyPromotion?.code || ''}`} open={!!historyPromotion} width={850}
      onCancel={() => setHistoryPromotion(undefined)} footer={<Button onClick={() => setHistoryPromotion(undefined)}>Đóng</Button>}>
      <QueryError error={usage.error} retry={usage.refetch} />
      <QueryError error={history.query.error} retry={history.query.refetch} />
      {usage.data && <Descriptions size="small" column={{ xs: 1, sm: 2 }} items={[
        { key: 'count', label: 'Lượt sử dụng thành công', children: usage.data.usageCount },
        { key: 'discount', label: 'Tổng tiền giảm', children: money(usage.data.totalDiscountAmount) },
      ]} />}
      <Table<Invoice> rowKey="id" size="small" loading={history.query.isFetching}
        dataSource={history.query.data?.items} pagination={history.pagination} scroll={{ x: 650 }}
        locale={{ emptyText: 'Khuyến mãi chưa được sử dụng.' }}
        columns={[
          { title: 'Hóa đơn', dataIndex: 'code' }, { title: 'Đơn hàng', dataIndex: 'orderCode' },
          { title: 'Thời gian', dataIndex: 'paidAt', render: dateTime },
          { title: 'Tiền giảm', dataIndex: 'discountAmount', render: money },
          { title: 'Tổng thanh toán', dataIndex: 'totalAmount', render: money },
        ]} />
    </Modal>
    <Modal title="Kiểm tra mã trên đơn thực tế" open={previewOpen} onCancel={() => setPreviewOpen(false)} onOk={() => previewForm.submit()} confirmLoading={previewBusy} okText="Kiểm tra">
      <QueryError error={orders.error} retry={orders.refetch} />
      <Form<PreviewForm> form={previewForm} layout="vertical" onFinish={async values => {
        const current = ++generation.current
        setPreview(undefined); setPreviewError(undefined); setPreviewBusy(true)
        try { const result = await send<PromotionPreview>('post', '/khuyen-mai/validate', { orderId: values.orderId, code: values.code.trim().toUpperCase() }); if (generation.current === current) setPreview(result) }
        catch (error) { if (generation.current === current) setPreviewError(error) }
        finally { if (generation.current === current) setPreviewBusy(false) }
      }}>
        <Form.Item name="orderId" label="Đơn chưa thanh toán" rules={[{ required: true }]}><Select showSearch optionFilterProp="label" loading={orders.isFetching} options={orders.data?.filter(row => row.status !== 'Paid' && row.status !== 'Cancelled').map(row => ({ value: row.id, label: `${row.code} · ${money(row.subTotal)}` }))} /></Form.Item>
        <Form.Item name="code" label="Mã" normalize={(value?: string) => value?.toUpperCase() ?? ''} rules={[{ required: true, whitespace: true, max: 50 }]}><Input maxLength={50} /></Form.Item>
      </Form>
      <QueryError error={previewError} retry={() => previewForm.submit()} />
      {preview && <Space direction="vertical">
        <Typography.Paragraph>
          Tiền món: {money(preview.subTotal)} · VAT: {money(preview.vatAmount)} · Phí dịch vụ: {money(preview.serviceFeeAmount)} ·
          Giảm: {money(preview.discountAmount)} · Cần thu: <strong>{money(preview.totalAmount)}</strong>
        </Typography.Paragraph>
        {preview.warnings.length > 0 && <Alert type="warning" message={preview.warnings.join(' ')} />}
      </Space>}
      <Alert type="info" message="Kết quả do máy chủ tính; thanh toán sẽ kiểm tra điều kiện và tính lại." />
    </Modal>
  </Space>
}
