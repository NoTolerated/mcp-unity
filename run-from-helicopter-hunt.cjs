const { spawn } = require('node:child_process');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..', '..');
const unityProjectRoot = path.join(repoRoot, 'games', 'helicopter-hunt', 'unity');
const serverEntry = path.join(__dirname, 'Server~', 'build', 'index.js');

const child = spawn(process.execPath, [serverEntry], {
  cwd: unityProjectRoot,
  env: process.env,
  stdio: 'inherit',
});

child.on('exit', (code, signal) => {
  if (signal) {
    process.kill(process.pid, signal);
    return;
  }

  process.exit(code ?? 0);
});

child.on('error', (error) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
});