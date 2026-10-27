"use client";

import { Field, Input, Select } from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import { newRow, rowFromTraveller, type FormErrors, type PassengerRow } from "../lib/passengers";
import type { PassengerType, Traveller } from "../lib/types";

const MANUAL = "manual";
const TYPES: PassengerType[] = ["adult", "child", "infant"];

export function PassengerFields({
  rows,
  onChange,
  errors,
  travellers,
  travellersState,
  showErrors,
}: {
  rows: PassengerRow[];
  onChange: (rows: PassengerRow[]) => void;
  errors: FormErrors;
  travellers: Traveller[];
  travellersState: "loading" | "ready" | "error";
  showErrors: boolean;
}) {
  const t = useTranslations("booking.start");
  const update = (index: number, patch: Partial<PassengerRow>) =>
    onChange(rows.map((r, i) => (i === index ? { ...r, ...patch } : r)));
  const usedIds = new Set(rows.map((r) => r.travellerId).filter(Boolean));

  return (
    <div className="space-y-5">
      {travellersState === "error" ? (
        <p role="status" className="text-small text-ink-muted">
          {t("travellers.savedError")}
        </p>
      ) : null}
      {rows.map((row, index) => {
        const rowErrors = showErrors ? (errors.rows[index] ?? {}) : {};
        const number = index + 1;
        return (
          <fieldset
            key={row.key}
            className="rounded-lg bg-surface p-5 shadow-[var(--shadow-hairline)]"
          >
            <legend className="px-1 text-lead font-semibold text-ink">
              {t("travellers.passenger", { number })}
            </legend>
            <div className="grid grid-cols-2 gap-x-5 gap-y-4">
              {travellers.length > 0 ? (
                <div className="col-span-2">
                  <Field label={t("travellers.saved")}>
                    <Select
                      value={row.travellerId ?? MANUAL}
                      placeholder={t("travellers.savedNone")}
                      onValueChange={(value) => {
                        const found = travellers.find((x) => x.id === value);
                        onChange(
                          rows.map((r, i) => {
                            if (i !== index) return r;
                            return found
                              ? rowFromTraveller(found, r.key)
                              : newRow({ key: r.key, passengerType: r.passengerType });
                          }),
                        );
                      }}
                      options={[
                        { value: MANUAL, label: t("travellers.savedNone") },
                        ...travellers.map((x) => ({
                          value: x.id,
                          label: x.fullName,
                          disabled: usedIds.has(x.id) && x.id !== row.travellerId,
                        })),
                      ]}
                    />
                  </Field>
                </div>
              ) : null}
              <div className="col-span-2">
                <Field
                  label={t("travellers.fullName")}
                  required
                  requiredLabel={t("travellers.required")}
                  {...(rowErrors.fullName ? { error: t(`errors.${rowErrors.fullName}`) } : {})}
                >
                  <Input
                    value={row.fullName}
                    placeholder={t("travellers.fullNamePlaceholder")}
                    autoComplete={index === 0 ? "name" : "off"}
                    onChange={(e) => update(index, { fullName: e.target.value })}
                  />
                </Field>
              </div>
              <Field label={t("travellers.type")}>
                <Select
                  value={row.passengerType}
                  placeholder={t("travellers.type")}
                  disabled
                  onValueChange={(value) =>
                    update(index, { passengerType: value as PassengerType })
                  }
                  options={TYPES.map((type) => ({ value: type, label: t(`types.${type}`) }))}
                />
              </Field>
              <Field
                label={t("travellers.phone")}
                hint={t("travellers.phoneHint")}
                {...(rowErrors.phone ? { error: t(`errors.${rowErrors.phone}`) } : {})}
              >
                <Input
                  type="tel"
                  inputMode="tel"
                  value={row.phone}
                  autoComplete={index === 0 ? "tel" : "off"}
                  onChange={(e) => update(index, { phone: e.target.value })}
                />
              </Field>
            </div>
          </fieldset>
        );
      })}
      {showErrors && errors.party ? (
        <p role="alert" className="text-small text-danger">
          {t(`errors.${errors.party}`)}
        </p>
      ) : null}
    </div>
  );
}
