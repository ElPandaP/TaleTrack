import type { Metadata } from "next";
import { cookies } from "next/headers";
import { Geist, Geist_Mono, Cormorant_Garamond } from "next/font/google";
import "./globals.css";
import { Providers } from "./providers";
import TopNav from "@/components/layout/topnav";
import { getMe } from "@/lib/api/server";
import { isJwtValid } from "@/lib/jwt";
import { getServerDict, getServerLocale } from "@/lib/i18n-server";

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

export async function generateMetadata(): Promise<Metadata> {
  const dict = await getServerDict();
  return {
    title: dict["meta.title"],
    description: dict["meta.description"],
  };
}

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const authed = isJwtValid((await cookies()).get("tt-token")?.value);
  const locale = await getServerLocale();
  const avatarUrl = authed ? await getMe().then((r) => r.data.avatarUrl).catch(() => null) : null;

  return (
    <html lang={locale} suppressHydrationWarning>
      <body className={`${geistSans.variable} ${geistMono.variable} ${cormorant.variable} flex min-h-dvh flex-col antialiased`}>
        {/* Applies the saved theme before React hydrates, so there's no flash of the
            wrong theme and the client's first render matches the server's (always
            light) — ThemeProvider itself picks up the real value post-mount. */}
        <script
          dangerouslySetInnerHTML={{
            __html: `try{if(localStorage.getItem('tt-theme')==='dark'){document.documentElement.classList.add('dark')}}catch(e){}`,
          }}
        />
        <Providers locale={locale}>
          <TopNav authed={authed} avatarUrl={avatarUrl} />
          <div className="flex flex-1 flex-col">{children}</div>
        </Providers>
      </body>
    </html>
  );
}
