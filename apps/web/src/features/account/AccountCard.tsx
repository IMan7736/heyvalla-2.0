import type { ReactNode } from "react";

/** Centered card used by every account page. Pages supply the heading text. */
export function AccountCard({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle?: string;
  children: ReactNode;
}) {
  return (
    <main id="main" className="mx-auto flex w-full max-w-md flex-col gap-6 px-6 py-16">
      <header className="flex flex-col gap-2">
        <h1 className="font-display text-3xl text-ink">{title}</h1>
        {subtitle ? <p className="text-ink-muted">{subtitle}</p> : null}
      </header>
      <div className="rounded-2xl bg-surface p-6 shadow-sm">{children}</div>
    </main>
  );
}
