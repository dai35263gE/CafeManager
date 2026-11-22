import { App as AntApp, ConfigProvider } from 'antd'
import viVN from 'antd/locale/vi_VN'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { ProtectedRoute, RoleGuard } from './auth/Guards'
import { billing, operations, stock, stockWrite } from './auth/permissions'
import Shell from './components/Shell'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import PasswordPage from './pages/PasswordPage'
import TablesPage from './pages/TablesPage'
import CatalogPage from './pages/CatalogPage'
import OrdersPage from './pages/OrdersPage'
import EmployeesPage from './pages/EmployeesPage'
import UsersPage from './pages/UsersPage'
import KitchenPage from './pages/KitchenPage'
import InvoicesPage from './pages/InvoicesPage'
import PromotionsPage from './pages/PromotionsPage'
import InventoryPage from './pages/InventoryPage'
import RecipesPage from './pages/RecipesPage'
import ReportsPage from './pages/ReportsPage'
import { Result } from 'antd'

const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 15000, refetchOnWindowFocus: true }, mutations: { retry: false } } })
export default function App() {
  return <ConfigProvider locale={viVN} theme={{ token: { colorPrimary: '#79513b', borderRadius: 8, fontFamily: 'system-ui, sans-serif' } }}>
    <AntApp><QueryClientProvider client={client}><BrowserRouter><AuthProvider><Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}><Route element={<Shell />}>
        <Route index element={<DashboardPage />} />
        <Route path="/password" element={<PasswordPage />} />
        <Route element={<RoleGuard roles={operations} />}>
          <Route path="/tables" element={<TablesPage />} />
          <Route path="/catalog" element={<CatalogPage />} />
          <Route path="/orders" element={<OrdersPage />} />
        </Route>
        <Route element={<RoleGuard roles={['Manager', 'Waiter', 'Barista', 'Cashier']} />}><Route path="/kitchen" element={<KitchenPage />} /></Route>
        <Route element={<RoleGuard roles={billing} />}>
          <Route path="/invoices" element={<InvoicesPage />} />
          <Route path="/promotions" element={<PromotionsPage />} />
        </Route>
        <Route element={<RoleGuard roles={stock} />}><Route path="/inventory" element={<InventoryPage />} /></Route>
        <Route element={<RoleGuard roles={['Manager']} />}><Route path="/recipes" element={<RecipesPage />} /></Route>
        <Route element={<RoleGuard roles={['Manager', 'Admin']} />}><Route path="/employees" element={<EmployeesPage />} /></Route>
        <Route element={<RoleGuard roles={stockWrite} />}><Route path="/reports" element={<ReportsPage />} /></Route>
        <Route element={<RoleGuard roles={['Admin']} />}><Route path="/users" element={<UsersPage />} /></Route>
        <Route path="*" element={<Result status="404" title="Không tìm thấy trang" />} />
      </Route></Route>
    </Routes></AuthProvider></BrowserRouter></QueryClientProvider></AntApp>
  </ConfigProvider>
}
