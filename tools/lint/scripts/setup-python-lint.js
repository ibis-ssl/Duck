"use strict";

const fs = require("fs");
const path = require("path");
const { spawnSync } = require("child_process");

const root = process.cwd();
const venvDir = path.join(root, ".venv");
const python = resolveSystemPython();

if (!fs.existsSync(venvDir)) {
  run(python.command, [...python.prefix, "-m", "venv", ".venv"], {});
}

const venvPython = path.join(
  venvDir,
  process.platform === "win32" ? "Scripts/python.exe" : "bin/python"
);

run(
  venvPython,
  ["-m", "pip", "install", "-r", "tools/lint/requirements.txt"],
  {}
);

function resolveSystemPython() {
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

  console.error("Python 3 was not found.");
  process.exit(2);
}

function run(command, args, extraEnv) {
  const result = spawnSync(command, args, {
    cwd: root,
    stdio: "inherit",
    env: { ...process.env, ...extraEnv }
  });

  if (result.error) {
    throw result.error;
  }
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}
