import * as RadixSelect from "@radix-ui/react-select";
import type { ReactNode } from "react";
import { Icon } from "../icons/Icon";
import { cn } from "../lib/cn";
import { controlBase } from "./Input";
import { useFieldControl } from "./Field";

export type SelectOption = { value: string; label: ReactNode; disabled?: boolean };

export type SelectProps = {
  options: SelectOption[];
  value?: string;
  defaultValue?: string;
  onValueChange?: (value: string) => void;
  placeholder: string;
  name?: string;
  id?: string;
  disabled?: boolean;
  className?: string;
};

/** Single-choice dropdown on Radix Select (typeahead, arrow keys, screen-reader semantics). */
export function Select({ options, placeholder, className, id, disabled, ...rest }: SelectProps) {
  const field = useFieldControl({ id });
  return (
    <RadixSelect.Root disabled={disabled ?? Boolean(field.disabled)} {...rest}>
      <RadixSelect.Trigger
        id={field.id}
        aria-describedby={field["aria-describedby"]}
        aria-invalid={field["aria-invalid"]}
        className={cn(
          controlBase,
          "flex h-10 items-center justify-between gap-2 rounded-sm px-3 text-start text-body data-[placeholder]:text-ink-muted",
          className,
        )}
      >
        <RadixSelect.Value placeholder={placeholder} />
        <RadixSelect.Icon>
          <Icon name="chevron-down" size={18} />
        </RadixSelect.Icon>
      </RadixSelect.Trigger>
      <RadixSelect.Portal>
        <RadixSelect.Content
          position="popper"
          sideOffset={6}
          className="z-50 max-h-[var(--radix-select-content-available-height)] min-w-[var(--radix-select-trigger-width)] overflow-hidden rounded-md bg-surface shadow-[var(--shadow-hairline),var(--shadow-float)] animate-[dh-pop-in_var(--duration-base)_var(--ease-calm)]"
        >
          <RadixSelect.Viewport className="p-1.5">
            {options.map((o) => (
              <RadixSelect.Item
                key={o.value}
                value={o.value}
                disabled={o.disabled}
                className="relative flex h-10 cursor-default select-none items-center rounded-sm ps-3 pe-9 text-body text-ink outline-none data-[disabled]:opacity-40 data-[highlighted]:bg-lagoon-50 data-[state=checked]:font-medium"
              >
                <RadixSelect.ItemText>{o.label}</RadixSelect.ItemText>
                <RadixSelect.ItemIndicator className="absolute inset-e-3 text-lagoon-600">
                  <Icon name="check" size={16} />
                </RadixSelect.ItemIndicator>
              </RadixSelect.Item>
            ))}
          </RadixSelect.Viewport>
        </RadixSelect.Content>
      </RadixSelect.Portal>
    </RadixSelect.Root>
  );
}
