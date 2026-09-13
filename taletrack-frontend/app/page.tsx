import { cookies } from 'next/headers';
import Landing from './_components/landing';
import HomeView, { type HomeData } from './_components/home/home-view';
import Footer from '@/components/layout/footer';
import { getStats, getLibrary, getPendingReviews, getMe } from '@/lib/api/server';
import { isJwtValid } from '@/lib/jwt';
import type { GetLibraryResponse, GetStatsResponse, LibraryItem } from '@/lib/types';

// How many covers a type carousel loads before "see all" takes over.
const CAROUSEL_LIMIT = 50;
const IN_PROGRESS_LIMIT = 12;

function decodeUsername(token: string): string {
  try {
    const payload = JSON.parse(
      Buffer.from(token.split('.')[1], 'base64').toString('utf8'),
    );
    return payload.unique_name ?? payload.username ?? 'reader';
  } catch {
    return 'reader';
  }
}

type LibRes = PromiseSettledResult<GetLibraryResponse>;
const libData = (r: LibRes): LibraryItem[] => (r.status === 'fulfilled' ? (r.value.data ?? []) : []);
const libTotal = (r: LibRes): number =>
  r.status === 'fulfilled' ? (r.value.total ?? r.value.data?.length ?? 0) : 0;

export default async function RootPage() {
  const token = (await cookies()).get('tt-token')?.value;

  // Logged-out or expired visitors get the public landing page.
  if (!token || !isJwtValid(token)) return <Landing />;

  const [me, stats, books, movies, series, inProgress, pending] = await Promise.allSettled([
    getMe(),
    getStats(),
    getLibrary({ type: 'Book', limit: CAROUSEL_LIMIT }),
    getLibrary({ type: 'Movie', limit: CAROUSEL_LIMIT }),
    getLibrary({ type: 'Series', limit: CAROUSEL_LIMIT }),
    getLibrary({ status: 'in_progress', limit: IN_PROGRESS_LIMIT }),
    getPendingReviews(),
  ]);

  const data: HomeData = {
    username:
      me.status === 'fulfilled' ? me.value.data.username : decodeUsername(token),
    avatarUrl: me.status === 'fulfilled' ? me.value.data.avatarUrl ?? null : null,
    stats:
      stats.status === 'fulfilled'
        ? (stats.value as GetStatsResponse).data
        : null,
    books: libData(books),
    movies: libData(movies),
    series: libData(series),
    totals: { Book: libTotal(books), Movie: libTotal(movies), Series: libTotal(series) },
    inProgress: libData(inProgress),
    pending: libData(pending),
  };

  return (
    <>
      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 lg:px-6 lg:py-8">
        <HomeView data={data} />
      </main>
      <Footer />
    </>
  );
}
