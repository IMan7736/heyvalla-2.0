import { clsx, type ClassValue } from "clsx";

/** Joins class names. Later classes do not override earlier ones: avoid conflicting utilities. */
export function cn(...inputs: ClassValue[]): string {
  return clsx(inputs);
}
