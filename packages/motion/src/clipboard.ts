"use client";

import { useCallback, useEffect, useRef, useState } from "react";

/**
 * Copy text and expose a short-lived `copied` flag for feedback (pair with <Bloom>).
 * Resolves false when the Clipboard API is unavailable or denied.
 */
export function useCopyToClipboard(resetMs = 1800): {
  copy: (text: string) => Promise<boolean>;
  copied: boolean;
} {
  const [copied, setCopied] = useState(false);
  const timer = useRef<number | undefined>(undefined);

  useEffect(() => () => window.clearTimeout(timer.current), []);

  const copy = useCallback(
    async (text: string) => {
      try {
        await navigator.clipboard.writeText(text);
      } catch {
        return false;
      }
      setCopied(true);
      window.clearTimeout(timer.current);
      timer.current = window.setTimeout(() => setCopied(false), resetMs);
      return true;
    },
    [resetMs],
  );
  return { copy, copied };
}
