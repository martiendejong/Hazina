import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import fs from 'fs'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    host: '0.0.0.0',  // Listen on all network interfaces (allows access from other devices)
    port: 5173,
    https: {
      key: fs.readFileSync('./desktop-ecbaunu.tailca9ff1.ts.net.key'),
      cert: fs.readFileSync('./desktop-ecbaunu.tailca9ff1.ts.net.crt'),
    },
    allowedHosts: ['desktop-ecbaunu.tailca9ff1.ts.net'],
    proxy: {
      '/api': {
        target: 'http://localhost:52871',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:52871',
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
