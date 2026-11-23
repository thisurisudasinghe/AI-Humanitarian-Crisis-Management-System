import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dir = path.resolve(__dirname, '../lib/i18n/locales');
const srcRoot = path.resolve(__dirname, '..');
let fail = false;

function failMsg(message) {
  console.error(message);
  fail = true;
}

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

function getValue(dict, key) {
  return key.split('.').reduce((o, p) => o?.[p], dict);
}

function placeholders(text) {
  return [...String(text).matchAll(/\{(\w+)\}/g)].map(m => m[1]).sort();
}

function walk(d, exts = ['.tsx', '.ts']) {
  const out = [];
  for (const e of fs.readdirSync(d, { withFileTypes: true })) {
    if (e.name === 'node_modules' || e.name === '.next' || e.name === 'dist') continue;
    const p = path.join(d, e.name);
    if (e.isDirectory()) out.push(...walk(p, exts));
    else if (exts.some(x => e.name.endsWith(x))) out.push(p);
  }
  return out;
}

function detectHardcoded() {
  const files = [...walk(path.join(srcRoot, 'app')), ...walk(path.join(srcRoot, 'components'))]
    .filter(f => f.endsWith('.tsx'))
    .filter(f => !f.includes(`${path.sep}lib${path.sep}i18n${path.sep}`));
  const issues = [];
  const attrRe = /\b(placeholder|title|aria-label|alt|label|description|emptyMessage|confirmLabel|message)\s*=\s*"([^"]{2,})"/g;
  const textRe = />\s*([A-Z][A-Za-z][A-Za-z0-9\s,'’\-:.!?()/]{2,})\s*</g;
  const allow = new Set(['SAFEWAY', 'SafeWay', 'Google', 'GitHub', 'SMS', 'API', 'PDF', 'JPG', 'PNG', 'WebP', 'EN', 'SI', 'TA', 'UTC', 'ID', 'USD', 'LKR', 'EUR', 'GBP']);
  for (const file of files) {
    const src = fs.readFileSync(file, 'utf8');
    const rel = path.relative(srcRoot, file).replace(/\\/g, '/');
    let m;
    while ((m = attrRe.exec(src))) {
      const v = m[2].trim();
      if (allow.has(v) || /^https?:/.test(v) || /^[0-9.,/% -]+$/.test(v) || v.includes('{')) continue;
      if (/[A-Za-z]{3}/.test(v)) issues.push(`${rel}:${m[1]}="${v}"`);
    }
    while ((m = textRe.exec(src))) {
      // "X" in `>X<` is JSX text only when followed by a closing tag; `=> X<Y>` is a TS generic.
      if (src[m.index + m[0].length] !== '/') continue;
      const v = m[1].trim();
      if (allow.has(v) || /^[0-9.,/% -]+$/.test(v) || v.includes('{') || v.length < 3) continue;
      if (/^[A-Z][a-z]+$/.test(v) && v.length < 4) continue;
      issues.push(`${rel}:text="${v}"`);
    }
  }
  return issues;
}

async function loadCore() {
  const ts = (await import('typescript')).default;
  const source = fs.readFileSync(path.join(srcRoot, 'lib/i18n/core.ts'), 'utf8');
  const js = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 }
  }).outputText;
  return import(`data:text/javascript;base64,${Buffer.from(js).toString('base64')}`);
}

function check(condition, message) {
  if (!condition) failMsg(`regression failure: ${message}`);
}

const locales = ['en', 'si', 'ta'];
const dicts = Object.fromEntries(locales.map(l => [l, load(`${l}.ts`)]));
const keys = Object.fromEntries(locales.map(l => [l, flatten(dicts[l])]));
const enKeys = keys.en;

const usedLiteralKeys = new Set();
for (const root of ['app', 'components', 'lib']) {
  for (const file of walk(path.join(srcRoot, root))) {
    if (file.includes(`${path.sep}lib${path.sep}i18n${path.sep}locales${path.sep}`)) continue;
    const source = fs.readFileSync(file, 'utf8');
    for (const match of source.matchAll(/\bt\(["']([^"']+)["']/g)) usedLiteralKeys.add(match[1]);
  }
}
const missingUsedKeys = [...usedLiteralKeys].filter(key => !enKeys.includes(key));
if (missingUsedKeys.length) {
  failMsg(`literal t() keys missing from dictionaries: ${missingUsedKeys.join(', ')}`);
}

for (const l of ['si', 'ta']) {
  const missing = enKeys.filter(k => !keys[l].includes(k));
  const extra = keys[l].filter(k => !enKeys.includes(k));
  if (missing.length) failMsg(`${l} missing keys: ${missing.join(', ')}`);
  if (extra.length) failMsg(`${l} extra keys: ${extra.join(', ')}`);
}

for (const l of locales) {
  const empties = keys[l].filter(k => {
    const v = getValue(dicts[l], k);
    return typeof v === 'string' && v.trim() === '';
  });
  if (empties.length) failMsg(`${l} empty values: ${empties.join(', ')}`);
}

for (const l of locales) {
  for (const key of enKeys) {
    const expected = placeholders(getValue(dicts.en, key));
    const actual = placeholders(getValue(dicts[l], key));
    if (expected.join('|') !== actual.join('|')) {
      failMsg(`${l} interpolation placeholder mismatch for ${key}: expected {${expected.join(',')}}, got {${actual.join(',')}}`);
    }
  }
}

const core = await loadCore();
check(core.resolveLocale('en') === 'en', 'resolveLocale accepts en');
check(core.resolveLocale('si') === 'si', 'resolveLocale accepts si');
check(core.resolveLocale('ta') === 'ta', 'resolveLocale accepts ta');
check(core.resolveLocale(null) === 'en', 'resolveLocale falls back to en');
check(core.resolveLocale('de') === 'en', 'resolveLocale rejects unsupported locale');

const tSi = core.createTranslator({ en: dicts.en, si: dicts.si }, 'si');
const localizedStep = tSi('report.stepOf', { step: 2, total: 3 });
check(!localizedStep.includes('{step}') && !localizedStep.includes('{total}'), 'interpolation replaces all variables');
check(tSi('report.stepOf', { step: 2 }).includes('{total}'), 'missing interpolation variables remain visible');
check(tSi('does.not.exist') === 'does.not.exist', 'missing key returns the key itself');

const tFallback = core.createTranslator({ en: dicts.en, si: {} }, 'si');
check(tFallback('common.backToHome') === getValue(dicts.en, 'common.backToHome'), 'missing localized key falls back to English');

for (const locale of locales) {
  check(typeof core.formatDate('2026-01-15T10:30:00Z', locale) === 'string', `formatDate works for ${locale}`);
  check(typeof core.formatNumber(12345.6, locale) === 'string', `formatNumber works for ${locale}`);
}

const coreSrc = fs.readFileSync(path.join(srcRoot, 'lib/i18n/core.ts'), 'utf8');
const providerSrc = fs.readFileSync(path.join(srcRoot, 'lib/i18n/index.tsx'), 'utf8');
check(coreSrc.includes('STORAGE_KEY = "safeway.locale"'), 'locale persistence key is safeway.locale');
check(coreSrc.includes('DEFAULT_LOCALE: Locale = "en"'), 'English is the default locale');
check(providerSrc.includes('formatDate, formatNumber } from "./core"'), 'locale-aware date/number helpers are re-exported');
check(providerSrc.includes('useState<Locale>(DEFAULT_LOCALE)'), 'provider starts with the default locale');
check(providerSrc.includes('document.documentElement.lang = stored'), 'stored locale updates html lang');
check(providerSrc.includes('document.documentElement.lang = next'), 'language switch updates html lang');
check(providerSrc.includes('window.localStorage.setItem(STORAGE_KEY, next)'), 'language switch persists to localStorage');

const errorSrc = fs.readFileSync(path.join(srcRoot, 'lib/i18n/errors.ts'), 'utf8');
check(errorSrc.includes('export function localizedErrorMessage'), 'stable API error localizer exists');
const referencedErrorKeys = [...errorSrc.matchAll(/apiErrors\.([A-Za-z]+)/g)].map(m => `apiErrors.${m[1]}`);
for (const key of new Set(referencedErrorKeys)) {
  check(enKeys.includes(key), `API error mapping key exists in dictionaries: ${key}`);
}
for (const key of ['VALIDATION_FAILED', 'NOT_FOUND', 'FORBIDDEN', 'UNAUTHORIZED', 'CONFLICT', 'DOMAIN_ERROR', 'GEOCODE_NOT_FOUND']) {
  check(errorSrc.includes(key), `API error code is mapped: ${key}`);
}

const sourceFiles = [...walk(path.join(srcRoot, 'app')), ...walk(path.join(srcRoot, 'components')), ...walk(path.join(srcRoot, 'lib'))]
  .filter(file => !file.includes(`${path.sep}lib${path.sep}i18n${path.sep}locales${path.sep}`));
const allSource = sourceFiles.map(file => fs.readFileSync(file, 'utf8')).join('\n');
check(!/new Intl\.(NumberFormat|DateTimeFormat)\(undefined/.test(allSource), 'no default-locale Intl formatters');
check(!/\.toLocale(String|DateString|TimeString)\(\)/.test(allSource), 'no default-locale toLocale helpers');

for (const role of ['Citizen', 'EmergencyCoordinator', 'CaseManager', 'LogisticsManager', 'ShelterAdmin', 'VolunteerCoordinator', 'Volunteer', 'DonorOfficer', 'PartnerOfficer', 'SystemAdmin', 'Donor']) {
  check(enKeys.includes(`roleLabels.${role}`), `role label exists for ${role}`);
}

const hardcoded = detectHardcoded();
console.log(`i18n keys: ${enKeys.length}, locales: ${locales.join(',')}`);
console.log(`hardcoded string candidates: ${hardcoded.length} (informational; review and translate where user-facing)`);
if (hardcoded.length) {
  for (const h of hardcoded.slice(0, 120)) console.log(' - ' + h);
  if (hardcoded.length > 120) console.log(` - ...and ${hardcoded.length - 120} more`);
}
if (fail) process.exit(1);
console.log('i18n check ok');
