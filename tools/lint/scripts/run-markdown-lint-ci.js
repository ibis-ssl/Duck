"use strict";

const fs = require("fs");
const path = require("path");
const { spawnSync } = require("child_process");

const root = process.cwd();
const resultsDir = path.join(root, "artifacts", "markdown-lint");
fs.mkdirSync(resultsDir, { recursive: true });

const setup = runPhase("setup", ["run", "lint:md:setup"]);
let lint = { exitCode: -1, stdout: "", stderr: "" };

if (setup.exitCode === 0) {
  lint = runPhase("lint", ["run", "lint:md"]);
}

const passed = setup.exitCode === 0 && lint.exitCode === 0;
const failedPhase = setup.exitCode !== 0 ? "setup" : (lint.exitCode !== 0 ? "lint" : "none");

fs.writeFileSync(
  path.join(resultsDir, "test-result.txt"),
  [
    `status=${passed ? "passed" : "failed"}`,
    `failed_phase=${failedPhase}`,
    `setup_exit_code=${setup.exitCode}`,
    `lint_exit_code=${lint.exitCode}`,
    ""
  ].join("\n"),
  "utf8"
);

process.exit(passed ? 0 : 1);

function runPhase(name, args) {
  const command = process.platform === "win32" ? "npm.cmd" : "npm";
  const result = spawnSync(command, args, {
    cwd: root,
    encoding: "utf8",
    maxBuffer: 20 * 1024 * 1024,
    shell: process.platform === "win32"
  });

  const stdout = result.stdout || "";
  let stderr = result.stderr || "";
  if (result.error) {
    stderr += `${stderr ? "\n" : ""}${result.error.stack || result.error.message}\n`;
  }

  fs.writeFileSync(path.join(resultsDir, `${name}.stdout.log`), stdout, "utf8");
  fs.writeFileSync(path.join(resultsDir, `${name}.stderr.log`), stderr, "utf8");
  fs.writeFileSync(
    path.join(resultsDir, `${name}.exit-code`),
    `${result.status ?? 1}\n`,
    "utf8"
  );

  process.stdout.write(stdout);
  process.stderr.write(stderr);

  return {
    exitCode: result.status ?? 1,
    stdout,
    stderr
  };
}
