'use client';

import { createContext, useContext, useSyncExternalStore } from 'react';

type Theme = 'light' | 'dark';

interface ThemeContextType {
  theme: Theme;
  toggleTheme: () => void;
}

const ThemeContext = createContext<ThemeContextType>({
  theme: 'light',
  toggleTheme: () => {},
});

// --- External store (mirrors lib/auth-context.tsx's pattern) ---
// localStorage is a client-only source of truth, so reading it during the render
// that produces the SSR/hydration output would make that render disagree with the
// server's (always 'light') and trigger a hydration mismatch. useSyncExternalStore
// is built exactly for this: it returns `light` (via getServerSnapshot) for the
// server render and the hydrating client render, then swaps to the real value
// right after, with no manual effect/setState needed.

const themeListeners = new Set<() => void>();

function notifyThemeChange() {
  themeListeners.forEach((fn) => fn());
}

function subscribeTheme(callback: () => void) {
  themeListeners.add(callback);
  return () => themeListeners.delete(callback);
}

function getThemeSnapshot(): Theme {
  return localStorage.getItem('tt-theme') === 'dark' ? 'dark' : 'light';
}

function getServerThemeSnapshot(): Theme {
  return 'light';
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const theme = useSyncExternalStore(subscribeTheme, getThemeSnapshot, getServerThemeSnapshot);

  const toggleTheme = () => {
    const next: Theme = getThemeSnapshot() === 'dark' ? 'light' : 'dark';
    localStorage.setItem('tt-theme', next);
    document.documentElement.classList.toggle('dark', next === 'dark');
    notifyThemeChange();
  };

  return (
    <ThemeContext.Provider value={{ theme, toggleTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}

export const useTheme = () => useContext(ThemeContext);
