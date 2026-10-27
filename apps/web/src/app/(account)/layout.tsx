import type { ReactNode } from "react";
import { PageFrame } from "@/shell/PageFrame";

/** Sign in, sign up, profile, travellers. Owned by DHONI (features/account). */
export default function AccountLayout({ children }: { children: ReactNode }) {
  return <PageFrame footer={false}>{children}</PageFrame>;
}
