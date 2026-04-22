'use client';

import { useEffect, useState, useCallback } from 'react';
import Image from 'next/image';
import { BookOpen, Search, SortAsc, RefreshCw } from 'lucide-react';
import { trackingService } from '@/lib/api/services';
import type { Book } from '@/lib/types';

const bookColor = 'text-[oklch(0.65_0.13_65)]';
const bookBg    = 'bg-[oklch(0.65_0.13_65)]/10';
const bookBdr   = 'border-[oklch(0.65_0.13_65)]/20';

function BookCard({ book }: { book: Book }) {
  const finishDate = new Date(book.finishedAt).toLocaleDateString('en-US', {
    month: 'short', year: 'numeric',
  });

  return (
    <div className="group flex flex-col cursor-pointer">
      <div className={`relative overflow-hidden rounded-2xl ${bookBg} border ${bookBdr} aspect-[2/3] mb-3 hover:border-[oklch(0.65_0.13_65)]/40 transition-colors`}>
        {book.coverUrl ? (
          <Image
            src={book.coverUrl}
            alt={book.title}
            fill
            className="object-cover group-hover:scale-105 transition-transform duration-500"
          />
        ) : (
          <div className="w-full h-full flex flex-col items-center justify-center p-4">
            <BookOpen className={`w-8 h-8 ${bookColor} opacity-40 mb-2`} />
            <p className={`text-xs ${bookColor} opacity-40 text-center font-medium line-clamp-3`}>
              {book.title}
            </p>
          </div>
        )}
        <div className="absolute bottom-2 right-2 bg-foreground/70 backdrop-blur-sm rounded-lg px-2 py-1">
          <p className={`text-[10px] ${bookColor} font-medium`}>{finishDate}</p>
        </div>
      </div>
      <div>
        <p className="text-sm font-medium text-foreground line-clamp-2 leading-snug mb-0.5">{book.title}</p>
        {book.author && <p className="text-xs text-muted-foreground line-clamp-1">{book.author}</p>}
        {book.pages && <p className="text-xs text-muted-foreground/60 mt-0.5">{book.pages} pages</p>}
      </div>
    </div>
  );
}

const controlClass = 'bg-secondary/60 border border-border rounded-xl text-sm text-foreground px-3 py-2.5 focus:outline-none focus:border-primary/50 cursor-pointer';

export default function BooksPage() {
  const [books, setBooks] = useState<Book[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [sort, setSort] = useState<'recent' | 'title' | 'author'>('recent');

  const fetchBooks = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await trackingService.getBooks();
      setBooks(res.data ?? []);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load books');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchBooks(); }, [fetchBooks]);

  const filtered = books
    .filter((b) =>
      b.title.toLowerCase().includes(search.toLowerCase()) ||
      (b.author ?? '').toLowerCase().includes(search.toLowerCase())
    )
    .sort((a, b) => {
      if (sort === 'title') return a.title.localeCompare(b.title);
      if (sort === 'author') return (a.author ?? '').localeCompare(b.author ?? '');
      return new Date(b.finishedAt).getTime() - new Date(a.finishedAt).getTime();
    });

  return (
    <div className="p-6 lg:p-8 max-w-7xl mx-auto">
      <div className="mb-8">
        <div className="flex items-center gap-3 mb-2">
          <div className={`p-2 rounded-xl ${bookBg}`}>
            <BookOpen className={`w-5 h-5 ${bookColor}`} />
          </div>
          <h1 className="font-heading text-2xl font-semibold">Books</h1>
          {!loading && (
            <span className="ml-1 text-sm text-muted-foreground bg-secondary border border-border px-2.5 py-0.5 rounded-full">
              {books.length}
            </span>
          )}
        </div>
        <p className="text-muted-foreground text-sm ml-[52px]">
          Your personal reading library, synced from KOReader.
        </p>
      </div>

      <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3 mb-8">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground/50" />
          <input
            type="text"
            placeholder="Search by title or author…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="tt-input pl-10 pr-4 py-2.5"
          />
        </div>
        <div className="flex items-center gap-2">
          <SortAsc className="w-4 h-4 text-muted-foreground/50 flex-shrink-0" />
          <select
            value={sort}
            onChange={(e) => setSort(e.target.value as 'recent' | 'title' | 'author')}
            className={controlClass}
          >
            <option value="recent">Recently added</option>
            <option value="title">Title A–Z</option>
            <option value="author">Author A–Z</option>
          </select>
          <button
            onClick={fetchBooks}
            disabled={loading}
            className="p-2.5 rounded-xl bg-secondary/60 border border-border text-muted-foreground hover:text-foreground transition-colors cursor-pointer disabled:opacity-50 flex-shrink-0"
            aria-label="Refresh"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>
        </div>
      </div>

      {error && (
        <div className="mb-6 p-4 bg-destructive/10 border border-destructive/20 rounded-xl text-destructive text-sm">
          {error}
        </div>
      )}

      {loading ? (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-5">
          {Array.from({ length: 12 }).map((_, i) => (
            <div key={i} className="flex flex-col gap-3">
              <div className="aspect-[2/3] bg-secondary/60 rounded-2xl animate-pulse" />
              <div className="h-3 bg-secondary rounded animate-pulse w-3/4" />
              <div className="h-2.5 bg-secondary/60 rounded animate-pulse w-1/2" />
            </div>
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <div className={`w-16 h-16 rounded-2xl ${bookBg} border ${bookBdr} flex items-center justify-center mb-4`}>
            <BookOpen className={`w-7 h-7 ${bookColor} opacity-60`} />
          </div>
          <p className="text-foreground font-medium mb-1">
            {search ? 'No books found' : 'No books yet'}
          </p>
          <p className="text-muted-foreground text-sm max-w-xs">
            {search
              ? 'Try a different search term.'
              : 'Start reading on your KOReader device and your books will appear here automatically.'}
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-5">
          {filtered.map((book) => (
            <BookCard key={book.id} book={book} />
          ))}
        </div>
      )}
    </div>
  );
}
