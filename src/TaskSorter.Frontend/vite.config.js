import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
export default defineConfig(({ mode }) => {
    const env = loadEnv(mode, process.cwd(), '');
    const target = env.VITE_API_PROXY_TARGET || 'http://localhost:5111';
    return {
        plugins: [react()],
        server: {
            port: 5173,
            proxy: {
                '/api': {
                    target,
                    changeOrigin: true,
                },
                '/health': {
                    target,
                    changeOrigin: true,
                },
            },
        },
        test: {
            environment: 'jsdom',
            globals: true,
        },
    };
});
