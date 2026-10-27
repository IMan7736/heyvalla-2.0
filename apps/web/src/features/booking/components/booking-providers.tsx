"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { BookingApiError } from "../lib/api";

/** Server state for booking routes. 4xx answers are final (never retried); network and 5xx get one retry. */
export function BookingProviders({ children }: { children: ReactNode }) {
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 5_000,
            refetchOnWindowFocus: true,
            retry: (count, error) =>
              !(error instanceof BookingApiError && error.status >= 400 && error.status < 500) &&
              count < 1,
          },
        },
      }),
  );
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
