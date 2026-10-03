import type { Metadata } from "next";
import { cookies } from "next/headers";
import { Geist, Geist_Mono, Cormorant_Garamond } from "next/font/google";
import "./globals.css";
import { Providers } from "./providers";
import TopNav from "@/components/layout/TopNav";
import ScrollToTop from "@/components/layout/ScrollToTop";
import { getMe } from "@/lib/api/server";
import { isJwtValid } from "@/lib/jwt";
import { getServerLocale, getServerT } from "@/lib/i18n-server";
import { ACCESS_TOKEN_COOKIE } from "@/lib/auth-storage";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

const cormorant = Cormorant_Garamond({
  variable: "--font-cormorant",
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
  style: ["normal", "italic"],
});

/** Page title and description, in the visitor's locale. */
export async function generateMetadata(): Promise<Metadata> {
  const t = await getServerT();
  return {
    title: t("meta.title"),
    description: t("meta.description"),
  };
}

/**
 * Root layout: fonts, theme bootstrap script, client providers, the top nav and the scroll
 * reset. Reads the session cookie so the nav renders the right variant on the first paint.
 */
export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const authed = isJwtValid((await cookies()).get(ACCESS_TOKEN_COOKIE)?.value);
  const locale = await getServerLocale();
  const avatarUrl = authed ? await getMe().then((r) => r.data.avatarUrl).catch(() => null) : null;

  return (
    <html lang={locale} suppressHydrationWarning>
      <body className={`${geistSans.variable} ${geistMono.variable} ${cormorant.variable} flex min-h-dvh flex-col antialiased`}>
        {/* Applies the saved theme before React hydrates, so the wrong theme never flashes.
            The client's first render still matches the server's (always light) and
            ThemeProvider picks up the real value after mounting. */}
        <script
          dangerouslySetInnerHTML={{
            __html: `try{if(localStorage.getItem('tt-theme')==='dark'){document.documentElement.classList.add('dark')}}catch(e){}`,
          }}
        />
        <Providers locale={locale}>
          <ScrollToTop />
          <TopNav authed={authed} avatarUrl={avatarUrl} />
          <div className="flex flex-1 flex-col">{children}</div>
        </Providers>
      </body>
    </html>
  );
}
