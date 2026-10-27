"use client";

import { useState, type FormEvent, type ReactNode } from "react";
import { useTranslations } from "next-intl";
import { Alert, Button, Field, Input } from "@dhathuru/ui";
import { postJson, type PostResult } from "./post-json";

export type AuthFieldName = "email" | "password" | "newPassword" | "displayName";

type Props = {
  endpoint: string;
  fields: AuthFieldName[];
  submitLabel: string;
  /** Extra values merged into the request body (for example a reset token). */
  extra?: Record<string, string>;
  /** Maps a form field to the API property name, for example `{ newPassword: "password" }`. */
  rename?: Partial<Record<AuthFieldName, string>>;
  onSuccess: (result: Extract<PostResult, { ok: true }>) => void;
  footer?: ReactNode;
};

const AUTOCOMPLETE: Record<AuthFieldName, string> = {
  email: "email",
  password: "current-password",
  newPassword: "new-password",
  displayName: "name",
};

/** Shared form for sign in, register, forgot and reset. Inline validation comes from API field codes. */
export function AuthForm({
  endpoint,
  fields,
  submitLabel,
  extra,
  rename,
  onSuccess,
  footer,
}: Props) {
  const t = useTranslations("account");
  const [values, setValues] = useState<Record<string, string>>({});
  const [reveal, setReveal] = useState(false);
  const [busy, setBusy] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const translate = (code: string) =>
    t.has(`errors.${code}`) ? t(`errors.${code}`) : t("errors.GENERIC");

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setFormError(null);
    setFieldErrors({});
    const body: Record<string, string> = { ...extra };
    for (const [name, value] of Object.entries(values))
      body[rename?.[name as AuthFieldName] ?? name] = value;
    const result = await postJson(endpoint, body);
    setBusy(false);
    if (result.ok) return onSuccess(result);
    if (result.fields.length > 0) {
      const next: Record<string, string> = {};
      const reverse = Object.fromEntries(
        Object.entries(rename ?? {}).map(([form, api]) => [api, form]),
      );
      for (const f of result.fields) next[reverse[f.field] ?? f.field] ??= translate(f.code);
      setFieldErrors(next);
    } else {
      setFormError(translate(result.code));
    }
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-5" aria-busy={busy}>
      {formError ? <Alert tone="danger">{formError}</Alert> : null}
      {fields.map((name) => {
        const isPassword = name === "password" || name === "newPassword";
        return (
          <Field
            key={name}
            label={t(`fields.${name}`)}
            required={name !== "displayName"}
            requiredLabel={t("fields.required")}
            {...(name === "displayName" ? { hint: t("fields.displayNameHint") } : {})}
            {...(name === "newPassword" ? { hint: t("fields.passwordHint") } : {})}
            {...(fieldErrors[name] ? { error: fieldErrors[name] } : {})}
          >
            <Input
              name={name}
              type={isPassword && !reveal ? "password" : name === "email" ? "email" : "text"}
              autoComplete={AUTOCOMPLETE[name]}
              value={values[name] ?? ""}
              onChange={(e) => setValues((v) => ({ ...v, [name]: e.target.value }))}
              {...(name === "email"
                ? { inputMode: "email" as const, autoCapitalize: "none", spellCheck: false }
                : {})}
              {...(isPassword
                ? {
                    end: (
                      <button
                        type="button"
                        className="inline-flex min-h-8 min-w-8 items-center justify-center px-2 text-sm font-medium underline"
                        aria-pressed={reveal}
                        onClick={() => setReveal((r) => !r)}
                      >
                        {reveal ? t("fields.hide") : t("fields.show")}
                      </button>
                    ),
                  }
                : {})}
            />
          </Field>
        );
      })}
      <Button type="submit" size="lg" fullWidth loading={busy}>
        {submitLabel}
      </Button>
      {footer}
    </form>
  );
}
