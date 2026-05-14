# Tracker RuntimeHost / DebugHost 分離 引き継ぎ

## 目的

次のチャットで、`Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合の途中状態を、同じ前提で再開できるようにする。

最終的な狙いは、tracker / 将来 AutoRef mode の実時間処理を Web UI や diagnostics logging / replay から切り離し、`Tracker.RuntimeHost` を本番寄り headless 実行体、`Tracker.DebugHost` を debug / diagnostics / replay / capture viewer 用 Web host として整理すること。

## リポジトリ状態

- repository: `/home/ibis/ssl/IbisDuck`
- active branch: `feat/raw-vision-diagnostics-loop-isolation`
- current HEAD: `785827c`
- base 状態: `785827c` は PR #15 `Issue #10 Vision画面に分割表示とオーバーレイを追加する` の merge commit で、`main` / `origin/main` と一致していることをこのチャットで確認済み。
- 直近の作業は未コミット。

## 重要な workflow 前提

- `AGENTS.md` により、開発再開時は `development-orchestrator` を最初に実行する。
- IbisDuck では関連 Skill を優先し、調査・設計レビュー・実装・検証・review は原則 sub-agent に委譲する。親は manager として tracking、設計判断、report 確認、git 操作を担う。
- 設計書と report は日本語で書く。
- task tracking は後追いでまとめて更新しない。
- review / report / tracking / commit / PR ready は別 gate として扱う。
- 旧ログ互換は今回の優先事項ではない。最新 capture / 最新 logging 経路が最高性能を発揮することを最優先にする。
- `BreakingChanges` は作成不要。

## このチャットで確定した命名と責務

- `Tracker.RuntimeHost`
  - tracker と将来 AutoRef mode を同一 process で低遅延に動かす本番寄り headless 実行体。
  - SSL-Vision input、tracker operation loop、tracker packet publish を担う。
  - 将来 AutoRef mode を同一 process に入れる前提を残す。ただし AutoRef 実装自体は今回対象外。
  - Web UI、diagnostics replay UI、capture viewer、debug comparison panel、旧 logging 互換維持は担当しない。

- `Tracker.DebugHost`
  - 現 `Tracker.Server` の後継名。
  - Web UI、raw vision viewer、diagnostics、capture / replay、comparison を担当する debug 用 host。
  - tracker operation loop を主実行責務として持たない。
  - RuntimeHost または published tracker output を読む側に回る。

- `Tracker.Core`
  - tracker algorithm、contract、pure model、RuntimeHost / DebugHost の共通ロジックを置く。

## このチャットで実施したこと

1. `development-orchestrator` で再開し、AGENTS / skill / tracking / report を確認した。
2. PR #15 が merge 済みであることを確認した。
3. diagnostics overlay で `トラッカーなし` が ER-FORCE より遅れて見える問題について、単なる表示補正ではなく loop isolation として扱う方針にした。
4. sub-agent に tracking audit と design audit を委譲した。
   - `reports/issue-10-loop-isolation-tracking-audit-20260514151709.md`
   - `reports/issue-10-loop-isolation-design-audit-20260514151709.md`
5. design update sub-agent が `raw-vision-viewer-plan.md` へ loop isolation 方針を追記した。
   - `reports/issue-10-loop-isolation-design-update-20260514152339.md`
6. ユーザー確認により、単なる `Tracker.Server` 内 loop 分離ではなく、`Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針へ拡張した。
7. `Tracker/Design/` を canonical design root として新設し、設計資料を移動した。
8. active tracking を `Tracker/Design/tasks-status.md` / `Tracker/Design/phases-status.md` に統合した。
9. 新しい通し番号を `RUNTIME-HOST-001` から開始した。
10. `Tracker/Design/RuntimeHost/runtime-host-plan.md` を作成した。
11. `.gitignore` を更新し、旧 `Tracker/Tracker.Core/Design/Ref/` と新 `Tracker/Design/Core/Ref/` を ignore した。
12. ユーザーから「整理終わったら止まってください」と指示があり、整理後に実装・review へ進まず停止している。

## 現在の主要変更

### 新規 / 移動後の canonical design

- `Tracker/Design/tasks-status.md`
- `Tracker/Design/phases-status.md`
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`
- `Tracker/Design/Core/tracker-architecture-plan.md`
- `Tracker/Design/Core/tracker-core-engine-detail-design.md`
- `Tracker/Design/Core/tracker-history-000-038.md`
- `Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
- `Tracker/Design/Archive/Core/tasks-status.md`
- `Tracker/Design/Archive/Core/phases-status.md`
- `Tracker/Design/Archive/DebugHost/tasks-status.md`
- `Tracker/Design/Archive/DebugHost/phases-status.md`

### 旧パスからの削除扱い

- `Tracker/Tracker.Core/Design/*.md`
- `Tracker/Tracker.Server/Design/*.md`

`Tracker/Tracker.Core/Design/Ref` も `Tracker/Design/Core/Ref` へ移したが、`.gitignore` により `Tracker/Design/Core/Ref/` は ignored。外部参照リポジトリ本体は commit 対象ではない。

### その他変更

- `.gitignore`
  - `Tracker/Tracker.Core/Design/Ref/`
  - `Tracker/Design/Core/Ref/`
- `Tracker/Tracker.Server/README.md`
  - `Tracker/Tracker.Core/Design/Ref/ibis` 参照を `Tracker/Design/Core/Ref/ibis` へ更新。

## 現在の tracking

active tracking は `Tracker/Design/tasks-status.md` と `Tracker/Design/phases-status.md`。

現在のタスク:

- ID: `RUNTIME-HOST-001`
- Title: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了する
- Phase: `design`
- Status: `in-progress`

固定残タスク:

- `RUNTIME-HOST-001`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了する。
- `RUNTIME-HOST-002`: RuntimeHost / DebugHost 境界と diagnostics sample boundary の TDD contract を追加する。
- `RUNTIME-HOST-003`: `Tracker.DebugHost` への project / namespace / documentation rename と debug host 起動経路を実装する。
- `RUNTIME-HOST-004`: `Tracker.RuntimeHost` の headless scaffold と tracker operation loop 境界を実装する。
- `RUNTIME-HOST-005`: RuntimeHost / DebugHost 分離の validation、review、progress sync、PR ready を完了する。

## 確認済み

- `git diff --check` は通過済み。
- `Tracker/Tracker.Core/Design` / `Tracker/Tracker.Server/Design` 配下に通常設計ファイルは残っていない。
- `Tracker/Design/Core/Ref/` は `.gitignore` により `!!` ignored 表示になることを確認済み。
- `Tracker/Design/` 配下の active design には RuntimeHost / DebugHost の基本方針を反映済み。

## 未実施

- design review は未実施。
- build / test は未実施。
- commit / PR 作成は未実施。
- `Tracker.Server` project の実 rename は未実施。
- `Tracker.RuntimeHost` project scaffold は未実施。
- `RUNTIME-HOST-002` 以降の TDD / implementation は未着手。

## 注意点

- `Tracker/Design/Archive/` 配下には旧 tracking の履歴をそのまま残しているため、古い `Tracker.Server` / `Tracker/Tracker.Core/Design` 参照が残る。これは active design ではなく履歴として扱う。
- active design の用語は `Tracker.DebugHost` / `Tracker.RuntimeHost` へ寄せているが、実 code / project name はまだ `Tracker.Server` のまま。
- `Tracker/Design/Core/Ref/` は ignore 対象。外部参照リポジトリ本体を誤って commit しないこと。
- `reports/issue-10-*` と `reports/issue-10-loop-isolation-*` は今回の設計判断の根拠 report として未追跡のまま存在する。

## 次チャットへの依頼文

```text
/home/ibis/ssl/IbisDuck で作業を再開してください。まず AGENTS.md に従って development-orchestrator を実行し、関連 Skill を確認してください。

現在の branch は feat/raw-vision-diagnostics-loop-isolation、HEAD は 785827c です。前チャットでは RuntimeHost / DebugHost 分離方針の整理まで行い、ユーザー指示により実装・review へ進まず停止しています。

active tracking は旧 Tracker/Tracker.Core/Design や Tracker/Tracker.Server/Design ではなく、Tracker/Design/tasks-status.md と Tracker/Design/phases-status.md です。現在タスクは RUNTIME-HOST-001 です。

重要方針:
- Tracker.RuntimeHost は tracker と将来 AutoRef mode を同一 process で低遅延に動かす本番寄り headless 実行体。
- Tracker.DebugHost は現 Tracker.Server の後継名で、Web UI / diagnostics / replay / capture viewer に専念する debug host。
- tracker operation は Web 描画処理と diagnostics logging / replay 処理から切り離す。
- AutoRef 実装自体は今回対象外。
- 旧ログ互換は非要件。最新 capture / 最新 logging 経路の性能を最優先。
- BreakingChanges は作成不要。

まず RUNTIME-HOST-001 の整理差分を確認してください。特に Tracker/Design/ 配下への移動、Tracker/Design/RuntimeHost/runtime-host-plan.md、Tracker/Design/DebugHost/raw-vision-viewer-plan.md、Tracker/Design/Core/tracker-architecture-plan.md、.gitignore、Tracker/Tracker.Server/README.md を確認してください。

次に、設計レビューを gpt-5.5 high sub-agent に委譲し、review report を reports/ に残してください。review が通るまでは実装、build/test、commit/PR へ進まないでください。
```
