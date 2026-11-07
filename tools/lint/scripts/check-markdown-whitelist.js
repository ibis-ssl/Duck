"use strict";

const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");
const { createRequire } = require("module");

const root = process.cwd();
const requireFromRepo = createRequire(path.join(root, "package.json"));
const YAML = requireFromRepo("yaml");
const whitelistPath = path.join(root, "tools", "lint", "markdown-whitelist.yaml");
const targetConfig = readTargetConfig();
const ignoreDirectories = new Set(targetConfig.ignoreDirectories);
const ignoredPrefixes = targetConfig.ignoredPrefixes;
const readsStdin = process.argv[2] === "--stdin";

const whitelist = readWhitelist(whitelistPath);
const inputs = readInputs().concat(readsStdin ? [] : readWhitelistDescriptionInputs(whitelist.entries));
const violations = [];
const unknownWords = new Map();

for (const input of inputs) {
  const relativePath = input.relativePath;
  const text = input.text;
  const cleaned = maskWhitelistValues(stripMarkdownNoise(text), whitelist.valuePattern);

  checkTokens(relativePath, cleaned, /(?<![A-Za-z0-9])[A-Za-z][A-Za-z0-9]*(?:[._-][A-Za-z0-9]+)*/g, shouldCheckEnglishToken);
  checkTokens(relativePath, cleaned, /(?<![\u30A0-\u30FF])[\u30A0-\u30FF]+(?:[・ー][\u30A0-\u30FF]+)*(?![\u30A0-\u30FF])/gu, shouldCheckKatakanaToken);
}

if (process.argv.includes("--list-unknown")) {
  for (const [, item] of [...unknownWords.entries()].sort((left, right) => left[0].localeCompare(right[0]))) {
    console.log(`${item.word}\t${item.count}`);
  }

  process.exit(violations.length > 0 ? 1 : 0);
}

if (violations.length > 0) {
  console.error("Markdown whitelist violations:");
  for (const violation of violations.slice(0, 200)) {
    console.error(`- ${violation}`);
  }

  if (violations.length > 200) {
    console.error(`- ... ${violations.length - 200} more violations`);
  }

  process.exit(1);
}

function readInputs() {
  if (readsStdin) {
    const relativePath = process.argv[3] || "<stdin>";
    const text = fs.readFileSync(0, "utf8");
    if (normalizePath(relativePath) === normalizePath(path.relative(root, whitelistPath))) {
      return readWhitelistValueInputs(relativePath, text);
    }

    return [{ relativePath, text }];
  }

  const filePaths = readExplicitMarkdownFiles() || (process.argv.includes("--changed") ? listChangedMarkdownFiles() : listMarkdownFiles(root));
  return filePaths.map((filePath) => ({
    relativePath: path.relative(root, filePath).replaceAll(path.sep, "/"),
    text: fs.readFileSync(filePath, "utf8")
  }));
}

function readWhitelistValueInputs(relativePath, text) {
  const data = YAML.parse(text);
  const entries = Array.isArray(data.entries) ? data.entries : [];
  return entries.flatMap((entry) => {
    const values = entryValues(entry).map((value) => ({
      relativePath: `${relativePath}:${entry.term} [whitelist value]`,
      text: value
    }));
    if (entry.description) {
      values.push({
        relativePath: `${relativePath}:${entry.term} [whitelist description]`,
        text: entry.description
      });
    }

    return values;
  });
}

function normalizePath(filePath) {
  return filePath.replaceAll(path.sep, "/");
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

function readWhitelistDescriptionInputs(entries) {
  return entries.map((entry) => ({
    relativePath: `${path.relative(root, whitelistPath)}:${entry.term} [whitelist description]`,
    text: entry.description
  }));
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

function addGitFiles(candidates, args) {
  const result = execFileSync("git", args, { cwd: root, encoding: "utf8" });
  for (const line of result.split(/\r?\n/)) {
    const file = line.trim();
    if (file) {
      candidates.add(file);
    }
  }
}

function listMarkdownFiles(directory) {
  const entries = fs.readdirSync(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const fullPath = path.join(directory, entry.name);
    const relativePath = path.relative(root, fullPath).replaceAll(path.sep, "/");

    if (entry.isDirectory()) {
      if (ignoreDirectories.has(entry.name) || ignoredPrefixes.some((prefix) => `${relativePath}/`.startsWith(prefix))) {
        continue;
      }

      files.push(...listMarkdownFiles(fullPath));
      continue;
    }

    if (entry.isFile() && entry.name.endsWith(".md") && !isIgnored(relativePath)) {
      files.push(fullPath);
    }
  }

  return files;
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

function readWhitelist(filePath) {
  const data = YAML.parse(fs.readFileSync(filePath, "utf8"));
  const terms = new Set();
  const values = new Set();
  const entries = Array.isArray(data.entries) ? data.entries : [];

  for (const entry of entries) {
    if (!entry.term || !entry.description) {
      throw new Error(`${filePath}: each whitelist entry must include term and description.`);
    }

    for (const value of entryValues(entry)) {
      terms.add(normalizeTerm(value));
      values.add(value);
    }
  }

  return { terms, entries, valuePattern: buildWhitelistValuePattern([...values]) };
}

function entryValues(entry) {
  return [entry.term, ...normalizeAliases(entry.aliases)].filter(Boolean);
}

function normalizeAliases(aliases) {
  if (!aliases) {
    return [];
  }

  return Array.isArray(aliases) ? aliases : [aliases];
}

function stripMarkdownNoise(text) {
  return text
    .replace(/^```[\s\S]*?^```/gm, blankPreserving)
    .replace(/`[^`\n]+`/g, blankPreserving)
    .replace(/^\[\^[^\]]+\]:.*$/gm, blankPreserving)
    .replace(/https?:\/\/[^\s)]+/g, blankPreserving)
    .replace(/mailto:[^\s)]+/g, blankPreserving)
    .replace(/<!--[\s\S]*?-->/g, blankPreserving)
    .replace(/^\[[^\]\n]+\]:\s+\S+.*$/gm, (value) => value.replace(/:\s+\S+.*$/, ": "))
    .replace(/\[[^\]\n]+\]\([^)]+\)/g, (value) => value.replace(/\([^)]+\)/g, ""));
}

function blankPreserving(value) {
  return value.replace(/[^\n]/g, " ");
}

function maskWhitelistValues(text, valuePattern) {
  if (!valuePattern) {
    return text;
  }

  return text.replace(valuePattern, blankPreserving);
}

function buildWhitelistValuePattern(values) {
  const alternatives = values
    .filter((value) => value.length > 1)
    .sort((left, right) => right.length - left.length)
    .map((value) => escapeRegExp(value).replace(/\s+/g, "\\s+"));

  if (alternatives.length === 0) {
    return null;
  }

  return new RegExp(`(^|[^A-Za-z0-9\\u30A0-\\u30FF])(${alternatives.join("|")})(?=$|[^A-Za-z0-9\\u30A0-\\u30FF])`, "giu");
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function checkTokens(relativePath, cleaned, pattern, shouldCheck) {
  for (const match of cleaned.matchAll(pattern)) {
    const word = match[0];
    if (!shouldCheck(word)) {
      continue;
    }

    const normalized = normalizeTerm(word);
    if (!whitelist.terms.has(normalized)) {
      const line = lineNumberAt(cleaned, match.index);
      violations.push(`${relativePath}:${line}: '${word}' is not in tools/lint/markdown-whitelist.yaml.`);
      const current = unknownWords.get(normalized) || { word, count: 0 };
      current.count += 1;
      unknownWords.set(normalized, current);
    }
  }
}

function shouldCheckEnglishToken(word) {
  return word.length > 1 && !/\d/.test(word);
}

function shouldCheckKatakanaToken(word) {
  const normalized = word.normalize("NFKC").replace(/[・ー._-]/g, "");
  return normalized.length > 1 && /[\u30A0-\u30FF]/u.test(normalized);
}

function normalizeTerm(term) {
  return term.normalize("NFKC").toLowerCase();
}

function lineNumberAt(text, index) {
  let line = 1;
  for (let cursor = 0; cursor < index; cursor += 1) {
    if (text.charCodeAt(cursor) === 10) {
      line += 1;
    }
  }

  return line;
}
