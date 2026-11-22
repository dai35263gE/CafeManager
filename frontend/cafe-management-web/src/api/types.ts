export type Role = 'Admin' | 'Manager' | 'Waiter' | 'Barista' | 'Cashier' | 'Warehouse'
export type TableStatus = 'Available' | 'InUse' | 'NeedsCleaning' | 'Disabled'
export type OrderStatus = 'Open' | 'Confirmed' | 'Preparing' | 'Served' | 'Paid' | 'Cancelled'
export type ItemStatus = 'Waiting' | 'Preparing' | 'Completed' | 'Served' | 'Cancelled'
export type MenuStatus = 'Available' | 'OutOfStock' | 'Discontinued'
export type PaymentMethod = 'Cash' | 'BankTransfer' | 'Card' | 'EWallet'
export type PromotionStatus = 'Active' | 'Inactive' | 'Expired'
export type PromotionType = 'Percentage' | 'FixedAmount'
export type EmployeeStatus = 'Working' | 'Resigned'
export type InventoryType = 'Import' | 'Export' | 'Adjust' | 'Stocktake'
export interface Envelope<T> { success: boolean; message: string; data: T; errors?: Record<string, string[]> | null }
export interface Page<T> { items: T[]; totalCount: number; page: number; pageSize: number }
export interface User { id: string; username: string; fullName: string; role: Role; employeeId: string | null; isActive: boolean; isLocked: boolean; lastLoginAt: string | null }
export interface LoginResponse { token: string; expiresAt: string; user: User }
export interface ActiveTableOrder { id: string; code: string; status: OrderStatus; rowVersion: string }
export interface Table {
  id: string; code: string; area: string; status: TableStatus; note: string | null
  rowVersion: string; activeOrder: ActiveTableOrder | null
}
export interface Category { id: string; name: string; description: string | null; isActive: boolean }
export interface Size { id: string; name: string; priceDelta: number; isActive: boolean }
export interface Topping { id: string; name: string; price: number; isActive: boolean }
export interface MenuSize { sizeId: string; name: string; price: number; isActive: boolean }
export interface MenuItem {
  id: string; categoryId: string; categoryName: string; name: string; description: string | null
  imageUrl: string | null; basePrice: number; status: MenuStatus; isActive: boolean
  sizes: MenuSize[]; toppings: Topping[]; rowVersion: string
}
export interface OrderTopping { toppingId: string; name: string; quantity: number; unitPrice: number }
export interface OrderItem {
  id: string; menuItemId: string; menuItemName: string; sizeId: string | null; sizeName: string | null
  quantity: number; unitPrice: number; lineTotal: number; note: string | null; status: ItemStatus
  preparedByUserId: string | null; rowVersion: string; toppings: OrderTopping[]
}
export interface Order {
  id: string; code: string; type: 'DineIn' | 'TakeAway'; tableId: string | null; tableCode: string | null
  status: OrderStatus; createdByUserId: string; createdAt: string; note: string | null; subTotal: number
  rowVersion: string; items: OrderItem[]
}
export interface NewOrderItem { menuItemId: string; sizeId?: string | null; quantity: number; note?: string; toppings: { toppingId: string; quantity: number }[] }
export interface KitchenItem { orderId: string; orderCode: string; tableId: string | null; tableCode: string | null; orderNote: string | null; orderStatus: OrderStatus; createdAt: string; item: OrderItem }
export interface KitchenNotice { id: string; userId: string | null; entityId: string | null; description: string; isRead: boolean; createdAt: string }
export interface Promotion {
  id: string; code: string; name: string; startAt: string; endAt: string; type: PromotionType
  discountValue: number; maxDiscount: number | null; minOrderAmount: number; status: PromotionStatus
  conditionDescription: string | null; effectiveStatus: 'Scheduled' | 'Active' | 'Inactive' | 'Ended'
}
export interface PromotionUsageSummary { usageCount: number; totalDiscountAmount: number }
export interface PromotionPreview {
  promotionId: string; code: string; subTotal: number; vatAmount: number; serviceFeeAmount: number
  discountAmount: number; totalAmount: number; warnings: string[]
}
export interface InvoiceQuote {
  orderId: string; rowVersion: string; promotionId: string | null; promotionCode: string | null
  subTotal: number; vatRatePercent: number; vatAmount: number; serviceFeeRatePercent: number
  serviceFeeAmount: number; discountAmount: number; totalAmount: number; warnings: string[]
}
export interface Invoice {
  id: string; code: string; orderId: string; orderCode: string; tableId: string | null; tableCode: string | null
  promotionId: string | null; promotionCode: string | null; subTotal: number; vatAmount: number
  serviceFeeAmount: number; discountAmount: number
  totalAmount: number; paymentMethod: PaymentMethod; receivedAmount: number; changeAmount: number; cashierId: string
  paidAt: string; status: 'Paid' | 'Cancelled'; cancelReason: string | null; cancelledByUserId: string | null
  cancelledAt: string | null; rowVersion: string; items: OrderItem[]
}
export interface Ingredient {
  id: string; code: string; name: string; unit: string; quantity: number; expiryDate: string | null
  minQuantity: number; isLowStock: boolean; isOutOfStock: boolean; isExpired: boolean; isExpiringSoon: boolean; rowVersion: string
}
export interface InventoryTransaction {
  id: string; ingredientId: string; type: InventoryType; quantity: number; previousQuantity: number
  resultingQuantity: number; reason: string; performedByUserId: string; createdAt: string
}
export interface InventoryMovement { ingredient: Ingredient; transaction: InventoryTransaction }
export interface Recipe {
  maDinhLuong: string; maMon: string; tenMon: string; maNguyenLieu: string
  tenNguyenLieu: string; donViTinh: string; soLuongSuDung: number
}
export interface Employee {
  id: string; code: string; fullName: string; position: string; phone: string | null; email: string | null
  status: EmployeeStatus; userId: string | null; accountIsActive: boolean | null; accountIsLocked: boolean | null
}
export interface RevenueDay { day: string; invoiceCount: number; revenue: number; discount: number }
export interface RevenueReport { from: string; to: string; invoiceCount: number; totalRevenue: number; totalDiscount: number; days: Page<RevenueDay> }
export interface TopItem { menuItemId: string; name: string; quantity: number; grossRevenue: number }
export interface PaymentReport { paymentMethod: PaymentMethod; invoiceCount: number; revenue: number }
export interface ReportInvoice {
  id: string; code: string; tableCode: string | null; paidAt: string; totalAmount: number; paymentMethod: PaymentMethod
}
