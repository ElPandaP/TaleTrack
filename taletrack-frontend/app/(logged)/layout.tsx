import Footer from '@/components/layout/Footer';

/**
 * Layout for the main app pages: a centred content column plus the footer. The top nav lives
 * in the root layout, and `proxy.ts` protects the routes that need a session.
 */
export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 lg:px-6 lg:py-8">{children}</main>
      <Footer />
    </>
  );
}
