import { Alert, Button, Space, Tag, Typography } from 'antd'
import { errorText } from '../api/client'
import { label } from '../utils/display'
import type { ReactNode } from 'react'

export function PageTitle({ title, extra }: { title: string; extra?: ReactNode }) {
  return <div className="page-title"><Typography.Title level={2}>{title}</Typography.Title><Space wrap>{extra}</Space></div>
}
export function QueryError({ error, retry }: { error: unknown; retry?: () => unknown }) {
  return error ? <Alert className="query-error" type="error" showIcon message={errorText(error)} action={retry && <Button onClick={() => { retry() }}>Tải lại</Button>} /> : null
}
export function StatusTag({ value }: { value: string }) {
  const color = ['Cancelled', 'Resigned', 'Expired', 'Ended', 'OutOfStock'].includes(value) ? 'red'
    : ['Paid', 'Available', 'Completed', 'Working', 'Active'].includes(value) ? 'green'
    : ['NeedsCleaning', 'Preparing', 'Waiting', 'Inactive'].includes(value) ? 'orange' : 'blue'
  return <Tag color={color}>{label(value)}</Tag>
}
