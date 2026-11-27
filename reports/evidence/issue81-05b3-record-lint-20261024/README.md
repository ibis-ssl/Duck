# Issue81 05B3 status-fragment lint evidence (2026-10-24)

Scope: the three changed status rows only (tasks-status I81-05B3 and phases-status current value/current unit), represented in two non-empty Markdown inputs. Current and HEAD baseline fragments were extracted from the worktree/Git and byte-checked before lint. Repository target configuration excludes reports/**; the implementation/reviewer reports were not tested by these commands.

No source/product tests were run here. The checks used the existing Windows Node 24.20.0 and existing node_modules junction/config/rules. Sudachi used the existing /home/donabe/venvs/duck-pr73-r04-ff50682/bin/python venv and existing dictionary hash recorded in preflight-current.json; the missing-yaml attempt did not install anything.

Results: textlint exit 0; CSpell exit 1 (6 findings, with Driver x3 current-only and one baseline-only referee); Sudachi exit 1 (23 current token findings; additions/removals are enumerated in findings-current.json). These are not a passing document gate. The raw setup failures and baseline/current outputs are retained separately under raw/ and history/.

No whitelist or dictionary edits were made.
