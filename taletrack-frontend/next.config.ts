import type { NextConfig } from "next";

// Where /api and /swagger are proxied to. The Docker image is built without INTERNAL_API_URL, so
// it falls back to the backend container; for `bun run dev` outside Docker set it to the local
// backend (e.g. http://localhost:8080/api).
const BACKEND_ORIGIN = (process.env.INTERNAL_API_URL ?? "http://backend:8080/api").replace(/\/api\/?$/, "");

const nextConfig: NextConfig = {
  turbopack: {},
  async rewrites() {
    return [
      {
        source: '/api/:path*',
        destination: `${BACKEND_ORIGIN}/api/:path*`,
      },
      {
        source: '/swagger/:path*',
        destination: `${BACKEND_ORIGIN}/swagger/:path*`,
      },
    ];
  },
  async headers() {
    return [
      {
        source: '/(.*)',
        headers: [
          { key: 'Cross-Origin-Opener-Policy', value: 'same-origin-allow-popups' },
        ],
      },
    ];
  },
  webpack: (config, { dev }) => {
    if (dev) {
      config.watchOptions = { poll: 500, aggregateTimeout: 300 };
    }
    return config;
  },
};

export default nextConfig;
