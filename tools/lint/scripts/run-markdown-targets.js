"use strict";

const path = require("path");
const { spawnSync } = require("child_process");

const root = process.cwd();
const mode = process.argv[2];
const selectorArgs = process.argv.slice(3);
const scriptsDir = path.join(root, ".agents", "skills", "review-enforcer", "scripts");

if (!["text", "spell"].includes(mode)) {
  console.error("Usage: node tools/lint/scripts/run-markdown-targets.js <text|spell> [--changed|--files ...]");
  process.exit(2);
}

const listResult = spawnSync(
  process.execPath,
  [path.join(scriptsDir, "list-markdown-targets.js"), ...selectorArgs],
  {
    cwd: root,
    encoding: "utf8"
  }
);

if (listResult.error) {
  throw listResult.error;
}
if (listResult.status !== 0) {
  process.stderr.write(listResult.stderr || "");
  process.exit(listResult.status ?? 1);
}

const files = (listResult.stdout || "")
  .split(/\r?\n/)
  .map((line) => line.trim())
  .filter(Boolean);

if (files.length === 0) {
  process.exit(0);
}

if (mode === "spell") {
  run(
    process.execPath,
    [path.join(scriptsDir, "run-cspell-markdown.js"), ...files],
    false
  );
} else {
  const textlintCli = path.join(
    root,
    "node_modules",
    "textlint",
    "bin",
    "textlint.js"
  );
  run(
    process.execPath,
    [
      textlintCli,
      "--config",
      ".textlintrc.json",
      "--rulesdir",
      path.join(".agents", "skills", "review-enforcer", "scripts", "textlint-rules"),
      ...files
    ],
    false
  );
}

function run(command, args, shell) {
  const result = spawnSync(command, args, {
    cwd: root,
    stdio: "inherit",
    shell
  });

  if (result.error) {
    throw result.error;
  }
  process.exit(result.status ?? 1);
}
