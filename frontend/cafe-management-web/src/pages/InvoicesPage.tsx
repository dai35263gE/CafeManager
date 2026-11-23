import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Alert, Button, DatePicker, Descriptions, Form, Input, InputNumber, Modal, Select, Space, Table, Typography } from 'antd'
import type { Dayjs } from 'dayjs'
import { ApiError, get, send } from '../api/client'
import { useAction, useOptions, usePage, useResource } from '../api/hooks'
import type { Invoice, InvoiceQuote, Order, OrderItem, PaymentMethod, Promotion } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { allowed, billing } from '../auth/permissions'
import { PageTitle, QueryError, StatusTag } from '../components/Common'
import { dateTime, money, options } from '../utils/display'

interface PaymentForm { orderId: string; promotionCode?: string; paymentMethod: PaymentMethod; receivedAmount: number }
interface CancelForm { reason: string }
const lineColumns = [
  { title: 'Món / kích cỡ / topping', key: 'name', render: (_: unknown, row: OrderItem) => <Space direction="vertical" size={0}>
    <span>{row.menuItemName}{row.sizeName ? ` · ${row.sizeName}` : ''}</span>
    {row.toppings.map(t => <Typography.Text type="secondary" key={t.toppingId}>{t.name} × {t.quantity} ({money(t.unitPrice)})</Typography.Text>)}
    {row.note && <Typography.Text type="secondary">{row.note}</Typography.Text>}
  </Space> },
  { title: 'SL', dataIndex: 'quantity' },
  { title: 'Đơn giá', dataIndex: 'unitPrice', render: money },
  { title: 'Thành tiền', dataIndex: 'lineTotal', render: money },
  { title: 'Trạng thái', dataIndex: 'status', render: (value: string) => <StatusTag value={value} /> },
]

export default function InvoicesPage() {
  const { user } = useAuth()
  const canRead = allowed(user?.role, billing)
  const manager = allowed(user?.role, ['Manager'])
  const [searchParams, setSearchParams] = useSearchParams()
  const routeOrder = searchParams.get('orderId')
  const [status, setStatus] = useState<string>()
  const [range, setRange] = useState<[Dayjs, Dayjs] | null>(null)
  const list = usePage<Invoice>('/hoa-don', { status, from: range?.[0].toISOString(), to: range?.[1].toISOString() }, canRead)
  const [detailId, setDetailId] = useState<string>()
  const detail = useResource<Invoice>(`/hoa-don/${detailId}`, undefined, canRead && !!detailId)
  const [paymentOpen, setPaymentOpen] = useState(false)
  const [promotionTimestamp, setPromotionTimestamp] = useState(0)
  const [cancelOpen, setCancelOpen] = useState(false)
  const [form] = Form.useForm<PaymentForm>()
  const [cancelForm] = Form.useForm<CancelForm>()
  const orderId = Form.useWatch('orderId', form)
  const code = Form.useWatch('promotionCode', form)?.trim().toUpperCase() || ''
  const method = Form.useWatch('paymentMethod', form)
  const received = Form.useWatch('receivedAmount', form)
  const served = useOptions<Order>('/don-hang', { status: 'Served' }, canRead && paymentOpen)
  const order = useResource<Order>(`/don-hang/${orderId}`, undefined, canRead && paymentOpen && !!orderId)
  const promotions = useOptions<Promotion>('/khuyen-mai', { status: 'Active' }, canRead && paymentOpen)
  const quote = useResource<InvoiceQuote>('/hoa-don/quote', {
    orderId, rowVersion: order.data?.rowVersion, promotionCode: code || undefined,
  }, canRead && paymentOpen && order.data?.status === 'Served')
  const action = useAction()
  const total = quote.data?.totalAmount
  const currentPromotions = promotions.data?.filter(promotion => {
    return promotion.status === 'Active' && Date.parse(promotion.startAt) <= promotionTimestamp &&
      Date.parse(promotion.endAt) >= promotionTimestamp
  })

  useEffect(() => {
    if (routeOrder && canRead) {
      form.setFieldsValue({ orderId: routeOrder, promotionCode: '', paymentMethod: 'Cash', receivedAmount: 0 })
      setPromotionTimestamp(Date.now())
      setPaymentOpen(true)
    }
  }, [routeOrder, canRead, form])
  useEffect(() => { if (method && method !== 'Cash' && total !== undefined) form.setFieldValue('receivedAmount', total) }, [method, total, form])

  function closePayment() {
    setPaymentOpen(false)
    if (routeOrder) { const next = new URLSearchParams(searchParams); next.delete('orderId'); setSearchParams(next, { replace: true }) }
  }
  async function pay(values: PaymentForm) {
    const promotionCode = values.promotionCode?.trim().toUpperCase() || ''
    if (!canRead) return
    if (!order.data || !quote.data || quote.isError || quote.isFetching || total === undefined ||
        quote.data.rowVersion !== order.data.rowVersion ||
        quote.data.promotionCode !== (promotionCode || null)) {
      await action.run(async () => {
        throw new ApiError('Chưa có báo giá hiện hành hợp lệ. Kiểm tra đơn và mã khuyến mãi trước khi thanh toán.', 409)
      })
      return
    }
    const shownOrder = order.data
    const ok = await action.run(async () => {
      const fresh = await get<Order>(`/don-hang/${values.orderId}`)
      if (fresh.status !== 'Served' || fresh.rowVersion !== shownOrder.rowVersion ||
          fresh.rowVersion !== quote.data?.rowVersion || fresh.subTotal !== shownOrder.subTotal) {
        await order.refetch()
        throw new ApiError('Đơn đã thay đổi. Kiểm tra lại đơn và mã khuyến mãi trước khi thanh toán.', 409)
      }
      const invoice = await send<Invoice>('post', '/hoa-don', {
        orderId: fresh.id, rowVersion: fresh.rowVersion, promotionCode: promotionCode || null,
        paymentMethod: values.paymentMethod, receivedAmount: values.receivedAmount,
      })
      setDetailId(invoice.id)
    }, 'Đã ghi nhận thanh toán.')
    if (ok) closePayment()
  }
  if (!canRead) return <Alert type="warning" showIcon message="Bạn không có quyền xem hóa đơn." />
  const invoice = detail.data
  return <Space direction="vertical" size="large" style={{ width: '100%' }}>
    <PageTitle title="Hóa đơn & thanh toán" extra={<Button type="primary" onClick={() => { form.resetFields(); form.setFieldsValue({ paymentMethod: 'Cash', receivedAmount: 0 }); setPromotionTimestamp(Date.now()); setPaymentOpen(true) }}>Thanh toán đơn</Button>} />
    <Space wrap>
      <Input.Search placeholder="Mã hóa đơn / đơn hàng" maxLength={150} value={list.search} onChange={e => list.setSearch(e.target.value)} allowClear />
      <Select placeholder="Tất cả trạng thái" allowClear style={{ width: 180 }} options={options(['Paid', 'Cancelled'])} onChange={(value: string | undefined) => { setStatus(value); list.resetPage() }} />
      <DatePicker.RangePicker showTime onChange={value => { setRange(value?.[0] && value[1] ? [value[0], value[1]] : null); list.resetPage() }} />
      <Typography.Text type="secondary">Thời điểm bắt đầu gồm, kết thúc không gồm: [từ, đến).</Typography.Text>
    </Space>
    <QueryError error={list.query.error} retry={list.query.refetch} />
    <Table<Invoice> rowKey="id" loading={list.query.isFetching} dataSource={list.query.data?.items} pagination={list.pagination} scroll={{ x: 950 }} columns={[
      { title: 'Hóa đơn', dataIndex: 'code' }, { title: 'Đơn', dataIndex: 'orderCode' }, { title: 'Bàn', dataIndex: 'tableCode' },
      { title: 'Thanh toán lúc', dataIndex: 'paidAt', render: dateTime }, { title: 'Tổng thu', dataIndex: 'totalAmount', render: money },
      { title: 'Phương thức', dataIndex: 'paymentMethod', render: (value: string) => options([value])[0].label },
      { title: 'Trạng thái', dataIndex: 'status', render: (value: string) => <StatusTag value={value} /> },
      { title: 'Thao tác', key: 'actions', render: (_: unknown, row) => <Button onClick={() => setDetailId(row.id)}>Chi tiết / in</Button> },
    ]} />
    <Modal open={!!detailId} title="Chi tiết hóa đơn" width={900} onCancel={() => { setDetailId(undefined); setCancelOpen(false) }} footer={<Space wrap>
      <Button disabled={!invoice || detail.isError} onClick={() => window.print()}>In hóa đơn</Button>
      {manager && invoice?.status === 'Paid' && <Button danger disabled={detail.isError || action.busy} onClick={() => { cancelForm.resetFields(); setCancelOpen(true) }}>Hủy hóa đơn</Button>}
    </Space>}>
      <QueryError error={detail.error} retry={detail.refetch} />
      {invoice && !detail.isError && <div className="invoice-print">
        <Typography.Title level={3}>Hóa đơn {invoice.code}</Typography.Title>
        <Descriptions size="small" column={{ xs: 1, sm: 2 }} items={[
          { key: 'order', label: 'Đơn / bàn', children: `${invoice.orderCode} / ${invoice.tableCode || 'Mang đi'}` },
          { key: 'date', label: 'Thanh toán', children: dateTime(invoice.paidAt) },
          { key: 'status', label: 'Trạng thái', children: <StatusTag value={invoice.status} /> },
          { key: 'cashier', label: 'ID thu ngân', children: invoice.cashierId },
          { key: 'method', label: 'Phương thức', children: options([invoice.paymentMethod])[0].label },
          { key: 'promotion', label: 'Mã khuyến mãi', children: invoice.promotionCode || 'Không' },
          { key: 'subtotal', label: 'Tiền món', children: money(invoice.subTotal) },
          { key: 'vat', label: 'Thuế VAT', children: money(invoice.vatAmount) },
          { key: 'serviceFee', label: 'Phí dịch vụ', children: money(invoice.serviceFeeAmount) },
          { key: 'discount', label: 'Giảm giá', children: money(invoice.discountAmount) },
          { key: 'total', label: 'Tổng thu', children: money(invoice.totalAmount) },
          { key: 'received', label: 'Khách đưa', children: money(invoice.receivedAmount) },
          { key: 'change', label: 'Tiền thừa', children: money(invoice.changeAmount) },
        ]} />
        <Table<OrderItem> size="small" rowKey="id" dataSource={invoice.items} columns={lineColumns} pagination={false} scroll={{ x: 600 }} />
        {invoice.status === 'Cancelled' && <Alert type="warning" message={`Đã hủy: ${invoice.cancelReason}. ${dateTime(invoice.cancelledAt)} · ID người hủy: ${invoice.cancelledByUserId || '—'}`} description="Đơn vẫn đóng. Hủy hóa đơn không tự hoàn tiền qua ngân hàng." />}
      </div>}
    </Modal>
    <Modal title="Hủy hóa đơn" open={cancelOpen} onCancel={() => setCancelOpen(false)} onOk={() => cancelForm.submit()} confirmLoading={action.busy}>
      <Alert type="warning" showIcon message="Đơn vẫn đóng; không có hoàn tiền tự động. Số tiền gốc được giữ nguyên." />
      <Form<CancelForm> form={cancelForm} layout="vertical" onFinish={async values => {
        if (!manager || !invoice) return
        if (await action.run(() => send<Invoice>('post', `/hoa-don/${invoice.id}/cancel`, { reason: values.reason.trim(), rowVersion: invoice.rowVersion }), 'Đã hủy hóa đơn.')) setCancelOpen(false)
      }}>
        <Form.Item name="reason" label="Lý do hủy" rules={[{ required: true, whitespace: true, max: 1000 }]}><Input.TextArea maxLength={1000} showCount rows={3} /></Form.Item>
      </Form>
    </Modal>
    <Modal title="Thanh toán đơn đã phục vụ" open={paymentOpen} width={800} onCancel={closePayment} onOk={() => form.submit()} confirmLoading={action.busy} okText="Ghi nhận thanh toán" okButtonProps={{ disabled: !order.data || order.isError || order.data.status !== 'Served' || !quote.data || quote.isError || quote.isFetching }}>
      <QueryError error={served.error} retry={served.refetch} />
      <QueryError error={order.error} retry={order.refetch} />
      <QueryError error={promotions.error} retry={promotions.refetch} />
      <Form<PaymentForm> form={form} layout="vertical" onFinish={pay}>
        <Form.Item name="orderId" label="Đơn chưa thanh toán, đã phục vụ" rules={[{ required: true }]}><Select showSearch optionFilterProp="label" loading={served.isFetching} options={served.data?.map(row => ({ value: row.id, label: `${row.code} · ${row.tableCode || 'Mang đi'} · ${money(row.subTotal)}` }))} /></Form.Item>
        {order.data && <><StatusTag value={order.data.status} /><Table<OrderItem> rowKey="id" size="small" columns={lineColumns} dataSource={order.data.items} pagination={false} scroll={{ x: 600 }} /></>}
        <Space wrap align="end">
          <Form.Item name="promotionCode" label="Mã khuyến mãi (không bắt buộc)" normalize={(value?: string) => value?.toUpperCase() ?? ''} rules={[{ max: 50 }]}><Input maxLength={50} allowClear /></Form.Item>
          <Select
            allowClear showSearch optionFilterProp="label" placeholder="Chọn chương trình hiện hành"
            style={{ width: 280 }}
            value={currentPromotions?.some(promotion => promotion.code === code) ? code : undefined}
            options={currentPromotions?.map(promotion => ({ value: promotion.code, label: `${promotion.code} · ${promotion.name}` }))}
            onChange={(value: string | undefined) => form.setFieldValue('promotionCode', value || '')}
          />
          <Button onClick={() => { void quote.refetch() }} loading={quote.isFetching} disabled={!order.data || order.isError}>Tính lại hóa đơn</Button>
        </Space>
        <QueryError error={quote.error} retry={quote.refetch} />
        {quote.data?.warnings.length ? <Alert type="warning" showIcon message="Cấu hình thuế/phí cần được kiểm tra." description={quote.data.warnings.join(' ')} /> : null}
        {quote.data && <Descriptions size="small" column={1} items={[
          { key: 'subtotal', label: 'Tiền món', children: money(quote.data.subTotal) },
          { key: 'discount', label: 'Giảm giá', children: money(quote.data.discountAmount) },
          { key: 'vat', label: `VAT (${quote.data.vatRatePercent}%)`, children: money(quote.data.vatAmount) },
          { key: 'serviceFee', label: `Phí dịch vụ (${quote.data.serviceFeeRatePercent}%)`, children: money(quote.data.serviceFeeAmount) },
        ]} />}
        <Typography.Title level={4}>Cần thu: {total === undefined ? 'Chưa có kết quả hợp lệ' : money(total)}</Typography.Title>
        <Form.Item name="paymentMethod" label="Phương thức" rules={[{ required: true }]}><Select options={options(['Cash', 'BankTransfer', 'Card', 'EWallet'])} /></Form.Item>
        {method && method !== 'Cash' && <Alert type="info" showIcon message="Chưa tích hợp cổng thanh toán. Thu ngân chỉ ghi nhận sau khi xác nhận đã nhận tiền từ khách." />}
        <Form.Item name="receivedAmount" label="Số tiền nhận" dependencies={['paymentMethod', 'promotionCode', 'orderId']} rules={[{ required: true }, { validator: (_, value: number | null) => {
          if (typeof value !== 'number' || !Number.isFinite(value) || value < 0 || value > Number.MAX_SAFE_INTEGER || total === undefined) return Promise.reject(new Error('Cần tổng tiền hợp lệ và số tiền nhận trong giới hạn.'))
          if (value < total || (method !== 'Cash' && value !== total)) return Promise.reject(new Error(method === 'Cash' ? 'Khách đưa phải đủ tổng tiền.' : 'Thanh toán không tiền mặt phải đúng tổng tiền.'))
          return Promise.resolve()
        } }]}><InputNumber min={0} max={Number.MAX_SAFE_INTEGER} precision={2} style={{ width: '100%' }} /></Form.Item>
        {method === 'Cash' && total !== undefined && typeof received === 'number' && <Typography.Text>Tiền thừa: {money(Math.max(0, received - total))}</Typography.Text>}
      </Form>
    </Modal>
  </Space>
}
