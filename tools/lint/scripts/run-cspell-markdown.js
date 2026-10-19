"use strict";

const fs = require("fs");
const os = require("os");
const path = require("path");
const { spawnSync } = require("child_process");
const { createRequire } = require("module");

const root = process.cwd();
const requireFromRepo = createRequire(path.join(root, "package.json"));
const YAML = requireFromRepo("yaml");
const whitelistPath = path.join(root, "tools", "lint", "markdown-whitelist.yaml");
const baseConfigPath = path.join(root, "cspell.config.jsonc");
const baseConfig = JSON.parse(fs.readFileSync(baseConfigPath, "utf8"));

const whitelist = YAML.parse(fs.readFileSync(whitelistPath, "utf8"));
const entries = Array.isArray(whitelist.entries) ? whitelist.entries : [];
const values = entries.flatMap(entryValues).filter(Boolean);
const dictionaryTerms = values.filter((value) => !/[\s._:<>-]/.test(value));
const ignoredValues = values.filter((value) => /[\s._:<>-]/.test(value));
const markdownLinkTargetPatterns = [
  "/\\]\\(\\s*<?[^)\\s>]+>?(?:\\s+[^)]*)?\\s*\\)/g",
  "/^\\[(?!\\^)[^\\]\\n]+\\]:\\s+\\S+.*$/gm",
  "/\\[\\^[^\\]\\n]+\\]/g"
];
const yamlKeyPatterns = [
  "/^\\s*-?\\s*(version|entries|term|aliases|description):/gm"
];

if (values.length === 0) {
  throw new Error(`${whitelistPath}: entries must contain at least one term.`);
}

const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), "codexskill-cspell-"));
const dictionaryPath = path.join(tempDir, "markdown-whitelist.txt");
const configPath = path.join(tempDir, "cspell.config.json");

fs.writeFileSync(dictionaryPath, `${dictionaryTerms.join("\n")}\n`, "utf8");
fs.writeFileSync(
  configPath,
  JSON.stringify(
    {
      ...baseConfig,
      dictionaryDefinitions: [
        ...(baseConfig.dictionaryDefinitions || []),
        {
          name: "markdown-whitelist",
          path: dictionaryPath,
          addWords: true
        }
      ],
      dictionaries: [...(baseConfig.dictionaries || []), "markdown-whitelist"],
      ignoreRegExpList: [
        ...markdownLinkTargetPatterns,
        ...yamlKeyPatterns,
        ...ignoredValues.map((value) => whitelistValuePattern(value)),
        ...(baseConfig.ignoreRegExpList || [])
      ]
    },
    null,
    2
  ),
  "utf8"
);

const cspellCli = path.join(root, "node_modules", "cspell", "bin.mjs");
const result = spawnSync(process.execPath, [cspellCli, "--no-default-configuration", "--config", configPath, ...process.argv.slice(2)], {
  cwd: root,
  stdio: "inherit"
});

fs.rmSync(tempDir, { recursive: true, force: true });
process.exit(result.status === null ? 1 : result.status);

function entryValues(entry) {
  return [entry.term, ...normalizeAliases(entry.aliases)].filter(Boolean);
}

function normalizeAliases(aliases) {
  if (!aliases) {
    return [];
  }

  return Array.isArray(aliases) ? aliases : [aliases];
}

function whitelistValuePattern(value) {
  const escaped = escapeRegExp(value).replace(/\s+/g, "\\s+");
  if (/[A-Za-z0-9]/.test(value)) {
    const identifier = "A-Za-z0-9_";
    const connector = "._-";
    return `/(?<![${identifier}])(?<![${identifier}][${connector}])${escaped}(?![${identifier}])(?![${connector}][${identifier}])/giu`;
  }

  const boundary = "A-Za-z0-9_\\u30A0-\\u30FF\\u3400-\\u9FFF";
  return `/(?<![${boundary}])${escaped}(?![${boundary}])/giu`;
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
