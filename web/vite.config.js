import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// The API serves the built app from its wwwroot - one deployable unit.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../src/Khan.Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: { '/api': 'http://127.0.0.1:5199' },
  },
})
