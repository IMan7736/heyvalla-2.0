import {
  BufferMeter,
  Card,
  Icon,
  JourneyRibbon,
  ModeChip,
  Pill,
  RiskGauge,
  Button,
  type IconName,
  type TransportMode,
} from "@dhathuru/ui";
import { getTranslations } from "next-intl/server";
import Link from "next/link";

const container = "mx-auto max-w-[var(--container-page)] px-8 min-[90rem]:px-12";

function SectionHeading({
  eyebrow,
  title,
  lead,
}: {
  eyebrow?: string;
  title: string;
  lead?: string;
}) {
  return (
    <div className="max-w-3xl">
      {eyebrow ? (
        <p className="mb-3 inline-flex items-center gap-2 text-small font-medium text-lagoon-700">
          <span aria-hidden="true" className="size-1.5 rounded-full bg-coral" />
          {eyebrow}
        </p>
      ) : null}
      <h2 className="font-display text-display text-ink text-balance">{title}</h2>
      {lead ? <p className="mt-4 text-lead text-ink-muted">{lead}</p> : null}
    </div>
  );
}

export async function HowItWorks() {
  const t = await getTranslations("home.how");
  const steps: { key: "search" | "check" | "book"; icon: IconName }[] = [
    { key: "search", icon: "search" },
    { key: "check", icon: "shield" },
    { key: "book", icon: "ticket" },
  ];
  return (
    <section className={`${container} pt-8`} aria-labelledby="how-title">
      <div id="how-title">
        <SectionHeading title={t("title")} lead={t("lead")} />
      </div>
      <ol className="mt-12 grid grid-cols-3 gap-6">
        {steps.map((s, i) => (
          <li key={s.key}>
            <Card padding="lg" className="h-full">
              <div className="mb-6 flex items-center justify-between">
                <span className="grid size-12 place-items-center rounded-full bg-lagoon-50 text-lagoon-700">
                  <Icon name={s.icon} size={24} />
                </span>
                <span className="tabular font-display text-headline text-sand-400">{i + 1}</span>
              </div>
              <h3 className="font-display text-title text-ink">{t(`steps.${s.key}.title`)}</h3>
              <p className="mt-2 text-body text-ink-muted">{t(`steps.${s.key}.body`)}</p>
            </Card>
          </li>
        ))}
      </ol>
    </section>
  );
}

export async function NeverMissARide() {
  const t = await getTranslations("home.safe");
  const legs = [
    { id: "l1", mode: "ferry" as const, durationMin: 50, key: "ferry" as const },
    { id: "l2", mode: "taxi" as const, durationMin: 10, key: "taxiToAirport" as const },
    { id: "l3", mode: "domestic_flight" as const, durationMin: 65, key: "flight" as const },
    { id: "l4", mode: "taxi" as const, durationMin: 15, key: "taxiToDestination" as const },
  ].map(({ key, ...leg }, i) => ({
    ...leg,
    durationLabel: t(`example.legs.${key}.duration`),
    description: t(`example.legs.${key}.description`),
    fromLabel: i === 0 ? t("example.from") : undefined,
  }));
  const points = ["buffers", "risk", "why"] as const;
  return (
    <section className={`${container} pt-32`} aria-labelledby="safe-title">
      <div className="grid grid-cols-[1fr_1.1fr] items-center gap-16">
        <div>
          <div id="safe-title">
            <SectionHeading eyebrow={t("eyebrow")} title={t("title")} lead={t("body")} />
          </div>
          <ul className="mt-8 flex flex-col gap-3">
            {points.map((p) => (
              <li key={p} className="flex items-start gap-3 text-body text-ink">
                <Icon name="check-circle" size={22} className="mt-0.5 text-lagoon-600" />
                {t(`points.${p}`)}
              </li>
            ))}
          </ul>
        </div>
        <Card elevation="raised" padding="lg" aria-label={t("example.badge")}>
          <div className="mb-6 flex items-center justify-between">
            <Pill tone="coral" dot>
              {t("example.badge")}
            </Pill>
            <p className="text-caption text-ink-muted">{t("example.caption")}</p>
          </div>
          <JourneyRibbon
            label={t("example.ribbon")}
            legs={legs}
            destinationLabel={t("example.to")}
            variant="detailed"
            buffers={[
              {
                afterLegIndex: 0,
                level: "comfortable",
                label: t("example.buffers.first"),
                levelLabel: t("example.levels.comfortable"),
              },
              {
                afterLegIndex: 1,
                level: "comfortable",
                label: t("example.buffers.second"),
                levelLabel: t("example.levels.comfortable"),
              },
              {
                afterLegIndex: 2,
                level: "ok",
                label: t("example.buffers.third"),
                levelLabel: t("example.levels.ok"),
              },
            ]}
          />
          <div className="mt-8 grid grid-cols-[1fr_auto] items-end gap-10">
            <BufferMeter
              minutes={55}
              requiredMinutes={30}
              comfortMinutes={45}
              label={t("example.bufferLabel")}
              valueText={t("example.bufferValue")}
              requiredLabel={t("example.requiredMark")}
              comfortLabel={t("example.comfortMark")}
            />
            <RiskGauge
              missProbability={0.02}
              level="low"
              label={t("example.riskLabel")}
              levelLabel={t("example.riskLevel")}
              valueText={t("example.riskValue")}
            />
          </div>
        </Card>
      </div>
    </section>
  );
}

export async function Modes() {
  const t = await getTranslations("home.modes");
  const modes: TransportMode[] = [
    "ferry",
    "speedboat",
    "seaplane",
    "domestic_flight",
    "bus",
    "taxi",
    "walk",
  ];
  return (
    <section className={`${container} pt-32`} aria-labelledby="modes-title">
      <div id="modes-title">
        <SectionHeading title={t("title")} lead={t("lead")} />
      </div>
      <ul className="mt-12 grid grid-cols-7 gap-4">
        {modes.map((m) => (
          <li
            key={m}
            className="flex flex-col items-center gap-3 rounded-lg bg-surface px-3 py-6 text-center shadow-[var(--shadow-hairline)]"
          >
            <ModeChip mode={m} size={56} />
            <span className="text-small font-medium text-ink">{t(m)}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}

export async function Honesty() {
  const t = await getTranslations("home.honest");
  const items = [
    { key: "fares", icon: "wallet" },
    { key: "routes", icon: "compass" },
    { key: "times", icon: "clock" },
  ] as const;
  return (
    <section className={`${container} pt-32`} aria-labelledby="honest-title">
      <div id="honest-title">
        <SectionHeading title={t("title")} lead={t("lead")} />
      </div>
      <div className="mt-10 grid grid-cols-3 gap-12 border-t border-line pt-10">
        {items.map((it) => (
          <div key={it.key}>
            <Icon name={it.icon} size={26} className="text-lagoon-600" />
            <h3 className="mt-4 font-display text-title text-ink">{t(`items.${it.key}.title`)}</h3>
            <p className="mt-2 text-body text-ink-muted">{t(`items.${it.key}.body`)}</p>
          </div>
        ))}
      </div>
    </section>
  );
}

export async function ClosingCta() {
  const t = await getTranslations("home.cta");
  return (
    <section className={`${container} pt-32`}>
      <div className="relative overflow-hidden rounded-2xl bg-ocean-900 px-16 py-16 text-ink-inverse on-dark">
        <div
          aria-hidden="true"
          className="absolute -inset-e-24 -top-24 size-96 rounded-full border border-white/10"
        />
        <div
          aria-hidden="true"
          className="absolute -inset-e-12 -top-12 size-72 rounded-full border border-white/10"
        />
        <div
          aria-hidden="true"
          className="absolute -inset-e-0 top-0 size-48 rounded-full border border-white/10"
        />
        <div className="relative flex items-center justify-between gap-8">
          <div>
            <h2 className="font-display text-display text-sand-50">{t("title")}</h2>
            <p className="mt-3 text-lead text-sand-300">{t("body")}</p>
          </div>
          <Button asChild variant="accent" size="xl">
            <Link href="/plan">
              {t("action")}
              <Icon name="arrow-right" size={22} directional />
            </Link>
          </Button>
        </div>
      </div>
    </section>
  );
}
