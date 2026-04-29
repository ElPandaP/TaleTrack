import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  turbopack: {},
  async rewrites() {
    return [
      {
        source: '/api/:path*',
        destination: 'http://backend:8080/api/:path*',
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
  images: {
    remotePatterns: [
      { protocol: 'https', hostname: '**' },
      { protocol: 'http',  hostname: '**' },
    ],
  },
  webpack: (config, { dev }) => {
    if (dev) {
      config.watchOptions = { poll: 500, aggregateTimeout: 300 };
    }
    return config;
  },
};

export default nextConfig;
