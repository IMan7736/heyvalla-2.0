import { getTranslations } from "next-intl/server";
import { Confirmation } from "@/features/booking";
import { requireBookingId } from "@/features/booking/server/guards";

export async function generateMetadata() {
  const t = await getTranslations("booking.done");
  return { title: t("pageTitle") };
}

export default async function DonePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  await requireBookingId(id, `/book/done/${id}`);
  return (
    <main id="main">
      <Confirmation bookingId={id} />
    </main>
  );
}
