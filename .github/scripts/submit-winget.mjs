import { spawnSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { readFileSync, readdirSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { pathToFileURL } from 'node:url';

export const packageId = 'CyberGems.CyberWall';
export const upstream = 'microsoft/winget-pkgs';
export const packageRoot = 'manifests/c/CyberGems/CyberWall/';
const sourceRepository = 'CyberGems/CyberWall';
const manifestNames = [
  `${packageId}.installer.yaml`, `${packageId}.locale.en-US.yaml`, `${packageId}.yaml`,
];
const startMarker = '<!-- cybersnap-winget:start -->';
const endMarker = '<!-- cybersnap-winget:end -->';
const packageMention = /(?:^|[^A-Za-z0-9_.])CyberGems\.CyberWall(?=$|[^A-Za-z0-9_.])/;

export function compareVersions(left, right) {
  const parts = value => {
    if (!/^\d+(?:\.\d+){1,3}$/.test(value)) throw new Error(`Unsupported package version: ${value}`);
    return value.split('.').map(BigInt);
  };
  const a = parts(left), b = parts(right);
  for (let i = 0; i < Math.max(a.length, b.length); i++) {
    if ((a[i] ?? 0n) !== (b[i] ?? 0n)) return (a[i] ?? 0n) > (b[i] ?? 0n) ? 1 : -1;
  }
  return 0;
}

function scalar(content, key) {
  const match = content.match(new RegExp(`^${key}:\\s*([^\\r\\n]+)`, 'm'));
  if (!match) throw new Error(`Missing manifest field: ${key}`);
  return match[1].trim().replace(/^(['"])(.*)\1$/, '$2');
}

export function loadManifests(directory) {
  const names = readdirSync(directory).sort();
  if (names.join('|') !== [...manifestNames].sort().join('|')) {
    throw new Error('Expected exactly the three CyberWall release manifests.');
  }
  const files = manifestNames.map(name => ({ name, content: readFileSync(join(directory, name), 'utf8') }));
  const version = scalar(files[0].content, 'PackageVersion');
  compareVersions(version, version);
  for (const file of files) {
    if (scalar(file.content, 'PackageIdentifier') !== packageId || scalar(file.content, 'PackageVersion') !== version) {
      throw new Error('All release manifests must describe the same CyberWall version.');
    }
  }
  return { version, files };
}

export function submissionBody(version, previous = '') {
  const section = `${startMarker}\nAdds ${packageId} version ${version} to Windows Package Manager using the installer and manifests published in the [v${version} release](https://github.com/${sourceRepository}/releases/tag/v${version}).\n\nValidation: \`winget validate --manifest <directory>\` passed for these release manifests.\n${endMarker}`;
  if (!previous.includes(startMarker) && !previous.includes(endMarker)) return section + (previous ? `\n\n${previous}` : '');
  if (previous.split(startMarker).length !== 2 || previous.split(endMarker).length !== 2 || previous.indexOf(endMarker) < previous.indexOf(startMarker)) {
    throw new Error('Malformed managed PR description; preserve it for manual review.');
  }
  return previous.slice(0, previous.indexOf(startMarker)) + section + previous.slice(previous.indexOf(endMarker) + endMarker.length);
}

function list(api, endpoint) {
  const values = [];
  for (let page = 1; ; page++) {
    const batch = api('GET', `${endpoint}${endpoint.includes('?') ? '&' : '?'}per_page=100&page=${page}`);
    values.push(...batch);
    if (batch.length < 100) return values;
  }
}

function optional(api, endpoint) {
  try { return api('GET', endpoint); }
  catch (error) { if (error.status === 404) return null; throw error; }
}

function filesFor(api, pr) {
  const files = list(api, `repos/${upstream}/pulls/${pr.number}/files`);
  const versions = new Set(files.map(file => file.filename.slice(packageRoot.length).split('/')[0]));
  const version = [...versions][0];
  if (versions.size !== 1 || files.length !== manifestNames.length || files.some(file =>
    !manifestNames.some(name => file.filename === `${packageRoot}${version}/${name}`) || file.status === 'removed')) {
    throw new Error(`PR #${pr.number} has unexpected changes; refusing to replace or close it.`);
  }
  compareVersions(version, version);
  return { files, version };
}

function openSubmissions(api, login) {
  // The issues endpoint includes PRs and supports creator filtering without a search-index delay.
  const issues = list(api, `repos/${upstream}/issues?state=open&creator=${encodeURIComponent(login)}&sort=created&direction=asc`);
  const submissions = [];
  for (const issue of issues.filter(issue => issue.pull_request)) {
    const pr = api('GET', `repos/${upstream}/pulls/${issue.number}`);
    if (!packageMention.test(pr.title) && !packageMention.test(pr.head.ref) && !pr.body?.includes(startMarker)) continue;
    if (pr.state !== 'open' || pr.head.repo?.owner.login.toLowerCase() !== login.toLowerCase() || pr.base.ref !== 'master') {
      throw new Error(`Unexpected ownership or state for PR #${pr.number}.`);
    }
    submissions.push({ ...pr, ...filesFor(api, pr), labels: (issue.labels ?? []).map(label => label.name) });
  }
  return submissions.sort((a, b) => a.created_at.localeCompare(b.created_at) || a.number - b.number);
}

function assertUnchanged(api, pr) {
  const current = api('GET', `repos/${upstream}/pulls/${pr.number}`);
  if (current.state !== 'open' || current.head.sha !== pr.head.sha || current.head.repo?.full_name !== pr.head.repo.full_name ||
      current.title !== pr.title || current.body !== pr.body) {
    throw new Error(`PR #${pr.number} changed during submission; retry after inspecting it.`);
  }
}

function verifySubmission(api, pr, sha, version, previousSha, previousVersion, pause) {
  let observed;
  // GitHub can briefly serve the pre-update PR head or diff after accepting a
  // branch update. Retry reads only, and only for the exact prior state; a human
  // edit, unexpected files, or an API failure must still stop the submission.
  for (let attempt = 0; attempt < 8; attempt++) {
    const current = api('GET', `repos/${upstream}/pulls/${pr.number}`);
    if (current.state !== 'open' || ![sha, previousSha].includes(current.head.sha)) {
      throw new Error(`PR #${pr.number} changed during verification; duplicates were left open.`);
    }
    observed = `head ${current.head.sha}`;
    if (current.head.sha === sha) {
      const currentFiles = filesFor(api, current);
      if (currentFiles.version === version) return current;
      observed += `, version ${currentFiles.version}`;
      if (currentFiles.version !== previousVersion) {
        throw new Error(`PR #${pr.number} contains an unexpected version during verification (${observed}).`);
      }
    }
    if (attempt < 7) pause(2000);
  }
  throw new Error(`PR #${pr.number} did not converge to head ${sha}, version ${version} after 8 reads (${observed}); duplicates were left open.`);
}

const pauseForRead = milliseconds => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, milliseconds);

export function submitWinget({ api, manifests, author, closeSuperseded = true, makeBranchId = randomUUID, pause = pauseForRead }) {
  const user = api('GET', 'user');
  const pending = openSubmissions(api, user.login);
  if (pending.some(pr => compareVersions(pr.version, manifests.version) > 0)) {
    return { skipped: true, reason: 'A newer CyberWall version is already pending.' };
  }
  if (optional(api, `repos/${upstream}/contents/${packageRoot}${manifests.version}?ref=master`)) {
    return { skipped: true, reason: 'This CyberWall version is already in winget.' };
  }
  // The Validation-Completed label is what puts a submission in the moderator queue, and any
  // new commit removes it and restarts that review. winget-pkgs currently needs weeks for a
  // new package, so keep an already queued PR frozen instead of pushing every release to it.
  const queued = pending.find(pr => (pr.labels ?? []).includes('Validation-Completed'));
  if (queued) {
    return { skipped: true, reason: `PR #${queued.number} is already waiting for moderator review (Validation-Completed); skip the update to keep its place in the queue.` };
  }
  let kept = pending[0];
  const fork = kept?.head.repo.full_name ?? `${user.login}/winget-pkgs`;
  const forkInfo = api('GET', `repos/${fork}`);
  if (!forkInfo.fork || forkInfo.parent?.full_name !== upstream || forkInfo.owner.login.toLowerCase() !== user.login.toLowerCase()) {
    throw new Error('Expected an owned fork of microsoft/winget-pkgs.');
  }
  const parent = kept?.head.sha ?? api('GET', `repos/${upstream}/git/ref/heads/master`).object.sha;
  const previous = api('GET', `repos/${fork}/git/commits/${parent}`);
  const entries = new Map((kept?.files ?? []).map(file => [file.filename, {
    path: file.filename, mode: '100644', type: 'blob', sha: null,
  }]));
  for (const file of manifests.files) {
    const path = `${packageRoot}${manifests.version}/${file.name}`;
    entries.set(path, { path, mode: '100644', type: 'blob', content: file.content });
  }
  const tree = api('POST', `repos/${fork}/git/trees`, { base_tree: previous.tree.sha, tree: [...entries.values()] });
  let sha = parent;
  if (tree.sha !== previous.tree.sha) {
    sha = api('POST', `repos/${fork}/git/commits`, {
      message: `Update CyberWall winget submission to version ${manifests.version}`,
      tree: tree.sha, parents: [parent],
      author: author ?? { name: user.name || user.login, email: `${user.id}+${user.login}@users.noreply.github.com` },
    }).sha;
  }
  const title = `${packageId} version ${manifests.version}`;
  const body = submissionBody(manifests.version, kept?.body);
  if (kept) {
    assertUnchanged(api, kept);
    if (sha !== parent) {
      const branch = kept.head.ref.split('/').map(encodeURIComponent).join('/');
      api('PATCH', `repos/${fork}/git/refs/heads/${branch}`, { sha, force: false });
    }
    if (kept.title !== title || kept.body !== body) api('PATCH', `repos/${upstream}/pulls/${kept.number}`, { title, body });
  } else {
    const branch = `codex/winget-${packageId}-${manifests.version}-${makeBranchId()}`;
    api('POST', `repos/${fork}/git/refs`, { ref: `refs/heads/${branch}`, sha });
    kept = api('POST', `repos/${upstream}/pulls`, {
      title, body, base: 'master', head: `${user.login}:${branch}`, maintainer_can_modify: true,
    });
  }
  const verified = verifySubmission(api, kept, sha, manifests.version, parent, pending[0]?.version, pause);
  const closed = [];
  if (closeSuperseded) {
    for (const duplicate of pending.slice(1)) {
      assertUnchanged(api, verified);
      assertUnchanged(api, duplicate);
      api('POST', `repos/${upstream}/issues/${duplicate.number}/comments`, {
        body: `Superseded by #${kept.number}, which now contains ${packageId} version ${manifests.version}. Please follow that PR for the consolidated submission.`,
      });
      api('PATCH', `repos/${upstream}/pulls/${duplicate.number}`, { state: 'closed' });
      closed.push(duplicate.number);
    }
  }
  return { number: kept.number, url: verified.html_url, sha, closed };
}

function command(program, args, input) {
  const result = spawnSync(program, args, { encoding: 'utf8', input, maxBuffer: 16 * 1024 * 1024, timeout: 120_000 });
  if (result.error || result.status !== 0) {
    const error = new Error(result.error?.message || result.stderr || result.stdout || `${program} failed.`);
    error.status = Number((result.stderr || '').match(/HTTP (\d{3})/)?.[1]);
    throw error;
  }
  return result.stdout;
}

function githubApi(method, endpoint, body) {
  const args = ['api', '--method', method, endpoint];
  if (body !== undefined) args.push('--input', '-');
  return JSON.parse(command('gh', args, body === undefined ? undefined : JSON.stringify(body)));
}

function main() {
  const args = process.argv.slice(2);
  const directoryIndex = args.indexOf('--manifest-directory');
  if (directoryIndex < 0 || !args[directoryIndex + 1]) throw new Error('Usage: submit-winget.mjs --manifest-directory <directory> [--defer-close]');
  const directory = resolve(args[directoryIndex + 1]);
  const manifests = loadManifests(directory);
  // Reject delayed publication events before they can create or downgrade a PR.
  const latest = githubApi('GET', `repos/${sourceRepository}/releases/latest`);
  const latestVersion = latest.tag_name.replace(/^v/, '');
  if (compareVersions(manifests.version, latestVersion) < 0) {
    console.log(`Skipping superseded release v${manifests.version}; latest stable is ${latest.tag_name}.`);
    return;
  }
  if (latest.tag_name !== `v${manifests.version}`) throw new Error(`Expected manifests for the latest stable release (${latest.tag_name}).`);
  console.log(command('winget', ['validate', '--manifest', directory]).trim());
  const config = key => {
    const result = spawnSync('git', ['config', '--get', key], { encoding: 'utf8' });
    return result.status === 0 ? result.stdout.trim() : '';
  };
  const name = config('user.name'), email = config('user.email');
  const result = submitWinget({ api: githubApi, manifests, author: name && email ? { name, email } : undefined,
    closeSuperseded: !args.includes('--defer-close') });
  console.log(JSON.stringify(result, null, 2));
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try { main(); }
  catch (error) { console.error(error.message); process.exitCode = 1; }
}
