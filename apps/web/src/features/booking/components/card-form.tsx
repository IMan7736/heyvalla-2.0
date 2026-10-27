"use client";

import { Alert, Button, Field, Icon, Input } from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import { useState } from "react";
import {
  TEST_CARDS,
  formatCardNumber,
  formatExpiry,
  tokenForCard,
  validateCard,
  type CardErrors,
  type CardForm as CardValues,
} from "../lib/cards";

const EMPTY: CardValues = { name: "", number: "", expiry: "", cvc: "" };

/**
 * Fake-gateway card form. Validates locally, maps the test card to a gateway token in the browser, and hands
 * ONLY the token to `onPay`. The number is wiped from state as soon as it is submitted.
 */
export function CardForm({
  amountLabel,
  busy,
  onPay,
  defaultName,
}: {
  amountLabel: string;
  busy: boolean;
  onPay: (token: string) => void;
  defaultName?: string;
}) {
  const t = useTranslations("booking.checkout");
  const [values, setValues] = useState<CardValues>({ ...EMPTY, name: defaultName ?? "" });
  const [errors, setErrors] = useState<CardErrors>({});
  const [showTest, setShowTest] = useState(false);
  const set = (patch: Partial<CardValues>) => setValues((v) => ({ ...v, ...patch }));
  const message = (field: "name" | "number" | "expiry" | "cvc") => {
    const code = errors[field];
    return code
      ? t(
          `card.errors.${field}${code === "required" ? "Required" : code === "notTestCard" ? "NotTestCard" : "Invalid"}` as "card.errors.nameRequired",
        )
      : undefined;
  };

  const submit = () => {
    const found = validateCard(values);
    setErrors(found);
    if (Object.keys(found).length > 0 || busy) return;
    const token = tokenForCard(values.number);
    if (!token) return;
    setValues((v) => ({ ...v, number: "", cvc: "" }));
    onPay(token);
  };

  const fillTest = (number: string) => {
    const year = (new Date().getFullYear() + 2) % 100;
    setValues((v) => ({
      name: v.name.trim() ? v.name : "Test Traveller",
      number: formatCardNumber(number),
      expiry: `12/${String(year).padStart(2, "0")}`,
      cvc: "123",
    }));
    setErrors({});
  };

  return (
    <form
      noValidate
      onSubmit={(e) => {
        e.preventDefault();
        submit();
      }}
      className="space-y-5"
      aria-label={t("card.title")}
    >
      <Alert tone="info" title={t("testMode.title")}>
        <p>{t("testMode.description")}</p>
        <button
          type="button"
          aria-expanded={showTest}
          onClick={() => setShowTest((s) => !s)}
          className="mt-2 text-link underline underline-offset-4"
        >
          {showTest ? t("testMode.hide") : t("testMode.show")}
        </button>
        {showTest ? (
          <ul className="mt-3 space-y-2">
            {TEST_CARDS.map((card) => (
              <li key={card.number} className="flex items-center justify-between gap-3">
                <span>
                  <span className="tabular font-medium">{formatCardNumber(card.number)}</span>
                  <span className="block text-caption text-ink-muted">
                    {t(`testMode.cards.${card.scenario}`)}
                  </span>
                </span>
                <Button
                  type="button"
                  size="sm"
                  variant="secondary"
                  onClick={() => fillTest(card.number)}
                >
                  {t("testMode.use")}
                </Button>
              </li>
            ))}
          </ul>
        ) : null}
      </Alert>

      <div className="grid grid-cols-2 gap-x-5 gap-y-4">
        <div className="col-span-2">
          <Field
            label={t("card.name")}
            required
            {...(message("name") ? { error: message("name") } : {})}
          >
            <Input
              value={values.name}
              autoComplete="cc-name"
              onChange={(e) => set({ name: e.target.value })}
            />
          </Field>
        </div>
        <div className="col-span-2">
          <Field
            label={t("card.number")}
            required
            {...(message("number") ? { error: message("number") } : {})}
          >
            <Input
              value={values.number}
              inputMode="numeric"
              autoComplete="cc-number"
              start={<Icon name="card" size={18} />}
              onChange={(e) => set({ number: formatCardNumber(e.target.value) })}
            />
          </Field>
        </div>
        <Field
          label={t("card.expiry")}
          required
          {...(message("expiry") ? { error: message("expiry") } : {})}
        >
          <Input
            value={values.expiry}
            inputMode="numeric"
            autoComplete="cc-exp"
            placeholder="MM/YY"
            onChange={(e) => set({ expiry: formatExpiry(e.target.value) })}
          />
        </Field>
        <Field
          label={t("card.cvc")}
          required
          {...(message("cvc") ? { error: message("cvc") } : {})}
        >
          <Input
            value={values.cvc}
            inputMode="numeric"
            autoComplete="cc-csc"
            maxLength={4}
            onChange={(e) => set({ cvc: e.target.value.replace(/\D/g, "") })}
          />
        </Field>
      </div>

      <p className="flex items-center gap-2 text-caption text-ink-muted">
        <Icon name="lock" size={14} /> {t("card.secureNote")}
      </p>
      <Button type="submit" size="xl" fullWidth loading={busy}>
        {t("pay", { amount: amountLabel })}
      </Button>
    </form>
  );
}
