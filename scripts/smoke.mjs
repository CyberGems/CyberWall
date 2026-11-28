import { execFileSync } from 'node:child_process';
// Minimal deterministic smoke check for CyberWall.
// Verifies that the repo still has its entry points and the core domain vocabulary.
import fs from 'node:fs';
import path from 'node:path';

const APP = path.basename(process.cwd());
const CHECKS = {
  "CyberClock": {
    "files": [
      "src-tauri/tauri.conf.json",
      "src"
    ],
    "keywords": [
      "clock",
      "timer",
      "calendar"
    ]
  },
  "CyberFeeds": {
    "files": [
      "package.json",
      "src"
    ],
    "keywords": [
      "rss",
      "feed",
      "youtube"
    ]
  },
  "CyberFiles": {
    "files": [
      "src-tauri/tauri.conf.json",
      "src"
    ],
    "keywords": [
      "dual",
      "pane",
      "file"
    ]
  },
  "CyberGo": {
    "files": [
      "package.json",
      "src"
    ],
    "keywords": [
      "reminder",
      "agenda",
      "task"
    ]
  },
  "CyberLauncher": {
    "files": [
      "package.json",
      "src"
    ],
    "keywords": [
      "hotkey",
      "launcher",
      "terminal"
    ]
  },
  "CyberManager": {
    "files": [
      "Directory.Build.props",
      "src"
    ],
    "keywords": [
      "process",
      "task manager",
      "compact"
    ]
  },
  "CyberNotes": {
    "files": [
      "package.json",
      "src"
    ],
    "keywords": [
      "note",
      "folder",
      "floating"
    ]
  },
  "CyberPaste": {
    "files": [
      "src-tauri/tauri.conf.json",
      "frontend"
    ],
    "keywords": [
      "clipboard",
      "history",
      "local"
    ],
    "walk": [
      "frontend",
      "src-tauri",
      "scripts"
    ]
  },
  "CyberSnap": {
    "files": [
      "Directory.Build.props",
      "tests/CyberSnap.ResultTabs.Smoke"
    ],
    "keywords": [
      "capture",
      "annotation",
      "ocr"
    ]
  },
  "CyberTray": {
    "files": [
      "package.json",
      "src"
    ],
    "keywords": [
      "hotspot",
      "tray",
      "shortcut"
    ]
  },
  "CyberViewer": {
    "files": [
      "package.json",
      "main.js"
    ],
    "keywords": [
      "viewer",
      "video",
      "audio"
    ],
    "walk": [
      "."
    ]
  },
  "CyberWall": {
    "files": [
      "Directory.Build.props",
      "src"
    ],
    "keywords": [
      "firewall",
      "mode",
      "connection"
    ]
  }
};
const cfg = CHECKS[APP];
if (!cfg) {
  console.error(`No smoke config for ${APP}`);
  process.exit(1);
}

const problems = [];
for (const rel of cfg.files) {
  if (!fs.existsSync(path.resolve(rel))) problems.push(`missing: ${rel}`);
}

const skip = new Set(['node_modules', 'dist', 'target', 'bin', 'obj', '.git', 'artifacts']);
const exts = new Set(['.ts', '.tsx', '.js', '.jsx', '.mjs', '.cjs', '.cs', '.rs', '.json', '.html', '.css']);
const chunks = [];
function walk(dir, depth = 0) {
  if (depth > 8 || !fs.existsSync(dir)) return;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (skip.has(entry.name)) continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, depth + 1);
    else if (exts.has(path.extname(entry.name))) {
      try { chunks.push(fs.readFileSync(full, 'utf8')); } catch {}
    }
  }
}
for (const dir of cfg.walk ?? ['src', 'tests', 'src-tauri']) walk(dir);
const corpus = chunks.join('\n').toLowerCase();

for (const kw of cfg.keywords) {
  if (!corpus.includes(kw.toLowerCase())) problems.push(`missing keyword: ${kw}`);
}

if (fs.existsSync('package.json')) {
  try {
    const pkg = JSON.parse(fs.readFileSync('package.json', 'utf8'));
    if (!pkg.scripts?.build && !pkg.scripts?.dev && !pkg.scripts?.start) problems.push('package.json has no build/dev/start script');
  } catch (err) {
    problems.push(`invalid package.json: ${err.message}`);
  }
}

if (fs.existsSync('src-tauri/Cargo.toml')) {
  const cargo = fs.readFileSync('src-tauri/Cargo.toml', 'utf8');
  if (!cargo.includes('[package]')) problems.push('src-tauri/Cargo.toml has no [package] section');
}

if (!fs.existsSync('LICENSE')) problems.push('missing: LICENSE');

if (problems.length) {
  console.error(`SMOKE FAIL ${APP}`);
  for (const problem of problems) console.error(` - ${problem}`);
  process.exit(1);
}
console.log(`SMOKE PASS ${APP} (${cfg.files.length} paths, ${cfg.keywords.length} keywords)`);

execFileSync(process.execPath, ['scripts/check-github-star.mjs'], { stdio: 'inherit' });
