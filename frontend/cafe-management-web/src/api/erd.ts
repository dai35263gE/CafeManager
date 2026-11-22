import type { EmployeeStatus, ItemStatus, MenuStatus, OrderStatus, TableStatus } from './types'

export interface NhanVien { maNhanVien: string; hoTen: string; viTri: string; thongTinLienHe: string | null; trangThai: EmployeeStatus }
export interface TaiKhoan { tenDangNhap: string; maNhanVien: string | null; maVaiTro: string; tenVaiTro: string; trangThai: string; thoiGianDangNhap: string | null }
export interface Ban { maBan: string; khuVuc: string; trangThai: TableStatus; ghiChu: string | null }
export interface DanhMuc { maDanhMuc: string; tenDanhMuc: string }
export interface KichCo { maKichCo: string; tenKichCo: string; giaBan: number }
export interface Topping { maTopping: string; tenTopping: string; giaBan: number }
export interface Mon {
  maMon: string; maDanhMuc: string; tenDanhMuc: string; tenMon: string; moTa: string | null
  hinhAnh: string | null; trangThai: MenuStatus; kichCos: KichCo[]; toppings: Topping[]
}
export interface ChiTietDonHang {
  maChiTiet: string; maDon: string; maMon: string; maKichCo: string | null; soLuong: number
  donGia: number; ghiChu: string | null; trangThaiMon: ItemStatus; maToppings: string[]
}
export interface DonHang {
  maDon: string; maBan: string | null; maNhanVien: string; loaiDon: 'DineIn' | 'TakeAway'
  thoiGianTao: string; trangThai: OrderStatus; tamTinh: number; chiTiets: ChiTietDonHang[]
}
