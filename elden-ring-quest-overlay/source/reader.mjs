import { readFileSync } from 'node:fs';
import { parse } from '@zebbedaja/er-save-parser';
import { STEPS } from './guide.js';

const savePath = process.argv[2];
const slotIndex = Number(process.argv[3] ?? 0);
if (!savePath || !Number.isInteger(slotIndex) || slotIndex < 0) {
  throw new Error('Cần đường dẫn ER0000.sl2 và số slot hợp lệ.');
}

const bytes = readFileSync(savePath);
const save = parse(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), { logLevel: 'none' });
const slots = (save.slots || []).map((slot, index) => slot?.character ? {
  index,
  name: slot.character.characterName || `Nhân vật ${index + 1}`,
  level: slot.character.level ?? 0,
} : null).filter(Boolean);
const selected = save.slots?.[slotIndex];
if (!selected?.character) throw new Error(`Slot ${slotIndex + 1} không có nhân vật.`);
const flags = new Set((selected.eventFlags || []).filter(flag => flag.state).map(flag => flag.id));

process.stdout.write(JSON.stringify({
  savePath,
  slotIndex,
  slots,
  readAt: new Date().toISOString(),
  steps: STEPS.map((step, index) => ({
    index,
    id: step.id,
    phase: step.phase,
    title: step.title,
    kind: step.kind,
    where: step.where,
    actions: step.actions,
    warning: step.warning || '',
    mapQuery: step.mapQuery,
    mapUrl: step.mapUrl,
    guideUrl: step.guideUrl,
    manual: Boolean(step.manual),
    flag: step.flag || null,
    confirmedBySave: !step.manual && flags.has(step.flag),
  })),
}));
