import { redirect } from 'next/navigation';

// The logged-in home now lives at `/`. Keep this path working for old links.
export default function DashboardPage() {
  redirect('/');
}
