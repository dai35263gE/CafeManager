import { describe, expect, it } from 'vitest'
import { allowed, billing, floor, kitchen, operations, stock, stockWrite } from './permissions'
import { label, money, options, roles } from '../utils/display'

describe('role permissions', () => {
  it('denies access when the role is unknown or not permitted', () => {
    expect(allowed(undefined, operations)).toBe(false)
    expect(allowed('Warehouse', operations)).toBe(false)
    expect(allowed('Manager', ['Admin'])).toBe(false)
  })

  it('grants Admin every business capability and role-restricted area', () => {
    for (const group of [operations, floor, kitchen, billing, stock, stockWrite, ...roles.map(role => [role])])
      expect(allowed('Admin', group)).toBe(true)
  })

  it('keeps payment, floor, kitchen and stock-write capabilities separated', () => {
    expect(allowed('Cashier', billing)).toBe(true)
    expect(allowed('Waiter', billing)).toBe(false)
    expect(allowed('Waiter', floor)).toBe(true)
    expect(allowed('Barista', floor)).toBe(false)
    expect(allowed('Barista', kitchen)).toBe(true)
    expect(allowed('Warehouse', stockWrite)).toBe(true)
    expect(allowed('Barista', stockWrite)).toBe(false)
  })

  it('lets Manager use every business area and defines the six expected roles', () => {
    for (const group of [operations, floor, kitchen, billing, stockWrite]) expect(group).toContain('Manager')
    expect([...roles].sort()).toEqual(['Admin', 'Barista', 'Cashier', 'Manager', 'Waiter', 'Warehouse'])
  })
})

describe('display helpers', () => {
  it('translates known enum values and keeps unknown values visible', () => {
    expect(label('TakeAway')).toBe('Mang đi')
    expect(label('SomethingNew')).toBe('SomethingNew')
    expect(options(['Cash'])).toEqual([{ value: 'Cash', label: 'Tiền mặt' }])
  })

  it('formats Vietnamese dong without losing the amount', () => {
    expect(money(25000)).toContain('25.000')
  })
})
