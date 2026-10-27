import { getTranslations } from "next-intl/server";
import { LoginScreen } from "@/features/account/screens";

export async function generateMetadata() {
  const t = await getTranslations("account.meta");
  return { title: t("login") };
}

export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ returnTo?: string }>;
}) {
  const { returnTo } = await searchParams;
  return <LoginScreen {...(returnTo ? { returnTo } : {})} />;
}
