const { readdirSync, readFileSync, statSync } = require('node:fs');
const { join } = require('node:path');

const forbidden = /HttpTestingController|provideHttpClientTesting|HttpClientTestingModule/;
const root = join('src', 'app', 'features');
const hits = [];

function walk(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) {
      walk(path);
      continue;
    }

    if (!name.endsWith('.spec.ts')) {
      continue;
    }

    const source = readFileSync(path, 'utf8');
    if (forbidden.test(source)) {
      hits.push(path);
    }
  }
}

walk(root);

if (hits.length > 0) {
  console.error('Component tests must use fake ports, not HTTP mocks (NFR-09):\n', hits.join('\n'));
  process.exit(1);
}

console.log('Verified: no HTTP mock in features/ component tests (NFR-09).');
