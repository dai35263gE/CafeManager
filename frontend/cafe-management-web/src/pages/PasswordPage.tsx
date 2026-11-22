import { Button, Card, Form, Input, Alert } from 'antd'
import { send } from '../api/client'
import { useAction } from '../api/hooks'
import { useAuth } from '../auth/AuthContext'
import { PageTitle } from '../components/Common'
interface Fields { currentPassword: string; newPassword: string; confirm: string }
export default function PasswordPage() {
  const [form] = Form.useForm<Fields>()
  const { logout } = useAuth()
  const action = useAction()
  return <><PageTitle title="Đổi mật khẩu" /><Card style={{ maxWidth: 520 }}>
    <Alert type="info" showIcon message="Sau khi đổi mật khẩu, tất cả phiên cũ bị vô hiệu. Bạn cần đăng nhập lại." />
    <Form form={form} layout="vertical" onFinish={async values => {
      if (await action.run(() => send('post', '/auth/change-password', { currentPassword: values.currentPassword, newPassword: values.newPassword }))) logout()
    }}>
      <Form.Item name="currentPassword" label="Mật khẩu hiện tại" rules={[{ required: true, max: 128 }]}><Input.Password autoComplete="current-password" /></Form.Item>
      <Form.Item name="newPassword" label="Mật khẩu mới" rules={[{ required: true, min: 8, max: 128 }]}><Input.Password autoComplete="new-password" /></Form.Item>
      <Form.Item name="confirm" label="Nhập lại mật khẩu mới" dependencies={['newPassword']} rules={[{ required: true }, ({ getFieldValue }) => ({
        validator: (_, value: string) => value === getFieldValue('newPassword') ? Promise.resolve() : Promise.reject(new Error('Mật khẩu không khớp.')),
      })]}><Input.Password autoComplete="new-password" /></Form.Item>
      <Button htmlType="submit" type="primary" loading={action.busy}>Đổi mật khẩu</Button>
    </Form>
  </Card></>
}
