import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // Keeps the browser on one origin during development, so CORS and the
      // SameSite=Strict refresh cookie behave exactly as they will in
      // production rather than needing dev-only exceptions.
      '/api': {
        target: 'https://localhost:7167',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
