import type { ReactNode } from "react";
import { cn } from "../lib/cn";
import { DriftingDhoniArt, EmptyLagoonArt, ReefSnagArt } from "../illustrations/StateArt";

type StateViewProps = {
  title: string;
  description?: string;
  /** Primary next step (Button). */
  action?: ReactNode;
  secondaryAction?: ReactNode;
  /** Heading level of the title; use 1 when the state is the whole page (default 2). */
  headingLevel?: 1 | 2 | 3;
  className?: string;
};

function StateView({
  art,
  title,
  description,
  action,
  secondaryAction,
  className,
  headingLevel = 2,
  role,
}: StateViewProps & { art: ReactNode; role?: "alert" | "status" }) {
  const Heading = `h${headingLevel}` as const;
  return (
    <div
      role={role}
      className={cn("flex flex-col items-center gap-4 px-6 py-12 text-center", className)}
    >
      <div className="w-40">{art}</div>
      <div className="flex max-w-md flex-col gap-1.5">
        <Heading className="font-display text-title text-ink">{title}</Heading>
        {description ? <p className="text-body text-ink-muted">{description}</p> : null}
      </div>
      {action || secondaryAction ? (
        <div className="mt-2 flex items-center gap-3">
          {action}
          {secondaryAction}
        </div>
      ) : null}
    </div>
  );
}

/** Nothing here yet (no trips, no results). Always offer the next step. */
export function EmptyState(props: StateViewProps) {
  return <StateView {...props} art={<EmptyLagoonArt className="w-full" />} />;
}

/** Something failed. Say what happened and offer retry. */
export function ErrorState(props: StateViewProps) {
  return <StateView {...props} role="alert" art={<ReefSnagArt className="w-full" />} />;
}

/** No connection. Say what still works. */
export function OfflineState(props: StateViewProps) {
  return <StateView {...props} role="status" art={<DriftingDhoniArt className="w-full" />} />;
}
