import { existsSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const templateUrl = new URL('../release-template.md', import.meta.url);
const template = readFileSync(templateUrl, 'utf8');
const startMarker = '<!-- changelog-summary:start -->';
const endMarker = '<!-- changelog-summary:end -->';
const errors = [];

const count = (value, token) => value.split(token).length - 1;
if (count(template, startMarker) !== 1 || count(template, endMarker) !== 1) {
  errors.push('Release template must contain exactly one changelog summary marker pair.');
}

const start = template.indexOf(startMarker);
const end = template.indexOf(endMarker);
let summary = '';

if (start >= 0 && end > start) {
  summary = template
    .slice(start + startMarker.length, end)
    .replace(/<!--\s*[\s\S]*?-->/g, ' ')
    .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1')
    .replace(/[*_~`>#]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

const words = summary.match(/[A-Za-z0-9][A-Za-z0-9'’-]*/g) ?? [];
const appName = template.match(/^##\s+.*?\b(Cyber[A-Za-z0-9]+)\s+\{\{VERSION\}\}/m)?.[1];

if (!summary) errors.push('Release summary is empty.');
if (words.length < 20 || words.length > 55) {
  errors.push(`Release summary must contain 20-55 words; found ${words.length}.`);
}
if (summary.length > 360) errors.push('Release summary must be 360 characters or fewer.');
if (/welcome\s+to\s+(?:the\s+)?official/i.test(summary)) {
  errors.push('Release summary must lead with changes, not a generic welcome.');
}
if (/\{\{(?:VERSION|RAW_VERSION|VERSION_NUM)\}\}/i.test(summary)) {
  errors.push('Release summary must not repeat a version token.');
}
if (/\b(?:TODO|TBD|REPLACE)\b/i.test(summary)) {
  errors.push('Release summary still contains placeholder text.');
}
if (appName && new RegExp(`\\b${appName}\\b`, 'i').test(summary)) {
  errors.push(`Release summary must not repeat the app name (${appName}).`);
}

// Images served from this repository break the rendered notes when the branch or the
// file does not exist, which is how the header logo went missing in v2.2.0.
const repoRawLink = /https:\/\/raw\.githubusercontent\.com\/CyberGems\/CyberWall\/([^/\s"'<>]+)\/([^\s"'<>)]+)/g;
for (const match of template.matchAll(repoRawLink)) {
  const [, branch, assetPath] = match;
  if (branch !== 'master') {
    errors.push(`Release notes reference the "${branch}" branch, but this repository only serves "master": ${match[0]}`);
    continue;
  }
  const assetUrl = new URL(`../../${assetPath}`, import.meta.url);
  if (!existsSync(fileURLToPath(assetUrl))) {
    errors.push(`Release notes reference a missing file: ${assetPath}`);
  }
}

if (errors.length > 0) {
  for (const error of errors) console.error(`::error::${error}`);
  process.exit(1);
}

console.log(`Release summary valid: ${words.length} words.`);
