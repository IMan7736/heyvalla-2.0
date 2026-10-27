"use client";

import { useInfiniteQuery, useQuery, useQueryClient } from "@tanstack/react-query";
import { bookingApi } from "./api";
import type { Booking } from "./types";

export const bookingKeys = {
  one: (id: string) => ["booking", id] as const,
  list: ["bookings"] as const,
  tickets: (id: string) => ["booking", id, "tickets"] as const,
  quote: (id: string) => ["booking", id, "quote"] as const,
  travellers: ["travellers"] as const,
  option: (id: string) => ["option", id] as const,
};

/** Polls while a payment is mid-flight (or `pollWhile` says so) so the screen settles by itself. */
export function useBooking(
  id: string,
  options: { pollWhile?: (data: Booking | undefined) => boolean } = {},
) {
  return useQuery({
    queryKey: bookingKeys.one(id),
    queryFn: ({ signal }) => bookingApi.get(id, signal),
    refetchInterval: (query) =>
      options.pollWhile?.(query.state.data) || query.state.data?.status === "payment_pending"
        ? 2500
        : false,
  });
}

export function useBookingList() {
  return useInfiniteQuery({
    queryKey: bookingKeys.list,
    initialPageParam: null as string | null,
    queryFn: ({ pageParam, signal }) => bookingApi.list(pageParam, signal),
    getNextPageParam: (last) => last.page.nextCursor ?? undefined,
    refetchInterval: 60_000,
  });
}

export function useTickets(id: string, enabled: boolean) {
  return useQuery({
    queryKey: bookingKeys.tickets(id),
    queryFn: ({ signal }) => bookingApi.tickets(id, signal),
    enabled,
  });
}

export function useTripOption(id: string | null) {
  return useQuery({
    queryKey: bookingKeys.option(id ?? ""),
    queryFn: ({ signal }) => bookingApi.option(id ?? "", signal),
    enabled: id !== null,
    staleTime: 60_000,
  });
}

export function useSavedTravellers() {
  return useQuery({
    queryKey: bookingKeys.travellers,
    queryFn: ({ signal }) => bookingApi.travellers(signal),
    staleTime: 5 * 60_000,
    retry: false,
  });
}

/** Writes a fresh booking into the cache after a mutation so every screen agrees at once. */
export function useStoreBooking() {
  const client = useQueryClient();
  return (booking: Booking) => {
    client.setQueryData(bookingKeys.one(booking.id), booking);
    void client.invalidateQueries({ queryKey: bookingKeys.list });
    void client.invalidateQueries({ queryKey: bookingKeys.tickets(booking.id) });
  };
}
