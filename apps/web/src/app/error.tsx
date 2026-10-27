"use client";

import { Button, ErrorState } from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useEffect } from "react";
import { fontClassName } from "@/shell/fonts";
import "@/styles/globals.css";

/** Route-level error boundary: a calm recovery screen, never a stack trace. */
export default function ErrorPage({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const t = useTranslations("errors.error");
  useEffect(() => {
    console.error(error);
  }, [error]);
  return (
    <div className={`${fontClassName} shell-fonts min-h-dvh`}>
      <main id="main" className="mx-auto max-w-[var(--container-page)] px-8 py-24">
        <ErrorState
          title={t("title")}
          description={t("description")}
          action={
            <Button size="lg" onClick={reset}>
              {t("retry")}
            </Button>
          }
          secondaryAction={
            <Button asChild variant="ghost" size="lg">
              <Link href="/">{t("home")}</Link>
            </Button>
          }
        />
      </main>
    </div>
  );
}
