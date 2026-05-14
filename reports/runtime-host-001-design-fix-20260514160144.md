# Sub-agent実行レポート

## タスク

RUNTIME-HOST-001 design review blocking findings の修正。

## sub-agentを使う理由

`codex-delegation-executor` の design-document editing として、review finding に対する設計文書修正を bounded な sub-agent 作業に分離するため。

## 対象範囲

- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
  - `RAW-VISION-018` として残っている future TDD acceptance を active fixed task list の `RUNTIME-HOST-002` に合わせる。
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
  - 旧 diagnostics logging / 旧 render snapshot sidecar 互換を守る要求として読める記述を、legacy / best-effort に留める方針へ揃える。
  - 最新 capture / 最新 logging 経路の性能を最優先し、旧互換のために new path の性能や RuntimeHost / DebugHost 分離を犠牲にしないことを明確化する。

## 対象外

- 実装、build、test、commit、PR 作成
- active tracking の task 追加
- `BreakingChanges` 作成
- AutoRef logic の追加
- 設計ファイルの大規模再構成

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `rg -n "blocking|Blocking|BLOCK|RAW-VISION-018|RUNTIME-HOST-002|sidecar|legacy|旧|compat|互換|latest|diagnostics sample|sample tick" reports/runtime-host-001-design-review-20260514155548.md`
- `rg -n "RAW-VISION-018|RUNTIME-HOST-002|sidecar|legacy|旧|compat|互換|diagnostics sample|latest raw|latest tracker|render snapshot" Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `rg -n "diagnostics log|render snapshot|sidecar|legacy|旧|compat|互換|bounded|RuntimeHost|DebugHost|sample sidecar|logging|capture" Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `rg -n "RUNTIME-HOST-001|RUNTIME-HOST-002|RAW-VISION-018|fixed|Fixed|RuntimeHost" Tracker/Design/tasks-status.md Tracker/Design/phases-status.md Tracker/Design/RuntimeHost/runtime-host-plan.md`
- `sed -n '300,328p' Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `sed -n '1,92p' Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `sed -n '118,134p' Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `rg -n "RAW-VISION-018|RUNTIME-HOST-002|unsupported / degraded legacy|diagnostics sample sidecar|legacy / best-effort|旧 diagnostics log|旧 render snapshot sidecar|bounded lookup" Tracker/Design/DebugHost/raw-vision-viewer-plan.md Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `git status --short -- Tracker/Design/DebugHost/raw-vision-viewer-plan.md Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md reports/runtime-host-001-design-fix-20260514160144.md`

## 対象ファイル

- 変更:
  - `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
  - `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
  - `reports/runtime-host-001-design-fix-20260514160144.md`
- 参照:
  - `reports/runtime-host-001-design-review-20260514155548.md`
  - `Tracker/Design/tasks-status.md`
  - `Tracker/Design/phases-status.md`
  - `Tracker/Design/RuntimeHost/runtime-host-plan.md`

## 指摘事項

1. `raw-vision-viewer-plan.md` の future TDD acceptance が `RAW-VISION-018` のまま残っていたため、active fixed list の `RUNTIME-HOST-002` と矛盾していた。
2. `debug-host-cli-ui-detail-design.md` が旧 diagnostics log / 旧 render snapshot sidecar の互換維持を通常要件として読める状態で、新規 capture / 新規 logging の性能優先、bounded lookup、RuntimeHost / DebugHost 分離、diagnostics sample sidecar 優先が十分に明確ではなかった。

## 結果

- Finding 1 は、`RAW-VISION-018` の TDD acceptance 記述を `RUNTIME-HOST-002` の TDD contract へ更新し、diagnostics sample tick、latest raw snapshot、latest tracker snapshot、Diagnostics `Vision Input` の diagnostics sample sidecar 復元、RuntimeHost / DebugHost 境界、`UI render tick` snapshot contract、旧 render snapshot sidecar の unsupported / degraded legacy 扱いを同じ文脈で固定した。
- Finding 2 は、DebugHost 詳細設計の対象範囲、保存形式、diagnostics / replay / playback 節を更新し、旧 diagnostics log / 旧 render snapshot sidecar の完全互換は非要件であり legacy / best-effort / degraded 表示に留めること、新規 capture / 新規 logging の write cadence、bounded lookup、RuntimeHost / DebugHost 分離、diagnostics sample sidecar を犠牲にしないことを明確化した。
- `rg` で対象ファイル内の `RAW-VISION-018`、`RUNTIME-HOST-002`、legacy / best-effort、diagnostics sample sidecar、bounded lookup 関連の記述を確認した。

## リスク

- 設計文書編集のみであり、実装、build、test は対象外。
- `Tracker/Design/` 配下は現時点の worktree では untracked として表示されるため、親 workflow 側の packaging 時に canonical design root の追加状態を確認する必要がある。
- 旧形式の実際の degraded 表示文言や diagnostics sample sidecar の具体 schema 名は、後続の `RUNTIME-HOST-002` 以降の TDD / implementation task で固定する。
