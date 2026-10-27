/**
 * Fake checkout only. The card number is mapped to a gateway test token IN THE BROWSER and never sent anywhere.
 * A real gateway replaces this whole file with its hosted fields (see docs/payments/GATEWAY-READINESS.md).
 */
export const TEST_CARDS = [
  { number: "4242424242424242", token: "tok_fake_success", scenario: "success" },
  { number: "4000000000000002", token: "tok_fake_declined", scenario: "declined" },
  {
    number: "4000000000009995",
    token: "tok_fake_insufficient_funds",
    scenario: "insufficientFunds",
  },
  { number: "4000000000003220", token: "tok_fake_verification", scenario: "verification" },
  { number: "4000000000000341", token: "tok_fake_leg_failure", scenario: "legFailure" },
  { number: "4000000000000119", token: "tok_fake_timeout", scenario: "timeout" },
] as const;

export type TestScenario = (typeof TEST_CARDS)[number]["scenario"];

export const VERIFICATION_CODE_HINT = "123456";

export function digitsOnly(value: string): string {
  return value.replace(/\D/g, "");
}

/** Groups digits in fours as the user types: "4242424242424242" -> "4242 4242 4242 4242". */
export function formatCardNumber(value: string): string {
  return digitsOnly(value)
    .slice(0, 19)
    .replace(/(.{4})/g, "$1 ")
    .trim();
}

export function luhnValid(value: string): boolean {
  const digits = digitsOnly(value);
  if (digits.length < 12 || digits.length > 19) return false;
  let sum = 0;
  for (let i = 0; i < digits.length; i += 1) {
    let d = Number(digits[digits.length - 1 - i]);
    if (i % 2 === 1) {
      d *= 2;
      if (d > 9) d -= 9;
    }
    sum += d;
  }
  return sum % 10 === 0;
}

/** "MM/YY" or "MMYY" -> true if the card is still valid in the month of `now`. */
export function expiryValid(value: string, now: Date = new Date()): boolean {
  const digits = digitsOnly(value);
  if (digits.length !== 4) return false;
  const month = Number(digits.slice(0, 2));
  const year = 2000 + Number(digits.slice(2));
  if (month < 1 || month > 12) return false;
  const endOfMonth = Date.UTC(year, month, 1);
  return endOfMonth > now.getTime();
}

export function formatExpiry(value: string): string {
  const digits = digitsOnly(value).slice(0, 4);
  return digits.length > 2 ? `${digits.slice(0, 2)}/${digits.slice(2)}` : digits;
}

export function cvcValid(value: string): boolean {
  return /^\d{3,4}$/.test(value);
}

export type CardErrors = Partial<
  Record<"name" | "number" | "expiry" | "cvc", "required" | "invalid" | "notTestCard">
>;

export type CardForm = { name: string; number: string; expiry: string; cvc: string };

export function validateCard(form: CardForm, now: Date = new Date()): CardErrors {
  const errors: CardErrors = {};
  if (form.name.trim().length < 2) errors.name = "required";
  if (digitsOnly(form.number).length === 0) errors.number = "required";
  else if (!luhnValid(form.number)) errors.number = "invalid";
  else if (!tokenForCard(form.number)) errors.number = "notTestCard";
  if (digitsOnly(form.expiry).length === 0) errors.expiry = "required";
  else if (!expiryValid(form.expiry, now)) errors.expiry = "invalid";
  if (form.cvc.length === 0) errors.cvc = "required";
  else if (!cvcValid(form.cvc)) errors.cvc = "invalid";
  return errors;
}

/** The gateway test token for a test card number, or null if it is not one of ours. */
export function tokenForCard(number: string): string | null {
  const digits = digitsOnly(number);
  return TEST_CARDS.find((c) => c.number === digits)?.token ?? null;
}
