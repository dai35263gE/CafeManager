import { Button, Form, Input, InputNumber, Select, Space, Typography } from 'antd'
import { useOptions } from '../api/hooks'
import type { MenuItem, NewOrderItem } from '../api/types'
import { QueryError } from './Common'
import { money } from '../utils/display'
export default function OrderItemForm({ onSubmit, busy }: { onSubmit: (item: NewOrderItem) => Promise<boolean>; busy: boolean }) {
  const menu = useOptions<MenuItem>('/mon', { orderableOnly: true })
  const [form] = Form.useForm<NewOrderItem>()
  const menuId: string | undefined = Form.useWatch('menuItemId', form)
  const sizeId: string | null | undefined = Form.useWatch('sizeId', form)
  const selected = menu.data?.find(item => item.id === menuId)
  const sizes = selected?.sizes.filter(size => size.isActive) || []
  const toppings = selected?.toppings.filter(item => item.isActive) || []
  const price = sizes.length ? sizes.find(size => size.sizeId === sizeId)?.price : selected?.basePrice
  return <><QueryError error={menu.error} retry={menu.refetch} /><Form form={form} layout="vertical" initialValues={{ quantity: 1, toppings: [] }}
    onValuesChange={values => { if ('menuItemId' in values) form.setFieldsValue({ sizeId: undefined, toppings: [] }) }}
    onFinish={async values => { if (await onSubmit({ ...values, sizeId: values.sizeId || null, toppings: values.toppings || [] })) form.resetFields() }}>
    <Form.Item name="menuItemId" label="Món" rules={[{ required: true, message: 'Chọn món.' }]}>
      <Select showSearch optionFilterProp="label" loading={menu.isLoading} options={menu.data?.map(item => ({ value: item.id, label: `${item.name} · ${money(item.basePrice)}` }))} />
    </Form.Item>
    {sizes.length > 0 && <Form.Item name="sizeId" label="Size (giá đầy đủ)" rules={[{ required: true, message: 'Chọn size.' }]}>
      <Select options={sizes.map(size => ({ value: size.sizeId, label: `${size.name} · ${money(size.price)}` }))} />
    </Form.Item>}
    <Form.Item name="quantity" label="Số ly" rules={[{ required: true }, { type: 'integer', min: 1, max: 100 }]}><InputNumber min={1} max={100} precision={0} /></Form.Item>
    <Form.List name="toppings">{(fields, { add, remove }) => <><Typography.Text>Topping (số phần trên mỗi ly)</Typography.Text>
      {fields.map(field => <Space key={field.key} align="baseline" wrap>
        <Form.Item name={[field.name, 'toppingId']} rules={[{ required: true }]}><Select style={{ width: 230 }} options={toppings.map(topping => ({ value: topping.id, label: `${topping.name} · ${money(topping.price)}` }))} /></Form.Item>
        <Form.Item name={[field.name, 'quantity']} rules={[{ required: true }, { type: 'integer', min: 1, max: 20 }]}><InputNumber aria-label="Số phần topping" min={1} max={20} precision={0} /></Form.Item>
        <Button danger onClick={() => remove(field.name)}>Bỏ</Button>
      </Space>)}
      <Form.Item><Button disabled={!toppings.length || fields.length >= toppings.length} onClick={() => add({ quantity: 1 })}>Thêm topping</Button></Form.Item>
    </>}</Form.List>
    <Form.Item name="note" label="Ghi chú món" rules={[{ max: 2000 }]}><Input.TextArea maxLength={2000} placeholder="Ít đá, ít đường..." /></Form.Item>
    {price !== undefined && <Typography.Paragraph type="secondary">Giá món/size: {money(price)}. Topping tính thêm; giá cuối cùng do server lưu snapshot. Không chọn trùng topping.</Typography.Paragraph>}
    <Button htmlType="submit" type="primary" loading={busy} disabled={!selected || menu.isError}>Thêm vào đơn</Button>
  </Form></>
}
