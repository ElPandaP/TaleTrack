'use client';

import { motion } from 'framer-motion';
import { Sun, Moon } from 'lucide-react';
import { useTheme } from '@/lib/theme-context';

export default function ThemeToggle({ compact = false }: { compact?: boolean }) {
  const { theme, toggleTheme } = useTheme();
  const isDark = theme === 'dark';

  if (compact) {
    return (
      <button
        onClick={toggleTheme}
        aria-label="Toggle theme"
        className="relative w-9 h-9 rounded-full border border-border bg-secondary/60 hover:bg-secondary flex items-center justify-center transition-colors cursor-pointer"
      >
        <motion.div
          key={theme}
          initial={{ scale: 0, rotate: -90, opacity: 0 }}
          animate={{ scale: 1, rotate: 0, opacity: 1 }}
          exit={{ scale: 0, rotate: 90, opacity: 0 }}
          transition={{ type: 'spring', stiffness: 300, damping: 20 }}
        >
          {isDark
            ? <Moon className="w-4 h-4 text-muted-foreground" />
            : <Sun className="w-4 h-4 text-muted-foreground" />
          }
        </motion.div>
      </button>
    );
  }

  return (
    <button
      onClick={toggleTheme}
      aria-label="Toggle theme"
      className="relative inline-flex items-center h-8 w-16 rounded-full p-1 border border-border transition-colors duration-300 cursor-pointer focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
      style={{ backgroundColor: isDark ? 'oklch(0.17 0.033 62)' : 'oklch(0.93 0.025 82)' }}
    >
      {/* Sliding pill */}
      <motion.div
        className="absolute top-1 h-6 w-6 rounded-full shadow-sm border border-border bg-card flex items-center justify-center"
        initial={false}
        animate={{ x: isDark ? '2rem' : 0 }}
        transition={{ type: 'spring', stiffness: 400, damping: 28 }}
      >
        <motion.div
          initial={false}
          animate={{ scale: isDark ? 0 : 1, rotate: isDark ? 90 : 0 }}
          transition={{ duration: 0.25 }}
          className="absolute"
        >
          <Sun className="w-3.5 h-3.5 text-amber-500" />
        </motion.div>
        <motion.div
          initial={false}
          animate={{ scale: isDark ? 1 : 0, rotate: isDark ? 0 : -90 }}
          transition={{ duration: 0.25 }}
          className="absolute"
        >
          <Moon className="w-3.5 h-3.5 text-primary" />
        </motion.div>
      </motion.div>

      {/* Background icons */}
      <div className="relative z-0 flex items-center justify-between w-full px-0.5">
        <motion.div animate={{ opacity: isDark ? 0.3 : 0.8 }} transition={{ duration: 0.2 }}>
          <Sun className="w-3 h-3 text-amber-500" />
        </motion.div>
        <motion.div animate={{ opacity: isDark ? 0.8 : 0.3 }} transition={{ duration: 0.2 }}>
          <Moon className="w-3 h-3 text-muted-foreground" />
        </motion.div>
      </div>
    </button>
  );
}
