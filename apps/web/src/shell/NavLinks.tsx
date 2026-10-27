"use client";

import { cn } from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { primaryNav } from "./nav";

export function NavLinks() {
  const t = useTranslations("shell");
  const pathname = usePathname();
  return (
    <nav aria-label={t("primaryNav")}>
      <ul className="flex items-center gap-1">
        {primaryNav.map((item) => {
          const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
          return (
            <li key={item.key}>
              <Link
                href={item.href}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "relative inline-flex h-10 items-center rounded-md px-4 text-small font-medium transition-colors duration-[var(--duration-quick)]",
                  active ? "text-lagoon-700" : "text-ink hover:bg-ocean/6",
                )}
              >
                {t(`nav.${item.key}`)}
                {active ? (
                  <span
                    aria-hidden="true"
                    className="absolute inset-x-4 -bottom-0.5 h-0.5 rounded-full bg-coral"
                  />
                ) : null}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
