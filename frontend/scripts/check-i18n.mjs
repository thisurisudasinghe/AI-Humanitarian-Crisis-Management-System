import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dir = path.resolve(__dirname, '../lib/i18n/locales');

function flatten(obj, prefix = '') {
  const out = [];
  for (const [k, v] of Object.entries(obj)) {
    const p = prefix ? `${prefix}.${k}` : k;
    if (v && typeof v === 'object' && !Array.isArray(v)) out.push(...flatten(v, p));
    else out.push(p);
  }
  return out.sort();
}

function load(file) {
  let t = fs.readFileSync(path.join(dir, file), 'utf8');
  t = t.replace(/^import .+$/gm, '')
       .replace(/^export type .+$/gm, '')
       .replace(/^type .+$/gm, '')
       .replace(/export const \w+\s*(:\s*Dictionary)?\s*=\s*/, 'return ')
       .replace(/as const;\s*$/m, ';')
       .replace(/;\s*$/m, ';');
  return Function(t)();
}

const locales = ['en', 'si', 'ta'];
const dicts = Object.fromEntries(locales.map(l => [l, load(`${l}.ts`)]));
const keys = Object.fromEntries(locales.map(l => [l, flatten(dicts[l])]));
const en = keys.en;
let fail = false;

for (const l of ['si', 'ta']) {
  const missing = en.filter(k => !keys[l].includes(k));
  const extra = keys[l].filter(k => !en.includes(k));
  if (missing.length) { console.error(`${l} missing keys:`, missing); fail = true; }
  if (extra.length) { console.error(`${l} extra keys:`, extra); fail = true; }
}

for (const l of locales) {
  const empties = keys[l].filter(k => {
    const v = k.split('.').reduce((o, p) => o?.[p], dicts[l]);
    return typeof v === 'string' && v.trim() === '';
  });
  if (empties.length) { console.error(`${l} empty values:`, empties); fail = true; }
}

console.log(`i18n keys: ${en.length}, locales: ${locales.join(',')}`);
if (fail) process.exit(1);
console.log('i18n check ok');
