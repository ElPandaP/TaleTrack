import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  turbopack: {},
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
