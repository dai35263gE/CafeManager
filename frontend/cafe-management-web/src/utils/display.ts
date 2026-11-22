export const roles = ['Admin', 'Manager', 'Waiter', 'Barista', 'Cashier', 'Warehouse'] as const
export const labels: Record<string, string> = {
  Admin: 'Quản trị hệ thống', Manager: 'Quản lý', Waiter: 'Phục vụ', Barista: 'Pha chế', Cashier: 'Thu ngân', Warehouse: 'Thủ kho',
  Available: 'Sẵn sàng', InUse: 'Đang sử dụng', NeedsCleaning: 'Cần vệ sinh', Disabled: 'Ngưng sử dụng',
  Open: 'Nháp', Confirmed: 'Đã xác nhận', Preparing: 'Đang pha chế', Served: 'Đã phục vụ', Paid: 'Đã thanh toán', Cancelled: 'Đã hủy',
  Waiting: 'Chờ pha chế', Completed: 'Pha xong', OutOfStock: 'Hết hàng', Discontinued: 'Ngừng bán',
  Active: 'Đang áp dụng', Inactive: 'Ngừng áp dụng', Expired: 'Hết hạn', Working: 'Đang làm', Resigned: 'Đã nghỉ',
  Scheduled: 'Sắp diễn ra', Ended: 'Đã kết thúc',
  DineIn: 'Tại quán', TakeAway: 'Mang đi', Cash: 'Tiền mặt', BankTransfer: 'Chuyển khoản', Card: 'Thẻ', EWallet: 'Ví điện tử',
  Percentage: 'Phần trăm', FixedAmount: 'Số tiền', Import: 'Nhập kho', Export: 'Xuất kho', Adjust: 'Điều chỉnh', Stocktake: 'Kiểm kê',
  revenue: 'Thống kê doanh thu', 'top-items': 'Thống kê món bán chạy',
  'payment-methods': 'Doanh thu theo phương thức thanh toán', inventory: 'Thống kê tồn kho nguyên liệu',
}
export const label = (value: string) => labels[value] || value
export const options = (values: readonly string[]) => values.map(value => ({ value, label: label(value) }))
export const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 2 }).format(value)
export const dateTime = (value?: string | null) => value ? new Date(value).toLocaleString('vi-VN') : '—'
export const quantity = (value: number) => new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 3 }).format(value)
