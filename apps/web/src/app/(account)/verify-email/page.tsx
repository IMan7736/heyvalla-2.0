import { getTranslations } from "next-intl/server";
import { VerifyEmailScreen } from "@/features/account/screens";

export async function generateMetadata() {
  const t = await getTranslations("account.meta");
  return { title: t("verify") };
}

export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ token?: string }>;
}) {
  const { token } = await searchParams;
  return <VerifyEmailScreen {...(token ? { token } : {})} />;
}
