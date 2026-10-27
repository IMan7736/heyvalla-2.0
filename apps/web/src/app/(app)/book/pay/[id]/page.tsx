import { getTranslations } from "next-intl/server";
import { Checkout } from "@/features/booking";
import { requireBookingId } from "@/features/booking/server/guards";

export async function generateMetadata() {
  const t = await getTranslations("booking.checkout");
  return { title: t("pageTitle") };
}

export default async function PayPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  await requireBookingId(id, `/book/pay/${id}`);
  return (
    <main id="main">
      <Checkout bookingId={id} />
    </main>
  );
}
