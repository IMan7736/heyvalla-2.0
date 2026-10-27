import type {
  Booking,
  BookingPage,
  CancellationQuote,
  PassengerInput,
  Ticket,
  Traveller,
  TripOption,
} from "./types";

/** Error from the booking BFF. `code` is the API's stable machine code; the UI owns the wording. */
export class BookingApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    readonly params: Record<string, unknown> = {},
  ) {
    super(code);
    this.name = "BookingApiError";
  }

  /** The request may or may not have reached the server, so it is safe (and right) to retry with the same key. */
  get isUncertain(): boolean {
    return (
      this.status === 0 ||
      this.status >= 500 ||
      this.code === "PAYMENT_IN_PROGRESS" ||
      this.code === "IDEMPOTENCY_IN_PROGRESS"
    );
  }
}

const BASE = "/book/api";

async function request<T>(
  path: string,
  init: { method?: string; body?: unknown; idempotencyKey?: string; signal?: AbortSignal } = {},
): Promise<T> {
  const headers: Record<string, string> = {};
  if (init.body !== undefined) headers["Content-Type"] = "application/json";
  if (init.idempotencyKey) headers["Idempotency-Key"] = init.idempotencyKey;
  let response: Response;
  try {
    response = await fetch(`${BASE}${path}`, {
      method: init.method ?? "GET",
      credentials: "same-origin",
      headers,
      ...(init.body !== undefined ? { body: JSON.stringify(init.body) } : {}),
      ...(init.signal ? { signal: init.signal } : {}),
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") throw error;
    throw new BookingApiError(0, "NETWORK");
  }
  if (response.ok) return (await response.json()) as T;
  const body = (await response.json().catch(() => null)) as {
    code?: unknown;
    params?: Record<string, unknown>;
  } | null;
  throw new BookingApiError(
    response.status,
    typeof body?.code === "string" ? body.code : "UNKNOWN",
    body?.params ?? {},
  );
}

export function newIdempotencyKey(): string {
  return crypto.randomUUID();
}

export const bookingApi = {
  create: (
    optionId: string,
    passengers: PassengerInput[],
    idempotencyKey: string,
    signal?: AbortSignal,
  ) =>
    request<Booking>("/bookings", {
      method: "POST",
      body: { optionId, passengers },
      idempotencyKey,
      ...(signal ? { signal } : {}),
    }),
  get: (id: string, signal?: AbortSignal) =>
    request<Booking>(`/bookings/${encodeURIComponent(id)}`, signal ? { signal } : {}),
  list: (cursor?: string | null, signal?: AbortSignal) =>
    request<BookingPage>(
      `/bookings?limit=50${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ""}`,
      signal ? { signal } : {},
    ),
  pay: (id: string, paymentToken: string, idempotencyKey: string) =>
    request<Booking>(`/bookings/${encodeURIComponent(id)}/pay`, {
      method: "POST",
      body: { method: "card", paymentToken },
      idempotencyKey,
    }),
  verify: (id: string, code: string) =>
    request<Booking>(`/bookings/${encodeURIComponent(id)}/pay/verify`, {
      method: "POST",
      body: { code },
    }),
  quote: (id: string, signal?: AbortSignal) =>
    request<CancellationQuote>(
      `/bookings/${encodeURIComponent(id)}/cancellation-quote`,
      signal ? { signal } : {},
    ),
  cancel: (id: string) =>
    request<Booking>(`/bookings/${encodeURIComponent(id)}/cancel`, { method: "POST", body: {} }),
  tickets: (id: string, signal?: AbortSignal) =>
    request<{ items: Ticket[] }>(
      `/bookings/${encodeURIComponent(id)}/tickets`,
      signal ? { signal } : {},
    ),
  acknowledge: (bookingId: string, disruptionId: string) =>
    request<Booking>(
      `/bookings/${encodeURIComponent(bookingId)}/disruptions/${encodeURIComponent(disruptionId)}/acknowledge`,
      { method: "POST", body: {} },
    ),
  option: (id: string, signal?: AbortSignal) =>
    request<TripOption>(`/options/${encodeURIComponent(id)}`, signal ? { signal } : {}),
  travellers: async (signal?: AbortSignal): Promise<Traveller[]> => {
    const response = await fetch("/api/account/travellers", {
      credentials: "same-origin",
      ...(signal ? { signal } : {}),
    });
    if (!response.ok) throw new BookingApiError(response.status, "TRAVELLERS_UNAVAILABLE");
    const body = (await response.json()) as { items?: Traveller[] } | Traveller[];
    return Array.isArray(body) ? body : (body.items ?? []);
  },
  qrUrl: (ticketId: string) => `${BASE}/tickets/${encodeURIComponent(ticketId)}/qr`,
  pdfUrl: (bookingId: string) => `${BASE}/bookings/${encodeURIComponent(bookingId)}/itinerary.pdf`,
};
