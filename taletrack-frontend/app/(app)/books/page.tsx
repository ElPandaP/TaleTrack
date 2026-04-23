import { getBooks } from '@/lib/api/server';
import BooksClient from './BooksClient';
import type { Book } from '@/lib/types';

export default async function BooksPage() {
  let books: Book[] = [];
  try {
    const res = await getBooks();
    books = res.data ?? [];
  } catch {
    // empty state shown in client
  }
  return <BooksClient initialBooks={books} />;
}
