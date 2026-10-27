import { Button } from "@dhathuru/ui";
import { getTranslations } from "next-intl/server";
import Link from "next/link";
import { Logo } from "@/components/logo";
import { getSession, type SessionUser } from "@/lib/session";
import { AccountMenu } from "./AccountMenu";
import { NavLinks } from "./NavLinks";
import { accountLinks } from "./nav";

/** Sticky glass header: logo, primary navigation, account actions. */
export async function SiteHeader() {
  const t = await getTranslations("shell");
  // An unreachable API must not break every page: treat it as signed out.
  const user: SessionUser | null = await getSession().catch(() => null);
  return (
    <header
      style={{ viewTransitionName: "site-header" }}
      className="sticky top-0 z-30 border-b border-line bg-canvas/80 backdrop-blur-xl"
    >
      <div className="mx-auto flex h-[72px] max-w-[var(--container-page)] items-center justify-between gap-8 px-8 min-[90rem]:px-12">
        <Link href="/" className="rounded-md">
          <Logo variant="full" size={36} />
        </Link>
        <NavLinks />
        <div className="flex items-center gap-2">
          {user ? (
            <AccountMenu name={user.displayName?.trim() || user.email} />
          ) : (
            <>
              <Button asChild variant="ghost" size="md">
                <Link href={accountLinks.signIn}>{t("account.signIn")}</Link>
              </Button>
              <Button asChild variant="primary" size="md">
                <Link href={accountLinks.signUp}>{t("account.signUp")}</Link>
              </Button>
            </>
          )}
        </div>
      </div>
    </header>
  );
}
