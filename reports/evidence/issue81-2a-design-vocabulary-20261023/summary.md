# 2a6fd94 design vocabulary comparison

- Baseline: `9284640aaec4af59e44afe7687e4bf8a39494c48`; current source: `2a6fd94455978021cd995104751f4f09b0a28dfd` plus the documented uncommitted prose corrections.
- Scope: 12 added lines in the main Aspire design and 4 in the tracker comparison design.
- Preflight: existing Windows Node junction and module resolution exit 0; existing Python Sudachi runtime; all 9 config hashes matched. No install/config/whitelist changes.
- Final full-document CSpell: primary baseline 1/current 7 (new: Unix, Resource, Container, Generation, Boot, challenge); comparison baseline 43/current 45 (new: Unix, Resource, Container, Generation, Boot; removed alignment, cursor, timeline). Both return exit 1 because these findings remain.
- Final Sudachi targeted changed-lines checks return exit 1. Remaining additions are formal identifiers/protocol names (`RunId`, `ResourceId`, `ContainerId`, `Generation`, `BootId`, `challenge`) and Unix socket terminology. General prose was translated into Japanese; no whitelist/dictionary edits.
- This is a focused vocabulary check only; it does not establish whole-document lint success. Textlint/Yomiyasu were not run in this unit.
- Exact commands, stdout/stderr, config hashes, source/input hashes, and preceding generations are in `closure-final-v5.json` and `closure-final-v5-*` raw logs.
