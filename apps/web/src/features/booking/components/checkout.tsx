"use client";

import {
  Alert,
  Button,
  Card,
  CardTitle,
  ErrorState,
  Icon,
  Skeleton,
  Spinner,
  formatMoney,
} from "@dhathuru/ui";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { BookingApiError, bookingApi, newIdempotencyKey } from "../lib/api";
import { bookingKeys, useBooking, useStoreBooking } from "../lib/hooks";
import { failureKey, paymentSettled } from "../lib/status";
import type { Booking } from "../lib/types";
import { BookingOutcome } from "./booking-outcome";
import { BookingProviders } from "./booking-providers";
import { BookingSteps } from "./book-start";
import { CardForm } from "./card-form";
import { HoldCountdown } from "./hold-countdown";
import { LegLine } from "./leg-line";
import { PriceBreakdown } from "./price-breakdown";
import { VerificationForm } from "./verification-form";

const PAY_PROBLEMS = new Set([
  "HOLD_EXPIRED",
  "PAYMENT_IN_PROGRESS",
  "NO_VERIFICATION_PENDING",
  "BOOKING_NOT_PAYABLE",
  "INVALID_PAYMENT_TOKEN",
  "EMAIL_NOT_VERIFIED",
  "FORBIDDEN",
  "AUTH_REQUIRED",
  "NETWORK",
]);

export function Checkout({ bookingId }: { bookingId: string }) {
  return (
    <BookingProviders>
      <CheckoutInner bookingId={bookingId} />
    </BookingProviders>
  );
}

function CheckoutInner({ bookingId }: { bookingId: string }) {
  const t = useTranslations("booking.checkout");
  const router = useRouter();
  const client = useQueryClient();
  const store = useStoreBooking();
  // Set while a pay request may have reached the server but we have no answer: keep polling until the booking settles.
  // `baseline` is the id of the latest payment when it started, so an older failed attempt is not mistaken for the answer.
  const [uncertain, setUncertain] = useState<{ baseline: string | null } | null>(null);
  const booking = useBooking(bookingId, {
    pollWhile: (d) => uncertain !== null && !paymentSettled(d, uncertain.baseline),
  });
  const payKey = useRef<string | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [wrongCode, setWrongCode] = useState(false);

  const apply = (updated: Booking) => {
    store(updated);
    setUncertain(null);
  };
  const fail = (error: unknown) => {
    if (error instanceof BookingApiError) {
      if (error.isUncertain) {
        setUncertain({ baseline: booking.data?.payment?.id ?? null });
        return;
      }
      // A definite answer ends this attempt: the next card gets a fresh idempotency key.
      payKey.current = null;
      setProblem(PAY_PROBLEMS.has(error.code) ? error.code : "UNKNOWN");
      if (error.code === "HOLD_EXPIRED")
        void client.invalidateQueries({ queryKey: bookingKeys.one(bookingId) });
    } else {
      setProblem("UNKNOWN");
    }
  };

  const pay = useMutation({
    mutationFn: (token: string) => {
      payKey.current ??= newIdempotencyKey();
      return bookingApi.pay(bookingId, token, payKey.current);
    },
    onMutate: () => setProblem(null),
    onSuccess: (updated) => {
      payKey.current = null;
      apply(updated);
    },
    onError: fail,
  });
  const verify = useMutation({
    mutationFn: (code: string) => bookingApi.verify(bookingId, code),
    onMutate: () => {
      setProblem(null);
      setWrongCode(false);
    },
    onSuccess: (updated) => {
      apply(updated);
      if (updated.payment?.failureCode === "VERIFICATION_FAILED") setWrongCode(true);
    },
    onError: (error) => {
      if (error instanceof BookingApiError && error.status === 422) setWrongCode(true);
      else fail(error);
    },
  });

  const data = booking.data;
  useEffect(() => {
    if (data?.status === "confirmed") router.replace(`/book/done/${encodeURIComponent(data.id)}`);
  }, [data?.status, data?.id, router]);
  const checking = uncertain !== null && !paymentSettled(data, uncertain.baseline);

  return (
    <Page>
      {booking.isPending ? (
        <div
          role="status"
          aria-busy="true"
          aria-label={t("loading")}
          className="grid grid-cols-[1fr_24rem] gap-8"
        >
          <Skeleton className="h-96 w-full" />
          <Skeleton className="h-80 w-full" />
        </div>
      ) : booking.isError ? (
        booking.error instanceof BookingApiError && booking.error.status === 404 ? (
          <ErrorState
            title={t("notFound.title")}
            description={t("notFound.description")}
            action={
              <Button asChild>
                <Link href="/trips">{t("notFound.action")}</Link>
              </Button>
            }
          />
        ) : (
          <ErrorState
            title={t("loadError.title")}
            description={t("loadError.description")}
            action={<Button onClick={() => void booking.refetch()}>{t("loadError.retry")}</Button>}
          />
        )
      ) : (
        <CheckoutBody
          booking={booking.data}
          processing={pay.isPending || verify.isPending}
          checking={checking}
          problem={problem}
          wrongCode={wrongCode}
          onPay={(token) => pay.mutate(token)}
          onVerify={(code) => verify.mutate(code)}
          onExpired={() => void booking.refetch()}
        />
      )}
    </Page>
  );
}

function CheckoutBody({
  booking,
  processing,
  checking,
  problem,
  wrongCode,
  onPay,
  onVerify,
  onExpired,
}: {
  booking: Booking;
  processing: boolean;
  checking: boolean;
  problem: string | null;
  wrongCode: boolean;
  onPay: (token: string) => void;
  onVerify: (code: string) => void;
  onExpired: () => void;
}) {
  const t = useTranslations("booking.checkout");
  const tFail = useTranslations("booking.failure");

  if (booking.status === "confirmed") {
    return (
      <div
        role="status"
        className="flex items-center justify-center gap-3 py-24 text-body text-ink-muted"
      >
        <Spinner /> {t("processing.status")}
      </div>
    );
  }
  if (
    booking.status === "failed" ||
    booking.status === "expired" ||
    booking.status === "cancelled" ||
    booking.status === "refunded" ||
    booking.status === "partially_cancelled" ||
    booking.status === "draft"
  ) {
    return <BookingOutcome booking={booking} />;
  }

  const payment = booking.payment;
  const needsVerification = payment?.status === "requires_action" && payment.challenge;
  const midFlight = processing || checking || booking.status === "payment_pending";
  const lastFailure = payment?.status === "failed" ? payment.failureCode : null;
  const amountLabel = formatMoney(booking.total);

  let main: React.ReactNode;
  if (needsVerification && !processing) {
    main = <VerificationForm busy={processing} wrong={wrongCode} onSubmit={onVerify} />;
  } else if (midFlight) {
    main = (
      <div
        role="status"
        aria-live="polite"
        aria-busy="true"
        className="flex flex-col items-center gap-4 px-6 py-16 text-center"
      >
        <Spinner size={40} />
        <h2 className="font-display text-title text-ink">
          {checking ? t("checking.title") : t("processing.title")}
        </h2>
        <p className="max-w-md text-body text-ink-muted">
          {checking ? t("checking.description") : t("processing.description")}
        </p>
      </div>
    );
  } else {
    main = (
      <div className="space-y-5">
        {lastFailure ? (
          <Alert tone="danger" title={t("failure.title")}>
            {tFail(failureKey(lastFailure) as "UNKNOWN")} {t("failure.keepTrying")}
          </Alert>
        ) : null}
        {problem ? (
          <Alert tone="danger">{t(`problems.${problem}` as "problems.UNKNOWN")}</Alert>
        ) : null}
        <CardForm amountLabel={amountLabel} busy={processing} onPay={onPay} />
      </div>
    );
  }
  if (needsVerification && !processing && problem) {
    main = (
      <div className="space-y-5">
        <Alert tone="danger">{t(`problems.${problem}` as "problems.UNKNOWN")}</Alert>
        {main}
      </div>
    );
  }

  return (
    <div className="grid grid-cols-[minmax(0,1fr)_24rem] items-start gap-8">
      <section aria-labelledby="pay-title">
        <h2 id="pay-title" className="mb-5 font-display text-title text-ink">
          {t("card.title")}
        </h2>
        {main}
      </section>
      <aside className="sticky top-6 space-y-4" aria-label={t("summaryTitle")}>
        {booking.holdExpiresAt ? (
          <HoldCountdown expiresAt={booking.holdExpiresAt} onExpired={onExpired} />
        ) : null}
        <Card elevation="rest" padding="md">
          <CardTitle className="mb-4">{t("summaryTitle")}</CardTitle>
          <ol className="mb-5 space-y-4">
            {booking.legs.map((l) => (
              <li key={l.id}>
                <LegLine leg={l.leg} number={l.legIndex + 1} trailing={<span />} />
              </li>
            ))}
          </ol>
          <PriceBreakdown
            lines={booking.priceBreakdown}
            total={booking.total}
            totalLabel={t("total")}
          />
          <p className="mt-3 flex items-start gap-2 text-caption text-ink-muted">
            <Icon name="info" size={14} className="mt-0.5 shrink-0" /> {t("subtotalNote")}
          </p>
        </Card>
      </aside>
    </div>
  );
}

function Page({ children }: { children: React.ReactNode }) {
  const t = useTranslations("booking.checkout");
  return (
    <div className="mx-auto max-w-[1184px] px-8 pb-24 pt-8">
      <header className="mb-8 space-y-6">
        <h1 className="font-display text-display text-ink">{t("title")}</h1>
        <BookingSteps current="payment" />
      </header>
      {children}
    </div>
  );
}
