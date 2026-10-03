'use client';

import { createContext, useContext, useSyncExternalStore } from 'react';

/** The two colour themes; `dark` adds the `dark` class to `<html>`. */
export type Theme = 'light' | 'dark';

/** What {@link useTheme} returns. */
export interface ThemeContextType {
  /** The active theme. */
  theme: Theme;
  /** Switches between light and dark and saves the choice in `localStorage` (`tt-theme`). */
  toggleTheme: () => void;
}

const ThemeContext = createContext<ThemeContextType>({
  theme: 'light',
  toggleTheme: () => {},
});

// The theme lives in an external store over localStorage (same pattern as auth-context.tsx).
// The server snapshot keeps the hydration render at 'light' so it matches the server, and
// useSyncExternalStore swaps in the stored value right after, with no hydration mismatch.

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

/** Provides the current theme and its toggle to every component below it. */
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

/** Returns the current theme and the function that toggles it. */
export const useTheme = () => useContext(ThemeContext);
