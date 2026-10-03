import { cookies } from 'next/headers';
import Landing from './_components/Landing';
import HomeView, { type HomeData } from './_components/home/HomeView';
import Footer from '@/components/layout/Footer';
import { getStats, getLibrary, getPendingReviews, getMe } from '@/lib/api/server';
import { isJwtValid } from '@/lib/jwt';
import type { GetLibraryResponse, GetStatsResponse, LibraryItem } from '@/lib/types';
import { ACCESS_TOKEN_COOKIE } from '@/lib/auth-storage';

// How many covers a type carousel loads before "see all" takes over.
const CAROUSEL_LIMIT = 50;
// How many items the "in progress" card receives.
const IN_PROGRESS_LIMIT = 12;

/** Reads the username from the token's payload, for when the profile request fails. */
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

// Helpers that turn a settled library request into its items or total (empty on failure).
type LibRes = PromiseSettledResult<GetLibraryResponse>;
const libData = (r: LibRes): LibraryItem[] => (r.status === 'fulfilled' ? (r.value.data ?? []) : []);
const libTotal = (r: LibRes): number =>
  r.status === 'fulfilled' ? (r.value.total ?? r.value.data?.length ?? 0) : 0;

/**
 * `/`: the public landing page for visitors without a valid session, or the signed-in home
 * (stats, carousels per type, in-progress and pending-review cards). Each home request is
 * independent, so one failing only empties its own section.
 */
export default async function RootPage() {
  const token = (await cookies()).get(ACCESS_TOKEN_COOKIE)?.value;

  // Visitors without a valid session get the public landing page.
  if (!token || !isJwtValid(token)) {
    return (
      <>
        <Landing />
        <Footer />
      </>
    );
  }

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
    inProgressTotal: libTotal(inProgress),
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
