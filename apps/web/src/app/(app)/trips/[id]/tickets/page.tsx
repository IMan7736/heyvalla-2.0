import { getTranslations } from "next-intl/server";
import { notFound } from "next/navigation";
import { TicketWallet } from "@/features/booking";
import { isBookingId } from "@/features/booking/server/guards";

export async function generateMetadata() {
  const t = await getTranslations("booking.wallet");
  return { title: t("pageTitle") };
}

export default async function TicketsPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!isBookingId(id)) notFound();
  return (
    <main id="main">
      <TicketWallet bookingId={id} />
    </main>
  );
}
