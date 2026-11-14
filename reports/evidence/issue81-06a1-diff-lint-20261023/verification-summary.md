# 06A1 focused document checks

Diff input: base `9284640aaec4af59e44afe7687e4bf8a39494c48`; 22 added source lines from two design files, tasks-status, and phases-status. Textlint exit 0. CSpell exit 1 with displayed cap 100; Sudachi exit 1, unknown-token listing captured, stderr summary capped at 200. Do not report these gates as passed. Nine configuration/dictionary-related source hashes are in `config-hashes.json`.

Yomiyasu strict compared baseline/current full design documents using the existing venv. `aspire-simulation-test-environment.md`: baseline/current exit 0, score 100, clean. `tracker-comparison-debug-design.md`: baseline/current exit 1, score 91; both have the same `excess_list` warning and two `negative_parallelism` info findings, unchanged classification/count, with line numbers shifted by the added section. See `yomiyasu/` raw outputs and source hashes.

Lint prep chronology: initial WSL symlink resolution failed; the mirror was repaired to a Windows Junction to the pre-existing node_modules cache. An initial Node preflight was run from the wrong cwd and failed with MODULE_NOT_FOUND; its raw JSON is retained. Correct-cwd preflight confirms `LinkType=Junction` and `require.resolve('textlint')`/`require.resolve('cspell')` exit 0. No lint rerun followed. No dictionary/config/dependency was changed.
