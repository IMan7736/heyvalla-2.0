import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const spec = JSON.parse(
  readFileSync(new URL("../generated/openapi.bundled.json", import.meta.url), "utf8"),
);
const operations = Object.entries(spec.paths).flatMap(([path, item]) =>
  ["get", "post", "put", "patch", "delete"]
    .filter((m) => item[m])
    .map((m) => ({ path, method: m, op: item[m] })),
);

// Every endpoint promised by docs/CONTRACT.md "Endpoints (v1 surface)".
const expected = [
  "GET /health",
  "POST /auth/register",
  "POST /auth/login",
  "POST /auth/refresh",
  "POST /auth/logout",
  "POST /auth/verify-email",
  "POST /auth/forgot-password",
  "POST /auth/reset-password",
  "GET /me",
  "PATCH /me",
  "DELETE /me",
  "GET /me/travellers",
  "POST /me/travellers",
  "PATCH /me/travellers/{travellerId}",
  "DELETE /me/travellers/{travellerId}",
  "GET /places",
  "GET /places/{placeId}",
  "GET /operators",
  "GET /services",
  "GET /status/services/{serviceId}",
  "POST /trips/search",
  "GET /trips/options/{optionId}",
  "POST /trips/replan",
  "GET /geo/legs",
  "POST /bookings",
  "GET /bookings",
  "GET /bookings/{bookingId}",
  "POST /bookings/{bookingId}/pay",
  "POST /bookings/{bookingId}/cancel",
  "GET /bookings/{bookingId}/tickets",
  "GET /tickets/{ticketId}/qr",
  "POST /tickets/verify",
];

test("every endpoint in CONTRACT.md exists", () => {
  const have = new Set(operations.map((o) => `${o.method.toUpperCase()} ${o.path}`));
  for (const e of expected) assert.ok(have.has(e), `missing ${e}`);
});

test("operation ids are unique and present", () => {
  const ids = operations.map((o) => o.op.operationId);
  assert.ok(ids.every(Boolean));
  assert.equal(new Set(ids).size, ids.length);
});

test("booking and payment POSTs require an Idempotency-Key", () => {
  for (const key of ["POST /bookings", "POST /bookings/{bookingId}/pay"]) {
    const [method, path] = key.split(" ");
    const op = spec.paths[path][method.toLowerCase()];
    const params = (op.parameters ?? []).map((p) =>
      p.$ref ? spec.components.parameters[p.$ref.split("/").pop()] : p,
    );
    assert.ok(
      params.some((p) => p.name === "Idempotency-Key" && p.required),
      `${key} lacks Idempotency-Key`,
    );
  }
});

test("money is integer minor units", () => {
  const money = spec.components.schemas.Money;
  assert.equal(money.properties.amountMinor.type, "integer");
  assert.deepEqual(money.properties.currency.enum, ["MVR"]);
});

test("errors are problem+json with a machine code", () => {
  assert.ok(spec.components.schemas.Problem.required.includes("code"));
  assert.ok(spec.components.responses.NotFound.content["application/problem+json"]);
});

test("TripOption carries every field the engine promises", () => {
  const required = spec.components.schemas.TripOption.required;
  for (const f of [
    "id",
    "legs",
    "price",
    "buffers",
    "risk",
    "labels",
    "score",
    "explanations",
    "warnings",
  ]) {
    assert.ok(required.includes(f), `TripOption missing ${f}`);
  }
});

test("every example id matches its prefix pattern", () => {
  const text = readFileSync(new URL("../generated/openapi.bundled.yaml", import.meta.url), "utf8");
  const ids =
    text.match(
      /\b(?:isl|atl|apt|jty|txs|poi|adr|opr|svc|trp|srch|leg|bkg|bkl|tkt|pay|usr|trv|pax)_[0-9A-Za-z]{26}\b/g,
    ) ?? [];
  assert.ok(ids.length > 20);
  for (const id of ids) assert.match(id, /^[a-z]+_[0-9A-HJKMNP-TV-Z]{26}$/, `bad example id ${id}`);
});
