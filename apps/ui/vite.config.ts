import path from 'path';
import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => {
    const env = loadEnv(mode, '.', '');
    
    // Aspire service discovery: Check for API endpoint environment variables
    // Prefer HTTP for local development, fallback to HTTPS, then localhost
    const apiUrl = 
        process.env.services__api__http__0 ||  // .NET Aspire format (HTTP)
        process.env.api_http ||                 // JavaScript format (HTTP)
        process.env.services__api__https__0 ||  // .NET Aspire format (HTTPS)
        process.env.api_https ||                // JavaScript format (HTTPS)
        'http://localhost:5075';                // Local fallback
    
    return {
      server: {
        port: process.env.PORT ? parseInt(process.env.PORT) : 3000,
        host: '0.0.0.0',
      },
      plugins: [react()],
      define: {
        'import.meta.env.VITE_API_URL': JSON.stringify(apiUrl),
        'process.env.API_KEY': JSON.stringify(env.SUMMARIZATION_API_KEY),
        'process.env.SUMMARIZATION_API_KEY': JSON.stringify(env.SUMMARIZATION_API_KEY)
      },
      resolve: {
        alias: {
          '@': path.resolve(__dirname, '.'),
        }
      }
    };
});
