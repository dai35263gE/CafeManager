import { Controller, useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Alert, Button, Card, Form, Input, Typography } from 'antd'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { errorText } from '../api/client'

const schema = z.object({
  username: z.string().trim().min(1, 'Nhập tên đăng nhập.').max(100, 'Tối đa 100 ký tự.'),
  password: z.string().min(1, 'Nhập mật khẩu.').max(128, 'Tối đa 128 ký tự.'),
})
type Fields = z.infer<typeof schema>
export function loginDestination(state: unknown): string {
  const from = state && typeof state === 'object' && 'from' in state ? state.from : null
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') && !from.startsWith('/login') ? from : '/'
}
export default function LoginPage() {
  const { user, loading, login } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const { control, handleSubmit, formState: { errors, isSubmitting } } = useForm<Fields>({ resolver: zodResolver(schema), defaultValues: { username: '', password: '' } })
  const destination = loginDestination(location.state)
  if (user) return <Navigate to={destination} replace />
  const submit = handleSubmit(async fields => {
    setError(null)
    try { await login(fields.username, fields.password); navigate(destination, { replace: true }) }
    catch (failure) { setError(errorText(failure)) }
  })
  return <main className="login-screen"><Card className="login-card">
    <Typography.Title level={2}>Quản lý quán cà phê</Typography.Title>
    <Typography.Paragraph type="secondary">Đăng nhập bằng tài khoản được cấp. Quyền truy cập phụ thuộc vai trò.</Typography.Paragraph>
    {error && <Alert type="error" showIcon message={error} className="query-error" />}
    <Form layout="vertical" onFinish={() => { void submit() }}>
      <Form.Item label="Tên đăng nhập" required validateStatus={errors.username && 'error'} help={errors.username?.message}>
        <Controller name="username" control={control} render={({ field }) => <Input {...field} id="username" autoComplete="username" maxLength={100} aria-label="Tên đăng nhập" />} />
      </Form.Item>
      <Form.Item label="Mật khẩu" required validateStatus={errors.password && 'error'} help={errors.password?.message}>
        <Controller name="password" control={control} render={({ field }) => <Input.Password {...field} id="password" autoComplete="current-password" maxLength={128} aria-label="Mật khẩu" />} />
      </Form.Item>
      <Button type="primary" htmlType="submit" block loading={isSubmitting || loading}>Đăng nhập</Button>
    </Form>
  </Card></main>
}
