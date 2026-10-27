import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import { afterEach, describe, expect, it, vi } from "vitest";
import common from "../../messages/en/common.json";
import shell from "../../messages/en/shell.json";
import { Logo } from "@/components/logo";
import { directionOf } from "./direction";
import { AccountMenu } from "./AccountMenu";
import { NavLinks } from "./NavLinks";
import { accountLinks, footerLinks, primaryNav } from "./nav";

afterEach(() => cleanup());

const pathname = vi.hoisted(() => ({ value: "/plan" }));
const push = vi.hoisted(() => vi.fn());
vi.mock("next/navigation", () => ({
  usePathname: () => pathname.value,
  useRouter: () => ({ push, refresh: vi.fn() }),
}));

function withIntl(ui: React.ReactNode) {
  return (
    <NextIntlClientProvider locale="en" messages={{ common, shell }}>
      {ui}
    </NextIntlClientProvider>
  );
}

describe("NavLinks", () => {
  it("lists the primary navigation and marks the current page", () => {
    pathname.value = "/plan";
    render(withIntl(<NavLinks />));
    const nav = screen.getByRole("navigation", { name: "Primary" });
    expect(nav).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Plan a trip" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(screen.getByRole("link", { name: "Help" })).not.toHaveAttribute("aria-current");
  });

  it("keeps a parent section active on nested routes", () => {
    pathname.value = "/trips/trp_123";
    render(withIntl(<NavLinks />));
    expect(screen.getByRole("link", { name: "My trips" })).toHaveAttribute("aria-current", "page");
  });
});

describe("Logo", () => {
  it("renders the mark with the wordmark and a single accessible name", () => {
    render(withIntl(<Logo variant="full" />));
    expect(screen.getAllByRole("img", { name: "Dhathuru" })).toHaveLength(1);
    expect(screen.getAllByText("Dhathuru", { selector: "span" })[0]).toHaveAttribute(
      "aria-hidden",
      "true",
    );
  });
});

describe("navigation config", () => {
  it("has a message for every nav and footer key", () => {
    for (const item of primaryNav) expect(shell.nav).toHaveProperty(item.key);
    for (const link of footerLinks) expect(shell.footer).toHaveProperty(link.key);
    expect(accountLinks.signIn).toMatch(/^\//);
  });
});

describe("AccountMenu", () => {
  it("opens a menu with trips, account and sign out for the signed-in user", async () => {
    const user = userEvent.setup();
    render(withIntl(<AccountMenu name="Aisha Naseem" />));
    await user.click(screen.getByRole("button", { name: "AN, Account menu" }));
    expect(await screen.findByRole("menuitem", { name: "My trips" })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Your account" })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Sign out" })).toBeInTheDocument();
  });
});

describe("directionOf", () => {
  it("is right to left for Dhivehi and left to right for English", () => {
    expect(directionOf("dv")).toBe("rtl");
    expect(directionOf("dv-MV")).toBe("rtl");
    expect(directionOf("en")).toBe("ltr");
  });
});
