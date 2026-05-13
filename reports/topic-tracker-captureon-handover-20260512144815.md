# CaptureOn 比較ログ開発 引き継ぎメモ

作成日時: 2026-05-12 14:48 JST  
対象リポジトリ: `/home/ibis/ssl/IbisDuck`  
対象ブランチ: `feat/tracker-captureon-compare-log`  
対象PR: https://github.com/ibis-ssl/Duck/pull/9

## 目的

このチャットの目的は、ibis の Tracker 実行中に CaptureOn したとき、同じ CaptureOn session folder 配下へ ibis 自身を含む見えているすべての official `TrackerWrapperPacket` を保存し、後から ibis 詳細ログ、ibis official packet、他 tracker packet を diagnostics / replay / playback で比較できるようにすること。

snapshot は表示用データとして保持してよいが、それだけでは不十分。比較のための元データとして raw payload または raw payload を復元できる参照、source identity / role / label、timestamp、semantic summary を保持する必要がある。

## 重要な進行ルール

- 既存Skillに従う。開発再開時は `development-orchestrator` から入る。
- ユーザーは、この大きな開発では親が実装・調査をせず、親はサブエージェントの報告を読んで判断するマネージャーに徹することを要求している。
- サブエージェントはすべて `gpt-5.5 high` にする必要がある。
- レポートは日本語で、`reports/` 配下へ残す。
- PRは先に作成済み。こまめに push / 報告する。
- 設計 first / TDD 厳守。TDD過程をチャットや task plan に細かく載せる必要はないが、各機能タスクでは TDD、実装、検証、review、commit まで閉じる。
- `Tracker/Tracker.Core/Design/tasks-status.md` と `Tracker/Tracker.Core/Design/phases-status.md` は canonical。まとめて最後に更新せず、作業の進行に合わせて同期する。
- `TRACKER-048` 以降を追加すること自体が問題ではない。問題は「最後までの固定一覧を先に出さず、後から増えて見えること」。次の作業では固定一覧を先に tracking / design に反映する。
- `R-1` や `A/B/C/D` のような補助番号を task plan に載せない。機能ごとに `TRACKER-047` 以降の連番で扱う。
- 保守性改善の設計書と機能設計書は分ける。旧巨大ファイル分割などの話を最新機能設計書へ残さない。

## 確認済みの現在状態

### Git / PR

- `git status --short --branch`:
  - `## feat/tracker-captureon-compare-log...origin/feat/tracker-captureon-compare-log`
  - 未追跡: `reports/tracker-captureon-remaining-plan-reset-20260512142924.md`
  - この引き継ぎメモ作成後は、未追跡に `reports/topic-tracker-captureon-handover-20260512144815.md` も加わる。
- 最新HEAD: `58f9a43 docs(tracker): TRACKER-047実装レポートを記録する`
- PR #9:
  - URL: https://github.com/ibis-ssl/Duck/pull/9
  - state: open
  - draft: true
  - merged: false
  - mergeable: true
  - head: `58f9a4333468a09fc5afcae5b4b20c048f86af8f`
  - changed files: 77
  - PR本文は `TRACKER-042` 付近で止まっており、`TRACKER-043` から `TRACKER-047`、最新テスト結果、残gateを反映していない。

### Tracking

- `Tracker/Tracker.Core/Design/tasks-status.md`
  - 現在タスク: `TRACKER-047`
  - Status: `in_progress`
  - 内容: `tracker snapshot を diagnostics / replay / playback に統合する`
  - production実装と検証は完了。`gpt-5.5 high` review待ち。
  - focused test: `TrackerReplayIntegrationTddTests` 4 passed
  - 関連 focused test: 39 passed
  - full `Tracker.Tests`: 191 passed
- `Tracker/Tracker.Core/Design/phases-status.md`
  - 現在フェーズ: `comparison-logging`
  - 現在タスク: `TRACKER-047`
  - `TRACKER-040` から `TRACKER-046` は done。
  - `TRACKER-047` は review gate 未完了。

### 設計書

- `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md` は、現在の機能方針としては概ね正しい。
- ただし「後続タスクへの固定事項」が古く、`TRACKER-041` から `TRACKER-045` の旧対応が残っている。現在の tracking は `TRACKER-047` まで進んでいるため、次の実装前に design / tracking を同期する必要がある。
- 維持すべき設計結論:
  - 見えている official `TrackerWrapperPacket` はすべて保存する。
  - self / 3rdparty / unknown の判別は保存除外条件にしない。
  - ibis 自身の official packet と詳細ログは重複保持してよい。
  - `TrackerConnectionLib` を official tracker packet 傍受の第一候補統合点にする。
  - official tracker packet は multicast endpoint から届くため、明示有効化時だけ multicast group join する。
  - CaptureOn session folder 配下に packet capture、metadata、tracker diagnostics、render snapshots、tracker packet snapshot sidecar JSONL をまとめる。
  - 異なる CaptureOn タイミングのログは同じ階層へ横並びにせず、別 session folder へ分ける。
  - metadata には各 file relative path を記録する。
  - snapshot は表示用。比較用には raw payload または復元可能参照と raw由来 semantic summary が必要。
  - diagnostics / replay / playback は metadata relative path から snapshot sidecar を読み、source、role、timestamp、ball / robot count、skipped/error、raw payload restore state を表示または出力できる必要がある。

## ここまでの時系列

1. `TRACKER-040`
   - CaptureOn 比較ログ拡張の設計と tracking を追加。
   - `TrackerConnectionLib` を傍受統合点、`Tracker.Server` を CaptureOn session 統合層、`Tracker.Core` を傍受・保存対象外として整理。
   - PR #9 を draft として作成。
   - 設計 review は blocking findings なし。

2. `TRACKER-041`
   - self 除外前提を撤回。
   - 見えているすべての tracker packet を snapshot sidecar へ保存する方針へ設計修正。
   - source role / label / metadata は保存後の表示・比較用情報に変更。

3. `TRACKER-042`
   - all tracker 保存 contract を production 実装。
   - `MultiTrackerManager` の self early return を廃止し、own / external / unknown を state に保持。
   - focused 5 passed、full `Tracker.Tests` 163 passed、review blocking findings なし。

4. `TRACKER-043`
   - CaptureOn session folder と metadata relative path を実装。
   - `TrackerPacketSnapshotRecord` / `TrackerPacketSnapshotLogReader` を追加。
   - focused 5 passed、関連 13 passed、full 168 passed、review blocking findings なし。

5. `TRACKER-044`
   - CaptureOn 中の全 tracker packet snapshot 保存を実装。
   - sidecar JSONL writer、raw payload round-trip、semantic summary、metadata source集計を追加。
   - focused 7 passed、関連 30 passed、full 175 passed、review blocking findings なし。
   - review follow-up で semantic summary 値一致 assertion を追加。

6. `TRACKER-045`
   - live 外部 tracker 受信を snapshot writer へ接続。
   - `TrackerConnectionLibSnapshotRecorder` を追加し、live update から snapshot writer へ packet / source metadata を渡す。
   - focused 5 passed、関連 35 passed、full 180 passed、review blocking findings なし。

7. `TRACKER-046`
   - runtime 起動登録を実装。
   - `TrackerConnectionLibReceiverHostedService`、DI登録、unknown packet hardening、handler例外隔離を追加。
   - 初回 review で official multicast packet normal path blocker が出た。
   - review fix として multicast group join、runtime への multicast endpoint 受け渡し、receiver 明示 enable default off、CaptureOff中の非書き込み contract を追加。
   - multicast fix focused 4 passed、関連 42 passed、full 187 passed。
   - r2 review blocking findings なし。
   - socket abstraction / DI startup test は non-blocking hardening として残る。

8. `TRACKER-047`
   - `TrackerSnapshotReplayReader` を実装。
   - metadata relative path から session folder 内 sidecar / diagnostics log を解決し、own / external / unknown snapshot を timestamp順 replay input、表示用 snapshot、比較用 raw payload / semantic summary、nearest timestamp summary として読めるようにした。
   - focused 4 passed、関連 39 passed、full 191 passed。
   - production実装・検証は完了。
   - `gpt-5.5 high` review はまだ未実施。

9. ユーザー指摘による進め方リセット
   - 後から `TRACKER` が増えるように見える進め方が強く否定された。
   - 問題は `TRACKER-048` を作ることではなく、最初に最後までの一覧を出していないこと。
   - `R-1` 表記、`A/B/C/D` 表記、TDD過程の plan 掲載は避ける。
   - 次にやるべきことは、引き継ぎ資料作成。そのためこのメモを作成した。

## 未解決事項

- `TRACKER-047` の専用 `gpt-5.5 high` review が未完了。
- design / tracking の固定残タスク一覧がまだ同期されていない。
- `tracker-server-cli-ui-detail-design.md` の「後続タスクへの固定事項」が古い。
- `reports/tracker-captureon-remaining-plan-reset-20260512142924.md` が未追跡。
- PR #9 body が古い。
- diagnostics / replay / playback でユーザーが比較結果を確認できる最終露出が、現状の `TrackerSnapshotReplayReader` contract だけで十分かは未確定。設計の完了条件から見ると、`Tracker.CaptureReplay` CLI または diagnostics playback UI/view-model への user-visible 接続が必要になる可能性が高い。
- README / 運用証跡が未完了。
- PR ready / draft解除は未完了。
- socket abstraction、DI startup test、invalid raw payload direct append handling は non-blocking risk。今回PRへ入れるか後続issueへ送るか未判断。

## 次に採用すべき固定タスク一覧

以下は次チャットで design / tracking へ反映すべき固定一覧。新しい番号を後から生やすのではなく、この一覧を先に載せてから進める。

| ID | 機能・目的 | 完了条件 |
| --- | --- | --- |
| `TRACKER-047` | tracker snapshot replay reader を review まで閉じる | 既存実装に対する `gpt-5.5 high` review を実施し、blocking findings がないこと。finding が出た場合は修正、テスト、r2 review まで完了する。design / tracking の番号ずれ同期も次実装前の管理作業として同時に閉じる。 |
| `TRACKER-048` | diagnostics / replay / playback の比較表示・出力へ接続する | metadata relative path から snapshot sidecar を読み、source role / label、tracked timestamp、ball / robot count、raw payload restored、nearest timestamp summary を `Tracker.CaptureReplay` または diagnostics playback で確認できる。既存 capture / diagnostics / render snapshot 表示を壊さない。 |
| `TRACKER-049` | CaptureOn 比較ログの運用ドキュメントと確認手順を整える | README または運用メモに `Tracker:Receive:Enabled`、multicast endpoint、CaptureOn session folder、snapshot sidecar、replay / diagnostics 確認方法が載る。manual evidence と最終 tracking が揃う。 |
| `TRACKER-050` | PR #9 を ready 化する | PR本文を `TRACKER-040` から最終状態まで更新し、final validation、review evidence、risk整理、tracking同期を完了する。draft解除できる状態にする。 |

`TRACKER-051` 以降は、socket abstraction 等の hardening を今回PRへ含める判断が明示された場合、またはユーザー承認がある場合だけ追加する。通常は後続issueまたはPR riskへ退避する。

## 次チャットで最初にやること

1. `development-orchestrator` を開始し、ユーザーに「実装ではなく、まず design / tracking の固定一覧同期から入る」ことを確認する。
2. 親は実装・調査をしない。`gpt-5.5 high` のサブエージェントへ、design / tracking 同期を委譲する。
3. 同期対象:
   - `Tracker/Tracker.Core/Design/tasks-status.md`
   - `Tracker/Tracker.Core/Design/phases-status.md`
   - `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
   - 必要なら `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
   - `reports/tracker-captureon-remaining-plan-reset-20260512142924.md`
   - 新規 progress / sync report
4. 同期内容:
   - `TRACKER-047` から `TRACKER-050` の固定一覧を反映する。
   - 古い「後続タスクへの固定事項」を削除または更新する。
   - `R-1` 等の補助番号を tracking / design へ載せない。
   - 040-046 の done 状態と report references を失わない。
   - `TRACKER-051` 以降はユーザー承認なしで追加しないと明記する。
5. 同期後に commit / push する。
6. その後、`TRACKER-047` の `gpt-5.5 high` review を実施する。

## 次チャット用プロンプト

```text
$development-orchestrator

/home/ibis/ssl/IbisDuck の `feat/tracker-captureon-compare-log` / PR #9 を続行してください。

まず `reports/topic-tracker-captureon-handover-20260512144815.md` を読んでください。

重要:
- 親は実装・調査をしてはいけません。親はサブエージェントのレポートを読んで判断するマネージャーに徹してください。
- すべてのサブエージェントは `gpt-5.5 high` にしてください。
- レポートは日本語で `reports/` に作成してください。
- 設計 first / TDD 厳守です。
- `Tracker/Tracker.Core/Design/tasks-status.md` と `phases-status.md` を常に最新にしてください。
- まず実装ではなく、design / tracking の固定残タスク一覧同期から始めてください。
- 固定一覧は `TRACKER-047` から `TRACKER-050` です。`R-1` や `A/B/C/D` 表記を plan / tracking に載せないでください。
- `TRACKER-051` 以降はユーザー承認なしで追加しないでください。

最初の作業:
1. `Tracker/Tracker.Core/Design/tasks-status.md`
2. `Tracker/Tracker.Core/Design/phases-status.md`
3. `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
を、`TRACKER-047` から `TRACKER-050` の固定一覧へ同期してください。

その後、`TRACKER-047` の `gpt-5.5 high` review gate を閉じてください。
```

## 作成時に確認したコマンド

- `git status --short --branch`
- `git log --oneline -8`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tasks-status.md`
- `sed -n '1,220p' Tracker/Tracker.Core/Design/phases-status.md`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `sed -n '1,240p' reports/tracker-captureon-remaining-plan-reset-20260512142924.md`
- GitHub connector `_get_pr_info(repository_full_name="ibis-ssl/Duck", pr_number=9)`

## このメモの状態

- このメモは `handover-memo-writer` の成果物として `reports/topic-tracker-captureon-handover-20260512144815.md` に作成した。
- このメモ作成時点では、commit / push はまだ行っていない。
