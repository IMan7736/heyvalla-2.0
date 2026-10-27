"use client";

import {
  Button,
  Card,
  Checkbox,
  DatePicker,
  Field,
  Icon,
  IconButton,
  Input,
  NumberStepper,
  Popover,
  PopoverContent,
  PopoverTrigger,
  SegmentedControl,
  Select,
  modeIcon,
} from "@dhathuru/ui";
import { useLocale, useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { useQueryStates } from "nuqs";
import { Suspense, useId, useState } from "react";
import { maldivesToday } from "../lib/format";
import {
  RISK_TOLERANCES,
  searchParsers,
  toSearchParams,
  validateSearch,
  type SearchIssue,
  type SearchState,
} from "../lib/search-state";
import { TRANSPORT_MODES } from "../lib/types";
import { PlacePicker } from "./place-picker";
import { PlannerProviders } from "./planner-providers";

const RISK_KEYS = {
  cautious: "riskCautious",
  balanced: "riskBalanced",
  relaxed: "riskRelaxed",
} as const;

function SearchFormBody({ compact = false }: { compact?: boolean }) {
  const t = useTranslations("planner.form");
  const tModes = useTranslations("planner.modes");
  const tUi = useTranslations("ui");
  const locale = useLocale();
  const router = useRouter();
  const uid = useId();
  const [urlState] = useQueryStates(searchParsers);
  const [draft, setDraft] = useState<SearchState>(urlState);
  const [issues, setIssues] = useState<SearchIssue[]>([]);
  const [prefsOpen, setPrefsOpen] = useState(false);
  const [pending, setPending] = useState(false);
  const urlKey = toSearchParams(urlState).toString();

  // Back/forward or a shared link changes the URL: the form follows (state adjusted during render).
  const [syncedKey, setSyncedKey] = useState(urlKey);
  if (syncedKey !== urlKey) {
    setSyncedKey(urlKey);
    setDraft(urlState);
    setPending(false);
  }

  const update = (patch: Partial<SearchState>) => setDraft((d) => ({ ...d, ...patch }));
  const today = maldivesToday();
  const pax = draft.adults + draft.children + draft.infants;

  function submit(e: React.FormEvent) {
    e.preventDefault();
    const found = validateSearch(draft, today);
    setIssues(found);
    if (found.length > 0) return;
    setPending(true);
    router.push(`/results?${toSearchParams(draft)}`);
  }

  const issueText = (...codes: SearchIssue[]) => {
    const found = issues.find((i) => codes.includes(i));
    return found ? t(`issue.${found}`) : undefined;
  };
  const fromError = issueText("MISSING_FROM");
  const toError = issueText("MISSING_TO", "SAME_PLACE");
  const dateError = issueText("BAD_DATE", "PAST_DATE");
  const timeError = issueText("BAD_TIME");

  const stepper = (key: "adults" | "children" | "infants", min: number) => (
    <NumberStepper
      label={t(key)}
      value={draft[key]}
      min={min}
      max={9}
      decrementLabel={t("decrease", { group: t(key) })}
      incrementLabel={t("increase", { group: t(key) })}
      onValueChange={(v) => update({ [key]: v })}
    />
  );

  return (
    <Card elevation="raised" padding="lg">
      <form onSubmit={submit} noValidate aria-label={t("title")} className="flex flex-col gap-5">
        {!compact ? (
          <header>
            <h1 className="font-display text-title text-ink">{t("title")}</h1>
            <p className="mt-1 text-small text-ink-muted">{t("subtitle")}</p>
          </header>
        ) : null}

        <div className="grid grid-cols-[1fr_auto_1fr] items-start gap-3">
          <PlacePicker
            label={t("from")}
            placeholder={t("fromPlaceholder")}
            placeId={draft.from}
            error={fromError}
            onSelect={(p) => update({ from: p?.id ?? null })}
          />
          <IconButton
            icon="swap"
            label={t("swap")}
            variant="outline"
            className="mt-7"
            onClick={() => update({ from: draft.to, to: draft.from })}
          />
          <PlacePicker
            label={t("to")}
            placeholder={t("toPlaceholder")}
            placeId={draft.to}
            error={toError}
            onSelect={(p) => update({ to: p?.id ?? null })}
          />
        </div>

        <div className="grid grid-cols-[1.4fr_1fr_1fr] items-start gap-3">
          <Field label={t("date")} {...(dateError ? { error: dateError } : {})}>
            <DatePicker
              size="lg"
              value={draft.date}
              min={today}
              locale={locale}
              placeholder={t("datePlaceholder")}
              onValueChange={(v) => update({ date: v })}
              labels={{
                dialog: tUi("chooseDate"),
                previousMonth: tUi("previousMonth"),
                nextMonth: tUi("nextMonth"),
              }}
            />
          </Field>
          <Field label={t("time")} {...(timeError ? { error: timeError } : {})}>
            <Input
              size="lg"
              type="time"
              value={draft.time}
              onChange={(e) => update({ time: e.target.value })}
            />
          </Field>
          <div>
            <span className="mb-1.5 block text-small font-medium text-ink">{t("passengers")}</span>
            <Popover>
              <PopoverTrigger asChild>
                <Button
                  variant="secondary"
                  size="lg"
                  fullWidth
                  trailingIcon="chevron-down"
                  leadingIcon="users"
                >
                  {t("passengersSummary", { count: pax })}
                </Button>
              </PopoverTrigger>
              <PopoverContent align="end" className="flex w-80 flex-col gap-4">
                {stepper("adults", 1)}
                {stepper("children", 0)}
                {stepper("infants", 0)}
              </PopoverContent>
            </Popover>
          </div>
        </div>

        <SegmentedControl
          label={t("timeMode")}
          value={draft.mode}
          onValueChange={(v) => update({ mode: v as SearchState["mode"] })}
          options={[
            { value: "departAfter", label: t("departAfter") },
            { value: "arriveBy", label: t("arriveBy") },
          ]}
        />

        <div className="border-t border-line pt-4">
          <button
            type="button"
            aria-expanded={prefsOpen}
            aria-controls={`${uid}-prefs`}
            onClick={() => setPrefsOpen((o) => !o)}
            className="inline-flex items-center gap-1.5 rounded-xs text-small font-semibold text-link underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
          >
            {t("preferences")}
            <Icon name={prefsOpen ? "chevron-up" : "chevron-down"} size={16} />
          </button>
          <div id={`${uid}-prefs`} hidden={!prefsOpen} className="mt-4 flex flex-col gap-5">
            <p className="text-small text-ink-muted">{t("preferencesHint")}</p>
            <div className="grid grid-cols-2 gap-4">
              <Field label={t("maxTransfers")}>
                <Select
                  placeholder={t("anyTransfers")}
                  value={draft.transfers === null ? "any" : String(draft.transfers)}
                  onValueChange={(v) => update({ transfers: v === "any" ? null : Number(v) })}
                  options={[
                    { value: "any", label: t("anyTransfers") },
                    ...[0, 1, 2, 3, 4].map((n) => ({
                      value: String(n),
                      label: t("transfersCount", { count: n }),
                    })),
                  ]}
                />
              </Field>
              <Field label={t("budget")}>
                <Input
                  inputMode="numeric"
                  value={draft.budget ?? ""}
                  placeholder={t("budgetPlaceholder")}
                  onChange={(e) => {
                    const digits = e.target.value.replace(/\D/g, "");
                    update({ budget: digits === "" ? null : Math.min(9_999_999, Number(digits)) });
                  }}
                />
              </Field>
            </div>
            <fieldset>
              <legend className="text-small font-medium text-ink">{t("modes")}</legend>
              <p className="mb-2 text-caption text-ink-muted">{t("modesHint")}</p>
              <div className="flex flex-wrap gap-x-5 gap-y-3">
                {TRANSPORT_MODES.filter((m) => m !== "walk").map((m) => {
                  const on = draft.modes.includes(m);
                  return (
                    <Checkbox
                      key={m}
                      checked={on}
                      onCheckedChange={() =>
                        update({
                          modes: on ? draft.modes.filter((x) => x !== m) : [...draft.modes, m],
                        })
                      }
                      label={
                        <span className="inline-flex items-center gap-2">
                          <Icon name={modeIcon[m]} size={16} />
                          {tModes(m)}
                        </span>
                      }
                    />
                  );
                })}
              </div>
            </fieldset>
            <fieldset>
              <legend className="mb-2 text-small font-medium text-ink">{t("risk")}</legend>
              <div className="grid grid-cols-3 gap-3">
                {RISK_TOLERANCES.map((r) => (
                  <label
                    key={r}
                    className={`cursor-pointer rounded-md border p-3 transition has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-action ${
                      draft.risk === r
                        ? "border-action bg-lagoon-50"
                        : "border-line bg-surface hover:border-ink-muted"
                    }`}
                  >
                    <input
                      type="radio"
                      name={`${uid}-risk`}
                      value={r}
                      checked={draft.risk === r}
                      onChange={() => update({ risk: r })}
                      className="sr-only"
                    />
                    <span className="block text-small font-semibold text-ink">
                      {t(RISK_KEYS[r])}
                    </span>
                    <span className="mt-1 block text-caption leading-snug text-ink-muted">
                      {t(`${RISK_KEYS[r]}Hint`)}
                    </span>
                  </label>
                ))}
              </div>
            </fieldset>
          </div>
        </div>

        <Button type="submit" size="xl" fullWidth loading={pending} leadingIcon="search">
          {pending ? t("searching") : t("search")}
        </Button>
      </form>
    </Card>
  );
}

/** Self-contained: brings its own URL-state and query providers, so any page (landing included) can embed it. */
export function TripSearchForm({ compact = false }: { compact?: boolean }) {
  return (
    <PlannerProviders>
      <Suspense fallback={null}>
        <SearchFormBody compact={compact} />
      </Suspense>
    </PlannerProviders>
  );
}

export { SearchFormBody };
