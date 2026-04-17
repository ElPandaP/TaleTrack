export const API_CONFIG = {
  baseURL: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080/api',
  internalApiKey: process.env.NEXT_PUBLIC_INTERNAL_API_KEY || '',
  googleClientId: process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID || '',
};
