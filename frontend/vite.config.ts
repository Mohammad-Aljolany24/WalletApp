import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    // MUI + React + Router in one chunk is ~500 kB minified.
    // Real code-splitting lands in Phase 2.5 once SignalR + Recharts are added.
    chunkSizeWarningLimit: 1000,
  },
})