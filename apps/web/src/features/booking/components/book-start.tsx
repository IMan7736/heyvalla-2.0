"use client";

import {
  Alert,
  Button,
  Card,
  CardTitle,
  EmptyState,
  ErrorState,
  Icon,
  Skeleton,
  Stepper,
  formatMoney,
} from "@dhathuru/ui";
import { useMutation } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { BookingApiError, bookingApi, newIdempotencyKey } from "../lib/api";
import { useSavedTravellers, useStoreBooking, useTripOption } from "../lib/hooks";
import {
  SOLO,
  estimateFares,
  hasErrors,
  rowsForParty,
  toInputs,
  validateRows,
  type Party,
  type PassengerRow,
} from "../lib/passengers";
import { BookingProviders } from "./booking-providers";
import { LegLine } from "./leg-line";
import { PassengerFields } from "./passenger-fields";

const HOLD_MINUTES = 12;
const KNOWN_PROBLEMS = new Set([
  "LEG_UNAVAILABLE",
  "OPTION_EXPIRED",
  "OPTION_NOT_FOUND",
  "NOTHING_TO_BOOK",
  "VALIDATION_FAILED",
  "EMAIL_NOT_VERIFIED",
  "FORBIDDEN",
  "AUTH_REQUIRED",
  "NETWORK",
]);

export function BookStart({ optionId, party }: { optionId: string | null; party: Party | null }) {
  return (
    <BookingProviders>
      <BookStartInner optionId={optionId} party={party ?? SOLO} />
    </BookingProviders>
  );
}

export function BookingSteps({ current }: { current: "travellers" | "payment" | "tickets" }) {
  const t = useTranslations("booking.steps");
  return (
    <Stepper
      label={t("label")}
      completeLabel={t("complete")}
      currentLabel={t("current")}
      currentId={current}
      steps={[
        { id: "travellers", label: t("travellers") },
        { id: "payment", label: t("payment") },
        { id: "tickets", label: t("tickets") },
      ]}
    />
  );
}

function BookStartInner({ optionId, party }: { optionId: string | null; party: Party }) {
  const t = useTranslations("booking.start");
  const router = useRouter();
  const store = useStoreBooking();
  const option = useTripOption(optionId);
  const saved = useSavedTravellers();
  const [rows, setRows] = useState<PassengerRow[]>(() => rowsForParty(party));
  const [showErrors, setShowErrors] = useState(false);
  // One key per distinct request body: retrying the same form never books twice, editing it starts a new request.
  const keyRef = useRef<{ body: string; key: string } | null>(null);
  const errors = validateRows(rows);

  const create = useMutation({
    mutationFn: () => {
      const passengers = toInputs(rows);
      const body = JSON.stringify({ optionId, passengers });
      if (keyRef.current?.body !== body) keyRef.current = { body, key: newIdempotencyKey() };
      return bookingApi.create(optionId ?? "", passengers, keyRef.current.key);
    },
    onSuccess: (booking) => {
      store(booking);
      router.push(`/book/pay/${encodeURIComponent(booking.id)}`);
    },
  });

  if (optionId === null) {
    return (
      <Shell>
        <EmptyState
          title={t("noOption.title")}
          description={t("noOption.description")}
          action={
            <Button asChild>
              <Link href="/plan">{t("noOption.action")}</Link>
            </Button>
          }
        />
      </Shell>
    );
  }
  if (option.isPending) {
    return (
      <Shell>
        <div
          role="status"
          aria-busy="true"
          aria-label={t("loading")}
          className="grid grid-cols-[1fr_24rem] gap-8"
        >
          <div className="space-y-4">
            <Skeleton className="h-8 w-64" />
            <Skeleton className="h-56 w-full" />
          </div>
          <Skeleton className="h-72 w-full" />
        </div>
      </Shell>
    );
  }
  if (option.isError) {
    return (
      <Shell>
        <ErrorState
          title={t("optionError.title")}
          description={t("optionError.description")}
          action={<Button onClick={() => void option.refetch()}>{t("optionError.retry")}</Button>}
          secondaryAction={
            <Button variant="secondary" asChild>
              <Link href="/plan">{t("optionError.search")}</Link>
            </Button>
          }
        />
      </Shell>
    );
  }

  const trip = option.data;
  const estimate = estimateFares(trip.legs);
  const currency = trip.price.total.currency;
  const bookableCount = trip.legs.filter((l) => l.bookable).length;
  const problem = create.error instanceof BookingApiError ? create.error : null;
  const problemKey = problem ? (KNOWN_PROBLEMS.has(problem.code) ? problem.code : "UNKNOWN") : null;
  const legNumber =
    problem && typeof problem.params.legIndex === "number" ? problem.params.legIndex + 1 : 1;

  const submit = () => {
    setShowErrors(true);
    if (hasErrors(errors) || create.isPending) return;
    create.mutate();
  };

  return (
    <Shell>
      <form
        noValidate
        onSubmit={(e) => {
          e.preventDefault();
          submit();
        }}
        className="grid grid-cols-[minmax(0,1fr)_24rem] items-start gap-8"
      >
        <section aria-labelledby="travellers-title" className="space-y-5">
          <div>
            <h2 id="travellers-title" className="font-display text-title text-ink">
              {t("travellers.title")}
            </h2>
            <p className="mt-1 text-body text-ink-muted">{t("travellers.hint")}</p>
          </div>
          <p className="rounded-md bg-lagoon-50 p-3 text-small text-ink">
            {t("travellers.partyNote", {
              adults: party.adults,
              children: party.children,
              infants: party.infants,
            })}{" "}
            <Link href="/plan" className="text-link underline underline-offset-4">
              {t("travellers.changeParty")}
            </Link>
          </p>
          {saved.isPending ? (
            <p role="status" className="text-small text-ink-muted">
              {t("travellers.savedLoading")}
            </p>
          ) : null}
          <PassengerFields
            rows={rows}
            onChange={setRows}
            errors={errors}
            travellers={saved.data ?? []}
            travellersState={saved.isPending ? "loading" : saved.isError ? "error" : "ready"}
            showErrors={showErrors}
          />
        </section>

        <aside className="sticky top-6 space-y-4" aria-label={t("summary.title")}>
          <Card elevation="rest" padding="md">
            <CardTitle className="mb-4">{t("summary.title")}</CardTitle>
            <ol className="space-y-4">
              {trip.legs.map((leg, i) => (
                <li key={leg.id}>
                  <LegLine leg={leg} number={i + 1} />
                  {!leg.bookable ? (
                    <p className="mt-1 ps-14 text-caption text-ink-muted">
                      {t("summary.payOnBoardHint")}
                    </p>
                  ) : null}
                </li>
              ))}
            </ol>
            <dl className="mt-5 space-y-1 border-t border-line pt-4 text-small">
              <div className="flex justify-between gap-4">
                <dt className="text-ink-muted">{t("summary.estimate")}</dt>
                <dd className="tabular font-semibold text-ink">
                  {formatMoney({ amountMinor: estimate, currency })}
                </dd>
              </div>
            </dl>
            <p className="mt-2 text-caption text-ink-muted">{t("summary.estimateNote")}</p>
          </Card>

          {bookableCount === 0 ? (
            <Alert tone="caution">{t("problems.NOTHING_TO_BOOK")}</Alert>
          ) : (
            <p className="flex items-start gap-2 text-small text-ink-muted">
              <Icon name="clock" size={18} className="mt-0.5 shrink-0" />
              {t("summary.holdNote", { minutes: HOLD_MINUTES })}
            </p>
          )}

          {problemKey ? (
            <Alert tone="danger" action={null}>
              {t(`problems.${problemKey}` as "problems.UNKNOWN", { leg: legNumber })}
              {problemKey === "LEG_UNAVAILABLE" ||
              problemKey === "OPTION_EXPIRED" ||
              problemKey === "OPTION_NOT_FOUND" ? (
                <span className="mt-2 block">
                  <Link href="/plan" className="text-link underline underline-offset-4">
                    {t("optionError.search")}
                  </Link>
                </span>
              ) : null}
            </Alert>
          ) : null}
          {showErrors && hasErrors(errors) ? (
            <p role="alert" className="text-small text-danger">
              {t("errors.fixHighlighted")}
            </p>
          ) : null}

          <Button
            type="submit"
            size="lg"
            fullWidth
            loading={create.isPending}
            disabled={bookableCount === 0}
          >
            {create.isPending ? t("submitting") : t("submit")}
          </Button>
          <Button variant="ghost" fullWidth asChild>
            <Link href={`/trip/${encodeURIComponent(trip.id)}`}>
              <Icon name="arrow-left" size={16} /> {t("back")}
            </Link>
          </Button>
        </aside>
      </form>
    </Shell>
  );
}

function Shell({ children }: { children: React.ReactNode }) {
  const t = useTranslations("booking.start");
  return (
    <div className="mx-auto max-w-[1184px] px-8 pb-24 pt-8">
      <header className="mb-8 space-y-6">
        <div>
          <h1 className="font-display text-display text-ink">{t("title")}</h1>
          <p className="mt-1 text-body text-ink-muted">{t("subtitle")}</p>
        </div>
        <BookingSteps current="travellers" />
      </header>
      {children}
    </div>
  );
}
