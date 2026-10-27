export type FieldProblem = { field: string; code: string };
export type PostResult =
  | { ok: true; status: number }
  | { ok: false; status: number; code: string; fields: FieldProblem[] };

/** POSTs JSON to a BFF route and normalises problem+json into stable codes (the UI translates them). */
export async function postJson(
  url: string,
  body: unknown,
  fetchImpl: typeof fetch = fetch,
): Promise<PostResult> {
  let response: Response;
  try {
    response = await fetchImpl(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      credentials: "same-origin",
    });
  } catch {
    return { ok: false, status: 0, code: "NETWORK", fields: [] };
  }
  if (response.ok) return { ok: true, status: response.status };
  const problem = (await response.json().catch(() => null)) as {
    code?: string;
    errors?: { field?: string; code?: string }[];
  } | null;
  return {
    ok: false,
    status: response.status,
    code: problem?.code ?? "GENERIC",
    fields: (problem?.errors ?? []).flatMap((e) =>
      e.field && e.code ? [{ field: e.field, code: e.code }] : [],
    ),
  };
}
