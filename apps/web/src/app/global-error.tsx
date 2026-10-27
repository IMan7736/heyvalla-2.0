"use client";

import { Button, ErrorState } from "@dhathuru/ui";
import { fontClassName } from "@/shell/fonts";
import errors from "../../messages/en/errors.json";
import "@/styles/globals.css";

/** Last-resort boundary that replaces the root layout, so it brings its own <html> and strings. */
export default function GlobalError({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const t = errors.error;
  return (
    <html lang="en" dir="ltr">
      <body>
        <div className={`${fontClassName} shell-fonts min-h-dvh`}>
          <main id="main" className="mx-auto max-w-[var(--container-page)] px-8 py-24">
            <ErrorState
              title={t.title}
              description={t.description}
              action={
                <Button size="lg" onClick={reset}>
                  {t.retry}
                </Button>
              }
            />
          </main>
        </div>
      </body>
    </html>
  );
}
