import { getServerT } from '@/lib/i18n-server';

const CONTACT_EMAIL = 'pelayo.garcia.varela@gmail.com';
const LAST_UPDATED = '2026-09-30';

// Each section renders `privacy.<id>.heading`, then its blocks in order:
// a string is a paragraph key, an array is a bulleted list of item keys.
const SECTIONS: { id: string; blocks: (string | string[])[] }[] = [
  { id: 'who', blocks: ['p1'] },
  { id: 'data', blocks: ['p1', ['i1', 'i2', 'i3', 'i4'], 'p2'] },
  { id: 'share', blocks: ['p1', 'p2', ['i1', 'i2', 'i3'], 'p3'] },
  { id: 'cookies', blocks: ['p1', ['i1', 'i2', 'i3']] },
  { id: 'rights', blocks: ['p1', 'p2'] },
];

/** Renders a message, turning its `{email}` placeholder into a mailto link. */
function WithEmail({ text }: { text: string }) {
  const parts = text.split('{email}');
  return parts.map((part, i) => (
    <span key={i}>
      {part}
      {i < parts.length - 1 && (
        <a href={`mailto:${CONTACT_EMAIL}`} className="text-primary hover:underline">
          {CONTACT_EMAIL}
        </a>
      )}
    </span>
  ));
}

/** `/privacy`: the privacy policy, built from translation keys. Public Server Component. */
export default async function PrivacyPage() {
  const t = await getServerT();

  return (
    <div className="max-w-2xl">
      <h1 className="mb-1 font-heading text-2xl font-semibold">{t('privacy.title')}</h1>
      <p className="mb-6 text-sm text-muted-foreground">
        {t('privacy.updated')} {LAST_UPDATED}
      </p>

      <div className="divide-y divide-border">
        {SECTIONS.map(({ id, blocks }) => (
          <section key={id} className="flex flex-col gap-2 py-5 leading-relaxed text-foreground/90 first:pt-0">
            <h2 className="font-heading text-lg font-semibold text-foreground">{t(`privacy.${id}.heading`)}</h2>
            {blocks.map((block, i) =>
              typeof block === 'string' ? (
                <p key={i}>
                  <WithEmail text={t(`privacy.${id}.${block}`)} />
                </p>
              ) : (
                <ul key={i} className="list-disc space-y-1 pl-5">
                  {block.map((item) => (
                    <li key={item}>{t(`privacy.${id}.${item}`)}</li>
                  ))}
                </ul>
              ),
            )}
          </section>
        ))}
      </div>
    </div>
  );
}
