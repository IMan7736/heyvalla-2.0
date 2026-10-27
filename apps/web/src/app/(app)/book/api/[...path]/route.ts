import type { NextRequest } from "next/server";
import { handleBookingRequest } from "@/features/booking/server/bff";

async function handle(request: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  return handleBookingRequest({
    method: request.method,
    path: path.join("/"),
    search: request.nextUrl.search,
    origin: request.headers.get("origin"),
    fetchSite: request.headers.get("sec-fetch-site"),
    ownOrigin: request.nextUrl.origin,
    idempotencyKey: request.headers.get("idempotency-key"),
    body: () => request.json().catch(() => ({})),
  });
}

export { handle as GET, handle as POST };
