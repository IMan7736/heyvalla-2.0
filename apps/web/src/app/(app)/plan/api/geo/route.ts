import { NextResponse, type NextRequest } from "next/server";
import { createApiClient } from "@/lib/api-client";

const IDS = /^[a-z]{3,4}_[0-9A-HJKMNP-TV-Z]{26}(,[a-z]{3,4}_[0-9A-HJKMNP-TV-Z]{26}){0,11}$/;

/** BFF: leg geometries (GeoJSON, flagged approximate) for the trip detail map. */
export async function GET(request: NextRequest) {
  const ids = request.nextUrl.searchParams.get("ids") ?? "";
  if (!IDS.test(ids)) return NextResponse.json({ code: "VALIDATION_FAILED" }, { status: 422 });
  try {
    const { data, error, response } = await createApiClient().GET("/geo/legs", {
      params: { query: { ids } },
    });
    if (data) return NextResponse.json(data);
    return NextResponse.json(error ?? { code: "UPSTREAM_ERROR" }, { status: response.status });
  } catch {
    return NextResponse.json({ code: "UPSTREAM_UNAVAILABLE" }, { status: 502 });
  }
}
