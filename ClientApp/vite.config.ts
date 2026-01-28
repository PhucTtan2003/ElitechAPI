import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react-swc'
import { resolve } from 'path'

export default defineConfig({
  plugins: [react()],
  root: '.',                      // ClientApp
    build: {
    outDir: '../wwwroot/app',     // xuất qua wwwroot/app
    emptyOutDir: true,
    rollupOptions: {
      // 2 entry riêng cho Admin/User => tên file cố định
      input: {
        admin: resolve(__dirname, 'src/entries/admin.tsx'),
        user: resolve(__dirname, 'src/entries/user.tsx')
      },
      output: {
        entryFileNames: 'entry-[name].js',     // => entry-admin.js, entry-user.js
        
        chunkFileNames: "chunk-[name]-[hash].js", 
        assetFileNames: 'asset-[name][extname]'
      }
    }
  },
  server: {
    port: 5173,
    strictPort: true,
    // Proxy về ASP.NET khi dev (nếu cần gọi /api từ Vite dev server)
    proxy: {
      '/api': 'https://localhost:5001'
    }
  }
})
