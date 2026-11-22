import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { App } from 'antd'
import { useQueryClient } from '@tanstack/react-query'
import { errorText, get, registerUnauthorized, send, setAccessToken } from '../api/client'
import type { LoginResponse, User } from '../api/types'

const key = 'cafe.accessToken'
interface Auth {
  user: User | null; loading: boolean; error: string | null
  login: (username: string, password: string) => Promise<void>; logout: () => void; retry: () => void
}
const Context = createContext<Auth | null>(null)
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const cache = useQueryClient()
  const { message } = App.useApp()
  const generation = useRef(0)
  const logout = useCallback(() => {
    generation.current++
    setAccessToken(null)
    sessionStorage.removeItem(key)
    setUser(null)
    setError(null)
    setLoading(false)
    cache.clear()
  }, [cache])
  const restore = useCallback(async () => {
    const current = ++generation.current
    const saved = sessionStorage.getItem(key)
    setError(null)
    if (!saved) { setLoading(false); return }
    setAccessToken(saved)
    setLoading(true)
    try {
      const profile = await get<User>('/auth/me')
      if (current === generation.current) setUser(profile)
    } catch (failure) {
      if (current === generation.current) setError(errorText(failure))
    } finally { if (current === generation.current) setLoading(false) }
  }, [])
  useEffect(() => {
    registerUnauthorized(() => { logout(); message.warning('Phiên đăng nhập hết hạn hoặc quyền đã thay đổi. Vui lòng đăng nhập lại.') })
    void restore()
    return () => { generation.current++; registerUnauthorized() }
  }, [logout, message, restore])
  async function login(username: string, password: string) {
    const response = await send<LoginResponse>('post', '/auth/login', { username, password })
    generation.current++
    cache.clear()
    setAccessToken(response.token)
    sessionStorage.setItem(key, response.token)
    setUser(response.user)
    setError(null)
    setLoading(false)
  }
  return <Context.Provider value={{ user, loading, error, login, logout, retry: () => { void restore() } }}>{children}</Context.Provider>
}
export function useAuth() {
  const auth = useContext(Context)
  if (!auth) throw new Error('AuthProvider chưa được khởi tạo.')
  return auth
}
