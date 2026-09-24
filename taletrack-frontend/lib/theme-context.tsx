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

// External store (same pattern as lib/auth-context.tsx): getServerSnapshot keeps
// the hydration render at 'light' so it matches the server, then useSyncExternalStore
// swaps in the real localStorage value right after — no hydration mismatch.

const themeListeners = new Set<() => void>();

function notifyThemeChange() {
  themeListeners.forEach((fn) => fn());
}

function subscribeTheme(callback: () => void) {
  themeListeners.add(callback);
  // A toggle in another tab only reaches this one through the storage event,
  // so the <html> class has to be synced here too, not just the React state.
  const onStorage = (e: StorageEvent) => {
    if (e.key !== null && e.key !== 'tt-theme') return;
    document.documentElement.classList.toggle('dark', getThemeSnapshot() === 'dark');
    callback();
  };
  window.addEventListener('storage', onStorage);
  return () => {
    themeListeners.delete(callback);
    window.removeEventListener('storage', onStorage);
  };
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
