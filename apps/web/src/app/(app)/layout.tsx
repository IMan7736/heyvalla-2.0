import type { ReactNode } from "react";
import { PageFrame } from "@/shell/PageFrame";

/** Signed-in product surface: planner, results, trips, booking. Frame is LAGOON's. */
export default function AppLayout({ children }: { children: ReactNode }) {
  return <PageFrame>{children}</PageFrame>;
}
