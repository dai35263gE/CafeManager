import { Alert } from 'antd'
import { PageTitle } from '../components/Common'

export default function ContractPendingPage({ title }: { title: string }) {
  return <><PageTitle title={title} /><Alert type="warning" showIcon message="Chức năng tạm thời chưa khả dụng"
    description="Hợp đồng API chi tiết cho chức năng này chưa được cung cấp. Các thao tác cũ đã được tạm ngưng để tránh gửi dữ liệu không đúng." /></>
}
