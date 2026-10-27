import { getTranslations } from "next-intl/server";
import Link from "next/link";
import { Logo } from "@/components/logo";
import { footerLinks } from "./nav";

/** Footer: horizon line, brand, legal links and the data-honesty note. */
export async function SiteFooter() {
  const t = await getTranslations("shell");
  return (
    <footer className="relative mt-24 bg-ocean-900 text-sand-100 on-dark">
      <div aria-hidden="true" className="absolute inset-x-0 top-0 h-px bg-white/20" />
      <div className="mx-auto grid max-w-[var(--container-page)] grid-cols-[1.4fr_1fr] gap-12 px-8 py-14 min-[90rem]:px-12">
        <div className="flex flex-col gap-4">
          <Logo variant="full" size={36} tone="inverse" />
          <p className="font-display text-lead text-sand-100">{t("footer.tagline")}</p>
          <p className="max-w-lg text-small text-sand-300">{t("footer.honesty")}</p>
          <p className="text-small text-sand-300">{t("footer.timeNote")}</p>
        </div>
        <nav aria-label={t("footerNav")} className="justify-self-end">
          <p className="mb-3 text-caption font-medium tracking-wide text-sand-400 uppercase">
            {t("footer.company")}
          </p>
          <ul className="flex flex-col gap-2">
            {footerLinks.map((l) => (
              <li key={l.key}>
                <Link
                  href={l.href}
                  className="rounded-xs text-small text-sand-100 underline-offset-4 hover:underline"
                >
                  {t(`footer.${l.key}`)}
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      </div>
      <div className="border-t border-white/10">
        <p className="mx-auto max-w-[var(--container-page)] px-8 py-5 text-caption text-sand-400 min-[90rem]:px-12">
          {t("footer.copyright", { year: new Date().getFullYear() })}
        </p>
      </div>
    </footer>
  );
}
