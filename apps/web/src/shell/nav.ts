/** Single source for primary navigation. Labels are keys under the "shell.nav" namespace. */
export const primaryNav = [
  { key: "plan", href: "/plan" },
  { key: "trips", href: "/trips" },
  { key: "help", href: "/help" },
] as const;

export const accountLinks = { signIn: "/login", signUp: "/signup" } as const;

export const footerLinks = [
  { key: "help", href: "/help" },
  { key: "terms", href: "/legal/terms" },
  { key: "privacy", href: "/legal/privacy" },
] as const;
