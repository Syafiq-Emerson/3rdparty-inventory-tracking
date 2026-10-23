/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';

const rootDir = path.dirname(fileURLToPath(import.meta.url));

// Where the ASP.NET Core API listens in development. Kestrel's default is 5000.
const apiTarget = process.env.VITE_API_TARGET ?? 'http://localhost:5000';

export default defineConfig(() => {
  return {
    plugins: [react(), tailwindcss()],
    define: {
      VITE_BUILD_DATE: JSON.stringify(new Date().toISOString()),
    },
    resolve: {
      alias: {
        '@': path.resolve(rootDir, '.'),
      },
    },
    build: {
      rolldownOptions: {
        output: {
          // Vendor code changes less often than the app, so it stays cached across releases.
          codeSplitting: {
            groups: [
              { name: 'react', test: /node_modules[\\/](react|react-dom|scheduler)[\\/]/, priority: 20 },
              { name: 'icons', test: /node_modules[\\/]lucide-react[\\/]/, priority: 10 },
            ],
          },
        },
      },
    },
    server: {
      // Proxying keeps API calls same-origin, so the auth cookie works and CORS is not needed.
      proxy: {
        '^/api': {
          target: apiTarget,
          changeOrigin: true,
          secure: false,
        },
      },
    },
    test: {
      include: ['src/**/*.test.ts'],
      environment: 'node',
    },
  };
});
