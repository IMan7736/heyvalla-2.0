"use client";

import { Avatar, DropdownMenu } from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";

/** Same rule as the Avatar, so the accessible name contains the visible text (WCAG 2.5.3). */
function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  const first = parts[0]?.[0] ?? "";
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? "") : "";
  return (first + last).toLocaleUpperCase();
}

/** Signed-in account area of the header: avatar button with trips, account and sign out. */
export function AccountMenu({ name }: { name: string }) {
  const t = useTranslations("shell.account");
  const router = useRouter();

  async function signOut() {
    try {
      await fetch("/api/auth/logout", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: "{}",
      });
    } finally {
      router.push("/login");
      router.refresh();
    }
  }

  const initials = initialsOf(name);

  return (
    <DropdownMenu
      trigger={
        <button
          type="button"
          aria-label={`${initials}, ${t("menu")}`}
          title={t("signedInAs", { name })}
          className="rounded-full outline-none focus-visible:ring-2 focus-visible:ring-lagoon-500 focus-visible:ring-offset-2"
        >
          <Avatar name={name} size={40} />
        </button>
      }
      items={[
        { label: t("myTrips"), icon: "ticket", onSelect: () => router.push("/trips") },
        { label: t("myAccount"), icon: "user", onSelect: () => router.push("/account") },
        { type: "separator" },
        { label: t("signOut"), icon: "logout", onSelect: () => void signOut() },
      ]}
    />
  );
}
