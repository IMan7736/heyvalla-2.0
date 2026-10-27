import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { BOOKING_ID, FakeBookingApi } from "./support/fake-bookings";

/**
 * Booking flow: search result -> book -> pay -> tickets -> cancel.
 *
 * Needs the Prism mock for the signed-in session and the planner pages (API_MODE=mock, API_MOCK_URL reachable).
 * Everything the booking BFF (`/book/api/*`) returns comes from the stateful fake below, which follows the
 * bookings.yaml contract, so each branch (decline, verification, expiry, refund) is deterministic.
 */
test.setTimeout(120_000);
const slowExpect = expect.configure({ timeout: 20_000 });

const MOCK = process.env.API_MOCK_URL ?? "http://localhost:4010";
const FROM = "isl_01K8Z3M5Q7R9T2V4W6X8Y0A1B2";
const TO = "isl_01K8Z3M5Q7R9T2V4W6X8Y0A1F2";
const RESULTS = `/results?from=${FROM}&to=${TO}&date=2099-01-01&time=17:00&mode=arriveBy&adults=1`;

async function mockUp(): Promise<boolean> {
  try {
    return (await fetch(`${MOCK}/health`)).ok;
  } catch {
    return false;
  }
}

async function install(page: Page): Promise<FakeBookingApi> {
  const fake = new FakeBookingApi();
  await page.route("**/book/api/**", (route) => fake.handle(route));
  await page.route("**/api/account/travellers", (route) =>
    route.fulfill({
      json: {
        items: [
          {
            id: "trv_01K8Z3M5Q7R9T2V4W6X8Y0P1Q2",
            fullName: "Aishath Naseem",
            passengerType: "adult",
            documentType: "none",
          },
        ],
      },
    }),
  );
  return fake;
}

async function expectNoA11yViolations(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag22aa"])
    .analyze();
  slowExpect(results.violations.map((v) => `${v.id}: ${v.nodes[0]?.target.join(" ")}`)).toEqual([]);
}

async function holdSeats(page: Page) {
  await page.goto("/book?option=trp_01K8Z3M5Q7R9T2V4W6X8Y0E1F2&adults=1");
  await slowExpect(page.getByRole("heading", { name: "Who is travelling?" })).toBeVisible();
  await page.getByLabel(/^Full name/).fill("Aishath Naseem");
  await page.getByRole("button", { name: "Hold seats and continue" }).click();
  await slowExpect(page).toHaveURL(new RegExp(`/book/pay/${BOOKING_ID}`));
}

async function payWith(page: Page, number: string) {
  await page.getByLabel(/^Name on card/).fill("Aishath Naseem");
  await page.getByLabel(/^Card number/).fill(number);
  await page.getByLabel(/^Expiry/).fill("1230");
  await page.getByLabel(/^Security code/).fill("123");
  await page.getByRole("button", { name: /^Pay MVR/ }).click();
}

test.describe("booking", () => {
  test.beforeEach(async ({ context, baseURL }) => {
    test.skip(!(await mockUp()), "needs the Prism mock (API_MOCK_URL) for the signed-in session");
    // Placeholder session so the auth proxy lets /trips through; the mock answers /me and /auth/refresh.
    await context.addCookies([
      { name: "dh_rt", value: "e2e", url: baseURL ?? "http://localhost:3105" },
    ]);
  });

  test("search result -> book -> pay -> tickets -> cancel", async ({ page }) => {
    const fake = await install(page);

    // Hand-off from a search result: the planner's Book link lands on /book?option=...
    await page.goto(RESULTS);
    await slowExpect(page.getByRole("article").first()).toBeVisible();
    await page
      .getByRole("link", { name: /View trip/ })
      .first()
      .click();
    await slowExpect(page.getByRole("link", { name: /Book this trip/ })).toHaveAttribute(
      "href",
      /\/book\?option=trp_/,
    );
    await page.getByRole("link", { name: /Book this trip/ }).click();

    // Travellers + review: a saved traveller can be picked, the summary shows pay-on-board honestly.
    await slowExpect(page.getByRole("heading", { name: "Who is travelling?" })).toBeVisible();
    await slowExpect(page.getByText("Pay on board").first()).toBeVisible();
    await expectNoA11yViolations(page);
    await page.getByRole("button", { name: "Hold seats and continue" }).click();
    await slowExpect(page.getByText("Check the highlighted fields.")).toBeVisible();
    await page.getByLabel(/^Full name/).fill("Aishath Naseem");
    await page.getByRole("button", { name: "Hold seats and continue" }).click();

    // Hold + countdown + price breakdown.
    await slowExpect(page).toHaveURL(new RegExp(`/book/pay/${BOOKING_ID}`));
    await slowExpect(page.getByText("Seats held for")).toBeVisible();
    await expect(page.getByText("Service fee")).toBeVisible();
    await expect(page.getByText("Pay on board: MVR 30")).toBeVisible();
    await expectNoA11yViolations(page);

    // Card validation happens in the browser, before anything is sent.
    await page.getByRole("button", { name: /^Pay MVR/ }).click();
    await expect(page.getByText("Enter the card number.")).toBeVisible();
    await payWith(page, "4242424242424242");

    // Confirmation, then the ticket wallet.
    await slowExpect(page).toHaveURL(new RegExp(`/book/done/${BOOKING_ID}`));
    await slowExpect(page.getByRole("heading", { name: "You’re booked" })).toBeVisible();
    await page.getByRole("link", { name: "View tickets" }).click();
    await slowExpect(page).toHaveURL(new RegExp(`/trips/${BOOKING_ID}/tickets`));
    await slowExpect(page.getByRole("heading", { name: "Your tickets" })).toBeVisible();
    await expect(page.getByRole("article")).toHaveCount(2);
    await expect(
      page.getByRole("img", { name: /QR code for the ticket from Maafushi to Hulhumalé/ }),
    ).toBeVisible();
    await expectNoA11yViolations(page);

    // My trips shows it as confirmed and upcoming.
    await page.goto("/trips");
    await slowExpect(page.getByRole("heading", { name: "Maafushi → Resort" })).toBeVisible();
    await expect(page.getByText("Confirmed")).toBeVisible();

    // Cancel with a quote: consequences first, then the refund.
    await page.getByRole("link", { name: /Open trip/ }).click();
    await page.getByRole("button", { name: "Cancel booking" }).click();
    const dialog = page.getByRole("dialog");
    await slowExpect(dialog.getByText("You get back")).toBeVisible();
    await expect(dialog.getByText("Cancelling can’t be undone.")).toBeVisible();
    await dialog.getByRole("button", { name: /Cancel and refund MVR 515/ }).click();
    await slowExpect(dialog.getByText("Booking cancelled")).toBeVisible();
    await expect(dialog.getByText(/MVR 515 is on its way back/)).toBeVisible();
    await dialog.getByRole("button", { name: "Done" }).click();
    await slowExpect(page.getByText("Cancelled", { exact: true }).first()).toBeVisible();

    // Idempotency and privacy of what the browser sent.
    const create = fake.requests.find((r) => r.method === "POST" && r.path === "bookings");
    const pay = fake.requests.find((r) => r.path.endsWith("/pay"));
    expect(create?.headers["idempotency-key"]).toBeTruthy();
    expect(pay?.headers["idempotency-key"]).toBeTruthy();
    expect(pay?.body).toEqual({ method: "card", paymentToken: "tok_fake_success" });
    expect(JSON.stringify(fake.requests)).not.toContain("4242");
  });

  test("a declined card keeps the seats; the next card works", async ({ page }) => {
    await install(page);
    await holdSeats(page);
    await payWith(page, "4000000000000002");
    await slowExpect(page.getByText("Payment didn’t go through")).toBeVisible();
    await expect(page.getByText(/Your seats are still held/)).toBeVisible();
    await payWith(page, "4242424242424242");
    await slowExpect(page).toHaveURL(new RegExp(`/book/done/${BOOKING_ID}`));
  });

  test("extra verification: wrong code is refused, the right one confirms", async ({ page }) => {
    await install(page);
    await holdSeats(page);
    await payWith(page, "4000000000003220");
    await slowExpect(page.getByRole("heading", { name: "Verify this payment" })).toBeVisible();
    await page.getByLabel(/^Verification code/).fill("000000");
    await page.getByRole("button", { name: "Verify and pay" }).click();
    await slowExpect(page.getByText("That code didn’t work.")).toBeVisible();
    await page.getByLabel(/^Verification code/).fill("123456");
    await page.getByRole("button", { name: "Verify and pay" }).click();
    await slowExpect(page).toHaveURL(new RegExp(`/book/done/${BOOKING_ID}`));
  });

  test("a leg that cannot be confirmed ends in a clear refunded failure", async ({ page }) => {
    await install(page);
    await holdSeats(page);
    await payWith(page, "4000000000000341");
    await slowExpect(page.getByText("We couldn’t complete this booking")).toBeVisible();
    await expect(page.getByText(/cancelled the whole trip and refunded you/)).toBeVisible();
    await expect(page.getByText(/full amount is being refunded/)).toBeVisible();
    await expect(page.getByRole("link", { name: "Search again" })).toBeVisible();
  });

  test("the hold running out is handled gracefully", async ({ page }) => {
    const fake = await install(page);
    fake.holdMs = 4_000;
    await holdSeats(page);
    await slowExpect(page.getByText("Your hold ran out")).toBeVisible();
    await expect(page.getByText(/you were not charged/)).toBeVisible();
  });

  test("My trips: empty state, then keyboard access to the tab control", async ({ page }) => {
    await install(page);
    await page.goto("/trips");
    await slowExpect(page.getByText("No upcoming trips")).toBeVisible();
    // The key press is retried until the page has hydrated and the control reacts.
    await expect(async () => {
      await page.getByRole("radio", { name: "Upcoming" }).focus();
      await page.keyboard.press("ArrowRight");
      await expect(page.getByText("No past trips yet")).toBeVisible({ timeout: 2_000 });
    }).toPass({ timeout: 30_000 });
    await expectNoA11yViolations(page);
  });
});
