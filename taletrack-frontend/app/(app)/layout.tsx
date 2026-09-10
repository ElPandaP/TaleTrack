import Footer from '@/components/layout/footer';

// Auth protection for this route group is handled by `proxy.ts`.
// The top nav lives in the root layout — this is just the centered content column.
export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <main className="mx-auto max-w-6xl px-4 py-6 lg:px-6 lg:py-8">{children}</main>
      <Footer />
    </>
  );
}
