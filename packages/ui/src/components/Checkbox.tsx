import * as RadixCheckbox from "@radix-ui/react-checkbox";
import * as RadixRadio from "@radix-ui/react-radio-group";
import * as RadixSwitch from "@radix-ui/react-switch";
import { useId, type ComponentProps, type ReactNode } from "react";
import { Icon } from "../icons/Icon";
import { cn } from "../lib/cn";

const box =
  "grid size-5 shrink-0 place-items-center bg-surface shadow-[inset_0_0_0_1.5px_var(--color-ocean-500)] transition-[background-color,box-shadow] duration-[var(--duration-quick)] hover:shadow-[inset_0_0_0_1.5px_var(--color-lagoon-600)] data-[state=checked]:bg-action data-[state=checked]:shadow-none disabled:opacity-50";

export type CheckboxProps = Omit<ComponentProps<typeof RadixCheckbox.Root>, "children"> & {
  label: ReactNode;
  description?: ReactNode;
};

export function Checkbox({ label, description, className, id, ...rest }: CheckboxProps) {
  const auto = useId();
  const cid = id ?? auto;
  return (
    <div className={cn("flex items-start gap-3", className)}>
      <RadixCheckbox.Root id={cid} className={cn(box, "mt-0.5 rounded-xs")} {...rest}>
        <RadixCheckbox.Indicator className="text-ink-inverse">
          <Icon name="check" size={14} strokeWidth={2.5} />
        </RadixCheckbox.Indicator>
      </RadixCheckbox.Root>
      <label htmlFor={cid} className="cursor-pointer text-body text-ink">
        {label}
        {description ? (
          <span className="block text-small text-ink-muted">{description}</span>
        ) : null}
      </label>
    </div>
  );
}

export type RadioOption = {
  value: string;
  label: ReactNode;
  description?: ReactNode;
  disabled?: boolean;
};

export type RadioGroupProps = Omit<ComponentProps<typeof RadixRadio.Root>, "children"> & {
  options: RadioOption[];
  /** Accessible name for the group. */
  label: string;
};

export function RadioGroup({ options, label, className, ...rest }: RadioGroupProps) {
  const base = useId();
  return (
    <RadixRadio.Root aria-label={label} className={cn("flex flex-col gap-3", className)} {...rest}>
      {options.map((o) => (
        <div key={o.value} className="flex items-start gap-3">
          <RadixRadio.Item
            id={`${base}-${o.value}`}
            value={o.value}
            disabled={o.disabled}
            className={cn(
              box,
              "mt-0.5 rounded-full data-[state=checked]:bg-surface data-[state=checked]:shadow-[inset_0_0_0_2px_var(--color-action)]",
            )}
          >
            <RadixRadio.Indicator className="size-2.5 rounded-full bg-action" />
          </RadixRadio.Item>
          <label htmlFor={`${base}-${o.value}`} className="cursor-pointer text-body text-ink">
            {o.label}
            {o.description ? (
              <span className="block text-small text-ink-muted">{o.description}</span>
            ) : null}
          </label>
        </div>
      ))}
    </RadixRadio.Root>
  );
}

export type SwitchProps = Omit<ComponentProps<typeof RadixSwitch.Root>, "children"> & {
  label: ReactNode;
};

export function Switch({ label, className, id, ...rest }: SwitchProps) {
  const auto = useId();
  const sid = id ?? auto;
  return (
    <div className={cn("flex items-center gap-3", className)}>
      <RadixSwitch.Root
        id={sid}
        className="relative h-6 w-11 shrink-0 rounded-full bg-ocean-500/40 transition-colors duration-[var(--duration-base)] data-[state=checked]:bg-action disabled:opacity-50"
        {...rest}
      >
        <RadixSwitch.Thumb className="block size-5 translate-x-0.5 rounded-full bg-white shadow-[var(--shadow-rest)] transition-transform duration-[var(--duration-base)] ease-[var(--ease-swell)] data-[state=checked]:translate-x-[22px] rtl:-translate-x-0.5 rtl:data-[state=checked]:-translate-x-[22px]" />
      </RadixSwitch.Root>
      <label htmlFor={sid} className="cursor-pointer text-body text-ink">
        {label}
      </label>
    </div>
  );
}
