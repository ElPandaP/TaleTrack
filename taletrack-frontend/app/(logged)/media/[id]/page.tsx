import { notFound } from 'next/navigation';
import { getMediaDetail } from '@/lib/api/server';
import MediaDetailClient from './MediaDetailClient';

export default async function MediaDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  let detail;
  try {
    const res = await getMediaDetail(id);
    detail = res.data;
  } catch {
    notFound();
  }

  if (!detail) notFound();

  return <MediaDetailClient detail={detail} />;
}
