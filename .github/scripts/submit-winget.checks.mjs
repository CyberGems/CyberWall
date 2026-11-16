import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, rmSync, rmdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { compareVersions, loadManifests, packageId, packageRoot, submissionBody, submitWinget, upstream } from './submit-winget.mjs';

const fork = 'CyberGems/winget-pkgs';
const names = [`${packageId}.installer.yaml`, `${packageId}.locale.en-US.yaml`, `${packageId}.yaml`];
const manifests = version => ({ version, files: names.map(name => ({
  name, content: `PackageIdentifier: ${packageId}\nPackageVersion: ${version}\n`,
})) });
const files = version => names.map(name => ({ filename: `${packageRoot}${version}/${name}`, status: 'added' }));
const pull = (number, version, date = '2026-10-10', labels = []) => ({
  number, title: `${packageId} version ${version}`, body: submissionBody(version),
  created_at: date + 'T00:00:00Z', state: 'open', html_url: `https://github.com/${upstream}/pull/${number}`,
  head: { sha: `head-${number}`, ref: `${packageId}-${version}-${number}`,
    repo: { full_name: fork, owner: { login: 'CyberGems' } } },
  base: { ref: 'master' }, files: files(version), labels: labels.map(name => ({ name })),
});
const clone = value => structuredClone(value);
const missing = () => { const error = new Error('Not found'); error.status = 404; throw error; };

function fixture(prs = [], options = {}) {
  const state = { prs: new Map(prs.map(pr => [pr.number, clone(pr)])), calls: [], comments: [], branches: [], commits: [] };
  const api = (method, path, body) => {
    state.calls.push({ method, path, body: clone(body) });
    options.before?.({ method, path, body, state });
    const result = route(method, path, body);
    return clone(options.response?.({ method, path, body, result: clone(result), state }) ?? result);
  };
  function route(method, path, body) {
    if (path === 'user') return { login: 'CyberGems', name: 'CyberGems', id: 282432203 };
    if (path.startsWith(`repos/${upstream}/issues?`)) {
      const page = Number(new URLSearchParams(path.split('?')[1]).get('page'));
      if (options.paginated && page === 1) return Array.from({ length: 100 }, (_, i) => ({ number: i }));
      if (page > (options.paginated ? 2 : 1)) return [];
      return [...state.prs.values()].filter(pr => pr.state === 'open').map(pr => ({ number: pr.number, pull_request: {}, labels: pr.labels ?? [] }));
    }
    if (path.startsWith(`repos/${upstream}/contents/`)) {
      if (options.merged) return [{ name: 'already-listed.yaml' }];
      return missing();
    }
    if (path === `repos/${fork}`) return { fork: true, parent: { full_name: upstream }, owner: { login: 'CyberGems' } };
    if (path === `repos/${upstream}/git/ref/heads/master`) return { object: { sha: 'base-head' } };
    if (path.startsWith(`repos/${fork}/git/commits/`)) return { tree: { sha: 'previous-tree' } };
    if (path === `repos/${fork}/git/trees` && method === 'POST') {
      state.tree = body;
      return { sha: options.unchanged ? 'previous-tree' : 'new-tree' };
    }
    if (path === `repos/${fork}/git/commits` && method === 'POST') {
      state.commits.push(body); return { sha: 'new-commit' };
    }
    if (path.startsWith(`repos/${fork}/git/refs/heads/`) && method === 'PATCH') {
      assert.equal(body.force, false);
      const pr = [...state.prs.values()].find(pr => path.endsWith(pr.head.ref));
      assert.ok(pr, 'only an existing PR branch may be updated');
      pr.head.sha = body.sha;
      pr.files = state.tree.tree.filter(entry => 'content' in entry).map(entry => ({ filename: entry.path, status: 'added' }));
      return { object: { sha: body.sha } };
    }
    if (path === `repos/${fork}/git/refs` && method === 'POST') { state.branches.push(body); return body; }
    if (path === `repos/${upstream}/pulls` && method === 'POST') {
      const pr = pull(500, '1.21.0');
      Object.assign(pr, { title: body.title, body: body.body });
      pr.head.sha = state.branches.at(-1).sha;
      pr.head.ref = body.head.split(':')[1];
      state.prs.set(pr.number, pr); return pr;
    }
    const fileMatch = path.match(/\/pulls\/(\d+)\/files\?/);
    if (fileMatch) return state.prs.get(Number(fileMatch[1])).files;
    const prMatch = path.match(/\/pulls\/(\d+)$/);
    if (prMatch) {
      const pr = state.prs.get(Number(prMatch[1]));
      if (method === 'PATCH') Object.assign(pr, body);
      return pr;
    }
    if (/\/issues\/\d+\/comments$/.test(path) && method === 'POST') { state.comments.push(body); return body; }
    throw new Error(`Unexpected API call: ${method} ${path}`);
  }
  return { state, run: extra => submitWinget({ api, manifests: manifests('1.21.0'), makeBranchId: () => 'fixture', ...extra }) };
}

const writes = state => state.calls.filter(call => call.method !== 'GET');

test('keeps the oldest PR, replaces its version and closes later submissions only after verification', () => {
  const { state, run } = fixture([pull(3, '1.21.0', '2026-10-20'), pull(1, '1.18.0'), pull(2, '1.19.0', '2026-10-16')]);
  const result = run();
  assert.equal(result.number, 1);
  assert.deepEqual(result.closed, [2, 3]);
  assert.equal(state.prs.get(1).created_at, '2026-10-10T00:00:00Z');
  assert.deepEqual(state.prs.get(1).files, files('1.21.0'));
  assert.equal(state.prs.get(1).title, `${packageId} version 1.21.0`);
  assert.equal(state.tree.base_tree, 'previous-tree');
  assert.deepEqual(state.tree.tree.filter(entry => entry.sha === null).map(entry => entry.path), files('1.18.0').map(file => file.filename));
  assert.deepEqual(state.commits[0].parents, ['head-1']);
  assert.deepEqual(state.commits[0].author, { name: 'CyberGems', email: '282432203+CyberGems@users.noreply.github.com' });
  assert.equal(state.prs.get(2).state, 'closed');
  assert.match(state.comments[0].body, /#1/);
  assert.equal(state.branches.length, 0);
  const refUpdate = state.calls.findIndex(call => call.path.includes('/git/refs/heads/'));
  const verification = state.calls.findIndex((call, i) => i > refUpdate && call.path.includes('/pulls/1/files?'));
  const firstClose = state.calls.findIndex(call => call.body?.state === 'closed');
  assert.ok(verification > refUpdate && firstClose > verification);
});

test('creates a new PR only when no CyberWall submission remains open', () => {
  const { state, run } = fixture();
  const result = run({ author: { name: 'Configured user', email: 'configured@example.test' } });
  assert.equal(result.number, 500);
  assert.match(state.branches[0].ref, /^refs\/heads\/codex\/winget-CyberGems\.CyberWall-1\.21\.0-/);
  assert.deepEqual(state.commits[0].parents, ['base-head']);
  assert.equal(state.commits[0].author.name, 'Configured user');
});

test('retrying the same release does not create a commit, update the ref or open another PR', () => {
  const { state, run } = fixture([pull(1, '1.21.0')], { unchanged: true });
  assert.equal(run().number, 1);
  assert.equal(state.commits.length, 0);
  assert.equal(state.branches.length, 0);
  assert.equal(writes(state).length, 1); // Creating an identical Git tree is harmless and returns its existing SHA.
});

test('waits for the prior PR head and diff to converge before closing duplicates', () => {
  let staleHeadReads = 0, staleDiffReads = 0;
  const pauses = [];
  const original = pull(1, '1.18.0');
  const { state, run } = fixture([original, pull(2, '1.19.0')], {
    response({ method, path, result, state }) {
      if (method !== 'GET' || state.prs.get(1).head.sha !== 'new-commit') return result;
      if (path.endsWith('/pulls/1') && staleHeadReads++ < 2) return original;
      if (path.includes('/pulls/1/files?') && staleDiffReads++ < 2) return files('1.18.0');
      return result;
    },
  });
  const result = run({ pause: milliseconds => {
    pauses.push(milliseconds);
    assert.equal(state.prs.get(2).state, 'open');
    assert.deepEqual(state.comments, []);
  } });
  assert.equal(result.sha, 'new-commit');
  assert.deepEqual(pauses, [2000, 2000, 2000, 2000]);
  assert.deepEqual(result.closed, [2]);
  assert.equal(state.commits.length, 1);
  assert.equal(state.calls.filter(call => call.path.includes('/git/refs/heads/')).length, 1);
  assert.equal(state.branches.length, 0);
});

test('a stale diff that never converges fails without closing duplicates or repeating writes', () => {
  let staleReads = 0, pauses = 0;
  const { state, run } = fixture([pull(1, '1.18.0'), pull(2, '1.19.0')], {
    response({ method, path, result, state }) {
      if (method === 'GET' && path.includes('/pulls/1/files?') && state.prs.get(1).head.sha === 'new-commit') {
        staleReads++;
        return files('1.18.0');
      }
      return result;
    },
  });
  assert.throws(() => run({ pause: () => pauses++ }), /did not converge.*after 8 reads/);
  assert.equal(staleReads, 8);
  assert.equal(pauses, 7);
  assert.equal(state.prs.get(2).state, 'open');
  assert.deepEqual(state.comments, []);
  assert.equal(state.commits.length, 1);
  assert.equal(state.branches.length, 0);
});

test('a human branch edit during verification fails immediately instead of retrying', () => {
  const { state, run } = fixture([pull(1, '1.18.0'), pull(2, '1.19.0')], {
    response({ method, path, result, state }) {
      if (method === 'GET' && path.endsWith('/pulls/1') && state.prs.get(1).head.sha === 'new-commit') {
        result.head.sha = 'human-edit';
      }
      return result;
    },
  });
  assert.throws(() => run({ pause: () => assert.fail('Concurrent edits must not be retried') }), /changed during verification/);
  assert.equal(state.prs.get(2).state, 'open');
  assert.deepEqual(state.comments, []);
});

test('unexpected diff files after an update fail immediately instead of being treated as stale', () => {
  const { state, run } = fixture([pull(1, '1.18.0'), pull(2, '1.19.0')], {
    response({ method, path, result, state }) {
      if (method === 'GET' && path.includes('/pulls/1/files?') && state.prs.get(1).head.sha === 'new-commit') {
        result.push({ filename: 'README.md', status: 'modified' });
      }
      return result;
    },
  });
  assert.throws(() => run({ pause: () => assert.fail('Unexpected files must not be retried') }), /unexpected changes/);
  assert.equal(state.prs.get(2).state, 'open');
  assert.deepEqual(state.comments, []);
});

test('an older release cannot replace any newer pending submission', () => {
  const { state, run } = fixture([pull(1, '1.18.0'), pull(2, '1.22.0', '2026-10-20')]);
  assert.equal(run().skipped, true);
  assert.deepEqual(writes(state), []);
});

test('already published winget versions do not create another PR', () => {
  const { state, run } = fixture([], { merged: true });
  assert.equal(run().skipped, true);
  assert.deepEqual(writes(state), []);
});

test('a pending PR already waiting for moderator review is frozen instead of updated', () => {
  const { state, run } = fixture([pull(1, '1.18.0', '2026-10-10', ['New-Package', 'Validation-Completed'])]);
  const result = run();
  assert.equal(result.skipped, true);
  assert.match(result.reason, /#1.*moderator review/);
  assert.deepEqual(writes(state), []);
});

test('a pending PR without the moderator label is still updated', () => {
  const { state, run } = fixture([pull(1, '1.18.0', '2026-10-10', ['New-Package'])]);
  assert.equal(run().number, 1);
  assert.equal(state.commits.length, 1);
  assert.deepEqual(state.prs.get(1).files, files('1.21.0'));
});

test('pagination and unrelated packages do not hide or change the pending CyberWall PR', () => {
  const other = pull(9, '1.21.0');
  other.title = 'CyberGems.CyberFeeds version 1.0.0'; other.head.ref = 'CyberGems.CyberFeeds-1.0.0';
  other.body = 'Adds CyberFeeds.';
  other.files = [{ filename: 'manifests/c/CyberGems/CyberFeeds/1.0.0/CyberGems.CyberFeeds.yaml', status: 'added' }];
  const { state, run } = fixture([other, pull(1, '1.18.0')], { paginated: true });
  assert.equal(run().number, 1);
  assert.equal(state.prs.get(9).state, 'open');
  assert.equal(state.prs.get(9).head.sha, 'head-9');
});

test('a similarly named package is never treated as CyberWall', () => {
  const other = pull(9, '1.21.0');
  other.title = 'CyberGems.CyberWallBeta version 1.0.0';
  other.head.ref = 'CyberGems.CyberWallBeta-1.0.0';
  other.body = 'Adds a different package.';
  other.files = [{ filename: 'manifests/c/CyberGems/CyberWallBeta/1.0.0/manifest.yaml', status: 'added' }];
  const { state, run } = fixture([other, pull(1, '1.18.0')]);
  assert.equal(run().number, 1);
  assert.equal(state.prs.get(9).state, 'open');
});

test('unexpected files are preserved for review instead of being overwritten', () => {
  const pr = pull(1, '1.18.0'); pr.files.push({ filename: 'README.md', status: 'modified' });
  const { state, run } = fixture([pr]);
  assert.throws(run, /unexpected changes/);
  assert.deepEqual(writes(state), []);
});

test('read failures propagate without falling back to creating a duplicate PR', () => {
  const { state, run } = fixture([pull(1, '1.18.0')], {
    before({ path }) { if (path.includes('/issues?')) throw new Error('API unavailable'); },
  });
  assert.throws(run, /API unavailable/);
  assert.deepEqual(writes(state), []);
});

test('a failed non-forced ref update leaves every duplicate open', () => {
  const { state, run } = fixture([pull(1, '1.18.0'), pull(2, '1.19.0')], {
    before({ path }) { if (path.includes('/git/refs/heads/')) throw new Error('Not a fast forward'); },
  });
  assert.throws(run, /Not a fast forward/);
  assert.equal(state.prs.get(2).state, 'open');
  assert.equal(state.branches.length, 0);
  assert.deepEqual(state.comments, []);
});

test('concurrent edits to the retained PR stop the update without overwriting them', () => {
  const { state, run } = fixture([pull(1, '1.18.0')], {
    before({ method, path, state }) {
      if (method === 'POST' && path.endsWith('/git/commits')) state.prs.get(1).head.sha = 'human-edit';
    },
  });
  assert.throws(run, /changed during submission/);
  assert.equal(state.prs.get(1).head.sha, 'human-edit');
  assert.equal(state.calls.some(call => call.path.includes('/git/refs/heads/')), false);
});

test('concurrent edits to a PR description are preserved', () => {
  const { state, run } = fixture([pull(1, '1.18.0')], {
    before({ method, path, state }) {
      if (method === 'POST' && path.endsWith('/git/commits')) state.prs.get(1).body += '\nHuman note';
    },
  });
  assert.throws(run, /changed during submission/);
  assert.ok(state.prs.get(1).body.endsWith('Human note'));
  assert.equal(state.calls.some(call => call.path.includes('/git/refs/heads/')), false);
});

test('a duplicate modified during submission is left open', () => {
  const { state, run } = fixture([pull(1, '1.18.0'), pull(2, '1.19.0')], {
    before({ method, path, state }) {
      if (method === 'PATCH' && path.includes('/git/refs/heads/')) state.prs.get(2).head.sha = 'human-edit';
    },
  });
  assert.throws(run, /changed during submission/);
  assert.equal(state.prs.get(2).state, 'open');
  assert.deepEqual(state.comments, []);
});

test('manual validation can defer closing duplicates', () => {
  const { state, run } = fixture([pull(1, '1.18.0'), pull(2, '1.19.0')]);
  assert.deepEqual(run({ closeSuperseded: false }).closed, []);
  assert.equal(state.prs.get(2).state, 'open');
});

test('updates the managed description while preserving reviewer notes', () => {
  const before = `Publisher note\n${submissionBody('1.18.0')}\nReviewer note`;
  const after = submissionBody('1.21.0', before);
  assert.ok(after.startsWith('Publisher note\n'));
  assert.ok(after.endsWith('\nReviewer note'));
  assert.equal(after.includes('1.18.0'), false);
  assert.equal(submissionBody('1.21.0', after), after);
  assert.throws(() => submissionBody('1.21.0', '<!-- cybersnap-winget:start -->'), /Malformed/);
});

test('compares numeric versions and rejects unsafe path components', () => {
  assert.equal(compareVersions('1.10.0', '1.9.0'), 1);
  assert.equal(compareVersions('1.21.0', '1.21.0.0'), 0);
  assert.equal(compareVersions('1.20.1', '1.21.0'), -1);
  assert.throws(() => compareVersions('../1.21.0', '1.21.0'), /Unsupported/);
});

test('only accepts the complete set of manifests for one CyberWall version', () => {
  const directory = mkdtempSync(join(tmpdir(), 'cybersnap-winget-test-'));
  try {
    for (const file of manifests('1.21.0').files) writeFileSync(join(directory, file.name), file.content);
    assert.equal(loadManifests(directory).version, '1.21.0');
    writeFileSync(join(directory, names[0]), 'PackageIdentifier: Other.App\nPackageVersion: 1.21.0\n');
    assert.throws(() => loadManifests(directory), /same CyberWall version/);
    writeFileSync(join(directory, 'extra.yaml'), 'unexpected');
    assert.throws(() => loadManifests(directory), /exactly the three/);
  } finally {
    for (const name of [...names, 'extra.yaml']) rmSync(join(directory, name), { force: true });
    rmdirSync(directory);
  }
});
