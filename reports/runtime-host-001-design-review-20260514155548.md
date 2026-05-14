# Sub-agent実行レポート

## タスク

RUNTIME-HOST-001: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了する。

## sub-agentを使う理由

`review-enforcer` に従い、RUNTIME-HOST-001 の完了前に task-scoped の設計レビューを独立した `gpt-5.5 high` sub-agent で実施するため。

## 対象範囲

- `Tracker/Design/` への canonical design root 統合
- `Tracker/Design/tasks-status.md`
- `Tracker/Design/phases-status.md`
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`
- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `Tracker/Design/Core/tracker-architecture-plan.md`
- `Tracker/Design/Core/tracker-core-engine-detail-design.md`
- `Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
- `.gitignore`
- `Tracker/Tracker.Server/README.md`
- 旧 `Tracker/Tracker.Core/Design/` / `Tracker/Tracker.Server/Design/` からの移動・削除扱い

レビュー基準:

- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md` の書き方を参考に、目的、スコープ、非スコープ、責務境界、用語注釈が読みやすく一貫していること。
- `Tracker.RuntimeHost` は tracker と将来 AutoRef mode を同一 process で低遅延に動かす本番寄り headless 実行体として設計されていること。
- `Tracker.DebugHost` は現 `Tracker.Server` の後継名として Web UI / diagnostics / replay / capture viewer に専念し、tracker operation loop を主実行責務にしないこと。
- tracker operation loop が Web UI rendering と diagnostics logging / replay processing から切り離されること。
- AutoRef 実装自体、旧 logging 互換、`BreakingChanges` 作成が非スコープとして明確であること。
- active tracking が `Tracker/Design/tasks-status.md` / `Tracker/Design/phases-status.md` に移り、旧 tracking が archive 扱いで混同されないこと。

## 対象外

- 実装、build、test、commit、PR 作成
- `Tracker.Server` project rename の実作業
- `Tracker.RuntimeHost` project scaffold の実作業
- AutoRef logic 実装
- 旧 diagnostics logging 形式の完全互換設計
- `BreakingChanges` 作成

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `nl -ba reports/runtime-host-001-design-review-20260514155548.md`
- `git status --short`
- `git diff --stat`
- `git diff --find-renames --name-status`
- `find Tracker/Tracker.Core/Design Tracker/Tracker.Server/Design Tracker/Design -maxdepth 3 -type f | sort`
- `nl -ba Tracker/Design/tasks-status.md`
- `nl -ba Tracker/Design/phases-status.md`
- `nl -ba Tracker/Design/RuntimeHost/runtime-host-plan.md`
- `nl -ba Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `rg -n "RuntimeHost|DebugHost|Tracker\\.Server|Tracker/Design|AutoRef|BreakingChanges|logging|operation loop|diagnostics sample|canonical|Archive|旧" Tracker/Design .gitignore Tracker/Tracker.Server/README.md`
- `git diff -- Tracker/Tracker.Server/README.md .gitignore`
- `git diff --no-index --unified=3 <(git show HEAD:Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md) Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `git diff --no-index --unified=3 <(git show HEAD:Tracker/Tracker.Core/Design/tracker-architecture-plan.md) Tracker/Design/Core/tracker-architecture-plan.md`
- `rg -n "RAW-VISION-017|RAW-VISION-018|RAW-VISION-019|RAW-VISION-020|RUNTIME-HOST-002|RUNTIME-HOST-003|RUNTIME-HOST-004|RUNTIME-HOST-005" Tracker/Design/RuntimeHost Tracker/Design/DebugHost Tracker/Design/Core Tracker/Design/tasks-status.md Tracker/Design/phases-status.md`
- `nl -ba Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md | sed -n '9,23p;34,58p;120,132p;150,156p'`
- `nl -ba Tracker/Design/RuntimeHost/runtime-host-plan.md | sed -n '51,75p'`
- `nl -ba Tracker/Design/DebugHost/raw-vision-viewer-plan.md | sed -n '236,260p;318,321p'`

## 対象ファイル

- 確認対象:
  - `Tracker/Design/tasks-status.md`
  - `Tracker/Design/phases-status.md`
  - `Tracker/Design/RuntimeHost/runtime-host-plan.md`
  - `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
  - `Tracker/Design/Core/tracker-architecture-plan.md`
  - `Tracker/Design/Core/tracker-core-engine-detail-design.md`
  - `Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
  - `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
  - `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
  - `.gitignore`
  - `Tracker/Tracker.Server/README.md`
  - 旧 `Tracker/Tracker.Core/Design/` / `Tracker/Tracker.Server/Design/` の削除状態
- 編集したファイル:
  - `reports/runtime-host-001-design-review-20260514155548.md`

## 指摘事項

### Blocking normal-path problems

1. `Tracker/Design/DebugHost/raw-vision-viewer-plan.md:320` が、今後の TDD acceptance を `RAW-VISION-018` として記述している。active tracking は `Tracker/Design/tasks-status.md:25` で RuntimeHost / DebugHost 分離 scope の固定一覧を `RUNTIME-HOST-001` から `RUNTIME-HOST-005` に限定し、`RAW-VISION-*` / `TRACKER-*` を追加しないと明記している。また同じ内容の後続 TDD task は `Tracker/Design/tasks-status.md:27` と `Tracker/Design/tasks-status.md:44` の `RUNTIME-HOST-002` に定義済みである。canonical design 本体が将来作業を旧 `RAW-VISION-018` として指しているため、RUNTIME-HOST-002 以降の固定残タスクと矛盾し、RUNTIME-HOST-001 の exit criteria「fixed remaining list と矛盾しない」を満たせない。

2. 追加ユーザー指示では、旧 diagnostics logging / 旧 render snapshot sidecar 互換は守らなくてよく、最新 capture / 最新 logging 経路の性能最優先が明確に読める必要がある。一方で `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md:51` から `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md:55` は diagnostics log 側を「互換追加に留める」とし、既存 key=value 行と snapshot sidecar がない既存ログを引き続き読めることを維持要件として書いている。さらに `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md:122` から `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md:128` も既存 capture / 既存 diagnostics log の fallback を通常扱いにしている。`Tracker/Design/RuntimeHost/runtime-host-plan.md:58` と `Tracker/Design/DebugHost/raw-vision-viewer-plan.md:258` では旧 render snapshot sidecar 互換を非要件とし新規 logging / new capture の性能を優先しているため、DebugHost 詳細設計側にも「旧互換は best-effort / legacy 表示に留め、new capture / new logging の性能を犠牲にしない」という優先順位を明示しないと、実装者が旧互換維持を要求として扱う余地が残る。

### ユーザー確認が必要な capability gap

- なし。

### Non-blocking concern / hold

- なし。

## 結果

RUNTIME-HOST-001 の設計整理は、RuntimeHost / DebugHost の責務境界、AutoRef 実装非スコープ、BreakingChanges 非作成、`Tracker/Design/` canonical root への移動方針を概ね満たしている。追加ユーザー指示のうち、性能最優先と旧 render snapshot sidecar 互換非要件は `runtime-host-plan.md` と `raw-vision-viewer-plan.md` では明確に読める。

ただし blocking finding が 2 件あるため、このままでは RUNTIME-HOST-001 は完了扱いにできない。`Tracker/Design/DebugHost/raw-vision-viewer-plan.md:320` の future TDD acceptance を active tracking の `RUNTIME-HOST-002` に合わせて修正する必要がある。加えて `debug-host-cli-ui-detail-design.md` の旧 diagnostics log 互換維持に読める記述を、最新 capture / 最新 logging 性能を最優先し、旧互換は非要件または legacy / best-effort に留める方針へ揃える必要がある。

## リスク

- この review は設計差分の静的確認であり、build / test は対象外のため実行していない。
- `Tracker/Design/` は現時点で untracked として表示されるため、最終 packaging 時に旧 design path の削除と新 canonical root の追加が同一 commit に入ることを別途確認する必要がある。
- 旧 archive 配下には `RAW-VISION-*` / `TRACKER-*` の履歴が残る。active tracking 側では archive が active ではないと明記されているため blocker ではないが、参照時は archive と active root を混同しないこと。
