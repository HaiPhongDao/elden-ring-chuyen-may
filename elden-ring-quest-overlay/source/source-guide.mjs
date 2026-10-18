import { load } from 'cheerio';

const pages = [
  { part: 'base', url: 'https://eldenring.redmaw.dev/sheets/walkthrough' },
  { part: 'dlc', url: 'https://eldenring.redmaw.dev/sheets/dlc-walkthrough' },
];
async function main() {
const steps = [];
const sections = [];

for (const page of pages) {
  const response = await fetch(page.url, { headers: { 'user-agent': 'EldenQuestOverlay/0.3 personal guide reader' } });
  if (!response.ok) throw new Error(`${page.url}: HTTP ${response.status}`);
  const $ = load(await response.text());
  $('form h3').each((_, heading) => {
    const list = $(heading).next('ul');
    if (!/^[wd]\d+$/.test(list.attr('id') || '')) return;
    const sectionName = $(heading).clone().children('button').remove().end().text().replace(/\s+/g, ' ').trim();
    const sectionId = `${page.part}:${$(heading).attr('id') || list.attr('id')}`;
    const startIndex = steps.length;
    list.find('input[type="checkbox"][id]').each((__, input) => {
      const id = $(input).attr('id') || '';
      if (!/^[wd]\d+-\d+$/.test(id)) return;
      const label = $(input).closest('label');
      const text = label.text().replace(/\s+/g, ' ').trim();
      if (!text) return;
      const links = label.find('a[href]').map((___, anchor) => ({
        text: $(anchor).text().replace(/\s+/g, ' ').trim(),
        url: new URL($(anchor).attr('href') || '', page.url).href,
      })).get();
      const mapLink = links.find(link => link.url.includes('mapgenie.io'));
      const entity = mapLink || links.find(link => link.url.includes('eldenring.wiki.gg')) || links[0];
      steps.push({
        id: `${page.part}:${id}`,
        sectionId,
        section: sectionName,
        part: page.part,
        text,
        optional: /^Optional\b/i.test(text),
        missable: /^MISSABLE\b/i.test(text),
        note: /^Note:/i.test(text),
        mapQuery: entity?.text || sectionName,
        sourceUrl: `${page.url}#${$(heading).attr('id') || ''}`,
        links,
      });
    });
    if (steps.length > startIndex) sections.push({ id: sectionId, name: sectionName, part: page.part, startIndex, count: steps.length - startIndex });
  });
}

process.stdout.write(JSON.stringify({ source: 'Redmaw 100% Walkthrough', fetchedAt: new Date().toISOString(), sections, steps }));
}

main().catch(error => { console.error(error); process.exitCode = 1; });
