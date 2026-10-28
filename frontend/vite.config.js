import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const API_BASE_URL = 'http://localhost:5022'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: API_BASE_URL,
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
