"use strict";

const fs = require("fs");
const path = require("path");
const { spawnSync } = require("child_process");

const root = process.cwd();
const script = process.argv[2];
const args = process.argv.slice(3);

if (!script) {
  console.error("Usage: node tools/lint/scripts/run-python-script.js <script> [args...]");
  process.exit(2);
}

const python = resolvePython();
const result = spawnSync(python.command, [...python.prefix, script, ...args], {
  cwd: root,
  stdio: "inherit"
});

if (result.error) {
  throw result.error;
}
process.exit(result.status ?? 1);

function resolvePython() {
  const venvPython = path.join(
    root,
    ".venv",
    process.platform === "win32" ? "Scripts/python.exe" : "bin/python"
  );
  if (fs.existsSync(venvPython)) {
    return { command: venvPython, prefix: [] };
  }

  const candidates = process.platform === "win32"
    ? [
        { command: "python", prefix: [] },
        { command: "py", prefix: ["-3"] }
      ]
    : [
        { command: "python3", prefix: [] },
        { command: "python", prefix: [] }
      ];

  for (const candidate of candidates) {
    const probe = spawnSync(
      candidate.command,
      [...candidate.prefix, "--version"],
      { cwd: root, stdio: "ignore" }
    );
    if (!probe.error && probe.status === 0) {
      return candidate;
    }
  }

  console.error("Python 3 was not found. Install Python 3, then run `npm run lint:md:setup`.");
  process.exit(2);
}
