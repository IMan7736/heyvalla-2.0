import type { ReactNode } from "react";
import { PageFrame } from "@/shell/PageFrame";

/** Landing, help and legal pages. */
export default function MarketingLayout({ children }: { children: ReactNode }) {
  return <PageFrame>{children}</PageFrame>;
}
