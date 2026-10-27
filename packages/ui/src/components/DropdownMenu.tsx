import * as Menu from "@radix-ui/react-dropdown-menu";
import type { ReactNode } from "react";
import { Icon } from "../icons/Icon";
import type { IconName } from "../icons/icons";
import { cn } from "../lib/cn";

export type DropdownItem =
  | {
      type?: "item";
      label: string;
      icon?: IconName;
      onSelect: () => void;
      destructive?: boolean;
      disabled?: boolean;
    }
  | { type: "separator" };

export type DropdownMenuProps = {
  trigger: ReactNode;
  items: DropdownItem[];
  align?: "start" | "end";
};

export function DropdownMenu({ trigger, items, align = "end" }: DropdownMenuProps) {
  return (
    <Menu.Root>
      <Menu.Trigger asChild>{trigger}</Menu.Trigger>
      <Menu.Portal>
        <Menu.Content
          align={align}
          sideOffset={8}
          className="z-50 min-w-52 rounded-lg bg-surface p-1.5 shadow-[var(--shadow-hairline),var(--shadow-float)] animate-[dh-pop-in_var(--duration-base)_var(--ease-calm)]"
        >
          {items.map((item, i) =>
            item.type === "separator" ? (
              <Menu.Separator key={`sep-${i}`} className="my-1.5 h-px bg-line" />
            ) : (
              <Menu.Item
                key={item.label}
                disabled={item.disabled}
                onSelect={item.onSelect}
                className={cn(
                  "flex h-10 cursor-default items-center gap-3 rounded-sm px-3 text-small outline-none data-[disabled]:opacity-40 data-[highlighted]:bg-lagoon-50",
                  item.destructive ? "text-danger" : "text-ink",
                )}
              >
                {item.icon ? <Icon name={item.icon} size={18} /> : null}
                {item.label}
              </Menu.Item>
            ),
          )}
        </Menu.Content>
      </Menu.Portal>
    </Menu.Root>
  );
}
