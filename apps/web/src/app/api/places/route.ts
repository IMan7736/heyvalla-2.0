import { NextResponse } from "next/server";
import { createApiClient } from "@/lib/api-client";

export const dynamic = "force-dynamic";

const MAX_QUERY = 80;
const MAX_LIMIT = 20;

/**
 * BFF for place search (used by the command palette). Same-origin only; the browser never calls the
 * .NET API directly. The API's `{ items, page }` JSON is returned unchanged.
 */
export async function GET(request: Request) {
  const params = new URL(request.url).searchParams;
  const query = (params.get("query") ?? "").trim().slice(0, MAX_QUERY);
  const limit = Math.min(
    Math.max(Number.parseInt(params.get("limit") ?? "6", 10) || 6, 1),
    MAX_LIMIT,
  );

  try {
    const { data, response } = await createApiClient().GET("/places", {
      params: { query: { query, limit } },
    });
    if (!data) {
      return NextResponse.json(
        { code: "UPSTREAM_ERROR" },
        { status: response.status >= 400 ? response.status : 502 },
      );
    }
    return NextResponse.json(data, { headers: { "Cache-Control": "no-store" } });
  } catch {
    return NextResponse.json({ code: "UPSTREAM_UNAVAILABLE" }, { status: 502 });
  }
}
