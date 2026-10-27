"use client";

import {
  Accordion,
  Alert,
  Avatar,
  BookingConfirmed,
  BufferMeter,
  Button,
  Card,
  CardDescription,
  CardHeader,
  CardTitle,
  Checkbox,
  Combobox,
  DatePicker,
  Dialog,
  DropdownMenu,
  EmptyState,
  ErrorState,
  Field,
  Icon,
  IconButton,
  Input,
  interfaceIconNames,
  JourneyRibbon,
  ModeChip,
  NumberStepper,
  OfflineState,
  Pill,
  Popover,
  PopoverContent,
  PopoverTrigger,
  Progress,
  RadioGroup,
  RiskGauge,
  SegmentedControl,
  Select,
  Separator,
  Skeleton,
  Stepper,
  Switch,
  Tab,
  TabPanel,
  Tabs,
  TabsList,
  Tag,
  TicketCard,
  Tooltip,
  transportIconNames,
  TripResultCard,
  useToast,
  Badge,
  Textarea,
  type TransportMode,
} from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import { useState, type ReactNode } from "react";

function Section({ id, title, children }: { id: string; title: string; children: ReactNode }) {
  return (
    <section aria-labelledby={`g-${id}`} className="border-t border-line py-12">
      <h2 id={`g-${id}`} className="mb-8 font-display text-headline text-ink">
        {title}
      </h2>
      {children}
    </section>
  );
}

const swatches: { name: string; cls: string; hex: string }[] = [
  { name: "lagoon", cls: "bg-lagoon", hex: "#087F8C" },
  { name: "sand", cls: "bg-sand", hex: "#FAF7F0" },
  { name: "coconut", cls: "bg-coconut", hex: "#FFFFFF" },
  { name: "ocean", cls: "bg-ocean", hex: "#12343B" },
  { name: "coral", cls: "bg-coral", hex: "#F28C78" },
];
// Full class names so Tailwind's scanner sees them (no string interpolation).
const scales: { name: string; steps: { cls: string; label: string }[] }[] = [
  {
    name: "lagoon",
    steps: [
      { cls: "bg-lagoon-50", label: "50" },
      { cls: "bg-lagoon-100", label: "100" },
      { cls: "bg-lagoon-200", label: "200" },
      { cls: "bg-lagoon-300", label: "300" },
      { cls: "bg-lagoon-400", label: "400" },
      { cls: "bg-lagoon-500", label: "500" },
      { cls: "bg-lagoon-600", label: "600" },
      { cls: "bg-lagoon-700", label: "700" },
      { cls: "bg-lagoon-800", label: "800" },
    ],
  },
  {
    name: "sand",
    steps: [
      { cls: "bg-sand-50", label: "50" },
      { cls: "bg-sand-100", label: "100" },
      { cls: "bg-sand-200", label: "200" },
      { cls: "bg-sand-300", label: "300" },
      { cls: "bg-sand-400", label: "400" },
    ],
  },
  {
    name: "ocean",
    steps: [
      { cls: "bg-ocean-500", label: "500" },
      { cls: "bg-ocean-600", label: "600" },
      { cls: "bg-ocean-700", label: "700" },
      { cls: "bg-ocean-800", label: "800" },
      { cls: "bg-ocean-900", label: "900" },
      { cls: "bg-ocean-950", label: "950" },
    ],
  },
  {
    name: "coral",
    steps: [
      { cls: "bg-coral-50", label: "50" },
      { cls: "bg-coral-100", label: "100" },
      { cls: "bg-coral-300", label: "300" },
      { cls: "bg-coral-500", label: "500" },
      { cls: "bg-coral-700", label: "700" },
    ],
  },
  {
    name: "status",
    steps: [
      { cls: "bg-success", label: "ok" },
      { cls: "bg-success-soft", label: "ok soft" },
      { cls: "bg-caution", label: "warn" },
      { cls: "bg-caution-soft", label: "warn soft" },
      { cls: "bg-danger", label: "bad" },
      { cls: "bg-danger-soft", label: "bad soft" },
    ],
  },
];

export function Gallery() {
  const t = useTranslations("gallery");
  const u = useTranslations("ui");
  const toast = useToast();
  const [date, setDate] = useState<string | null>("2026-10-27");
  const [place, setPlace] = useState<string | null>(null);
  const [pax, setPax] = useState(2);
  const [mode, setMode] = useState("departAfter");
  const [dialog, setDialog] = useState(false);

  const places = [
    {
      value: "isl_maafushi",
      label: t("forms.maafushi"),
      description: t("forms.kaafu"),
      icon: "pin" as const,
    },
    {
      value: "isl_male",
      label: t("forms.male"),
      description: t("forms.kaafu"),
      icon: "pin" as const,
    },
    { value: "apt_velana", label: t("forms.velana"), icon: "airport" as const },
  ];
  const legModes: {
    id: string;
    mode: TransportMode;
    d: number;
    key: "ferry" | "taxiA" | "flight" | "taxiB";
  }[] = [
    { id: "a", mode: "ferry", d: 50, key: "ferry" },
    { id: "b", mode: "taxi", d: 10, key: "taxiA" },
    { id: "c", mode: "domestic_flight", d: 65, key: "flight" },
    { id: "d", mode: "taxi", d: 15, key: "taxiB" },
  ];
  const ribbon = {
    label: t("journey.ribbon"),
    destinationLabel: t("journey.destination"),
    legs: legModes.map((l, i) => ({
      id: l.id,
      mode: l.mode,
      durationMin: l.d,
      durationLabel: `${l.d} min`,
      description: t(`journey.${l.key}`),
      fromLabel: i === 0 ? t("journey.origin") : undefined,
    })),
    buffers: [
      {
        afterLegIndex: 0,
        level: "comfortable" as const,
        label: "25 min",
        levelLabel: t("journey.comfortableLevel"),
      },
      {
        afterLegIndex: 1,
        level: "ok" as const,
        label: "35 min",
        levelLabel: t("journey.adequateLevel"),
      },
      {
        afterLegIndex: 2,
        level: "tight" as const,
        label: "12 min",
        levelLabel: t("journey.tightLevel"),
      },
    ],
  };

  return (
    <main id="main" className="mx-auto max-w-[var(--container-page)] px-8 py-16 min-[90rem]:px-12">
      <h1 className="font-display text-display text-ink">{t("title")}</h1>
      <p className="mt-3 max-w-xl text-lead text-ink-muted">{t("lead")}</p>

      <Section id="colour" title={t("sections.colour")}>
        <p className="mb-6 text-small text-ink-muted">{t("colour.note")}</p>
        <div className="mb-8 grid grid-cols-5 gap-4">
          {swatches.map((s) => (
            <div
              key={s.name}
              className="overflow-hidden rounded-lg shadow-[var(--shadow-hairline)]"
            >
              <div className={`${s.cls} h-24`} />
              <div className="bg-surface p-3 text-small">
                <p className="font-medium text-ink">{s.name}</p>
                <p className="tabular text-caption text-ink-muted">{s.hex}</p>
              </div>
            </div>
          ))}
        </div>
        <div className="flex flex-col gap-3">
          {scales.map((sc) => (
            <div key={sc.name} className="flex items-center gap-4">
              <p className="w-16 text-small text-ink-muted">{sc.name}</p>
              <div className="flex flex-1 overflow-hidden rounded-md shadow-[var(--shadow-hairline)]">
                {sc.steps.map((st) => (
                  <div key={st.label} className={`${st.cls} flex h-12 flex-1 items-end p-1.5`}>
                    <span className="rounded-xs bg-white/70 px-1 text-caption text-ocean-900">
                      {st.label}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      </Section>

      <Section id="type" title={t("sections.type")}>
        <div className="flex flex-col gap-4">
          <p className="font-display text-hero text-ink">{t("type.display")}</p>
          <p className="font-display text-headline text-ink">{t("type.title")}</p>
          <p className="max-w-2xl text-lead text-ink">{t("type.body")}</p>
          <p className="text-small text-ink-muted">{t("type.small")}</p>
          <p className="tabular font-display text-display text-ink">{t("type.numerals")}</p>
        </div>
      </Section>

      <Section id="icons" title={t("sections.icons")}>
        <div className="mb-6 grid grid-cols-10 gap-3">
          {transportIconNames.map((n) => (
            <div
              key={n}
              className="flex flex-col items-center gap-2 rounded-lg bg-surface p-3 shadow-[var(--shadow-hairline)]"
            >
              <Icon name={n} size={32} className="text-ocean-900" />
              <span className="text-caption text-ink-muted">{n}</span>
            </div>
          ))}
        </div>
        <div className="grid grid-cols-12 gap-3">
          {interfaceIconNames.map((n) => (
            <div
              key={n}
              className="flex flex-col items-center gap-2 rounded-lg bg-surface p-3 shadow-[var(--shadow-hairline)]"
            >
              <Icon name={n} size={24} className="text-ocean-900" />
              <span className="max-w-full truncate text-caption text-ink-muted">{n}</span>
            </div>
          ))}
        </div>
        <div className="mt-6 flex gap-3">
          {(
            [
              "ferry",
              "speedboat",
              "seaplane",
              "domestic_flight",
              "bus",
              "taxi",
              "walk",
              "resort_transfer",
            ] as const
          ).map((m) => (
            <ModeChip key={m} mode={m} size={44} />
          ))}
        </div>
      </Section>

      <Section id="buttons" title={t("sections.buttons")}>
        <div className="flex flex-wrap items-center gap-4">
          <Button leadingIcon="search">{t("buttons.primary")}</Button>
          <Button variant="secondary">{t("buttons.secondary")}</Button>
          <Button variant="ghost">{t("buttons.ghost")}</Button>
          <Button variant="accent" trailingIcon="arrow-right">
            {t("buttons.accent")}
          </Button>
          <Button variant="danger">{t("buttons.danger")}</Button>
          <Button variant="link">{t("buttons.link")}</Button>
          <Button loading>{t("buttons.loading")}</Button>
          <Button disabled>{t("buttons.primary")}</Button>
        </div>
        <div className="mt-6 flex items-center gap-4">
          <Button size="sm">{t("buttons.small")}</Button>
          <Button size="md">{t("buttons.primary")}</Button>
          <Button size="lg">{t("buttons.large")}</Button>
          <Button size="xl">{t("buttons.large")}</Button>
          <IconButton icon="swap" label={t("buttons.iconLabel")} variant="outline" />
          <IconButton icon="x" label={u("close")} />
          <IconButton icon="bell" label={u("notifications")} variant="soft" />
        </div>
      </Section>

      <Section id="forms" title={t("sections.forms")}>
        <div className="grid grid-cols-3 gap-x-8 gap-y-6">
          <Field
            label={t("forms.email")}
            hint={t("forms.emailHint")}
            required
            requiredLabel={u("required")}
          >
            <Input type="email" start={<Icon name="mail" size={18} />} />
          </Field>
          <Field label={t("forms.email")} error={t("forms.emailError")}>
            <Input defaultValue="aisha@" start={<Icon name="mail" size={18} />} />
          </Field>
          <Field label={t("forms.email")} disabled>
            <Input defaultValue="aisha@example.com" />
          </Field>
          <Field label={t("forms.from")}>
            <Combobox
              options={places}
              value={place}
              onValueChange={setPlace}
              placeholder={t("forms.fromPlaceholder")}
              emptyLabel={t("forms.noPlaces")}
              clearLabel={u("clear")}
              resultsLabel={(count) => u("results", { count })}
              start={<Icon name="pin" size={18} />}
            />
          </Field>
          <Field label={t("forms.date")}>
            <DatePicker
              value={date}
              onValueChange={setDate}
              placeholder={t("forms.datePlaceholder")}
              labels={{
                dialog: u("chooseDate"),
                previousMonth: u("previousMonth"),
                nextMonth: u("nextMonth"),
              }}
            />
          </Field>
          <Field label={t("forms.select")}>
            <Select
              placeholder={t("forms.selectPlaceholder")}
              options={[
                { value: "departAfter", label: t("forms.leaveAfter") },
                { value: "arriveBy", label: t("forms.arriveBy") },
              ]}
            />
          </Field>
          <Field label={t("forms.notes")} className="col-span-2">
            <Textarea rows={3} />
          </Field>
          <div className="flex flex-col gap-4">
            <p className="text-small font-medium text-ink">{t("forms.passengers")}</p>
            <NumberStepper
              value={pax}
              onValueChange={setPax}
              label={t("forms.passengers")}
              decrementLabel={t("forms.fewer")}
              incrementLabel={t("forms.more")}
            />
            <SegmentedControl
              label={t("forms.mode")}
              value={mode}
              onValueChange={setMode}
              options={[
                { value: "departAfter", label: t("forms.leaveAfter") },
                { value: "arriveBy", label: t("forms.arriveBy") },
              ]}
            />
          </div>
          <Checkbox
            label={t("forms.remember")}
            description={t("forms.rememberHint")}
            defaultChecked
          />
          <RadioGroup
            label={t("forms.risk")}
            defaultValue="balanced"
            options={[
              { value: "cautious", label: t("forms.cautious") },
              { value: "balanced", label: t("forms.balanced") },
              { value: "relaxed", label: t("forms.relaxed") },
            ]}
          />
          <Switch label={t("forms.notify")} defaultChecked />
        </div>
      </Section>

      <Section id="navigation" title={t("sections.navigation")}>
        <Stepper
          label={t("navigation.stepsLabel")}
          completeLabel={u("stepComplete")}
          currentLabel={u("stepCurrent")}
          currentId="payment"
          steps={[
            { id: "travellers", label: t("navigation.travellers") },
            { id: "payment", label: t("navigation.payment") },
            { id: "tickets", label: t("navigation.confirmed") },
          ]}
          className="max-w-2xl"
        />
        <Tabs defaultValue="overview" className="mt-10 max-w-3xl">
          <TabsList>
            <Tab value="overview" icon="route">
              {t("navigation.overview")}
            </Tab>
            <Tab value="legs" icon="ferry">
              {t("navigation.legs")}
            </Tab>
            <Tab value="tickets" icon="ticket">
              {t("navigation.tickets")}
            </Tab>
          </TabsList>
          <TabPanel value="overview">
            <p className="text-body text-ink-muted">{t("type.body")}</p>
          </TabPanel>
          <TabPanel value="legs">
            <p className="text-body text-ink-muted">{t("journey.ferry")}</p>
          </TabPanel>
          <TabPanel value="tickets">
            <p className="text-body text-ink-muted">{t("ticket.confirmedBody")}</p>
          </TabPanel>
        </Tabs>
      </Section>

      <Section id="overlays" title={t("sections.overlays")}>
        <div className="flex items-center gap-4">
          <Button variant="secondary" onClick={() => setDialog(true)}>
            {t("overlays.open")}
          </Button>
          <Dialog
            open={dialog}
            onOpenChange={setDialog}
            title={t("overlays.dialogTitle")}
            description={t("overlays.dialogBody")}
            closeLabel={u("close")}
            footer={
              <>
                <Button variant="ghost" onClick={() => setDialog(false)}>
                  {t("overlays.keep")}
                </Button>
                <Button variant="danger" onClick={() => setDialog(false)}>
                  {t("overlays.confirm")}
                </Button>
              </>
            }
          >
            <p className="text-body text-ink-muted">{t("overlays.dialogBody")}</p>
          </Dialog>
          <Popover>
            <PopoverTrigger asChild>
              <Button variant="secondary" trailingIcon="chevron-down">
                {t("overlays.popover")}
              </Button>
            </PopoverTrigger>
            <PopoverContent>
              <p className="font-medium text-ink">{t("overlays.popover")}</p>
              <p className="mt-1 text-small text-ink-muted">{t("overlays.popoverBody")}</p>
            </PopoverContent>
          </Popover>
          <Tooltip content={t("overlays.tooltip")}>
            <Button variant="ghost" leadingIcon="info">
              {t("overlays.tooltip")}
            </Button>
          </Tooltip>
          <DropdownMenu
            trigger={<IconButton icon="menu" label={t("overlays.menu")} variant="outline" />}
            items={[
              { label: t("overlays.viewTrip"), icon: "route", onSelect: () => {} },
              { label: t("overlays.share"), icon: "share", onSelect: () => {} },
              { type: "separator" },
              { label: t("overlays.remove"), icon: "trash", destructive: true, onSelect: () => {} },
            ]}
          />
        </div>
      </Section>

      <Section id="feedback" title={t("sections.feedback")}>
        <div className="grid grid-cols-2 gap-4">
          <Alert tone="info" title={t("feedback.info")}>
            {t("feedback.infoBody")}
          </Alert>
          <Alert tone="success" title={t("feedback.success")}>
            {t("feedback.successBody")}
          </Alert>
          <Alert tone="caution" title={t("feedback.caution")}>
            {t("feedback.cautionBody")}
          </Alert>
          <Alert tone="danger" title={t("feedback.danger")}>
            {t("feedback.dangerBody")}
          </Alert>
        </div>
        <div className="mt-8 flex flex-wrap items-center gap-3">
          <Button
            variant="secondary"
            onClick={() =>
              toast({
                title: t("feedback.toastTitle"),
                description: t("feedback.toastBody"),
                tone: "success",
              })
            }
          >
            {t("feedback.toast")}
          </Button>
          <Pill dot tone="lagoon">
            {t("feedback.fastest")}
          </Pill>
          <Pill tone="coral" dot>
            {t("feedback.best")}
          </Pill>
          <Pill tone="success">{t("feedback.safest")}</Pill>
          <Pill tone="neutral">{t("feedback.cheapest")}</Pill>
          <Badge tone="caution" icon="clock">
            {t("feedback.delayed")}
          </Badge>
          <Badge tone="success" icon="check">
            {t("feedback.confirmed")}
          </Badge>
          <Badge tone="danger">{t("feedback.cancelled")}</Badge>
          <Badge tone="neutral">{t("feedback.estimated")}</Badge>
          <Tag onRemove={() => {}} removeLabel={t("feedback.tagRemove")}>
            {t("feedback.tagFerry")}
          </Tag>
          <Avatar name={t("feedback.name")} />
        </div>
        <div className="mt-8 flex max-w-xl flex-col gap-3">
          <p className="text-caption text-ink-muted">{t("feedback.skeleton")}</p>
          <Skeleton className="h-6 w-2/3" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-5/6" />
          <Progress value={62} label={t("feedback.progress")} className="mt-2" />
        </div>
        <Separator className="my-8" />
        <Accordion
          items={[
            { id: "a", title: t("overlays.dialogTitle"), content: t("overlays.dialogBody") },
            { id: "b", title: t("overlays.popover"), content: t("overlays.popoverBody") },
          ]}
          className="max-w-xl"
        />
      </Section>

      <Section id="journey" title={t("sections.journey")}>
        <Card padding="lg" className="mb-8">
          <JourneyRibbon {...ribbon} variant="detailed" />
        </Card>
        <div className="mb-8 grid grid-cols-3 gap-8">
          <BufferMeter
            minutes={12}
            requiredMinutes={20}
            comfortMinutes={45}
            label={t("journey.bufferLabel")}
            valueText={t("journey.tightValue")}
            requiredLabel={t("journey.required")}
            comfortLabel={t("journey.comfort")}
          />
          <BufferMeter
            minutes={35}
            requiredMinutes={20}
            comfortMinutes={45}
            label={t("journey.bufferLabel")}
            valueText={t("journey.bufferValue")}
            requiredLabel={t("journey.required")}
            comfortLabel={t("journey.comfort")}
          />
          <BufferMeter
            minutes={70}
            requiredMinutes={20}
            comfortMinutes={45}
            label={t("journey.bufferLabel")}
            valueText={t("journey.comfortableValue")}
            requiredLabel={t("journey.required")}
            comfortLabel={t("journey.comfort")}
          />
        </div>
        <div className="mb-10 flex items-end gap-12">
          <RiskGauge
            missProbability={0.02}
            level="low"
            label={t("journey.riskLabel")}
            levelLabel={t("journey.low")}
            valueText="2%"
          />
          <RiskGauge
            missProbability={0.11}
            level="medium"
            label={t("journey.riskLabel")}
            levelLabel={t("journey.medium")}
            valueText="11%"
          />
          <RiskGauge
            missProbability={0.34}
            level="high"
            label={t("journey.riskLabel")}
            levelLabel={t("journey.high")}
            valueText="34%"
          />
          <RiskGauge
            variant="bar"
            missProbability={0.11}
            level="medium"
            label={t("journey.riskLabel")}
            levelLabel={t("journey.medium")}
            valueText="11%"
            className="w-56"
          />
        </div>
        <div className="flex flex-col gap-6">
          <TripResultCard
            label={t("journey.cardLabel")}
            departTime="13:05"
            arriveTime="16:40"
            arriveDay={t("journey.arriveDay")}
            durationLabel={t("journey.duration")}
            transfersLabel={t("journey.transfers")}
            price="MVR 1,250"
            priceNote={t("journey.priceNote")}
            labels={[
              { id: "safest", label: t("feedback.safest"), tone: "success" },
              { id: "best", label: t("feedback.best"), tone: "coral" },
            ]}
            ribbon={{ ...ribbon, variant: "compact", buffers: ribbon.buffers.slice(0, 2) }}
            risk={{
              missProbability: 0.02,
              level: "low",
              label: t("journey.riskLabel"),
              levelLabel: t("journey.low"),
              valueText: "2%",
            }}
            explanationsTitle={t("journey.why")}
            explanations={[
              { id: "a", text: t("journey.whySafe"), severity: "good" },
              { id: "b", text: t("journey.whyEstimate"), severity: "info" },
              { id: "c", text: t("journey.whyLast"), severity: "caution" },
            ]}
            interactive
            action={
              <Button size="md" fullWidth trailingIcon="arrow-right">
                {t("journey.select")}
              </Button>
            }
          />
          <TripResultCard
            selected
            label={t("journey.cardLabel")}
            departTime="11:20"
            arriveTime="18:15"
            arriveDay={t("journey.arriveDay")}
            durationLabel={t("journey.duration")}
            transfersLabel={t("journey.transfers")}
            price="MVR 980"
            priceNote={t("journey.priceNote")}
            labels={[{ id: "cheapest", label: t("feedback.cheapest"), tone: "neutral" }]}
            ribbon={{ ...ribbon, variant: "compact", buffers: ribbon.buffers.slice(1) }}
            risk={{
              missProbability: 0.18,
              level: "medium",
              label: t("journey.riskLabel"),
              levelLabel: t("journey.medium"),
              valueText: "18%",
            }}
            interactive
            action={
              <Button size="md" variant="secondary" fullWidth>
                {t("journey.select")}
              </Button>
            }
          />
        </div>
      </Section>

      <Section id="ticket" title={t("sections.ticket")}>
        <div className="flex items-start gap-12">
          <TicketCard
            mode="ferry"
            modeLabel={t("ticket.mode")}
            fromLabel={t("ticket.from")}
            toLabel={t("ticket.to")}
            departTime="13:05"
            arriveTime="13:55"
            day={t("ticket.day")}
            operatorLabel={t("ticket.operator")}
            details={[
              { id: "p", label: t("ticket.passenger"), value: t("feedback.name") },
              { id: "s", label: t("ticket.seat"), value: "14B" },
              { id: "r", label: t("ticket.reference"), value: "DH7K2Q" },
              { id: "g", label: t("ticket.gate"), value: "13:35" },
            ]}
            status={{ label: t("ticket.status"), tone: "success" }}
            qr={
              <svg
                viewBox="0 0 29 29"
                shapeRendering="crispEdges"
                role="img"
                aria-label={t("ticket.qrCaption")}
              >
                <rect width="29" height="29" fill="#fff" />
                {Array.from({ length: 29 * 29 }, (_, i) => {
                  const x = i % 29,
                    y = Math.floor(i / 29);
                  const finder = (x < 8 && y < 8) || (x > 20 && y < 8) || (x < 8 && y > 20);
                  const on = finder
                    ? x % 7 === 0 ||
                      y % 7 === 0 ||
                      (x % 7 > 1 && x % 7 < 5 && y % 7 > 1 && y % 7 < 5)
                    : (x * 7 + y * 13 + x * y) % 5 < 2;
                  return on ? (
                    <rect key={i} x={x} y={y} width="1" height="1" fill="#12343B" />
                  ) : null;
                })}
              </svg>
            }
            qrCaption={t("ticket.qrCaption")}
          />
          <BookingConfirmed
            className="flex-1"
            title={t("ticket.confirmedTitle")}
            description={t("ticket.confirmedBody")}
            reference="DH7K2Q"
            referenceLabel={t("ticket.referenceLabel")}
          />
        </div>
      </Section>

      <Section id="states" title={t("sections.states")}>
        <div className="grid grid-cols-3 gap-4">
          <Card>
            <EmptyState
              title={t("states.emptyTitle")}
              description={t("states.emptyBody")}
              action={<Button>{t("states.emptyAction")}</Button>}
            />
          </Card>
          <Card>
            <ErrorState
              title={t("states.errorTitle")}
              description={t("states.errorBody")}
              action={<Button>{t("states.errorAction")}</Button>}
            />
          </Card>
          <Card>
            <OfflineState
              title={t("states.offlineTitle")}
              description={t("states.offlineBody")}
              action={<Button variant="secondary">{t("states.offlineAction")}</Button>}
            />
          </Card>
        </div>
      </Section>

      <Section id="elevation" title={t("sections.elevation")}>
        <div className="grid grid-cols-5 gap-6">
          {(["hairline", "rest", "raised", "float", "overlay"] as const).map((e) => (
            <div
              key={e}
              className="grid h-28 place-items-center rounded-lg bg-surface text-small text-ink-muted"
              style={{ boxShadow: `var(--shadow-${e})` }}
            >
              {t(`elevation.${e}`)}
            </div>
          ))}
        </div>
        <Card className="mt-8 max-w-sm">
          <CardHeader>
            <CardTitle>{t("type.title")}</CardTitle>
            <CardDescription>{t("type.small")}</CardDescription>
          </CardHeader>
          <p className="text-body text-ink-muted">{t("type.body")}</p>
        </Card>
      </Section>
    </main>
  );
}
