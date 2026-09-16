import { Suspense } from 'react';
import { getLibrary } from '@/lib/api/server';
import type { LibraryItem } from '@/lib/types';
import LibraryClient from './LibraryClient';

export default async function LibraryPage() {
  let items: LibraryItem[] = [];
  try {
    // No limit — the whole library loads and LibraryClient paginates it.
    const res = await getLibrary();
    items = res.data ?? [];
  } catch {
    // empty state handled in the client
  }
  return (
    <Suspense fallback={null}>
      <LibraryClient items={items} />
    </Suspense>
  );
}
