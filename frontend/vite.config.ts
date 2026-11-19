import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';

// Khi chạy dev (npm run dev), /api được chuyển tới API ở cổng 5080.
// Khi chạy bằng Docker, NGINX làm việc này (Buổi 8).
const API = process.env.API_URL ?? 'http://localhost:5080';
export default defineConfig({
  plugins: [react()],
  // '@/...' = thư mục src: import sang module khác / shared / app viết đường dẫn tuyệt đối cho dễ đọc
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  // API_URL: đổi đích proxy, ví dụ API_URL=http://localhost:8080 để dùng backend đang chạy trong Docker.
  server: { proxy: { '/api': API, '/hub': { target: API, ws: true } } },
});
