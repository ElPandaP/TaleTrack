import { Suspense } from 'react';
import { getLibrary } from '@/lib/api/server';
import type { LibraryItem } from '@/lib/types';
import LibraryClient from './LibraryClient';

/** `/library`: everything the user tracks, with filters, search and pagination. */
export default async function LibraryPage() {
  let items: LibraryItem[] = [];
  try {
    // No limit: the whole library loads and LibraryClient filters and paginates it.
    const res = await getLibrary();
    items = res.data ?? [];
  } catch {
    // LibraryClient shows the empty state.
  }
  return (
    <Suspense fallback={null}>
      <LibraryClient items={items} />
    </Suspense>
  );
}
