"use client";

import { Button, Field, Input } from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { VERIFICATION_CODE_HINT } from "../lib/cards";

export function VerificationForm({
  busy,
  wrong,
  onSubmit,
}: {
  busy: boolean;
  wrong: boolean;
  onSubmit: (code: string) => void;
}) {
  const t = useTranslations("booking.checkout.verify");
  const [code, setCode] = useState("");
  const [empty, setEmpty] = useState(false);
  const error = empty ? t("errors.required") : wrong ? t("errors.wrong") : undefined;
  return (
    <form
      noValidate
      className="space-y-5"
      onSubmit={(e) => {
        e.preventDefault();
        if (code.trim().length === 0) {
          setEmpty(true);
          return;
        }
        setEmpty(false);
        onSubmit(code.trim());
      }}
    >
      <div>
        <h2 className="font-display text-title text-ink">{t("title")}</h2>
        <p className="mt-1 text-body text-ink-muted">{t("description")}</p>
        <p className="mt-2 text-small text-ink-muted">
          {t("testHint", { code: VERIFICATION_CODE_HINT })}
        </p>
      </div>
      <Field label={t("code")} required {...(error ? { error } : {})}>
        <Input
          value={code}
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={12}
          autoFocus
          onChange={(e) => setCode(e.target.value.replace(/\D/g, ""))}
        />
      </Field>
      <Button type="submit" size="lg" fullWidth loading={busy}>
        {t("submit")}
      </Button>
    </form>
  );
}
