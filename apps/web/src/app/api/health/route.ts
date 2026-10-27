import { NextResponse } from "next/server";

export const dynamic = "force-dynamic";

/** Liveness for the web tier (used by smoke tests and the host). */
export function GET() {
  return NextResponse.json({ status: "ok", time: new Date().toISOString() });
}
