import { NextResponse, type NextRequest } from "next/server";
import { createApiClient } from "@/lib/api-client";

/** BFF: place search / lookup for the planner comboboxes. Browser never talks to the API directly. */
export async function GET(request: NextRequest) {
  const params = request.nextUrl.searchParams;
  const id = params.get("id");
  const client = createApiClient();
  try {
    if (id) {
      const { data, error, response } = await client.GET("/places/{placeId}", {
        params: { path: { placeId: id } },
      });
      if (data) return NextResponse.json(data);
      return NextResponse.json(error ?? { code: "UPSTREAM_ERROR" }, { status: response.status });
    }
    const query = (params.get("query") ?? "").slice(0, 80);
    const { data, error, response } = await client.GET("/places", {
      params: { query: { query, limit: 8 } },
    });
    if (data) return NextResponse.json(data);
    return NextResponse.json(error ?? { code: "UPSTREAM_ERROR" }, { status: response.status });
  } catch {
    return NextResponse.json({ code: "UPSTREAM_UNAVAILABLE" }, { status: 502 });
  }
}
