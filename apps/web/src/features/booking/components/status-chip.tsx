"use client";

import { Badge } from "@dhathuru/ui";
import { useTranslations } from "next-intl";
import { STATUS_TONE } from "../lib/status";
import type { BookingStatus } from "../lib/types";

export function StatusChip({ status }: { status: BookingStatus }) {
  const t = useTranslations("booking.status");
  return (
    <Badge tone={STATUS_TONE[status]} shape="pill">
      {t(status)}
    </Badge>
  );
}
