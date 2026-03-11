import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import fs from 'fs'

const keyPath = './desktop-ecbaunu.tailca9ff1.ts.net.key'
const certPath = './desktop-ecbaunu.tailca9ff1.ts.net.crt'
const hasSSL = fs.existsSync(keyPath) && fs.existsSync(certPath)

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    host: '0.0.0.0',
    port: 5200,
    ...(hasSSL ? {
      https: {
        key: fs.readFileSync(keyPath),
        cert: fs.readFileSync(certPath),
      },
      allowedHosts: ['desktop-ecbaunu.tailca9ff1.ts.net'],
    } : {}),
    proxy: {
      '/api': {
        target: 'http://localhost:52873',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:52873',
        changeOrigin: true,
        ws: true,
      },
    },
  },
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
  },
})
