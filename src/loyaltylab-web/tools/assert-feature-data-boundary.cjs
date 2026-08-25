const { mkdirSync, writeFileSync, rmSync } = require('node:fs');
const { join } = require('node:path');
const { spawnSync } = require('node:child_process');

const checkDir = join('src', 'app', 'features', '_boundary-check');
const checkFile = join(checkDir, 'illegal-import.ts');

mkdirSync(checkDir, { recursive: true });
writeFileSync(
  checkFile,
  "import { dataLayer } from '../../data';\nexport const leak = dataLayer;\n",
);

const ngJs = join('node_modules', '@angular', 'cli', 'bin', 'ng.js');
const result = spawnSync(process.execPath, [ngJs, 'lint'], {
  encoding: 'utf8',
});

rmSync(checkDir, { recursive: true, force: true });

const output = `${result.stdout ?? ''}\n${result.stderr ?? ''}`;
if (result.status === 0) {
  console.error('Expected ng lint to reject a features/ import of data/.');
  process.exit(1);
}

if (!output.includes('no-restricted-imports')) {
  console.error('Lint failed, but not because of the layer boundary:\n', output);
  process.exit(1);
}

console.log('Verified: an import from features/ to data/ fails lint (NFR-09).');
