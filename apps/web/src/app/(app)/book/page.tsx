import { getTranslations } from "next-intl/server";
import { BookStart } from "@/features/booking";
import { requireSession } from "@/features/booking/server/guards";
import { parseParty } from "@/features/booking/lib/passengers";

export async function generateMetadata() {
  const t = await getTranslations("booking.start");
  return { title: t("pageTitle") };
}

const OPTION_ID = /^trp_[0-9A-Za-z]{10,40}$/;

export default async function BookPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const query = await searchParams;
  const raw = typeof query.option === "string" ? query.option : null;
  const optionId = raw !== null && OPTION_ID.test(raw) ? raw : null;
  const carried = new URLSearchParams(
    Object.entries(query).flatMap(([k, v]) =>
      typeof v === "string" ? [[k, v] as [string, string]] : [],
    ),
  );
  await requireSession(optionId ? `/book?${carried.toString()}` : "/book");
  return (
    <main id="main">
      <BookStart optionId={optionId} party={parseParty(query)} />
    </main>
  );
}
