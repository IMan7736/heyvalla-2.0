"use client";

import { useTranslations } from "next-intl";
import { resolveCode } from "../lib/explanations";

/** Translates one structured code with its params. Unknown codes fall back to a generic note. */
export function useExplain() {
  const t = useTranslations("planner");
  return (code: string, params: Record<string, unknown>): string => {
    const resolved = resolveCode(code, params);
    const values: Record<string, string | number> = { ...resolved.params };
    if (typeof values.mode === "string") values.mode = t(`modes.${values.mode}` as never);
    return t(`explain.${resolved.key}` as never, values as never);
  };
}
