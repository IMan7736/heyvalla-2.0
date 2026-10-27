import { expect, test, type Page } from "@playwright/test";

/**
 * Needs the real API (API_MODE=real) and Mailpit: `pnpm infra:up`, API on API_BASE_URL.
 * Skipped unless E2E_REAL_API=1 so the default smoke run still works against the mock.
 */
test.skip(process.env.E2E_REAL_API !== "1", "needs the real API and Mailpit");

const MAILPIT = process.env.MAILPIT_URL ?? "http://localhost:8025";
const PASSWORD = "correct horse battery staple";

async function verificationLink(email: string): Promise<string> {
  for (let i = 0; i < 20; i++) {
    const list = (await (
      await fetch(`${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:${email}`)}`)
    ).json()) as {
      messages: { ID: string }[];
    };
    if (list.messages[0]) {
      const msg = (await (
        await fetch(`${MAILPIT}/api/v1/message/${list.messages[0].ID}`)
      ).json()) as { Text: string };
      const match = /https?:\/\/\S+verify-email\?token=\S+/.exec(msg.Text);
      if (match) return match[0];
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  throw new Error("verification email never arrived");
}

test("register, verify by email, sign in, manage account, sign out", async ({ page }) => {
  const email = `e2e-${Date.now()}@example.com`;

  await page.goto("/signup");
  await page.getByLabel(/^Email/).fill(email);
  await page.getByLabel(/^New password/).fill(PASSWORD);
  await page.getByRole("button", { name: "Create account" }).click();
  await expect(page.getByRole("heading", { name: "Check your email" })).toBeVisible();

  const link = await verificationLink(email);
  await page.goto(new URL(link).pathname + new URL(link).search);
  await expect(page.getByText("Your email is confirmed")).toBeVisible();

  await page.goto("/account");
  await expect(page).toHaveURL(/\/login\?returnTo=%2Faccount/);
  await page.getByLabel(/^Email/).fill(email);
  await page.getByLabel(/^Password/).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Your account" })).toBeVisible();

  await page.getByLabel(/^Full name/).fill("Aishath Naseem");
  await page.getByRole("button", { name: "Add traveller" }).click();
  await expect(page.getByText("Aishath Naseem")).toBeVisible();

  await page.getByRole("button", { name: "Sign out", exact: true }).first().click();
  await expect(page).toHaveURL(/\/login/);
  await page.goto("/account");
  await expect(page).toHaveURL(/\/login\?returnTo/);
});

test("wrong password shows a translated message, not a raw code", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel(/^Email/).fill("nobody@example.com");
  await page.getByLabel(/^Password/).fill("not the password at all");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("alert").filter({ hasText: "match" })).toContainText("don't match");
});

async function signUpAndIn(page: Page): Promise<string> {
  const email = `e2e-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.com`;
  await page.goto("/signup");
  await page.getByLabel(/^Email/).fill(email);
  await page.getByLabel(/^New password/).fill(PASSWORD);
  await page.getByRole("button", { name: "Create account" }).click();
  await expect(page.getByRole("heading", { name: "Check your email" })).toBeVisible();
  const link = new URL(await verificationLink(email));
  await page.goto(link.pathname + link.search);
  await expect(page.getByText("Your email is confirmed")).toBeVisible();
  await page.goto("/login?returnTo=%2Faccount");
  await page.getByLabel(/^Email/).fill(email);
  await page.getByLabel(/^Password/).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Your account" })).toBeVisible();
  return email;
}

async function cookieValue(page: Page, name: string): Promise<string | undefined> {
  return (await page.context().cookies()).find((c) => c.name === name)?.value;
}

// L-01: the access cookie lives 15 minutes. Deleting it is exactly what the browser does at expiry.
test("an expired access cookie is renewed on the next navigation and the session survives", async ({
  page,
}) => {
  await signUpAndIn(page);
  const firstRefresh = await cookieValue(page, "dh_rt");
  expect(firstRefresh).toBeTruthy();

  // A public page renders and persists the renewed cookies.
  await page.context().clearCookies({ name: "dh_at" });
  await page.goto("/help");
  expect(await cookieValue(page, "dh_at")).toBeTruthy();
  const secondRefresh = await cookieValue(page, "dh_rt");
  expect(secondRefresh).not.toBe(firstRefresh);

  // A protected page does not bounce to /login.
  await page.context().clearCookies({ name: "dh_at" });
  await page.goto("/account");
  await expect(page).not.toHaveURL(/\/login/);
  await expect(page.getByRole("heading", { name: "Your account" })).toBeVisible();
  expect(await cookieValue(page, "dh_at")).toBeTruthy();
  const thirdRefresh = await cookieValue(page, "dh_rt");
  expect(thirdRefresh).not.toBe(secondRefresh);

  // And again, so a rotated token is never replayed: the old session must still be alive.
  await page.context().clearCookies({ name: "dh_at" });
  await page.goto("/trips");
  await expect(page).not.toHaveURL(/\/login/);
  await page.goto("/account");
  await expect(page.getByRole("heading", { name: "Your account" })).toBeVisible();
});

test("a dead refresh cookie sends a protected page to sign-in and is cleared", async ({
  page,
  context,
  baseURL,
}) => {
  await context.addCookies([
    {
      name: "dh_rt",
      value: "rt_not-a-real-token",
      url: baseURL ?? "http://localhost:3000",
      httpOnly: true,
    },
  ]);
  await page.goto("/account");
  await expect(page).toHaveURL(/\/login\?returnTo/);
  expect(await cookieValue(page, "dh_rt")).toBeUndefined();
});

test("sign-in and sign-up have their own page titles and a big enough show-password target", async ({
  page,
}) => {
  await page.goto("/login");
  await expect(page).toHaveTitle("Sign in | Dhathuru");
  const toggle = page.getByRole("button", { name: "Show password" });
  const box = await toggle.boundingBox();
  expect(box?.height ?? 0).toBeGreaterThanOrEqual(24);
  expect(box?.width ?? 0).toBeGreaterThanOrEqual(24);
  await page.goto("/signup");
  await expect(page).toHaveTitle("Create account | Dhathuru");
});
