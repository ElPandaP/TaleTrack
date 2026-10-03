import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

/**
 * Joins class names (strings, arrays, conditional objects) and resolves conflicting
 * Tailwind utilities so the last one wins.
 *
 * @param inputs - Class values in any form accepted by `clsx`.
 * @returns A single class string.
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
