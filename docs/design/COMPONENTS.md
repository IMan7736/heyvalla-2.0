# @dhathuru/ui: components (LAGOON, v1)

```ts
import { Button, Field, Input, TripResultCard, formatMoney } from "@dhathuru/ui";
```

Source-only package (`packages/ui/src`), consumed by `apps/web` through the workspace dependency. Tailwind scans it through `@source` in `apps/web/src/styles/globals.css`; tokens come from `packages/ui/src/styles/theme.css`. Live gallery: `/dev/ui` (dev only; set `NEXT_PUBLIC_ENABLE_UI_GALLERY=1` to expose in production).

## Ground rules
- **No built-in English.** Every visible or announced string is a prop (`label`, `closeLabel`, `emptyLabel`, ...). Callers pass next-intl results. Shared labels for the common ones live in `messages/en/ui.json` (`useTranslations("ui")`).
- **Numbers and times are pre-formatted by the caller** (Intl). Helpers: `formatMoney({amountMinor,currency})`, `formatTime(iso)` (Maldives time, 24 h), `formatDay(iso)`, `formatDuration(min)`. `"en"` formats day-first (`27 Oct`).
- **Roles over hex.** Use `bg-surface`, `text-ink`, `text-ink-muted`, `border-line`, `bg-action`, `text-link`. Brand names (`lagoon-500`, `coral`...) are for decoration and illustration. Teal text under 24px must be `text-link` or `lagoon-600+`. Coral is never text.
- **Logical CSS only** (`ps-`, `pe-`, `inset-s-`, `text-start`). Directional icons pass `directional`.
- `className` is appended, not merged: do not fight a component's own utilities, use a variant or wrap it.
- Interactive overlays are Radix (focus trap, escape, roving focus, ARIA). Do not re-implement them.

## Foundations
| Export | Purpose |
|---|---|
| `Icon` | `name`, `size=20`, `label?` (omit = decorative), `directional?`. 54 original icons: transport (`ferry speedboat seaplane plane bus taxi walk jetty airport resort-transfer`) + interface (`transportIconNames`, `interfaceIconNames`). |
| `ModeChip` | `mode: TransportMode`, `size`, `label?`. Tonal chip: sea = lagoon tint, air = ocean, land = sand. `modeIcon[mode]` maps a contract mode to an icon name. |
| `cn` | Joins class names (clsx). |

## Actions
| Component | Key props |
|---|---|
| `Button` | `variant` primary \| secondary \| ghost \| accent \| danger \| link; `size` sm \| md \| lg \| xl; `loading`; `leadingIcon`/`trailingIcon`; `fullWidth`; `asChild` (wrap a Next `<Link>`). Primary = white on `#087F8C` (4.75:1). |
| `IconButton` | `icon`, **`label` (required)**, `variant` ghost \| soft \| outline \| solid, `size`. |
| `DropdownMenu` | `trigger`, `items: ({label, icon?, onSelect, destructive?, disabled?} \| {type:"separator"})[]`. |
| `Tooltip` / `TooltipProvider` | Short hint only; never the sole carrier of information. |

## Forms
Wrap controls in `Field` to get label, hint, error, required marker and `aria-describedby`/`aria-invalid` wiring automatically.

| Component | Key props |
|---|---|
| `Field` | `label`, `hint?`, `error?`, `required?`, `requiredLabel?`, `disabled?`, `hideLabel?`. |
| `Input`, `Textarea` | native props; `Input` adds `start`/`end` slots and `size` md \| lg. |
| `Select` | `options[{value,label,disabled?}]`, `placeholder`, `value`/`defaultValue`/`onValueChange`. Radix Select (typeahead, keyboard). |
| `Combobox` | `options[{value,label,description?,icon?}]`, `value`, `onValueChange(value\|null)`, `placeholder`, `emptyLabel`, `resultsLabel(count)`, `clearLabel?`, `loading?`, `onQueryChange?`, `filter?=true` (set `false` when options are fetched per keystroke). ARIA 1.2 editable combobox, focus stays in the input. |
| `DatePicker` | `value` (`"YYYY-MM-DD"` Maldives date or null), `onValueChange`, `min?`, `max?`, `labels{dialog,previousMonth,nextMonth}`, `locale?`, `calendarLocale?`. Week starts Saturday. |
| `NumberStepper` | `value`, `onValueChange`, `min`, `max`, `label`, `decrementLabel`, `incrementLabel`, `valueText?`. |
| `SegmentedControl` | `options`, `value`, `onValueChange`, `label`. Radio-group semantics. |
| `Checkbox`, `RadioGroup`, `Switch` | `label` (+ `description` for checkbox/radio options); Radix state props. |

## Navigation and structure
`Tabs`, `TabsList variant="underline"\|"segmented"`, `Tab icon?`, `TabPanel` · `Stepper` (`steps`, `currentId`, `label`, `completeLabel`, `currentLabel`) · `Breadcrumbs` (`items`, `label`) · `Accordion` (`items[{id,title,content}]`) · `Separator` · `Card` (`elevation` flat \| rest \| raised \| float, `padding`, `interactive`) with `CardHeader/CardTitle/CardDescription`.

## Overlays and feedback
| Component | Key props |
|---|---|
| `Dialog` | `open`, `onOpenChange`, `title`, `description?`, **`closeLabel`**, `footer?`, `size`, `variant` dialog \| sheet, `trigger?`. Also `DialogRoot/Trigger/Close` primitives. |
| `Popover`, `PopoverTrigger`, `PopoverContent` | Radix Popover with the shared surface style. |
| `ToastProvider` + `useToast()` | `ToastProvider closeLabel regionLabel`; `toast({title, description?, tone, action?})`. Tones info/success/caution/danger. `ToastCardContent` + `toastCardClass` let another toast host reuse the same look. |
| `Alert` | `tone` info \| success \| caution \| danger, `title?`, `action?`. Danger uses `role="alert"`. |
| `Badge` | `tone` neutral \| lagoon \| solid \| coral \| success \| caution \| danger, `icon?`, `size`. `Pill` adds `dot` (coral). `Tag` is a removable chip (`onRemove`, `removeLabel`). |
| `Skeleton`, `Progress`, `Spinner`, `Avatar` | `Progress value label`; `Spinner label?` (or `decorative`); `Avatar name src? size?`. |
| `EmptyState`, `ErrorState`, `OfflineState` | `title`, `description?`, `action?`, `secondaryAction?`. Each with its own original illustration. |

## Signature components
| Component | What it is |
|---|---|
| `JourneyRibbon` | `legs[{id,mode,durationMin,durationLabel,description,fromLabel?}]`, `buffers[{afterLegIndex,level,label,levelLabel}]`, `destinationLabel?`, `label`, `variant` compact \| detailed. Ordered list; screen readers get each leg's `description` and each buffer's level in words. |
| `BufferMeter` | `minutes`, `requiredMinutes`, `comfortMinutes`, `label`, `valueText`, optional marker labels. `role="meter"` + visible value text. |
| `RiskGauge` | `missProbability` 0..1, `level` low \| medium \| high, `label`, `levelLabel`, `valueText`, `variant` gauge \| bar. `role="meter"`; icon + words accompany colour. |
| `TripResultCard` | Boarding-pass result card: `departTime`, `arriveTime`, `arriveDay`, `durationLabel`, `transfersLabel`, `price`, `priceNote?`, `labels[{id,label,tone}]`, `ribbon` (JourneyRibbon props), `risk` (RiskGauge props), `explanations[{id,text,severity}]`, `selected?`, `interactive?` (hover lift), `action?` (stub CTA), `label` (accessible name). Pass `selected` for the teal outline. |
| `TicketCard` | Wallet ticket: `mode`, `modeLabel`, `fromLabel`, `toLabel`, `departTime`, `arriveTime`, `day`, `operatorLabel`, `details[{id,label,value}]`, `status{label,tone}`, **`qr` (an `<img>` or `<svg>`; THILA renders the QR)**, `qrCaption?`. |
| `BookingConfirmed` | Confirmation moment: `title`, `description?`, `reference?`, `referenceLabel?`, children. Ripples + horizon draw (static under reduced motion). |
| `LagoonScene` | Hero backdrop (sky, coral sun, islands, caustics, waves, dhoni). Children render on top. Hero only; do not use behind dense content. |
| `Dhoni`, `Island`, `EmptyLagoonArt`, `ReefSnagArt`, `DriftingDhoniArt` | Original SVG illustrations. |

## Web shell (`apps/web/src/shell`)
`PageFrame` (skip link, sticky glass header, content, footer, UI providers, fonts; pages supply their own `<main id="main">`), `SiteHeader`, `NavLinks`, `SiteFooter`, `fonts.ts`, `nav.ts` (single source for navigation and account links). `<Logo variant="mark"\|"full" size tone />` in `apps/web/src/components/logo.tsx`.

## CSS you can rely on
Tokens (`theme.css`): colours, `text-caption|small|body|lead|title|headline|display|hero`, radii `xs..2xl`, shadows `hairline|rest|raised|float|overlay|inset`, `--ease-calm`, `--ease-swell`, `--duration-quick|base|slow`, keyframes `dh-*`. Utilities/classes: `.font-display`, `.tabular`, `.on-dark` (white focus ring), `.pass` / `.pass-body` / `.pass-perforation` (boarding-pass notches), `.sheen`.
