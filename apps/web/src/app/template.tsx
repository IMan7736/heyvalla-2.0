import type { ReactNode } from "react";
import { PageTransition } from "@/motion";

/** Templates re-mount on navigation, so enter/exit transitions run per page (layouts do not). */
export default function Template({ children }: { children: ReactNode }) {
  return <PageTransition>{children}</PageTransition>;
}
