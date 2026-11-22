import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => ({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: loadEnv(mode, process.cwd(), '').VITE_API_PROXY_TARGET || 'http://localhost:5080', changeOrigin: true },
    },
  },
}))
