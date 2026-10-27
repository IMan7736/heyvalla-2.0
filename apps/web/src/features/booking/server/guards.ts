import { notFound, redirect } from "next/navigation";
import { getSession } from "@/lib/session";
import { safeReturnTo } from "@/proxy";

const BOOKING_ID = /^bkg_[0-9A-Za-z]{10,40}$/;

export function isBookingId(value: string): boolean {
  return BOOKING_ID.test(value);
}

/** Signed-in pages: unauthenticated visitors go to /login and come back here afterwards. */
export async function requireSession(returnTo: string): Promise<void> {
  const session = await getSession();
  if (!session) redirect(`/login?returnTo=${encodeURIComponent(safeReturnTo(returnTo))}`);
}

export async function requireBookingId(id: string, returnTo: string): Promise<string> {
  if (!isBookingId(id)) notFound();
  await requireSession(returnTo);
  return id;
}
