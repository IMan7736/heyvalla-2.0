import { getTranslations } from "next-intl/server";
import { ResetPasswordScreen } from "@/features/account/screens";

export async function generateMetadata() {
  const t = await getTranslations("account.meta");
  return { title: t("reset") };
}

export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ token?: string }>;
}) {
  const { token } = await searchParams;
  return <ResetPasswordScreen {...(token ? { token } : {})} />;
}
