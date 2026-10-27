import type { PassengerInput, PassengerType, Traveller } from "./types";

export const MAX_PASSENGERS = 9;

export type PassengerRow = {
  key: string;
  travellerId?: string;
  fullName: string;
  passengerType: PassengerType;
  phone: string;
};

export type RowErrors = { fullName?: "nameRequired" | "nameTooLong"; phone?: "phoneInvalid" };
export type FormErrors = { rows: RowErrors[]; party?: "adultRequired" | "tooManyInfants" };

let counter = 0;
export function newRow(partial: Partial<PassengerRow> = {}): PassengerRow {
  counter += 1;
  return { key: `row-${counter}`, fullName: "", passengerType: "adult", phone: "", ...partial };
}

export function rowFromTraveller(traveller: Traveller, key?: string): PassengerRow {
  return newRow({
    ...(key ? { key } : {}),
    travellerId: traveller.id,
    fullName: traveller.fullName,
    passengerType: traveller.passengerType ?? "adult",
    phone: traveller.phone ?? "",
  });
}

const PHONE = /^[+0-9 ()-]{0,32}$/;

export function validateRows(rows: PassengerRow[]): FormErrors {
  const result: FormErrors = { rows: rows.map(() => ({})) };
  rows.forEach((row, i) => {
    const errors: RowErrors = {};
    const name = row.fullName.trim();
    if (name.length === 0) errors.fullName = "nameRequired";
    else if (name.length > 120) errors.fullName = "nameTooLong";
    if (!PHONE.test(row.phone.trim())) errors.phone = "phoneInvalid";
    result.rows[i] = errors;
  });
  const adults = rows.filter((r) => r.passengerType === "adult").length;
  const infants = rows.filter((r) => r.passengerType === "infant").length;
  if (adults === 0) result.party = "adultRequired";
  else if (infants > adults) result.party = "tooManyInfants";
  return result;
}

export function hasErrors(errors: FormErrors): boolean {
  return errors.party !== undefined || errors.rows.some((r) => Object.keys(r).length > 0);
}

export function toInputs(rows: PassengerRow[]): PassengerInput[] {
  return rows.map((r) => ({
    ...(r.travellerId ? { travellerId: r.travellerId } : {}),
    fullName: r.fullName.trim(),
    passengerType: r.passengerType,
    ...(r.phone.trim() ? { phone: r.phone.trim() } : {}),
  }));
}

export type Party = { adults: number; children: number; infants: number };

export const SOLO: Party = { adults: 1, children: 0, infants: 0 };

/**
 * The planner quotes every fare for the group that was searched, so the group is fixed when booking:
 * one row per person, in the order adult, child, infant.
 */
export function rowsForParty(party: Party): PassengerRow[] {
  const rows: PassengerRow[] = [];
  for (let i = 0; i < party.adults; i += 1) rows.push(newRow({ passengerType: "adult" }));
  for (let i = 0; i < party.children; i += 1) rows.push(newRow({ passengerType: "child" }));
  for (let i = 0; i < party.infants; i += 1) rows.push(newRow({ passengerType: "infant" }));
  return rows;
}

/** Reads `adults/children/infants` from a query string; null when none is present or the group is not bookable. */
export function parseParty(query: Record<string, string | string[] | undefined>): Party | null {
  const read = (key: string): number | null => {
    const value = query[key];
    if (typeof value !== "string" || !/^\d{1,2}$/.test(value)) return null;
    return Number(value);
  };
  const adults = read("adults");
  const children = read("children") ?? 0;
  const infants = read("infants") ?? 0;
  if (
    adults === null ||
    adults < 1 ||
    children + adults + infants > MAX_PASSENGERS ||
    infants > adults
  )
    return null;
  return { adults, children, infants };
}

/** Pre-hold estimate: the planner's own quote for the group (bookable legs only). The hold returns the exact price. */
export function estimateFares(
  legs: { bookable: boolean; price: { amountMinor: number } }[],
): number {
  return legs.filter((l) => l.bookable).reduce((sum, l) => sum + l.price.amountMinor, 0);
}
