import { getTranslations } from "next-intl/server";
import { RegisterScreen } from "@/features/account/screens";

export async function generateMetadata() {
  const t = await getTranslations("account.meta");
  return { title: t("register") };
}

export default function Page() {
  return <RegisterScreen />;
}
