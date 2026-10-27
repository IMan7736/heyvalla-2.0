export type Problem = { code: string; fields: { field: string; code: string }[]; status: number };

export type CallResult<T> = { ok: true; status: number; data: T } | { ok: false; problem: Problem };

/** Calls the BFF account proxy (`/api/account/...`). Session cookies are sent automatically. */
export async function account<T = unknown>(
  path: string,
  init: { method?: string; body?: unknown } = {},
  fetchImpl: typeof fetch = fetch,
): Promise<CallResult<T>> {
  let response: Response;
  try {
    response = await fetchImpl(`/api/account${path}`, {
      method: init.method ?? "GET",
      credentials: "same-origin",
      ...(init.body !== undefined
        ? { headers: { "Content-Type": "application/json" }, body: JSON.stringify(init.body) }
        : {}),
    });
  } catch {
    return { ok: false, problem: { code: "NETWORK", fields: [], status: 0 } };
  }
  if (response.ok) {
    const data = response.status === 204 ? null : await response.json().catch(() => null);
    return { ok: true, status: response.status, data: data as T };
  }
  const body = (await response.json().catch(() => null)) as {
    code?: string;
    errors?: { field?: string; code?: string }[];
  } | null;
  return {
    ok: false,
    problem: {
      code: body?.code ?? "GENERIC",
      status: response.status,
      fields: (body?.errors ?? []).flatMap((e) =>
        e.field && e.code ? [{ field: e.field, code: e.code }] : [],
      ),
    },
  };
}
