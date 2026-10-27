import { getTranslations } from "next-intl/server";
import type { ReactNode } from "react";
import { fontClassName } from "./fonts";
import { UiProviders } from "./Providers";
import { SiteFooter } from "./SiteFooter";
import { SiteHeader } from "./SiteHeader";
import "@/styles/globals.css";

/**
 * Page frame for every public and signed-in surface: skip link, header, content, footer.
 * Pages supply their own <main id="main"> so the skip link has a target.
 */
export async function PageFrame({
  children,
  footer = true,
}: {
  children: ReactNode;
  footer?: boolean;
}) {
  const t = await getTranslations("shell");
  return (
    <div className={`${fontClassName} shell-fonts flex min-h-dvh flex-col`}>
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:fixed focus:inset-s-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-ocean-900 focus:px-4 focus:py-2 focus:text-ink-inverse"
      >
        {t("skipToContent")}
      </a>
      <UiProviders>
        <SiteHeader />
        <div className="flex-1">{children}</div>
        {footer ? <SiteFooter /> : null}
      </UiProviders>
    </div>
  );
}
