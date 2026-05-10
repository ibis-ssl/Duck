# TRACKER 開発引き継ぎメモ

【1. このチャットの目的】
- `Tracker` 開発の続きを、別チャットでもそのまま再開できる状態で引き継ぐこと。
- 最終的なゴールは、`/home/ibis/ssl/IbisDuck` 内の TRACKER 実装を `TRACKER-007` から継続し、task ごとに tracking・review・commit を伴う形で前進できるようにすること。
- 今回の handover は「TRACKER 開発だけ」を対象にし、SKILL リポジトリ側の作業は含めない。

【3. 背景・前提条件】
- 対象リポジトリは `/home/ibis/ssl/IbisDuck`。
- TRACKER の tracking ファイルは `Tracker/Tracker.Core/Design/tasks-status.md` と `Tracker/Tracker.Core/Design/phases-status.md`。
- 現在の作業 branch は `feat/tracker-004-contract-surface`。
- 現在の worktree には未コミット差分がある。特に `TRACKER-007` の実装差分が commit 前で止まっている。
- 現在の `git status --short --branch` は以下の状態。
- branch: `## feat/tracker-004-contract-surface`
- modified: `.gitignore`
- modified: `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- modified: `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
- untracked: `reports/task-tracker-001-review-20260501192139.md`
- untracked: `reports/task-tracker-002-review-20260501192140.md`
- untracked: `reports/task-tracker-003-review-20260501192141.md`
- untracked: `reports/task-tracker-004-review-20260501192142.md`
- untracked: `reports/task-tracker-007-evidence-20260509154458.md`
- untracked: `reports/task-tracker-007-evidence-r2-20260509155129.md`
- untracked: `reports/task-tracker-007-review-20260509154458.md`
- untracked: `reports/task-tracker-007-review-r2-20260509155129.md`
- `.gitignore` は今回の TRACKER-007 と無関係な変更として扱っていた。勝手に戻さないこと。
- standing rule として、TRACKER 側の実装は TDD ベースで進める方針だった。
- standing rule として、review は sub-agent に依頼する前提だった。
- standing rule として、reviewer sub-agent は `gpt-5.4` の `high` を使う前提だった。
- standing rule として、review 用 report は親が先に雛形を作り、sub-agent はその空欄だけを埋める前提だった。
- standing rule として、sub-agent に `codex exec` をやらせないことがユーザー指示として入っていた。
- standing rule として、sub-agent review は timeout で止めず、ユーザーが止めろと言うまで待つことが指示されていた。
- standing rule として、task を進めるときは tracking 更新と commit を意識して進める運用だった。
- TRACKER の phase 状態は engine フェーズ中で、integration / ui / verification / review は未着手。
- `tasks-status.md` の「現在のタスク」欄は `TRACKER-007` を `in_progress` としているが、タスク一覧の `TRACKER-007` 行は `pending` のままで不整合がある。これは次のチャットで明示的に直す必要がある。
- 最新の design doc 本文は `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`。

【4. ここまでの経緯】
- `TRACKER-001` を完了し、contract test 基盤と fixture を追加した。
- `TRACKER-002` を完了し、packet generator 契約テストを追加した。
- `TRACKER-003` を完了し、engine temporal contract tests を追加した。
- `TRACKER-004` を完了し、typed contract surface を整備した。
- `TRACKER-005` を完了し、`TrackerPacketGenerator` を実装した。
- `TRACKER-006` を完了し、`TrackerEngine` の reorder buffer と flush pipeline を実装した。commit は `d3e9ef7`。
- その後 `TRACKER-007` を開始し、tracking start 用 commit として `d144d4e` を作成した。commit message は `docs(tracker): start TRACKER-007 engine work`。
- `TRACKER-007` の初回実装後、evidence と review を回した。
- 初回 review report `reports/task-tracker-007-review-20260509154458.md` で `Medium` 指摘が 1 件出た。
- その指摘は「goal geometry change が `GeometryReset` 判定に含まれていない」という内容だった。
- 指摘内容に対応する follow-up 実装を、未コミット差分として `TrackerExecutionContracts.cs` と `TrackerEngineTemporalContractTests.cs` に入れた。
- follow-up では `goal width / depth` 変更でも reset 判定するよう `ShouldResetForGeometryChange` を拡張した。
- follow-up では profile switch 時に pending state を clear し、late cutoff を進める `ClearPendingStateAndAdvanceLateCutoff` を入れた。
- follow-up では `ResolvedBaseSettings` を実際の flush/settings 適用に使うよう変更した。
- follow-up では goal geometry reset と profile switch 後の pending buffered detections clear を拘束する test を追加した。
- その後の evidence report `reports/task-tracker-007-evidence-r2-20260509155129.md` では `TrackerEngineTemporalContractTests` が `Total: 16, Passed: 16, Failed: 0, Skipped: 0` だった。
- ただし final review report `reports/task-tracker-007-review-r2-20260509155129.md` は雛形だけがあり、中身が埋まっていない。
- つまり `TRACKER-007` は「実装差分あり」「evidence pass あり」だが、「final review の materialized report なし」「task 完了 commit なし」で止まっている。
- その後は TRACKER ではなく SKILL repo 側の作業に移ったため、TRACKER 側はこの中途状態のまま据え置かれている。

【5. 決定事項】
- TRACKER 側の現在の再開地点は `TRACKER-007`。
- `TRACKER-006` までは完了済みで commit 済み。
- `TRACKER-007` の実装自体はかなり進んでおり、未コミット差分として存在する。
- `TRACKER-007` の follow-up 実装で狙っている契約は次の通り。
- profile switch 時に `ResolvedBaseSettings` を使う。
- profile switch 時に pending detections を clear する。
- profile switch 時に late cutoff を適切に進める。
- geometry change による reset 判定に goal geometry も含める。
- `ProfileSwitched` / `GeometryReset` / `WorldFrameCommitted` の event publish 順を契約どおりにする。
- temporal contract suite の再証跡は pass 済みで、少なくとも `TrackerEngineTemporalContractTests` 16 件は通っている。
- `TRACKER-007` の次に進む前に、まず `TRACKER-007` 自体を review / tracking / commit の観点で閉じるべき。
- `.gitignore` と legacy review report 4 件は TRACKER-007 の本質ではないため、勝手に整理・破棄しない方がよい。
- current task は tracking 上 `TRACKER-007` で、phase は `engine` のままでよい。
- 今後も review は sub-agent で行う前提を維持する。
- 今後も reviewer は `gpt-5.4 high` を使う前提を維持する。
- 今後も report は親が先に作り、sub-agent は内容の穴埋めのみ行う前提を維持する。
- 今後も sub-agent に `codex exec` をさせない前提を維持する。

【6. 未解決事項・保留事項】
- `TRACKER-007` の final review report `reports/task-tracker-007-review-r2-20260509155129.md` が未記入のまま。
- `TRACKER-007` は review gate を抜けていないので、task 完了扱いにしてはいけない。
- `TRACKER-007` の差分は未コミットなので、commit を作る必要がある。
- `tasks-status.md` の「現在のタスク」欄とタスク一覧の `TRACKER-007` 行の status が不整合。
- `phases-status.md` は `TRACKER-007` in_progress 前提で整合しているが、task table 側が遅れている。
- `TRACKER-007` を commit する前に、review を再実行して no findings か、もしくは findings を materialize したうえで disposition を確定する必要がある。
- `TRACKER-007` で対象にした temporal suite 以外の broader test suite や integration 観点は未再検証。
- `.gitignore` と untracked legacy reports をどう扱うかは未整理だが、少なくとも TRACKER-007 commit に巻き込むべきではない。
- `TRACKER-007` を閉じた後、次 task は `TRACKER-008` の robot tracking / merge 実装へ進むのが自然だが、まだそこには着手していない。
- `tracker-architecture-plan.md` 自体は今回の follow-up で未編集のままなので、もし task 完了時に design sync が必要と判断されるなら確認が必要。
- current branch `feat/tracker-004-contract-surface` は作業継続用 branch としてそのまま使われているが、PR 状態までは未確認。

【7. 次のチャットで最初に依頼すべき内容】
- そのまま貼って使える依頼文:
- `/home/ibis/ssl/IbisDuck` の TRACKER 開発を再開してください。対象は TRACKER-007 のみです。branch は feat/tracker-004-contract-surface で、未コミット差分として Tracker/Tracker.Core/TrackerExecutionContracts.cs と Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs に follow-up 実装が入っています。task-tracker-007-evidence-r2-20260509155129.md では TrackerEngineTemporalContractTests 16件 pass 済みですが、task-tracker-007-review-r2-20260509155129.md は未記入で final review が閉じていません。まず TRACKER-007 の差分と既存 report を確認し、gpt-5.4 high の sub-agent review を proper にやり直して report を埋め、tasks-status / phases-status の不整合も直し、その後 TRACKER-007 を commit してください。.gitignore と legacy untracked review reports は今回の task とは無関係なので勝手に戻したり巻き込んだりしないでください。sub-agent に codex exec は使わせず、report は親が雛形を作って sub-agent に穴埋めさせてください。TDD と task ごとの tracking / review / commit 運用は維持してください。`

【8. 引き継ぎ本文】
- 以下を次のチャットの先頭にそのまま貼って使ってください。

```md
TRACKER 開発の引き継ぎです。対象は `/home/ibis/ssl/IbisDuck` のみで、SKILL repo 側の作業は無視してください。

目的は TRACKER 開発を `TRACKER-007` から再開することです。現在の branch は `feat/tracker-004-contract-surface` です。tracking 上の current task は `Tracker/Tracker.Core/Design/tasks-status.md` と `Tracker/Tracker.Core/Design/phases-status.md` で `TRACKER-007` / `engine` になっています。

ただし tracking には不整合があります。`tasks-status.md` の「現在のタスク」欄では `TRACKER-007` が `in_progress` ですが、同ファイルのタスク一覧では `TRACKER-007` 行の status が `pending` のままです。次の作業で直してください。

git 状態は次の通りです。
- modified: `.gitignore`
- modified: `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- modified: `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
- untracked: `reports/task-tracker-001-review-20260501192139.md`
- untracked: `reports/task-tracker-002-review-20260501192140.md`
- untracked: `reports/task-tracker-003-review-20260501192141.md`
- untracked: `reports/task-tracker-004-review-20260501192142.md`
- untracked: `reports/task-tracker-007-evidence-20260509154458.md`
- untracked: `reports/task-tracker-007-evidence-r2-20260509155129.md`
- untracked: `reports/task-tracker-007-review-20260509154458.md`
- untracked: `reports/task-tracker-007-review-r2-20260509155129.md`

`.gitignore` と legacy untracked review reports 4 件は今回の TRACKER-007 と無関係です。勝手に戻さないでください。TRACKER-007 の commit に巻き込まないでください。

完了済み commit は少なくとも以下です。
- `d3e9ef7` = `TRACKER-006` 完了
- `d144d4e` = `docs(tracker): start TRACKER-007 engine work`

`TRACKER-007` は未コミット差分として follow-up 実装が入っています。主な対象ファイルは以下の 2 つです。
- `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`

この follow-up は、初回 review `reports/task-tracker-007-review-20260509154458.md` の指摘に対応したものです。初回 review の指摘は「goal geometry change が `GeometryReset` 判定に含まれていない」という `Medium` finding でした。

その後の未コミット follow-up 実装では、少なくとも次の内容が差分に入っています。
- profile switch 時に `ResolvedBaseSettings` を使う
- profile switch 時に pending state を clear する
- profile switch 時に late cutoff を進める
- geometry reset 判定に goal width / goal depth の変化を含める
- `ProfileSwitched` / `GeometryReset` / `WorldFrameCommitted` の順序を拘束する temporal tests を追加する
- profile switch 後に old profile 側の pending buffered detections が committed frame に混ざらないことを test で拘束する

follow-up 後の evidence は `reports/task-tracker-007-evidence-r2-20260509155129.md` にあります。ここでは `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerEngineTemporalContractTests"` が成功しており、`Total: 16, Passed: 16, Failed: 0, Skipped: 0` です。

ただし final review は閉じていません。`reports/task-tracker-007-review-r2-20260509155129.md` は雛形だけがあり、中身が未記入です。つまり `TRACKER-007` は evidence pass 済みですが、review-enforcer 的にはまだ未完了です。

次にやるべきことは以下です。
1. `TRACKER-007` の現在差分と既存 report を確認する
2. 親が review report の雛形を作る
3. `gpt-5.4 high` の sub-agent に review を依頼する
4. sub-agent には report の空欄だけを埋めさせる
5. sub-agent に `codex exec` は使わせない
6. timeout しても user が止めるまで review sub-agent を止めない
7. no findings なら review report を完成させる
8. `tasks-status.md` の `TRACKER-007` status 不整合を直す
9. 必要なら `phases-status.md` も整合させる
10. `.gitignore` と無関係な untracked legacy reports を触らずに、`TRACKER-007` だけを commit する

standing rules は次の通りです。
- TDD で進める
- review は sub-agent に任せる
- reviewer model は `gpt-5.4` `high`
- report は親が雛形を作成し、sub-agent は穴埋めだけする
- normal path を壊さない限りは hold もあり得るが、今回は `TRACKER-007` を proper に閉じることが優先
- task ごとに tracking / review / commit を行う
- 無関係な差分や user change は戻さない

この引き継ぎを前提に、まずは `TRACKER-007` の review 再開と commit まで進めてください。
```
