"use strict";

const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");

const root = process.cwd();
const targetConfig = readTargetConfig();
const ignoreDirectories = new Set(targetConfig.ignoreDirectories);
const ignoredPrefixes = targetConfig.ignoredPrefixes;

const print0 = process.argv.includes("--print0");
const files = readExplicitMarkdownFiles() || (process.argv.includes("--changed") ? listChangedMarkdownFiles() : listAllMarkdownFiles(root));
const output = files.map((file) => path.relative(root, file).replaceAll(path.sep, "/"));

process.stdout.write(output.join(print0 ? "\0" : "\n"));
if (output.length > 0) {
  process.stdout.write(print0 ? "\0" : "\n");
}

function listChangedMarkdownFiles() {
  const candidates = new Set();
  addGitFiles(candidates, ["diff", "--name-only", "--diff-filter=ACMR", "--", "*.md"]);
  addGitFiles(candidates, ["diff", "--cached", "--name-only", "--diff-filter=ACMR", "--", "*.md"]);
  addGitFiles(candidates, ["ls-files", "--others", "--exclude-standard", "--", "*.md"]);

  return [...candidates]
    .filter((file) => file.endsWith(".md") && !isIgnored(file) && fs.existsSync(path.join(root, file)))
    .sort((left, right) => left.localeCompare(right))
    .map((file) => path.join(root, file));
}

function readExplicitMarkdownFiles() {
  const filesIndex = process.argv.indexOf("--files");
  if (filesIndex === -1) {
    return null;
  }

  const explicitFiles = process.argv.slice(filesIndex + 1).filter((arg) => !arg.startsWith("--"));
  return explicitFiles
    .map((file) => file.replaceAll(path.sep, "/"))
    .filter((file) => file.endsWith(".md") && !isIgnored(file) && fs.existsSync(path.join(root, file)))
    .sort((left, right) => left.localeCompare(right))
    .map((file) => path.join(root, file));
}

function addGitFiles(candidates, args) {
  const result = execFileSync("git", args, { cwd: root, encoding: "utf8" });
  for (const line of result.split(/\r?\n/)) {
    const file = line.trim();
    if (file) {
      candidates.add(file);
    }
  }
}

function listAllMarkdownFiles(directory) {
  const entries = fs.readdirSync(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const fullPath = path.join(directory, entry.name);
    const relativePath = path.relative(root, fullPath).replaceAll(path.sep, "/");

    if (entry.isDirectory()) {
      if (ignoreDirectories.has(entry.name) || ignoredPrefixes.some((prefix) => `${relativePath}/`.startsWith(prefix))) {
        continue;
      }

      files.push(...listAllMarkdownFiles(fullPath));
      continue;
    }

    if (entry.isFile() && entry.name.endsWith(".md") && !isIgnored(relativePath)) {
      files.push(fullPath);
    }
  }

  return files.sort((left, right) => left.localeCompare(right));
}

function isIgnored(relativePath) {
  const normalizedPath = relativePath.replaceAll(path.sep, "/");
  const pathSegments = normalizedPath.split("/");
  return ignoredPrefixes.some((prefix) => normalizedPath.startsWith(prefix)) || pathSegments.some((segment) => ignoreDirectories.has(segment));
}

function readTargetConfig() {
  const configPath = path.join(root, "tools", "lint", "markdown-targets.json");
  const config = JSON.parse(fs.readFileSync(configPath, "utf8"));
  return {
    ignoreDirectories: config.ignoreDirectories || [],
    ignoredPrefixes: config.ignoredPrefixes || []
  };
}
