import type { Role } from '../api/types'
export const operations: Role[] = ['Manager', 'Waiter', 'Barista', 'Cashier']
export const floor: Role[] = ['Manager', 'Waiter']
export const kitchen: Role[] = ['Manager', 'Barista']
export const billing: Role[] = ['Manager', 'Cashier']
export const stock: Role[] = ['Manager', 'Warehouse', 'Barista']
export const stockWrite: Role[] = ['Manager', 'Warehouse']
export function allowed(role: Role | undefined, permitted: readonly Role[]): boolean { return role !== undefined && permitted.includes(role) }
