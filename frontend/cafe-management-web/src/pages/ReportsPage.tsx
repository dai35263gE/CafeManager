import { useState } from 'react'
import dayjs from 'dayjs'
import type { Dayjs } from 'dayjs'
import { Alert, Button, Card, Col, DatePicker, Descriptions, Dropdown, Form, Input, Modal, Progress, Row, Select, Space, Statistic, Table, Tabs, Typography } from 'antd'
import type { MenuProps } from 'antd'
import type { Content } from 'pdfmake/interfaces'
import { ApiError, http } from '../api/client'
import { useAction, usePage, useResource } from '../api/hooks'
import type { Ingredient, Invoice, OrderItem, Page, PaymentReport, ReportInvoice, RevenueDay, RevenueReport, TopItem } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { allowed, stockWrite } from '../auth/permissions'
import { PageTitle, QueryError } from '../components/Common'
import { dateTime, label, money, quantity } from '../utils/display'
import { makeWorkbook, parseCsv, responseFileName, saveBlob } from '../utils/reportExports'

interface RangeForm { anchor?: Dayjs; from?: Dayjs; to?: Dayjs }
interface Range { from: string; to: string }
type ReportName = 'revenue' | 'top-items' | 'payment-methods' | 'inventory'
type PeriodMode = 'day' | 'week' | 'month' | 'custom'
type StockFilter = 'all' | 'low-stock' | 'expiring'
interface ReportRowSelection { report: Exclude<ReportName, 'inventory'>; key: string; title: string }

const periodOptions = [
  { value: 'day', label: 'Theo ngày' },
  { value: 'week', label: 'Theo tuần' },
  { value: 'month', label: 'Theo tháng' },
  { value: 'custom', label: 'Từ ngày đến ngày' },
]
const stockFilterOptions = [
  { value: 'all', label: 'Tất cả nguyên liệu' },
  { value: 'low-stock', label: 'Dưới định mức tối thiểu' },
  { value: 'expiring', label: 'Sắp hết hạn (7 ngày)' },
]

function ReportError({ error, retry }: { error: unknown; retry: () => unknown }) {
  return <>{error instanceof ApiError && error.status === 404 && <Alert type="info" showIcon message="Không có dữ liệu để thống kê." />}<QueryError error={error} retry={retry} /></>
}

async function fetchReportCsv(report: ReportName, range: Range | undefined, filter: StockFilter, search: string) {
  const params = {
    ...(report === 'inventory' ? {} : range),
    ...(report === 'inventory' ? { filter } : {}),
    ...(search.trim() ? { search: search.trim() } : {}),
  }
  const response = await http.get<Blob>(`/bao-cao/export/${report}`, {
    params, responseType: 'blob', validateStatus: () => true,
  })
  const contentType = String(response.headers['content-type'] || '')
  if (response.status < 200 || response.status >= 300 || contentType.includes('json')) {
    let message = response.status === 404 ? 'Không có dữ liệu để xuất.' : 'Không xuất được báo cáo.'
    const body = await response.data.text()
    try {
      const parsed: unknown = JSON.parse(body)
      if (typeof parsed === 'object' && parsed !== null && 'message' in parsed && typeof parsed.message === 'string')
        message = parsed.message
    } catch (error) { if (!(error instanceof SyntaxError)) throw error }
    throw new ApiError(message, response.status)
  }
  if (!contentType.includes('text/csv')) throw new ApiError('Máy chủ không trả về dữ liệu CSV hợp lệ.', response.status)
  const csv = await response.data.text()
  const rows = parseCsv(csv)
  if (!rows.length) throw new ApiError('Không có dữ liệu để xuất.', 404)
  const fileName = responseFileName(String(response.headers['content-disposition'] || ''), `cafe-${report}.csv`)
  return { csv, rows, fileName }
}

async function downloadReport(report: ReportName, format: 'csv' | 'xlsx' | 'pdf',
  range: Range | undefined, filter: StockFilter, search: string) {
  const { csv, rows, fileName } = await fetchReportCsv(report, range, filter, search)
  if (format === 'csv') {
    saveBlob(new Blob([csv], { type: 'text/csv;charset=utf-8' }), fileName)
    return
  }
  const baseName = fileName.replace(/\.csv$/i, '')
  if (format === 'xlsx') {
    const bytes = makeWorkbook(rows, report)
    saveBlob(new Blob([bytes], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }), `${baseName}.xlsx`)
    return
  }
  const [{ createPdf }, fontModule] = await Promise.all([
    import('pdfmake/build/pdfmake'),
    import('pdfmake/build/vfs_fonts'),
  ])
  const vfs = 'default' in fontModule ? fontModule.default : fontModule
  const headers = rows[0]
  if (!headers) throw new ApiError('Không có dữ liệu để xuất.', 404)
  const content: Content[] = [
    { text: 'BÁO CÁO QUẢN LÝ QUÁN CÀ PHÊ', style: 'title', margin: [0, 0, 0, 10] },
    { text: label(report), style: 'subtitle', margin: [0, 0, 0, 12] },
  ]
  if (range) content.push({ text: `Kỳ UTC: ${range.from} → ${range.to}`, margin: [0, 0, 0, 12] })
  content.push({
    table: { headerRows: 1, widths: headers.map(() => '*'), body: rows.map(row => row.map(text => ({ text }))) },
    layout: 'lightHorizontalLines', fontSize: 8,
  })
  createPdf({
    pageOrientation: 'landscape',
    pageMargins: [24, 28, 24, 34],
    content,
    styles: { title: { fontSize: 16, bold: true }, subtitle: { fontSize: 11 } },
    defaultStyle: { font: 'Roboto' },
    footer: (currentPage: number, pageCount: number) => ({
      text: `Trang ${currentPage}/${pageCount}`, alignment: 'right', margin: [0, 8, 24, 0],
    }),
  }, undefined, {
    Roboto: {
      normal: 'Roboto-Regular.ttf',
      bold: 'Roboto-Medium.ttf',
      italics: 'Roboto-Italic.ttf',
      bolditalics: 'Roboto-MediumItalic.ttf',
    },
  }, vfs).download(`${baseName}.pdf`)
}

function mondayStart(date: Dayjs) {
  return date.startOf('day').subtract((date.day() + 6) % 7, 'day')
}

function utcDayStart(date: Dayjs) {
  return new Date(Date.UTC(date.year(), date.month(), date.date())).toISOString()
}

export default function ReportsPage() {
  const { user } = useAuth()
  const canStock = allowed(user?.role, stockWrite)
  const financial = user?.role === 'Manager'
  const [tab, setTab] = useState<ReportName>(financial ? 'revenue' : 'inventory')
  const [range, setRange] = useState<Range>()
  const [periodMode, setPeriodMode] = useState<PeriodMode>('month')
  const [rangeForm] = Form.useForm<RangeForm>()
  const [dayPage, setDayPage] = useState(1)
  const [daySize, setDaySize] = useState(20)
  const [stockFilter, setStockFilter] = useState<StockFilter>('all')
  const [reportDetails, setReportDetails] = useState<ReportRowSelection>()
  const [detailPage, setDetailPage] = useState(1)
  const [invoiceId, setInvoiceId] = useState<string>()
  const revenue = useResource<RevenueReport>('/bao-cao/revenue', { ...range, page: dayPage, pageSize: daySize }, financial && !!range && tab === 'revenue')
  const top = usePage<TopItem>('/bao-cao/top-items', range ? { ...range } : undefined, financial && !!range && tab === 'top-items')
  const methods = usePage<PaymentReport>('/bao-cao/payment-methods', range ? { ...range } : undefined, financial && !!range && tab === 'payment-methods')
  const inventory = usePage<Ingredient>('/bao-cao/inventory', { filter: stockFilter }, canStock && (!financial || tab === 'inventory'))
  const detailQuery = useResource<Page<ReportInvoice>>('/bao-cao/details', {
    report: reportDetails?.report, key: reportDetails?.key, ...range, page: detailPage, pageSize: 10,
  }, !!reportDetails && !!range)
  const invoiceQuery = useResource<Invoice>(`/hoa-don/${invoiceId}`, undefined, !!invoiceId)
  const action = useAction()
  const revenueRows = revenue.data?.days.items || []
  const maxDailyRevenue = Math.max(0, ...revenueRows.map(row => row.revenue))
  const topRows = top.query.data?.items || []
  const maxItemQuantity = Math.max(0, ...topRows.map(row => row.quantity))
  const methodRows = methods.query.data?.items || []
  const methodRevenue = methodRows.reduce((total, row) => total + row.revenue, 0)
  if (!canStock) return <Alert type="warning" showIcon message="Bạn không có quyền xem báo cáo." />

  const effectiveTab: ReportName = financial ? tab : 'inventory'
  function changeTab(value: string) {
    if (value === 'inventory' || (financial && (value === 'revenue' || value === 'top-items' || value === 'payment-methods'))) setTab(value)
  }
  function applyRange(values: RangeForm) {
    if (periodMode === 'custom') {
      if (!values.from || !values.to || !values.to.isAfter(values.from)) return
      setRange({ from: values.from.toISOString(), to: values.to.toISOString() })
    } else {
      if (!values.anchor) return
      const selected = values.anchor.startOf('day')
      let from: Dayjs
      let to: Dayjs
      if (periodMode === 'day') { from = selected; to = selected.add(1, 'day') }
      else if (periodMode === 'week') { from = mondayStart(selected); to = from.add(7, 'day') }
      else { from = selected.startOf('month'); to = from.add(1, 'month') }
      setRange({ from: utcDayStart(from), to: utcDayStart(to) })
    }
    setDayPage(1)
    top.resetPage()
    methods.resetPage()
    setDetailPage(1)
    setReportDetails(undefined)
    setInvoiceId(undefined)
  }
  function openDetails(selection: ReportRowSelection) {
    setDetailPage(1)
    setReportDetails(selection)
  }
  const exportItems: MenuProps['items'] = [
    { key: 'xlsx', label: 'Excel (.xlsx)' },
    { key: 'pdf', label: 'PDF (.pdf)' },
    { key: 'csv', label: 'CSV (.csv)' },
  ]
  function exportReport(key: string) {
    if (key !== 'xlsx' && key !== 'pdf' && key !== 'csv') return
    void action.run(() => downloadReport(effectiveTab, key, range, stockFilter,
    effectiveTab === 'top-items' ? top.search : effectiveTab === 'inventory' ? inventory.search : ''),
    key === 'xlsx' ? 'Đã tải báo cáo Excel.' : key === 'pdf' ? 'Đã tải báo cáo PDF.' : 'Đã tải báo cáo CSV.')
  }

  return <Space direction="vertical" size="large" style={{ width: '100%' }}>
    <PageTitle title="Báo cáo và thống kê" extra={<Dropdown menu={{ items: exportItems, onClick: ({ key }) => exportReport(key) }} trigger={['click']}>
      <Button loading={action.busy} disabled={effectiveTab !== 'inventory' && !range}>Xuất báo cáo</Button>
    </Dropdown>} />
    <Alert type="info" showIcon message="Báo cáo tài chính chỉ tính hóa đơn đã thanh toán. Chọn dòng doanh thu, món hoặc phương thức để xem các hóa đơn; chọn hóa đơn để mở chi tiết." />
    {financial && <Card size="small" title="Kỳ báo cáo tài chính">
      <Space wrap style={{ marginBottom: 16 }}>
        <Typography.Text>Thống kê</Typography.Text>
        <Select value={periodMode} options={periodOptions} onChange={(value: PeriodMode) => setPeriodMode(value)} style={{ width: 190 }} />
      </Space>
      <Form<RangeForm> form={rangeForm} layout="vertical" initialValues={{
        anchor: dayjs(), from: dayjs().startOf('month'), to: dayjs().add(1, 'day').startOf('day'),
      }} onFinish={applyRange}>
        <Space wrap align="start">
          {periodMode === 'custom'
            ? <>
              <Form.Item name="from" label="Từ (gồm thời điểm này)" rules={[{ required: true }]}><DatePicker showTime /></Form.Item>
              <Form.Item name="to" label="Đến (không gồm thời điểm này)" dependencies={['from']} rules={[
                { required: true },
                { validator: (_, value: Dayjs | undefined) => !value || !rangeForm.getFieldValue('from') ||
                  value.isAfter(rangeForm.getFieldValue('from')) ? Promise.resolve() : Promise.reject(new Error('Khoảng thời gian không hợp lệ.')) },
              ]}><DatePicker showTime /></Form.Item>
            </>
            : <Form.Item name="anchor" label="Ngày đại diện (lịch UTC)" rules={[{ required: true }]}><DatePicker /></Form.Item>}
          <Form.Item label=" "><Button type="primary" htmlType="submit">Xem báo cáo</Button></Form.Item>
        </Space>
      </Form>
      <Typography.Paragraph>
        {periodMode === 'week' ? 'Tuần bắt đầu từ thứ Hai. ' : ''}
        Kỳ ngày/tuần/tháng lấy mốc 00:00 UTC; khoảng tùy chọn chuyển từ giờ địa phương sang UTC.
        API sử dụng khoảng [từ, đến), trong đó thời điểm đến không được tính.
      </Typography.Paragraph>
      {range && <Typography.Text>Kỳ đang áp dụng (UTC): {range.from} → {range.to}</Typography.Text>}
    </Card>}
    <Tabs activeKey={effectiveTab} onChange={changeTab} items={[
      ...(financial ? [
        { key: 'revenue', label: 'Doanh thu', children: <Space direction="vertical" style={{ width: '100%' }}>
          {!range && <Alert type="info" message="Chọn kỳ và nhấn “Xem báo cáo” để tải thống kê." />}
          <ReportError error={revenue.error} retry={revenue.refetch} />
          {revenue.data && !revenue.isError && <Row gutter={[16, 16]}>
            <Col xs={24} sm={8}><Card><Statistic title="Hóa đơn đã thanh toán" value={revenue.data.invoiceCount} /></Card></Col>
            <Col xs={24} sm={8}><Card><Statistic title="Doanh thu thuần (sau giảm)" value={revenue.data.totalRevenue} formatter={() => money(revenue.data!.totalRevenue)} /></Card></Col>
            <Col xs={24} sm={8}><Card><Statistic title="Tổng giảm giá" value={revenue.data.totalDiscount} formatter={() => money(revenue.data!.totalDiscount)} /></Card></Col>
          </Row>}
          <Typography.Text type="secondary">Chọn một ngày trong bảng để xem hóa đơn. Thanh biểu đồ thể hiện tỷ lệ doanh thu trong các ngày đang hiển thị.</Typography.Text>
          <Table<RevenueDay> rowKey="day" loading={revenue.isFetching} dataSource={revenue.isError ? [] : revenueRows} scroll={{ x: 700 }}
            onRow={record => ({ onClick: () => openDetails({ report: 'revenue', key: record.day, title: `Doanh thu ngày ${record.day}` }), style: { cursor: 'pointer' } })}
            pagination={{ current: dayPage, pageSize: daySize, total: revenue.data?.days.totalCount || 0, showSizeChanger: true,
              pageSizeOptions: [10, 20, 50, 100], onChange: (page, size) => { setDayPage(page); setDaySize(size) } }}
            columns={[
              { title: 'Ngày UTC', dataIndex: 'day' }, { title: 'Số hóa đơn', dataIndex: 'invoiceCount' },
              { title: 'Doanh thu thuần', dataIndex: 'revenue', render: (value: number) => <Space><Progress percent={maxDailyRevenue ? Math.round(value / maxDailyRevenue * 100) : 0} showInfo={false} style={{ width: 100 }} />{money(value)}</Space> },
              { title: 'Giảm giá', dataIndex: 'discount', render: money },
            ]} />
        </Space> },
        { key: 'top-items', label: 'Món bán chạy', children: <Space direction="vertical" style={{ width: '100%' }}>
          <Alert type="info" message="Xếp theo số lượng đã bán. Doanh thu gộp gồm topping, trước giảm giá cấp hóa đơn." />
          {!range && <Alert type="info" message="Chọn kỳ và nhấn “Xem báo cáo” để tải thống kê." />}
          <Input.Search maxLength={150} allowClear placeholder="Tìm tên món (lọc bảng và tệp xuất)" value={top.search} onChange={event => top.setSearch(event.target.value)} />
          <ReportError error={top.query.error} retry={top.query.refetch} />
          <Table<TopItem> rowKey="menuItemId" loading={top.query.isFetching} dataSource={top.query.isError ? [] : topRows} pagination={top.pagination} scroll={{ x: 700 }}
            onRow={record => ({ onClick: () => openDetails({ report: 'top-items', key: record.menuItemId, title: `Hóa đơn có món ${record.name}` }), style: { cursor: 'pointer' } })}
            columns={[
              { title: 'Món', dataIndex: 'name' },
              { title: 'Số lượng', dataIndex: 'quantity', render: (value: number) => <Space><Progress percent={maxItemQuantity ? Math.round(value / maxItemQuantity * 100) : 0} showInfo={false} style={{ width: 100 }} />{quantity(value)}</Space> },
              { title: 'Doanh thu gộp trước giảm', dataIndex: 'grossRevenue', render: money },
            ]} />
        </Space> },
        { key: 'payment-methods', label: 'Phương thức thanh toán', children: <Space direction="vertical" style={{ width: '100%' }}>
          {!range && <Alert type="info" message="Chọn kỳ và nhấn “Xem báo cáo” để tải thống kê." />}
          <ReportError error={methods.query.error} retry={methods.query.refetch} />
          <Table<PaymentReport> rowKey="paymentMethod" loading={methods.query.isFetching} dataSource={methods.query.isError ? [] : methodRows} pagination={methods.pagination} scroll={{ x: 700 }}
            onRow={record => ({ onClick: () => openDetails({ report: 'payment-methods', key: record.paymentMethod, title: `Hóa đơn thanh toán ${label(record.paymentMethod)}` }), style: { cursor: 'pointer' } })}
            columns={[
              { title: 'Phương thức', dataIndex: 'paymentMethod', render: label },
              { title: 'Số hóa đơn', dataIndex: 'invoiceCount' },
              { title: 'Doanh thu thuần', dataIndex: 'revenue', render: (value: number) => <Space><Progress percent={methodRevenue ? Math.round(value / methodRevenue * 100) : 0} showInfo={false} style={{ width: 100 }} />{money(value)}</Space> },
              { title: 'Tỷ trọng doanh thu', key: 'share', render: (_: unknown, row: PaymentReport) => `${methodRevenue ? (row.revenue / methodRevenue * 100).toFixed(1) : '0.0'}%` },
            ]} />
        </Space> },
      ] : []),
      { key: 'inventory', label: 'Tồn kho nguyên liệu', children: <Space direction="vertical" style={{ width: '100%' }}>
        <Alert type="info" message="Báo cáo tồn kho hiện tại, không phải số liệu lịch sử. Nguyên liệu dưới định mức được đánh dấu cảnh báo." />
        <Space wrap>
          <Select value={stockFilter} options={stockFilterOptions} onChange={(value: StockFilter) => { setStockFilter(value); inventory.resetPage() }} style={{ width: 240 }} />
          <Input.Search maxLength={150} placeholder="Tìm mã / tên (lọc bảng và tệp xuất)" value={inventory.search} onChange={event => inventory.setSearch(event.target.value)} allowClear style={{ width: 300 }} />
        </Space>
        <ReportError error={inventory.query.error} retry={inventory.query.refetch} />
        <Table<Ingredient> rowKey="id" loading={inventory.query.isFetching} dataSource={inventory.query.isError ? [] : inventory.query.data?.items} pagination={inventory.pagination} scroll={{ x: 950 }} columns={[
          { title: 'Mã', dataIndex: 'code' }, { title: 'Tên', dataIndex: 'name' }, { title: 'Đơn vị', dataIndex: 'unit' },
          { title: 'Tồn', dataIndex: 'quantity', render: quantity }, { title: 'Tối thiểu', dataIndex: 'minQuantity', render: quantity },
          { title: 'Hạn dùng UTC', dataIndex: 'expiryDate', render: (value: string | null) => value || 'Không có' },
          { title: 'Cảnh báo', key: 'warning', render: (_: unknown, row: Ingredient) => [
            row.isOutOfStock ? 'Hết hàng · cần nhập gấp' : row.isLowStock ? 'Bằng/dưới mức tối thiểu' : '',
            row.isExpired ? 'Đã hết hạn' : '', row.isExpiringSoon ? 'Hết hạn trong 7 ngày' : '',
          ].filter(Boolean).join(' · ') || 'Bình thường' },
        ]} />
      </Space> },
    ]} />

    <Modal title={reportDetails?.title || 'Hóa đơn trong báo cáo'} open={!!reportDetails} onCancel={() => setReportDetails(undefined)}
      footer={<Button onClick={() => setReportDetails(undefined)}>Đóng</Button>} width={900}>
      <ReportError error={detailQuery.error} retry={detailQuery.refetch} />
      <Table<ReportInvoice> rowKey="id" loading={detailQuery.isFetching} dataSource={detailQuery.data?.items} scroll={{ x: 700 }}
        onRow={record => ({ onClick: () => setInvoiceId(record.id), style: { cursor: 'pointer' } })}
        pagination={{ current: detailPage, pageSize: 10, total: detailQuery.data?.totalCount || 0,
          onChange: setDetailPage }}
        columns={[
          { title: 'Mã hóa đơn', dataIndex: 'code' }, { title: 'Thời gian lập', dataIndex: 'paidAt', render: dateTime },
          { title: 'Bàn', dataIndex: 'tableCode', render: value => value || 'Mang đi' },
          { title: 'Tổng thanh toán', dataIndex: 'totalAmount', render: money },
          { title: 'Phương thức', dataIndex: 'paymentMethod', render: label },
        ]} />
    </Modal>

    <Modal title="Chi tiết hóa đơn" open={!!invoiceId} onCancel={() => setInvoiceId(undefined)} footer={<Button onClick={() => setInvoiceId(undefined)}>Đóng</Button>} width={900}>
      <ReportError error={invoiceQuery.error} retry={invoiceQuery.refetch} />
      {invoiceQuery.data && <>
        <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }}>
          <Descriptions.Item label="Mã hóa đơn">{invoiceQuery.data.code}</Descriptions.Item>
          <Descriptions.Item label="Thời gian lập">{dateTime(invoiceQuery.data.paidAt)}</Descriptions.Item>
          <Descriptions.Item label="Bàn">{invoiceQuery.data.tableCode || 'Mang đi'}</Descriptions.Item>
          <Descriptions.Item label="Phương thức">{label(invoiceQuery.data.paymentMethod)}</Descriptions.Item>
          <Descriptions.Item label="Khuyến mãi">{invoiceQuery.data.promotionCode || 'Không áp dụng'}</Descriptions.Item>
          <Descriptions.Item label="Nhân viên lập">{invoiceQuery.data.cashierId}</Descriptions.Item>
          <Descriptions.Item label="Số tiền giảm">{money(invoiceQuery.data.discountAmount)}</Descriptions.Item>
          <Descriptions.Item label="Tổng thanh toán">{money(invoiceQuery.data.totalAmount)}</Descriptions.Item>
        </Descriptions>
        <Table<OrderItem> rowKey="id" style={{ marginTop: 16 }} dataSource={invoiceQuery.data.items} pagination={false} scroll={{ x: 700 }} columns={[
          { title: 'Món', dataIndex: 'menuItemName' }, { title: 'Số lượng', dataIndex: 'quantity', render: quantity },
          { title: 'Đơn giá', dataIndex: 'unitPrice', render: money }, { title: 'Thành tiền', dataIndex: 'lineTotal', render: money },
          { title: 'Trạng thái', dataIndex: 'status', render: label },
        ]} />
      </>}
    </Modal>
  </Space>
}
